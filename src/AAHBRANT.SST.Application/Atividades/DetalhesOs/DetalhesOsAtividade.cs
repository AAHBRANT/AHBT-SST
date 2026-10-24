using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.Riscos.Medidas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Atividades.DetalhesOs;

// Detalhes de uma atividade do PGR para a Ordem de Serviço (20/10/2026): condições de execução,
// ferramentas, EPCs, procedimentos, treinamentos exigidos (lidos da Matriz de Treinamento das funções
// do GHE) e os itens "interromper quando" / "não executar sem" / autorizações.

public class DetalhesOsAtividadeDto
{
    public Guid AtividadeId { get; set; }
    public string AtividadeNome { get; set; } = string.Empty;
    public string? Condicoes { get; set; }
    // true quando a atividade ainda não tem condições próprias e o texto veio do GHE do PGR.
    public bool CondicoesDoPgr { get; set; }
    public List<string> Ferramentas { get; set; } = new();
    public List<ItemCatalogoDto> Epcs { get; set; } = new();
    public List<DocumentoVinculadoDto> Documentos { get; set; } = new();
    public List<TreinamentoExigidoDto> Treinamentos { get; set; } = new();
    public List<ItemAtividadeOsDto> Itens { get; set; } = new();
}

public record ItemCatalogoDto(Guid Id, string Nome);

public record DocumentoVinculadoDto(Guid Id, TipoDocumentoSst Tipo, string? Codigo, string Titulo, string? Revisao, SituacaoDocumentoSst? Situacao);

public record TreinamentoExigidoDto(string Curso, string? Norma, string Funcao);

public record ItemAtividadeOsDto(Guid Id, TipoItemAtividadeOs Tipo, string Texto, string? Requisito, OrigemMedidaPrevencao Origem,
    StatusMedidaPrevencao Status, string? ValidadoPorNome, DateTime? ValidadoEmUtc);

public record ObterDetalhesOsAtividadeQuery(Guid AtividadeId) : IRequest<DetalhesOsAtividadeDto?>;

public class ObterDetalhesOsAtividadeQueryHandler : IRequestHandler<ObterDetalhesOsAtividadeQuery, DetalhesOsAtividadeDto?>
{
    private readonly IAppDbContext _db;

    public ObterDetalhesOsAtividadeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DetalhesOsAtividadeDto?> Handle(ObterDetalhesOsAtividadeQuery request, CancellationToken ct)
    {
        var a = await _db.Atividades.NoEscopoDaAtividade(_db, x => x.Id)
            .Include(x => x.Ghe)
            .Include(x => x.Epcs).ThenInclude(e => e.CatalogoEpc)
            .Include(x => x.Documentos).ThenInclude(d => d.MaterialApoio)
            .Include(x => x.ItensOs)
            .FirstOrDefaultAsync(x => x.Id == request.AtividadeId, ct);
        if (a is null) return null;

        var condicoesGhe = a.Ghe is null ? null : string.Join("\n", new[]
        {
            string.IsNullOrWhiteSpace(a.Ghe.DescricaoAmbiente) ? null : $"Ambiente: {a.Ghe.DescricaoAmbiente!.Trim()}",
            string.IsNullOrWhiteSpace(a.Ghe.JornadaTrabalho) ? null : $"Jornada: {a.Ghe.JornadaTrabalho!.Trim()}",
        }.Where(t => t is not null));

        var treinamentos = a.GheId is null
            ? new List<TreinamentoExigidoDto>()
            : await (from gf in _db.GheFuncoes
                     where gf.GheId == a.GheId
                     join m in _db.MatrizTreinamentoFuncoes on gf.FuncaoId equals m.FuncaoId
                     select new TreinamentoExigidoDto(m.CursoTreinamento!.Nome, m.CursoTreinamento.NormaReferencia, gf.Funcao!.Nome))
                .ToListAsync(ct);

        return new DetalhesOsAtividadeDto
        {
            AtividadeId = a.Id,
            AtividadeNome = a.Nome,
            Condicoes = a.CondicoesExecucao ?? (string.IsNullOrWhiteSpace(condicoesGhe) ? null : condicoesGhe),
            CondicoesDoPgr = a.CondicoesExecucao is null && !string.IsNullOrWhiteSpace(condicoesGhe),
            Ferramentas = DetalhesOsTexto.Linhas(a.Ferramentas),
            Epcs = a.Epcs.Where(e => e.CatalogoEpc is not null)
                .Select(e => new ItemCatalogoDto(e.CatalogoEpcId, e.CatalogoEpc!.Nome)).OrderBy(e => e.Nome).ToList(),
            Documentos = a.Documentos.Where(d => d.MaterialApoio is not null)
                .Select(d => new DocumentoVinculadoDto(d.MaterialApoioId, d.MaterialApoio!.Tipo, d.MaterialApoio.Codigo,
                    d.MaterialApoio.Nome, d.MaterialApoio.Revisao, d.MaterialApoio.Situacao))
                .OrderBy(d => d.Codigo).ToList(),
            Treinamentos = treinamentos.OrderBy(t => t.Curso).ToList(),
            Itens = a.ItensOs.Where(i => i.Status != StatusMedidaPrevencao.Descartada)
                .OrderBy(i => i.Tipo).ThenBy(i => i.CreatedAtUtc)
                .Select(i => new ItemAtividadeOsDto(i.Id, i.Tipo, i.Texto, i.Requisito, i.OrigemItem, i.Status, i.ValidadoPorNome, i.ValidadoEmUtc))
                .ToList(),
        };
    }
}

