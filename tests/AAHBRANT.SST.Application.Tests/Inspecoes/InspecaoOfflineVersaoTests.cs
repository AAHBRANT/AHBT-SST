using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

public class InspecaoOfflineVersaoTests
{
    private static async Task<(SstDbContext Db, InspecaoItemResposta Resposta)> Preparar()
    {
        var db = new SstDbContext(new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new CurrentUserService());
        var obra = new Obra { Codigo = "OFF", Nome = "Obra offline" };
        var usuario = new Usuario { Email = "offline@teste.com", Nome = "Técnico" };
        var inspecao = new Inspecao { Obra = obra, ResponsavelUsuario = usuario, Status = StatusInspecao.EmAndamento };
        var resposta = new InspecaoItemResposta { Inspecao = inspecao, StatusItem = StatusItemChecklist.Conforme,
            Observacao = "Versão do servidor", RowVersion = [1, 2, 3] };
        db.InspecaoItemRespostas.Add(resposta);
        await db.SaveChangesAsync();
        return (db, resposta);
    }

    [Theory]
    [InlineData("resposta")]
    [InlineData("foto")]
    [InlineData("foto-depois")]
    public async Task VersaoAntiga_NaoSobrescreveRespostaNemFotos(string tipo)
    {
        var (db, resposta) = await Preparar();
        using (db)
        {
            var antiga = Convert.ToBase64String([9, 9, 9]);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            {
                if (tipo == "resposta") await new ResponderItemInspecaoCommandHandler(db).Handle(
                    new(resposta.Id, StatusItemChecklist.NaoConforme, "Offline", null, null, null, null, null, antiga), default);
                else if (tipo == "foto") await new AnexarFotoItemInspecaoCommandHandler(db).Handle(
                    new(resposta.Id, [1], "image/jpeg", null, antiga), default);
                else await new AnexarFotoDepoisItemInspecaoCommandHandler(db).Handle(
                    new(resposta.Id, [1], "image/jpeg", null, antiga), default);
            });
            Assert.Equal("Versão do servidor", resposta.Observacao);
            Assert.Empty(resposta.FotoConteudo);
            Assert.True(resposta.FotoDepoisConteudo is null || resposta.FotoDepoisConteudo.Length == 0);
        }
    }

    [Fact]
    public async Task VersaoAtual_PermiteSalvarEDevolveVersao()
    {
        var (db, resposta) = await Preparar();
        using (db)
        {
            var atual = Convert.ToBase64String(resposta.RowVersion!);
            var versao = await new ResponderItemInspecaoCommandHandler(db).Handle(
                new(resposta.Id, StatusItemChecklist.NaoConforme, "Enviado do tablet", null, null, null, null, null, atual), default);
            Assert.Equal("Enviado do tablet", resposta.Observacao);
            Assert.Equal(Convert.ToBase64String(resposta.RowVersion!), versao);
        }
    }
}
