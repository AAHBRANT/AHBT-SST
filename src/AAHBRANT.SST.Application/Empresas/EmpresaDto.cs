namespace AAHBRANT.SST.Application.Empresas;

public record EmpresaDto(
    Guid Id,
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? TipoServicoPrestado,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail,
    string Status);
