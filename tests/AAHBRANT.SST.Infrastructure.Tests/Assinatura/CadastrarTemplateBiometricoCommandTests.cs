using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class CriptografiaFalsaParaTeste : ITemplateBiometricoCriptografia
{
    public string Criptografar(byte[] templateBruto) => Convert.ToBase64String(templateBruto);
}

public class CadastrarTemplateBiometricoCommandTests
{
    [Fact]
    public async Task Handle_ComTrabalhadorElegivel_DeveCriarTemplateCriptografado()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_ComTrabalhadorElegivel_DeveCriarTemplateCriptografado));
        var obra = new Obra { Codigo = "OBR-007", Nome = "Obra Teste 7" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Sicrano",
            Matricula = "M-004",
            Cpf = "55566677788",
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow,
            ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaFalsaParaTeste());
        var templateBruto = new byte[] { 1, 2, 3 };

        await handler.Handle(new CadastrarTemplateBiometricoCommand(trabalhador.Id, templateBruto), CancellationToken.None);

        var salvo = await db.TemplatesBiometricoFutronic.FirstOrDefaultAsync(t => t.TrabalhadorId == trabalhador.Id);
        Assert.NotNull(salvo);
        Assert.Equal(Convert.ToBase64String(templateBruto), salvo!.TemplateCriptografado);
    }

    [Fact]
    public async Task Handle_SemConsentimentoBiometria_DeveLancarInvalidOperationException()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_SemConsentimentoBiometria_DeveLancarInvalidOperationException));
        var obra = new Obra { Codigo = "OBR-008", Nome = "Obra Teste 8" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Sicrano2", Matricula = "M-005", Cpf = "99988877766" };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaFalsaParaTeste());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CadastrarTemplateBiometricoCommand(trabalhador.Id, new byte[] { 1 }), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ComTrabalhadorInexistente_DeveLancarKeyNotFoundException()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_ComTrabalhadorInexistente_DeveLancarKeyNotFoundException));
        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaFalsaParaTeste());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new CadastrarTemplateBiometricoCommand(Guid.NewGuid(), new byte[] { 1 }), CancellationToken.None));
    }
}
