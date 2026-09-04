using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class ExportarDdsPdfQueryMontarModeloTests
{
    private static readonly RastreabilidadeDocumentoResultado RastreioFake = new("hash", "https://validar.teste", Array.Empty<byte>(), false);

    private static DdsDetalheDto CriarDetalhe(List<DdsTemaAtividadeDto> temas, string? temaLivreNome, string? temaLivreDescricao)
    {
        return new DdsDetalheDto
        {
            Dds = new DdsDto
            {
                ObraNome = "Obra Teste",
                Data = new DateTime(2026, 9, 1),
                ResponsavelUsuarioNome = "Técnico Teste",
                TemasAtividades = temas,
                TemaLivreNome = temaLivreNome,
                TemaLivreDescricao = temaLivreDescricao,
            },
        };
    }

    [Fact]
    public void MontarModelo_AtividadeComRisco_MapeiaTodosOsCamposDoTema()
    {
        var temas = new List<DdsTemaAtividadeDto>
        {
            new()
            {
                AtividadeId = Guid.NewGuid(),
                AtividadeNome = "Montagem de andaime",
                PerigoNome = "Queda de altura",
                PerigoDescricao = "Trabalho acima de 2m sem proteção de borda",
                Consequencia = "Fratura, óbito",
                ControlesExistentes = "Uso de cinto tipo paraquedista",
                ControlesAdicionais = "Inspeção do cinto antes de cada uso",
            },
        };
        var detalhe = CriarDetalhe(temas, null, null);

        var modelo = ExportarDdsPdfQueryHandler.MontarModelo(detalhe, null, null, RastreioFake);

        var tema = Assert.Single(modelo.Temas);
        Assert.Equal("Montagem de andaime", tema.AtividadeNome);
        Assert.Equal("Queda de altura", tema.PerigoNome);
        Assert.Equal("Trabalho acima de 2m sem proteção de borda", tema.PerigoDescricao);
        Assert.Equal("Fratura, óbito", tema.Consequencia);
        Assert.Equal("Uso de cinto tipo paraquedista", tema.ControlesExistentes);
        Assert.Equal("Inspeção do cinto antes de cada uso", tema.ControlesAdicionais);
        Assert.Null(modelo.TemaLivreNome);
    }

    [Fact]
    public void MontarModelo_AtividadeSemRisco_MapeiaPerigoNomeNulo()
    {
        var temas = new List<DdsTemaAtividadeDto>
        {
            new() { AtividadeId = Guid.NewGuid(), AtividadeNome = "Limpeza do canteiro" },
        };
        var detalhe = CriarDetalhe(temas, null, null);

        var modelo = ExportarDdsPdfQueryHandler.MontarModelo(detalhe, null, null, RastreioFake);

        var tema = Assert.Single(modelo.Temas);
        Assert.Equal("Limpeza do canteiro", tema.AtividadeNome);
        Assert.Null(tema.PerigoNome);
    }

    [Fact]
    public void MontarModelo_ComTemaLivre_MapeiaNomeEDescricao()
    {
        var detalhe = CriarDetalhe(new List<DdsTemaAtividadeDto>(), "Outubro Amarelo", "Prevenção ao suicídio");

        var modelo = ExportarDdsPdfQueryHandler.MontarModelo(detalhe, null, null, RastreioFake);

        Assert.Equal("Outubro Amarelo", modelo.TemaLivreNome);
        Assert.Equal("Prevenção ao suicídio", modelo.TemaLivreDescricao);
    }
}
