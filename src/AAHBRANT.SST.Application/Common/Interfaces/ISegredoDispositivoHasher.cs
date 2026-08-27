namespace AAHBRANT.SST.Application.Common.Interfaces;

public interface ISegredoDispositivoHasher
{
    string GerarSegredo();
    string GerarHash(string segredo);
    bool Verificar(string segredo, string hash);
}
