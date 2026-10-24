using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Temas;

// PeriodicidadeInspecaoDias nulo no cadastro herda a periodicidade padrão do tema.
public record CriarItemInspecionavelCommand(
    Guid TemaInspecaoId, Guid ObraId, string Identificacao, string? Descricao,
    int? PeriodicidadeInspecaoDias = null, Guid? ResponsavelInspecaoUsuarioId = null) : IRequest<Guid>;

public class CriarItemInspecionavelCommandValidator : AbstractValidator<CriarItemInspecionavelCommand>
{
    public CriarItemInspecionavelCommandValidator()
    {
        RuleFor(x => x.TemaInspecaoId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty().WithMessage("Selecione a obra do item.");
        RuleFor(x => x.Identificacao).NotEmpty().WithMessage("Informe como o item é identificado (ex.: Andaime A-03).").MaximumLength(150);
        RuleFor(x => x.Descricao).MaximumLength(500);
        RuleFor(x => x.PeriodicidadeInspecaoDias)
            .InclusiveBetween(PeriodicidadeInspecao.MinimoDias, PeriodicidadeInspecao.MaximoDias)
            .When(x => x.PeriodicidadeInspecaoDias.HasValue)
            .WithMessage($"A periodicidade deve ficar entre {PeriodicidadeInspecao.MinimoDias} e {PeriodicidadeInspecao.MaximoDias} dias.");
    }
}

public class CriarItemInspecionavelCommandHandler : IRequestHandler<CriarItemInspecionavelCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarItemInspecionavelCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarItemInspecionavelCommand request, CancellationToken ct)
    {
        var tema = await _db.TemasInspecao.FirstOrDefaultAsync(t => t.Id == request.TemaInspecaoId, ct)
            ?? throw new KeyNotFoundException($"Tema {request.TemaInspecaoId} não encontrado.");
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var identificacao = request.Identificacao.Trim();
        var existentes = await _db.ItensInspecionaveis
            .Where(i => i.TemaInspecaoId == tema.Id && i.ObraId == request.ObraId)
            .Select(i => i.Identificacao)
            .ToListAsync(ct);
        if (existentes.Any(n => string.Equals(n, identificacao, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Já existe \"{identificacao}\" em {tema.Nome} nesta obra.");

        var dias = request.PeriodicidadeInspecaoDias ?? tema.PeriodicidadeDiasPadrao;
        if (dias.HasValue && request.ResponsavelInspecaoUsuarioId is null)
            throw new InvalidOperationException("Designe quem será avisado da inspeção periódica.");
        if (request.ResponsavelInspecaoUsuarioId is { } responsavelId && !await _db.Usuarios.AnyAsync(u => u.Id == responsavelId, ct))
            throw new KeyNotFoundException($"Usuário {responsavelId} não encontrado.");

        var item = new ItemInspecionavel
        {
            TemaInspecaoId = tema.Id,
            ObraId = request.ObraId,
            Identificacao = identificacao,
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            PeriodicidadeInspecaoDias = dias,
            ResponsavelInspecaoUsuarioId = dias.HasValue ? request.ResponsavelInspecaoUsuarioId : null,
        };
        _db.ItensInspecionaveis.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}

// Edição da periodicidade e do responsável da inspeção de um item (e da descrição).
public record AtualizarItemInspecionavelCommand(
    Guid Id, string? Descricao, int? PeriodicidadeInspecaoDias, Guid? ResponsavelInspecaoUsuarioId) : IRequest;

public class AtualizarItemInspecionavelCommandValidator : AbstractValidator<AtualizarItemInspecionavelCommand>
{
    public AtualizarItemInspecionavelCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Descricao).MaximumLength(500);
        RuleFor(x => x.PeriodicidadeInspecaoDias)
            .InclusiveBetween(PeriodicidadeInspecao.MinimoDias, PeriodicidadeInspecao.MaximoDias)
            .When(x => x.PeriodicidadeInspecaoDias.HasValue)
            .WithMessage($"A periodicidade deve ficar entre {PeriodicidadeInspecao.MinimoDias} e {PeriodicidadeInspecao.MaximoDias} dias.");
        RuleFor(x => x.ResponsavelInspecaoUsuarioId).NotEmpty()
            .When(x => x.PeriodicidadeInspecaoDias.HasValue)
            .WithMessage("Designe quem será avisado da inspeção periódica.");
    }
}

public class AtualizarItemInspecionavelCommandHandler : IRequestHandler<AtualizarItemInspecionavelCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarItemInspecionavelCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarItemInspecionavelCommand request, CancellationToken ct)
    {
        var item = await _db.ItensInspecionaveis.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Item {request.Id} não encontrado.");
        if (request.ResponsavelInspecaoUsuarioId is { } responsavelId && !await _db.Usuarios.AnyAsync(u => u.Id == responsavelId, ct))
            throw new KeyNotFoundException($"Usuário {responsavelId} não encontrado.");

        item.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        item.PeriodicidadeInspecaoDias = request.PeriodicidadeInspecaoDias;
        item.ResponsavelInspecaoUsuarioId = request.PeriodicidadeInspecaoDias.HasValue ? request.ResponsavelInspecaoUsuarioId : null;
        await _db.SaveChangesAsync(ct);
    }
}

// Exclusão lógica (Ativo=false): o histórico de inspeções continua preservado no banco. A política
// "só Administrador" é aplicada no controller, como nos Veículos.
public record ExcluirItemInspecionavelCommand(Guid Id) : IRequest;

public class ExcluirItemInspecionavelCommandHandler : IRequestHandler<ExcluirItemInspecionavelCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirItemInspecionavelCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirItemInspecionavelCommand request, CancellationToken ct)
    {
        var item = await _db.ItensInspecionaveis.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Item {request.Id} não encontrado.");

        if (await _db.Inspecoes.AnyAsync(i => i.ItemInspecionavelId == item.Id && i.Status == StatusInspecao.EmAndamento, ct))
            throw new InvalidOperationException(
                "Não é possível excluir o item com uma inspeção em andamento. Conclua ou exclua a inspeção primeiro.");

        item.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
