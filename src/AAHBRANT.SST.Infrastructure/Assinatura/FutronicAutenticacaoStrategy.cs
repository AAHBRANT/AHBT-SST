using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

public class FutronicAutenticacaoStrategy : IAutenticacaoBiometriaLocalService
{
    private readonly IAppDbContext _db;
    private readonly IDispositivoAgenteAutenticador _dispositivoAutenticador;
    private readonly AssinaturaOptions _options;

    public FutronicAutenticacaoStrategy(IAppDbContext db, IDispositivoAgenteAutenticador dispositivoAutenticador, IOptions<AssinaturaOptions> options)
    {
        _db = db;
        _dispositivoAutenticador = dispositivoAutenticador;
        _options = options.Value;
    }

    public async Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, CancellationToken ct)
    {
        var dispositivo = await _dispositivoAutenticador.ValidarAsync(dispositivoId, segredoDispositivo, ct);

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct);
        if (trabalhador is null || trabalhador.ObraId != dispositivo.ObraId)
        {
            throw new KeyNotFoundException("Trabalhador não encontrado.");
        }

        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct);
        if (obra is null || !obra.MetodosAutenticacaoHabilitados.HasFlag(MetodoAutenticacaoObra.Biometria))
        {
            throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra deste trabalhador.");
        }

        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null || trabalhador.ConsentimentoBiometriaEm is null)
        {
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite ou o consentimento de biometria.");
        }

        if (score < _options.LimiarConfiancaBiometriaLocal)
        {
            throw new InvalidOperationException("Confiança do match biométrico abaixo do limiar exigido.");
        }

        return new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.Biometria);
    }
}
