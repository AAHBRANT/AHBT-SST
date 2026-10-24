using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Application.SessoesTreinamento.Queries;
using AAHBRANT.SST.Application.TagsIdentificacao.Commands;
using AAHBRANT.SST.Application.TagsIdentificacao.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

// Auditoria de 09/10/2026, PR 3: filhos acessados pelo próprio id (foto de participante/evidência do
// DDS, foto da turma, resposta de item de inspeção, tag de identificação, candidato/treinamento da
// CIPA) não têm ObraId nem filtro global — a obra está no pai, e nenhum handler a conferia. Também o
// crachá público (sst/p/{uid}) mostrava o status do ASO a qualquer autenticado, de qualquer obra.
public class EscopoObraFotosTagsCipaTests
{
    static EscopoObraFotosTagsCipaTests() => ChavesCpfDeTeste.Configurar();

    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    private sealed record Cenario(
        SstDbContext Global, SstDbContext Restrito,
        Trabalhador DeA, Trabalhador DeB,
        Domain.Entidades.Dds DdsA, Domain.Entidades.Dds DdsB,
        DdsParticipante ParticipanteA, DdsParticipante ParticipanteB,
        DdsFotoEvidencia EvidenciaB,
        FotoEvidenciaSessaoTreinamento FotoTurmaB,
        InspecaoItemResposta RespostaA, InspecaoItemResposta RespostaB,
        TagIdentificacao TagA, TagIdentificacao TagB, TagIdentificacao TagLivre,
        CandidatoCipa CandidatoB, TreinamentoCipa TreinamentoCipaA, TreinamentoCipa TreinamentoCipaB);

