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
        // Procedimento de Inspeção Técnica de Campo (§7): quando o prazo não é informado
        // explicitamente, sugere um valor a partir da prioridade (Crítica=24h/Alta=48h/Média=5 dias
        // úteis/Baixa=10 dias úteis) — ver SlaPrioridadeCalculator. O usuário pode ajustar depois via
        // AtualizarAcaoPlanoCommand; os prazos do documento são "referência para parametrização",
        // não um teto rígido.
        var prazo = request.Prazo ?? SlaPrioridadeCalculator.CalcularPrazoSugerido(request.Prioridade, DateTime.UtcNow);

        var acao = new AcaoPlano
        {
            OrigemTipo = request.OrigemTipo,
            OrigemId = request.OrigemId,
            Tipo = request.Tipo,
            Descricao = request.Descricao,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Prioridade = request.Prioridade,
            Prazo = prazo,
        };

        _db.AcoesPlano.Add(acao);
        await _db.SaveChangesAsync(ct);
        return acao.Id;
    }
}
