using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Commands;

public record ExcluirEmpresaCommand(Guid Id) : IRequest;

public class ExcluirEmpresaCommandValidator : AbstractValidator<ExcluirEmpresaCommand>
{
    public ExcluirEmpresaCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class ExcluirEmpresaCommandHandler : IRequestHandler<ExcluirEmpresaCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirEmpresaCommand request, CancellationToken ct)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        empresa.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
