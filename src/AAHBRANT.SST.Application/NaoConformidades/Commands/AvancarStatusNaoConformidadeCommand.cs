using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

// Avança um passo no fluxo literal do §25: Aberta → Em tratamento → Aguardando validação → Encerrada.
// Bloqueio de negócio (decisão própria, não citada literalmente na Base de Conhecimento): o último
// passo (AguardandoValidacao → Encerrada) só é permitido se todo AcaoPlano vinculado (via
// OrigemTipo=nameof(NaoConformidade)/OrigemId) já estiver com Status == Concluido — mesmo padrão de
// "bloqueio preventivo" usado em EncerrarInspecaoCommand.
public record AvancarStatusNaoConformidadeCommand(Guid Id) : IRequest;

public class AvancarStatusNaoConformidadeCommandValidator : AbstractValidator<AvancarStatusNaoConformidadeCommand>
{
    public AvancarStatusNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class AvancarStatusNaoConformidadeCommandHandler : IRequestHandler<AvancarStatusNaoConformidadeCommand>
{
    private readonly IAppDbContext _db;

    public AvancarStatusNaoConformidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AvancarStatusNaoConformidadeCommand request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.Id} não encontrada.");

        if (nc.Status == StatusNaoConformidade.Encerrada)
            throw new InvalidOperationException("Esta não conformidade já está encerrada.");

        if (nc.Status == StatusNaoConformidade.AguardandoValidacao)
        {
            var existeAcaoPendente = await _db.AcoesPlano.AnyAsync(
                a => a.OrigemTipo == nameof(Domain.Entidades.NaoConformidade) &&
                     a.OrigemId == nc.Id &&
                     a.Status != StatusControleRisco.Concluido,
                ct);

            if (existeAcaoPendente)
                throw new InvalidOperationException(
                    "Não é possível encerrar: existem ações do plano vinculadas ainda não concluídas.");

            nc.DataConclusao = DateTime.UtcNow;
        }

        nc.Status = nc.Status switch
        {
            StatusNaoConformidade.Aberta => StatusNaoConformidade.EmTratamento,
            StatusNaoConformidade.EmTratamento => StatusNaoConformidade.AguardandoValidacao,
            StatusNaoConformidade.AguardandoValidacao => StatusNaoConformidade.Encerrada,
            _ => nc.Status,
        };

        await _db.SaveChangesAsync(ct);
    }
}
