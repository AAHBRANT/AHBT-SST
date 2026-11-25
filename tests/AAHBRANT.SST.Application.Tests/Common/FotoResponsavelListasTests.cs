using AAHBRANT.SST.Application.Aprs.Queries;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Application.PermissoesTrabalho.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Common;

// Foto do responsável nas listas de DDS, APR e PT (25/11/2026): cada lista diz o funcionário e se ele
// tem foto; sem vínculo ou sem foto, a tela mostra as iniciais.
public class FotoResponsavelListasTests
{
    private static (AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, Obra Obra, Atividade Atividade, Trabalhador ComFoto, Trabalhador SemFoto, Usuario Ana, Usuario Carla) Cenario()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var atividade = new Atividade { ObraId = obra.Id, Nome = "Armação" };
        var comFoto = new Trabalhador { Nome = "Ana Lima", ObraId = obra.Id, FotoConteudo = new byte[] { 1 }, FotoContentType = "image/jpeg" };
        var semFoto = new Trabalhador { Nome = "Bruno Silva", ObraId = obra.Id };
        var ana = new Usuario { Email = "ana@x.com", Nome = "Ana Lima", TrabalhadorId = comFoto.Id };
        var carla = new Usuario { Email = "c@x.com", Nome = "Carla" };
        db.AddRange(obra, atividade, comFoto, semFoto, ana, carla);
        return (db, obra, atividade, comFoto, semFoto, ana, carla);
    }

    [Fact]
    public async Task DdsSemanal_InformaFotoDoResponsavelEOsDiasRegistrados()
    {
        var (db, obra, _, comFoto, _, ana, carla) = Cenario();
        var segunda = new DateTime(2026, 11, 23);
        var semanaAna = new DdsSemanal { ObraId = obra.Id, ResponsavelUsuarioId = ana.Id, DataInicioSemana = segunda, DataFimSemana = segunda.AddDays(4) };
        var semanaCarla = new DdsSemanal { ObraId = obra.Id, ResponsavelUsuarioId = carla.Id, DataInicioSemana = segunda.AddDays(-7), DataFimSemana = segunda.AddDays(-3) };
        db.AddRange(semanaAna, semanaCarla);
        db.AddRange(
            new AAHBRANT.SST.Domain.Entidades.Dds { ObraId = obra.Id, DdsSemanal = semanaAna, Data = segunda.AddDays(2), Status = StatusDds.Concluido, ResponsavelUsuarioId = ana.Id },
            new AAHBRANT.SST.Domain.Entidades.Dds { ObraId = obra.Id, DdsSemanal = semanaAna, Data = segunda, Status = StatusDds.EmAndamento, ResponsavelUsuarioId = ana.Id });
        await db.SaveChangesAsync();

        var lista = await new ListarDdsSemanaisQueryHandler(db).Handle(new ListarDdsSemanaisQuery(), default);

        var daAna = lista.Single(s => s.Id == semanaAna.Id);
        Assert.Equal(comFoto.Id, daAna.ResponsavelTrabalhadorId);
        Assert.True(daAna.ResponsavelTemFoto);
        Assert.Equal(new[] { (segunda, false), (segunda.AddDays(2), true) }, daAna.DiasResumo.Select(d => (d.Data, d.Concluido)));

        var daCarla = lista.Single(s => s.Id == semanaCarla.Id);
        Assert.Null(daCarla.ResponsavelTrabalhadorId);
        Assert.False(daCarla.ResponsavelTemFoto);
    }

    [Fact]
    public async Task Apr_InformaNomeEFotoDeQuemElaboraESupervisiona()
    {
        var (db, _, atividade, comFoto, semFoto, _, _) = Cenario();
        var apr = new Apr { NumeroApr = "APR-1", AtividadeId = atividade.Id, Local = "L", Data = DateTime.UtcNow, ElaboradorTrabalhadorId = comFoto.Id, SupervisorTrabalhadorId = semFoto.Id };
        db.Add(apr);
        await db.SaveChangesAsync();

        var dto = (await new ListarAprsQueryHandler(db).Handle(new ListarAprsQuery(), default)).Single();

        Assert.Equal("Ana Lima", dto.ElaboradorNome);
        Assert.True(dto.ElaboradorTemFoto);
        Assert.Equal("Bruno Silva", dto.SupervisorNome);
    }

    [Fact]
    public async Task Pt_InformaResponsavelPelaExecucaoComFoto()
    {
        var (db, _, atividade, comFoto, _, ana, _) = Cenario();
        db.Add(new PermissaoTrabalho { NumeroPt = "PT-1", AtividadeId = atividade.Id, DescricaoAtividade = "D", Local = "L", Data = DateTime.UtcNow, ResponsavelExecucaoUsuarioId = ana.Id });
        db.Add(new PermissaoTrabalho { NumeroPt = "PT-2", AtividadeId = atividade.Id, DescricaoAtividade = "D", Local = "L", Data = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var lista = await new ListarPermissoesTrabalhoQueryHandler(db).Handle(new ListarPermissoesTrabalhoQuery(), default);

        var comResponsavel = lista.Single(p => p.NumeroPt == "PT-1");
        Assert.Equal("Ana Lima", comResponsavel.ResponsavelExecucaoUsuarioNome);
        Assert.Equal(comFoto.Id, comResponsavel.ResponsavelExecucaoTrabalhadorId);
        Assert.True(comResponsavel.ResponsavelExecucaoTemFoto);

        var semResponsavel = lista.Single(p => p.NumeroPt == "PT-2");
        Assert.Null(semResponsavel.ResponsavelExecucaoUsuarioNome);
        Assert.False(semResponsavel.ResponsavelExecucaoTemFoto);
    }
}
