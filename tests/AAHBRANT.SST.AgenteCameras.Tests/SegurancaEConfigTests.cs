using System.Text.Json.Nodes;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Tests;

public class MascaradorTests
{
    [Fact]
    public void MascaraPasswordNaUrlRtspPadrao()
    {
        var url = "rtsp://192.168.0.8:554/user=admin&password=MinhaSenha&channel=1&stream=1.sdp?real_stream";
        var m = new Mascarador().Mascarar(url);
        Assert.DoesNotContain("MinhaSenha", m);
        Assert.Contains("password=***&channel=1", m);
    }

    [Fact]
    public void MascaraUsuarioESenhaNoFormatoUserSenhaArroba()
    {
        var m = new Mascarador().Mascarar("Connection to rtsp://admin:S3nh4!@192.168.0.8:554/ch1 failed");
        Assert.DoesNotContain("S3nh4", m);
        Assert.Contains("rtsp://admin:***@192.168.0.8", m);
    }

    [Fact]
    public void MascaraSenhaVaziaEMaiusculas()
    {
        Assert.Equal("x PASSWORD=***&y", new Mascarador().Mascarar("x PASSWORD=&y"));
    }

    [Fact]
    public void MascaraValoresRegistradosEmQualquerLugarInclusiveEscapados()
    {
        var m = new Mascarador();
        m.Registrar("p&ss w0rd");
        var saida = m.Mascarar("erro com p&ss w0rd e com p%26ss%20w0rd no texto");
        Assert.DoesNotContain("w0rd", saida);
        Assert.DoesNotContain("p%26ss", saida);
    }

    [Fact]
    public void IgnoraSegredoCurtoDemaisParaNaoMascararTextoComum()
    {
        var m = new Mascarador();
        m.Registrar("ab");
        Assert.Equal("abacate", m.Mascarar("abacate"));
    }

    [Fact]
    public void SaidaDeErroDoFfmpegNaoVazaSenha()
    {
        var camera = new CameraConfig("c", "n", "192.168.0.8", 554, "admin", "Abc#123&x", 1, 1, null, TimeSpan.FromSeconds(3));
        var m = new Mascarador();
        m.Registrar(camera.Senha);
        var url = UrlRtsp.Montar(camera);
        var linhas = new[]
        {
            $"[rtsp @ 000001] method DESCRIBE failed: 401 Unauthorized for {url}",
            $"{url}: Server returned 401 Unauthorized (authorization failed)",
            $"Error opening input: rtsp://admin:{camera.Senha}@192.168.0.8:554/x",
        };
        foreach (var linha in linhas)
        {
            var limpa = m.Mascarar(linha);
            Assert.DoesNotContain("Abc", limpa);
            Assert.DoesNotContain("123", limpa.Replace("000001", ""));
        }
    }
}

