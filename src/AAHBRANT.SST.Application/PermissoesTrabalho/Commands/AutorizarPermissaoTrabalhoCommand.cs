using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// "Autorização" (§18) — ação dedicada em vez de edição genérica de Status, mesmo padrão de
// AprovarAprCommand. Bloqueia se algum item do checklist de "requisitos" (§18) não estiver
// atendido — aplicação direta do princípio geral de bloqueio preventivo (§45: "impedir liberação
// de atividade quando requisitos obrigatórios não estiverem atendidos", texto literal do §19
// para NR-35, adotado aqui por analogia para a PT genérica) — não é uma regra literal do §18
// isoladamente; avisar o usuário se quiser autorizar mesmo com pendências.
public record AutorizarPermissaoTrabalhoCommand(Guid Id, Guid AutorizadoPorUsuarioId) : IRequest;

public class AutorizarPermissaoTrabalhoCommandValidator : AbstractValidator<AutorizarPermissaoTrabalhoCommand>
{
    public AutorizarPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AutorizadoPorUsuarioId).NotEmpty();
    }
}

public class AutorizarPermissaoTrabalhoCommandHandler : IRequestHandler<AutorizarPermissaoTrabalhoCommand>
{
    private readonly IAppDbContext _db;

    public AutorizarPermissaoTrabalhoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AutorizarPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho
            .Include(p => p.Requisitos)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.Id} não encontrada.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.AutorizadoPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.AutorizadoPorUsuarioId} não encontrado.");

        var pendentes = pt.Requisitos.Where(r => r.Ativo && !r.Atendido).ToList();
        if (pendentes.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível autorizar: {pendentes.Count} requisito(s) pendente(s) ({string.Join(", ", pendentes.Select(p => p.Descricao))}).");

        pt.Status = StatusPt.Autorizada;
        pt.AutorizadoPorUsuarioId = request.AutorizadoPorUsuarioId;
        pt.DataAutorizacao = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
