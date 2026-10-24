using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class CadastroFacialAlertaProviderTests
{
    [Fact]
    public async Task TresFalhasDepoisDoCadastro_GeraAlertaDeRefazerCadastro()
    {
        using var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Ponte" };
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Dailton", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow };
        db.Obras.Add(obra);
        db.Trabalhadores.Add(trabalhador);
        db.FotosCadastroFacial.Add(new FotoCadastroFacial { TrabalhadorId = trabalhador.Id, Conteudo = new byte[] { 1 }, ContentType = "image/jpeg", HashSha256 = "h", CapturadaEm = DateTime.UtcNow.AddDays(-10) });
        for (var i = 0; i < 3; i++)
            db.FalhasReconhecimentoFacial.Add(new FalhaReconhecimentoFacial { ObraId = obra.Id, TrabalhadorId = trabalhador.Id, Motivo = MotivoFalhaFacial.ConfiancaBaixa, Confianca = 0.75, OcorridaEm = DateTime.UtcNow.AddDays(-i) });
        await db.SaveChangesAsync();

        var itens = await new CadastroFacialAlertaProvider(db).ObterItensAsync();

        var item = Assert.Single(itens);
        Assert.Equal(trabalhador.Id, item.EntidadeOrigemId);
        Assert.Equal(TipoAlerta.CadastroFacialFraco, item.TipoAlertaVencido);
        Assert.Contains("Dailton", item.Titulo);
        Assert.False(item.Substituido);
    }

    [Fact]
    public async Task ListaDeFalhas_TrazConfiancaEmPercentual()
    {
        using var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Ponte" };
        db.Obras.Add(obra);
        db.FalhasReconhecimentoFacial.Add(new FalhaReconhecimentoFacial { ObraId = obra.Id, Motivo = MotivoFalhaFacial.RostoNaoReconhecido, Confianca = 0.5293, OcorridaEm = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var falha = Assert.Single(await new ListarFalhasReconhecimentoFacialQueryHandler(db).Handle(new ListarFalhasReconhecimentoFacialQuery(null), default));

        Assert.Equal(52.9, falha.ConfiancaPercentual);
        Assert.Equal("Ponte", falha.ObraNome);
        Assert.Null(falha.TrabalhadorNome);
    }
}
