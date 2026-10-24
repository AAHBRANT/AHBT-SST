extern alias instalador;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using AAHBRANT.SST.AgenteBiometria.Atualizacao;
using instalador::AAHBRANT.SST.AgenteBiometria.Instalador;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Atualizacao;

public class VerificadorAtualizacaoTests
{
    private static (string Privada, string Publica) GerarChaves()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (ecdsa.ExportPkcs8PrivateKeyPem(), ecdsa.ExportSubjectPublicKeyInfoPem());
    }

    private static ManifestoAtualizacao Assinar(string privada, string versao, string url, byte[] setup)
    {
        var m = AssinadorManifesto.Assinar(privada, versao, url, setup);
        return new ManifestoAtualizacao(m.Versao, m.UrlSetup, m.Sha256, m.Assinatura);
    }

    [Fact]
    public void Manifesto_assinado_com_a_chave_certa_e_aceito()
    {
        var (privada, publica) = GerarChaves();
        var setup = new byte[] { 1, 2, 3 };
        var manifesto = Assinar(privada, "1.2.0", "https://x/Setup.exe", setup);

        Assert.True(VerificadorAtualizacao.AssinaturaValida(manifesto, publica));
        Assert.True(VerificadorAtualizacao.Sha256Confere(setup, manifesto.Sha256));
    }

    [Fact]
    public void Manifesto_alterado_ou_de_outra_chave_e_recusado()
    {
        var (privada, publica) = GerarChaves();
        var (_, outraPublica) = GerarChaves();
        var manifesto = Assinar(privada, "1.2.0", "https://x/Setup.exe", new byte[] { 1 });

        Assert.False(VerificadorAtualizacao.AssinaturaValida(manifesto with { UrlSetup = "https://atacante/Setup.exe" }, publica));
        Assert.False(VerificadorAtualizacao.AssinaturaValida(manifesto with { Versao = "9.9.9" }, publica));
        Assert.False(VerificadorAtualizacao.AssinaturaValida(manifesto, outraPublica));
        Assert.False(VerificadorAtualizacao.AssinaturaValida(manifesto with { Assinatura = "nao-e-base64" }, publica));
    }

    [Fact]
    public void Arquivo_trocado_nao_confere_com_o_sha()
    {
        var (privada, _) = GerarChaves();
        var manifesto = Assinar(privada, "1.2.0", "https://x/Setup.exe", new byte[] { 1, 2, 3 });

        Assert.False(VerificadorAtualizacao.Sha256Confere(new byte[] { 9, 9, 9 }, manifesto.Sha256));
    }

    [Theory]
    [InlineData("1.2.0", "1.1.0", true)]
    [InlineData("1.1.0", "1.1.0", false)]
    [InlineData("1.1", "1.1.0.0", false)]
    [InlineData("1.0.9", "1.1.0", false)]
    [InlineData("lixo", "1.1.0", false)]
    public void So_versao_maior_atualiza(string manifesto, string atual, bool esperado)
    {
        Assert.Equal(esperado, VerificadorAtualizacao.EhMaisNova(manifesto, Version.Parse(atual)));
    }
}

public class InstaladorAgenteTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "teste-instalador-" + Guid.NewGuid().ToString("N"));
    private string Destino => Path.Combine(_raiz, "AgenteBiometria");

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    private static MemoryStream Pacote(string conteudoExe)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var w = new StreamWriter(zip.CreateEntry(InstaladorAgente.NomeExe + ".exe").Open());
            w.Write(conteudoExe);
        }
        ms.Position = 0;
        return ms;
    }

    private InstaladorAgente Criar(bool novaResponde = true)
    {
        var instalador = new InstaladorAgente(Destino, _ => { })
        {
            PararAgente = () => { },
            RegistrarInicioAutomatico = _ => { },
        };
        instalador.IniciarEConferir = exe => novaResponde || File.ReadAllText(exe) != "v2";
        return instalador;
    }

    private static ConfiguracaoPosto Config() =>
        new(Guid.NewGuid(), "segredo", "chave", "https://api", "https://web", "https://up/manifesto.json");

    [Fact]
    public void Primeira_instalacao_sem_configuracao_e_recusada()
    {
        Assert.Throws<InvalidOperationException>(() => Criar().Instalar(Pacote("v1"), config: null));
    }

    [Fact]
    public void Primeira_instalacao_grava_executavel_e_configuracao()
    {
        var config = Config();
        Criar().Instalar(Pacote("v1"), config);

        Assert.Equal("v1", File.ReadAllText(Path.Combine(Destino, InstaladorAgente.NomeExe + ".exe")));
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Destino, InstaladorAgente.ArquivoConfig)))!;
        Assert.Equal(config.DispositivoId.ToString(), json["Agente"]!["DispositivoId"]!.GetValue<string>());
        Assert.Equal("Futronic", json["Agente"]!["Leitor"]!.GetValue<string>());
    }

    [Fact]
    public void Atualizacao_troca_o_executavel_e_mantem_a_configuracao_do_posto()
    {
        var config = Config();
        Criar().Instalar(Pacote("v1"), config);

        Criar().Instalar(Pacote("v2"), config: null);

        Assert.Equal("v2", File.ReadAllText(Path.Combine(Destino, InstaladorAgente.NomeExe + ".exe")));
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Destino, InstaladorAgente.ArquivoConfig)))!;
        Assert.Equal("segredo", json["Agente"]!["SegredoDispositivo"]!.GetValue<string>());
    }

    [Fact]
    public void Versao_nova_que_nao_responde_volta_para_a_anterior()
    {
        Criar().Instalar(Pacote("v1"), Config());

        Assert.Throws<InvalidOperationException>(() => Criar(novaResponde: false).Instalar(Pacote("v2"), config: null));

        Assert.Equal("v1", File.ReadAllText(Path.Combine(Destino, InstaladorAgente.NomeExe + ".exe")));
    }

    [Fact]
    public void Completar_acrescenta_url_de_atualizacao_sem_mexer_no_resto()
    {
        var antigo = "{\"Agente\":{\"DispositivoId\":\"abc\",\"SegredoDispositivo\":\"s\"}}";

        var novo = JsonNode.Parse(InstaladorAgente.Completar(antigo, "https://up/manifesto.json"))!;

        Assert.Equal("abc", novo["Agente"]!["DispositivoId"]!.GetValue<string>());
        Assert.Equal("https://up/manifesto.json", novo["Agente"]!["UrlManifestoAtualizacao"]!.GetValue<string>());
    }

    [Fact]
    public void Completar_nao_sobrescreve_url_ja_configurada()
    {
        var antigo = "{\"Agente\":{\"UrlManifestoAtualizacao\":\"https://minha\"}}";

        var novo = JsonNode.Parse(InstaladorAgente.Completar(antigo, "https://padrao"))!;

        Assert.Equal("https://minha", novo["Agente"]!["UrlManifestoAtualizacao"]!.GetValue<string>());
    }

    [Fact]
    public void Completar_corrige_enderecos_azurewebsites_antigos()
    {
        var antigo = "{\"Agente\":{\"BackendBaseUrl\":\"https://sst-api-hml.azurewebsites.net\",\"OrigemPermitida\":\"https://sst-web-hml.azurewebsites.net/\"}}";

        var novo = JsonNode.Parse(InstaladorAgente.Completar(antigo, null))!;

        Assert.Equal("https://sst-api-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io", novo["Agente"]!["BackendBaseUrl"]!.GetValue<string>());
        Assert.Equal("https://sst-web-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io", novo["Agente"]!["OrigemPermitida"]!.GetValue<string>());
    }
}
