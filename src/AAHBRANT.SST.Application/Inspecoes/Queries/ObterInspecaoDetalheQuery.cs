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
        // Obra continua no Include de propósito: o filtro global dela é o escopo RBAC por obra
        // (SstDbContext), e quem não tem acesso à obra deve receber 404.
        var inspecao = await _db.Inspecoes
            .Include(i => i.Obra)
            .Include(i => i.Atividade)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (inspecao is null) return null;

        // Checklist e responsável vêm à parte, ignorando o filtro de soft delete: nova versão do
        // checklist (CriarNovaVersaoChecklistModelo) desativa a anterior, e um Include aqui virava
        // inner join que sumia com a inspeção inteira ("Not Found" ao continuar — bug 24/09/2026).
        var checklist = await _db.ChecklistModelos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == inspecao.ChecklistModeloId, ct);
        var responsavelNome = await _db.Usuarios.IgnoreQueryFilters()
            .Where(u => u.Id == inspecao.ResponsavelUsuarioId)
            .Select(u => u.Nome)
            .FirstOrDefaultAsync(ct);

        var respostas = await _db.InspecaoItemRespostas.IgnoreQueryFilters()
            .Where(r => r.InspecaoId == inspecao.Id && r.Ativo)
            .Include(r => r.ChecklistModeloItem)
            .Include(r => r.ResponsavelUsuario)
            .ToListAsync(ct);

        var respostasOrdenadas = respostas.OrderBy(r => r.ChecklistModeloItem?.Ordem ?? 0).ToList();

        var idsRespostas = respostasOrdenadas.Select(r => r.Id).ToList();
        // Dictionary<Guid, Guid?> (não Dictionary<Guid, Guid>) é proposital: GetValueOrDefault abaixo
        // precisa devolver null pra quem não tem Não Conformidade gerada — com Guid não-nullable ele
        // devolveria Guid.Empty, uma string de GUID zerada só que "verdadeira" no front (o botão
        // "Ver ocorrência" aparecia em vez de "Gerar ocorrência", apontando pra um Id inexistente).
        var ncPorResposta = await _db.NaoConformidades
            .Where(n => n.InspecaoItemRespostaId != null && idsRespostas.Contains(n.InspecaoItemRespostaId!.Value))
            .ToDictionaryAsync(n => n.InspecaoItemRespostaId!.Value, n => (Guid?)n.Id, ct);

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
                ChecklistModeloNome = checklist?.Nome ?? string.Empty,
                ChecklistModeloVersao = checklist?.Versao ?? 0,
                Data = inspecao.Data,
                ResponsavelUsuarioId = inspecao.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = responsavelNome ?? string.Empty,
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
                Secao = r.ChecklistModeloItem?.Secao,
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
