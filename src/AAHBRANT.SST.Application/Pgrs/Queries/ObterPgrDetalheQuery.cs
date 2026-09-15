using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pgrs.Queries;

// Monta os componentes do §16 que não viram tabela própria: "caracterização das atividades",
// "inventário de riscos" e "classificação dos riscos" são resolvidos aqui como uma consulta sobre
// Atividade/Risco/Perigo já cadastrados na Obra do PGR — sem duplicar dado em uma tabela nova.
public record ObterPgrDetalheQuery(Guid Id) : IRequest<PgrDetalheDto?>;

public class ObterPgrDetalheQueryHandler : IRequestHandler<ObterPgrDetalheQuery, PgrDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterPgrDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PgrDetalheDto?> Handle(ObterPgrDetalheQuery request, CancellationToken ct)
    {
        var pgr = await _db.Pgrs
            .Where(p => p.Id == request.Id)
            .Select(p => new PgrDto
            {
                Id = p.Id,
                ObraId = p.ObraId,
                Nome = p.Nome,
                Descricao = p.Descricao,
                DataElaboracao = p.DataElaboracao,
                DataProximaRevisao = p.DataProximaRevisao,
                DataTermino = p.DataTermino,
                ResponsavelUsuarioId = p.ResponsavelUsuarioId,
                Status = p.Status
            })
            .FirstOrDefaultAsync(ct);
        if (pgr is null) return null;

        var atividades = await _db.Atividades
            .Where(a => a.ObraId == pgr.ObraId)
            .Include(a => a.Riscos).ThenInclude(r => r.Perigo)
            .OrderBy(a => a.Nome)
            .ToListAsync(ct);

        var planoDeAcao = await _db.PlanoAcaoItens
            .Where(i => i.PgrId == pgr.Id)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);

        var revisoes = await _db.PgrRevisoes
            .Where(r => r.PgrId == pgr.Id)
            .OrderByDescending(r => r.NumeroRevisao)
            .ToListAsync(ct);

        return new PgrDetalheDto
        {
            Pgr = pgr,
            Atividades = atividades.Select(a => new AtividadeCaracterizadaDto
            {
                AtividadeId = a.Id,
                AtividadeNome = a.Nome,
                Riscos = a.Riscos.Select(r => new RiscoClassificadoDto
                {
                    RiscoId = r.Id,
                    PerigoNome = r.Perigo?.Nome ?? string.Empty,
                    NivelRisco = r.NivelRisco,
                    ControlesExistentes = r.ControlesExistentes,
                    ControlesAdicionais = r.ControlesAdicionais,
                    Status = r.Status
                }).ToList()
            }).ToList(),
            PlanoDeAcao = planoDeAcao.Select(i => new PlanoAcaoItemDto
            {
                Id = i.Id,
                PgrId = i.PgrId,
                RiscoId = i.RiscoId,
                Descricao = i.Descricao,
                ResponsavelUsuarioId = i.ResponsavelUsuarioId,
                Prazo = i.Prazo,
                DataConclusao = i.DataConclusao,
                Status = i.Status
            }).ToList(),
            Revisoes = revisoes.Select(r => new PgrRevisaoDto
            {
                Id = r.Id,
                PgrId = r.PgrId,
                NumeroRevisao = r.NumeroRevisao,
                DataRevisao = r.DataRevisao,
                Motivo = r.Motivo,
                ResponsavelUsuarioId = r.ResponsavelUsuarioId
            }).ToList()
        };
    }
}
