using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class CriarDdsCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Obra Obra, Usuario Usuario, DdsSemanal Semanal, Atividade AtividadeComRisco, Atividade AtividadeSemRisco)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico Teste" };
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        var semanal = new DdsSemanal
        {
            ObraId = obra.Id,
            ResponsavelUsuarioId = usuario.Id,
            DataInicioSemana = new DateTime(2026, 9, 7),
            DataFimSemana = new DateTime(2026, 9, 11),
        };
        var atividadeComRisco = new Atividade { ObraId = obra.Id, Nome = "Montagem de andaime" };
        var atividadeSemRisco = new Atividade { ObraId = obra.Id, Nome = "Limpeza do canteiro" };
        db.DdsSemanais.Add(semanal);
        db.Atividades.AddRange(atividadeComRisco, atividadeSemRisco);
        await db.SaveChangesAsync();

        var perigo = new Perigo { Nome = "Queda de altura", Descricao = "Trabalho acima de 2m sem proteção de borda" };
        db.Perigos.Add(perigo);
        await db.SaveChangesAsync();

        db.Riscos.Add(new Risco
        {
            AtividadeId = atividadeComRisco.Id,
            PerigoId = perigo.Id,
            Consequencia = "Fratura, óbito",
            Probabilidade = 3,
            Severidade = 5,
            NivelRisco = NivelRisco.Alto,
            ControlesExistentes = "Uso de cinto tipo paraquedista\nAncoragem dupla",
            ControlesAdicionais = "Inspeção do cinto antes de cada uso",
        });
        await db.SaveChangesAsync();

        return (obra, usuario, semanal, atividadeComRisco, atividadeSemRisco);
    }

    [Fact]
    public async Task Handle_AtividadeComRisco_GravaSnapshotDoMaiorRiscoNaDdsAtividade()
    {
        var db = CriarDb(nameof(Handle_AtividadeComRisco_GravaSnapshotDoMaiorRiscoNaDdsAtividade));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, null), default);

        var ddsAtividade = await db.DdsAtividades.Include(a => a.Dds).FirstAsync(a => a.Dds!.Id == id);
        Assert.Equal("Queda de altura", ddsAtividade.PerigoNome);
        Assert.Equal("Trabalho acima de 2m sem proteção de borda", ddsAtividade.PerigoDescricao);
        Assert.Equal("Fratura, óbito", ddsAtividade.Consequencia);
        Assert.Equal("Uso de cinto tipo paraquedista\nAncoragem dupla", ddsAtividade.ControlesExistentes);
        Assert.Equal("Inspeção do cinto antes de cada uso", ddsAtividade.ControlesAdicionais);
    }

    [Fact]
    public async Task Handle_AtividadeSemRisco_GravaSnapshotNuloSemQuebrar()
    {
        var db = CriarDb(nameof(Handle_AtividadeSemRisco_GravaSnapshotNuloSemQuebrar));
        var (_, _, semanal, _, atividadeSemRisco) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeSemRisco.Id }, semanal.DataInicioSemana, null), default);

        var ddsAtividade = await db.DdsAtividades.Include(a => a.Dds).FirstAsync(a => a.Dds!.Id == id);
        Assert.Null(ddsAtividade.PerigoNome);
    }

    [Fact]
    public async Task Handle_DuasAtividades_GravaUmaDdsAtividadePorAtividadeMarcada()
    {
        var db = CriarDb(nameof(Handle_DuasAtividades_GravaUmaDdsAtividadePorAtividadeMarcada));
        var (_, _, semanal, atividadeComRisco, atividadeSemRisco) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id, atividadeSemRisco.Id }, semanal.DataInicioSemana, null), default);

        var ddsAtividades = await db.DdsAtividades.Where(a => a.DdsId == id).ToListAsync();
        Assert.Equal(2, ddsAtividades.Count);
    }

    [Fact]
    public async Task Handle_ComTemaLivre_CopiaNomeEDescricaoDoCatalogoParaODds()
    {
        var db = CriarDb(nameof(Handle_ComTemaLivre_CopiaNomeEDescricaoDoCatalogoParaODds));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var tema = new CatalogoTemaDds { Nome = "Outubro Amarelo", Descricao = "Prevenção ao suicídio" };
        db.CatalogosTemaDds.Add(tema);
        await db.SaveChangesAsync();
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, tema.Id), default);

        var dds = await db.Dds.FirstAsync(d => d.Id == id);
        Assert.Equal(tema.Id, dds.CatalogoTemaDdsId);
        Assert.Equal("Outubro Amarelo", dds.TemaLivreNome);
        Assert.Equal("Prevenção ao suicídio", dds.TemaLivreDescricao);
    }

    [Fact]
    public async Task Handle_SemTemaLivre_CriaDdsComTemaLivreNulo()
    {
        var db = CriarDb(nameof(Handle_SemTemaLivre_CriaDdsComTemaLivreNulo));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, null), default);

        var dds = await db.Dds.FirstAsync(d => d.Id == id);
        Assert.Null(dds.CatalogoTemaDdsId);
        Assert.Null(dds.TemaLivreNome);
    }

    [Fact]
    public async Task Handle_CatalogoTemaDdsInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_CatalogoTemaDdsInexistente_LancaKeyNotFoundException));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_CriaDds_GeraNumeroDocumentoComPrefixoDDS_D()
    {
        var db = CriarDb(nameof(Handle_CriaDds_GeraNumeroDocumentoComPrefixoDDS_D));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, null), default);

        var dds = await db.Dds.FirstAsync(d => d.Id == id);
        Assert.StartsWith("DDS-D-", dds.NumeroDocumento);
    }
}
