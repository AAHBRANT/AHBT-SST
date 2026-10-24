using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Application.Veiculos.Commands;
using AAHBRANT.SST.Application.Veiculos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Veiculos;

// Inspeção de Veículos (02/10/2026): cadastro manual por obra, checklist por tipo de veículo e
// foto obrigatória só nos itens Não Conforme.
public class VeiculosHandlersTests
{
    private const string AzureAdObjectId = "oid-tecnico-veiculo";

    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static ChecklistModelo NovoChecklist(TipoVeiculo tipo, params string[] itens)
    {
        var checklist = new ChecklistModelo
        {
            Nome = $"Checklist {tipo}",
            TipoInspecao = TipoInspecao.Veiculo,
            TipoVeiculo = tipo,
            Versao = 1,
        };
        var ordem = 1;
        foreach (var descricao in itens)
            checklist.Itens.Add(new ChecklistModeloItem { Ordem = ordem++, Descricao = descricao });
        return checklist;
    }

    private static async Task<(SstDbContext db, Obra obra, Veiculo veiculo)> PrepararCenario(string nomeBanco)
    {
        var db = CriarDb(nomeBanco);
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico", AzureAdObjectId = AzureAdObjectId };
        var veiculo = new Veiculo { ObraId = obra.Id, Tipo = TipoVeiculo.Retroescavadeira, PlacaPrefixo = "JD-310" };
        db.AddRange(obra, usuario, veiculo,
            NovoChecklist(TipoVeiculo.Retroescavadeira, "Espelhos retrovisores", "Buzina funcionando"),
            NovoChecklist(TipoVeiculo.CaminhaoMunck, "Trava do gancho"));
        await db.SaveChangesAsync();
        return (db, obra, veiculo);
    }

    [Fact]
    public async Task ObterOuCriar_UsaChecklistDoTipoDoVeiculoEPreencheVeiculoId()
    {
        var (db, obra, veiculo) = await PrepararCenario(nameof(ObterOuCriar_UsaChecklistDoTipoDoVeiculoEPreencheVeiculoId));
        var handler = new ObterOuCriarInspecaoVeiculoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var resultado = await handler.Handle(new ObterOuCriarInspecaoVeiculoCommand(veiculo.Id, AzureAdObjectId), default);

        Assert.True(resultado.FoiCriadaAgora);
        var inspecao = await db.Inspecoes.Include(i => i.ChecklistModelo).FirstAsync(i => i.Id == resultado.InspecaoId);
        Assert.Equal(TipoInspecao.Veiculo, inspecao.TipoInspecao);
        Assert.Equal(veiculo.Id, inspecao.VeiculoId);
        Assert.Equal(obra.Id, inspecao.ObraId);
        Assert.Equal(TipoVeiculo.Retroescavadeira, inspecao.ChecklistModelo!.TipoVeiculo);
        Assert.Equal(2, inspecao.Respostas.Count);
    }

    [Fact]
    public async Task ObterOuCriar_ComInspecaoEmAndamento_RetornaAMesmaSemDuplicar()
    {
        var (db, _, veiculo) = await PrepararCenario(nameof(ObterOuCriar_ComInspecaoEmAndamento_RetornaAMesmaSemDuplicar));
        var handler = new ObterOuCriarInspecaoVeiculoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var primeira = await handler.Handle(new ObterOuCriarInspecaoVeiculoCommand(veiculo.Id, AzureAdObjectId), default);
        var segunda = await handler.Handle(new ObterOuCriarInspecaoVeiculoCommand(veiculo.Id, AzureAdObjectId), default);

        Assert.Equal(primeira.InspecaoId, segunda.InspecaoId);
        Assert.False(segunda.FoiCriadaAgora);
        Assert.Equal(1, await db.Inspecoes.CountAsync(i => i.VeiculoId == veiculo.Id));
    }

