using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Riscos.Medidas;

// Responsável de SST confirma que revisou probabilidade, severidade e consequência do risco. Quem
// confirmou vem do token (preenchido pelo controller), nunca do corpo.
public record ConfirmarAvaliacaoRiscoCommand(Guid RiscoId, Guid? UsuarioId, string? NomeUsuario) : IRequest;

public class ConfirmarAvaliacaoRiscoCommandHandler : IRequestHandler<ConfirmarAvaliacaoRiscoCommand>
{
    private readonly IAppDbContext _db;

    public ConfirmarAvaliacaoRiscoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ConfirmarAvaliacaoRiscoCommand request, CancellationToken ct)
    {
        var risco = await _db.Riscos.NoEscopoDaAtividade(_db, r => r.AtividadeId)
            .FirstOrDefaultAsync(r => r.Id == request.RiscoId, ct)
            ?? throw new KeyNotFoundException($"Risco {request.RiscoId} não encontrado.");

        risco.AvaliadoEmUtc = DateTime.UtcNow;
        risco.AvaliadoPorUsuarioId = request.UsuarioId;
        risco.AvaliadoPorNome = await NomeUsuarioMedidas.ResolverAsync(_db, request.UsuarioId, request.NomeUsuario, ct);
        await _db.SaveChangesAsync(ct);
    }
}

// Lê o texto de controles do PGR com IA e grava as medidas por fase. Só roda em risco que ainda não
// tem medidas: refazer apagaria decisões já tomadas pela SST.
public record SepararMedidasRiscoCommand(Guid RiscoId) : IRequest<int>;

public class SepararMedidasRiscoCommandHandler : IRequestHandler<SepararMedidasRiscoCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly IAnalistaMedidasPrevencao _analista;

    public SepararMedidasRiscoCommandHandler(IAppDbContext db, IAnalistaMedidasPrevencao analista)
    {
        _db = db;
        _analista = analista;
    }

    public async Task<int> Handle(SepararMedidasRiscoCommand request, CancellationToken ct)
    {
        var risco = await _db.Riscos.NoEscopoDaAtividade(_db, r => r.AtividadeId)
            .Include(r => r.Atividade)
            .Include(r => r.Perigo)
            .FirstOrDefaultAsync(r => r.Id == request.RiscoId, ct)
            ?? throw new KeyNotFoundException($"Risco {request.RiscoId} não encontrado.");

        if (await _db.MedidasPrevencao.AnyAsync(m => m.RiscoId == risco.Id, ct))
            throw new InvalidOperationException("Este risco já tem medidas. Ajuste-as uma a uma.");

        var textoPgr = string.Join("\n", new[] { risco.ControlesExistentes, risco.ControlesAdicionais }
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        // Só requisitos validados (Ativo) entram como base — "Em revisão" fica de fora.
        var requisitos = await _db.RequisitosLegais
            .Where(q => q.Status == StatusRequisitoLegal.Ativo && q.Criterios.Any(c => c.PerigoId == risco.PerigoId))
            .Select(q => q.Norma + " " + q.Artigo + " — " + q.Titulo)
            .Take(15)
            .ToListAsync(ct);

        var sugeridas = await _analista.SepararAsync(new ContextoMedidasRisco(
            risco.Atividade?.Nome ?? string.Empty,
            risco.Perigo?.Nome ?? string.Empty,
            risco.Consequencia,
            risco.NivelRisco.ToString(),
            textoPgr.Length == 0 ? null : textoPgr,
            requisitos), ct);

        var conciliadas = ConciliacaoMedidasPgr.Conciliar(textoPgr, sugeridas);
        var agora = DateTime.UtcNow;
        var ordem = 0;
        foreach (var m in conciliadas.OrderBy(m => m.Fase))
        {
            var validada = m.Status == StatusMedidaPrevencao.Validada;
            _db.MedidasPrevencao.Add(new MedidaPrevencao
            {
                RiscoId = risco.Id,
                Fase = m.Fase,
                Texto = m.Texto,
                OrigemMedida = m.Origem,
                Status = m.Status,
                Ordem = ordem++,
                ValidadoEmUtc = validada ? agora : null,
                ValidadoPorNome = validada ? "Texto do PGR" : null,
            });
        }

        await _db.SaveChangesAsync(ct);
        return conciliadas.Count;
    }
}

