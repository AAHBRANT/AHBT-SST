namespace AAHBRANT.SST.Application.Common.Interfaces;

// Publica eventos de Acidente para o G-RH consumir (Integração G-RH — SST é fonte única de verdade
// para Acidente; decisão do usuário em 2026-09-09, ver SincronizarColaboradorGrhCommand para o sentido
// inverso). Chamado por CriarAcidenteCommandHandler, AtualizarAcidenteCommandHandler e
// AvancarStatusAcidenteCommandHandler logo após SaveChangesAsync.
//
// Duas implementações em Infrastructure (mesmo padrão de IFilaNotificacaoTeams):
//   - ServiceBusPublicadorAcidenteGrh: real, ativa quando "ServiceBus:ConnectionString" existir.
//   - NoOpPublicadorAcidenteGrh: só loga enquanto isso não existir — publicar o evento nunca deve
//     travar o fluxo de registro do acidente no SST, e não há consumidor local para simular (quem
//     consome é o processo externo do G-RH).
public interface IPublicadorAcidenteGrh
{
    Task PublicarAsync(AcidenteGrhEvento evento, CancellationToken ct = default);
}

public record AcidenteGrhEvento(
    Guid AcidenteId,
    string? TrabalhadorCpf,
    string Local,
    DateTime Data,
    TimeSpan? Hora,
    string? NumeroCat,
    bool CatEmitida,
    string Status);
