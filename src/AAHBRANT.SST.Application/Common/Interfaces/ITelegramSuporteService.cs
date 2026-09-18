namespace AAHBRANT.SST.Application.Common.Interfaces;

public interface ITelegramSuporteService
{
    Task EnviarDemandaAsync(string mensagem, CancellationToken ct = default);
}