    [Fact]
    public async Task ObterOuCriar_TipoSemChecklist_LancaInvalidOperationException()
    {
        var (db, obra, _) = await PrepararCenario(nameof(ObterOuCriar_TipoSemChecklist_LancaInvalidOperationException));
        var escavadeira = new Veiculo { ObraId = obra.Id, Tipo = TipoVeiculo.EscavadeiraHidraulica, PlacaPrefixo = "ESC-01" };
        db.Veiculos.Add(escavadeira);
        await db.SaveChangesAsync();
        var handler = new ObterOuCriarInspecaoVeiculoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new ObterOuCriarInspecaoVeiculoCommand(escavadeira.Id, AzureAdObjectId), default));

        Assert.Contains("tipo de veículo", ex.Message);
    }

    private static async Task<(SstDbContext db, Inspecao inspecao, InspecaoItemResposta resposta)> PrepararInspecaoComUmItem(
        string nomeBanco, StatusItemChecklist status, byte[]? foto)
    {
        var (db, _, veiculo) = await PrepararCenario(nomeBanco);
        var handler = new ObterOuCriarInspecaoVeiculoCommandHandler(db, new GeradorNumeroDocumentoService(db));
        var atual = await handler.Handle(new ObterOuCriarInspecaoVeiculoCommand(veiculo.Id, AzureAdObjectId), default);
        var inspecao = await db.Inspecoes.Include(i => i.Respostas).FirstAsync(i => i.Id == atual.InspecaoId);

        // O checklist de teste tem 2 itens: o 1º recebe o cenário do teste; o 2º fica Conforme sem foto.
        var primeiro = inspecao.Respostas.First();
        primeiro.StatusItem = status;
        if (foto is not null) primeiro.FotoConteudo = foto;
        foreach (var outro in inspecao.Respostas.Where(r => r.Id != primeiro.Id))
            outro.StatusItem = StatusItemChecklist.Conforme;
        await db.SaveChangesAsync();
        return (db, inspecao, primeiro);
    }

    [Fact]
    public async Task Encerrar_ItemNaoConformeSemFoto_LancaInvalidOperationException()
    {
        var (db, inspecao, _) = await PrepararInspecaoComUmItem(nameof(Encerrar_ItemNaoConformeSemFoto_LancaInvalidOperationException), StatusItemChecklist.NaoConforme, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default));

        Assert.Contains("Não Conforme", ex.Message);
        Assert.Contains("exigem foto", ex.Message);
    }

    [Fact]
    public async Task Encerrar_ItemConformeSemFoto_Conclui()
    {
        var (db, inspecao, _) = await PrepararInspecaoComUmItem(nameof(Encerrar_ItemConformeSemFoto_Conclui), StatusItemChecklist.Conforme, null);

        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        Assert.Equal(StatusInspecao.Concluida, (await db.Inspecoes.FirstAsync(i => i.Id == inspecao.Id)).Status);
    }

    [Fact]
    public async Task Encerrar_ItemNaoAplicavelSemFoto_Conclui()
    {
        var (db, inspecao, _) = await PrepararInspecaoComUmItem(nameof(Encerrar_ItemNaoAplicavelSemFoto_Conclui), StatusItemChecklist.NaoAplicavel, null);

        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        Assert.Equal(StatusInspecao.Concluida, (await db.Inspecoes.FirstAsync(i => i.Id == inspecao.Id)).Status);
    }

    [Fact]
    public async Task Criar_PlacaDuplicada_LancaInvalidOperationException()
    {
        var (db, obra, veiculo) = await PrepararCenario(nameof(Criar_PlacaDuplicada_LancaInvalidOperationException));
        var handler = new CriarVeiculoCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CriarVeiculoCommand(obra.Id, TipoVeiculo.CaminhaoMunck, $" {veiculo.PlacaPrefixo} ", null, null, null, null, null), default));
    }

    [Fact]
    public async Task Atualizar_TrocaDeObraLevaAInspecaoEmAndamento_MasNaoAsConcluidas()
    {
        var (db, _, veiculo) = await PrepararCenario(nameof(Atualizar_TrocaDeObraLevaAInspecaoEmAndamento_MasNaoAsConcluidas));
        var outraObra = new Obra { Codigo = "OB2", Nome = "Outra Obra" };
        db.Obras.Add(outraObra);
        var obraOriginalId = veiculo.ObraId;
        var concluida = new Inspecao
        {
            TipoInspecao = TipoInspecao.Veiculo,
            ObraId = obraOriginalId,
            VeiculoId = veiculo.Id,
            ChecklistModeloId = (await db.ChecklistModelos.FirstAsync(c => c.TipoVeiculo == TipoVeiculo.Retroescavadeira)).Id,
            ResponsavelUsuarioId = (await db.Usuarios.FirstAsync()).Id,
            Data = DateTime.UtcNow.AddDays(-3),
            Status = StatusInspecao.Concluida,
        };
        db.Inspecoes.Add(concluida);
        await db.SaveChangesAsync();
        var atual = await new ObterOuCriarInspecaoVeiculoCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoVeiculoCommand(veiculo.Id, AzureAdObjectId), default);

        await new AtualizarVeiculoCommandHandler(db).Handle(
            new AtualizarVeiculoCommand(veiculo.Id, outraObra.Id, veiculo.Tipo, veiculo.PlacaPrefixo, null, null, null, null, null), default);

        Assert.Equal(outraObra.Id, (await db.Inspecoes.FirstAsync(i => i.Id == atual.InspecaoId)).ObraId);
        Assert.Equal(obraOriginalId, (await db.Inspecoes.FirstAsync(i => i.Id == concluida.Id)).ObraId);
    }

    [Fact]
    public async Task Atualizar_TrocaDeTipoComInspecaoEmAndamento_LancaInvalidOperationException()
    {
        var (db, _, veiculo) = await PrepararCenario(nameof(Atualizar_TrocaDeTipoComInspecaoEmAndamento_LancaInvalidOperationException));
        await new ObterOuCriarInspecaoVeiculoCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoVeiculoCommand(veiculo.Id, AzureAdObjectId), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new AtualizarVeiculoCommandHandler(db).Handle(
            new AtualizarVeiculoCommand(veiculo.Id, veiculo.ObraId, TipoVeiculo.CaminhaoMunck, veiculo.PlacaPrefixo, null, null, null, null, null), default));
    }

    [Fact]
    public async Task Listar_FiltraPorTipoEIndicaStatusDaInspecao()
    {
        var (db, obra, veiculo) = await PrepararCenario(nameof(Listar_FiltraPorTipoEIndicaStatusDaInspecao));
        db.Veiculos.Add(new Veiculo { ObraId = obra.Id, Tipo = TipoVeiculo.CaminhaoMunck, PlacaPrefixo = "MUNCK-01" });
        await db.SaveChangesAsync();
        await new ObterOuCriarInspecaoVeiculoCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoVeiculoCommand(veiculo.Id, AzureAdObjectId), default);

        var retros = await new ListarVeiculosQueryHandler(db).Handle(new ListarVeiculosQuery(obra.Id, TipoVeiculo.Retroescavadeira), default);

        var retro = Assert.Single(retros);
        Assert.Equal("JD-310", retro.PlacaPrefixo);
        Assert.Equal("nunca", retro.StatusUltimaInspecao);
        Assert.NotNull(retro.InspecaoEmAndamento);
    }

    [Fact]
    public async Task Excluir_ComInspecaoEmAndamento_LancaInvalidOperationException()
    {
        var (db, _, veiculo) = await PrepararCenario(nameof(Excluir_ComInspecaoEmAndamento_LancaInvalidOperationException));
        await new ObterOuCriarInspecaoVeiculoCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoVeiculoCommand(veiculo.Id, AzureAdObjectId), default);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ExcluirVeiculoCommandHandler(db).Handle(new ExcluirVeiculoCommand(veiculo.Id), default));
    }
}
