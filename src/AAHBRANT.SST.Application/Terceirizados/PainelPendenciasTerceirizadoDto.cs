namespace AAHBRANT.SST.Application.Terceirizados;

public record PendenciaPessoaDto(
    Guid TrabalhadorId,
    string Nome,
    Guid EmpresaId,
    string EmpresaRazaoSocial,
    List<string> Pendencias);

public record ContratoEncerradoComPessoasAtivasDto(
    Guid ContratoId,
    string NumeroContrato,
    string EmpresaRazaoSocial,
    int QuantidadePessoasAtivas);

public record AlertaEstoqueInsuficienteDto(
    Guid AlertaId,
    string Titulo,
    string? Descricao,
    DateTime CriadoEmUtc);

public record PainelPendenciasTerceirizadoDto(
    List<PendenciaPessoaDto> PessoasBloqueadas,
    List<ContratoEncerradoComPessoasAtivasDto> ContratosEncerradosComPessoasAtivas,
    List<AlertaEstoqueInsuficienteDto> AlertasEstoqueInsuficiente);
