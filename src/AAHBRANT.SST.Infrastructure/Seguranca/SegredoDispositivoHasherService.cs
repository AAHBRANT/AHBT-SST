using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

public class SegredoDispositivoHasherService : ISegredoDispositivoHasher
{
    public string GerarSegredo() => SegredoDispositivoHasher.GerarSegredo();
    public string GerarHash(string segredo) => SegredoDispositivoHasher.GerarHash(segredo);
    public bool Verificar(string segredo, string hash) => SegredoDispositivoHasher.Verificar(segredo, hash);
}
