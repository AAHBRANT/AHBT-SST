using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoPreRequisitos.Commands;

// §2 do formulário — cada PT já nasce com os 6 itens fixos (CriarPermissaoTrabalhoCommand); este
// comando só alterna Atendido, não cria/exclui linha.
public record MarcarPermissaoTrabalhoPreRequisitoCommand(Guid Id, bool Atendido) : IRequest;

public class MarcarPermissaoTrabalhoPreRequisitoCommandValidator : AbstractValidator<MarcarPermissaoTrabalhoPreRequisitoCommand>
{
    public MarcarPermissaoTrabalhoPreRequisitoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class MarcarPermissaoTrabalhoPreRequisitoCommandHandler : IRequestHandler<MarcarPermissaoTrabalhoPreRequisitoCommand>
{
    private readonly IAppDbContext _db;

    public MarcarPermissaoTrabalhoPreRequisitoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(MarcarPermissaoTrabalhoPreRequisitoCommand request, CancellationToken ct)
    {
        var item = await _db.PermissaoTrabalhoPreRequisitos.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Pré-requisito {request.Id} não encontrado.");

        item.Atendido = request.Atendido;
        await _db.SaveChangesAsync(ct);
    }
}
