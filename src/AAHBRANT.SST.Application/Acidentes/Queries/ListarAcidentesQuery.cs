using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Queries;

public record ListarAcidentesQuery(TipoOcorrencia? Tipo, StatusAcidente? Status, Guid? ObraId)
    : IRequest<List<AcidenteDto>>;

public class ListarAcidentesQueryHandler : IRequestHandler<ListarAcidentesQuery, List<AcidenteDto>>
{
    private readonly IAppDbContext _db;

    public ListarAcidentesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AcidenteDto>> Handle(ListarAcidentesQuery request, CancellationToken ct)
    {
        var query = _db.Acidentes.AsQueryable();

        if (request.Tipo.HasValue)
            query = query.Where(a => a.Tipo == request.Tipo.Value);

        if (request.Status.HasValue)
            query = query.Where(a => a.Status == request.Status.Value);

        if (request.ObraId.HasValue)
            query = query.Where(a => a.ObraId == request.ObraId.Value);

        return await query
            .Include(a => a.Obra)
            .Include(a => a.Trabalhador)
            .Include(a => a.Atividade)
            .OrderByDescending(a => a.Data)
            .Select(a => new AcidenteDto
            {
                Id = a.Id,
                Tipo = a.Tipo,
                ObraId = a.ObraId,
                ObraNome = a.Obra != null ? a.Obra.Nome : null,
                TrabalhadorId = a.TrabalhadorId,
                TrabalhadorNome = a.Trabalhador != null ? a.Trabalhador.Nome : null,
                AtividadeId = a.AtividadeId,
                AtividadeNome = a.Atividade != null ? a.Atividade.Nome : null,
                Local = a.Local,
                Data = a.Data,
                Hora = a.Hora,
                Descricao = a.Descricao,
                Lesao = a.Lesao,
                Consequencia = a.Consequencia,
                Atendimento = a.Atendimento,
                HouveAfastamento = a.HouveAfastamento,
                DiasAfastamento = a.DiasAfastamento,
                NumeroCat = a.NumeroCat,
                Gravidade = a.Gravidade,
                DiasDebitados = a.DiasDebitados,
                MetodologiaInvestigacao = a.MetodologiaInvestigacao,
                Causas = a.Causas,
                Status = a.Status,
                DataConclusaoInvestigacao = a.DataConclusaoInvestigacao,
            })
            .ToListAsync(ct);
    }
}