public class SegredosConfiguracaoTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new();
    public void Dispose() => _pasta.Dispose();

    private string Arquivo(string json)
    {
        var caminho = Path.Combine(_pasta.Caminho, "appsettings.local.json");
        File.WriteAllText(caminho, json);
        return caminho;
    }

    private const string Exemplo = """
        { "AgenteCameras": { "ApiBaseUrl": "https://x", "Segredo": "segredo-claro",
          "Cameras": [ { "CameraId": "a", "Senha": "senhaA" }, { "CameraId": "b", "Senha": "" }, { "CameraId": "c", "Senha": "senhaC" } ] } }
        """;

    [Fact]
    public void RegravaSegredoESenhasEmTextoComPrefixoEPreservaOResto()
    {
        var caminho = Arquivo(Exemplo);
        var protetor = new ProtetorFalso();
        Assert.Equal(3, SegredosConfiguracao.ProtegerArquivo(caminho, protetor));

        var texto = File.ReadAllText(caminho);
        Assert.DoesNotContain("segredo-claro", texto);
        Assert.DoesNotContain("senhaA", texto);
        Assert.DoesNotContain("senhaC", texto);

        var raiz = JsonNode.Parse(texto)!["AgenteCameras"]!;
        Assert.StartsWith(SegredosConfiguracao.Prefixo, (string)raiz["Segredo"]!);
        Assert.Equal("https://x", (string)raiz["ApiBaseUrl"]!);
        Assert.Equal("", (string)raiz["Cameras"]![1]!["Senha"]!); // vazia continua vazia
        Assert.Equal("senhaA", SegredosConfiguracao.Resolver((string)raiz["Cameras"]![0]!["Senha"]!, protetor));
        Assert.Equal("segredo-claro", SegredosConfiguracao.Resolver((string)raiz["Segredo"]!, protetor));
    }

    [Fact]
    public void SegundaPassadaNaoMexeEmValorJaProtegido()
    {
        var caminho = Arquivo(Exemplo);
        var protetor = new ProtetorFalso();
        SegredosConfiguracao.ProtegerArquivo(caminho, protetor);
        var antes = File.ReadAllText(caminho);
        Assert.Equal(0, SegredosConfiguracao.ProtegerArquivo(caminho, protetor));
        Assert.Equal(antes, File.ReadAllText(caminho));
    }

    [Fact]
    public void ArquivoSemSecaoOuInexistenteNaoEAlterado()
    {
        Assert.Equal(0, SegredosConfiguracao.ProtegerArquivo(Path.Combine(_pasta.Caminho, "nao-existe.json"), new ProtetorFalso()));
        var c = Arquivo("""{ "Outra": 1 }""");
        Assert.Equal(0, SegredosConfiguracao.ProtegerArquivo(c, new ProtetorFalso()));
    }

    [Fact]
    public void JsonInvalidoGeraErroClaroSemApagarOArquivo()
    {
        var c = Arquivo("{ nao é json");
        Assert.Throws<InvalidOperationException>(() => SegredosConfiguracao.ProtegerArquivo(c, new ProtetorFalso()));
        Assert.Equal("{ nao é json", File.ReadAllText(c));
    }

    [Fact]
    public void DpapiRealFazIdaEVolta()
    {
        var p = new ProtetorDpapi();
        var protegido = SegredosConfiguracao.Proteger("S3gredo!ção", p);
        Assert.StartsWith("dpapi:", protegido);
        Assert.DoesNotContain("S3gredo", protegido);
        Assert.Equal("S3gredo!ção", SegredosConfiguracao.Resolver(protegido, p));
    }

    [Fact]
    public void ArquivoProtegidoComDpapiRealELegivelNaConfiguracao()
    {
        var caminho = Arquivo(Exemplo);
        var p = new ProtetorDpapi();
        SegredosConfiguracao.ProtegerArquivo(caminho, p);
        var senha = (string)JsonNode.Parse(File.ReadAllText(caminho))!["AgenteCameras"]!["Cameras"]![2]!["Senha"]!;
        Assert.Equal("senhaC", SegredosConfiguracao.Resolver(senha, p));
    }

    [Fact]
    public void ValorProtegidoIlegivelGeraErroEspecifico() =>
        Assert.Throws<SegredoIlegivelException>(() => SegredosConfiguracao.Resolver("dpapi:AAAA", new ProtetorDpapi()));

    [Fact]
    public void TextoClaroPassaDireto() =>
        Assert.Equal("abc", SegredosConfiguracao.Resolver("abc", new ProtetorFalso()));
}

public class ValidadorConfiguracaoTests
{
    private const string AgenteGuid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private const string C1 = "11111111-1111-1111-1111-111111111111";
    private const string C2 = "22222222-2222-2222-2222-222222222222";
    private const string C3 = "33333333-3333-3333-3333-333333333333";

    private static AgenteCamerasOptions Base() => new()
    {
        ApiBaseUrl = "https://api.exemplo.com.br",
        AgenteId = AgenteGuid,
        Segredo = "segredo",
        CaminhoFfmpeg = @"C:\ffmpeg\ffmpeg.exe",
        Cameras =
        {
            new CameraOptions { CameraId = C1, Nome = "Portaria", Host = "192.168.0.8", Senha = "abc" },
        },
    };

    private static ResultadoConfiguracao Validar(AgenteCamerasOptions o) => ValidadorConfiguracao.Validar(o, new ProtetorFalso());

    [Fact]
    public void ConfiguracaoBoaFicaValidaComPadroes()
    {
        var r = Validar(Base());
        Assert.True(r.Valida, string.Join("; ", r.Erros));
        var c = r.Configuracao!.Cameras.Single();
        Assert.Equal(554, c.PortaRtsp);
        Assert.Equal(TimeSpan.FromSeconds(3), c.IntervaloEnvio);
        Assert.Equal(TimeSpan.FromHours(24), r.Configuracao.FilaIdadeMaxima);
        Assert.Equal(500L * 1024 * 1024, r.Configuracao.FilaTamanhoMaximoBytes);
        Assert.EndsWith(Path.Combine("AAHBRANT", "AgenteCameras"), r.Configuracao.PastaDados);
    }

    [Fact]
    public void CameraSemSenhaNaoEAtivadaComAvisoClaroEAsOutrasSeguem()
    {
        var o = Base();
        o.Cameras.Add(new CameraOptions { CameraId = C2, Nome = "Depósito", Host = "192.168.0.9", Senha = "" });
        var r = Validar(o);
        Assert.True(r.Valida);
        Assert.Equal(new[] { C1 }, r.Configuracao!.Cameras.Select(c => c.CameraId));
        var aviso = Assert.Single(r.Avisos, a => a.Contains("Depósito"));
        Assert.Contains("sem senha", aviso);
        Assert.Contains("NÃO ativada", aviso);
    }