    private static async Task<Cenario> CriarAsync()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());
        var usuarioRestrito = new CurrentUserService();
        usuarioRestrito.DefinirEscopo(false, new[] { ObraA });
        var restrito = new SstDbContext(opcoes, usuarioRestrito);

        var funcao = new Funcao { Nome = "Pedreiro" };
        var deA = new Trabalhador { ObraId = ObraA, Funcao = funcao, Nome = "Trabalhador A", Cpf = "00000000001" };
        var deB = new Trabalhador { ObraId = ObraB, Funcao = funcao, Nome = "Trabalhador B", Cpf = "00000000002" };
        global.Funcoes.Add(funcao);
        global.Trabalhadores.AddRange(deA, deB);

        var ddsA = new Domain.Entidades.Dds { ObraId = ObraA };
        var ddsB = new Domain.Entidades.Dds { ObraId = ObraB };
        global.Dds.AddRange(ddsA, ddsB);

        var sessaoB = new SessaoTreinamento { ObraId = ObraB, CursoTreinamentoId = Guid.NewGuid(), DataRealizacao = DateTime.UtcNow };
        global.SessoesTreinamento.Add(sessaoB);

        var inspecaoA = new Inspecao { ObraId = ObraA, ChecklistModeloId = Guid.NewGuid(), Data = DateTime.UtcNow };
        var inspecaoB = new Inspecao { ObraId = ObraB, ChecklistModeloId = Guid.NewGuid(), Data = DateTime.UtcNow };
        global.Inspecoes.AddRange(inspecaoA, inspecaoB);

        var membroA = new MembroCipa { ObraId = ObraA, TrabalhadorId = deA.Id };
        var membroB = new MembroCipa { ObraId = ObraB, TrabalhadorId = deB.Id };
        var processoB = new ProcessoEleitoralCipa { ObraId = ObraB };
        global.MembrosCipa.AddRange(membroA, membroB);
        global.ProcessosEleitoraisCipa.Add(processoB);
        await global.SaveChangesAsync();

        var foto = new byte[] { 0xFF, 0xD8, 1 };
        var participanteA = new DdsParticipante { DdsId = ddsA.Id, TrabalhadorId = deA.Id, FotoConteudo = foto, FotoContentType = "image/jpeg" };
        var participanteB = new DdsParticipante { DdsId = ddsB.Id, TrabalhadorId = deB.Id, FotoConteudo = foto, FotoContentType = "image/jpeg" };
        var evidenciaB = new DdsFotoEvidencia { DdsId = ddsB.Id, Ordem = 1, FotoConteudo = foto, FotoContentType = "image/jpeg" };
        var fotoTurmaB = new FotoEvidenciaSessaoTreinamento { SessaoTreinamentoId = sessaoB.Id, Ordem = 1, FotoConteudo = foto, FotoContentType = "image/jpeg" };
        var respostaA = new InspecaoItemResposta { InspecaoId = inspecaoA.Id, ChecklistModeloItemId = Guid.NewGuid(), FotoConteudo = foto, FotoContentType = "image/jpeg" };
        var respostaB = new InspecaoItemResposta { InspecaoId = inspecaoB.Id, ChecklistModeloItemId = Guid.NewGuid(), FotoConteudo = foto, FotoContentType = "image/jpeg" };
        var tagA = new TagIdentificacao { Uid = "TAG-A", Tipo = TipoTag.QrCode, Status = StatusTag.Vinculada, EntidadeVinculadaTipo = TipoEntidadeVinculada.Trabalhador, EntidadeVinculadaId = deA.Id };
        var tagB = new TagIdentificacao { Uid = "TAG-B", Tipo = TipoTag.QrCode, Status = StatusTag.Vinculada, EntidadeVinculadaTipo = TipoEntidadeVinculada.Trabalhador, EntidadeVinculadaId = deB.Id };
        var tagLivre = new TagIdentificacao { Uid = "TAG-LIVRE", Tipo = TipoTag.Ntag215, Status = StatusTag.Disponivel };
        var candidatoB = new CandidatoCipa { ProcessoEleitoralId = processoB.Id, TrabalhadorId = deB.Id, DataInscricao = DateTime.UtcNow };
        var treinamentoCipaA = new TreinamentoCipa { MembroCipaId = membroA.Id, CargaHoraria = 20, DataRealizacao = DateTime.UtcNow, CertificadoConteudo = new byte[] { 1 }, CertificadoContentType = "application/pdf" };
        var treinamentoCipaB = new TreinamentoCipa { MembroCipaId = membroB.Id, CargaHoraria = 20, DataRealizacao = DateTime.UtcNow, CertificadoConteudo = new byte[] { 1 }, CertificadoContentType = "application/pdf" };

        global.DdsParticipantes.AddRange(participanteA, participanteB);
        global.DdsFotosEvidencia.Add(evidenciaB);
        global.FotosEvidenciaSessaoTreinamento.Add(fotoTurmaB);
        global.InspecaoItemRespostas.AddRange(respostaA, respostaB);
        global.TagsIdentificacao.AddRange(tagA, tagB, tagLivre);
        global.CandidatosCipa.Add(candidatoB);
        global.TreinamentosCipa.AddRange(treinamentoCipaA, treinamentoCipaB);
        global.Asos.Add(new Aso { TrabalhadorId = deB.Id, DataExame = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddYears(1), ResultadoStatus = ResultadoAso.Apto });
        await global.SaveChangesAsync();

        return new Cenario(global, restrito, deA, deB, ddsA, ddsB, participanteA, participanteB, evidenciaB, fotoTurmaB,
            respostaA, respostaB, tagA, tagB, tagLivre, candidatoB, treinamentoCipaA, treinamentoCipaB);
    }

    [Fact]
    public async Task FotoParticipanteDds_DeOutraObra_RetornaNulo()
    {
        var c = await CriarAsync();
        var handler = new ObterFotoParticipanteQueryHandler(c.Restrito);

        Assert.Null(await handler.Handle(new ObterFotoParticipanteQuery(c.ParticipanteB.Id), default));
        Assert.NotNull(await handler.Handle(new ObterFotoParticipanteQuery(c.ParticipanteA.Id), default));
        Assert.NotNull(await new ObterFotoParticipanteQueryHandler(c.Global).Handle(new ObterFotoParticipanteQuery(c.ParticipanteB.Id), default));
    }

    [Fact]
    public async Task FotoParticipanteDds_DdsExcluidoDaPropriaObra_ContinuaVisivel()
    {
        var c = await CriarAsync();
        var dds = await c.Global.Dds.FirstAsync(d => d.Id == c.DdsA.Id);
        dds.Ativo = false;
        await c.Global.SaveChangesAsync();

        Assert.NotNull(await new ObterFotoParticipanteQueryHandler(c.Restrito).Handle(new ObterFotoParticipanteQuery(c.ParticipanteA.Id), default));
    }

    [Fact]
    public async Task FotoEvidenciaDds_DeOutraObra_NaoLeNemRemove()
    {
        var c = await CriarAsync();

        Assert.Null(await new ObterFotoEvidenciaDdsQueryHandler(c.Restrito).Handle(new ObterFotoEvidenciaDdsQuery(c.EvidenciaB.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new RemoverFotoEvidenciaDdsCommandHandler(c.Restrito).Handle(new RemoverFotoEvidenciaDdsCommand(c.EvidenciaB.Id), default));

        Assert.True(await c.Global.DdsFotosEvidencia.AnyAsync(f => f.Id == c.EvidenciaB.Id));
    }

    [Fact]
    public async Task FotoTurmaTreinamento_DeOutraObra_RetornaNulo()
    {
        var c = await CriarAsync();

        Assert.Null(await new ObterFotoEvidenciaSessaoTreinamentoQueryHandler(c.Restrito)
            .Handle(new ObterFotoEvidenciaSessaoTreinamentoQuery(c.FotoTurmaB.Id), default));
    }

    [Fact]
    public async Task FotoItemInspecao_DeOutraObra_RetornaNulo()
    {
        var c = await CriarAsync();
        var handler = new ObterFotoItemInspecaoQueryHandler(c.Restrito);

        Assert.Null(await handler.Handle(new ObterFotoItemInspecaoQuery(c.RespostaB.Id), default));
        Assert.NotNull(await handler.Handle(new ObterFotoItemInspecaoQuery(c.RespostaA.Id), default));
    }

    [Fact]
    public async Task ResponderItemInspecao_DeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();
        var handler = new ResponderItemInspecaoCommandHandler(c.Restrito);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new ResponderItemInspecaoCommand(c.RespostaB.Id, StatusItemChecklist.NaoConforme, "invadido", null, null, null, null, null), default));
        await handler.Handle(
            new ResponderItemInspecaoCommand(c.RespostaA.Id, StatusItemChecklist.Conforme, "ok", null, null, null, null, null), default);

        Assert.Null((await c.Global.InspecaoItemRespostas.AsNoTracking().FirstAsync(r => r.Id == c.RespostaB.Id)).Observacao);
        Assert.Equal("ok", (await c.Global.InspecaoItemRespostas.AsNoTracking().FirstAsync(r => r.Id == c.RespostaA.Id)).Observacao);
    }

    [Fact]
    public async Task Tag_ObterListarEExcluirDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        Assert.Null(await new ObterTagIdentificacaoPorIdQueryHandler(c.Restrito).Handle(new ObterTagIdentificacaoPorIdQuery(c.TagB.Id), default));
        Assert.NotNull(await new ObterTagIdentificacaoPorIdQueryHandler(c.Restrito).Handle(new ObterTagIdentificacaoPorIdQuery(c.TagA.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirTagIdentificacaoCommandHandler(c.Restrito).Handle(new ExcluirTagIdentificacaoCommand(c.TagB.Id), default));

        var lista = await new ListarTagsIdentificacaoQueryHandler(c.Restrito).Handle(new ListarTagsIdentificacaoQuery(), default);
        Assert.Equal(new[] { c.TagA.Id, c.TagLivre.Id }.OrderBy(i => i), lista.Select(t => t.Id).OrderBy(i => i));
        Assert.True(await c.Global.TagsIdentificacao.AnyAsync(t => t.Id == c.TagB.Id));
    }

    [Fact]
    public async Task Tag_VincularTagLivreATrabalhadorDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new VincularTagCommandHandler(c.Restrito).Handle(
            new VincularTagCommand(c.TagLivre.Id, TipoEntidadeVinculada.Trabalhador, c.DeB.Id), default));

        var tag = await c.Global.TagsIdentificacao.AsNoTracking().FirstAsync(t => t.Id == c.TagLivre.Id);
        Assert.Equal(StatusTag.Disponivel, tag.Status);
    }

    [Fact]
    public async Task Cipa_AvaliarCandidatoEArquivoDeTreinamentoDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AvaliarInscricaoCandidatoCipaCommandHandler(c.Restrito).Handle(
            new AvaliarInscricaoCandidatoCipaCommand(c.CandidatoB.Id, true, null), default));

        var arquivo = new ObterArquivoTreinamentoCipaQueryHandler(c.Restrito);
        Assert.Null(await arquivo.Handle(new ObterArquivoTreinamentoCipaQuery(c.TreinamentoCipaB.Id, TipoArquivoTreinamentoCipa.Certificado), default));
        Assert.NotNull(await arquivo.Handle(new ObterArquivoTreinamentoCipaQuery(c.TreinamentoCipaA.Id, TipoArquivoTreinamentoCipa.Certificado), default));

        var candidato = await c.Global.CandidatosCipa.AsNoTracking().FirstAsync(x => x.Id == c.CandidatoB.Id);
        Assert.Equal(StatusCandidatoCipa.Inscrito, candidato.Status);
    }

    [Fact]
    public async Task CrachaPublico_RestritoSobreTrabalhadorDeOutraObra_NaoIncluiAso()
    {
        var c = await CriarAsync();

        var restrito = await new ResolverTrabalhadorPublicoQueryHandler(c.Restrito)
            .Handle(new ResolverTrabalhadorPublicoQuery("TAG-B", IncluirDadosSensiveis: true), default);
        var global = await new ResolverTrabalhadorPublicoQueryHandler(c.Global)
            .Handle(new ResolverTrabalhadorPublicoQuery("TAG-B", IncluirDadosSensiveis: true), default);

        Assert.NotNull(restrito);
        Assert.Null(restrito!.StatusAptidao);
        Assert.Equal("Apto", global!.StatusAptidao);
    }
}
