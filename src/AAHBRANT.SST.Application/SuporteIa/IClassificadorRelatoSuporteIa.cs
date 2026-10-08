using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.SuporteIa;

/// <summary>
/// Lê o relato livre do usuário (normalmente vindo de áudio transcrito) e sugere os campos do
/// chamado. É só sugestão: o usuário confere e pode corrigir antes de abrir o chamado.
/// </summary>
public interface IClassificadorRelatoSuporteIa
{
    Task<ChamadoSugeridoSuporteIa> ClassificarAsync(string relato, CancellationToken ct);
}

public record ChamadoSugeridoSuporteIa(
    TipoSolicitacaoSuporteIa Tipo,
    SeveridadeSolicitacaoSuporteIa Severidade,
    string Titulo,
    string? Modulo,
    string Descricao);
