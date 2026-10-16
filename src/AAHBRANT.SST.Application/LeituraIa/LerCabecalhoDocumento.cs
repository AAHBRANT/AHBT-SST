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

    public LerCabecalhoDocumentoHandler(IExtratorTextoPdf extrator, ILeitorCabecalhoDocumentoIa leitor)
    {
        _extrator = extrator;
        _leitor = leitor;
    }

    public Task<CabecalhoCadastroPgrIa> Handle(LerCabecalhoPgrQuery request, CancellationToken ct)
        => _leitor.LerCabecalhoPgrAsync(Paginas(request.Pdf), ct);

    public Task<CabecalhoCadastroPcmsoIa> Handle(LerCabecalhoPcmsoQuery request, CancellationToken ct)
        => _leitor.LerCabecalhoPcmsoAsync(Paginas(request.Pdf), ct);

    private IReadOnlyList<string> Paginas(byte[] pdf)
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
        var primeiras = paginas.Take(PaginasLidas).ToList();
        if (primeiras.All(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("O PDF não tem texto (parece digitalizado). Preencha os campos manualmente.");
        return primeiras;
    }
}
