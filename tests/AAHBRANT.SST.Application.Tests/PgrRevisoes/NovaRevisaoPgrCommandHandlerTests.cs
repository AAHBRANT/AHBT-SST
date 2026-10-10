using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.PgrRevisoes.Commands;
using AAHBRANT.SST.Application.PgrRevisoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.PgrRevisoes;

public class NovaRevisaoPgrCommandHandlerTests
{
    private static readonly byte[] PdfAntigo = { 1, 2, 3 };
    private static readonly byte[] PdfNovo = { 9, 9, 9 };

    private static SstDbContext CriarDb(string nome, CurrentUserService? usuario = null) =>
        new(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, usuario ?? new CurrentUserService());

    private static async Task<Pgr> SemearAsync(SstDbContext db, bool comPdf, params int[] revisoesSemPdf)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra" };
        var pgr = new Pgr
        {
            Obra = obra, Nome = "PGR", DataElaboracao = new DateTime(2024, 10, 10),
            DocumentoConteudo = comPdf ? PdfAntigo : null, DocumentoContentType = comPdf ? "application/pdf" : null,
        };
        db.Pgrs.Add(pgr);
        foreach (var n in revisoesSemPdf)
            db.PgrRevisoes.Add(new PgrRevisao { Pgr = pgr, NumeroRevisao = n, DataRevisao = DateTime.Today, Motivo = $"Rev {n}" });
        await db.SaveChangesAsync();
        return pgr;
    }

    private static NovaRevisaoPgrCommand Comando(Guid pgrId, int? numero = null) =>
        new(pgrId, numero, new DateTime(2026, 10, 10), "Renovação anual", PdfNovo, "application/pdf", "rev.pdf");

    [Fact]
    public async Task PDF_antigo_vai_para_a_ultima_revisao_sem_PDF_e_o_novo_vira_o_atual()
    {
        var db = CriarDb(nameof(PDF_antigo_vai_para_a_ultima_revisao_sem_PDF_e_o_novo_vira_o_atual));
        var pgr = await SemearAsync(db, comPdf: true, 0, 1);

        await new NovaRevisaoPgrCommandHandler(db).Handle(Comando(pgr.Id), CancellationToken.None);

        var revisoes = await db.PgrRevisoes.OrderBy(r => r.NumeroRevisao).ToListAsync();
        Assert.Equal(new[] { 0, 1, 2 }, revisoes.Select(r => r.NumeroRevisao));
        Assert.Null(revisoes[0].DocumentoConteudo);
        Assert.Equal(PdfAntigo, revisoes[1].DocumentoConteudo);
        Assert.Equal(PdfNovo, revisoes[2].DocumentoConteudo);
        Assert.Equal(PdfNovo, (await db.Pgrs.SingleAsync()).DocumentoConteudo);
    }

    [Fact]
    public async Task Sem_revisoes_o_PDF_antigo_vira_a_revisao_00_e_o_novo_a_01()
    {
        var db = CriarDb(nameof(Sem_revisoes_o_PDF_antigo_vira_a_revisao_00_e_o_novo_a_01));
        var pgr = await SemearAsync(db, comPdf: true);

        await new NovaRevisaoPgrCommandHandler(db).Handle(Comando(pgr.Id), CancellationToken.None);

        var revisoes = await db.PgrRevisoes.OrderBy(r => r.NumeroRevisao).ToListAsync();
        Assert.Equal(2, revisoes.Count);
        Assert.Equal(RevisaoDocumento.MotivoDocumentoAnterior, revisoes[0].Motivo);
        Assert.Equal(PdfAntigo, revisoes[0].DocumentoConteudo);
        Assert.Equal(1, revisoes[1].NumeroRevisao);
    }

    [Fact]
    public async Task Recusa_numero_que_deixaria_o_PDF_antigo_sem_lugar()
    {
        var db = CriarDb(nameof(Recusa_numero_que_deixaria_o_PDF_antigo_sem_lugar));
        var pgr = await SemearAsync(db, comPdf: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new NovaRevisaoPgrCommandHandler(db).Handle(Comando(pgr.Id, numero: 0), CancellationToken.None));
        Assert.Equal(PdfAntigo, (await db.Pgrs.SingleAsync()).DocumentoConteudo);
    }

    [Fact]
    public async Task Recusa_numero_repetido()
    {
        var db = CriarDb(nameof(Recusa_numero_repetido));
        var pgr = await SemearAsync(db, comPdf: false, 0, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new NovaRevisaoPgrCommandHandler(db).Handle(Comando(pgr.Id, numero: 1), CancellationToken.None));
    }

    [Fact]
    public async Task Listagem_informa_se_a_revisao_tem_PDF_e_usuario_de_outra_obra_nao_ve()
    {
        var nome = nameof(Listagem_informa_se_a_revisao_tem_PDF_e_usuario_de_outra_obra_nao_ve);
        var db = CriarDb(nome);
        var pgr = await SemearAsync(db, comPdf: false, 0);
        await new NovaRevisaoPgrCommandHandler(db).Handle(Comando(pgr.Id), CancellationToken.None);

        var lista = await new ListarPgrRevisoesQueryHandler(db).Handle(new ListarPgrRevisoesQuery(pgr.Id), CancellationToken.None);
        Assert.Equal(new[] { true, false }, lista.Select(r => r.TemDocumento));

        var restrito = new CurrentUserService();
        restrito.DefinirEscopo(false, new[] { Guid.NewGuid() });
        var dbRestrito = CriarDb(nome, restrito);
        Assert.Empty(await new ListarPgrRevisoesQueryHandler(dbRestrito).Handle(new ListarPgrRevisoesQuery(pgr.Id), CancellationToken.None));
        Assert.Null(await new ObterDocumentoRevisaoPgrQueryHandler(dbRestrito)
            .Handle(new ObterDocumentoRevisaoPgrQuery(lista[0].Id), CancellationToken.None));
    }
}
