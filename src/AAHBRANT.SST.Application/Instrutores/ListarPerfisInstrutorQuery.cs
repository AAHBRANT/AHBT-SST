using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Instrutores;

public record ListarPerfisInstrutorQuery : IRequest<List<PerfilInstrutorDto>>;

public class ListarPerfisInstrutorQueryHandler : IRequestHandler<ListarPerfisInstrutorQuery, List<PerfilInstrutorDto>>
{
    private readonly IAppDbContext _db;

    public ListarPerfisInstrutorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PerfilInstrutorDto>> Handle(ListarPerfisInstrutorQuery request, CancellationToken ct)
    {
        // O instrutor pode ser de outra obra: o filtro de escopo do Trabalhador esconderia a linha dele
        // para quem só vê a obra da turma, e o join interno apagaria o perfil da lista.
        var perfis = await _db.PerfisInstrutor
            .Join(_db.Trabalhadores.IgnoreQueryFilters().Include(t => t.Funcao).Include(t => t.Obra),
                p => p.TrabalhadorId, t => t.Id, (p, t) => new { p, t })
            .OrderBy(x => x.t.Nome)
            .ToListAsync(ct);

        return perfis.Select(x => new PerfilInstrutorDto(
            x.p.Id, x.p.TrabalhadorId, x.t.Nome, x.t.Funcao?.Nome, x.t.Obra?.Nome,
            x.p.Formacao, DescricaoInstrutor.Formacao(x.p.Formacao),
            x.p.Conselho, x.p.NumeroRegistro, x.p.UfRegistro,
            DescricaoInstrutor.Registro(x.p.Conselho, x.p.NumeroRegistro, x.p.UfRegistro),
            x.p.PodeAssinarEmOutrasObras, x.p.AutorizadoOutrasObrasEm)).ToList();
    }
}
