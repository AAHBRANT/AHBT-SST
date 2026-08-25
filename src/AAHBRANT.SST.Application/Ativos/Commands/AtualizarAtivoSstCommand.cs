using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ativos.Commands;

public record AtualizarAtivoSstCommand(
    Guid Id,
    Guid ObraId,
    TipoAtivo TipoAtivo,
    string Identificacao,
    string Descricao,
    string? Localizacao,
    DateTime DataValidade,
    string? Observacoes) : IRequest;

public class AtualizarAtivoSstCommandValidator : AbstractValidator<AtualizarAtivoSstCommand>
{
    public AtualizarAtivoSstCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Identificacao).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarAtivoSstCommandHandler : IRequestHandler<AtualizarAtivoSstCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAtivoSstCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAtivoSstCommand request, CancellationToken ct)
    {
        var ativo = await _db.AtivosSst.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ativo {request.Id} não encontrado.");

        ativo.ObraId = request.ObraId;
        ativo.TipoAtivo = request.TipoAtivo;
        ativo.Identificacao = request.Identificacao;
        ativo.Descricao = request.Descricao;
        ativo.Localizacao = request.Localizacao;
        ativo.DataValidade = request.DataValidade;
        ativo.Observacoes = request.Observacoes;

        await _db.SaveChangesAsync(ct);
    }
}
