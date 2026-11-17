using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Rotas do técnico na OS (16/11/2026): papel fixo no servidor, obra do grupo facial vinda da OS e a regra de
// quem pode assinar conferida ANTES de gastar o desafio de uso único do leitor.
public class RegistrarAssinaturaTecnicoOsCommandsTests
{
    private sealed class AutenticacaoBiometriaFalsa : IAutenticacaoBiometriaLocalService
    {
        public int Chamadas { get; private set; }
        public bool? PermitiuOutraObra { get; private set; }
        public Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
            Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, LeituraBiometricaAssinada? leitura, CancellationToken ct,
            bool permitirInstrutorDeOutraObra = false)
        {
            Chamadas++;
            PermitiuOutraObra = permitirInstrutorDeOutraObra;
            return Task.FromResult(new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.Biometria));
        }
    }

    private sealed class AutenticacaoFacialFalsa : IAutenticacaoFacialService
    {
        public Guid? ObraConsultada { get; private set; }
        public Guid IdentificadoId { get; set; }
        public Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) => throw new NotSupportedException();
        public Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = true)
        {
            ObraConsultada = obraId;
            return Task.FromResult(new ResultadoIdentificacaoFacial(true,
                new ResultadoAutenticacaoAssinatura(IdentificadoId, MetodoAutenticacaoAssinatura.ReconhecimentoFacial), null, 90));
        }
    }

    private sealed class RegistradorFalso : IRegistradorAssinaturaService
    {
        public PapelAssinatura? PapelRecebido { get; private set; }
        public Task<DocumentoSignatarioDto> RegistrarAsync(Guid documentoAssinaturaId, ResultadoAutenticacaoAssinatura resultado, string? ipAddress, CancellationToken ct,
            byte[]? fotoEvidenciaConteudo = null, string? fotoEvidenciaContentType = null, PapelAssinatura? papel = null, LocalizacaoAssinatura? localizacao = null, string? userAgent = null)
        {
            PapelRecebido = papel;
            return Task.FromResult(new DocumentoSignatarioDto(resultado.TrabalhadorId, "Técnico", resultado.Metodo, DateTime.UtcNow, ipAddress));
        }
    }

    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, DocumentoAssinatura Documento, Obra Obra, Trabalhador Tecnico, Trabalhador Colega)> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        db.Obras.Add(obra);
        Trabalhador Novo(string nome, string funcao) => new()
        {
            ObraId = obra.Id, Nome = nome, Cpf = Guid.NewGuid().ToString("N")[..11], DataAdmissao = DateTime.UtcNow, Funcao = new Funcao { Nome = funcao },
        };
        var funcionario = Novo("Pedro Pedreiro", "Pedreiro");
        var tecnico = Novo("Tânia Técnica", "Técnico de Segurança do Trabalho");
        var colega = Novo("Carlos Carpinteiro", "Carpinteiro");
        db.Trabalhadores.AddRange(funcionario, tecnico, colega);
        var os = new OrdemServico { TrabalhadorId = funcionario.Id, ObraId = obra.Id, Numero = "OS-2026-0002", EmitidaEm = DateTime.UtcNow, ConteudoJson = "{}" };
        db.OrdensServico.Add(os);
        var documento = new DocumentoAssinatura { EntidadeTipo = OrdemServico.EntidadeTipoAssinatura, EntidadeId = os.Id, ObraId = obra.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        return (db, documento, obra, tecnico, colega);
    }

    [Fact]
    public async Task Digital_RegistraComOPapelFixo_ENaoPermiteOutraObraForaDaIntegracao()
    {
        var (db, documento, _, tecnico, _) = await PrepararAsync();
        var auth = new AutenticacaoBiometriaFalsa();
        var registrador = new RegistradorFalso();

        await new RegistrarAssinaturaTecnicoOsBiometriaLocalCommandHandler(db, auth, registrador).Handle(
            new RegistrarAssinaturaTecnicoOsBiometriaLocalCommand(documento.Id, Guid.NewGuid(), "x", tecnico.Id, 90), default);

        Assert.Equal(PapelAssinatura.TecnicoSegurancaOs, registrador.PapelRecebido);
        Assert.Equal(1, auth.Chamadas);
        Assert.False(auth.PermitiuOutraObra);
    }

    [Fact]
    public async Task Digital_DeQuemNaoPodeSerTecnico_ERecusadaAntesDeLerOLeitor()
    {
        var (db, documento, _, _, colega) = await PrepararAsync();
        var auth = new AutenticacaoBiometriaFalsa();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RegistrarAssinaturaTecnicoOsBiometriaLocalCommandHandler(db, auth, new RegistradorFalso()).Handle(
                new RegistrarAssinaturaTecnicoOsBiometriaLocalCommand(documento.Id, Guid.NewGuid(), "x", colega.Id, 90), default));

        Assert.Equal("A digital lida não é de um técnico ou engenheiro de segurança desta obra.", erro.Message);
        Assert.Equal(0, auth.Chamadas);
    }

    [Fact]
    public async Task Documento_QueNaoEDeOs_ENaoEncontrado()
    {
        var (db, _, obra, tecnico, _) = await PrepararAsync();
        var outro = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = Guid.NewGuid(), ObraId = obra.Id };
        db.DocumentosAssinatura.Add(outro);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new RegistrarAssinaturaTecnicoOsBiometriaLocalCommandHandler(db, new AutenticacaoBiometriaFalsa(), new RegistradorFalso()).Handle(
                new RegistrarAssinaturaTecnicoOsBiometriaLocalCommand(outro.Id, Guid.NewGuid(), "x", tecnico.Id, 90), default));
    }

    [Fact]
    public async Task Facial_ForaDaIntegracao_UsaOGrupoDaObraDaOs_ERegistraComOPapelFixo()
    {
        var (db, documento, obra, tecnico, _) = await PrepararAsync();
        var facial = new AutenticacaoFacialFalsa { IdentificadoId = tecnico.Id };
        var registrador = new RegistradorFalso();

        await new RegistrarAssinaturaTecnicoOsFacialCommandHandler(db, facial, registrador).Handle(
            new RegistrarAssinaturaTecnicoOsFacialCommand(documento.Id, new byte[] { 1, 2, 3 }), default);

        Assert.Equal(obra.Id, facial.ObraConsultada);
        Assert.Equal(PapelAssinatura.TecnicoSegurancaOs, registrador.PapelRecebido);
    }

    // OS dentro de uma integração em andamento (etapa da OS aberta), conduzida por um instrutor de OUTRA obra.
    private static async Task<Trabalhador> DeixarOsDentroDeIntegracaoAsync(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext db, Obra obraDaOs)
    {
        var outraObra = new Obra { Codigo = "OB2", Nome = "Outra obra" };
        db.Obras.Add(outraObra);
        var instrutor = new Trabalhador
        {
            ObraId = outraObra.Id, Nome = "Ivo Instrutor", Cpf = Guid.NewGuid().ToString("N")[..11], DataAdmissao = DateTime.UtcNow, Funcao = new Funcao { Nome = "Instrutor" },
        };
        db.Trabalhadores.Add(instrutor);
        var os = await db.OrdensServico.IgnoreQueryFilters().SingleAsync();
        var atendimento = new IntegracaoAtendimento
        {
            ObraId = obraDaOs.Id, Numero = "INT-2026-0001", InstrutorTrabalhadorId = instrutor.Id, IniciadaEmUtc = DateTime.UtcNow,
            Status = StatusIntegracaoAtendimento.EmAndamento,
        };
        db.IntegracoesAtendimento.Add(atendimento);
        db.IntegracaoEtapas.Add(new IntegracaoEtapa
        {
            ObraId = obraDaOs.Id, IntegracaoAtendimentoId = atendimento.Id, Ordem = 5, Tipo = TipoEtapaIntegracao.OrdemServico,
            Status = StatusEtapaIntegracao.EmAndamento, IniciadaEmUtc = DateTime.UtcNow,
        });
        db.ParticipantesIntegracao.Add(new ParticipanteIntegracao
        {
            ObraId = obraDaOs.Id, IntegracaoAtendimentoId = atendimento.Id, TrabalhadorId = os.TrabalhadorId, FuncaoId = Guid.NewGuid(), Nome = "Pedro Pedreiro",
        });
        await db.SaveChangesAsync();
        return instrutor;
    }

    [Fact]
    public async Task Facial_DentroDaIntegracao_InstrutorDeOutraObra_BuscaNoGrupoDaObraDoInstrutor()
    {
        var (db, documento, obra, _, _) = await PrepararAsync();
        var instrutor = await DeixarOsDentroDeIntegracaoAsync(db, obra);
        var facial = new AutenticacaoFacialFalsa { IdentificadoId = instrutor.Id };
        var registrador = new RegistradorFalso();

        await new RegistrarAssinaturaTecnicoOsFacialCommandHandler(db, facial, registrador).Handle(
            new RegistrarAssinaturaTecnicoOsFacialCommand(documento.Id, new byte[] { 1, 2, 3 }), default);

        Assert.Equal(instrutor.ObraId, facial.ObraConsultada);
        Assert.NotEqual(obra.Id, facial.ObraConsultada);
        Assert.Equal(PapelAssinatura.TecnicoSegurancaOs, registrador.PapelRecebido);
    }

    [Fact]
    public async Task Facial_DentroDaIntegracao_RostoQueNaoEDoInstrutor_Recusa()
    {
        var (db, documento, obra, tecnico, _) = await PrepararAsync();
        await DeixarOsDentroDeIntegracaoAsync(db, obra);
        var facial = new AutenticacaoFacialFalsa { IdentificadoId = tecnico.Id };
        var registrador = new RegistradorFalso();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RegistrarAssinaturaTecnicoOsFacialCommandHandler(db, facial, registrador).Handle(
                new RegistrarAssinaturaTecnicoOsFacialCommand(documento.Id, new byte[] { 1, 2, 3 }), default));

        Assert.Equal(TecnicoOrdemServicoRegra.MensagemNaoEhInstrutorFace, erro.Message);
        Assert.Null(registrador.PapelRecebido);
    }
}
