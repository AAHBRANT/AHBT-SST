using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public record ObterTrabalhadorPorIdQuery(Guid Id) : IRequest<TrabalhadorDto?>;

public class ObterTrabalhadorPorIdQueryHandler : IRequestHandler<ObterTrabalhadorPorIdQuery, TrabalhadorDto?>
{
    private readonly IAppDbContext _db;

    public ObterTrabalhadorPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TrabalhadorDto?> Handle(ObterTrabalhadorPorIdQuery request, CancellationToken ct)
    {
        return await _db.Trabalhadores
            .Where(t => t.Id == request.Id)
            .Select(t => new TrabalhadorDto
            {
                Id = t.Id,
                ObraId = t.ObraId,
                SetorId = t.SetorId,
                EquipeId = t.EquipeId,
                FuncaoId = t.FuncaoId,
                Nome = t.Nome,
                Matricula = t.Matricula,
                Cpf = t.Cpf,
                Vinculo = t.Vinculo,
                DataAdmissao = t.DataAdmissao,
                DataDemissao = t.DataDemissao,
                Turno = t.Turno,
                TelegramVinculado = t.TelegramChatId != null,
                TelegramCodigoVinculo = t.TelegramCodigoVinculo
            })
            .FirstOrDefaultAsync(ct);
    }
}
