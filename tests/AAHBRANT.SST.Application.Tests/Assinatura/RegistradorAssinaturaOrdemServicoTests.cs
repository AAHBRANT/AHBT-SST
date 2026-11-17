using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Ordem de Serviço: o funcionário assina primeiro (digital/facial) e, depois dele, o técnico — desde 16/11/2026
// SÓ por biometria, com o papel TecnicoSegurancaOs. Na integração só o instrutor dela; fora, técnico/engenheiro
// de segurança da própria obra da OS. A assinatura por login (sessão) é recusada para OS novas; OS antigas já
// assinadas por login continuam válidas.
public class RegistradorAssinaturaOrdemServicoTests
{
    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed record Cenario(
        SstDbContext Db, DocumentoAssinatura Documento, OrdemServico Os, Obra ObraA, Obra ObraB,
        Trabalhador Funcionario, Trabalhador Tecnico, Trabalhador Engenheiro, Trabalhador Colega, Trabalhador TecnicoOutraObra, Trabalhador Instrutor);

    private static async Task<Cenario> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obraA = new Obra { Codigo = "OB1", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OB2", Nome = "Obra B" };
        db.Obras.AddRange(obraA, obraB);
        Trabalhador Novo(Obra obra, string nome, string funcao) => new()
        {
            ObraId = obra.Id, Nome = nome, Cpf = Guid.NewGuid().ToString("N")[..11], DataAdmissao = DateTime.UtcNow, Funcao = new Funcao { Nome = funcao },
        };
        var funcionario = Novo(obraA, "Pedro Pedreiro", "Pedreiro");
        var tecnico = Novo(obraA, "Tânia Técnica", "Técnico de Segurança do Trabalho");
        var engenheiro = Novo(obraA, "Eva Engenheira", "Engenheiro de Segurança do Trabalho");
        var colega = Novo(obraA, "Carlos Carpinteiro", "Carpinteiro");
        var tecnicoOutraObra = Novo(obraB, "Tiago Técnico", "Técnico de Segurança do Trabalho");
        // Instrutor da integração: de outra obra e sem função de segurança (só o vínculo com a integração importa).
        var instrutor = Novo(obraB, "Irene Instrutora", "Instrutor");
        db.Trabalhadores.AddRange(funcionario, tecnico, engenheiro, colega, tecnicoOutraObra, instrutor);
        var os = new OrdemServico { TrabalhadorId = funcionario.Id, ObraId = obraA.Id, Numero = "OS-2026-0001", EmitidaEm = DateTime.UtcNow, ConteudoJson = "{}" };
        db.OrdensServico.Add(os);
        var documento = new DocumentoAssinatura { EntidadeTipo = OrdemServico.EntidadeTipoAssinatura, EntidadeId = os.Id, ObraId = obraA.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        return new Cenario(db, documento, os, obraA, obraB, funcionario, tecnico, engenheiro, colega, tecnicoOutraObra, instrutor);
    }

    // Põe a OS dentro de uma integração da obra, com a etapa da OS aberta e o funcionário como participante.
    private static async Task IniciarIntegracaoAsync(Cenario c, Guid instrutorId, StatusIntegracaoAtendimento status = StatusIntegracaoAtendimento.EmAndamento)
    {
        var at = new IntegracaoAtendimento
        {
            ObraId = c.ObraA.Id, Numero = "INT-2026-0001", InstrutorTrabalhadorId = instrutorId, Status = status, IniciadaEmUtc = DateTime.UtcNow.AddHours(-2),
        };
        c.Db.IntegracoesAtendimento.Add(at);
        c.Db.IntegracaoEtapas.Add(new IntegracaoEtapa
        {
            ObraId = c.ObraA.Id, IntegracaoAtendimentoId = at.Id, Ordem = 5, Tipo = TipoEtapaIntegracao.OrdemServico, Status = StatusEtapaIntegracao.EmAndamento,
        });
        c.Db.ParticipantesIntegracao.Add(new ParticipanteIntegracao
        {
            ObraId = c.ObraA.Id, IntegracaoAtendimentoId = at.Id, TrabalhadorId = c.Funcionario.Id, FuncaoId = c.Funcionario.FuncaoId, FuncaoNome = "Pedreiro", Nome = c.Funcionario.Nome,
        });
        await c.Db.SaveChangesAsync();
    }

    private static RegistradorAssinaturaService Registrador(SstDbContext db) => new(db, new AuditoriaFalsa());

    private static Task FuncionarioAssinaAsync(Cenario c) =>
        Registrador(c.Db).RegistrarAsync(c.Documento.Id, new ResultadoAutenticacaoAssinatura(c.Funcionario.Id, MetodoAutenticacaoAssinatura.Biometria), null, default);

    private static Task<DocumentoSignatarioDto> TecnicoAssinaAsync(Cenario c, Trabalhador quem, MetodoAutenticacaoAssinatura metodo = MetodoAutenticacaoAssinatura.Biometria) =>
        Registrador(c.Db).RegistrarAsync(c.Documento.Id, new ResultadoAutenticacaoAssinatura(quem.Id, metodo), null, default, papel: PapelAssinatura.TecnicoSegurancaOs);

    [Fact]
    public void PapelNovo_TemValor8_ENaoColideComOsExistentes()
    {
        Assert.Equal(8, (int)PapelAssinatura.TecnicoSegurancaOs);
        var valores = Enum.GetValues<PapelAssinatura>().Select(p => (int)p).ToList();
        Assert.Equal(valores.Count, valores.Distinct().Count());
    }

    [Theory]
    [InlineData(MetodoAutenticacaoAssinatura.Biometria)]
    [InlineData(MetodoAutenticacaoAssinatura.ReconhecimentoFacial)]
    public async Task Tecnico_PorBiometria_AposOFuncionario_Registra(MetodoAutenticacaoAssinatura metodo)
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);

