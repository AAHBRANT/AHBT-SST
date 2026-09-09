using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.PermissoesTrabalho;

public record PtPdfItemBinario(string Rotulo, bool Marcado);
public record PtPdfTipoTrabalho(string Rotulo, string? DescricaoOutro);
public record PtPdfVerificacao(string Rotulo, RespostaVerificacaoPt? Resposta);
public record PtPdfEpi(string Rotulo, string? Complemento);
public record PtPdfRiscoCritico(string RiscoCondicao, string? ControleComplementar, string? ResponsavelEvidencia);
public record PtPdfEnvolvido(string Nome, string? Funcao, bool Assinou);
public record PtPdfAssinatura(string? Nome, DateTime? Data);
public record PtPdfSuspensao(string? Nome, DateTime? Data, string? Motivo);
public record PtPdfEncerramento(string? Nome, DateTime? Data, string? Observacoes);

public record PtPdfModelo(
    string? NumeroPt,
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string DescricaoAtividade,
    string Local,
    string? EmpresaExecutante,
    DateTime Data,
    TimeSpan? HorarioInicio,
    TimeSpan? HorarioFim,
    DateTime? Validade,
    string? ResponsavelExecucaoNome,
    string? ResponsavelAreaNome,
    List<PtPdfItemBinario> PreRequisitos,
    List<PtPdfTipoTrabalho> TiposTrabalho,
    List<PtPdfVerificacao> Verificacoes,
    List<PtPdfEpi> Epis,
    string? OutrosEpis,
    List<string> Epcs,
    string? OutrosEpcs,
    List<PtPdfRiscoCritico> RiscosCriticos,
    PtPdfAssinatura Emitente,
    PtPdfAssinatura Execucao,
    PtPdfAssinatura? Sst,
    PtPdfSuspensao? Suspensao,
    PtPdfEncerramento? Encerramento,
    List<PtPdfEnvolvido> Envolvidos,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);

public interface IPtPdfService
{
    byte[] Gerar(PtPdfModelo modelo);
}
