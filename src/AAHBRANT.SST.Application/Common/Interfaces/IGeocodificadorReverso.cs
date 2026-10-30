namespace AAHBRANT.SST.Application.Common.Interfaces;

// Endereço por extenso de uma coordenada, para o carimbo da foto (rua, bairro, cidade/UF, CEP, país).
// Nunca lança: serviço desligado, sem resposta ou sem endereço = lista vazia (a foto sai só com as coordenadas).
public interface IGeocodificadorReverso
{
    Task<IReadOnlyList<string>> LinhasAsync(double latitude, double longitude, CancellationToken ct);
}
