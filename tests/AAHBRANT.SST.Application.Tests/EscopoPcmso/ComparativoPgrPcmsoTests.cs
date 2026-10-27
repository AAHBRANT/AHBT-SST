using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EscopoPcmso;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EscopoPcmso;

// Comparativo de funções PGR × PCMSO e rascunho do PCMSO a partir do PGR (apontamento da supervisão, 03/08/2026).
public class ComparativoPgrPcmsoTests
{
    private sealed class GeradorNumeroFake : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-TESTE-001");
    }

    // Obra com PGR vigente: GHE 1 com ARMADOR e SERVENTE, um risco de queda; exames só para ARMADOR e para uma função
    // que não está no PGR (AUXILIAR DE LIMPEZA); dois serventes ativos e um vigia sem PGR nem PCMSO.
    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Obra Obra)> CriarAsync(bool comPgrVigente = true)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Complexo Beira Rio", Codigo = "BR" };
        var armador = new Funcao { Nome = "ARMADOR" };
        var servente = new Funcao { Nome = "SERVENTE" };
        var limpeza = new Funcao { Nome = "AUXILIAR DE LIMPEZA" };
        var vigia = new Funcao { Nome = "VIGIA" };
        db.AddRange(obra, armador, servente, limpeza, vigia);
        if (comPgrVigente) db.Pgrs.Add(new Pgr { ObraId = obra.Id, Nome = "PGR Beira Rio", Status = StatusPgr.Vigente, DataElaboracao = new DateTime(2026, 10, 21) });
        var ghe = new Ghe { ObraId = obra.Id, Numero = 1, Setor = "Estrutura" };
        ghe.Funcoes.Add(new GheFuncao { FuncaoId = armador.Id });
        ghe.Funcoes.Add(new GheFuncao { FuncaoId = servente.Id });
        db.Ghes.Add(ghe);
        var atividade = new Atividade { ObraId = obra.Id, GheId = ghe.Id, Nome = "Montagem de pilares" };
        db.Atividades.Add(atividade);
        var queda = new Perigo { Nome = "Queda de altura", Agente = "Acidente" };
        db.Perigos.Add(queda);
        db.Riscos.Add(new Risco { AtividadeId = atividade.Id, PerigoId = queda.Id, NivelRisco = NivelRisco.Critico });
        db.ExamesFuncaoObra.AddRange(
            new ExameFuncaoObra { ObraId = obra.Id, FuncaoId = armador.Id, Exame = "Audiometria" },
            new ExameFuncaoObra { ObraId = obra.Id, FuncaoId = armador.Id, Exame = "Acuidade visual" },
            new ExameFuncaoObra { ObraId = obra.Id, FuncaoId = limpeza.Id, Exame = "Clínico" });
        var admissao = DateTime.UtcNow.AddYears(-1);
        db.Trabalhadores.AddRange(
            new Trabalhador { Nome = "A", Cpf = "11111111111", ObraId = obra.Id, FuncaoId = servente.Id, DataAdmissao = admissao },
            new Trabalhador { Nome = "B", Cpf = "22222222222", ObraId = obra.Id, FuncaoId = servente.Id, DataAdmissao = admissao },
            new Trabalhador { Nome = "C", Cpf = "33333333333", ObraId = obra.Id, FuncaoId = servente.Id, DataAdmissao = admissao, DataDemissao = DateTime.UtcNow.AddDays(-3) },
            new Trabalhador { Nome = "D", Cpf = "44444444444", ObraId = obra.Id, FuncaoId = vigia.Id, DataAdmissao = admissao });
        await db.SaveChangesAsync();
        return (db, obra);
    }

    [Fact]
    public async Task Comparativo_ClassificaCadaFuncaoEContaOsTrabalhadoresAtivos()
    {
        var (db, obra) = await CriarAsync();

        var r = await new ObterComparativoPgrPcmsoQueryHandler(db).Handle(new ObterComparativoPgrPcmsoQuery(obra.Id), default);

        Assert.NotNull(r);
        Assert.Equal("PGR Beira Rio", r!.PgrNome);
        SituacaoFuncaoPgrPcmso Situacao(string f) => r.Funcoes.Single(x => x.Funcao == f).Situacao;
        Assert.Equal(SituacaoFuncaoPgrPcmso.EmOrdem, Situacao("ARMADOR"));
        Assert.Equal(SituacaoFuncaoPgrPcmso.FaltaNoPcmso, Situacao("SERVENTE"));
        Assert.Equal(SituacaoFuncaoPgrPcmso.FaltaNoPgr, Situacao("AUXILIAR DE LIMPEZA"));
        Assert.Equal(SituacaoFuncaoPgrPcmso.FaltaNosDois, Situacao("VIGIA"));
        var servente = r.Funcoes.Single(x => x.Funcao == "SERVENTE");
        Assert.Equal(2, servente.Trabalhadores); // o demitido não conta
        Assert.Equal(new[] { 1 }, servente.GhesPgr);
        Assert.Equal(2, r.Funcoes.Single(x => x.Funcao == "ARMADOR").ExamesPcmso);
        // As que precisam de atenção vêm antes das que estão em ordem.
        Assert.Equal("ARMADOR", r.Funcoes.Last().Funcao);
        Assert.Null(r.MotivoSemRascunho);
    }

    [Fact]
    public async Task Rascunho_PreencheFuncoesERiscosDoPgr_ESemExamesNemMedico()
    {
        var (db, obra) = await CriarAsync();

        var id = await new CriarRascunhoPcmsoDoPgrCommandHandler(db, new GeradorNumeroFake())
            .Handle(new CriarRascunhoPcmsoDoPgrCommand(obra.Id), default);

        var pcmso = await db.PcmsoDetalhes.SingleAsync(p => p.Id == id);
        Assert.Equal(StatusPcmsoDocumento.Rascunho, pcmso.Status);
        Assert.Equal(obra.Id, pcmso.ObraId);
        Assert.Equal("PCMSO-TESTE-001", pcmso.NumeroDocumento);
        Assert.Contains("ARMADOR (GHE 01)", pcmso.FuncoesContempladas);
        Assert.Contains("SERVENTE (GHE 01)", pcmso.FuncoesContempladas);
        Assert.Equal("Queda de altura", pcmso.RiscosConsiderados);
        Assert.Null(pcmso.ExamesPrevistos);
        Assert.Null(pcmso.Periodicidades);
        Assert.Null(pcmso.MedicoResponsavelNome);
        Assert.Null(pcmso.MedicoResponsavelCrm);
        // Depois de criado, o comparativo avisa que já existe rascunho.
        var comparativo = await new ObterComparativoPgrPcmsoQueryHandler(db).Handle(new ObterComparativoPgrPcmsoQuery(obra.Id), default);
        Assert.Contains("rascunho", comparativo!.MotivoSemRascunho);
    }

    [Theory]
    [InlineData(StatusPcmsoDocumento.Vigente, "em vigor")]
    [InlineData(StatusPcmsoDocumento.EmAprovacao, "em aprovação")]
    [InlineData(StatusPcmsoDocumento.Rascunho, "rascunho")]
    public async Task Rascunho_ComPcmsoAtivoNaObra_NaoCria(StatusPcmsoDocumento status, string trecho)
    {
        var (db, obra) = await CriarAsync();
        db.PcmsoDetalhes.Add(new PcmsoDetalhe { Nome = "PCMSO existente", ObraId = obra.Id, Status = status, DataEmissao = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarRascunhoPcmsoDoPgrCommandHandler(db, new GeradorNumeroFake()).Handle(new CriarRascunhoPcmsoDoPgrCommand(obra.Id), default));

        Assert.Contains(trecho, ex.Message);
        Assert.Single(await db.PcmsoDetalhes.ToListAsync());
    }

    [Fact]
    public async Task Rascunho_PcmsoObsoletoOuCancelado_NaoImpede()
    {
        var (db, obra) = await CriarAsync();
        db.PcmsoDetalhes.AddRange(
            new PcmsoDetalhe { Nome = "Antigo", ObraId = obra.Id, Status = StatusPcmsoDocumento.Obsoleto, DataEmissao = DateTime.UtcNow },
            new PcmsoDetalhe { Nome = "Cancelado", ObraId = obra.Id, Status = StatusPcmsoDocumento.Cancelado, DataEmissao = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var id = await new CriarRascunhoPcmsoDoPgrCommandHandler(db, new GeradorNumeroFake()).Handle(new CriarRascunhoPcmsoDoPgrCommand(obra.Id), default);

        Assert.Equal(3, await db.PcmsoDetalhes.CountAsync());
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Rascunho_SemPgrVigente_NaoCria()
    {
        var (db, obra) = await CriarAsync(comPgrVigente: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarRascunhoPcmsoDoPgrCommandHandler(db, new GeradorNumeroFake()).Handle(new CriarRascunhoPcmsoDoPgrCommand(obra.Id), default));

        Assert.Contains("PGR vigente", ex.Message);
        Assert.Empty(await db.PcmsoDetalhes.ToListAsync());
    }
}
