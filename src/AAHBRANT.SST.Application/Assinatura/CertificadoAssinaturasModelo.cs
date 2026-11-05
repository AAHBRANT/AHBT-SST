using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Assinatura;

// Certificado de Assinaturas Eletrônicas (05/11): o mesmo modelo para todo documento com assinatura.
// Uma página de evidências por assinatura (ou duas por página nas listas grandes), com o resumo dela no
// topo, o QR de validação pública, a biometria cadastrada ao lado da capturada (marca CONFIDENCIAL) e o
// rastro técnico. O "painel" à esquerda é o que muda por documento (cupom de EPI, dados do curso, tema
// do DDS...). Começou na Ficha de EPI (LogAssinaturasEpiPdf).
public record CertificadoAssinaturasModelo(
    // Ex.: "Ficha de EPI", "Lista de Presença de Treinamento".
    string NomeDocumento,
    // Linha abaixo do título. Ex.: "Ficha de EPI · Alan Franklin dos Santos".
    string Subtitulo,
    string IdRegistro,
    DateTime EmitidoEmUtc,
    string Origem,
    string ConteudoHash,
    bool Pendente,
    List<CertificadoAssinaturaItem> Itens,
    // Cabeçalho e rodapé das páginas (os mesmos do documento).
    string ObraNome,
    byte[]? ObraLogoConteudo,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    // Listas com muitos participantes (DDS, treinamento): duas assinaturas por página, imagens menores.
    bool Compacto = false,
    // Painel comum a todas as assinaturas (ex.: dados do curso da turma). Vai uma vez só, no resumo da
    // 1ª página, em vez de repetir em cada evidência.
    PainelCertificado? PainelGeral = null);

public record CertificadoAssinaturaItem(
    string Descricao,
    string SignatarioNome,
    // Ex.: "Ajudante Geral · CBO 7170-20 · Mat. 30" ou "Técnico de Segurança · MTE-0123056/SP".
    string? SignatarioDetalhe,
    DateTime? TermoAceiteEm,
    DateTime? ConsentimentoLgpdEm,
    EvidenciaAssinaturaPdf Evidencia,
    PainelCertificado? Painel = null);

// Dados técnicos e biométricos de uma assinatura (DocumentoSignatario + referências do cadastro vigentes
// na data da assinatura).
public record EvidenciaAssinaturaPdf(
    MetodoAutenticacaoAssinatura Metodo,
    DateTime AssinadoEmUtc,
    DateTime? CriadoEmUtc,
    string? Ip,
    string? UserAgent,
    StatusLocalizacaoAssinatura LocalizacaoStatus,
    double? Latitude,
    double? Longitude,
    double? PrecisaoMetros,
    string? ValidacaoModelo,
    string? ValidacaoGrupoId,
    string? ValidacaoRequisicaoId,
    Guid? LeitorId,
    byte[]? EvidenciaAssinatura,
    string? EvidenciaAssinaturaHash,
    byte[]? FotoCadastro,
    DateTime? FotoCadastroEmUtc,
    string? FotoCadastroHash,
    byte[]? DigitalCadastroImagem,
    DateTime? DigitalCadastroEmUtc,
    byte[]? QrValidacaoPng = null);

// Quadro à esquerda das imagens: título, subtítulo e seções com linhas "rótulo: valor" e itens com foto.
public record PainelCertificado(string Titulo, string? Subtitulo, List<SecaoPainelCertificado> Secoes);

public record SecaoPainelCertificado(
    string Titulo,
    List<LinhaPainelCertificado>? Linhas = null,
    List<ItemPainelCertificado>? Itens = null);

public record LinhaPainelCertificado(string Rotulo, string Valor);

public record ItemPainelCertificado(string Nome, List<string> Detalhes, byte[]? Foto = null);
