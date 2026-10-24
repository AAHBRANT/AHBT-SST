using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DdsEntidade = AAHBRANT.SST.Domain.Entidades.Dds;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class SelecaoFuncionariosDdsTests
{
    private static SstDbContext CriarDb(CurrentUserService? usuario = null) => new(
        new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        usuario ?? new CurrentUserService());

    private static async Task<(DdsEntidade Dds, Trabalhador Ana, Trabalhador Bruno, Trabalhador OutraObra)> Semear(SstDbContext db)
    {
        var obra = new Obra { Nome = "Obra A", Codigo = "A" };
        var outraObra = new Obra { Nome = "Obra B", Codigo = "B" };
        var usuario = new Usuario { Nome = "Responsável", Email = "dds@example.test" };
        var dds = new DdsEntidade { Obra = obra, ObraId = obra.Id, ResponsavelUsuario = usuario,
            ResponsavelUsuarioId = usuario.Id, Data = new DateTime(2026, 9, 29) };
        var ana = new Trabalhador { Nome = "Ana", Matricula = "001", Obra = obra, ObraId = obra.Id };
        var bruno = new Trabalhador { Nome = "Bruno", Matricula = "002", Obra = obra, ObraId = obra.Id };
        var outro = new Trabalhador { Nome = "Outra obra", Obra = outraObra, ObraId = outraObra.Id };
        db.AddRange(dds, ana, bruno, outro);
        await db.SaveChangesAsync();
        return (dds, ana, bruno, outro);
    }

    [Theory]
    [InlineData(28)] // segunda-feira
    [InlineData(29)] // terça-feira: nenhuma regra depende do dia da semana
    [InlineData(30)]
    public async Task SelecionarTodos_UsaSomenteFuncionariosAtivosDaObra_EmQualquerDia(int dia)
    {
        await using var db = CriarDb();
        var (dds, ana, bruno, _) = await Semear(db);
        dds.Data = new DateTime(2026, 9, dia);
        db.Trabalhadores.AddRange(
            new Trabalhador { Nome = "Excluído", ObraId = dds.ObraId, Ativo = false },
            new Trabalhador { Nome = "Desligado", ObraId = dds.ObraId, Situacao = SituacaoTrabalhador.Desligado },
            new Trabalhador { Nome = "Demitido", ObraId = dds.ObraId, DataDemissao = new DateTime(2026, 9, 1) });
        await db.SaveChangesAsync();

        var disponiveis = await new ListarFuncionariosDdsQueryHandler(db).Handle(new(dds.Id), default);
        Assert.Equal(new[] { ana.Id, bruno.Id }, disponiveis.Select(f => f.TrabalhadorId));
        await new AtualizarFuncionariosDdsCommandHandler(db).Handle(new(dds.Id, disponiveis.Select(f => f.TrabalhadorId).ToList()), default);
        db.ChangeTracker.Clear();

        var detalhe = await new ObterDdsDetalheQueryHandler(db).Handle(new(dds.Id), default);
        Assert.Equal(2, detalhe!.FuncionariosSelecionados.Count);
        Assert.Equal("001", detalhe.FuncionariosSelecionados[0].Matricula);
        Assert.Empty(detalhe.Participantes);
        Assert.Equal(0, detalhe.Dds.TotalParticipantes);
        Assert.Empty(await db.DocumentoSignatarios.ToListAsync());
    }

    [Fact]
    public async Task EditarLista_RemoveReincluiESalvaSemDuplicar_NemAlterarOutroDds()
    {
        await using var db = CriarDb();
        var (dds, ana, bruno, _) = await Semear(db);
        var outroDds = new DdsEntidade { ObraId = dds.ObraId };
        db.Dds.Add(outroDds);
        db.DdsFuncionariosSelecionados.Add(new() { DdsId = outroDds.Id, TrabalhadorId = ana.Id });
        await db.SaveChangesAsync();
        var handler = new AtualizarFuncionariosDdsCommandHandler(db);

        await handler.Handle(new(dds.Id, new() { ana.Id, ana.Id, bruno.Id }), default);
        Assert.Equal(2, await db.DdsFuncionariosSelecionados.CountAsync(s => s.DdsId == dds.Id));
        await handler.Handle(new(dds.Id, new() { bruno.Id }), default);
        Assert.Equal(bruno.Id, (await db.DdsFuncionariosSelecionados.SingleAsync(s => s.DdsId == dds.Id)).TrabalhadorId);
        await handler.Handle(new(dds.Id, new() { ana.Id, bruno.Id }), default);
        Assert.Equal(2, await db.DdsFuncionariosSelecionados.CountAsync(s => s.DdsId == dds.Id));
        await handler.Handle(new(dds.Id, new()), default);
        Assert.Empty(await db.DdsFuncionariosSelecionados.Where(s => s.DdsId == dds.Id).ToListAsync());
        Assert.Equal(ana.Id, (await db.DdsFuncionariosSelecionados.SingleAsync(s => s.DdsId == outroDds.Id)).TrabalhadorId);
    }

    [Theory]
    [InlineData("outra-obra")]
    [InlineData("inexistente")]
    [InlineData("desligado")]
    public async Task LoteInvalido_ERecusadoInteiroPreservandoSelecaoAnterior(string caso)
    {
        await using var db = CriarDb();
        var (dds, ana, bruno, outro) = await Semear(db);
        var handler = new AtualizarFuncionariosDdsCommandHandler(db);
        await handler.Handle(new(dds.Id, new() { ana.Id }), default);
        bruno.Situacao = SituacaoTrabalhador.Desligado;
        await db.SaveChangesAsync();
        var invalido = caso == "outra-obra" ? outro.Id : caso == "desligado" ? bruno.Id : Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new(dds.Id, new() { invalido }), default));
        Assert.Equal(ana.Id, (await db.DdsFuncionariosSelecionados.SingleAsync()).TrabalhadorId);
    }

    [Fact]
    public async Task LimparSelecao_PreservaPresencaEAssinaturaConfirmadas()
    {
        await using var db = CriarDb();
        var (dds, ana, bruno, _) = await Semear(db);
        var handler = new AtualizarFuncionariosDdsCommandHandler(db);
        await handler.Handle(new(dds.Id, new() { ana.Id, bruno.Id }), default);
        var presenca = new DdsParticipante { DdsId = dds.Id, TrabalhadorId = ana.Id, FotoTipo = TipoFotoParticipante.Biometria };
        var documento = new DocumentoAssinatura { EntidadeId = dds.Id, EntidadeTipo = "Dds" };
        var assinatura = new DocumentoSignatario { DocumentoAssinatura = documento, DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = ana.Id, AssinadoEm = DateTime.UtcNow };
        db.AddRange(presenca, assinatura);
        await db.SaveChangesAsync();

        await handler.Handle(new(dds.Id, new()), default);
        Assert.Empty(await db.DdsFuncionariosSelecionados.ToListAsync());
        Assert.Equal(presenca.Id, (await db.DdsParticipantes.SingleAsync()).Id);
        Assert.Equal(assinatura.Id, (await db.DocumentoSignatarios.SingleAsync()).Id);
    }

    [Theory]
    [InlineData("concluido")]
    [InlineData("sem-expediente")]
    [InlineData("semana-encerrada")]
    public async Task DdsFechado_NaoPermiteAlterarSelecaoNemRegistrarPresenca(string caso)
    {
        await using var db = CriarDb();
        var (dds, ana, _, _) = await Semear(db);
        if (caso == "concluido") dds.Status = StatusDds.Concluido;
        if (caso == "sem-expediente") dds.SemExpediente = true;
        if (caso == "semana-encerrada")
        {
            dds.DdsSemanal = new DdsSemanal { ObraId = dds.ObraId, Status = StatusDdsSemanal.Concluida };
            db.DdsSemanais.Add(dds.DdsSemanal);
        }
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new AtualizarFuncionariosDdsCommandHandler(db)
            .Handle(new(dds.Id, new() { ana.Id }), default));
        var presenca = new RegistrarParticipanteCommandHandler(db, null!, null!, null!, NullLogger<RegistrarParticipanteCommandHandler>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenca.Handle(new(dds.Id, ana.Id, Guid.NewGuid(), "teste", 90), default));
        Assert.Empty(await db.DdsParticipantes.ToListAsync());
    }

    [Fact]
    public async Task RegistrarPresenca_RecusaFuncionarioDeOutraObraAntesDaBiometria()
    {
        await using var db = CriarDb();
        var (dds, _, _, outro) = await Semear(db);
        var presenca = new RegistrarParticipanteCommandHandler(db, null!, null!, null!, NullLogger<RegistrarParticipanteCommandHandler>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenca.Handle(new(dds.Id, outro.Id, Guid.NewGuid(), "teste", 90), default));
    }

    [Fact]
    public async Task EscopoDoUsuario_ProtegeConsultaEEdicaoDaSelecao()
    {
        var usuario = new CurrentUserService();
        await using var db = CriarDb(usuario);
        var (dds, ana, _, outro) = await Semear(db);
        await new AtualizarFuncionariosDdsCommandHandler(db).Handle(new(dds.Id, new() { ana.Id }), default);
        usuario.DefinirEscopo(false, new[] { outro.ObraId });
        db.ChangeTracker.Clear();

        Assert.Empty(await db.DdsFuncionariosSelecionados.ToListAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ListarFuncionariosDdsQueryHandler(db).Handle(new(dds.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AtualizarFuncionariosDdsCommandHandler(db).Handle(new(dds.Id, new()), default));
    }
}