// Medida escrita pela própria SST: já nasce validada por quem escreveu.
public record AdicionarMedidaPrevencaoCommand(Guid RiscoId, FaseMedidaPrevencao Fase, string Texto, Guid? UsuarioId, string? NomeUsuario)
    : IRequest<Guid>;

public class AdicionarMedidaPrevencaoCommandValidator : AbstractValidator<AdicionarMedidaPrevencaoCommand>
{
    public AdicionarMedidaPrevencaoCommandValidator()
    {
        RuleFor(x => x.RiscoId).NotEmpty();
        RuleFor(x => x.Fase).IsInEnum();
        RuleFor(x => x.Texto).NotEmpty().MaximumLength(1000);
    }
}

public class AdicionarMedidaPrevencaoCommandHandler : IRequestHandler<AdicionarMedidaPrevencaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public AdicionarMedidaPrevencaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AdicionarMedidaPrevencaoCommand request, CancellationToken ct)
    {
        if (!await _db.Riscos.NoEscopoDaAtividade(_db, r => r.AtividadeId).AnyAsync(r => r.Id == request.RiscoId, ct))
            throw new KeyNotFoundException($"Risco {request.RiscoId} não encontrado.");

        var ordem = await _db.MedidasPrevencao.Where(m => m.RiscoId == request.RiscoId)
            .Select(m => (int?)m.Ordem).MaxAsync(ct) ?? -1;

        var medida = new MedidaPrevencao
        {
            RiscoId = request.RiscoId,
            Fase = request.Fase,
            Texto = request.Texto.Trim(),
            OrigemMedida = OrigemMedidaPrevencao.Manual,
            Status = StatusMedidaPrevencao.Validada,
            Ordem = ordem + 1,
            ValidadoEmUtc = DateTime.UtcNow,
            ValidadoPorUsuarioId = request.UsuarioId,
            ValidadoPorNome = await NomeUsuarioMedidas.ResolverAsync(_db, request.UsuarioId, request.NomeUsuario, ct),
        };
        _db.MedidasPrevencao.Add(medida);
        await _db.SaveChangesAsync(ct);
        return medida.Id;
    }
}

// Aceitar (com texto opcionalmente editado) ou descartar uma medida. Descartar não apaga: a medida
// some da OS e da tela, mas fica no banco para auditoria.
public record DecidirMedidaPrevencaoCommand(Guid MedidaId, bool Aceitar, string? TextoEditado, Guid? UsuarioId, string? NomeUsuario) : IRequest;

public class DecidirMedidaPrevencaoCommandValidator : AbstractValidator<DecidirMedidaPrevencaoCommand>
{
    public DecidirMedidaPrevencaoCommandValidator()
    {
        RuleFor(x => x.MedidaId).NotEmpty();
        RuleFor(x => x.TextoEditado).MaximumLength(1000);
    }
}

public class DecidirMedidaPrevencaoCommandHandler : IRequestHandler<DecidirMedidaPrevencaoCommand>
{
    private readonly IAppDbContext _db;

    public DecidirMedidaPrevencaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DecidirMedidaPrevencaoCommand request, CancellationToken ct)
    {
        var medida = await _db.MedidasPrevencao.FirstOrDefaultAsync(m => m.Id == request.MedidaId, ct)
            ?? throw new KeyNotFoundException($"Medida {request.MedidaId} não encontrada.");
        if (!await _db.Riscos.NoEscopoDaAtividade(_db, r => r.AtividadeId).AnyAsync(r => r.Id == medida.RiscoId, ct))
            throw new KeyNotFoundException($"Medida {request.MedidaId} não encontrada.");

        if (request.Aceitar && !string.IsNullOrWhiteSpace(request.TextoEditado))
            medida.Texto = request.TextoEditado.Trim();

        medida.Status = request.Aceitar ? StatusMedidaPrevencao.Validada : StatusMedidaPrevencao.Descartada;
        medida.ValidadoEmUtc = DateTime.UtcNow;
        medida.ValidadoPorUsuarioId = request.UsuarioId;
        medida.ValidadoPorNome = await NomeUsuarioMedidas.ResolverAsync(_db, request.UsuarioId, request.NomeUsuario, ct);
        await _db.SaveChangesAsync(ct);
    }
}

internal static class NomeUsuarioMedidas
{
    public static async Task<string?> ResolverAsync(IAppDbContext db, Guid? usuarioId, string? nomeToken, CancellationToken ct)
    {
        var nome = usuarioId is { } id
            ? await db.Usuarios.Where(u => u.Id == id).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;
        return nome ?? nomeToken;
    }
}
