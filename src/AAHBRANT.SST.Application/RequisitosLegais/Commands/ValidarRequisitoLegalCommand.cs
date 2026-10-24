using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RequisitosLegais.Commands;

// QSMS conferiu o texto da norma (e os perigos ligados) e ativa o requisito: a partir daí ele vale
// para o plano de ação sugerido pela IA. Só sai de "Em revisão"; Ativo/Revogado seguem pela edição.
// Quem validou vem do token (preenchido pelo controller), nunca do corpo.
public record ValidarRequisitoLegalCommand(Guid Id, Guid? UsuarioId, string? NomeUsuario) : IRequest;

public class ValidarRequisitoLegalCommandValidator : AbstractValidator<ValidarRequisitoLegalCommand>
{
    public ValidarRequisitoLegalCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class ValidarRequisitoLegalCommandHandler : IRequestHandler<ValidarRequisitoLegalCommand>
{
    private readonly IAppDbContext _db;

    public ValidarRequisitoLegalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ValidarRequisitoLegalCommand request, CancellationToken ct)
    {
        var requisito = await _db.RequisitosLegais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Requisito legal {request.Id} não encontrado.");
        if (requisito.Status != StatusRequisitoLegal.EmRevisao)
            throw new InvalidOperationException("Só um requisito em revisão pode ser validado.");

        var nome = request.UsuarioId is { } id
            ? await _db.Usuarios.Where(u => u.Id == id).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;

        requisito.Status = StatusRequisitoLegal.Ativo;
        requisito.ValidadoEmUtc = DateTime.UtcNow;
        requisito.ValidadoPorUsuarioId = request.UsuarioId;
        requisito.ValidadoPorNome = nome ?? request.NomeUsuario;

        await _db.SaveChangesAsync(ct);
    }
}
