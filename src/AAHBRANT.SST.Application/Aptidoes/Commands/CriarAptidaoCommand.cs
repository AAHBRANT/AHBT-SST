using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Aptidoes.Commands;

public record CriarAptidaoCommand(
    Guid TrabalhadorId,
    string AtividadeCritica,
    ResultadoAso Aptidao,
    DateTime DataAvaliacao,
    DateTime? DataValidade,
    string? MedicoResponsavel,
    string? Observacoes) : IRequest<Guid>;

public class CriarAptidaoCommandValidator : AbstractValidator<CriarAptidaoCommand>
{
    public CriarAptidaoCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.AtividadeCritica).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DataAvaliacao).NotEmpty();
        RuleFor(x => x.DataValidade).GreaterThanOrEqualTo(x => x.DataAvaliacao).When(x => x.DataValidade.HasValue);
    }
}

public class CriarAptidaoCommandHandler : IRequestHandler<CriarAptidaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAptidaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAptidaoCommand request, CancellationToken ct)
    {
        var aptidao = new AptidaoAtividadeEspecifica
        {
            TrabalhadorId = request.TrabalhadorId,
            AtividadeCritica = request.AtividadeCritica,
            Aptidao = request.Aptidao,
            DataAvaliacao = request.DataAvaliacao,
            DataValidade = request.DataValidade,
            MedicoResponsavel = request.MedicoResponsavel,
            Observacoes = request.Observacoes
        };

        _db.AptidoesAtividadeEspecifica.Add(aptidao);
        await _db.SaveChangesAsync(ct);
        return aptidao.Id;
    }
}
