using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record ExcluirPcmsoCommand(Guid Id) : IRequest;

public class ExcluirPcmsoCommandValidator : AbstractValidator<ExcluirPcmsoCommand>
{
    public ExcluirPcmsoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPcmsoCommandHandler : IRequestHandler<ExcluirPcmsoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public Task Handle(ExcluirPcmsoCommand request, CancellationToken ct)
    {
        // PENDENTE: dependia de DocumentoGestao, removido junto com Gestão Documental (Conformidade)
        // em 2026-08-28 — ver nota em PcmsoDetalhe (Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs).
        throw new NotSupportedException(
            "Exclusão de PCMSO está temporariamente indisponível: dependia de DocumentoGestao, removido junto com o módulo de Conformidade.");
    }
}
