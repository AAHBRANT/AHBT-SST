using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public record ListarProcessosEleitoraisCipaQuery(Guid? ObraId = null) : IRequest<List<ProcessoEleitoralCipaDto>>;

public class ListarProcessosEleitoraisCipaQueryHandler : IRequestHandler<ListarProcessosEleitoraisCipaQuery, List<ProcessoEleitoralCipaDto>>
{
    private readonly IAppDbContext _db;
    public ListarProcessosEleitoraisCipaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ProcessoEleitoralCipaDto>> Handle(ListarProcessosEleitoraisCipaQuery request, CancellationToken ct)
    {
        var query = _db.ProcessosEleitoraisCipa
            .Include(p => p.Obra)
            .Include(p => p.Candidatos)
            .AsQueryable();
        if (request.ObraId.HasValue) query = query.Where(p => p.ObraId == request.ObraId.Value);

        var lista = await query.OrderByDescending(p => p.DataConvocacao).ToListAsync(ct);
        return lista.Select(MapearParaDto).ToList();
    }

    internal static ProcessoEleitoralCipaDto MapearParaDto(Domain.Entidades.ProcessoEleitoralCipa p) => new()
    {
        Id = p.Id,
        ObraId = p.ObraId,
        ObraNome = p.Obra?.Nome ?? string.Empty,
        NumeroDocumento = p.NumeroDocumento,
        DataConvocacao = p.DataConvocacao,
        DataInicioInscricoes = p.DataInicioInscricoes,
        DataFimInscricoes = p.DataFimInscricoes,
        DataVotacao = p.DataVotacao,
        DataApuracao = p.DataApuracao,
        Status = p.Status,
        TotalCandidatos = p.Candidatos.Count(c => c.Ativo),
    };
}

public record ObterProcessoEleitoralCipaDetalheQuery(Guid Id) : IRequest<ProcessoEleitoralCipaDetalheDto?>;

public class ObterProcessoEleitoralCipaDetalheQueryHandler : IRequestHandler<ObterProcessoEleitoralCipaDetalheQuery, ProcessoEleitoralCipaDetalheDto?>
{
    private readonly IAppDbContext _db;
    public ObterProcessoEleitoralCipaDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ProcessoEleitoralCipaDetalheDto?> Handle(ObterProcessoEleitoralCipaDetalheQuery request, CancellationToken ct)
    {
        var processo = await _db.ProcessosEleitoraisCipa
            .Include(p => p.Obra)
            .Include(p => p.Candidatos.Where(c => c.Ativo)).ThenInclude(c => c.Trabalhador)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (processo is null) return null;

        return new ProcessoEleitoralCipaDetalheDto
        {
            Processo = ListarProcessosEleitoraisCipaQueryHandler.MapearParaDto(processo),
            Candidatos = processo.Candidatos.Where(c => c.Ativo).Select(c => new CandidatoCipaDto
            {
                Id = c.Id,
                ProcessoEleitoralId = c.ProcessoEleitoralId,
                TrabalhadorId = c.TrabalhadorId,
                TrabalhadorNome = c.Trabalhador?.Nome ?? string.Empty,
                TrabalhadorMatricula = c.Trabalhador?.Matricula ?? string.Empty,
                DataInscricao = c.DataInscricao,
                Status = c.Status,
                MotivoIndeferimento = c.MotivoIndeferimento,
                VotosRecebidos = c.VotosRecebidos,
            }).OrderByDescending(c => c.VotosRecebidos).ToList(),
        };
    }
}
