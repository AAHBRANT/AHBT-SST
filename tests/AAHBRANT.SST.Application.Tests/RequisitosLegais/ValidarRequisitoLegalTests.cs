using AAHBRANT.SST.Application.RequisitosLegais.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.RequisitosLegais;

// Validação do QSMS sobre os requisitos carregados das NRs (09/10/2026).
public class ValidarRequisitoLegalTests
{
    [Fact]
    public async Task Validar_EmRevisao_AtivaERegistraQuemValidou()
    {
        var db = DbContextFactory.Criar();
        var qsms = new Usuario { Nome = "Ana QSMS", Email = "ana@x.com" };
        var requisito = new RequisitoLegal { Norma = "NR-35", Artigo = "35.5.5", Titulo = "AR", Descricao = "35.5.5 ...", Status = StatusRequisitoLegal.EmRevisao };
        db.AddRange(qsms, requisito);
        await db.SaveChangesAsync();

        await new ValidarRequisitoLegalCommandHandler(db).Handle(new ValidarRequisitoLegalCommand(requisito.Id, qsms.Id, "nome do token"), default);

        var salvo = await db.RequisitosLegais.SingleAsync();
        Assert.Equal(StatusRequisitoLegal.Ativo, salvo.Status);
        Assert.Equal("Ana QSMS", salvo.ValidadoPorNome); // nome do cadastro tem precedência sobre o do token
        Assert.Equal(qsms.Id, salvo.ValidadoPorUsuarioId);
        Assert.NotNull(salvo.ValidadoEmUtc);
    }

    [Theory]
    [InlineData(StatusRequisitoLegal.Ativo)]
    [InlineData(StatusRequisitoLegal.Revogado)]
    public async Task Validar_ForaDeRevisao_Recusa(StatusRequisitoLegal status)
    {
        var db = DbContextFactory.Criar();
        var requisito = new RequisitoLegal { Norma = "NR-06", Artigo = "6.3", Titulo = "EPI", Descricao = "6.3 ...", Status = status };
        db.Add(requisito);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ValidarRequisitoLegalCommandHandler(db).Handle(new ValidarRequisitoLegalCommand(requisito.Id, null, null), default));
        Assert.Null((await db.RequisitosLegais.SingleAsync()).ValidadoEmUtc);
    }
}
