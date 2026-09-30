using AAHBRANT.SST.Application.Aprs;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Catalogo;

// "Modelo" de APR/PT = a Atividade da obra com os Riscos do PGR já avaliados. Gerar cria um
// documento em ELABORAÇÃO já preenchido a partir deles; quem emite revisa e completa antes de
// aprovar/liberar. Nada aqui aprova ou libera.
public record GerarAprDaAtividadeCommand(
    Guid AtividadeId, string Local, Guid? EquipeId, DateTime Data, DateTime? Validade) : IRequest<Guid>;

public record GerarPtDaAtividadeCommand(
    Guid AtividadeId, string Local, Guid? EquipeId, DateTime Data, DateTime? Validade) : IRequest<Guid>;

public class GerarAprDaAtividadeCommandValidator : AbstractValidator<GerarAprDaAtividadeCommand>
{
    public GerarAprDaAtividadeCommandValidator()
    {
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
    }
}

public class GerarPtDaAtividadeCommandValidator : AbstractValidator<GerarPtDaAtividadeCommand>
{
    public GerarPtDaAtividadeCommandValidator()
    {
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
    }
}

internal static class ModeloDaAtividade
{
    internal record RiscoModelo(string Perigo, string? Fonte, string? Consequencia, string? Controles, NivelRisco Nivel, int P, int S);

    public static string? Cortar(string? texto, int max)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        return t.Length <= max ? t : t[..max];
    }

    public static async Task<(Atividade atividade, List<RiscoModelo> riscos, string? pgrNome)> CarregarAsync(
        IAppDbContext db, Guid atividadeId, CancellationToken ct)
    {
        var atividade = await db.Atividades.AsNoTracking().FirstOrDefaultAsync(a => a.Id == atividadeId, ct)
            ?? throw new KeyNotFoundException($"Atividade {atividadeId} não encontrada.");

        var riscos = await db.Riscos.AsNoTracking().Where(r => r.AtividadeId == atividadeId).ToListAsync(ct);
        if (riscos.Count == 0)
            throw new InvalidOperationException(
                "Esta atividade ainda não tem riscos avaliados no PGR. Cadastre os riscos antes de gerar o documento.");

        var perigoIds = riscos.Select(r => r.PerigoId).Distinct().ToList();
        var perigos = await db.Perigos.AsNoTracking().Where(p => perigoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var pgrNome = await db.Pgrs.AsNoTracking()
            .Where(p => p.ObraId == atividade.ObraId)
            .OrderByDescending(p => p.DataElaboracao)
            .Select(p => p.Nome)
            .FirstOrDefaultAsync(ct);

        var modelo = riscos
            .OrderByDescending(r => r.NivelRisco)
            .Select(r =>
            {
                perigos.TryGetValue(r.PerigoId, out var perigo);
                var controles = string.Join(" | ", new[] { r.ControlesExistentes, r.ControlesAdicionais }
                    .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()));
                return new RiscoModelo(
                    perigo?.Nome ?? "Perigo não identificado", perigo?.Fonte, r.Consequencia,
                    controles.Length == 0 ? null : controles, r.NivelRisco,
                    Math.Clamp(r.Probabilidade, 1, 5), Math.Clamp(r.Severidade, 1, 5));
            })
            .ToList();

        return (atividade, modelo, pgrNome);
    }
}

