using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AcoesPlano.Commands;

// "Validação" (§26, linha 656) como evento concreto — ver disclosure em AcaoPlano.cs. Marca a ação
// como concluída e validada por um usuário, o que a NC (§25) usa como pré-condição para encerrar.
public record ValidarAcaoPlanoCommand(Guid Id, Guid ValidadoPorUsuarioId) : IRequest;

public class ValidarAcaoPlanoCommandValidator : AbstractValidator<ValidarAcaoPlanoCommand>
{
    public ValidarAcaoPlanoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ValidadoPorUsuarioId).NotEmpty();
    }
}

public class ValidarAcaoPlanoCommandHandler : IRequestHandler<ValidarAcaoPlanoCommand>
{
    private readonly IAppDbContext _db;

    public ValidarAcaoPlanoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ValidarAcaoPlanoCommand request, CancellationToken ct)
    {
        var acao = await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ação de plano {request.Id} não encontrada.");

        if (acao.DataValidacao != null)
            throw new InvalidOperationException("Esta ação já foi validada.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.ValidadoPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.ValidadoPorUsuarioId} não encontrado.");

        var agora = DateTime.UtcNow;
        acao.Status = StatusControleRisco.Concluido;
        acao.DataConclusao ??= agora;
        acao.DataValidacao = agora;
        acao.ValidadoPorUsuarioId = request.ValidadoPorUsuarioId;

        await _db.SaveChangesAsync(ct);
    }
}
