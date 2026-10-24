using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

public record AnexarFotoAcidenteCommand(Guid AcidenteId, int Ordem, byte[] Conteudo, string ContentType, string? Metadados) : IRequest<Guid>;
public class AnexarFotoAcidenteCommandValidator : AbstractValidator<AnexarFotoAcidenteCommand>
{
    public AnexarFotoAcidenteCommandValidator()
    {
        RuleFor(x => x.AcidenteId).NotEmpty();
        RuleFor(x => x.Ordem).InclusiveBetween(1, 3);
        RuleFor(x => x.ContentType).Must(t => t is "image/jpeg" or "image/png");
        RuleFor(x => x.Conteudo).NotEmpty().Must(b => b.Length <= 5 * 1024 * 1024)
            .Must((x, b) => ValidadorAssinaturaArquivo.AssinaturaConfere(b, x.ContentType));
    }
}

public class AnexarFotoAcidenteCommandHandler : IRequestHandler<AnexarFotoAcidenteCommand, Guid>
{
    private readonly IAppDbContext _db;
    public AnexarFotoAcidenteCommandHandler(IAppDbContext db) => _db = db;
    public async Task<Guid> Handle(AnexarFotoAcidenteCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.AcidenteId, ct)
            ?? throw new KeyNotFoundException("Acidente não encontrado.");
        if (acidente.Status == StatusAcidente.Concluido) throw new InvalidOperationException("O acidente já foi finalizado.");
        var dados = await DadosCapturaFoto.PrepararAsync(request.Metadados, acidente.ObraId, _db, ct);
        var anterior = await _db.AcidentesFotos.FirstOrDefaultAsync(f => f.AcidenteId == acidente.Id && f.Ordem == request.Ordem, ct);
        if (anterior is not null) anterior.Ativo = false;
        var foto = new AcidenteFoto { AcidenteId = acidente.Id, Ordem = request.Ordem, FotoConteudo = request.Conteudo, FotoContentType = request.ContentType, FotoMetadadosJson = dados };
        _db.AcidentesFotos.Add(foto);
        await _db.SaveChangesAsync(ct);
        return foto.Id;
    }
}

public record RemoverFotoAcidenteCommand(Guid FotoId) : IRequest;
public class RemoverFotoAcidenteCommandHandler : IRequestHandler<RemoverFotoAcidenteCommand>
{
    private readonly IAppDbContext _db;
    public RemoverFotoAcidenteCommandHandler(IAppDbContext db) => _db = db;
    public async Task Handle(RemoverFotoAcidenteCommand request, CancellationToken ct)
    {
        var foto = await _db.AcidentesFotos.Include(f => f.Acidente).FirstOrDefaultAsync(f => f.Id == request.FotoId, ct)
            ?? throw new KeyNotFoundException("Foto não encontrada.");
        if (foto.Acidente!.Status == StatusAcidente.Concluido) throw new InvalidOperationException("O acidente já foi finalizado.");
        foto.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
