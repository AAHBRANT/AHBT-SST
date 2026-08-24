using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Queries;

public record ObterAprDetalheQuery(Guid Id) : IRequest<AprDetalheDto?>;

public class ObterAprDetalheQueryHandler : IRequestHandler<ObterAprDetalheQuery, AprDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterAprDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AprDetalheDto?> Handle(ObterAprDetalheQuery request, CancellationToken ct)
    {
        var apr = await _db.Aprs
            .Include(a => a.Atividade)
            .Include(a => a.Equipe)
            .Include(a => a.AprovadoPorUsuario)
            .FirstOrDefaultAsync(a => a.Id == request.Id, ct);
        if (apr is null) return null;

        var etapas = await _db.AprEtapas
            .Where(e => e.AprId == apr.Id)
            .Include(e => e.Riscos)
            .OrderBy(e => e.Ordem)
            .ToListAsync(ct);

        var responsaveis = await _db.AprResponsaveis
            .Where(r => r.AprId == apr.Id)
            .Include(r => r.Trabalhador)
            .ToListAsync(ct);

        var assinaturas = await _db.AprAssinaturas
            .Where(s => s.AprId == apr.Id)
            .Include(s => s.Trabalhador)
            .OrderByDescending(s => s.DataAssinatura)
            .ToListAsync(ct);

        return new AprDetalheDto
        {
            Apr = new AprDto
            {
                Id = apr.Id,
                AtividadeId = apr.AtividadeId,
                AtividadeNome = apr.Atividade?.Nome ?? string.Empty,
                Local = apr.Local,
                EquipeId = apr.EquipeId,
                EquipeNome = apr.Equipe?.Nome,
                Data = apr.Data,
                Validade = apr.Validade,
                Status = apr.Status,
                AprovadoPorUsuarioId = apr.AprovadoPorUsuarioId,
                AprovadoPorUsuarioNome = apr.AprovadoPorUsuario?.Nome,
                DataAprovacao = apr.DataAprovacao,
                MotivoReprovacao = apr.MotivoReprovacao
            },
            Etapas = etapas.Select(e => new AprEtapaDto
            {
                Id = e.Id,
                AprId = e.AprId,
                Ordem = e.Ordem,
                Descricao = e.Descricao,
                MedidasPreventivas = e.MedidasPreventivas,
                RiscosIds = e.Riscos.Select(r => r.RiscoId).ToList()
            }).ToList(),
            Responsaveis = responsaveis.Select(r => new AprResponsavelDto
            {
                Id = r.Id,
                AprId = r.AprId,
                TrabalhadorId = r.TrabalhadorId,
                TrabalhadorNome = r.Trabalhador?.Nome ?? string.Empty
            }).ToList(),
            Assinaturas = assinaturas.Select(s => new AprAssinaturaDto
            {
                Id = s.Id,
                AprId = s.AprId,
                TrabalhadorId = s.TrabalhadorId,
                TrabalhadorNome = s.Trabalhador?.Nome ?? string.Empty,
                Papel = s.Papel,
                DataAssinatura = s.DataAssinatura
            }).ToList()
        };
    }
}
