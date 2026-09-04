using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.InstalacoesEpc.Commands;

public record ExcluirInstalacaoEpcCommand(Guid Id) : IRequest;

public class ExcluirInstalacaoEpcCommandValidator : AbstractValidator<ExcluirInstalacaoEpcCommand>
{
    public ExcluirInstalacaoEpcCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirInstalacaoEpcCommandHandler : IRequestHandler<ExcluirInstalacaoEpcCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirInstalacaoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirInstalacaoEpcCommand request, CancellationToken ct)
    {
        var instalacao = await _db.InstalacoesEpc.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Instalação de EPC não encontrada.");

        _db.InstalacoesEpc.Remove(instalacao);
        await _db.SaveChangesAsync(ct);
    }
}
