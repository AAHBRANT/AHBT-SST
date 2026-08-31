using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

// Exclusões simples (soft-delete padrão) agrupadas num arquivo só — todas seguem o mesmo formato de
// ExcluirCatalogoEpiCommand (carrega, .Remove(), salva), sem regra de negócio adicional.

public record ExcluirDimensionamentoCipaCommand(Guid Id) : IRequest;

public class ExcluirDimensionamentoCipaCommandHandler : IRequestHandler<ExcluirDimensionamentoCipaCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirDimensionamentoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirDimensionamentoCipaCommand request, CancellationToken ct)
    {
        var entidade = await _db.DimensionamentosCipa.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Dimensionamento {request.Id} não encontrado.");
        _db.DimensionamentosCipa.Remove(entidade);
        await _db.SaveChangesAsync(ct);
    }
}

public record ExcluirProcessoEleitoralCipaCommand(Guid Id) : IRequest;

public class ExcluirProcessoEleitoralCipaCommandHandler : IRequestHandler<ExcluirProcessoEleitoralCipaCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirProcessoEleitoralCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirProcessoEleitoralCipaCommand request, CancellationToken ct)
    {
        var entidade = await _db.ProcessosEleitoraisCipa.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Processo eleitoral {request.Id} não encontrado.");
        if (entidade.Status is StatusProcessoEleitoralCipa.Apurado or StatusProcessoEleitoralCipa.Encerrado)
            throw new InvalidOperationException("Não é possível excluir um processo eleitoral já apurado.");
        _db.ProcessosEleitoraisCipa.Remove(entidade);
        await _db.SaveChangesAsync(ct);
    }
}

public record ExcluirReuniaoCipaCommand(Guid Id) : IRequest;

public class ExcluirReuniaoCipaCommandHandler : IRequestHandler<ExcluirReuniaoCipaCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirReuniaoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirReuniaoCipaCommand request, CancellationToken ct)
    {
        var entidade = await _db.ReunioesCipa.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Reunião {request.Id} não encontrada.");
        _db.ReunioesCipa.Remove(entidade);
        await _db.SaveChangesAsync(ct);
    }
}

public record ExcluirInspecaoCipaCommand(Guid Id) : IRequest;

public class ExcluirInspecaoCipaCommandHandler : IRequestHandler<ExcluirInspecaoCipaCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirInspecaoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirInspecaoCipaCommand request, CancellationToken ct)
    {
        var entidade = await _db.InspecoesCipa.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Inspeção {request.Id} não encontrada.");
        _db.InspecoesCipa.Remove(entidade);
        await _db.SaveChangesAsync(ct);
    }
}

public record ExcluirEventoSipatCommand(Guid Id) : IRequest;

public class ExcluirEventoSipatCommandHandler : IRequestHandler<ExcluirEventoSipatCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirEventoSipatCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirEventoSipatCommand request, CancellationToken ct)
    {
        var entidade = await _db.EventosSipat.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Evento SIPAT {request.Id} não encontrado.");
        _db.EventosSipat.Remove(entidade);
        await _db.SaveChangesAsync(ct);
    }
}
