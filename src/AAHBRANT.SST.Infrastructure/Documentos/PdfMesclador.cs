using AAHBRANT.SST.Application.Dds;
using QuestPDF.Fluent;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// O QuestPDF só mescla PDFs prontos a partir de arquivos (DocumentOperation), então grava os
// PDFs numa pasta temporária, junta e apaga a pasta no fim.
public class PdfMesclador : IPdfMesclador
{
    public byte[] Mesclar(IReadOnlyList<byte[]> pdfs)
    {
        if (pdfs.Count == 0) throw new ArgumentException("Nenhum PDF para mesclar.", nameof(pdfs));
        if (pdfs.Count == 1) return pdfs[0];

        var pasta = Path.Combine(Path.GetTempPath(), "sst-mesclar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            var arquivos = new List<string>();
            for (var i = 0; i < pdfs.Count; i++)
            {
                var caminho = Path.Combine(pasta, $"{i:D3}.pdf");
                File.WriteAllBytes(caminho, pdfs[i]);
                arquivos.Add(caminho);
            }

            var saida = Path.Combine(pasta, "saida.pdf");
            var operacao = DocumentOperation.LoadFile(arquivos[0]);
            foreach (var arquivo in arquivos.Skip(1))
                operacao = operacao.MergeFile(arquivo);
            operacao.Save(saida);

            return File.ReadAllBytes(saida);
        }
        finally
        {
            try { Directory.Delete(pasta, recursive: true); } catch { /* pasta temporária: limpeza best-effort */ }
        }
    }
}
