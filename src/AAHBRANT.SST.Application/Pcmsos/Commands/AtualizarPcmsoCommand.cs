using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record AtualizarPcmsoCommand(
    Guid Id,
    string Nome,
    string? Versao,
    DateTime? Validade,
    DateTime DataEmissao,
    Guid? ResponsavelUsuarioId,
    Guid? ObraId,
    Guid? SetorId,
    string? Arquivo,
    string? MedicoResponsavelNome,
    string? MedicoResponsavelCrm,
    string? FuncoesContempladas,
    string? RiscosConsiderados,
    string? ExamesPrevistos,
    string? Periodicidades,
    string? UnidadesObrasAbrangidas) : IRequest;

public class AtualizarPcmsoCommandValidator : AbstractValidator<AtualizarPcmsoCommand>
{
    public AtualizarPcmsoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Versao).MaximumLength(50);
        RuleFor(x => x.Arquivo).MaximumLength(500);
        RuleFor(x => x.MedicoResponsavelNome).MaximumLength(150);
        RuleFor(x => x.MedicoResponsavelCrm).MaximumLength(30);
        RuleFor(x => x.DataEmissao).NotEmpty();
    }
}

public class AtualizarPcmsoCommandHandler : IRequestHandler<AtualizarPcmsoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarPcmsoCommand request, CancellationToken ct)
    {
        var pcmso = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"PCMSO {request.Id} não encontrado.");

        pcmso.Nome = request.Nome;
        pcmso.Versao = request.Versao;
        pcmso.Validade = request.Validade;
        pcmso.DataEmissao = request.DataEmissao;
        pcmso.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        pcmso.ObraId = request.ObraId;
        pcmso.SetorId = request.SetorId;
        pcmso.Arquivo = request.Arquivo;
        pcmso.MedicoResponsavelNome = request.MedicoResponsavelNome;
        pcmso.MedicoResponsavelCrm = request.MedicoResponsavelCrm;
        pcmso.FuncoesContempladas = request.FuncoesContempladas;
        pcmso.RiscosConsiderados = request.RiscosConsiderados;
        pcmso.ExamesPrevistos = request.ExamesPrevistos;
        pcmso.Periodicidades = request.Periodicidades;
        pcmso.UnidadesObrasAbrangidas = request.UnidadesObrasAbrangidas;

        await _db.SaveChangesAsync(ct);
    }
}
