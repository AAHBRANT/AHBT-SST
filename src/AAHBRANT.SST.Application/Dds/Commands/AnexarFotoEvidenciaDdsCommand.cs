using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Evidência fotográfica do registro diário (31/08) — até 3 fotos por Dds, obrigatórias para
// encerrar (ver EncerrarDdsCommand). Distinta da foto de presença por participante.
public record AnexarFotoEvidenciaDdsCommand(
    Guid DdsId,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest<Guid>;

public class AnexarFotoEvidenciaDdsCommandValidator : AbstractValidator<AnexarFotoEvidenciaDdsCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoEvidenciaDdsCommandValidator()
    {
        RuleFor(x => x.DdsId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoEvidenciaDdsCommandHandler : IRequestHandler<AnexarFotoEvidenciaDdsCommand, Guid>
{
    private const int TotalFotosMaximo = 3;
    private readonly IAppDbContext _db;

    public AnexarFotoEvidenciaDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AnexarFotoEvidenciaDdsCommand request, CancellationToken ct)
    {
        var ddsExiste = await _db.Dds.AnyAsync(d => d.Id == request.DdsId, ct);
        if (!ddsExiste)
            throw new KeyNotFoundException($"DDS {request.DdsId} não encontrado.");

        var totalAtual = await _db.DdsFotosEvidencia.CountAsync(f => f.DdsId == request.DdsId && f.Ativo, ct);
        if (totalAtual >= TotalFotosMaximo)
            throw new InvalidOperationException($"Este DDS já tem as {TotalFotosMaximo} fotos de evidência obrigatórias.");

        var foto = new DdsFotoEvidencia
        {
            DdsId = request.DdsId,
            Ordem = totalAtual + 1,
            FotoConteudo = request.FotoConteudo,
            FotoContentType = request.FotoContentType,
        };
        _db.DdsFotosEvidencia.Add(foto);
        await _db.SaveChangesAsync(ct);
        return foto.Id;
    }
}
