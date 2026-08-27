using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

public interface IDispositivoAgenteAutenticador
{
    Task<DispositivoAgenteBiometrico> ValidarAsync(Guid dispositivoId, string segredoDispositivo, CancellationToken ct);
}

public class DispositivoAgenteAutenticador : IDispositivoAgenteAutenticador
{
    private readonly IAppDbContext _db;
    private readonly ISegredoDispositivoHasher _hasher;

    public DispositivoAgenteAutenticador(IAppDbContext db, ISegredoDispositivoHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<DispositivoAgenteBiometrico> ValidarAsync(Guid dispositivoId, string segredoDispositivo, CancellationToken ct)
    {
        var dispositivo = await _db.DispositivosAgenteBiometrico.FirstOrDefaultAsync(d => d.Id == dispositivoId, ct);

        // InvalidOperationException (não UnauthorizedAccessException): TratamentoDeExcecaoMiddleware
        // não tem handler para 401, então cairia no 500 genérico em vez do 400 esperado para esta
        // falha de regra de negócio — mesma convenção de CrachaPinAutenticacaoStrategy.
        if (dispositivo is null || !_hasher.Verificar(segredoDispositivo, dispositivo.SegredoHash))
        {
            throw new InvalidOperationException("Dispositivo não encontrado ou segredo inválido.");
        }

        return dispositivo;
    }
}
