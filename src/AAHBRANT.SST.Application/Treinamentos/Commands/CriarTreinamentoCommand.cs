using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Treinamentos.Commands;

public record CriarTreinamentoCommand(
    Guid TrabalhadorId,
    Guid CursoTreinamentoId,
    DateTime DataRealizacao,
    DateTime DataValidade,
    int CargaHorariaRealizada,
    string? InstituicaoInstrutor,
    string? NumeroCertificado) : IRequest<Guid>;

public class CriarTreinamentoCommandValidator : AbstractValidator<CriarTreinamentoCommand>
{
    public CriarTreinamentoCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CursoTreinamentoId).NotEmpty();
        RuleFor(x => x.DataRealizacao).NotEmpty();
        RuleFor(x => x.DataValidade).NotEmpty().GreaterThanOrEqualTo(x => x.DataRealizacao);
    }
}

public class CriarTreinamentoCommandHandler : IRequestHandler<CriarTreinamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarTreinamentoCommand request, CancellationToken ct)
    {
        var treinamento = new Treinamento
        {
            TrabalhadorId = request.TrabalhadorId,
            CursoTreinamentoId = request.CursoTreinamentoId,
            DataRealizacao = request.DataRealizacao,
            DataValidade = request.DataValidade,
            CargaHorariaRealizada = request.CargaHorariaRealizada,
            InstituicaoInstrutor = request.InstituicaoInstrutor,
            NumeroCertificado = request.NumeroCertificado,
        };
        _db.Treinamentos.Add(treinamento);
        await _db.SaveChangesAsync(ct);
        return treinamento.Id;
    }
}