        await TecnicoAssinaAsync(c, c.Tecnico, metodo);

        var assinaturas = c.Db.DocumentoSignatarios.Where(s => s.DocumentoAssinaturaId == c.Documento.Id).ToList();
        Assert.Equal(2, assinaturas.Count);
        var doTecnico = Assert.Single(assinaturas, s => s.TrabalhadorId == c.Tecnico.Id);
        Assert.Equal(PapelAssinatura.TecnicoSegurancaOs, doTecnico.Papel);
        Assert.Equal(metodo, doTecnico.MetodoAutenticacao);
    }

    [Fact]
    public async Task EngenheiroDeSegurancaDaObra_ForaDaIntegracao_Registra()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);

        await TecnicoAssinaAsync(c, c.Engenheiro);

        Assert.Contains(c.Db.DocumentoSignatarios, s => s.TrabalhadorId == c.Engenheiro.Id && s.Papel == PapelAssinatura.TecnicoSegurancaOs);
    }

    [Fact]
    public async Task Tecnico_AntesDoFuncionario_ERecusado()
    {
        var c = await PrepararAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Tecnico));
        Assert.Contains("funcionário assina primeiro", erro.Message);
    }

    [Fact]
    public async Task Funcionario_NaoPodeSerOTecnicoDaPropriaOs()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Funcionario));
        Assert.Contains("próprio funcionário", erro.Message);
    }

    [Fact]
    public async Task FuncaoErrada_ForaDaIntegracao_ERecusada_ComAMensagemDaDigital()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Colega));
        Assert.Equal("A digital lida não é de um técnico ou engenheiro de segurança desta obra.", erro.Message);
    }

    [Fact]
    public async Task FuncaoErrada_PorFace_UsaAMensagemDaFace()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Colega, MetodoAutenticacaoAssinatura.ReconhecimentoFacial));
        Assert.Equal("A face lida não é de um técnico ou engenheiro de segurança desta obra.", erro.Message);
    }

    [Fact]
    public async Task TecnicoDeOutraObra_ForaDaIntegracao_ERecusado()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.TecnicoOutraObra));
        Assert.Contains("técnico ou engenheiro de segurança desta obra", erro.Message);
        Assert.DoesNotContain(c.Db.DocumentoSignatarios, s => s.TrabalhadorId == c.TecnicoOutraObra.Id);
    }

    [Fact]
    public async Task TecnicoDesligado_ERecusado()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);
        c.Tecnico.Situacao = SituacaoTrabalhador.Desligado;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Tecnico));
    }

    [Fact]
    public async Task NaIntegracao_SoOInstrutorDaIntegracaoAssina()
    {
        var c = await PrepararAsync();
        await IniciarIntegracaoAsync(c, c.Instrutor.Id);
        await FuncionarioAssinaAsync(c);

        // O técnico da obra (que valeria fora da integração) é recusado aqui.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Tecnico));
        Assert.Equal("A digital lida não é a do instrutor desta integração.", erro.Message);
        var erroFace = await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Engenheiro, MetodoAutenticacaoAssinatura.ReconhecimentoFacial));
        Assert.Equal("A face lida não é a do instrutor desta integração.", erroFace.Message);

        // O instrutor, mesmo de outra obra e sem função de segurança, assina.
        await TecnicoAssinaAsync(c, c.Instrutor);
        Assert.Contains(c.Db.DocumentoSignatarios, s => s.TrabalhadorId == c.Instrutor.Id && s.Papel == PapelAssinatura.TecnicoSegurancaOs);
    }

    [Fact]
    public async Task IntegracaoJaConcluida_NaoPrendeAOsAoInstrutor_ValeARegraDaObra()
    {
        var c = await PrepararAsync();
        await IniciarIntegracaoAsync(c, c.Instrutor.Id, StatusIntegracaoAtendimento.Concluido);
        await FuncionarioAssinaAsync(c);

        await TecnicoAssinaAsync(c, c.Tecnico);

        Assert.Contains(c.Db.DocumentoSignatarios, s => s.TrabalhadorId == c.Tecnico.Id && s.Papel == PapelAssinatura.TecnicoSegurancaOs);
    }

    [Fact]
    public async Task SegundaAssinaturaDeTecnico_ERecusada()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);
        await TecnicoAssinaAsync(c, c.Tecnico);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Engenheiro));
        Assert.Contains("já tem a assinatura do técnico", erro.Message);
    }

    [Fact]
    public async Task Login_ParaOsNova_ERecusado_ComMensagemClara()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Registrador(c.Db).RegistrarAsync(
            c.Documento.Id, new ResultadoAutenticacaoAssinatura(c.Tecnico.Id, MetodoAutenticacaoAssinatura.SessaoLogada), null, default));
        Assert.Contains("só por digital ou reconhecimento facial", erro.Message);
        Assert.DoesNotContain(c.Db.DocumentoSignatarios, s => s.TrabalhadorId == c.Tecnico.Id);
    }

    [Fact]
    public async Task PapelDoTecnico_ComLogin_ERecusado()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);

        await Assert.ThrowsAsync<InvalidOperationException>(() => TecnicoAssinaAsync(c, c.Tecnico, MetodoAutenticacaoAssinatura.SessaoLogada));
    }

    [Fact]
    public async Task BiometriaDeOutraPessoa_NaRotaComum_ERecusada()
    {
        var c = await PrepararAsync();

        // Sem o papel, a biometria só vale para o dono da OS: o técnico não consegue assinar pela rota genérica.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Registrador(c.Db).RegistrarAsync(
            c.Documento.Id, new ResultadoAutenticacaoAssinatura(c.Colega.Id, MetodoAutenticacaoAssinatura.Biometria), null, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Registrador(c.Db).RegistrarAsync(
            c.Documento.Id, new ResultadoAutenticacaoAssinatura(c.Tecnico.Id, MetodoAutenticacaoAssinatura.Biometria), null, default));
    }

    [Fact]
    public async Task OsAntiga_AssinadaPorLogin_ContinuaValida()
    {
        var c = await PrepararAsync();
        // Estado gravado antes de 16/11: funcionário por digital e técnico por sessão logada, sem papel.
        c.Db.DocumentoSignatarios.AddRange(
            new DocumentoSignatario { DocumentoAssinaturaId = c.Documento.Id, TrabalhadorId = c.Funcionario.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = DateTime.UtcNow.AddMinutes(-5) },
            new DocumentoSignatario { DocumentoAssinaturaId = c.Documento.Id, TrabalhadorId = c.Tecnico.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.SessaoLogada, AssinadoEm = DateTime.UtcNow.AddMinutes(-4) });
        await c.Db.SaveChangesAsync();

        var os = Assert.Single(await new ListarOrdensServicoQueryHandler(c.Db).Handle(new ListarOrdensServicoQuery(c.Funcionario.Id), default));

        Assert.Equal(SituacaoOrdemServico.Valida, os.Situacao);
        Assert.Equal(c.Tecnico.Nome, os.Tecnico?.Nome);
        Assert.Equal(c.Funcionario.Nome, os.Funcionario?.Nome);
    }

    [Fact]
    public async Task OsNova_AssinadaPorBiometria_FicaValida_ELeituraSeparaFuncionarioETecnico()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);
        var aguardando = Assert.Single(await new ListarOrdensServicoQueryHandler(c.Db).Handle(new ListarOrdensServicoQuery(c.Funcionario.Id), default));
        Assert.Equal(SituacaoOrdemServico.AguardandoTecnico, aguardando.Situacao);

        await TecnicoAssinaAsync(c, c.Tecnico);

        var os = Assert.Single(await new ListarOrdensServicoQueryHandler(c.Db).Handle(new ListarOrdensServicoQuery(c.Funcionario.Id), default));
        Assert.Equal(SituacaoOrdemServico.Valida, os.Situacao);
        Assert.Equal(c.Tecnico.Nome, os.Tecnico?.Nome);
        Assert.Equal(MetodoAutenticacaoAssinatura.Biometria, os.Tecnico?.Metodo);
        Assert.Equal(c.Funcionario.Nome, os.Funcionario?.Nome);
    }

    [Fact]
    public async Task FinalizarDocumento_AceitaOPapelNovoComoAssinaturaDoTecnico()
    {
        var c = await PrepararAsync();
        await FuncionarioAssinaAsync(c);
        var pdf = new byte[] { 1, 2, 3 };
        var handler = new FinalizarDocumentoCommandHandler(c.Db, new AuditoriaFalsa(),
            new MediatorSimulado(r => r is ExportarOrdemServicoPdfQuery ? pdf : null), new PdfFalso(), new QrFalso());

        // Só o funcionário assinou: não finaliza.
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new FinalizarDocumentoCommand(c.Documento.Id), default));

        await TecnicoAssinaAsync(c, c.Tecnico);
        var finalizado = await handler.Handle(new FinalizarDocumentoCommand(c.Documento.Id), default);

        Assert.Equal(StatusDocumentoAssinatura.Finalizado, finalizado.Status);
        Assert.Equal(2, finalizado.Signatarios.Count);
    }

    private sealed class PdfFalso : IDocumentoAssinaturaPdfService
    {
        public byte[] Gerar(DocumentoAssinaturaPdfModelo modelo) => new byte[] { 1 };
    }

    private sealed class QrFalso : IQrCodeDocumentoService
    {
        public QrCodeDocumentoResultado Gerar(string token) => new(Array.Empty<byte>(), $"https://validar.teste/#/validar/{token}");
    }
}
