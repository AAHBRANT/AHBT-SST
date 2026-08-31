using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public record ListarTrabalhadoresQuery(Guid? ObraId = null) : IRequest<List<TrabalhadorDto>>;

public class ListarTrabalhadoresQueryHandler : IRequestHandler<ListarTrabalhadoresQuery, List<TrabalhadorDto>>
{
    private readonly IAppDbContext _db;

    public ListarTrabalhadoresQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TrabalhadorDto>> Handle(ListarTrabalhadoresQuery request, CancellationToken ct)
    {
        var query = _db.Trabalhadores.AsQueryable();

        if (request.ObraId.HasValue)
        {
            query = query.Where(t => t.ObraId == request.ObraId.Value);
        }

        return await query
            .OrderBy(t => t.Nome)
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
                TelegramCodigoVinculo = t.TelegramCodigoVinculo,
                TemFoto = t.FotoConteudo != null
            })
            .ToListAsync(ct);
    }
}
