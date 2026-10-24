using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.InstalacoesEpc.Commands;

// Registro de inspeção periódica de um EPC instalado — a decisão confirmada com o usuário foi
// guardar a última inspeção direto na linha da instalação, sem tela de agenda/histórico separada.
public record RegistrarInspecaoEpcCommand(
    Guid InstalacaoEpcId,
    DateTime DataInspecao,
    StatusInspecaoEpc Status,
    string? Observacoes) : IRequest;

public class RegistrarInspecaoEpcCommandValidator : AbstractValidator<RegistrarInspecaoEpcCommand>
{
    public RegistrarInspecaoEpcCommandValidator()
    {
        RuleFor(x => x.InstalacaoEpcId).NotEmpty();
        RuleFor(x => x.DataInspecao).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class RegistrarInspecaoEpcCommandHandler : IRequestHandler<RegistrarInspecaoEpcCommand>
{
    private readonly IAppDbContext _db;
    public RegistrarInspecaoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarInspecaoEpcCommand request, CancellationToken ct)
    {
        var instalacao = await _db.InstalacoesEpc.FirstOrDefaultAsync(x => x.Id == request.InstalacaoEpcId, ct)
            ?? throw new KeyNotFoundException("Instalação de EPC não encontrada.");

        instalacao.DataUltimaInspecao = request.DataInspecao;
        instalacao.StatusUltimaInspecao = request.Status;
        instalacao.ObservacoesInspecao = request.Observacoes;

        await _db.SaveChangesAsync(ct);
    }
}
