using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record AtualizarConfiguracaoAlojamentoCommand(int DiasParaInspecaoAtrasada) : IRequest;

public class AtualizarConfiguracaoAlojamentoCommandValidator : AbstractValidator<AtualizarConfiguracaoAlojamentoCommand>
{
    public AtualizarConfiguracaoAlojamentoCommandValidator()
    {
        RuleFor(x => x.DiasParaInspecaoAtrasada).GreaterThan(0).LessThanOrEqualTo(365);
    }
}

public class AtualizarConfiguracaoAlojamentoCommandHandler : IRequestHandler<AtualizarConfiguracaoAlojamentoCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarConfiguracaoAlojamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarConfiguracaoAlojamentoCommand request, CancellationToken ct)
    {
        var config = await _db.ConfiguracoesAlojamento.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Configuração de Alojamento não foi seedada.");
        config.DiasParaInspecaoAtrasada = request.DiasParaInspecaoAtrasada;
        await _db.SaveChangesAsync(ct);
    }
}
