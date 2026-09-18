namespace AAHBRANT.SST.Application.Contratos;

public record ContratoDto(
    Guid Id,
    Guid EmpresaId,
    Guid ObraId,
    string ObraNome,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    string Status);

public record VagaFuncaoDto(
    Guid Id,
    Guid FuncaoId,
    string FuncaoNome,
    int QuantidadeVagas,
    int QuantidadePreenchidas);

public record ContratoDetalheDto(
    Guid Id,
    Guid EmpresaId,
    string EmpresaRazaoSocial,
    Guid ObraId,
    string ObraNome,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    string Status,
    List<VagaFuncaoDto> Vagas);
