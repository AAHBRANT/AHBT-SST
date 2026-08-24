using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Queries;

public record ListarPermissoesTrabalhoQuery(Guid? AtividadeId = null) : IRequest<List<PermissaoTrabalhoDto>>;

public class ListarPermissoesTrabalhoQueryHandler : IRequestHandler<ListarPermissoesTrabalhoQuery, List<PermissaoTrabalhoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPermissoesTrabalhoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PermissaoTrabalhoDto>> Handle(ListarPermissoesTrabalhoQuery request, CancellationToken ct)
    {
        var query = _db.PermissoesTrabalho
            .Include(p => p.Atividade)
            .Include(p => p.Equipe)
            .Include(p => p.AutorizadoPorUsuario)
            .Include(p => p.EncerradaPorUsuario)
            .AsQueryable();

        if (request.AtividadeId.HasValue)
            query = query.Where(p => p.AtividadeId == request.AtividadeId.Value);

        var permissoes = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);

        return permissoes.Select(p => new PermissaoTrabalhoDto
        {
            Id = p.Id,
            AtividadeId = p.AtividadeId,
            AtividadeNome = p.Atividade?.Nome ?? string.Empty,
            Local = p.Local,
            EquipeId = p.EquipeId,
            EquipeNome = p.Equipe?.Nome,
            Data = p.Data,
            HorarioInicio = p.HorarioInicio,
            HorarioFim = p.HorarioFim,
            Validade = p.Validade,
            Status = p.Status,
            AutorizadoPorUsuarioId = p.AutorizadoPorUsuarioId,
            AutorizadoPorUsuarioNome = p.AutorizadoPorUsuario?.Nome,
            DataAutorizacao = p.DataAutorizacao,
            EncerradaPorUsuarioId = p.EncerradaPorUsuarioId,
            EncerradaPorUsuarioNome = p.EncerradaPorUsuario?.Nome,
            DataEncerramento = p.DataEncerramento,
            ObservacoesEncerramento = p.ObservacoesEncerramento
        }).ToList();
    }
}
