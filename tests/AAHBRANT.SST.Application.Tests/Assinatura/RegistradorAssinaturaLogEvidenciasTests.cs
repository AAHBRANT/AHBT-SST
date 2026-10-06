using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Rastro técnico que alimenta o log de assinaturas da Ficha de EPI: leitor, equipamento e
// geolocalização declarada pelo aparelho — mais a imagem de referência do cadastro da digital.
public class RegistradorAssinaturaLogEvidenciasTests
{
    private sealed class ClienteFalso(string? ip, string? userAgent) : IClienteIpProvider
    {
        public string? ObterIp() => ip;
        public string? ObterUserAgent() => userAgent;
    }

    private sealed class CriptografiaImagemFalsa : IImagemBiometricaCriptografia
    {
        public string Criptografar(byte[] imagem) => "x:" + Convert.ToBase64String(imagem);
        public byte[] Descriptografar(string cifradoBase64) => Convert.FromBase64String(cifradoBase64[2..]);
    }

    private sealed class CriptografiaTemplateFalsa : ITemplateBiometricoCriptografia
    {
        public string Criptografar(byte[] templateBruto) => Convert.ToBase64String(templateBruto);
    }

    private static async Task<(IAppDbContext Db, Trabalhador Trabalhador, DocumentoAssinatura Documento)> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador { Nome = "Alan", Cpf = "11122233344", Funcao = new Funcao { Nome = "Ajudante" } };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = Guid.NewGuid() };
        db.Trabalhadores.Add(trabalhador);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        return (db, trabalhador, documento);
    }

    [Fact]
    public async Task RegistrarAsync_GravaLeitorEquipamentoELocalizacao()
    {
        var (db, trabalhador, documento) = await PrepararAsync();
        var leitor = Guid.NewGuid();
        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa(), new ClienteFalso("206.42.35.39", "Chrome/129 Windows"));

        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.Biometria, leitor),
            ipAddress: null, CancellationToken.None,
            localizacao: new LocalizacaoAssinatura(StatusLocalizacaoAssinatura.Capturada, -7.1195, -34.8450, 35));

        var s = await db.DocumentoSignatarios.SingleAsync();
        Assert.Equal("206.42.35.39", s.IpAddress);
        Assert.Equal(leitor, s.DispositivoAgenteId);
        Assert.Equal("Chrome/129 Windows", s.UserAgent);
        Assert.Equal(StatusLocalizacaoAssinatura.Capturada, s.LocalizacaoStatus);
        Assert.Equal(-7.1195, s.Latitude);
        Assert.Equal(-34.8450, s.Longitude);
        Assert.Equal(35, s.PrecisaoMetros);
    }

    [Fact]
    public async Task RegistrarAsync_SemLocalizacaoInformada_FicaComoNaoInformada()
    {
        var (db, trabalhador, documento) = await PrepararAsync();
        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.SessaoLogada),
            "10.0.0.1", CancellationToken.None);

        var s = await db.DocumentoSignatarios.SingleAsync();
        Assert.Equal(StatusLocalizacaoAssinatura.NaoInformada, s.LocalizacaoStatus);
        Assert.Null(s.Latitude);
        Assert.Null(s.UserAgent);
    }

    [Theory]
    [InlineData(95.0, 10.0)]
    [InlineData(10.0, -190.0)]
    public async Task RegistrarAsync_CoordenadaImplausivel_ViraIndisponivelSemGravarLixo(double lat, double lon)
    {
        var (db, trabalhador, documento) = await PrepararAsync();
        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.SessaoLogada),
            "10.0.0.1", CancellationToken.None,
            localizacao: new LocalizacaoAssinatura(StatusLocalizacaoAssinatura.Capturada, lat, lon, 10));

        var s = await db.DocumentoSignatarios.SingleAsync();
        Assert.Equal(StatusLocalizacaoAssinatura.Indisponivel, s.LocalizacaoStatus);
        Assert.Null(s.Latitude);
        Assert.Null(s.Longitude);
    }

    [Fact]
    public async Task RegistrarAsync_NaoAutorizada_NaoGuardaCoordenadasEnviadas()
    {
        var (db, trabalhador, documento) = await PrepararAsync();
        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.SessaoLogada),
            "10.0.0.1", CancellationToken.None,
            localizacao: new LocalizacaoAssinatura(StatusLocalizacaoAssinatura.NaoAutorizada, -7.0, -34.0, 5));

        var s = await db.DocumentoSignatarios.SingleAsync();
        Assert.Equal(StatusLocalizacaoAssinatura.NaoAutorizada, s.LocalizacaoStatus);
        Assert.Null(s.Latitude);
    }

    [Fact]
    public async Task CadastrarTemplate_GuardaImagemDeCadastroCriptografada()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador
        {
            Nome = "Alan", Cpf = "11122233344", Funcao = new Funcao { Nome = "Ajudante" },
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow, ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaTemplateFalsa(), new CriptografiaImagemFalsa());
        var png = new byte[] { 137, 80, 78, 71, 1, 2, 3 };

        await handler.Handle(new CadastrarTemplateBiometricoCommand(trabalhador.Id, new byte[] { 9, 9 }, png), CancellationToken.None);

        var t = await db.TemplatesBiometricoFutronic.SingleAsync();
        Assert.NotNull(t.ImagemCadastroCriptografada);
        Assert.NotEqual(Convert.ToBase64String(png), t.ImagemCadastroCriptografada);
        Assert.Equal(png, new CriptografiaImagemFalsa().Descriptografar(t.ImagemCadastroCriptografada!));
    }

    [Fact]
    public async Task CadastrarTemplate_SemImagem_FicaNulo()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador
        {
            Nome = "Alan", Cpf = "11122233344", Funcao = new Funcao { Nome = "Ajudante" },
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow, ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaTemplateFalsa(), new CriptografiaImagemFalsa());

        await handler.Handle(new CadastrarTemplateBiometricoCommand(trabalhador.Id, new byte[] { 9, 9 }), CancellationToken.None);

        Assert.Null((await db.TemplatesBiometricoFutronic.SingleAsync()).ImagemCadastroCriptografada);
    }

    private sealed class AuditoriaServiceFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(
            string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }
}
