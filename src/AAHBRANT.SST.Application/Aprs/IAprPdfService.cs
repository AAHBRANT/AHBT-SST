using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Aprs;

public record AprPdfEnvolvido(string Nome, string? Funcao, bool Assinou);

public record AprPdfRiscoLinha(
    string Etapa,
    string PerigoEventoPerigoso,
    string? FonteCircunstancia,
    string? PossiveisLesoes,
    string? TrabalhadoresExpostos,
    int ProbabilidadeInicial,
    int SeveridadeInicial,
    NivelRiscoApr NivelRiscoInicial,
    string? MedidasPrevencao,
    string? Responsavel,
    int ProbabilidadeResidual,
    int SeveridadeResidual,
    NivelRiscoApr NivelRiscoResidual);

public record AprPdfAssinatura(string? Nome, string? Funcao, DateTime? Data);

// Modelo achatado (não o AprDetalheDto direto) — mesmo princípio de DdsPdfModelo: o serviço de PDF
// não depende do EF/Include, só dos dados já resolvidos pela query.
public record AprPdfModelo(
    string? NumeroApr,
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string AtividadeNome,
    string Local,
    string? MaquinasEquipamentos,
    string? PgrReferencia,
    DateTime Data,
    IReadOnlyList<AprPdfEnvolvido> Envolvidos,
    IReadOnlyList<AprPdfRiscoLinha> Riscos,
    AprPdfAssinatura Elaboracao,
    AprPdfAssinatura Supervisao,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);

public interface IAprPdfService
{
    byte[] Gerar(AprPdfModelo modelo);
}
