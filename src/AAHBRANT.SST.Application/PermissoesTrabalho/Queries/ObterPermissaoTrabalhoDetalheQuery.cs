using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Queries;

public record ObterPermissaoTrabalhoDetalheQuery(Guid Id) : IRequest<PermissaoTrabalhoDetalheDto?>;

public class ObterPermissaoTrabalhoDetalheQueryHandler : IRequestHandler<ObterPermissaoTrabalhoDetalheQuery, PermissaoTrabalhoDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterPermissaoTrabalhoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PermissaoTrabalhoDetalheDto?> Handle(ObterPermissaoTrabalhoDetalheQuery request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho
            .Include(p => p.Atividade)
            .Include(p => p.Equipe)
            .Include(p => p.AutorizadoPorUsuario)
            .Include(p => p.EncerradaPorUsuario)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (pt is null) return null;

        var perigos = await _db.PermissaoTrabalhoPerigos
            .Where(pp => pp.PermissaoTrabalhoId == pt.Id)
            .Include(pp => pp.Perigo)
            .ToListAsync(ct);

        var controles = await _db.PermissaoTrabalhoControles
            .Where(c => c.PermissaoTrabalhoId == pt.Id)
            .ToListAsync(ct);

        var requisitos = await _db.PermissaoTrabalhoRequisitos
            .Where(r => r.PermissaoTrabalhoId == pt.Id)
            .ToListAsync(ct);

        var responsaveis = await _db.PermissaoTrabalhoResponsaveis
            .Where(r => r.PermissaoTrabalhoId == pt.Id)
            .Include(r => r.Trabalhador)
            .ToListAsync(ct);

        return new PermissaoTrabalhoDetalheDto
        {
            PermissaoTrabalho = new PermissaoTrabalhoDto
            {
                Id = pt.Id,
                AtividadeId = pt.AtividadeId,
                AtividadeNome = pt.Atividade?.Nome ?? string.Empty,
                Local = pt.Local,
                EquipeId = pt.EquipeId,
                EquipeNome = pt.Equipe?.Nome,
                Data = pt.Data,
                HorarioInicio = pt.HorarioInicio,
                HorarioFim = pt.HorarioFim,
                Validade = pt.Validade,
                Status = pt.Status,
                AutorizadoPorUsuarioId = pt.AutorizadoPorUsuarioId,
                AutorizadoPorUsuarioNome = pt.AutorizadoPorUsuario?.Nome,
                DataAutorizacao = pt.DataAutorizacao,
                EncerradaPorUsuarioId = pt.EncerradaPorUsuarioId,
                EncerradaPorUsuarioNome = pt.EncerradaPorUsuario?.Nome,
                DataEncerramento = pt.DataEncerramento,
                ObservacoesEncerramento = pt.ObservacoesEncerramento
            },
            Perigos = perigos.Select(pp => new PermissaoTrabalhoPerigoDto
            {
                Id = pp.Id,
                PermissaoTrabalhoId = pp.PermissaoTrabalhoId,
                PerigoId = pp.PerigoId,
                PerigoNome = pp.Perigo?.Nome ?? string.Empty
            }).ToList(),
            Controles = controles.Select(c => new PermissaoTrabalhoControleDto
            {
                Id = c.Id,
                PermissaoTrabalhoId = c.PermissaoTrabalhoId,
                Descricao = c.Descricao
            }).ToList(),
            Requisitos = requisitos.Select(r => new PermissaoTrabalhoRequisitoDto
            {
                Id = r.Id,
                PermissaoTrabalhoId = r.PermissaoTrabalhoId,
                Descricao = r.Descricao,
                Atendido = r.Atendido
            }).ToList(),
            Responsaveis = responsaveis.Select(r => new PermissaoTrabalhoResponsavelDto
            {
                Id = r.Id,
                PermissaoTrabalhoId = r.PermissaoTrabalhoId,
                TrabalhadorId = r.TrabalhadorId,
                TrabalhadorNome = r.Trabalhador?.Nome ?? string.Empty
            }).ToList()
        };
    }
}
