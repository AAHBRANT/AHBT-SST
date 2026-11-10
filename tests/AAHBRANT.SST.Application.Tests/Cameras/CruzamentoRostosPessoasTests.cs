using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Cameras;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class CruzamentoRostosPessoasTests
{
    private static readonly Guid Joao = Guid.NewGuid();
    private static readonly Guid Pedro = Guid.NewGuid();

    // Rosto de 6% x 10% da imagem centrado em (cx, cy).
    private static RostoNaCena Rosto(double cx, double cy, Guid? trabalhador)
        => new(cx - 0.03, cy - 0.05, 0.06, 0.10, trabalhador, trabalhador is null ? null : 90);

    [Fact]
    public void DuasPessoasAfastadas_CadaRostoVaiParaAPessoaCerta()
    {
        var pessoas = new[] { new PessoaNaCena(0.25, 0.30, true, false), new PessoaNaCena(0.75, 0.32, false, false) };
        var rostos = new[] { Rosto(0.74, 0.33, Pedro), Rosto(0.26, 0.31, Joao) };

        var r = CruzamentoRostosPessoas.Cruzar(pessoas, rostos);

        Assert.Equal(Joao, r[0].TrabalhadorId);
        Assert.Equal(Pedro, r[1].TrabalhadorId);
    }

    [Fact]
    public void RostoNaoReconhecido_NaoEntraNoCruzamento()
    {
        var pessoas = new[] { new PessoaNaCena(0.25, 0.30, true, false), new PessoaNaCena(0.75, 0.32, true, false) };
        var rostos = new[] { Rosto(0.25, 0.30, null), Rosto(0.75, 0.32, Pedro) };

        var r = CruzamentoRostosPessoas.Cruzar(pessoas, rostos);

        Assert.False(r.ContainsKey(0));
        Assert.Equal(Pedro, r[1].TrabalhadorId);
    }

    [Fact]
    public void RostoLongeDeQualquerCabeca_FicaSemPessoa()
    {
        var pessoas = new[] { new PessoaNaCena(0.20, 0.30, true, false), new PessoaNaCena(0.80, 0.30, true, false) };

        var r = CruzamentoRostosPessoas.Cruzar(pessoas, new[] { Rosto(0.50, 0.80, Joao) });

        Assert.Empty(r);
    }

    [Fact]
    public void DuasPessoasJuntasComUmRostoNoMeio_NaoLigaNinguem()
    {
        var pessoas = new[] { new PessoaNaCena(0.48, 0.30, true, false), new PessoaNaCena(0.54, 0.30, false, false) };

        var r = CruzamentoRostosPessoas.Cruzar(pessoas, new[] { Rosto(0.51, 0.30, Joao) });

        Assert.Empty(r);
    }

    [Fact]
    public void DoisRostosParaAMesmaPessoa_NaoLigaNinguemNela()
    {
        var pessoas = new[] { new PessoaNaCena(0.30, 0.30, true, false), new PessoaNaCena(0.80, 0.30, true, false) };
        var rostos = new[] { Rosto(0.29, 0.30, Joao), Rosto(0.32, 0.31, Pedro) };

        var r = CruzamentoRostosPessoas.Cruzar(pessoas, rostos);

        Assert.Empty(r);
    }

    [Fact]
    public void PessoaRegularProxima_ImpedeLigarORostoAoIrregularVizinho()
    {
        // O rosto está em cima da pessoa regular; o irregular ao lado não pode herdar o nome dela.
        var pessoas = new[] { new PessoaNaCena(0.40, 0.30, true, false), new PessoaNaCena(0.50, 0.30, false, false) };

        var r = CruzamentoRostosPessoas.Cruzar(pessoas, new[] { Rosto(0.50, 0.30, Pedro) });

        Assert.False(r.ContainsKey(0));
        Assert.Equal(Pedro, r[1].TrabalhadorId);
    }
}
