using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Wrapper fino sobre PinHasher (lógica de criptografia pura, sem dependências externas) para que a
// Application layer dependa de uma abstração (IPinHasher) em vez de Infrastructure diretamente —
// mesma regra de dependência do Clean Architecture já aplicada a IAppDbContext/SstDbContext.
public class PinHasherService : IPinHasher
{
    public string GerarHash(string pin) => PinHasher.GerarHash(pin);
}
