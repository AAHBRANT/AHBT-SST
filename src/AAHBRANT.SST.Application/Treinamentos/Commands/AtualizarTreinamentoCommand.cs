using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Commands;

public record AtualizarTreinamentoCommand(
    Guid Id,
    Guid TrabalhadorId,
    Guid CursoTreinamentoId,
    DateTime DataRealizacao,
    DateTime DataValidade,
    int CargaHorariaRealizada,
    string? InstituicaoInstrutor,
    string? NumeroCertificado) : IRequest;

public class AtualizarTreinamentoCommandValidator : AbstractValidator<AtualizarTreinamentoCommand>
{
    public AtualizarTreinamentoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CursoTreinamentoId).NotEmpty();
        RuleFor(x => x.DataRealizacao).NotEmpty();
        RuleFor(x => x.DataValidade).NotEmpty().GreaterThanOrEqualTo(x => x.DataRealizacao);
    }
}

public class AtualizarTreinamentoCommandHandler : IRequestHandler<AtualizarTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarTreinamentoCommand request, CancellationToken ct)
    {
        var treinamento = await _db.Treinamentos.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Treinamento não encontrado.");

        treinamento.TrabalhadorId = request.TrabalhadorId;
        treinamento.CursoTreinamentoId = request.CursoTreinamentoId;
        treinamento.DataRealizacao = request.DataRealizacao;
        treinamento.DataValidade = request.DataValidade;
        treinamento.CargaHorariaRealizada = request.CargaHorariaRealizada;
        treinamento.InstituicaoInstrutor = request.InstituicaoInstrutor;
        treinamento.NumeroCertificado = request.NumeroCertificado;

        await _db.SaveChangesAsync(ct);
    }
}
