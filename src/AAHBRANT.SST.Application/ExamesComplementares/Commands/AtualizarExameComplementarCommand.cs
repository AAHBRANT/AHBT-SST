using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ExamesComplementares.Commands;

public record AtualizarExameComplementarCommand(
    Guid Id,
    Guid TrabalhadorId,
    Guid? AsoId,
    TipoExameComplementar Tipo,
    DateTime DataRealizacao,
    DateTime DataValidade,
    string Resultado,
    string? Observacoes,
    string? ResponsavelTecnico) : IRequest;

public class AtualizarExameComplementarCommandValidator : AbstractValidator<AtualizarExameComplementarCommand>
{
    public AtualizarExameComplementarCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DataRealizacao).NotEmpty();
        RuleFor(x => x.DataValidade).NotEmpty().GreaterThanOrEqualTo(x => x.DataRealizacao);
        RuleFor(x => x.Resultado).NotEmpty().MaximumLength(300);
    }
}

public class AtualizarExameComplementarCommandHandler : IRequestHandler<AtualizarExameComplementarCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarExameComplementarCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarExameComplementarCommand request, CancellationToken ct)
    {
        var exame = await _db.ExamesComplementares.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Exame complementar {request.Id} não encontrado.");

        exame.TrabalhadorId = request.TrabalhadorId;
        exame.AsoId = request.AsoId;
        exame.Tipo = request.Tipo;
        exame.DataRealizacao = request.DataRealizacao;
        exame.DataValidade = request.DataValidade;
        exame.Resultado = request.Resultado;
        exame.Observacoes = request.Observacoes;
        exame.ResponsavelTecnico = request.ResponsavelTecnico;

        await _db.SaveChangesAsync(ct);
    }
}
