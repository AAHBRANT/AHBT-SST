using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pcmso;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PcmsoItensMatriz.Queries;

public record ListarItensMatrizQuery(Guid PcmsoId) : IRequest<List<PcmsoItemMatrizDto>>;

public class ListarItensMatrizQueryHandler : IRequestHandler<ListarItensMatrizQuery, List<PcmsoItemMatrizDto>>
{
    private readonly IAppDbContext _db;

    public ListarItensMatrizQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PcmsoItemMatrizDto>> Handle(ListarItensMatrizQuery request, CancellationToken ct)
    {
        var itens = await _db.PcmsoItensMatriz
            .Where(i => i.PcmsoId == request.PcmsoId)
            .Select(i => new PcmsoItemMatrizDto
            {
                Id = i.Id,
                PcmsoId = i.PcmsoId,
                FuncaoId = i.FuncaoId,
                FuncaoNome = i.Funcao!.Nome,
                RiscoId = i.RiscoId,
                NomeExame = i.NomeExame,
                PeriodicidadeEmMeses = i.PeriodicidadeEmMeses,
                ObrigatorioNoAdmissional = i.ObrigatorioNoAdmissional,
                ObrigatorioNoDemissional = i.ObrigatorioNoDemissional,
                Observacoes = i.Observacoes,
            })
            .OrderBy(i => i.FuncaoNome).ThenBy(i => i.NomeExame)
            .ToListAsync(ct);

        return itens;
    }
}
