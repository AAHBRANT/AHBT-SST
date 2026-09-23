namespace AAHBRANT.SST.Application.Assinatura;

// Vocabulário único dos tipos de documento do Motor de Assinatura Eletrônica: o nome técnico da
// entidade (DocumentoAssinatura.EntidadeTipo, sempre um nameof ou chave sintética) nunca deve
// aparecer para quem lê o documento — nem na página pública de validação, nem no painel
// administrativo. Aqui ficam o rótulo institucional e a natureza de cada tipo.
//
// Natureza CONSOLIDADA: documentos que agregam N registros individuais e, por isso, nunca recebem
// signatário próprio — quem assina são os registros que eles reúnem (ver comentário em
// ExportarFichaEpiTrabalhadorQuery: "ninguém assina a Ficha em si"). Sem essa distinção, a página
// pública dizia "Nenhuma assinatura eletrônica registrada" para uma Ficha de EPI cujo PDF mostra
// assinatura em cada linha — contradição visível a fiscal, cliente e auditoria.
public static class TipoDocumentoAssinatura
{
    private static readonly Dictionary<string, string> Rotulos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Apr"] = "APR — Análise Preliminar de Risco",
        ["Dds"] = "DDS — Diálogo Diário de Segurança",
        ["DdsSemanal"] = "Registro Semanal de DDS",
        ["Inspecao"] = "Inspeção de Segurança",
        ["PermissaoTrabalho"] = "Permissão de Trabalho",
        ["ProcessoEleitoralCipa"] = "Ata do Processo Eleitoral da CIPA",
        ["ReuniaoCipa"] = "Ata de Reunião da CIPA",
        ["FichaEpiTrabalhador"] = "Ficha de EPI",
        ["SessaoTreinamento"] = "Ata de Sessão de Treinamento",
        ["Treinamento"] = "Certificado de Treinamento",
        ["EntregaEpi"] = "Entrega de EPI",
        ["DevolucaoEpi"] = "Devolução de EPI",
        ["EntregaUniforme"] = "Entrega de Uniforme",
        ["NaoConformidade"] = "Registro de Não Conformidade",
    };

    // Onde estão, de fato, as assinaturas de cada documento consolidado. A frase entra na página
    // pública no lugar de "nenhuma assinatura registrada".
    private static readonly Dictionary<string, string> OrigemAssinaturasConsolidadas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FichaEpiTrabalhador"] = "Documento consolidado: as assinaturas são colhidas em cada entrega e devolução de EPI relacionada nesta ficha.",
        ["SessaoTreinamento"] = "Documento consolidado: as assinaturas são colhidas no certificado de cada participante da turma.",
        ["DdsSemanal"] = "Documento consolidado: as assinaturas são colhidas no DDS de cada dia da semana.",
    };

    public static string Rotulo(string entidadeTipo) =>
        Rotulos.TryGetValue(entidadeTipo, out var rotulo) ? rotulo : entidadeTipo;

    public static bool EhConsolidado(string entidadeTipo) =>
        OrigemAssinaturasConsolidadas.ContainsKey(entidadeTipo);

    public static string? OrigemAssinaturas(string entidadeTipo) =>
        OrigemAssinaturasConsolidadas.TryGetValue(entidadeTipo, out var origem) ? origem : null;
}
