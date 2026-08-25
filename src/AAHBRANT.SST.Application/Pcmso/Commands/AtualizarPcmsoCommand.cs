using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmso.Commands;

public record AtualizarPcmsoCommand(
    Guid Id,
    Guid ObraId,
    string Nome,
    string? Objetivo,
    string MedicoCoordenadorNome,
    string? MedicoCoordenadorCrm,
    Guid? MedicoCoordenadorUsuarioId,
    DateTime DataElaboracao,
    DateTime? DataVigenciaInicio,
    DateTime? DataVigenciaFim,
    StatusPcmso Status) : IRequest;

public class AtualizarPcmsoCommandValidator : AbstractValidator<AtualizarPcmsoCommand>
{
    public AtualizarPcmsoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MedicoCoordenadorNome).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarPcmsoCommandHandler : IRequestHandler<AtualizarPcmsoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarPcmsoCommand request, CancellationToken ct)
    {
        var pcmso = await _db.Pcmsos.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"PCMSO {request.Id} não encontrado.");

        pcmso.ObraId = request.ObraId;
        pcmso.Nome = request.Nome;
        pcmso.Objetivo = request.Objetivo;
        pcmso.MedicoCoordenadorNome = request.MedicoCoordenadorNome;
        pcmso.MedicoCoordenadorCrm = request.MedicoCoordenadorCrm;
        pcmso.MedicoCoordenadorUsuarioId = request.MedicoCoordenadorUsuarioId;
        pcmso.DataElaboracao = request.DataElaboracao;
        pcmso.DataVigenciaInicio = request.DataVigenciaInicio;
        pcmso.DataVigenciaFim = request.DataVigenciaFim;
        pcmso.Status = request.Status;

        await _db.SaveChangesAsync(ct);
    }
}
