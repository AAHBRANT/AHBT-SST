using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Temas;

// Mesmo desenho de ObterOuCriarInspecaoVeiculoCommand: abrir a inspeção de um item sempre resolve para
// UMA inspeção em andamento (retoma ou cria). A unicidade real vive no índice único filtrado de
// InspecaoConfiguracao (ItemInspecionavelId + Status=EmAndamento); o catch de DbUpdateException cobre a
// corrida de dois cliques simultâneos. O checklist é o vigente do TEMA do item.
public record ObterOuCriarInspecaoItemCommand(Guid ItemInspecionavelId, string? AzureAdObjectId) : IRequest<InspecaoAtualDto>;

public class ObterOuCriarInspecaoItemCommandHandler : IRequestHandler<ObterOuCriarInspecaoItemCommand, InspecaoAtualDto>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public ObterOuCriarInspecaoItemCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<InspecaoAtualDto> Handle(ObterOuCriarInspecaoItemCommand request, CancellationToken ct)
    {
        var item = await _db.ItensInspecionaveis.FirstOrDefaultAsync(i => i.Id == request.ItemInspecionavelId, ct)
            ?? throw new KeyNotFoundException($"Item {request.ItemInspecionavelId} não encontrado.");

        var existente = await _db.Inspecoes
            .Where(i => i.ItemInspecionavelId == item.Id && i.Status == StatusInspecao.EmAndamento)
            .OrderByDescending(i => i.Data)
            .FirstOrDefaultAsync(ct);

        if (existente is not null)
            return new InspecaoAtualDto(existente.Id, false, existente.ResponsavelUsuarioId, existente.CreatedAtUtc);

        var checklist = await _db.ChecklistModelos
            .Include(c => c.Itens)
            .Where(c => c.TipoInspecao == TipoInspecao.Tema && c.TemaInspecaoId == item.TemaInspecaoId)
            .OrderByDescending(c => c.Versao)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhum checklist cadastrado para este tema.");

        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario is null)
            throw new InvalidOperationException(
                "Seu usuário não está vinculado a um cadastro reconhecido. Peça a um administrador para revisar seu acesso antes de abrir uma inspeção.");

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Tema,
            ObraId = item.ObraId,
            ItemInspecionavelId = item.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
            NumeroDocumento = await _geradorNumero.GerarAsync("INSP", ct),
        };
        foreach (var itemChecklist in checklist.Itens.Where(i => i.Ativo))
            inspecao.Respostas.Add(new InspecaoItemResposta { ChecklistModeloItemId = itemChecklist.Id });

        _db.Inspecoes.Add(inspecao);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Perdeu a corrida para outro clique: devolve a inspeção que venceu (resultado idempotente em
            // vez de 500). Se não houver vencedora, o erro é real — propaga.
            var vencedora = await _db.Inspecoes
                .Where(i => i.ItemInspecionavelId == item.Id && i.Status == StatusInspecao.EmAndamento)
                .OrderByDescending(i => i.Data)
                .FirstOrDefaultAsync(ct);
            if (vencedora is null) throw;

            return new InspecaoAtualDto(vencedora.Id, false, vencedora.ResponsavelUsuarioId, vencedora.CreatedAtUtc);
        }

        return new InspecaoAtualDto(inspecao.Id, true, inspecao.ResponsavelUsuarioId, inspecao.CreatedAtUtc);
    }
}
