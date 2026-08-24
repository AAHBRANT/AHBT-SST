using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

// Avança um passo no fluxo não-literal (ver Domain/Enums/Enums.cs, StatusAcidente):
// Registrado → Em investigação → Concluído.
// Bloqueio de negócio (decisão própria, mesmo padrão de "bloqueio preventivo" usado em
// AvancarStatusNaoConformidadeCommand/EncerrarInspecaoCommand): o último passo (EmInvestigacao →
// Concluido) só é permitido se todo AcaoPlano vinculado (via OrigemTipo=nameof(Acidente)/OrigemId)
// já estiver com Status == Concluido.
public record AvancarStatusAcidenteCommand(Guid Id) : IRequest;

public class AvancarStatusAcidenteCommandValidator : AbstractValidator<AvancarStatusAcidenteCommand>
{
    public AvancarStatusAcidenteCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class AvancarStatusAcidenteCommandHandler : IRequestHandler<AvancarStatusAcidenteCommand>
{
    private readonly IAppDbContext _db;

    public AvancarStatusAcidenteCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AvancarStatusAcidenteCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        if (acidente.Status == StatusAcidente.Concluido)
            throw new InvalidOperationException("Este acidente já está com a investigação concluída.");

        if (acidente.Status == StatusAcidente.EmInvestigacao)
        {
            var existeAcaoPendente = await _db.AcoesPlano.AnyAsync(
                a => a.OrigemTipo == nameof(Domain.Entidades.Acidente) &&
                     a.OrigemId == acidente.Id &&
                     a.Status != StatusControleRisco.Concluido,
                ct);

            if (existeAcaoPendente)
                throw new InvalidOperationException(
                    "Não é possível concluir: existem ações do plano vinculadas ainda não concluídas.");

            acidente.DataConclusaoInvestigacao = DateTime.UtcNow;
        }

        acidente.Status = acidente.Status switch
        {
            StatusAcidente.Registrado => StatusAcidente.EmInvestigacao,
            StatusAcidente.EmInvestigacao => StatusAcidente.Concluido,
            _ => acidente.Status,
        };

        await _db.SaveChangesAsync(ct);
    }
}
