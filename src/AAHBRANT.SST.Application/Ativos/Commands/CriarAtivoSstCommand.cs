using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Ativos.Commands;

public record CriarAtivoSstCommand(
    Guid ObraId,
    TipoAtivo TipoAtivo,
    string Identificacao,
    string Descricao,
    string? Localizacao,
    DateTime DataValidade,
    string? Observacoes) : IRequest<Guid>;

public class CriarAtivoSstCommandValidator : AbstractValidator<CriarAtivoSstCommand>
{
    public CriarAtivoSstCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Identificacao).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(200);
    }
}

public class CriarAtivoSstCommandHandler : IRequestHandler<CriarAtivoSstCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAtivoSstCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAtivoSstCommand request, CancellationToken ct)
    {
        var ativo = new AtivoSst
        {
            ObraId = request.ObraId,
            TipoAtivo = request.TipoAtivo,
            Identificacao = request.Identificacao,
            Descricao = request.Descricao,
            Localizacao = request.Localizacao,
            DataValidade = request.DataValidade,
            Observacoes = request.Observacoes
        };

        _db.AtivosSst.Add(ativo);
        await _db.SaveChangesAsync(ct);
        return ativo.Id;
    }
}
