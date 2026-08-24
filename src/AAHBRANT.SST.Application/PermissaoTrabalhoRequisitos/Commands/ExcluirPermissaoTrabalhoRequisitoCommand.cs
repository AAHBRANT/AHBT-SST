using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoRequisitos.Commands;

public record ExcluirPermissaoTrabalhoRequisitoCommand(Guid Id) : IRequest;

public class ExcluirPermissaoTrabalhoRequisitoCommandValidator : AbstractValidator<ExcluirPermissaoTrabalhoRequisitoCommand>
{
    public ExcluirPermissaoTrabalhoRequisitoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPermissaoTrabalhoRequisitoCommandHandler : IRequestHandler<ExcluirPermissaoTrabalhoRequisitoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPermissaoTrabalhoRequisitoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPermissaoTrabalhoRequisitoCommand request, CancellationToken ct)
    {
        var requisito = await _db.PermissaoTrabalhoRequisitos.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Requisito {request.Id} não encontrado.");

        _db.PermissaoTrabalhoRequisitos.Remove(requisito);
        await _db.SaveChangesAsync(ct);
    }
}
