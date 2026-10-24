using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

// Encerramento gera NC automática para cada item Não Conforme que ainda não tem NC (decisão do usuário, 17/10).
public class EncerrarInspecaoGeraNcAutomaticaTests
{
    private static async Task<(IAppDbContext db, Inspecao inspecao, Obra obra)> Montar(string nome, params StatusItemChecklist[] status)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options;
        IAppDbContext db = new SstDbContext(options, new CurrentUserService());
        var obra = new Obra { Codigo = "OB-NC", Nome = "Obra NC" };
        var usuario = new Usuario { Email = "r@aahbrant.com", Nome = "R" };
        var checklist = new ChecklistModelo { Nome = "Alojamento", TipoInspecao = TipoInspecao.Alojamento };
        for (var i = 0; i < status.Length; i++)
            checklist.Itens.Add(new ChecklistModeloItem { Ordem = i + 1, Descricao = $"Item {i + 1}" });
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);
        db.ChecklistModelos.Add(checklist);
        await db.SaveChangesAsync();
        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento, ObraId = obra.Id, ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow, ResponsavelUsuarioId = usuario.Id,
        };
        var itens = checklist.Itens.OrderBy(i => i.Ordem).ToList();
        for (var i = 0; i < status.Length; i++)
            inspecao.Respostas.Add(new InspecaoItemResposta { ChecklistModeloItemId = itens[i].Id, StatusItem = status[i], Observacao = i == 0 ? "Colchão rasgado" : null });
        db.Inspecoes.Add(inspecao);
        await db.SaveChangesAsync();
        return (db, inspecao, obra);
    }

    [Fact]
    public async Task UmItemNaoConforme_GeraUmaNcComObraDaInspecao()
    {
        var (db, inspecao, obra) = await Montar(nameof(UmItemNaoConforme_GeraUmaNcComObraDaInspecao),
            StatusItemChecklist.NaoConforme, StatusItemChecklist.Conforme, StatusItemChecklist.NaoAplicavel);
        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        var nc = Assert.Single(db.NaoConformidades.ToList());
        Assert.Equal(obra.Id, nc.ObraId);
        Assert.Equal(OrigemNaoConformidade.Inspecao, nc.OrigemDeteccao);
        Assert.Equal("Item 1 — Colchão rasgado", nc.Descricao);
    }

    [Fact]
    public async Task DoisItensNaoConformes_UmJaComNc_GeraSoOOutro()
    {
        var (db, inspecao, _) = await Montar(nameof(DoisItensNaoConformes_UmJaComNc_GeraSoOOutro),
            StatusItemChecklist.NaoConforme, StatusItemChecklist.NaoConforme);
        var primeiro = inspecao.Respostas.First(r => r.Observacao != null);
        db.NaoConformidades.Add(new NaoConformidade { Descricao = "manual", InspecaoItemRespostaId = primeiro.Id });
        await db.SaveChangesAsync();

        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        Assert.Equal(2, db.NaoConformidades.Count());
        Assert.Equal(2, db.NaoConformidades.Select(n => n.InspecaoItemRespostaId).Distinct().Count());
    }

    [Fact]
    public async Task TudoConforme_NaoGeraNc()
    {
        var (db, inspecao, _) = await Montar(nameof(TudoConforme_NaoGeraNc), StatusItemChecklist.Conforme);
        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);
        Assert.Empty(db.NaoConformidades);
    }
}
