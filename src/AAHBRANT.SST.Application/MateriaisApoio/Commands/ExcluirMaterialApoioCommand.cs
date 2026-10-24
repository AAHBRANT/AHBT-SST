using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MateriaisApoio.Commands;

public record ExcluirMaterialApoioCommand(Guid Id) : IRequest;

public class ExcluirMaterialApoioCommandValidator : AbstractValidator<ExcluirMaterialApoioCommand>
{
    public ExcluirMaterialApoioCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirMaterialApoioCommandHandler : IRequestHandler<ExcluirMaterialApoioCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirMaterialApoioCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirMaterialApoioCommand request, CancellationToken ct)
    {
        var material = await _db.MateriaisApoio.FirstOrDefaultAsync(m => m.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Material {request.Id} não encontrado.");

        _db.MateriaisApoio.Remove(material);
        await _db.SaveChangesAsync(ct);
    }
}
