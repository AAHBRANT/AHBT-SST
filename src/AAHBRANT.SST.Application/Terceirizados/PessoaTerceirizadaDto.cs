namespace AAHBRANT.SST.Application.Terceirizados;

public enum StatusLiberacaoTerceirizado
{
    Pendente,
    Liberada
}

public record StatusLiberacaoTerceirizadoDto(
    Guid TrabalhadorId,
    StatusLiberacaoTerceirizado Status,
    List<string> Pendencias);

public record PessoaTerceirizadaDto(
    Guid TrabalhadorId,
    string Nome,
    string? Matricula,
    Guid EmpresaId,
    string EmpresaRazaoSocial,
    Guid ContratoId,
    string NumeroContrato,
    Guid FuncaoId,
    string FuncaoNome,
    StatusLiberacaoTerceirizado Status,
    List<string> Pendencias);
