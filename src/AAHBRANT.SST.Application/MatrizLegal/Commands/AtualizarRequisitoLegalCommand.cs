using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizLegal.Commands;

public record AtualizarRequisitoLegalCommand(
    Guid Id,
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
    Guid? ObraId) : IRequest;

public class AtualizarRequisitoLegalCommandValidator : AbstractValidator<AtualizarRequisitoLegalCommand>
{
    public AtualizarRequisitoLegalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class AtualizarRequisitoLegalCommandHandler : IRequestHandler<AtualizarRequisitoLegalCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarRequisitoLegalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarRequisitoLegalCommand request, CancellationToken ct)
    {
        var requisito = await _db.RequisitosLegais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Requisito legal {request.Id} não encontrado.");

        if (request.ResponsavelUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        if (request.ObraId.HasValue &&
            !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        requisito.Codigo = request.Codigo;
        requisito.Norma = request.Norma;
        requisito.Item = request.Item;
        requisito.Tema = request.Tema;
        requisito.Requisito = request.Requisito;
        requisito.Aplicabilidade = request.Aplicabilidade;
        requisito.Justificativa = request.Justificativa;
        requisito.Evidencia = request.Evidencia;
        requisito.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        requisito.Periodicidade = request.Periodicidade;
        requisito.Prazo = request.Prazo;
        requisito.UltimaRevisao = request.UltimaRevisao;
        requisito.ProximaRevisao = request.ProximaRevisao;
        requisito.ObraId = request.ObraId;

        await _db.SaveChangesAsync(ct);
    }
}
