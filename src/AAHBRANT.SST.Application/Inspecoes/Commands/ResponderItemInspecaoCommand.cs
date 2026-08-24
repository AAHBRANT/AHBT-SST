using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

public record ResponderItemInspecaoCommand(
    Guid RespostaId,
    StatusItemChecklist StatusItem,
    string? Observacao,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo) : IRequest;

public class ResponderItemInspecaoCommandValidator : AbstractValidator<ResponderItemInspecaoCommand>
{
    public ResponderItemInspecaoCommandValidator()
    {
        RuleFor(x => x.RespostaId).NotEmpty();
        RuleFor(x => x.Observacao).MaximumLength(1000);
    }
}

public class ResponderItemInspecaoCommandHandler : IRequestHandler<ResponderItemInspecaoCommand>
{
    private readonly IAppDbContext _db;

    public ResponderItemInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ResponderItemInspecaoCommand request, CancellationToken ct)
    {
        var resposta = await _db.InspecaoItemRespostas.FirstOrDefaultAsync(r => r.Id == request.RespostaId, ct)
            ?? throw new KeyNotFoundException($"Resposta {request.RespostaId} não encontrada.");

        if (request.ResponsavelUsuarioId.HasValue)
        {
            var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId.Value, ct);
            if (!usuarioExiste)
                throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");
        }

        resposta.StatusItem = request.StatusItem;
        resposta.Observacao = request.Observacao;
        resposta.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        resposta.Prazo = request.Prazo;

        await _db.SaveChangesAsync(ct);
    }
}
