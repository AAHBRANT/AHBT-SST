using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aptidoes.Commands;

public record AtualizarAptidaoCommand(
    Guid Id,
    Guid TrabalhadorId,
    string AtividadeCritica,
    ResultadoAso Aptidao,
    DateTime DataAvaliacao,
    DateTime? DataValidade,
    string? MedicoResponsavel,
    string? Observacoes) : IRequest;

public class AtualizarAptidaoCommandValidator : AbstractValidator<AtualizarAptidaoCommand>
{
    public AtualizarAptidaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.AtividadeCritica).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DataAvaliacao).NotEmpty();
        RuleFor(x => x.DataValidade).GreaterThanOrEqualTo(x => x.DataAvaliacao).When(x => x.DataValidade.HasValue);
    }
}

public class AtualizarAptidaoCommandHandler : IRequestHandler<AtualizarAptidaoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAptidaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAptidaoCommand request, CancellationToken ct)
    {
        var aptidao = await _db.AptidoesAtividadeEspecifica.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Aptidão {request.Id} não encontrada.");

        aptidao.TrabalhadorId = request.TrabalhadorId;
        aptidao.AtividadeCritica = request.AtividadeCritica;
        aptidao.Aptidao = request.Aptidao;
        aptidao.DataAvaliacao = request.DataAvaliacao;
        aptidao.DataValidade = request.DataValidade;
        aptidao.MedicoResponsavel = request.MedicoResponsavel;
        aptidao.Observacoes = request.Observacoes;

        await _db.SaveChangesAsync(ct);
    }
}
