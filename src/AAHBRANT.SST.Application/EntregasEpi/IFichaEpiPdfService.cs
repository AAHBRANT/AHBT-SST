using AAHBRANT.SST.Domain.Enums;

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
    string? NumeroCertificadoNr6 = null);

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
    MetodoAutenticacaoAssinatura? MetodoResponsavel = null);

public record LinhaDevolucaoEpiPdf(
    int NumeroReferenciaEntrega,
    string EpiNome,
    int QuantidadeDevolvida,
    DateTime DataDevolucao,
    bool AssinadoPeloEmpregado,
    DateTime? AssinadoPeloEmpregadoEm,
    string? VistoResponsavel,
    MetodoAutenticacaoAssinatura? MetodoEmpregado = null);

public interface IFichaEpiPdfService
{
    byte[] Gerar(FichaEpiPdfModelo modelo);
}
