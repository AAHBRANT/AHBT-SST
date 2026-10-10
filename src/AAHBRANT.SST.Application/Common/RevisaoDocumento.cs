namespace AAHBRANT.SST.Application.Common;

// Regras comuns à "Nova revisão" do PGR e do PCMSO (10/10/2026).
public static class RevisaoDocumento
{
    public const int TamanhoMaximoBytes = 20 * 1024 * 1024;

    public const string MotivoDocumentoAnterior = "Documento anexado antes do histórico de revisões";
    public const string NomeDocumentoAnterior = "documento-anterior.pdf";

    // Número livre abaixo da revisão nova para arquivar o PDF antigo; nulo quando não sobra número
    // (o chamador recusa a operação em vez de perder o PDF).
    public static int? NumeroLivreAbaixo(int numeroNovo, IEnumerable<int> existentes)
    {
        var usados = existentes.ToHashSet();
        for (var n = numeroNovo - 1; n >= 0; n--)
            if (!usados.Contains(n)) return n;
        return null;
    }
}
