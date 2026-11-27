using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Queries;

public record ListarAprsQuery(Guid? AtividadeId = null) : IRequest<List<AprDto>>;

public class ListarAprsQueryHandler : IRequestHandler<ListarAprsQuery, List<AprDto>>
{
    private readonly IAppDbContext _db;

    public ListarAprsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AprDto>> Handle(ListarAprsQuery request, CancellationToken ct)
    {
        var query = _db.Aprs
            .NoEscopoDaAtividade(_db, a => a.AtividadeId)
            .Include(a => a.Atividade!).ThenInclude(at => at.Obra)
            .Include(a => a.Equipe)
            .Include(a => a.AprovadoPorUsuario)
            .AsQueryable();

        if (request.AtividadeId.HasValue)
            query = query.Where(a => a.AtividadeId == request.AtividadeId.Value);

        var aprs = await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct);
        // Nome e foto de quem elabora e de quem supervisiona (25/11/2026), sem carregar as imagens.
        var pessoas = await FotosPessoas.DeTrabalhadoresAsync(
            _db, aprs.SelectMany(a => new[] { a.ElaboradorTrabalhadorId, a.SupervisorTrabalhadorId }), ct);
        PessoaComFoto? Pessoa(Guid? id) => id.HasValue && pessoas.TryGetValue(id.Value, out var p) ? p : null;

        return aprs.Select(a => new AprDto
        {
            Id = a.Id,
            NumeroApr = a.NumeroApr,
            AtividadeId = a.AtividadeId,
            AtividadeNome = a.Atividade?.Nome ?? string.Empty,
            ObraNome = a.Atividade?.Obra?.Nome,
            Local = a.Local,
            MaquinasEquipamentos = a.MaquinasEquipamentos,
            PgrReferencia = a.PgrReferencia,
            EquipeId = a.EquipeId,
            EquipeNome = a.Equipe?.Nome,
            Data = a.Data,
            Validade = a.Validade,
            Status = a.Status,
            AprovadoPorUsuarioId = a.AprovadoPorUsuarioId,
            AprovadoPorUsuarioNome = a.AprovadoPorUsuario?.Nome,
            DataAprovacao = a.DataAprovacao,
            MotivoReprovacao = a.MotivoReprovacao,
            ElaboradorTrabalhadorId = a.ElaboradorTrabalhadorId,
            ElaboradorNome = Pessoa(a.ElaboradorTrabalhadorId)?.Nome,
            ElaboradorTemFoto = Pessoa(a.ElaboradorTrabalhadorId)?.TemFoto ?? false,
            SupervisorTrabalhadorId = a.SupervisorTrabalhadorId,
            SupervisorNome = Pessoa(a.SupervisorTrabalhadorId)?.Nome,
        }).ToList();
    }
}
