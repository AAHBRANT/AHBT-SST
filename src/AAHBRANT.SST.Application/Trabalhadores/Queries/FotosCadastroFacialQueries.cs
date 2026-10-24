using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public class FotoCadastroFacialDto
{
    public Guid Id { get; set; }
    public DateTime CapturadaEm { get; set; }
    public string HashSha256 { get; set; } = string.Empty;
}

// Fotos usadas no cadastro facial do trabalhador, da mais recente para a mais antiga (sem os bytes —
// o conteúdo é baixado sob demanda por ObterFotoCadastroFacialQuery).
public record ListarFotosCadastroFacialQuery(Guid TrabalhadorId) : IRequest<List<FotoCadastroFacialDto>>;

public class ListarFotosCadastroFacialQueryHandler : IRequestHandler<ListarFotosCadastroFacialQuery, List<FotoCadastroFacialDto>>
{
    private readonly IAppDbContext _db;

    public ListarFotosCadastroFacialQueryHandler(IAppDbContext db) => _db = db;

    public Task<List<FotoCadastroFacialDto>> Handle(ListarFotosCadastroFacialQuery request, CancellationToken ct) =>
        _db.FotosCadastroFacial
            .Where(f => f.TrabalhadorId == request.TrabalhadorId)
            .OrderByDescending(f => f.CapturadaEm)
            .Select(f => new FotoCadastroFacialDto { Id = f.Id, CapturadaEm = f.CapturadaEm, HashSha256 = f.HashSha256 })
            .ToListAsync(ct);
}

public record ObterFotoCadastroFacialQuery(Guid TrabalhadorId, Guid FotoId) : IRequest<FotoTrabalhadorResultado?>;

public class ObterFotoCadastroFacialQueryHandler : IRequestHandler<ObterFotoCadastroFacialQuery, FotoTrabalhadorResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoCadastroFacialQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoTrabalhadorResultado?> Handle(ObterFotoCadastroFacialQuery request, CancellationToken ct)
    {
        // TrabalhadorId entra no filtro de propósito: a foto só sai pelo perfil do dono dela.
        var foto = await _db.FotosCadastroFacial
            .FirstOrDefaultAsync(f => f.Id == request.FotoId && f.TrabalhadorId == request.TrabalhadorId, ct);
        if (foto is null) return null;

        return new FotoTrabalhadorResultado
        {
            Conteudo = foto.Conteudo,
            ContentType = foto.ContentType,
            NomeArquivo = $"trabalhador-{foto.TrabalhadorId}-facial-{foto.CapturadaEm:yyyyMMddHHmmss}.jpg",
        };
    }
}
