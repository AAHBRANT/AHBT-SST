using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração do endpoint de carga inicial do G-RH (GET /api/integracoes/sst/colaboradores — contrato
// acordado em 2026-09-09, autenticação client-credentials com o App Role Grh.LerColaboradores).
// Implementação real em Infrastructure (ColaboradorGrhClient). Usado só por
// ImportarColaboradoresGrhCommand — a atualização contínua depois da carga inicial é por evento via
// Service Bus (ServiceBusColaboradorGrhProcessor), não por chamadas repetidas a este cliente (o
// endpoint não tem filtro incremental).
public interface IColaboradorGrhClient
{
    Task<IReadOnlyList<ColaboradorGrhDto>> ListarTodosAsync(CancellationToken ct = default);
}

public record ColaboradorGrhDto(
    string Cpf,
    string Nome,
    string? Pis,
    string? Ctps,
    DateTime? DataNascimento,
    string? NomeMae,
    string? Endereco,
    string? Municipio,
    string? Uf,
    string? Cep,
    string Matricula,
    DateTime DataAdmissao,
    DateTime? DataDemissao,
    SituacaoTrabalhador Situacao,
    decimal? Salario,
    DateTime? DataFimExperiencia1,
    DateTime? DataFimExperiencia2,
    string? TamanhoBlusaEpi,
    string? TamanhoCalcaEpi,
    string? TamanhoCalcadoEpi,
    string? ObraNome,
    string? CargoNome,
    string? CargoCboCodigo);
