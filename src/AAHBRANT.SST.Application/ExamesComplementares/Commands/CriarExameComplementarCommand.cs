using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.ExamesComplementares.Commands;

public record CriarExameComplementarCommand(
    Guid TrabalhadorId,
    Guid? AsoId,
    TipoExameComplementar Tipo,
    DateTime DataRealizacao,
    DateTime DataValidade,
    string Resultado,
    string? Observacoes,
    string? ResponsavelTecnico) : IRequest<Guid>;

public class CriarExameComplementarCommandValidator : AbstractValidator<CriarExameComplementarCommand>
{
    public CriarExameComplementarCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DataRealizacao).NotEmpty();
        RuleFor(x => x.DataValidade).NotEmpty().GreaterThanOrEqualTo(x => x.DataRealizacao);
        RuleFor(x => x.Resultado).NotEmpty().MaximumLength(300);
    }
}

public class CriarExameComplementarCommandHandler : IRequestHandler<CriarExameComplementarCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarExameComplementarCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarExameComplementarCommand request, CancellationToken ct)
    {
        var exame = new ExameComplementar
        {
            TrabalhadorId = request.TrabalhadorId,
            AsoId = request.AsoId,
            Tipo = request.Tipo,
            DataRealizacao = request.DataRealizacao,
            DataValidade = request.DataValidade,
            Resultado = request.Resultado,
            Observacoes = request.Observacoes,
            ResponsavelTecnico = request.ResponsavelTecnico
        };

        _db.ExamesComplementares.Add(exame);
        await _db.SaveChangesAsync(ct);
        return exame.Id;
    }
}
