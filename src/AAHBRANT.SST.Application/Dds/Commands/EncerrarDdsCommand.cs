using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record EncerrarDdsCommand(Guid Id) : IRequest;

public class EncerrarDdsCommandValidator : AbstractValidator<EncerrarDdsCommand>
{
    public EncerrarDdsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarDdsCommandHandler : IRequestHandler<EncerrarDdsCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarDdsCommandHandler(IAppDbContext db) => _db = db;

    private const int TotalFotosObrigatorias = 3;

    public async Task Handle(EncerrarDdsCommand request, CancellationToken ct)
    {
        var dds = await _db.Dds.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"DDS {request.Id} não encontrado.");

        // Pedido do usuário (31/08): "obrigatoriedade de 3 fotos por registro de DDS para
        // liberação do encerramento" — evidência do registro em si, distinta da foto de presença
        // por participante (que já é obrigatória em RegistrarParticipanteCommand).
        var totalFotos = await _db.DdsFotosEvidencia.CountAsync(f => f.DdsId == dds.Id && f.Ativo, ct);
        if (totalFotos < TotalFotosObrigatorias)
            throw new InvalidOperationException(
                $"São necessárias {TotalFotosObrigatorias} fotos de evidência para encerrar o DDS do dia (faltam {TotalFotosObrigatorias - totalFotos}).");

        dds.Status = StatusDds.Concluido;
        await _db.SaveChangesAsync(ct);
    }
}
