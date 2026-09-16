using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

// Endpoint atômico "obter ou criar" (Task 5, feature Alojamento em Inspeções): abrir a aba
// Inspeções de um alojamento sempre resolve para UMA inspeção em andamento — se já existe, retoma
// (não duplica); se não existe, cria na mesma transação. A garantia de unicidade real vive no
// banco (índice único parcial em InspecaoConfiguracao, ver InspecoesConfiguracoes.cs) — esta
// checagem aqui é só o caminho feliz, não a garantia contra concorrência (dois cliques
// simultâneos): sem o índice, dois técnicos clicando ao mesmo tempo no mesmo alojamento
// poderiam ambos passar pelo "não existe" antes de qualquer um commitar.
public record ObterOuCriarInspecaoAlojamentoCommand(Guid AlojamentoId, string? AzureAdObjectId) : IRequest<InspecaoAtualDto>;

public record InspecaoAtualDto(Guid InspecaoId, bool FoiCriadaAgora, Guid ResponsavelUsuarioId, DateTime CriadaEm);

public class ObterOuCriarInspecaoAlojamentoCommandHandler : IRequestHandler<ObterOuCriarInspecaoAlojamentoCommand, InspecaoAtualDto>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public ObterOuCriarInspecaoAlojamentoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<InspecaoAtualDto> Handle(ObterOuCriarInspecaoAlojamentoCommand request, CancellationToken ct)
    {
        var alojamento = await _db.Alojamentos.FirstOrDefaultAsync(a => a.Id == request.AlojamentoId, ct)
            ?? throw new KeyNotFoundException($"Alojamento {request.AlojamentoId} não encontrado.");

        var existente = await _db.Inspecoes
            .Where(i => i.AlojamentoId == alojamento.Id && i.Status == StatusInspecao.EmAndamento)
            .OrderByDescending(i => i.Data)
            .FirstOrDefaultAsync(ct);

        if (existente is not null)
            return new InspecaoAtualDto(existente.Id, false, existente.ResponsavelUsuarioId, existente.CreatedAtUtc);

        var checklist = await _db.ChecklistModelos
            .Include(c => c.Itens)
            .Where(c => c.TipoInspecao == TipoInspecao.Alojamento)
            .OrderByDescending(c => c.Versao)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhum checklist de Alojamento cadastrado.");

        // Usuário logado é sempre o responsável (não escolhido em tela) — mesmo padrão de
        // CriarDdsSemanalCommandHandler: o Controller extrai o claim "oid" do ClaimsPrincipal, e é
        // este handler que resolve o Usuario correspondente na mesma transação.
        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario is null)
            throw new InvalidOperationException(
                "Seu usuário não está vinculado a um cadastro reconhecido. Peça a um administrador para revisar seu acesso antes de abrir uma inspeção de alojamento.");

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = alojamento.ObraId,
            AlojamentoId = alojamento.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
            NumeroDocumento = await _geradorNumero.GerarAsync("INSP", ct),
        };
        foreach (var item in checklist.Itens.Where(i => i.Ativo))
            inspecao.Respostas.Add(new InspecaoItemResposta { ChecklistModeloItemId = item.Id });

        _db.Inspecoes.Add(inspecao);
        await _db.SaveChangesAsync(ct);
        return new InspecaoAtualDto(inspecao.Id, true, inspecao.ResponsavelUsuarioId, inspecao.CreatedAtUtc);
    }
}
