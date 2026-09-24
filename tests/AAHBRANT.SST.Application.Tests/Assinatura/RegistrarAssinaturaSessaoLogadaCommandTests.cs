using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class RegistrarAssinaturaSessaoLogadaCommandTests
{
    [Fact]
    public async Task Handle_DeveRepassarIpParaRegistrador()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador { Nome = "Responsável", Cpf = "11122233344", DataAdmissao = DateTime.UtcNow };
        var usuario = new Usuario
        {
            Nome = "Responsável",
            Email = "responsavel@teste.com",
            AzureAdObjectId = "oid-123",
            Trabalhador = trabalhador,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        var registrador = new RegistradorFake();
        var handler = new RegistrarAssinaturaSessaoLogadaCommandHandler(db, registrador);

        await handler.Handle(
            new RegistrarAssinaturaSessaoLogadaCommand(Guid.NewGuid(), "oid-123", "187.19.184.130"),
            CancellationToken.None);

        Assert.Equal("187.19.184.130", registrador.IpRecebido);
        Assert.Equal(trabalhador.Id, registrador.ResultadoRecebido?.TrabalhadorId);
        Assert.Equal(MetodoAutenticacaoAssinatura.SessaoLogada, registrador.ResultadoRecebido?.Metodo);
    }

    private sealed class RegistradorFake : IRegistradorAssinaturaService
    {
        public string? IpRecebido { get; private set; }
        public ResultadoAutenticacaoAssinatura? ResultadoRecebido { get; private set; }

        public Task<DocumentoSignatarioDto> RegistrarAsync(
            Guid documentoAssinaturaId,
            ResultadoAutenticacaoAssinatura resultado,
            string? ipAddress,
            CancellationToken ct)
        {
            IpRecebido = ipAddress;
            ResultadoRecebido = resultado;
            return Task.FromResult(new DocumentoSignatarioDto(
                resultado.TrabalhadorId,
                "Responsável",
                resultado.Metodo,
                DateTime.UtcNow,
                ipAddress));
        }
    }
}
