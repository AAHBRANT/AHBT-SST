using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.AreasSst.Commands;

public record CriarAreaSstCommand(
    string Codigo,
    string Nome,
    TipoArea Tipo,
    Guid ObraId,
    string? DetalhesLocalizacao,
    List<string> Riscos,
    List<string> Requisitos) : IRequest<Guid>;

public class CriarAreaSstCommandValidator : AbstractValidator<CriarAreaSstCommand>
{
    public CriarAreaSstCommandValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ObraId).NotEmpty();
    }
}

public class CriarAreaSstCommandHandler : IRequestHandler<CriarAreaSstCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAreaSstCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAreaSstCommand request, CancellationToken ct)
    {
        var area = new AreaSst
        {
            Codigo = request.Codigo,
            Nome = request.Nome,
            Tipo = request.Tipo,
            ObraId = request.ObraId,
            DetalhesLocalizacao = request.DetalhesLocalizacao,
            Riscos = request.Riscos ?? new List<string>(),
            Requisitos = request.Requisitos ?? new List<string>()
        };

        _db.AreasSst.Add(area);
        await _db.SaveChangesAsync(ct);
        return area.Id;
    }
}
