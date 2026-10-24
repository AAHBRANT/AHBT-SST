using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Seguranca;

public class FotoBiometricaCriptografiaTests
{
    static FotoBiometricaCriptografiaTests()
    {
        CpfCriptografiaContexto.Configurar(Enumerable.Repeat((byte)1, 32).ToArray(), Enumerable.Repeat((byte)2, 32).ToArray());
    }

    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8 };

    [Fact]
    public void Cifrar_NaoDeixaAImagemEmTextoPuroEVoltaIgual()
    {
        var cifrado = FotoBiometricaCriptografiaConversor.Cifrar(Jpeg);

        Assert.True(FotoBiometricaCriptografiaConversor.EstaCifrado(cifrado));
        Assert.NotEqual(Jpeg, cifrado.Skip(8 + 12).Take(Jpeg.Length).ToArray());
        Assert.Equal(Jpeg, FotoBiometricaCriptografiaConversor.Decifrar(cifrado));
    }

    // Foto gravada antes da criptografia: lida como está, sem quebrar o log de assinaturas antigo.
    [Fact]
    public void Decifrar_FotoAntigaEmTextoPuro_DevolveComoEsta()
    {
        Assert.Equal(Jpeg, FotoBiometricaCriptografiaConversor.Decifrar(Jpeg));
    }

    [Fact]
    public void Cifrar_DuasVezes_NaoCifraDeNovo()
    {
        var cifrado = FotoBiometricaCriptografiaConversor.Cifrar(Jpeg);
        Assert.Equal(cifrado, FotoBiometricaCriptografiaConversor.Cifrar(cifrado));
    }

    [Fact]
    public async Task GravarELer_PeloEf_DevolveAImagemOriginal()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var id = Guid.NewGuid();
        await using (var db = new SstDbContext(opcoes, new CurrentUserService()))
        {
            db.FotosCadastroFacial.Add(new FotoCadastroFacial { Id = id, TrabalhadorId = Guid.NewGuid(), Conteudo = Jpeg, ContentType = "image/jpeg", HashSha256 = "h", CapturadaEm = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await using (var db = new SstDbContext(opcoes, new CurrentUserService()))
        {
            Assert.Equal(Jpeg, (await db.FotosCadastroFacial.IgnoreQueryFilters().SingleAsync(f => f.Id == id)).Conteudo);
        }
    }
}
