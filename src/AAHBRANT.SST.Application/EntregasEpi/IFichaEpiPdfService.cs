using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;

using AAHBRANT.SST.Application.TermosCompromissoEpi;

namespace AAHBRANT.SST.Application.EntregasEpi;

// ObraCliente ("empresa contratante") não estava no literal original da spec, mas a seção de
// decisões da mesma spec (2026-08-27-ficha-epi-reformulada-design.md) é explícita: "Empresa
// contratante / CNPJ: vem do cadastro de Obra (campo Cliente já existente + novo campo Cnpj)" —
// sem este campo não há como preencher "Empresa contratante" na identificação (item 1 do modelo
// oficial), que é distinto de "Obra / Frente de trabalho" (ObraNome).
public record FichaEpiPdfModelo(
    string ObraNome,
    string? ObraCliente,
    string? ObraCnpj,
    byte[]? ObraLogoConteudo,
    string? ObraLogoContentType,
    string TrabalhadorNome,
    string TrabalhadorCpfMascarado,
    string TrabalhadorMatricula,
    string TrabalhadorFuncaoNome,
    string? TrabalhadorTurno,
    DateTime TrabalhadorDataAdmissao,
    List<LinhaEntregaEpiPdf> Entregas,
    List<LinhaDevolucaoEpiPdf> Devolucoes,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    // Certificado de NR-06 do trabalhador (mesma escolha da tela de entrega: o de validade mais
    // distante entre os cursos marcados AtendeNr6). Preenche a cláusula 2 do termo com a data de
    // realização e o nº do certificado — nulos quando ele não tem certificado de NR-06.
    DateTime? DataTreinamentoNr6 = null,
    string? NumeroCertificadoNr6 = null,
    // Situação do termo de recebimento e compromisso (digital, em papel ou pendente) — vira o
    // bloco de assinatura no fim do item 4 do PDF.
    TermoCompromissoEpiDto? Termo = null,
    // Cabeçalho de identificação (pedido do usuário, 06/10): CPF completo (liberado pelo jurídico) e
    // a foto de cadastro do funcionário. Sem eles (testes antigos), vale o CPF mascarado e sem foto.
    string? TrabalhadorCpf = null,
    byte[]? TrabalhadorFoto = null,
    // Páginas finais da ficha com o log de todas as assinaturas de EPI do funcionário. Nulo = sem log.
    LogAssinaturasFichaEpi? Log = null,
    // CBO da função (05/11), ao lado do nome da função na identificação. Ex.: "7170-20".
    string? TrabalhadorFuncaoCbo = null);

// Log de assinaturas de EPI do funcionário (só entrega, devolução e termo). Vai nas páginas finais da
// Ficha de EPI, no estilo de um certificado de conclusão: resumo, uma linha por assinatura e uma
// página de evidências por assinatura.
public record LogAssinaturasFichaEpi(
    string IdRegistro,
    DateTime EmitidoEmUtc,
    int TotalAssinaturas,
    DateTime? TermoAceiteEm,
    DateTime? ConsentimentoLgpdEm,
    List<LogAssinaturaEpiItem> Itens);

public enum TipoLogAssinaturaEpi { Entrega = 1, Devolucao = 2, TermoCompromisso = 3 }

public record LogAssinaturaEpiItem(
    TipoLogAssinaturaEpi Tipo,
    string Descricao,
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
    // Evidência capturada na assinatura: foto do rosto (facial) ou imagem da impressão (digital).
    byte[]? EvidenciaAssinatura,
    string? EvidenciaAssinaturaHash,
    // Referências do cadastro vigentes na data da assinatura.
    byte[]? FotoCadastro,
    DateTime? FotoCadastroEmUtc,
    string? FotoCadastroHash,
    byte[]? DigitalCadastroImagem,
    DateTime? DigitalCadastroEmUtc,
    CupomEntregaEpi? Cupom,
    // QR da validação pública desta assinatura (05/11): leva à página /#/validar do documento da
    // entrega/devolução. Nulo quando o documento ainda não tem token (não finalizado).
    byte[]? QrValidacaoPng = null,
    string? UrlValidacao = null);

// Mesmo conteúdo do canhoto "EPIs recebidos" impresso do carrinho, reconstruído dos dados da entrega.
public record CupomEntregaEpi(
    DateTime DataEntrega,
    string? Motivo,
    string? ObservacaoMotivo,
    string? ListaNr6,
    DateTime? TreinamentoNr6,
    string? Responsavel,
    List<ItemCupomEpi> Itens);

public record ItemCupomEpi(
    string Nome, string? Fabricante, int Quantidade, string? CertificadoAprovacao,
    DateTime? ValidadeCertificado, DateTime? ValidadeEntrega, byte[]? Foto);

public record LinhaEntregaEpiPdf(
    int Numero,
    string EpiNome,
    string? CertificadoAprovacaoNumero,
    MotivoEntregaEpi? MotivoTipo,
    string? MotivoObservacao,
    int Quantidade,
    DateTime DataEntrega,
    bool AssinadoPeloEmpregado,
    bool AssinadoPeloResponsavel,
    DateTime? AssinadoPeloEmpregadoEm,
    DateTime? AssinadoPeloResponsavelEm,
    MetodoAutenticacaoAssinatura? MetodoEmpregado = null,
    MetodoAutenticacaoAssinatura? MetodoResponsavel = null,
    // Foto do item no catálogo (05/11): miniatura antes do nome no Controle de Entrega.
    byte[]? EpiFoto = null,
    // Selo compacto na linha (05/11): quem assinou, registro (CBO / MTE) e código.
    SeloAssinaturaPdf? SeloEmpregado = null,
    SeloAssinaturaPdf? SeloResponsavel = null);


public record LinhaDevolucaoEpiPdf(
    int NumeroReferenciaEntrega,
    string EpiNome,
    int QuantidadeDevolvida,
    DateTime DataDevolucao,
    bool AssinadoPeloEmpregado,
    DateTime? AssinadoPeloEmpregadoEm,
    string? VistoResponsavel,
    MetodoAutenticacaoAssinatura? MetodoEmpregado = null,
    // Foto do item no catálogo (05/11): miniatura antes do nome no Controle de Devolução.
    byte[]? EpiFoto = null,
    SeloAssinaturaPdf? SeloEmpregado = null);

public interface IFichaEpiPdfService
{
    byte[] Gerar(FichaEpiPdfModelo modelo);
}
