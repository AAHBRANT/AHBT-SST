using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Bloqueia encerramento enquanto houver item sem resposta — aplicação do mesmo princípio de
// bloqueio preventivo já usado em AutorizarPermissaoTrabalhoCommand; não é uma regra literal do
// §23 isoladamente, mas garante que "cada inspeção deverá gerar evidência" (§23) tenha, no
// mínimo, todo item do checklist respondido antes de fechar.
public record EncerrarInspecaoCommand(Guid Id) : IRequest;

public class EncerrarInspecaoCommandValidator : AbstractValidator<EncerrarInspecaoCommand>
{
    public EncerrarInspecaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarInspecaoCommandHandler : IRequestHandler<EncerrarInspecaoCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarInspecaoCommand request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes
            .Include(i => i.Respostas)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Inspeção {request.Id} não encontrada.");

        var pendentes = inspecao.Respostas.Where(r => r.Ativo && r.StatusItem == null).ToList();
        if (pendentes.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível encerrar: {pendentes.Count} item(ns) do checklist ainda não respondido(s).");

        inspecao.Status = StatusInspecao.Concluida;
        await _db.SaveChangesAsync(ct);
    }
}
