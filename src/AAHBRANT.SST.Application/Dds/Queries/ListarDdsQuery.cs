using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ListarDdsQuery(Guid? ObraId = null) : IRequest<List<DdsDto>>;

public class ListarDdsQueryHandler : IRequestHandler<ListarDdsQuery, List<DdsDto>>
{
    private readonly IAppDbContext _db;

    public ListarDdsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<DdsDto>> Handle(ListarDdsQuery request, CancellationToken ct)
    {
        var query = _db.Dds
            .Include(d => d.Obra)
            .Include(d => d.ResponsavelUsuario)
            .Include(d => d.Atividades).ThenInclude(a => a.Atividade)
            .Include(d => d.ItensChecklist)
            .Include(d => d.Participantes)
            .Include(d => d.FotosEvidencia)
            .AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(d => d.ObraId == request.ObraId.Value);

        var lista = await query.OrderByDescending(d => d.CreatedAtUtc).ToListAsync(ct);

        return lista.Select(MapearParaDto).ToList();
    }

    internal static DdsDto MapearParaDto(Domain.Entidades.Dds dds)
    {
        var itensAtivos = dds.ItensChecklist.Where(i => i.Ativo).ToList();
        return new DdsDto
        {
            Id = dds.Id,
            ObraId = dds.ObraId,
            ObraNome = dds.Obra?.Nome ?? string.Empty,
            DdsSemanalId = dds.DdsSemanalId,
            Data = dds.Data,
            ResponsavelUsuarioId = dds.ResponsavelUsuarioId,
            ResponsavelUsuarioNome = dds.ResponsavelUsuario?.Nome ?? string.Empty,
            CatalogoTemaDdsId = dds.CatalogoTemaDdsId,
            TemaLivreNome = dds.TemaLivreNome,
            TemaLivreDescricao = dds.TemaLivreDescricao,
            Status = dds.Status,
            TemasAtividades = dds.Atividades.Where(a => a.Ativo).OrderBy(a => a.Ordem).Select(a => new DdsTemaAtividadeDto
            {
                AtividadeId = a.AtividadeId,
                AtividadeNome = a.AtividadeNome ?? a.Atividade?.Nome ?? string.Empty,
                PerigoNome = a.PerigoNome,
                PerigoDescricao = a.PerigoDescricao,
                Consequencia = a.Consequencia,
                ControlesExistentes = a.ControlesExistentes,
                ControlesAdicionais = a.ControlesAdicionais,
            }).ToList(),
            AtividadesNomes = dds.Atividades.Where(a => a.Ativo).OrderBy(a => a.Ordem).Select(a => a.AtividadeNome ?? a.Atividade?.Nome ?? string.Empty).ToList(),
            TotalItensChecklist = itensAtivos.Count,
            ItensVerificados = itensAtivos.Count(i => i.Verificado),
            TotalParticipantes = dds.Participantes.Count(p => p.Ativo),
            TotalFotosEvidencia = dds.FotosEvidencia.Count(f => f.Ativo),
            SemExpediente = dds.SemExpediente,
            MotivoSemExpediente = dds.MotivoSemExpediente,
        };
    }
}
