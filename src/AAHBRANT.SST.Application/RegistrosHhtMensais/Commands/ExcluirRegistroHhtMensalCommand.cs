using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegistrosHhtMensais.Commands;

public record ExcluirRegistroHhtMensalCommand(Guid Id) : IRequest;

public class ExcluirRegistroHhtMensalCommandValidator : AbstractValidator<ExcluirRegistroHhtMensalCommand>
{
    public ExcluirRegistroHhtMensalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirRegistroHhtMensalCommandHandler : IRequestHandler<ExcluirRegistroHhtMensalCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirRegistroHhtMensalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirRegistroHhtMensalCommand request, CancellationToken ct)
    {
        var registro = await _db.RegistrosHhtMensais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Registro de HHT {request.Id} não encontrado.");

        _db.RegistrosHhtMensais.Remove(registro);
        await _db.SaveChangesAsync(ct);
    }
}
