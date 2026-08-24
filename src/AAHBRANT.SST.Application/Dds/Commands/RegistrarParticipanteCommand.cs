using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record RegistrarParticipanteCommand(
    Guid DdsId,
    Guid TrabalhadorId,
    TipoFotoParticipante FotoTipo,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest<Guid>;

public class RegistrarParticipanteCommandValidator : AbstractValidator<RegistrarParticipanteCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public RegistrarParticipanteCommandValidator()
    {
        RuleFor(x => x.DdsId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto (da pessoa ou do documento assinado) é obrigatória para registrar a presença.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class RegistrarParticipanteCommandHandler : IRequestHandler<RegistrarParticipanteCommand, Guid>
{
    private readonly IAppDbContext _db;

    public RegistrarParticipanteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(RegistrarParticipanteCommand request, CancellationToken ct)
    {
        var ddsExiste = await _db.Dds.AnyAsync(d => d.Id == request.DdsId, ct);
        if (!ddsExiste)
            throw new KeyNotFoundException($"DDS {request.DdsId} não encontrado.");

        var trabalhadorExiste = await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct);
        if (!trabalhadorExiste)
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        var jaParticipa = await _db.DdsParticipantes
            .AnyAsync(p => p.DdsId == request.DdsId && p.TrabalhadorId == request.TrabalhadorId, ct);
        if (jaParticipa)
            throw new InvalidOperationException("Este trabalhador já está registrado como participante deste DDS.");

        var participante = new Domain.Entidades.DdsParticipante
        {
            DdsId = request.DdsId,
            TrabalhadorId = request.TrabalhadorId,
            FotoTipo = request.FotoTipo,
            FotoConteudo = request.FotoConteudo,
            FotoContentType = request.FotoContentType,
        };
        _db.DdsParticipantes.Add(participante);
        await _db.SaveChangesAsync(ct);
        return participante.Id;
    }
}
