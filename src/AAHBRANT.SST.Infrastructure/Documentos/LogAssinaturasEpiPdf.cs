using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.EntregasEpi;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Páginas finais da Ficha de EPI: Certificado de Assinaturas Eletrônicas (pedido do usuário, 06/10;
// formato de 05/11). Desde 05/11 o desenho é o modelo único CertificadoAssinaturasPdf — aqui só se
// converte o log de EPI para ele, com o cupom da entrega como painel à esquerda das imagens.
internal static class LogAssinaturasEpiPdf
{
    public static void Adicionar(QuestPDF.Infrastructure.IDocumentContainer documento, FichaEpiPdfModelo modelo, LogAssinaturasFichaEpi log) =>
        CertificadoAssinaturasPdf.Adicionar(documento, Converter(modelo, log));

    internal static CertificadoAssinaturasModelo Converter(FichaEpiPdfModelo modelo, LogAssinaturasFichaEpi log)
    {
        // Pendente quando alguma entrega ou devolução da ficha ainda não tem a assinatura do funcionário.
        var pendente = modelo.Entregas.Any(e => !e.AssinadoPeloEmpregado) || modelo.Devolucoes.Any(d => !d.AssinadoPeloEmpregado);
        var funcao = string.IsNullOrWhiteSpace(modelo.TrabalhadorFuncaoCbo)
            ? modelo.TrabalhadorFuncaoNome
            : $"{modelo.TrabalhadorFuncaoNome} · CBO {modelo.TrabalhadorFuncaoCbo}";

        var itens = log.Itens.Select(item => new CertificadoAssinaturaItem(
            item.Descricao,
            modelo.TrabalhadorNome,
            $"{funcao} · Mat. {modelo.TrabalhadorMatricula}",
            log.TermoAceiteEm,
            log.ConsentimentoLgpdEm,
            new EvidenciaAssinaturaPdf(
                item.Metodo, item.AssinadoEmUtc, item.CriadoEmUtc, item.Ip, item.UserAgent, item.LocalizacaoStatus,
                item.Latitude, item.Longitude, item.PrecisaoMetros, item.ValidacaoModelo, item.ValidacaoGrupoId,
                item.ValidacaoRequisicaoId, item.LeitorId, item.EvidenciaAssinatura, item.EvidenciaAssinaturaHash,
                item.FotoCadastro, item.FotoCadastroEmUtc, item.FotoCadastroHash, item.DigitalCadastroImagem,
                item.DigitalCadastroEmUtc, item.QrValidacaoPng),
            item.Cupom is { } cupom ? PainelCupom(modelo, cupom, item) : null)).ToList();

        return new CertificadoAssinaturasModelo(
            "Ficha de EPI",
            $"Ficha de EPI · {modelo.TrabalhadorNome}",
            log.IdRegistro,
            log.EmitidoEmUtc,
            $"Sistema SST · {modelo.ObraNome}",
            modelo.ConteudoHash,
            pendente,
            itens,
            modelo.ObraNome,
            modelo.ObraLogoConteudo,
            modelo.UrlValidacaoPublica,
            modelo.QrCodePng);
    }

    private static PainelCertificado PainelCupom(FichaEpiPdfModelo modelo, CupomEntregaEpi cupom, LogAssinaturaEpiItem item)
    {
        var documentacao = new List<LinhaPainelCertificado> { new("Motivo", cupom.Motivo ?? "—") };
        if (!string.IsNullOrWhiteSpace(cupom.ObservacaoMotivo)) documentacao.Add(new("Obs. motivo", cupom.ObservacaoMotivo!));
        documentacao.Add(new("Lista NR-6", cupom.ListaNr6 ?? "—"));
        documentacao.Add(new("Trein. NR-6", Dia(cupom.TreinamentoNr6)));
        documentacao.Add(new("Responsável", cupom.Responsavel ?? "—"));

        return new PainelCertificado(
            "EPIs recebidos",
            $"Cupom da entrega assinada em {CertificadoAssinaturasPdf.Data(item.AssinadoEmUtc)}",
            new List<SecaoPainelCertificado>
            {
                new("Identificação", new List<LinhaPainelCertificado>
                {
                    new("Funcionário", modelo.TrabalhadorNome),
                    new("Função", modelo.TrabalhadorFuncaoNome),
                    new("Obra", modelo.ObraNome),
                    new("Data", cupom.DataEntrega.ToString("dd/MM/yyyy")),
                }),
                new("Itens do carrinho", Itens: cupom.Itens.Select(epi => new ItemPainelCertificado(
                    epi.Nome,
                    new List<string>
                    {
                        $"Fabricante: {epi.Fabricante ?? "—"}  ·  Qtd: {epi.Quantidade}",
                        $"CA: {epi.CertificadoAprovacao ?? "—"}  ·  Val. CA: {Dia(epi.ValidadeCertificado)}",
                        $"Val. entrega: {Dia(epi.ValidadeEntrega)}",
                    },
                    epi.Foto)).ToList()),
                new("Documentação e motivo", documentacao),
            });
    }

    // Mantido para os testes e outros PDFs que já descreviam o equipamento por aqui.
    internal static string Equipamento(string? userAgent) => CertificadoAssinaturasPdf.Equipamento(userAgent);

    private static string Dia(DateTime? data) => data is { } d ? d.ToString("dd/MM/yyyy") : "—";
}
