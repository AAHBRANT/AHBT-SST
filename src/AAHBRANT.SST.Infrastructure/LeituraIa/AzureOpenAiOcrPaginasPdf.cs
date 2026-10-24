using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure.LeituraIa;

// OCR das páginas digitalizadas pelo próprio gpt5mini (21/10/2026): o modelo já usado na leitura lê
// imagem, então não precisa de outro serviço no Azure. Uma chamada por página, em paralelo; a IA só
// transcreve (não interpreta) e o texto entra no mesmo fluxo de leitura das páginas com texto.
public class AzureOpenAiOcrPaginasPdf : IOcrPaginasPdf
{
    private const int Paralelismo = 4;

    private const string Instrucoes = """
        Você transcreve páginas digitalizadas de documentos de obras no Brasil (contratos, PGR, PCMSO).
        Copie TODO o texto legível da imagem, na ordem de leitura, sem resumir, corrigir ou completar.
        Tabelas: uma linha por linha da tabela, colunas separadas por " | ". Mantenha números, CNPJ, CPF,
        datas e nomes exatamente como impressos. Trecho ilegível = [ilegível]. Página sem texto = "".
        """;

    private record PaginaTranscrita(string Texto);

    private static readonly object Schema = new
    {
        type = "object",
        properties = new { texto = new { type = "string" } },
        required = new[] { "texto" },
        additionalProperties = false,
    };

    private readonly AzureOpenAiChatJsonCliente _chat;
    private readonly ILogger<AzureOpenAiOcrPaginasPdf> _logger;

    public AzureOpenAiOcrPaginasPdf(AzureOpenAiChatJsonCliente chat, ILogger<AzureOpenAiOcrPaginasPdf> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> CompletarAsync(byte[] pdf, IReadOnlyList<string> paginas, int maxPaginasOcr, CancellationToken ct)
    {
        var semTexto = paginas
            .Select((texto, indice) => (texto, indice))
            .Where(p => PaginasDigitalizadas.SemTexto(p.texto))
            .Select(p => p.indice)
            .Take(maxPaginasOcr)
            .ToList();
        if (semTexto.Count == 0) return paginas;

        Dictionary<int, byte[]> imagens;
        try
        {
            imagens = ImagemPaginaPdf.ExtrairJpegs(pdf, semTexto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível extrair as imagens do PDF para OCR");
            return paginas;
        }
        if (imagens.Count == 0) return paginas;

        var resultado = paginas.ToArray();
        var limite = new SemaphoreSlim(Paralelismo);
        var tarefas = imagens.Select(async par =>
        {
            await limite.WaitAsync(ct);
            try
            {
                var lida = await _chat.ObterAsync<PaginaTranscrita>(Instrucoes,
                    $"Transcreva o texto da página {par.Key + 1} do documento.",
                    "pagina_transcrita", Schema, ct, maxTokens: 8000, timeoutSegundos: 180,
                    imagens: new[] { $"data:image/jpeg;base64,{Convert.ToBase64String(par.Value)}" });
                resultado[par.Key] = lida.Texto ?? string.Empty;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Uma página que falha não derruba a leitura: ela fica vazia e o técnico confere/preenche.
                _logger.LogWarning(ex, "OCR da página {Pagina} falhou", par.Key + 1);
            }
            finally
            {
                limite.Release();
            }
        });
        await Task.WhenAll(tarefas);
        return resultado;
    }
}
