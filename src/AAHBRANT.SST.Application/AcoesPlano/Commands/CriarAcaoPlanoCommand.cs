using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.AcoesPlano.Commands;

public record CriarAcaoPlanoCommand(
    string OrigemTipo,
    Guid OrigemId,
    TipoAcaoPlano Tipo,
    string Descricao,
    Guid? ResponsavelUsuarioId,
    PrioridadeAcao Prioridade,
    DateTime? Prazo) : IRequest<Guid>;

public class CriarAcaoPlanoCommandValidator : AbstractValidator<CriarAcaoPlanoCommand>
{
    public CriarAcaoPlanoCommandValidator()
    {
        RuleFor(x => x.OrigemTipo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OrigemId).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
    }
}

public class CriarAcaoPlanoCommandHandler : IRequestHandler<CriarAcaoPlanoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAcaoPlanoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAcaoPlanoCommand request, CancellationToken ct)
    {
        var acao = new AcaoPlano
        {
            OrigemTipo = request.OrigemTipo,
            OrigemId = request.OrigemId,
            Tipo = request.Tipo,
            Descricao = request.Descricao,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Prioridade = request.Prioridade,
            Prazo = request.Prazo,
        };

        _db.AcoesPlano.Add(acao);
        await _db.SaveChangesAsync(ct);
        return acao.Id;
    }
}