    [Fact]
    public void SenhaProtegidaEResolvida()
    {
        var o = Base();
        o.Cameras[0].Senha = SegredosConfiguracao.Proteger("senha-real", new ProtetorFalso());
        o.Segredo = SegredosConfiguracao.Proteger("segredo-real", new ProtetorFalso());
        var r = Validar(o);
        Assert.Equal("senha-real", r.Configuracao!.Cameras[0].Senha);
        Assert.Equal("segredo-real", r.Configuracao.Segredo);
    }

    [Fact]
    public void SegredoProtegidoEmOutraMaquinaImpedeAPartida()
    {
        var o = Base();
        o.Segredo = "dpapi:AAAA";
        var r = ValidadorConfiguracao.Validar(o, new ProtetorDpapi());
        Assert.False(r.Valida);
        Assert.Contains(r.Erros, e => e.Contains("AgenteCameras:Segredo") && e.Contains("ilegível"));
    }

    [Fact]
    public void ErrosGlobaisImpedemAPartidaComMensagensClaras()
    {
        var r = Validar(new AgenteCamerasOptions());
        Assert.False(r.Valida);
        Assert.Contains(r.Erros, e => e.Contains("ApiBaseUrl"));
        Assert.Contains(r.Erros, e => e.Contains("AgenteId"));
        Assert.Contains(r.Erros, e => e.Contains("Segredo"));
        Assert.Contains(r.Erros, e => e.Contains("CaminhoFfmpeg"));
    }

    [Fact]
    public void ApiBaseUrlInvalidaEHttpGeramErroEAviso()
    {
        var o = Base();
        o.ApiBaseUrl = "api.exemplo.com.br";
        Assert.Contains(Validar(o).Erros, e => e.Contains("ApiBaseUrl inválida"));
        o.ApiBaseUrl = "http://api.exemplo.com.br";
        var r = Validar(o);
        Assert.True(r.Valida);
        Assert.Contains(r.Avisos, a => a.Contains("http"));
    }

    [Theory]
    [InlineData("Canal", 3)]
    [InlineData("Stream", 2)]
    [InlineData("PortaRtsp", 0)]
    [InlineData("IntervaloEnvioSegundos", 0)]
    public void CameraComValorInvalidoNaoSobeEAsOutrasSeguem(string campo, int valor)
    {
        var o = Base();
        var ruim = new CameraOptions { CameraId = C2, Nome = "Ruim", Host = "h", Senha = "x" };
        typeof(CameraOptions).GetProperty(campo)!.SetValue(ruim, valor);
        o.Cameras.Add(ruim);
        var r = Validar(o);
        Assert.True(r.Valida);
        Assert.Single(r.Configuracao!.Cameras);
        Assert.Contains(r.Avisos, a => a.Contains("Ruim") && a.Contains(campo));
    }

    [Fact]
    public void CameraIdRepetidoEModeloComMarcadorDesconhecidoSaoRecusados()
    {
        var o = Base();
        o.Cameras.Add(new CameraOptions { CameraId = C1.ToUpperInvariant(), Nome = "Repetida", Host = "h", Senha = "x" });
        o.Cameras.Add(new CameraOptions { CameraId = C3, Nome = "Modelo", Host = "h", Senha = "x", ModeloUrl = "rtsp://{host}/{foo}" });
        var r = Validar(o);
        Assert.Single(r.Configuracao!.Cameras);
        Assert.Contains(r.Avisos, a => a.Contains("repetido"));
        Assert.Contains(r.Avisos, a => a.Contains("{foo}"));
    }

    [Fact]
    public void CameraDesabilitadaEIgnorada()
    {
        var o = Base();
        o.LerConfiguracaoDoServidor = false; // sem servidor, "nenhuma câmera ativa" é o aviso de espera; com servidor, as câmeras vêm do cadastro
        o.Cameras[0].Habilitada = false;
        var r = Validar(o);
        Assert.True(r.Valida);
        Assert.Empty(r.Configuracao!.Cameras);
        Assert.Contains(r.Avisos, a => a.Contains("desabilitada"));
        Assert.Contains(r.Avisos, a => a.Contains("Nenhuma câmera ativa"));
    }

    [Fact]
    public void ConfiguracaoNaoGuardaSenhaNosAvisos()
    {
        var o = Base();
        o.Cameras.Add(new CameraOptions { CameraId = C2, Nome = "X", Host = "", Senha = "SenhaMuitoSecreta" });
        var r = Validar(o);
        Assert.DoesNotContain("SenhaMuitoSecreta", string.Join("\n", r.Avisos.Concat(r.Erros)));
    }
}
