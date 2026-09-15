using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Queries;

public record ObterConfiguracaoAlojamentoQuery : IRequest<int>;

public class ObterConfiguracaoAlojamentoQueryHandler : IRequestHandler<ObterConfiguracaoAlojamentoQuery, int>
{
    private readonly IAppDbContext _db;
    public ObterConfiguracaoAlojamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(ObterConfiguracaoAlojamentoQuery request, CancellationToken ct) =>
        (await _db.ConfiguracoesAlojamento.FirstOrDefaultAsync(ct))?.DiasParaInspecaoAtrasada ?? 30;
}
