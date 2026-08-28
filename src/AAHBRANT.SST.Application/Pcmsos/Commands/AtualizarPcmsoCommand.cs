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
        var documento = await _db.DocumentosGestao.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"PCMSO {request.Id} não encontrado.");

        var detalhe = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.DocumentoGestaoId == request.Id, ct)
            ?? throw new KeyNotFoundException($"Detalhe de PCMSO {request.Id} não encontrado.");

        documento.Nome = request.Nome;
        documento.Versao = request.Versao;
        documento.Validade = request.Validade;
        documento.DataEmissao = request.DataEmissao;
        documento.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        documento.ObraId = request.ObraId;
        documento.SetorId = request.SetorId;
        documento.Arquivo = request.Arquivo;

        detalhe.MedicoResponsavelNome = request.MedicoResponsavelNome;
        detalhe.MedicoResponsavelCrm = request.MedicoResponsavelCrm;
        detalhe.FuncoesContempladas = request.FuncoesContempladas;
        detalhe.RiscosConsiderados = request.RiscosConsiderados;
        detalhe.ExamesPrevistos = request.ExamesPrevistos;
        detalhe.Periodicidades = request.Periodicidades;
        detalhe.UnidadesObrasAbrangidas = request.UnidadesObrasAbrangidas;

        await _db.SaveChangesAsync(ct);
    }
}
