using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EstoquesEpi.Reposicao;

public record SolicitacaoReposicaoEpiItemPdfModelo(
    string EpiNome, string? CertificadoAprovacaoNumero, int Saldo, int Reservado, int EstoqueMinimo, int QuantidadeSugerida, string? TamanhosEmFalta);

public record SolicitacaoReposicaoEpiPdfModelo(
    string ObraNome, byte[]? ObraLogoConteudo, string NumeroDocumento, DateTime EmitidaEmUtc, string SolicitanteNome,
    string? SolicitanteFuncao, int PrazoReposicaoDias, string? Observacao, IReadOnlyList<SolicitacaoReposicaoEpiItemPdfModelo> Itens,
    DateTime? AssinadoEmUtc, MetodoAutenticacaoAssinatura? MetodoAssinatura,
    string ConteudoHash, string UrlValidacaoPublica, byte[] QrCodePng);

public interface ISolicitacaoReposicaoEpiPdfService
{
    byte[] Gerar(SolicitacaoReposicaoEpiPdfModelo modelo);
}
