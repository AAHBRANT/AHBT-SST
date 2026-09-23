using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Commands;

// Remove o certificado digitalizado anexado a um treinamento (22/09) — mesmo padrão de
// RemoverFotoEvidenciaSessaoTreinamentoCommand.
public record RemoverArquivoCertificadoTreinamentoCommand(Guid TreinamentoId) : IRequest;

public class RemoverArquivoCertificadoTreinamentoCommandValidator : AbstractValidator<RemoverArquivoCertificadoTreinamentoCommand>
{
    public RemoverArquivoCertificadoTreinamentoCommandValidator()
    {
        RuleFor(x => x.TreinamentoId).NotEmpty();
    }
}

public class RemoverArquivoCertificadoTreinamentoCommandHandler : IRequestHandler<RemoverArquivoCertificadoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    public RemoverArquivoCertificadoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverArquivoCertificadoTreinamentoCommand request, CancellationToken ct)
    {
        var arquivo = await _db.ArquivosCertificadoTreinamento
            .FirstOrDefaultAsync(a => a.TreinamentoId == request.TreinamentoId, ct)
            ?? throw new KeyNotFoundException($"Treinamento {request.TreinamentoId} não tem certificado anexado.");

        _db.ArquivosCertificadoTreinamento.Remove(arquivo);
        await _db.SaveChangesAsync(ct);
    }
}
