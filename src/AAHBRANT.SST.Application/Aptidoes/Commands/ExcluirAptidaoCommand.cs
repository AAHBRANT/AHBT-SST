using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aptidoes.Commands;

public record ExcluirAptidaoCommand(Guid Id) : IRequest;

public class ExcluirAptidaoCommandValidator : AbstractValidator<ExcluirAptidaoCommand>
{
    public ExcluirAptidaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAptidaoCommandHandler : IRequestHandler<ExcluirAptidaoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAptidaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAptidaoCommand request, CancellationToken ct)
    {
        var aptidao = await _db.AptidoesAtividadeEspecifica.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Aptidão {request.Id} não encontrada.");

        _db.AptidoesAtividadeEspecifica.Remove(aptidao);
        await _db.SaveChangesAsync(ct);
    }
}
