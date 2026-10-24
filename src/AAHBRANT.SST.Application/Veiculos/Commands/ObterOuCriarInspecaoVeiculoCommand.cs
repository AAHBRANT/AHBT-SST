using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Veiculos.Commands;

// Mesmo desenho de ObterOuCriarInspecaoAlojamentoCommand: abrir a inspeção de um veículo sempre
// resolve para UMA inspeção em andamento (retoma ou cria). A unicidade real vive no índice único
// filtrado de InspecaoConfiguracao (VeiculoId + Status=EmAndamento); o catch de DbUpdateException
// abaixo cobre a corrida de dois cliques simultâneos. O checklist é o vigente do TIPO do veículo.
public record ObterOuCriarInspecaoVeiculoCommand(Guid VeiculoId, string? AzureAdObjectId) : IRequest<InspecaoAtualDto>;

public class ObterOuCriarInspecaoVeiculoCommandHandler : IRequestHandler<ObterOuCriarInspecaoVeiculoCommand, InspecaoAtualDto>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public ObterOuCriarInspecaoVeiculoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<InspecaoAtualDto> Handle(ObterOuCriarInspecaoVeiculoCommand request, CancellationToken ct)
    {
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == request.VeiculoId, ct)
            ?? throw new KeyNotFoundException($"Veículo {request.VeiculoId} não encontrado.");

        var existente = await _db.Inspecoes
            .Where(i => i.VeiculoId == veiculo.Id && i.Status == StatusInspecao.EmAndamento)
            .OrderByDescending(i => i.Data)
            .FirstOrDefaultAsync(ct);

        if (existente is not null)
            return new InspecaoAtualDto(existente.Id, false, existente.ResponsavelUsuarioId, existente.CreatedAtUtc);

        var checklist = await _db.ChecklistModelos
            .Include(c => c.Itens)
            .Where(c => c.TipoInspecao == TipoInspecao.Veiculo && c.TipoVeiculo == veiculo.Tipo)
            .OrderByDescending(c => c.Versao)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhum checklist cadastrado para este tipo de veículo.");

        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario is null)
            throw new InvalidOperationException(
                "Seu usuário não está vinculado a um cadastro reconhecido. Peça a um administrador para revisar seu acesso antes de abrir uma inspeção de veículo.");

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Veiculo,
            ObraId = veiculo.ObraId,
            VeiculoId = veiculo.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
            NumeroDocumento = await _geradorNumero.GerarAsync("INSP", ct),
        };
        foreach (var item in checklist.Itens.Where(i => i.Ativo))
            inspecao.Respostas.Add(new InspecaoItemResposta { ChecklistModeloItemId = item.Id });

        _db.Inspecoes.Add(inspecao);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Perdeu a corrida para outro clique: devolve a inspeção que venceu (resultado
            // idempotente em vez de 500). Se não houver vencedora, o erro é real — propaga.
            var vencedora = await _db.Inspecoes
                .Where(i => i.VeiculoId == veiculo.Id && i.Status == StatusInspecao.EmAndamento)
                .OrderByDescending(i => i.Data)
                .FirstOrDefaultAsync(ct);
            if (vencedora is null) throw;

            return new InspecaoAtualDto(vencedora.Id, false, vencedora.ResponsavelUsuarioId, vencedora.CreatedAtUtc);
        }

        return new InspecaoAtualDto(inspecao.Id, true, inspecao.ResponsavelUsuarioId, inspecao.CreatedAtUtc);
    }
}