// Salva condições, ferramentas, EPCs e procedimentos. Só procedimento/IT vigente pode ser vinculado.
public record SalvarDetalhesOsAtividadeCommand(Guid AtividadeId, string? Condicoes, List<string> Ferramentas, List<Guid> EpcIds, List<Guid> DocumentoIds) : IRequest;

public class SalvarDetalhesOsAtividadeCommandValidator : AbstractValidator<SalvarDetalhesOsAtividadeCommand>
{
    public SalvarDetalhesOsAtividadeCommandValidator()
    {
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.Condicoes).MaximumLength(2000);
        RuleFor(x => string.Join("\n", x.Ferramentas)).MaximumLength(2000).WithName("Ferramentas");
    }
}

public class SalvarDetalhesOsAtividadeCommandHandler : IRequestHandler<SalvarDetalhesOsAtividadeCommand>
{
    private readonly IAppDbContext _db;

    public SalvarDetalhesOsAtividadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(SalvarDetalhesOsAtividadeCommand request, CancellationToken ct)
    {
        var a = await _db.Atividades.NoEscopoDaAtividade(_db, x => x.Id)
            .Include(x => x.Epcs).Include(x => x.Documentos)
            .FirstOrDefaultAsync(x => x.Id == request.AtividadeId, ct)
            ?? throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        var docIds = request.DocumentoIds.Distinct().ToList();
        var validos = await _db.MateriaisApoio
            .Where(m => docIds.Contains(m.Id)
                && (m.Tipo == TipoDocumentoSst.Procedimento || m.Tipo == TipoDocumentoSst.InstrucaoTrabalho)
                && m.Situacao == SituacaoDocumentoSst.Vigente)
            .Select(m => m.Id).ToListAsync(ct);
        if (validos.Count != docIds.Count)
            throw new InvalidOperationException("Só procedimento ou instrução de trabalho vigente pode ser vinculado à atividade.");

        var epcIds = request.EpcIds.Distinct().ToList();
        if (await _db.CatalogoEpcs.CountAsync(e => epcIds.Contains(e.Id), ct) != epcIds.Count)
            throw new KeyNotFoundException("EPC não encontrado no catálogo.");

        a.CondicoesExecucao = string.IsNullOrWhiteSpace(request.Condicoes) ? null : request.Condicoes.Trim();
        var ferramentas = request.Ferramentas.Select(f => f.Trim()).Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
        a.Ferramentas = string.Join("\n", ferramentas) is { Length: > 0 } t ? t : null;

        foreach (var e in a.Epcs.Where(e => !epcIds.Contains(e.CatalogoEpcId)).ToList()) _db.AtividadeEpcs.Remove(e);
        foreach (var id in epcIds.Where(id => a.Epcs.All(e => e.CatalogoEpcId != id)))
            _db.AtividadeEpcs.Add(new AtividadeEpc { AtividadeId = a.Id, CatalogoEpcId = id });

        foreach (var d in a.Documentos.Where(d => !docIds.Contains(d.MaterialApoioId)).ToList()) _db.AtividadeDocumentos.Remove(d);
        foreach (var id in docIds.Where(id => a.Documentos.All(d => d.MaterialApoioId != id)))
            _db.AtividadeDocumentos.Add(new AtividadeDocumento { AtividadeId = a.Id, MaterialApoioId = id });

        await _db.SaveChangesAsync(ct);
    }
}

