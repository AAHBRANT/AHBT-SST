using AAHBRANT.SST.Application.Cipa.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cipa;

public class ObterSituacaoCipaPorObraQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nome)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static Obra NovaObra(string nome) => new() { Nome = nome, Codigo = nome, Status = StatusObra.EmAndamento };

    private static void AdicionarTrabalhadores(IAppDbContext db, Guid obraId, int quantidade, TipoVinculo vinculo = TipoVinculo.Clt, bool demitidos = false)
    {
        for (var i = 0; i < quantidade; i++)
            db.Trabalhadores.Add(new Trabalhador
            {
                ObraId = obraId,
                FuncaoId = Guid.NewGuid(),
                Nome = $"T{i}",
                Cpf = Guid.NewGuid().ToString("N")[..11],
                Vinculo = vinculo,
                DataAdmissao = DateTime.UtcNow.AddYears(-1),
                DataDemissao = demitidos ? DateTime.UtcNow.AddDays(-1) : null,
            });
    }

    [Fact]
    public async Task Handle_ContaSoCltAtivos_EClassificaCadaObra()
    {
        var db = CriarDb(nameof(Handle_ContaSoCltAtivos_EClassificaCadaObra));
        var grande = NovaObra("Grande");
        var pequena = NovaObra("Pequena");
        var concluida = NovaObra("Concluida");
        concluida.Status = StatusObra.Concluida;
        db.Obras.AddRange(grande, pequena, concluida);
        await db.SaveChangesAsync();

        AdicionarTrabalhadores(db, grande.Id, 23);
        AdicionarTrabalhadores(db, grande.Id, 4, demitidos: true);
        AdicionarTrabalhadores(db, grande.Id, 6, TipoVinculo.Terceirizado);
        AdicionarTrabalhadores(db, pequena.Id, 12);
        AdicionarTrabalhadores(db, pequena.Id, 3, TipoVinculo.Estagiario);
        await db.SaveChangesAsync();

        var resultado = await new ObterSituacaoCipaPorObraQueryHandler(db).Handle(new ObterSituacaoCipaPorObraQuery(), default);

        Assert.Equal(2, resultado.Count);
        var g = resultado.Single(r => r.ObraId == grande.Id);
        Assert.Equal(23, g.EmpregadosAtivos);
        Assert.Equal(6, g.TerceirizadosAtivos);
        Assert.True(g.TerceirosExigemRepresentante);
        Assert.Equal(SituacaoCipaObraTipo.PrecisaDeCipa, g.Situacao);

        var p = resultado.Single(r => r.ObraId == pequena.Id);
        Assert.Equal(12, p.EmpregadosAtivos);
        Assert.Equal(SituacaoCipaObraTipo.NomearRepresentante, p.Situacao);

        Assert.Equal(grande.Id, resultado[0].ObraId); // mais urgente primeiro
    }

    [Theory]
    [InlineData(25, 0, false, null, SituacaoCipaObraTipo.PrecisaDeCipa)]
    [InlineData(25, 0, true, null, SituacaoCipaObraTipo.EleicaoEmAndamento)]
    [InlineData(25, 4, false, 200, SituacaoCipaObraTipo.CipaAtiva)]
    [InlineData(25, 4, false, 45, SituacaoCipaObraTipo.ConvocarEleicao)]
    [InlineData(19, 1, false, 200, SituacaoCipaObraTipo.RepresentanteNomeado)]
    [InlineData(19, 0, false, null, SituacaoCipaObraTipo.NomearRepresentante)]
    public void Classificar_AplicaPisoDe20EPrazoDe60Dias(int empregados, int membros, bool processo, int? diasParaFim, SituacaoCipaObraTipo esperado)
    {
        var hoje = new DateTime(2026, 11, 16);
        DateTime? fim = diasParaFim.HasValue ? hoje.AddDays(diasParaFim.Value) : null;
        Assert.Equal(esperado, ObterSituacaoCipaPorObraQueryHandler.Classificar(empregados, membros, processo, fim, hoje));
    }

    [Theory]
    [InlineData(100, 105, false)]
    [InlineData(100, 115, true)]
    [InlineData(19, 20, true)]
    [InlineData(0, 3, true)]
    public void DimensionamentoDesatualizado_DetectaVariacaoOuCruzamentoDoPiso(int dimensionados, int atuais, bool esperado)
        => Assert.Equal(esperado, ObterSituacaoCipaPorObraQueryHandler.DimensionamentoDesatualizado(dimensionados, atuais));
}
