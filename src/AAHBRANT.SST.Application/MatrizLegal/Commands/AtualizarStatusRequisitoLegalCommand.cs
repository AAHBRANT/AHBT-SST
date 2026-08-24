using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizLegal.Commands;

// Diferente do fluxo linear de NaoConformidade/Acidente: o "status" do §32 (Conforme/Não conforme)
// não é uma sequência de etapas, é uma reclassificação pontual feita a cada revisão do requisito —
// por isso o comando recebe o novo status diretamente, em vez de "avançar" um passo fixo. Decisão
// própria: sempre que o status é reclassificado por este comando, UltimaRevisao é carimbada com a
// data atual, tornando o campo "Última revisão" (§32, controle) um dado concreto de fato preenchido.
public record AtualizarStatusRequisitoLegalCommand(Guid Id, StatusRequisitoLegal NovoStatus) : IRequest;

public class AtualizarStatusRequisitoLegalCommandValidator : AbstractValidator<AtualizarStatusRequisitoLegalCommand>
{
    public AtualizarStatusRequisitoLegalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class AtualizarStatusRequisitoLegalCommandHandler : IRequestHandler<AtualizarStatusRequisitoLegalCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarStatusRequisitoLegalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarStatusRequisitoLegalCommand request, CancellationToken ct)
    {
        var requisito = await _db.RequisitosLegais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Requisito legal {request.Id} não encontrado.");

        requisito.Status = request.NovoStatus;
        requisito.UltimaRevisao = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
