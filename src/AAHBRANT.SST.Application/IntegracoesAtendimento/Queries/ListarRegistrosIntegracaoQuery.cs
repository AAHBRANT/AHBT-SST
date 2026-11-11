using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;

// "Integração realizada" no perfil › Treinamentos: registro isolado, sem certificado. Pelo escopo do trabalhador
// (técnico de outra obra recebe 404), sem INNER JOIN: o histórico continua aparecendo se a obra for desativada.
public record RegistroIntegracaoDto(Guid Id, DateTime RealizadaEmUtc, Guid ObraId, string ObraNome, string TecnicoNome, string FuncaoNome, string NumeroAtendimento);

public record ListarRegistrosIntegracaoQuery(Guid TrabalhadorId) : IRequest<List<RegistroIntegracaoDto>>;

public class ListarRegistrosIntegracaoQueryHandler : IRequestHandler<ListarRegistrosIntegracaoQuery, List<RegistroIntegracaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarRegistrosIntegracaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RegistroIntegracaoDto>> Handle(ListarRegistrosIntegracaoQuery request, CancellationToken ct)
    {
        await _db.GarantirTrabalhadorNoEscopoAsync(request.TrabalhadorId, ct);

        var registros = await _db.RegistrosIntegracao.IgnoreQueryFilters()
            .Where(r => r.Ativo && r.TrabalhadorId == request.TrabalhadorId)
            .OrderByDescending(r => r.RealizadaEm)
            .ToListAsync(ct);
        var obraIds = registros.Select(r => r.ObraId).Distinct().ToList();
        var atendimentoIds = registros.Select(r => r.IntegracaoAtendimentoId).Distinct().ToList();
        var obras = await _db.Obras.IgnoreQueryFilters().Where(o => obraIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        var numeros = await _db.IntegracoesAtendimento.IgnoreQueryFilters().Where(a => atendimentoIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Numero, ct);

        return registros.Select(r => new RegistroIntegracaoDto(
            r.Id, r.RealizadaEm, r.ObraId, obras.GetValueOrDefault(r.ObraId, string.Empty), r.TecnicoNome, r.FuncaoNome,
            numeros.GetValueOrDefault(r.IntegracaoAtendimentoId, string.Empty))).ToList();
    }
}
