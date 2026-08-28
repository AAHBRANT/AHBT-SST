using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record AtualizarPcmsoCommand(
    Guid Id,
    string Nome,
    string? Versao,
    DateTime? Validade,
    DateTime DataEmissao,
    Guid? ResponsavelUsuarioId,
    Guid? ObraId,
    Guid? SetorId,
    string? Arquivo,
    string? MedicoResponsavelNome,
    string? MedicoResponsavelCrm,
    string? FuncoesContempladas,
    string? RiscosConsiderados,
    string? ExamesPrevistos,
    string? Periodicidades,
    string? UnidadesObrasAbrangidas) : IRequest;

public class AtualizarPcmsoCommandValidator : AbstractValidator<AtualizarPcmsoCommand>
{
    public AtualizarPcmsoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Versao).MaximumLength(50);
        RuleFor(x => x.Arquivo).MaximumLength(500);
        RuleFor(x => x.MedicoResponsavelNome).MaximumLength(150);
        RuleFor(x => x.MedicoResponsavelCrm).MaximumLength(30);
        RuleFor(x => x.DataEmissao).NotEmpty();
    }
}

public class AtualizarPcmsoCommandHandler : IRequestHandler<AtualizarPcmsoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public Task Handle(AtualizarPcmsoCommand request, CancellationToken ct)
    {
        // PENDENTE: dependia de DocumentoGestao, removido junto com Gestão Documental (Conformidade)
        // em 2026-08-28 — ver nota em PcmsoDetalhe (Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs).
        throw new NotSupportedException(
            "Atualização de PCMSO está temporariamente indisponível: dependia de DocumentoGestao, removido junto com o módulo de Conformidade.");
    }
}
