using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AreasSst.Commands;

public record AtualizarAreaSstCommand(
    Guid Id,
    string Codigo,
    string Nome,
    TipoArea Tipo,
    Guid ObraId,
    string? DetalhesLocalizacao,
    List<string> Riscos,
    List<string> Requisitos,
    StatusArea Status) : IRequest;

public class AtualizarAreaSstCommandValidator : AbstractValidator<AtualizarAreaSstCommand>
{
    public AtualizarAreaSstCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ObraId).NotEmpty();
    }
}

public class AtualizarAreaSstCommandHandler : IRequestHandler<AtualizarAreaSstCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAreaSstCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAreaSstCommand request, CancellationToken ct)
    {
        var area = await _db.AreasSst.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Área {request.Id} não encontrada.");

        area.Codigo = request.Codigo;
        area.Nome = request.Nome;
        area.Tipo = request.Tipo;
        area.ObraId = request.ObraId;
        area.DetalhesLocalizacao = request.DetalhesLocalizacao;
        area.Riscos = request.Riscos ?? new List<string>();
        area.Requisitos = request.Requisitos ?? new List<string>();
        area.Status = request.Status;

        await _db.SaveChangesAsync(ct);
    }
}
