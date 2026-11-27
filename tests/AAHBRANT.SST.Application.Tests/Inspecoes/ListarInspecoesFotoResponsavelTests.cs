using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

// Foto do responsável na lista de inspeções (25/11/2026): a lista diz se o usuário responsável está
// vinculado a um funcionário com foto; a tela baixa a foto pelo funcionário ou mostra as iniciais.
public class ListarInspecoesFotoResponsavelTests
{
    [Fact]
    public async Task Listar_InformaFuncionarioEFotoDoResponsavel()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var comFoto = new Trabalhador { Nome = "Ana Lima", ObraId = obra.Id, FotoConteudo = new byte[] { 1, 2, 3 }, FotoContentType = "image/jpeg" };
        var semFoto = new Trabalhador { Nome = "Bruno Silva", ObraId = obra.Id };
        var ana = new Usuario { Email = "ana@x.com", Nome = "Ana Lima", TrabalhadorId = comFoto.Id };
        var bruno = new Usuario { Email = "bruno@x.com", Nome = "Bruno Silva", TrabalhadorId = semFoto.Id };
        var semVinculo = new Usuario { Email = "c@x.com", Nome = "Carla" };
        var checklist = new ChecklistModelo { Nome = "Checklist de Obra", TipoInspecao = TipoInspecao.Obra, Versao = 1 };
        db.AddRange(obra, comFoto, semFoto, ana, bruno, semVinculo, checklist);
        Inspecao Nova(Usuario u) => new()
        {
            ObraId = obra.Id, TipoInspecao = TipoInspecao.Obra, Status = StatusInspecao.EmAndamento,
            Data = DateTime.UtcNow, ChecklistModeloId = checklist.Id, ResponsavelUsuarioId = u.Id,
        };
        db.AddRange(Nova(ana), Nova(bruno), Nova(semVinculo));
        await db.SaveChangesAsync();

        var lista = await new ListarInspecoesQueryHandler(db).Handle(new ListarInspecoesQuery(), default);

        var daAna = lista.Single(i => i.ResponsavelUsuarioId == ana.Id);
        Assert.Equal(comFoto.Id, daAna.ResponsavelTrabalhadorId);
        Assert.True(daAna.ResponsavelTemFoto);

        var doBruno = lista.Single(i => i.ResponsavelUsuarioId == bruno.Id);
        Assert.Equal(semFoto.Id, doBruno.ResponsavelTrabalhadorId);
        Assert.False(doBruno.ResponsavelTemFoto);

        var daCarla = lista.Single(i => i.ResponsavelUsuarioId == semVinculo.Id);
        Assert.Null(daCarla.ResponsavelTrabalhadorId);
        Assert.False(daCarla.ResponsavelTemFoto);
    }
}
