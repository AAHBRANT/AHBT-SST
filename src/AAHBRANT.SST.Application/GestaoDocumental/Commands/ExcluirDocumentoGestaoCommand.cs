using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GestaoDocumental.Commands;

public record ExcluirDocumentoGestaoCommand(Guid Id) : IRequest;

public class ExcluirDocumentoGestaoCommandValidator : AbstractValidator<ExcluirDocumentoGestaoCommand>
{
    public ExcluirDocumentoGestaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirDocumentoGestaoCommandHandler : IRequestHandler<ExcluirDocumentoGestaoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirDocumentoGestaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirDocumentoGestaoCommand request, CancellationToken ct)
    {
        var documento = await _db.DocumentosGestao.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Documento {request.Id} não encontrado.");

        _db.DocumentosGestao.Remove(documento);
        await _db.SaveChangesAsync(ct);
    }
}
