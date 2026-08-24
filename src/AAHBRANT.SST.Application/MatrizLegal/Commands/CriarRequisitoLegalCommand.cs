using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizLegal.Commands;

public record CriarRequisitoLegalCommand(
    string Codigo,
    string Norma,
    string? Item,
    string Tema,
    string Requisito,
    bool Aplicabilidade,
    string? Justificativa,
    string? Evidencia,
    Guid? ResponsavelUsuarioId,
    string? Periodicidade,
    DateTime? Prazo,
    DateTime? UltimaRevisao,
    DateTime? ProximaRevisao,
    Guid? ObraId) : IRequest<Guid>;

public class CriarRequisitoLegalCommandValidator : AbstractValidator<CriarRequisitoLegalCommand>
{
    public CriarRequisitoLegalCommandValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Norma).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Item).MaximumLength(100);
        RuleFor(x => x.Tema).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Requisito).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Justificativa).MaximumLength(1000);
        RuleFor(x => x.Evidencia).MaximumLength(500);
        RuleFor(x => x.Periodicidade).MaximumLength(100);
    }
}

public class CriarRequisitoLegalCommandHandler : IRequestHandler<CriarRequisitoLegalCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarRequisitoLegalCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarRequisitoLegalCommand request, CancellationToken ct)
    {
        if (request.ResponsavelUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        if (request.ObraId.HasValue &&
            !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var requisito = new RequisitoLegal
        {
            Codigo = request.Codigo,
            Norma = request.Norma,
            Item = request.Item,
            Tema = request.Tema,
            Requisito = request.Requisito,
            Aplicabilidade = request.Aplicabilidade,
            Justificativa = request.Justificativa,
            Evidencia = request.Evidencia,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Periodicidade = request.Periodicidade,
            Prazo = request.Prazo,
            UltimaRevisao = request.UltimaRevisao,
            ProximaRevisao = request.ProximaRevisao,
            ObraId = request.ObraId,
        };

        _db.RequisitosLegais.Add(requisito);
        await _db.SaveChangesAsync(ct);
        return requisito.Id;
    }
}
