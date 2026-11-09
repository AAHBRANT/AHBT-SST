using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Usado para marcar "falso alerta" (a IA errou) ou voltar a ocorrência para nova/conferida.
public record DefinirSituacaoOcorrenciaEpiCommand(Guid Id, SituacaoOcorrenciaEpi Situacao) : IRequest;

public class DefinirSituacaoOcorrenciaEpiCommandValidator : AbstractValidator<DefinirSituacaoOcorrenciaEpiCommand>
{
    public DefinirSituacaoOcorrenciaEpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Situacao).IsInEnum();
    }
}

public class DefinirSituacaoOcorrenciaEpiCommandHandler : IRequestHandler<DefinirSituacaoOcorrenciaEpiCommand>
{
    private readonly IAppDbContext _db;
    public DefinirSituacaoOcorrenciaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirSituacaoOcorrenciaEpiCommand request, CancellationToken ct)
    {
        var ocorrencia = await _db.OcorrenciasEpiCamera.FirstOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ocorrência {request.Id} não encontrada.");

        ocorrencia.Situacao = request.Situacao;
        await _db.SaveChangesAsync(ct);
    }
}
