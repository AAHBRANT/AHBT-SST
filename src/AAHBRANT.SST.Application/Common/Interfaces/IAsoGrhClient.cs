namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração da leitura de ASO no G-RH — diferente de Colaborador/Alojamento (que viriam de um
// endpoint HTTP dedicado), aqui a leitura é direta no banco do G-RH (tabela `documentos`, filtrada
// pelo tipo "ASO"), decisão tomada em 2026-09-16 porque o time do G-RH não tinha token/capacidade
// pra construir o endpoint na hora. Implementação real em Infrastructure (AsoGrhDbClient).
public interface IAsoGrhClient
{
    Task<IReadOnlyList<AsoGrhDto>> ListarTodosAsync(CancellationToken ct = default);
}

// Campos clínicos (Aptidao/RestricaoClinica/MedicoNome) vêm nulos na maioria dos registros hoje —
// isso é esperado (ver SincronizarAsoGrhCommand: nunca sobrescrevem um valor já lançado no SST).
// DataValidade nula significa "ainda não sincronizável" — o comando de sincronização rejeita o
// registro até o G-RH ter uma validade preenchida (Aso.DataValidade é obrigatório no SST, usado por
// regra de elegibilidade e alerta de vencimento — não pode ficar em branco).
public record AsoGrhDto(
    string GrhAsoId,
    string Cpf,
    string? Numero,
    DateTime DataExame,
    DateTime? DataValidade,
    string? Aptidao,
    string? RestricaoClinica,
    string? MedicoNome);
