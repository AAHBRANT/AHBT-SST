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
            .Include(a => a.Atividade!).ThenInclude(at => at.Obra)
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
            .Include(r => r.Trabalhador!).ThenInclude(t => t.Funcao)
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
                NumeroApr = apr.NumeroApr,
                AtividadeId = apr.AtividadeId,
                AtividadeNome = apr.Atividade?.Nome ?? string.Empty,
                ObraNome = apr.Atividade?.Obra?.Nome,
                Local = apr.Local,
                MaquinasEquipamentos = apr.MaquinasEquipamentos,
                PgrReferencia = apr.PgrReferencia,
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
                Riscos = e.Riscos.Select(r => new AprEtapaRiscoDto
                {
                    Id = r.Id,
                    AprEtapaId = r.AprEtapaId,
                    PerigoEventoPerigoso = r.PerigoEventoPerigoso,
                    FonteCircunstancia = r.FonteCircunstancia,
                    PossiveisLesoes = r.PossiveisLesoes,
                    TrabalhadoresExpostos = r.TrabalhadoresExpostos,
                    ProbabilidadeInicial = r.ProbabilidadeInicial,
                    SeveridadeInicial = r.SeveridadeInicial,
                    NivelRiscoInicial = r.NivelRiscoInicial,
                    MedidasPrevencao = r.MedidasPrevencao,
                    Responsavel = r.Responsavel,
                    ProbabilidadeResidual = r.ProbabilidadeResidual,
                    SeveridadeResidual = r.SeveridadeResidual,
                    NivelRiscoResidual = r.NivelRiscoResidual,
                }).ToList()
            }).ToList(),
            Responsaveis = responsaveis.Select(r => new AprResponsavelDto
            {
                Id = r.Id,
                AprId = r.AprId,
                TrabalhadorId = r.TrabalhadorId,
                TrabalhadorNome = r.Trabalhador?.Nome ?? string.Empty,
                TrabalhadorFuncaoNome = r.Trabalhador?.Funcao?.Nome
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
