using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

public record CriarAcidenteCommand(
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
    string? Causas,
    GravidadeAcidente Gravidade,
    int? DiasDebitadosInformados) : IRequest<Guid>;

public class CriarAcidenteCommandValidator : AbstractValidator<CriarAcidenteCommand>
{
    public CriarAcidenteCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Lesao).MaximumLength(500);
        RuleFor(x => x.Consequencia).MaximumLength(500);
        RuleFor(x => x.Atendimento).MaximumLength(500);
        RuleFor(x => x.NumeroCat).MaximumLength(50);
        RuleFor(x => x.Causas).MaximumLength(2000);
        RuleFor(x => x.DiasAfastamento).GreaterThanOrEqualTo(0).When(x => x.DiasAfastamento.HasValue);
        RuleFor(x => x.DiasDebitadosInformados)
            .NotNull().WithMessage("Informe os Dias Debitados consultando o Quadro III da NBR 14280.")
            .GreaterThan(0)
            .When(x => x.Gravidade == GravidadeAcidente.IncapacidadePermanenteParcial);
    }
}

public class CriarAcidenteCommandHandler : IRequestHandler<CriarAcidenteCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAcidenteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAcidenteCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.TrabalhadorId.HasValue &&
            !await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct))
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        if (request.AtividadeId.HasValue &&
            !await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct))
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        var acidente = new Acidente
        {
            Tipo = request.Tipo,
            ObraId = request.ObraId,
            TrabalhadorId = request.TrabalhadorId,
            AtividadeId = request.AtividadeId,
            Local = request.Local,
            Data = request.Data,
            Hora = request.Hora,
            Descricao = request.Descricao,
            Lesao = request.Lesao,
            Consequencia = request.Consequencia,
            Atendimento = request.Atendimento,
            HouveAfastamento = request.HouveAfastamento,
            DiasAfastamento = request.DiasAfastamento,
            NumeroCat = request.NumeroCat,
            MetodologiaInvestigacao = request.MetodologiaInvestigacao,
            Causas = request.Causas,
            Gravidade = request.Gravidade,
            DiasDebitados = TabelaDiasDebitados.Calcular(request.Gravidade, request.DiasDebitadosInformados),
        };

        _db.Acidentes.Add(acidente);
        await _db.SaveChangesAsync(ct);
        return acidente.Id;
    }
}
