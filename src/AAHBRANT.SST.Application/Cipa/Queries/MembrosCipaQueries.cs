using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public record ListarMembrosCipaQuery(Guid? ObraId = null, bool SomenteMandatoAtivo = false) : IRequest<List<MembroCipaDto>>;

public class ListarMembrosCipaQueryHandler : IRequestHandler<ListarMembrosCipaQuery, List<MembroCipaDto>>
{
    private readonly IAppDbContext _db;
    public ListarMembrosCipaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<MembroCipaDto>> Handle(ListarMembrosCipaQuery request, CancellationToken ct)
    {
        var query = _db.MembrosCipa
            .Include(m => m.Obra)
            .Include(m => m.Trabalhador)
            .Include(m => m.Treinamentos)
            .AsQueryable();

        if (request.ObraId.HasValue) query = query.Where(m => m.ObraId == request.ObraId.Value);

        var agora = DateTime.UtcNow;
        if (request.SomenteMandatoAtivo) query = query.Where(m => m.DataFimMandato >= agora);

        var lista = await query.OrderBy(m => m.Cargo).ThenBy(m => m.Trabalhador!.Nome).ToListAsync(ct);
        return lista.Select(m => MapearParaDto(m, agora)).ToList();
    }

    internal static MembroCipaDto MapearParaDto(Domain.Entidades.MembroCipa m, DateTime agora) => new()
    {
        Id = m.Id,
        ObraId = m.ObraId,
        ObraNome = m.Obra?.Nome ?? string.Empty,
        TrabalhadorId = m.TrabalhadorId,
        TrabalhadorNome = m.Trabalhador?.Nome ?? string.Empty,
        TrabalhadorMatricula = m.Trabalhador?.Matricula ?? string.Empty,
        OrigemMembro = m.OrigemMembro,
        Cargo = m.Cargo,
        DataInicioMandato = m.DataInicioMandato,
        DataFimMandato = m.DataFimMandato,
        MandatoAtivo = m.DataFimMandato >= agora,
        TotalTreinamentos = m.Treinamentos.Count(t => t.Ativo),
    };
}

public record ObterMembroCipaDetalheQuery(Guid Id) : IRequest<MembroCipaDetalheDto?>;

public class ObterMembroCipaDetalheQueryHandler : IRequestHandler<ObterMembroCipaDetalheQuery, MembroCipaDetalheDto?>
{
    private readonly IAppDbContext _db;
    public ObterMembroCipaDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<MembroCipaDetalheDto?> Handle(ObterMembroCipaDetalheQuery request, CancellationToken ct)
    {
        var membro = await _db.MembrosCipa
            .Include(m => m.Obra)
            .Include(m => m.Trabalhador)
            .Include(m => m.Treinamentos.Where(t => t.Ativo))
            .FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (membro is null) return null;

        return new MembroCipaDetalheDto
        {
            Membro = ListarMembrosCipaQueryHandler.MapearParaDto(membro, DateTime.UtcNow),
            Treinamentos = membro.Treinamentos.Where(t => t.Ativo).OrderByDescending(t => t.DataRealizacao).Select(t => new TreinamentoCipaDto
            {
                Id = t.Id,
                MembroCipaId = t.MembroCipaId,
                CargaHoraria = t.CargaHoraria,
                ConteudoProgramatico = t.ConteudoProgramatico,
                DataRealizacao = t.DataRealizacao,
                DataValidade = t.DataValidade,
                InstituicaoInstrutor = t.InstituicaoInstrutor,
                TemCertificado = t.CertificadoConteudo != null,
                TemListaPresenca = t.ListaPresencaConteudo != null,
            }).ToList(),
        };
    }
}
