using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ListarDdsQuery(Guid? ObraId = null) : IRequest<List<DdsDto>>;

public class ListarDdsQueryHandler : IRequestHandler<ListarDdsQuery, List<DdsDto>>
{
    private readonly IAppDbContext _db;

    public ListarDdsQueryHandler(IAppDbContext db) => _db = db;

    // Projeção direta via Select em vez de Include de 5 coleções (Obra, ResponsavelUsuario,
    // Atividades, ItensChecklist, Participantes, FotosEvidencia): múltiplos Include de coleções
    // irmãs no mesmo nível causam explosão cartesiana (JOINs multiplicando linhas) — o dashboard,
    // que chama esta query sem filtro de obra, sentia isso na pele. Select gera subconsultas
    // correlacionadas por coleção, sem duplicar linhas. Filtro Ativo das coleções filhas continua
    // aplicado automaticamente pelo HasQueryFilter de cada entidade (ver DdsConfiguracoes.cs).
    public async Task<List<DdsDto>> Handle(ListarDdsQuery request, CancellationToken ct)
    {
        var query = _db.Dds.AsNoTracking().AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(d => d.ObraId == request.ObraId.Value);

        var lista = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new DdsDto
            {
                Id = d.Id,
                ObraId = d.ObraId,
                ObraNome = d.Obra != null ? d.Obra.Nome : string.Empty,
                DdsSemanalId = d.DdsSemanalId,
                Data = d.Data,
                ResponsavelUsuarioId = d.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = d.ResponsavelUsuario != null ? d.ResponsavelUsuario.Nome : string.Empty,
                CatalogoTemaDdsId = d.CatalogoTemaDdsId,
                TemaLivreNome = d.TemaLivreNome,
                TemaLivreDescricao = d.TemaLivreDescricao,
                Status = d.Status,
                TemasAtividades = d.Atividades
                    .OrderBy(a => a.Ordem)
                    .Select(a => new DdsTemaAtividadeDto
                    {
                        AtividadeId = a.AtividadeId,
                        AtividadeNome = a.AtividadeNome ?? (a.Atividade != null ? a.Atividade.Nome : string.Empty),
                        PerigoNome = a.PerigoNome,
                        PerigoDescricao = a.PerigoDescricao,
                        Consequencia = a.Consequencia,
                        ControlesExistentes = a.ControlesExistentes,
                        ControlesAdicionais = a.ControlesAdicionais,
                    }).ToList(),
                TotalItensChecklist = d.ItensChecklist.Count,
                ItensVerificados = d.ItensChecklist.Count(i => i.Verificado),
                TotalParticipantes = d.Participantes.Count,
                TotalFotosEvidencia = d.FotosEvidencia.Count,
                SemExpediente = d.SemExpediente,
                MotivoSemExpediente = d.MotivoSemExpediente,
            })
            .ToListAsync(ct);

        // AtividadesNomes é só a lista de nomes de TemasAtividades (mesma coleção) — derivado em
        // memória em vez de projetado de novo no SQL, pra não duplicar o LEFT JOIN de DdsAtividades.
        foreach (var dto in lista)
            dto.AtividadesNomes = dto.TemasAtividades.Select(t => t.AtividadeNome).ToList();

        return lista;
    }

    // Usado por ObterDdsDetalheQuery, que já carrega a entidade Dds (com Includes pontuais) e
    // precisa do mesmo mapeamento — mantido aqui para não duplicar a lógica de fallback
    // AtividadeNome/Atividade?.Nome.
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