public class GerarAprDaAtividadeCommandHandler : IRequestHandler<GerarAprDaAtividadeCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public GerarAprDaAtividadeCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<Guid> Handle(GerarAprDaAtividadeCommand request, CancellationToken ct)
    {
        var (atividade, riscos, pgrNome) = await ModeloDaAtividade.CarregarAsync(_db, request.AtividadeId, ct);

        var apr = new Apr
        {
            NumeroApr = await _geradorNumero.GerarAsync("APR", ct),
            AtividadeId = atividade.Id,
            Local = request.Local,
            PgrReferencia = ModeloDaAtividade.Cortar(pgrNome, 300),
            EquipeId = request.EquipeId,
            Data = request.Data,
            Validade = request.Validade,
        };

        if (request.EquipeId.HasValue)
        {
            var membros = await _db.Trabalhadores.AsNoTracking()
                .Where(t => t.EquipeId == request.EquipeId).Select(t => t.Id).ToListAsync(ct);
            foreach (var id in membros) apr.Responsaveis.Add(new AprResponsavel { TrabalhadorId = id });
        }

        var ordem = 1;
        foreach (var r in riscos)
        {
            var etapa = new AprEtapa { Ordem = ordem++, Descricao = ModeloDaAtividade.Cortar(r.Perigo, 300)! };
            // Risco residual começa igual ao inicial: quem revisa reavalia depois das medidas.
            etapa.Riscos.Add(new AprEtapaRisco
            {
                PerigoEventoPerigoso = ModeloDaAtividade.Cortar(r.Perigo, 300)!,
                FonteCircunstancia = ModeloDaAtividade.Cortar(r.Fonte, 500),
                PossiveisLesoes = ModeloDaAtividade.Cortar(r.Consequencia, 500),
                ProbabilidadeInicial = r.P,
                SeveridadeInicial = r.S,
                NivelRiscoInicial = AprNivelRiscoCalculator.Calcular(r.P, r.S),
                MedidasPrevencao = ModeloDaAtividade.Cortar(r.Controles, 1000),
                ProbabilidadeResidual = r.P,
                SeveridadeResidual = r.S,
                NivelRiscoResidual = AprNivelRiscoCalculator.Calcular(r.P, r.S),
            });
            apr.Etapas.Add(etapa);
        }

        _db.Aprs.Add(apr);
        await _db.SaveChangesAsync(ct);
        return apr.Id;
    }
}

public class GerarPtDaAtividadeCommandHandler : IRequestHandler<GerarPtDaAtividadeCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public GerarPtDaAtividadeCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<Guid> Handle(GerarPtDaAtividadeCommand request, CancellationToken ct)
    {
        var (atividade, riscos, _) = await ModeloDaAtividade.CarregarAsync(_db, request.AtividadeId, ct);

        var pt = new PermissaoTrabalho
        {
            NumeroPt = await _geradorNumero.GerarAsync("PT", ct),
            AtividadeId = atividade.Id,
            DescricaoAtividade = ModeloDaAtividade.Cortar(atividade.Nome, 500)!,
            Local = request.Local,
            EquipeId = request.EquipeId,
            Data = request.Data,
            Validade = request.Validade,
        };

        foreach (var item in Enum.GetValues<ItemPreRequisitoPt>())
            pt.PreRequisitos.Add(new PermissaoTrabalhoPreRequisito { Item = item });
        foreach (var item in Enum.GetValues<ItemVerificacaoPt>())
            pt.Verificacoes.Add(new PermissaoTrabalhoVerificacao { Item = item });

        if (request.EquipeId.HasValue)
        {
            var membros = await _db.Trabalhadores.AsNoTracking()
                .Where(t => t.EquipeId == request.EquipeId).Select(t => t.Id).ToListAsync(ct);
            foreach (var id in membros) pt.Responsaveis.Add(new PermissaoTrabalhoResponsavel { TrabalhadorId = id });
        }

        // Riscos críticos da PT = riscos do PGR de nível Alto ou Crítico.
        foreach (var r in riscos.Where(r => r.Nivel >= NivelRisco.Alto))
            pt.RiscosCriticos.Add(new PermissaoTrabalhoRiscoCritico
            {
                RiscoCondicao = ModeloDaAtividade.Cortar(r.Perigo, 300)!,
                ControleComplementar = ModeloDaAtividade.Cortar(r.Controles, 500),
            });

        _db.PermissoesTrabalho.Add(pt);
        await _db.SaveChangesAsync(ct);
        return pt.Id;
    }
}
