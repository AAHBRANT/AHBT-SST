using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

// Nunca inclui o segredo nem o hash: só o que a tela de administração precisa mostrar.
public class DispositivoAgenteDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public DateTime? UltimaSincronizacaoEm { get; set; }
    public DateTime RegistradoEm { get; set; }
}

public record ListarDispositivosAgenteQuery(Guid? ObraId = null) : IRequest<List<DispositivoAgenteDto>>;

public class ListarDispositivosAgenteQueryHandler : IRequestHandler<ListarDispositivosAgenteQuery, List<DispositivoAgenteDto>>
{
    private readonly IAppDbContext _db;

    public ListarDispositivosAgenteQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<DispositivoAgenteDto>> Handle(ListarDispositivosAgenteQuery request, CancellationToken ct)
    {
        var dispositivos = await _db.DispositivosAgenteBiometrico
            .Where(d => request.ObraId == null || d.ObraId == request.ObraId)
            .OrderBy(d => d.Obra!.Nome).ThenBy(d => d.Nome)
            .Select(d => new { d.Id, d.ObraId, ObraNome = d.Obra!.Nome, d.Nome, d.UltimaSincronizacaoEm, Registrado = d.CreatedAtUtc })
            .ToListAsync(ct);

        // O banco devolve datetime2 sem fuso (Kind Unspecified) e o JSON sairia sem "Z": o navegador leria
        // como hora local e a tela mostraria o horário e o "conectado" errados. Marca como UTC (é o que são).
        return dispositivos.Select(d => new DispositivoAgenteDto
        {
            Id = d.Id,
            ObraId = d.ObraId,
            ObraNome = d.ObraNome,
            Nome = d.Nome,
            UltimaSincronizacaoEm = d.UltimaSincronizacaoEm is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null,
            RegistradoEm = DateTime.SpecifyKind(d.Registrado, DateTimeKind.Utc),
        }).ToList();
    }
}
