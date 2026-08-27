using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class AutenticacaoBiometriaLocalFalsa : IAutenticacaoBiometriaLocalService
{
    public Guid? DispositivoIdRecebido { get; private set; }
    public double? ScoreRecebido { get; private set; }

    public Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, CancellationToken ct)
    {
        DispositivoIdRecebido = dispositivoId;
        ScoreRecebido = score;
        return Task.FromResult(new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.Biometria));
    }
}

public class RegistradorAssinaturaFalso : IRegistradorAssinaturaService
{
    public Guid? DocumentoIdRecebido { get; private set; }

    public Task<DocumentoSignatarioDto> RegistrarAsync(Guid documentoAssinaturaId, ResultadoAutenticacaoAssinatura resultado, CancellationToken ct)
    {
        DocumentoIdRecebido = documentoAssinaturaId;
        return Task.FromResult(new DocumentoSignatarioDto(resultado.TrabalhadorId, "Nome Fake", resultado.Metodo, DateTime.UtcNow));
    }
}

public class RegistrarAssinaturaBiometriaLocalCommandTests
{
    [Fact]
    public async Task Handle_DeveAutenticarERegistrarAssinatura()
    {
        var autenticacao = new AutenticacaoBiometriaLocalFalsa();
        var registrador = new RegistradorAssinaturaFalso();
        var handler = new RegistrarAssinaturaBiometriaLocalCommandHandler(autenticacao, registrador);

        var documentoId = Guid.NewGuid();
        var dispositivoId = Guid.NewGuid();
        var trabalhadorId = Guid.NewGuid();

        var resultado = await handler.Handle(
            new RegistrarAssinaturaBiometriaLocalCommand(documentoId, dispositivoId, "segredo", trabalhadorId, 90), CancellationToken.None);

        Assert.Equal(dispositivoId, autenticacao.DispositivoIdRecebido);
        Assert.Equal(90, autenticacao.ScoreRecebido);
        Assert.Equal(documentoId, registrador.DocumentoIdRecebido);
        Assert.Equal(trabalhadorId, resultado.TrabalhadorId);
    }
}
