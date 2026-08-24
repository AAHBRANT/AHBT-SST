using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Commands;

public record RemoverPerfilObraCommand(Guid Id) : IRequest;

public class RemoverPerfilObraCommandValidator : AbstractValidator<RemoverPerfilObraCommand>
{
    public RemoverPerfilObraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class RemoverPerfilObraCommandHandler : IRequestHandler<RemoverPerfilObraCommand>
{
    private readonly IAppDbContext _db;

    public RemoverPerfilObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverPerfilObraCommand request, CancellationToken ct)
    {
        var vinculo = await _db.UsuariosPerfilObra.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Vínculo {request.Id} não encontrado.");

        _db.UsuariosPerfilObra.Remove(vinculo);
        await _db.SaveChangesAsync(ct);
    }
}
