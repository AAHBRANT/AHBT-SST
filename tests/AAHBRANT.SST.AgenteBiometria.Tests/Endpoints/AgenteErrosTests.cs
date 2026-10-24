using AAHBRANT.SST.AgenteBiometria.Endpoints;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Endpoints;

public class AgenteErrosTests
{
    [Fact]
    public void Traduzir_TimeoutDoLeitor_Retorna408ComMensagem()
    {
        var r = AgenteErros.Traduzir(new TimeoutException("Nenhum dedo detectado."));

        Assert.Equal(408, r!.Value.Status);
        Assert.Equal("Nenhum dedo detectado.", r.Value.Mensagem);
    }

    [Fact]
    public void Traduzir_LeitorAusenteOuEmUso_Retorna503()
    {
        var r = AgenteErros.Traduzir(new InvalidOperationException("Leitor Futronic não encontrado."));

        Assert.Equal(503, r!.Value.Status);
    }

    [Fact]
    public void Traduzir_SdkAusente_Retorna503ComOrientacao()
    {
        var r = AgenteErros.Traduzir(new DllNotFoundException());

        Assert.Equal(503, r!.Value.Status);
        Assert.Contains("ftrScanAPI.dll", r.Value.Mensagem);
    }

    [Fact]
    public void Traduzir_ImagemInvalida_Retorna422SemNomeDoParametro()
    {
        var r = AgenteErros.Traduzir(new ArgumentException("Imagem com 4 bytes.", "capturaBruta"));

        Assert.Equal(422, r!.Value.Status);
        Assert.Equal("Imagem com 4 bytes.", r.Value.Mensagem);
    }

    [Fact]
    public void Traduzir_ErroDesconhecido_NaoTraduz()
    {
        Assert.Null(AgenteErros.Traduzir(new NullReferenceException()));
    }
}
