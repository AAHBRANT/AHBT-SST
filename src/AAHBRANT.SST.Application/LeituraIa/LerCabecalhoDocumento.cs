using MediatR;

namespace AAHBRANT.SST.Application.LeituraIa;

// Preenche o formulário de cadastro do PGR/PCMSO a partir do PDF (16/10/2026). Nada é gravado: o PDF é
// lido, o cabeçalho volta para a tela e só vira registro quando o técnico confere e clica em Adicionar.
public record LerCabecalhoPgrQuery(byte[] Pdf) : IRequest<CabecalhoCadastroPgrIa>;

public record LerCabecalhoPcmsoQuery(byte[] Pdf) : IRequest<CabecalhoCadastroPcmsoIa>;

public class LerCabecalhoDocumentoHandler :
    IRequestHandler<LerCabecalhoPgrQuery, CabecalhoCadastroPgrIa>,
    IRequestHandler<LerCabecalhoPcmsoQuery, CabecalhoCadastroPcmsoIa>
{
    // Capa, sumário, identificação e responsáveis ficam nas primeiras páginas; o resto só encarece.
    private const int PaginasLidas = 8;

    private readonly IExtratorTextoPdf _extrator;
    private readonly ILeitorCabecalhoDocumentoIa _leitor;
    private readonly IOcrPaginasPdf? _ocr;

    public LerCabecalhoDocumentoHandler(IExtratorTextoPdf extrator, ILeitorCabecalhoDocumentoIa leitor, IOcrPaginasPdf? ocr = null)
    {
        _extrator = extrator;
        _leitor = leitor;
        _ocr = ocr;
    }

    public async Task<CabecalhoCadastroPgrIa> Handle(LerCabecalhoPgrQuery request, CancellationToken ct)
        => await _leitor.LerCabecalhoPgrAsync(await PaginasAsync(request.Pdf, ct), ct);

    public async Task<CabecalhoCadastroPcmsoIa> Handle(LerCabecalhoPcmsoQuery request, CancellationToken ct)
        => await _leitor.LerCabecalhoPcmsoAsync(await PaginasAsync(request.Pdf, ct), ct);

    private async Task<IReadOnlyList<string>> PaginasAsync(byte[] pdf, CancellationToken ct)
    {
        IReadOnlyList<string> paginas;
        try
        {
            paginas = _extrator.ExtrairPaginas(pdf);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Não foi possível abrir o PDF. Confira se o arquivo é um PDF válido.");
        }
        IReadOnlyList<string> primeiras = paginas.Take(PaginasLidas).ToList();
        // PDF digitalizado: a IA lê as imagens das páginas (OCR) antes de ler o cabeçalho.
        if (_ocr is not null && primeiras.Any(PaginasDigitalizadas.SemTexto))
            primeiras = await _ocr.CompletarAsync(pdf, primeiras, PaginasLidas, ct);
        if (primeiras.All(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Não foi possível ler o texto do PDF, nem pelas imagens das páginas. Preencha os campos manualmente.");
        return primeiras;
    }
}