// Item escrito pela SST: já nasce validado por quem escreveu.
public record AdicionarItemAtividadeOsCommand(Guid AtividadeId, TipoItemAtividadeOs Tipo, string Texto, string? Requisito, Guid? UsuarioId, string? NomeUsuario) : IRequest<Guid>;

public class AdicionarItemAtividadeOsCommandValidator : AbstractValidator<AdicionarItemAtividadeOsCommand>
{
    public AdicionarItemAtividadeOsCommandValidator()
    {
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Texto).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Requisito).MaximumLength(300);
        RuleFor(x => x.Requisito).NotEmpty().When(x => x.Tipo == TipoItemAtividadeOs.Restricao)
            .WithMessage("Informe o que é exigido para poder executar.");
    }
}

public class AdicionarItemAtividadeOsCommandHandler : IRequestHandler<AdicionarItemAtividadeOsCommand, Guid>
{
    private readonly IAppDbContext _db;

    public AdicionarItemAtividadeOsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AdicionarItemAtividadeOsCommand request, CancellationToken ct)
    {
        await _db.GarantirAtividadeNoEscopoAsync(request.AtividadeId, ct);
        var item = new AtividadeItemOs
        {
            AtividadeId = request.AtividadeId,
            Tipo = request.Tipo,
            Texto = request.Texto.Trim(),
            Requisito = request.Tipo == TipoItemAtividadeOs.Restricao ? request.Requisito?.Trim() : null,
            OrigemItem = OrigemMedidaPrevencao.Manual,
            Status = StatusMedidaPrevencao.Validada,
            ValidadoEmUtc = DateTime.UtcNow,
            ValidadoPorUsuarioId = request.UsuarioId,
            ValidadoPorNome = await NomeUsuarioDetalhesOs.ResolverAsync(_db, request.UsuarioId, request.NomeUsuario, ct),
        };
        _db.AtividadeItensOs.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}

public record DecidirItemAtividadeOsCommand(Guid ItemId, bool Aceitar, string? TextoEditado, string? RequisitoEditado, Guid? UsuarioId, string? NomeUsuario) : IRequest;

public class DecidirItemAtividadeOsCommandHandler : IRequestHandler<DecidirItemAtividadeOsCommand>
{
    private readonly IAppDbContext _db;

    public DecidirItemAtividadeOsCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DecidirItemAtividadeOsCommand request, CancellationToken ct)
    {
        var item = await _db.AtividadeItensOs.NoEscopoDaAtividade(_db, i => i.AtividadeId)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new KeyNotFoundException($"Item {request.ItemId} não encontrado.");

        if (request.Aceitar)
        {
            if (!string.IsNullOrWhiteSpace(request.TextoEditado)) item.Texto = request.TextoEditado.Trim()[..Math.Min(500, request.TextoEditado.Trim().Length)];
            if (item.Tipo == TipoItemAtividadeOs.Restricao && !string.IsNullOrWhiteSpace(request.RequisitoEditado))
                item.Requisito = request.RequisitoEditado.Trim()[..Math.Min(300, request.RequisitoEditado.Trim().Length)];
        }

        item.Status = request.Aceitar ? StatusMedidaPrevencao.Validada : StatusMedidaPrevencao.Descartada;
        item.ValidadoEmUtc = DateTime.UtcNow;
        item.ValidadoPorUsuarioId = request.UsuarioId;
        item.ValidadoPorNome = await NomeUsuarioDetalhesOs.ResolverAsync(_db, request.UsuarioId, request.NomeUsuario, ct);
        await _db.SaveChangesAsync(ct);
    }
}

