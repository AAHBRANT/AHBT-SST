using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

public record AtualizarAcidenteCommand(
    Guid Id,
    TipoOcorrencia Tipo,
    Guid ObraId,
    Guid? TrabalhadorId,
    Guid? AtividadeId,
    string Local,
    DateTime Data,
    TimeSpan? Hora,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    MetodologiaInvestigacao? MetodologiaInvestigacao,
    string? Causas) : IRequest;

public class AtualizarAcidenteCommandValidator : AbstractValidator<AtualizarAcidenteCommand>
{
    public AtualizarAcidenteCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Lesao).MaximumLength(500);
        RuleFor(x => x.Consequencia).MaximumLength(500);
        RuleFor(x => x.Atendimento).MaximumLength(500);
        RuleFor(x => x.NumeroCat).MaximumLength(50);
        RuleFor(x => x.Causas).MaximumLength(2000);
        RuleFor(x => x.DiasAfastamento).GreaterThanOrEqualTo(0).When(x => x.DiasAfastamento.HasValue);
    }
}

public class AtualizarAcidenteCommandHandler : IRequestHandler<AtualizarAcidenteCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAcidenteCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAcidenteCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.TrabalhadorId.HasValue &&
            !await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct))
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        if (request.AtividadeId.HasValue &&
            !await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct))
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        acidente.Tipo = request.Tipo;
        acidente.ObraId = request.ObraId;
        acidente.TrabalhadorId = request.TrabalhadorId;
        acidente.AtividadeId = request.AtividadeId;
        acidente.Local = request.Local;
        acidente.Data = request.Data;
        acidente.Hora = request.Hora;
        acidente.Descricao = request.Descricao;
        acidente.Lesao = request.Lesao;
        acidente.Consequencia = request.Consequencia;
        acidente.Atendimento = request.Atendimento;
        acidente.HouveAfastamento = request.HouveAfastamento;
        acidente.DiasAfastamento = request.DiasAfastamento;
        acidente.NumeroCat = request.NumeroCat;
        acidente.MetodologiaInvestigacao = request.MetodologiaInvestigacao;
        acidente.Causas = request.Causas;

        await _db.SaveChangesAsync(ct);
    }
}
