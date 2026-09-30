using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Regra do usuário (30/09): digital e facial são cadastrados uma única vez por trabalhador.
public class CadastroBiometricoUnicoTests
{
    private class CriptografiaFalsa : ITemplateBiometricoCriptografia
    {
        public string Criptografar(byte[] templateBruto) => Convert.ToBase64String(templateBruto);
    }

    private static async Task<Trabalhador> CriarTrabalhadorComTermosAsync(IAppDbContext db)
    {
        var trabalhador = new Trabalhador
        {
            Nome = "Fulano",
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow,
            ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        return trabalhador;
    }

    [Fact]
    public async Task CadastrarDigital_SegundaVez_EhRecusado()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarTrabalhadorComTermosAsync(db);
        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaFalsa());

        await handler.Handle(new CadastrarTemplateBiometricoCommand(trabalhador.Id, new byte[] { 1 }), CancellationToken.None);
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CadastrarTemplateBiometricoCommand(trabalhador.Id, new byte[] { 2 }), CancellationToken.None));

        Assert.Contains("já está cadastrada", erro.Message);
        Assert.Equal(1, await db.TemplatesBiometricoFutronic.CountAsync(tb => tb.TrabalhadorId == trabalhador.Id));
    }

    [Fact]
    public async Task Status_SemCadastro_DevolveTudoFalso()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarTrabalhadorComTermosAsync(db);

        var status = await new ObterStatusCadastroBiometricoQueryHandler(db)
            .Handle(new ObterStatusCadastroBiometricoQuery(trabalhador.Id), CancellationToken.None);

        Assert.False(status.TemDigital);
        Assert.False(status.TemFacial);
    }

    [Fact]
    public async Task Status_ComDigitalEFacial_DevolveDataDoPrimeiroCadastro()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarTrabalhadorComTermosAsync(db);
        var quando = new DateTime(2026, 9, 29, 14, 0, 0, DateTimeKind.Utc);
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = trabalhador.Id, TemplateCriptografado = "x", CapturadoEm = quando });
        db.FotosCadastroFacial.Add(new FotoCadastroFacial
        {
            TrabalhadorId = trabalhador.Id,
            Conteudo = new byte[] { 1 },
            ContentType = "image/jpeg",
            HashSha256 = new string('a', 64),
            CapturadaEm = quando,
        });
        await db.SaveChangesAsync();

        var status = await new ObterStatusCadastroBiometricoQueryHandler(db)
            .Handle(new ObterStatusCadastroBiometricoQuery(trabalhador.Id), CancellationToken.None);

        Assert.True(status.TemDigital);
        Assert.True(status.TemFacial);
        Assert.Equal(quando, status.DigitalCadastradaEm);
        Assert.Equal(quando, status.FacialCadastradoEm);
    }
}