// IA sugere itens "interromper quando", "não executar sem" e autorizações. Tudo entra pendente;
// o que repete item já existente (validado ou pendente) é ignorado.
public record SugerirItensAtividadeOsCommand(Guid AtividadeId) : IRequest<int>;

public class SugerirItensAtividadeOsCommandHandler : IRequestHandler<SugerirItensAtividadeOsCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly IAnalistaItensAtividadeOs _analista;

    public SugerirItensAtividadeOsCommandHandler(IAppDbContext db, IAnalistaItensAtividadeOs analista)
    {
        _db = db;
        _analista = analista;
    }

    public async Task<int> Handle(SugerirItensAtividadeOsCommand request, CancellationToken ct)
    {
        var a = await _db.Atividades.NoEscopoDaAtividade(_db, x => x.Id)
            .Include(x => x.Ghe)
            .Include(x => x.Riscos).ThenInclude(r => r.Perigo)
            .Include(x => x.ItensOs)
            .FirstOrDefaultAsync(x => x.Id == request.AtividadeId, ct)
            ?? throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        var sugestoes = await _analista.SugerirAsync(new ContextoItensAtividadeOs(
            a.Nome,
            a.CondicoesExecucao ?? a.Ghe?.DescricaoAmbiente,
            a.Ghe?.AtividadesCriticas,
            a.Riscos.Select(r => $"{r.Perigo?.Nome} (nível {r.NivelRisco})").ToList()), ct);

        var existentes = a.ItensOs.Where(i => i.Status != StatusMedidaPrevencao.Descartada)
            .Select(i => ConciliacaoMedidasPgr.Normalizar(i.Texto)).ToHashSet();
        var novos = 0;
        foreach (var s in sugestoes)
        {
            var texto = s.Texto?.Trim();
            if (string.IsNullOrWhiteSpace(texto) || s.Tipo is null || !existentes.Add(ConciliacaoMedidasPgr.Normalizar(texto))) continue;
            _db.AtividadeItensOs.Add(new AtividadeItemOs
            {
                AtividadeId = a.Id,
                Tipo = s.Tipo.Value,
                Texto = texto[..Math.Min(500, texto.Length)],
                Requisito = s.Tipo == TipoItemAtividadeOs.Restricao ? s.Requisito?.Trim() is { Length: > 0 } r ? r[..Math.Min(300, r.Length)] : null : null,
                OrigemItem = OrigemMedidaPrevencao.SugestaoIa,
                Status = StatusMedidaPrevencao.Pendente,
            });
            novos++;
        }

        await _db.SaveChangesAsync(ct);
        return novos;
    }
}

public interface IAnalistaItensAtividadeOs
{
    Task<IReadOnlyList<ItemSugeridoIa>> SugerirAsync(ContextoItensAtividadeOs contexto, CancellationToken ct);
}

public record ContextoItensAtividadeOs(string Atividade, string? Condicoes, string? AtividadesCriticas, IReadOnlyList<string> Riscos);

public record ItemSugeridoIa(TipoItemAtividadeOs? Tipo, string? Texto, string? Requisito);

internal static class DetalhesOsTexto
{
    public static List<string> Linhas(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? new List<string>()
            : texto.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
}

internal static class NomeUsuarioDetalhesOs
{
    public static async Task<string?> ResolverAsync(IAppDbContext db, Guid? usuarioId, string? nomeToken, CancellationToken ct)
    {
        var nome = usuarioId is { } id
            ? await db.Usuarios.Where(u => u.Id == id).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;
        return nome ?? nomeToken;
    }
}
