namespace AAHBRANT.SST.Application.Contratos;

public record ContratoDto(
    Guid Id,
    Guid EmpresaId,
    Guid ObraId,
    string ObraNome,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    string Status,
    // true = veio do webhook do G-Juri (só leitura no SST); false = cadastrado à mão pelo técnico.
    bool OrigemGJuri,
    // PDF do contrato guardado no SST ("Subir contrato", 21/10/2026).
    bool TemArquivo = false);

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
    List<VagaFuncaoDto> Vagas,
    bool OrigemGJuri,
    bool TemArquivo = false,
    string? ArquivoNome = null);
