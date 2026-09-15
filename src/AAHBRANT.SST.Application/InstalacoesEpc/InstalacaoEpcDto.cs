using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.InstalacoesEpc;

public record InstalacaoEpcDto(
    Guid Id,
    Guid CatalogoEpcId,
    Guid ObraId,
    string? LocalInstalacao,
    int Quantidade,
    DateTime DataInstalacao,
    DateTime? DataValidade,
    DateTime? DataUltimaInspecao,
    StatusInspecaoEpc? StatusUltimaInspecao,
    string? ObservacoesInspecao,
    DateTime? DataRemocao,
    string? Observacoes);
