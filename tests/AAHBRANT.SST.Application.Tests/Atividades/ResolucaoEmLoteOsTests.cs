using AAHBRANT.SST.Application.Atividades.DetalhesOs;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Atividades;

// Resolução das pendências da OS em lote (17/11/2026): procedimentos em várias atividades e sugestões da IA em bloco.
public class ResolucaoEmLoteOsTests
{
    private static MaterialApoio Doc(string codigo, SituacaoDocumentoSst situacao, TipoDocumentoSst tipo = TipoDocumentoSst.Procedimento) => new()
    {
        Nome = $"Doc {codigo}", NomeArquivo = "a.pdf", ContentType = "application/pdf", Tipo = tipo, Codigo = codigo, Revisao = "01", Situacao = situacao,
    };

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Atividade A1, Atividade A2)> CriarAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste" };
        var a1 = new Atividade { ObraId = obra.Id, Nome = "01.01.2 Serviços Preliminares" };
        var a2 = new Atividade { ObraId = obra.Id, Nome = "01.01.2.1 Remoções / demolições" };
        db.AddRange(obra, a1, a2);
        await db.SaveChangesAsync();
        return (db, a1, a2);
    }

    [Fact]
    public async Task VincularEmLote_SubstituiSoOsVigentes_EMantemDocumentoForaDeVigencia()
    {
        var (db, a1, a2) = await CriarAsync();
        var po1 = Doc("PO-1", SituacaoDocumentoSst.Vigente);
        var po2 = Doc("PO-2", SituacaoDocumentoSst.Vigente);
        var obsoleto = Doc("PO-9", SituacaoDocumentoSst.EmRevisao);
        db.AddRange(po1, po2, obsoleto,
            new AtividadeDocumento { AtividadeId = a1.Id, MaterialApoioId = po1.Id },
            new AtividadeDocumento { AtividadeId = a1.Id, MaterialApoioId = obsoleto.Id });
        await db.SaveChangesAsync();

        await new VincularProcedimentosEmLoteCommandHandler(db).Handle(new VincularProcedimentosEmLoteCommand(new()
        {
            new(a1.Id, new() { po2.Id }),
            new(a2.Id, new() { po1.Id, po2.Id }),
        }), default);

        var docsA1 = await db.AtividadeDocumentos.Where(d => d.AtividadeId == a1.Id).Select(d => d.MaterialApoioId).ToListAsync();
        Assert.Equal(new[] { po2.Id, obsoleto.Id }.OrderBy(x => x), docsA1.OrderBy(x => x));
        var docsA2 = await db.AtividadeDocumentos.Where(d => d.AtividadeId == a2.Id).Select(d => d.MaterialApoioId).ToListAsync();
        Assert.Equal(new[] { po1.Id, po2.Id }.OrderBy(x => x), docsA2.OrderBy(x => x));

        var consulta = await new ObterProcedimentosAtividadesQueryHandler(db).Handle(new ObterProcedimentosAtividadesQuery(new() { a2.Id, a1.Id }), default);
        Assert.Equal(new[] { a2.Id, a1.Id }, consulta.Select(c => c.AtividadeId));
        Assert.Equal(new[] { po2.Id }, consulta[1].DocumentoIds); // o obsoleto não aparece
    }

    [Fact]
    public async Task VincularEmLote_RecusaDocumentoNaoVigenteOuQueNaoEhProcedimento()
    {
        var (db, a1, _) = await CriarAsync();
        var emRevisao = Doc("PO-2", SituacaoDocumentoSst.EmRevisao);
        var politica = Doc("POL-1", SituacaoDocumentoSst.Vigente, TipoDocumentoSst.Politica);
        db.AddRange(emRevisao, politica);
        await db.SaveChangesAsync();
        var handler = new VincularProcedimentosEmLoteCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new VincularProcedimentosEmLoteCommand(new() { new(a1.Id, new() { emRevisao.Id }) }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new VincularProcedimentosEmLoteCommand(new() { new(a1.Id, new() { politica.Id }) }), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new VincularProcedimentosEmLoteCommand(new() { new(Guid.NewGuid(), new()) }), default));
        Assert.Empty(await db.AtividadeDocumentos.ToListAsync());
    }

    [Fact]
    public async Task Sugestoes_ListaSoPendentes_EDecideEmLoteIgnorandoJaDecididas()
    {
        var (db, a1, a2) = await CriarAsync();
        var perigo = new Perigo { Nome = "Queda de altura" };
        var risco = new Risco { AtividadeId = a1.Id, PerigoId = perigo.Id, Probabilidade = 3, Severidade = 4 };
        var m1 = new MedidaPrevencao { RiscoId = risco.Id, Fase = FaseMedidaPrevencao.Antes, Texto = "Inspecionar cinto", Status = StatusMedidaPrevencao.Pendente, OrigemMedida = OrigemMedidaPrevencao.SugestaoIa };
        var m2 = new MedidaPrevencao { RiscoId = risco.Id, Fase = FaseMedidaPrevencao.Durante, Texto = "Ancorar", Status = StatusMedidaPrevencao.Pendente, OrigemMedida = OrigemMedidaPrevencao.SugestaoIa };
        var mValidada = new MedidaPrevencao { RiscoId = risco.Id, Fase = FaseMedidaPrevencao.Apos, Texto = "Guardar", Status = StatusMedidaPrevencao.Validada };
        var item = new AtividadeItemOs { AtividadeId = a2.Id, Tipo = TipoItemAtividadeOs.Restricao, Texto = "Operar policorte", Requisito = "Autorização", Status = StatusMedidaPrevencao.Pendente, OrigemItem = OrigemMedidaPrevencao.SugestaoIa };
        db.AddRange(perigo, risco, m1, m2, mValidada, item);
        await db.SaveChangesAsync();

        var lista = await new ListarSugestoesPendentesOsQueryHandler(db).Handle(new ListarSugestoesPendentesOsQuery(new() { a1.Id, a2.Id }), default);

        Assert.Equal(new[] { m1.Id, m2.Id, item.Id }, lista.Select(s => s.Id));
        Assert.Equal("Queda de altura", lista[0].Perigo);
        Assert.Equal("Antes do trabalho", lista[0].Categoria);
        Assert.Equal(OrigemSugestaoOs.ItemOs, lista[2].Origem);
        Assert.Equal("Autorização", lista[2].Requisito);

        var handler = new DecidirSugestoesOsEmLoteCommandHandler(db);
        var aceitas = await handler.Handle(new DecidirSugestoesOsEmLoteCommand(new() { m1.Id, mValidada.Id }, new() { item.Id }, true, null, "Eng. Ana"), default);
        var descartadas = await handler.Handle(new DecidirSugestoesOsEmLoteCommand(new() { m1.Id, m2.Id }, new(), false, null, "Eng. Ana"), default);

        Assert.Equal(2, aceitas);      // a já validada é ignorada
        Assert.Equal(1, descartadas);  // m1 já tinha sido aceita
        Assert.Equal(StatusMedidaPrevencao.Validada, (await db.MedidasPrevencao.SingleAsync(m => m.Id == m1.Id)).Status);
        Assert.Equal(StatusMedidaPrevencao.Descartada, (await db.MedidasPrevencao.SingleAsync(m => m.Id == m2.Id)).Status);
        var itemSalvo = await db.AtividadeItensOs.SingleAsync();
        Assert.Equal(StatusMedidaPrevencao.Validada, itemSalvo.Status);
        Assert.Equal("Eng. Ana", itemSalvo.ValidadoPorNome);
    }

    [Fact]
    public void DecidirEmLote_SemNenhumaSugestao_EhInvalido()
        => Assert.False(new DecidirSugestoesOsEmLoteCommandValidator()
            .Validate(new DecidirSugestoesOsEmLoteCommand(new(), new(), true, null, null)).IsValid);
}
