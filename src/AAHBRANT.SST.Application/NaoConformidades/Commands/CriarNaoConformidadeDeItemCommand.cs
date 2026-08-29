using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

// Procedimento de Inspeção Técnica de Campo (§6.2) — "gerar ocorrência a partir do item não
// conforme da inspeção". Idempotente: se o item já tiver uma NC gerada, devolve o Id existente em
// vez de duplicar (mesmo padrão de CriarDocumentoAssinaturaCommand) — permite reabrir a tela do
// item e clicar "gerar ocorrência" de novo sem criar duas linhas para o mesmo item.
public record CriarNaoConformidadeDeItemCommand(
    Guid InspecaoItemRespostaId,
    string? RequisitoRelacionado,
    string? Local,
    Guid? RiscoId,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo) : IRequest<Guid>;

public class CriarNaoConformidadeDeItemCommandValidator : AbstractValidator<CriarNaoConformidadeDeItemCommand>
{
    public CriarNaoConformidadeDeItemCommandValidator()
    {
        RuleFor(x => x.InspecaoItemRespostaId).NotEmpty();
        RuleFor(x => x.RequisitoRelacionado).MaximumLength(300);
        RuleFor(x => x.Local).MaximumLength(200);
    }
}

public class CriarNaoConformidadeDeItemCommandHandler : IRequestHandler<CriarNaoConformidadeDeItemCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarNaoConformidadeDeItemCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarNaoConformidadeDeItemCommand request, CancellationToken ct)
    {
        var existente = await _db.NaoConformidades
            .FirstOrDefaultAsync(n => n.InspecaoItemRespostaId == request.InspecaoItemRespostaId, ct);
        if (existente is not null)
            return existente.Id;

        var item = await _db.InspecaoItemRespostas
            .Include(i => i.Inspecao)
            .Include(i => i.ChecklistModeloItem)
            .FirstOrDefaultAsync(i => i.Id == request.InspecaoItemRespostaId, ct)
            ?? throw new KeyNotFoundException($"Item de inspeção {request.InspecaoItemRespostaId} não encontrado.");

        if (item.StatusItem != StatusItemChecklist.NaoConforme)
            throw new InvalidOperationException(
                "Só é possível gerar ocorrência a partir de um item marcado como não conforme.");

        if (request.RiscoId.HasValue && !await _db.Riscos.AnyAsync(r => r.Id == request.RiscoId, ct))
            throw new KeyNotFoundException($"Risco {request.RiscoId} não encontrado.");

        var responsavelId = request.ResponsavelUsuarioId ?? item.ResponsavelUsuarioId;
        if (responsavelId.HasValue && !await _db.Usuarios.AnyAsync(u => u.Id == responsavelId, ct))
            throw new KeyNotFoundException($"Usuário {responsavelId} não encontrado.");

        var descricao = string.IsNullOrWhiteSpace(item.Observacao)
            ? item.ChecklistModeloItem?.Descricao ?? "Item de checklist não conforme"
            : $"{item.ChecklistModeloItem?.Descricao} — {item.Observacao}";

        var nc = new NaoConformidade
        {
            OrigemDeteccao = OrigemNaoConformidade.Inspecao,
            RequisitoRelacionado = request.RequisitoRelacionado,
            Descricao = descricao,
            Local = request.Local,
            AtividadeId = item.Inspecao?.AtividadeId,
            RiscoId = request.RiscoId,
            InspecaoItemRespostaId = item.Id,
            ResponsavelUsuarioId = responsavelId,
            Prazo = request.Prazo ?? item.Prazo,
        };

        _db.NaoConformidades.Add(nc);
        await _db.SaveChangesAsync(ct);
        return nc.Id;
    }
}
