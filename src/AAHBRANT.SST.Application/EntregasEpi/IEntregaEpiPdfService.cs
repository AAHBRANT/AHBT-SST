namespace AAHBRANT.SST.Application.EntregasEpi;

public record EntregaEpiPdfModelo(
    string ObraNome,
    string TrabalhadorNome,
    string TrabalhadorMatricula,
    string TrabalhadorFuncaoNome,
    string EpiNome,
    string? EpiFabricante,
    string? CertificadoAprovacaoNumero,
    DateTime DataEntrega,
    DateTime? DataDevolucao,
    DateTime? DataValidade,
    int Quantidade,
    int? QuantidadeDevolucao,
    string? VistoConsorcioResponsavel,
    string? Motivo,
    string? Observacoes);

public interface IEntregaEpiPdfService
{
    byte[] Gerar(EntregaEpiPdfModelo modelo);
}
