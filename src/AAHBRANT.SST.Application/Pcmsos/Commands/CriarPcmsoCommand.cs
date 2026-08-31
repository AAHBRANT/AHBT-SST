using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record CriarPcmsoCommand(
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
    string? UnidadesObrasAbrangidas) : IRequest<Guid>;

public class CriarPcmsoCommandValidator : AbstractValidator<CriarPcmsoCommand>
{
    public CriarPcmsoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Versao).MaximumLength(50);
        RuleFor(x => x.Arquivo).MaximumLength(500);
        RuleFor(x => x.MedicoResponsavelNome).MaximumLength(150);
        RuleFor(x => x.MedicoResponsavelCrm).MaximumLength(30);
        RuleFor(x => x.DataEmissao).NotEmpty();
    }
}

public class CriarPcmsoCommandHandler : IRequestHandler<CriarPcmsoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public Task<Guid> Handle(CriarPcmsoCommand request, CancellationToken ct)
    {
        // PENDENTE: este handler criava um DocumentoGestao (Tipo="PCMSO") para guardar
        // nome/versão/validade/status/arquivo — DocumentoGestao foi removido junto com Gestão
        // Documental (Conformidade) em 2026-08-28. Precisa ser reformulado para não depender mais
        // dele antes de voltar a funcionar (ver PcmsoDetalhe).
        throw new NotSupportedException(
            "Criação de PCMSO está temporariamente indisponível: dependia de DocumentoGestao, removido junto com o módulo de Conformidade.");
    }
}
