using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Queries;

public record ObterInspecaoDetalheQuery(Guid Id) : IRequest<InspecaoDetalheDto?>;

public class ObterInspecaoDetalheQueryHandler : IRequestHandler<ObterInspecaoDetalheQuery, InspecaoDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterInspecaoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<InspecaoDetalheDto?> Handle(ObterInspecaoDetalheQuery request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes
            .Include(i => i.Obra)
            .Include(i => i.Atividade)
            .Include(i => i.ChecklistModelo)
            .Include(i => i.ResponsavelUsuario)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (inspecao is null) return null;

        var respostas = await _db.InspecaoItemRespostas
            .Where(r => r.InspecaoId == inspecao.Id)
            .Include(r => r.ChecklistModeloItem)
            .Include(r => r.ResponsavelUsuario)
            .ToListAsync(ct);

        var respostasOrdenadas = respostas.OrderBy(r => r.ChecklistModeloItem?.Ordem ?? 0).ToList();

        var idsRespostas = respostasOrdenadas.Select(r => r.Id).ToList();
        var ncPorResposta = await _db.NaoConformidades
            .Where(n => n.InspecaoItemRespostaId != null && idsRespostas.Contains(n.InspecaoItemRespostaId!.Value))
            .ToDictionaryAsync(n => n.InspecaoItemRespostaId!.Value, n => n.Id, ct);

        return new InspecaoDetalheDto
        {
            Inspecao = new InspecaoDto
            {
                Id = inspecao.Id,
                TipoInspecao = inspecao.TipoInspecao,
                ObraId = inspecao.ObraId,
                ObraNome = inspecao.Obra?.Nome ?? string.Empty,
                AtividadeId = inspecao.AtividadeId,
                AtividadeNome = inspecao.Atividade?.Nome,
                ChecklistModeloId = inspecao.ChecklistModeloId,
                ChecklistModeloNome = inspecao.ChecklistModelo?.Nome ?? string.Empty,
                ChecklistModeloVersao = inspecao.ChecklistModelo?.Versao ?? 0,
                Data = inspecao.Data,
                ResponsavelUsuarioId = inspecao.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = inspecao.ResponsavelUsuario?.Nome ?? string.Empty,
                Status = inspecao.Status,
                TotalItens = respostasOrdenadas.Count(r => r.Ativo),
                ItensRespondidos = respostasOrdenadas.Count(r => r.Ativo && r.StatusItem != null),
                ItensNaoConformes = respostasOrdenadas.Count(r => r.Ativo && r.StatusItem == StatusItemChecklist.NaoConforme)
            },
            Respostas = respostasOrdenadas.Select(r => new InspecaoItemRespostaDto
            {
                Id = r.Id,
                InspecaoId = r.InspecaoId,
                ChecklistModeloItemId = r.ChecklistModeloItemId,
                Ordem = r.ChecklistModeloItem?.Ordem ?? 0,
                Descricao = r.DescricaoPersonalizada ?? r.ChecklistModeloItem?.Descricao ?? string.Empty,
                ExigeFotografia = r.ChecklistModeloItem?.ExigeFotografia ?? false,
                ExigeResponsavel = r.ChecklistModeloItem?.ExigeResponsavel ?? false,
                ExigePrazo = r.ChecklistModeloItem?.ExigePrazo ?? false,
                StatusItem = r.StatusItem,
                Observacao = r.Observacao,
                Local = r.Local,
                PlanoDeAcao = r.PlanoDeAcao,
                ResponsavelUsuarioId = r.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = r.ResponsavelUsuario?.Nome,
                Prazo = r.Prazo,
                TemFoto = r.FotoConteudo.Length > 0,
                TemFotoDepois = r.FotoDepoisConteudo != null && r.FotoDepoisConteudo.Length > 0,
                NaoConformidadeId = ncPorResposta.GetValueOrDefault(r.Id)
            }).ToList()
        };
    }
}
