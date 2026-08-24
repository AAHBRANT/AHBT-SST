using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoRequisitos.Commands;

// Alterna o atendimento de um requisito da PT. Não é literal do §18 (que só cita "requisitos"
// como campo), mas é necessário para operar o checklist — este é o gate consultado por
// AutorizarPermissaoTrabalhoCommand antes de liberar a atividade.
public record MarcarPermissaoTrabalhoRequisitoCommand(Guid Id, bool Atendido) : IRequest;

public class MarcarPermissaoTrabalhoRequisitoCommandValidator : AbstractValidator<MarcarPermissaoTrabalhoRequisitoCommand>
{
    public MarcarPermissaoTrabalhoRequisitoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class MarcarPermissaoTrabalhoRequisitoCommandHandler : IRequestHandler<MarcarPermissaoTrabalhoRequisitoCommand>
{
    private readonly IAppDbContext _db;

    public MarcarPermissaoTrabalhoRequisitoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(MarcarPermissaoTrabalhoRequisitoCommand request, CancellationToken ct)
    {
        var requisito = await _db.PermissaoTrabalhoRequisitos.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Requisito {request.Id} não encontrado.");

        requisito.Atendido = request.Atendido;
        await _db.SaveChangesAsync(ct);
    }
}
