using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Instrutores;

public record PerfilInstrutorDto(
    Guid Id,
    Guid TrabalhadorId,
    string TrabalhadorNome,
    string? FuncaoNome,
    string? ObraNome,
    FormacaoInstrutor Formacao,
    string FormacaoDescricao,
    ConselhoInstrutor? Conselho,
    string? NumeroRegistro,
    string? UfRegistro,
    string? RegistroDescricao,
    bool PodeAssinarEmOutrasObras,
    DateTime? AutorizadoOutrasObrasEm);

// Textos que saem no certificado e na tela. Ficam num lugar só para o PDF e a tela não divergirem.
public static class DescricaoInstrutor
{
    public static string Formacao(FormacaoInstrutor formacao) => formacao switch
    {
        FormacaoInstrutor.EngenheiroSegurancaTrabalho => "Engenheiro de Segurança do Trabalho",
        _ => "Técnico de Segurança do Trabalho",
    };

    // "MTE nº 12345/PB"; nulo quando o número não foi informado (o certificado não inventa registro).
    public static string? Registro(ConselhoInstrutor? conselho, string? numero, string? uf)
    {
        if (string.IsNullOrWhiteSpace(numero)) return null;
        var sigla = conselho switch
        {
            ConselhoInstrutor.Crea => "CREA",
            ConselhoInstrutor.Mte => "MTE",
            _ => "Registro",
        };
        var sufixo = string.IsNullOrWhiteSpace(uf) ? string.Empty : $"/{uf.Trim().ToUpperInvariant()}";
        return $"{sigla} nº {numero.Trim()}{sufixo}";
    }
}
