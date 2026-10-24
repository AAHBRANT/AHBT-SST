using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using DdsEntidade = AAHBRANT.SST.Domain.Entidades.Dds;

namespace AAHBRANT.SST.Application.Tests.Dds;

// O fechamento passa a ser registrado (EncerradoEm, UTC): o relatório mostra a que horas o DDS foi fechado e quanto
// se demorou para fechar.
public class EncerrarDdsEncerradoEmTests
{
    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, DdsEntidade Dds)> CriarAsync(int fotos)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra", Codigo = "O" };
        var usuario = new Usuario { Nome = "R", Email = "r@example.test" };
        var dds = new DdsEntidade { Obra = obra, ObraId = obra.Id, ResponsavelUsuario = usuario, ResponsavelUsuarioId = usuario.Id, Data = new DateTime(2026, 10, 7) };
        db.AddRange(obra, usuario, dds);
        for (var i = 1; i <= fotos; i++)
            db.DdsFotosEvidencia.Add(new DdsFotoEvidencia { DdsId = dds.Id, Ordem = i, FotoConteudo = new byte[] { 1 }, FotoContentType = "image/jpeg" });
        await db.SaveChangesAsync();
        return (db, dds);
    }

    [Fact]
    public async Task Encerrar_GravaOMomentoDoFechamento()
    {
        var (db, dds) = await CriarAsync(fotos: 3);
        var antes = DateTime.UtcNow;

        await new EncerrarDdsCommandHandler(db).Handle(new EncerrarDdsCommand(dds.Id), CancellationToken.None);

        var salvo = await db.Dds.SingleAsync();
        Assert.Equal(StatusDds.Concluido, salvo.Status);
        Assert.NotNull(salvo.EncerradoEm);
        Assert.InRange(salvo.EncerradoEm!.Value, antes.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task SemAsTresFotos_NaoEncerraENaoGravaOFechamento()
    {
        var (db, dds) = await CriarAsync(fotos: 2);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EncerrarDdsCommandHandler(db).Handle(new EncerrarDdsCommand(dds.Id), CancellationToken.None));

        var salvo = await db.Dds.SingleAsync();
        Assert.Equal(StatusDds.EmAndamento, salvo.Status);
        Assert.Null(salvo.EncerradoEm);
    }
}
