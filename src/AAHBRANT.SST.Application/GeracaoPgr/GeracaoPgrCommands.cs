using System.Text.Json;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Assistente "Gerar PGR pela EAP" (21/10/2026). Passos: 1 enviar EAP → 2 conferir leitura → 3 funções e
// GHE → 4 inventário de riscos → 5 aprovação do Engenheiro de Segurança. Nada vale como avaliado ou
// validado antes da aprovação.

public class GeracaoPgrEapDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public DateTime DataElaboracao { get; set; }
    public DateTime? DataProximaRevisao { get; set; }
    public string? ElaboradoPorNome { get; set; }
    public string NomeArquivoEap { get; set; } = string.Empty;
    public StatusGeracaoPgrEap Status { get; set; }
    public int Passo { get; set; }
    public string? MotivoDevolucao { get; set; }
    public string? AprovadoPorNome { get; set; }
    public DateTime? AprovadoEmUtc { get; set; }
    public Guid? PgrId { get; set; }
    public EstruturaGeracaoPgr Estrutura { get; set; } = new();
    public List<string> PendenciasAprovacao { get; set; } = new();
    public List<FuncaoCadastroDto> FuncoesCadastradas { get; set; } = new();
    public int MaxProbabilidade { get; set; }
    public int MaxSeveridade { get; set; }
}

public record FuncaoCadastroDto(Guid Id, string Nome);

public record ResumoGeracaoPgrEapDto(Guid Id, string Nome, string ObraNome, StatusGeracaoPgrEap Status, int Passo, DateTime CriadoEmUtc);

internal static class SerializacaoGeracao
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static EstruturaGeracaoPgr Ler(string json) =>
        (string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<EstruturaGeracaoPgr>(json, Opcoes) ?? new()).Normalizar();

    public static string Gravar(EstruturaGeracaoPgr e) => JsonSerializer.Serialize(e, Opcoes);

    public static async Task<GeracaoPgrEap> CarregarAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var g = await db.GeracoesPgrEap.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Geração de PGR não encontrada.");
        if (!db.ObraNoEscopo(g.ObraId)) throw new KeyNotFoundException("Geração de PGR não encontrada.");
        return g;
    }

    public static void GarantirEditavel(GeracaoPgrEap g)
    {
        if (g.Status is StatusGeracaoPgrEap.Aprovado)
            throw new InvalidOperationException("Este PGR já foi aprovado e não pode mais ser alterado por aqui.");
        if (g.Status is StatusGeracaoPgrEap.AguardandoAprovacao)
            throw new InvalidOperationException("O PGR está aguardando aprovação. Devolva para ajustes antes de alterar.");
    }

    public static async Task<string?> NomeUsuarioAsync(IAppDbContext db, Guid? usuarioId, string? nomeToken, CancellationToken ct)
    {
        var nome = usuarioId is { } id ? await db.Usuarios.Where(u => u.Id == id).Select(u => u.Nome).FirstOrDefaultAsync(ct) : null;
        return nome ?? nomeToken;
    }
}

// O que ainda impede a aprovação. A tela mostra a lista; o botão Aprovar só libera com ela vazia.
public static class PendenciasGeracaoPgr
{
    public static List<string> Calcular(EstruturaGeracaoPgr e, int maxP, int maxS)
    {
        var p = new List<string>();
        if (e.DataTermino is null) p.Add("Informe o término da vigência do PGR.");
        foreach (var a in e.AcoesPlano.Where(a => !string.IsNullOrWhiteSpace(a.Descricao)))
        {
            var resumo = a.Descricao.Length <= 40 ? a.Descricao : a.Descricao[..40] + "…";
            if (a.ResponsavelUsuarioId is null && string.IsNullOrWhiteSpace(a.ResponsavelNome)) p.Add($"Plano de ação \"{resumo}\" sem responsável.");
            if (a.Prazo is null) p.Add($"Plano de ação \"{resumo}\" sem prazo.");
        }
        if (e.Ghes.Count == 0) p.Add("Nenhum GHE definido.");
        foreach (var g in e.Ghes)
        {
            var nome = $"GHE {g.Numero:00}";
            if (g.Funcoes.Count == 0) p.Add($"{nome} sem funções.");
            foreach (var f in g.Funcoes.Where(f => string.IsNullOrWhiteSpace(f.Descricao)))
                p.Add($"{nome}: função {f.Nome} sem descrição das atividades.");
            if (g.Atividades.Count == 0) p.Add($"{nome} sem atividades da EAP.");
            foreach (var a in g.Atividades.Where(a => !a.RiscosGerados))
                p.Add($"{nome}: riscos ainda não gerados para {a.Nome}.");

            var riscos = g.Atividades.SelectMany(a => a.Riscos).Where(r => !r.Excluido).ToList();
            var naoRevisados = riscos.Count(r => !r.Revisado);
            if (naoRevisados > 0) p.Add($"{nome}: {naoRevisados} risco(s) não revisado(s).");
            var medidas = riscos.SelectMany(r => r.Medidas).Count(m => m.Decisao == "Pendente");
            if (medidas > 0) p.Add($"{nome}: {medidas} medida(s) sem decisão.");
            var semCategoria = riscos.SelectMany(r => r.Medidas).Count(m => m.Decisao == "Aceita" && !CategoriasMedida.Valida(m.Categoria));
            if (semCategoria > 0) p.Add($"{nome}: {semCategoria} medida(s) aceita(s) sem classificação (eliminação, EPC, administrativa ou EPI).");
            foreach (var r in riscos.Where(r => r.Probabilidade < 1 || r.Probabilidade > maxP || r.Severidade < 1 || r.Severidade > maxS))
                p.Add($"{nome}: {r.Perigo} com probabilidade/severidade fora da matriz.");
        }
        return p;
    }
}

public record ObterGeracaoPgrEapQuery(Guid Id) : IRequest<GeracaoPgrEapDto>;

public class ObterGeracaoPgrEapQueryHandler : IRequestHandler<ObterGeracaoPgrEapQuery, GeracaoPgrEapDto>
{
    private readonly IAppDbContext _db;

    public ObterGeracaoPgrEapQueryHandler(IAppDbContext db) => _db = db;

    public async Task<GeracaoPgrEapDto> Handle(ObterGeracaoPgrEapQuery request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        var estrutura = SerializacaoGeracao.Ler(g.EstruturaJson);
        var matriz = await _db.MatrizRiscoConfigs.FirstOrDefaultAsync(ct);
        var maxP = matriz?.NumNiveisProbabilidade ?? 5;
        var maxS = matriz?.NumNiveisSeveridade ?? 5;

        return new GeracaoPgrEapDto
        {
            Id = g.Id,
            ObraId = g.ObraId,
            ObraNome = await _db.Obras.Where(o => o.Id == g.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct) ?? string.Empty,
            Nome = g.Nome,
            DataElaboracao = g.DataElaboracao,
            DataProximaRevisao = g.DataProximaRevisao,
            ElaboradoPorNome = g.ElaboradoPorNome,
            NomeArquivoEap = g.NomeArquivoEap,
            Status = g.Status,
            Passo = g.Passo,
            MotivoDevolucao = g.MotivoDevolucao,
            AprovadoPorNome = g.AprovadoPorNome,
            AprovadoEmUtc = g.AprovadoEmUtc,
            PgrId = g.PgrId,
            Estrutura = estrutura,
            PendenciasAprovacao = PendenciasGeracaoPgr.Calcular(estrutura, maxP, maxS),
            FuncoesCadastradas = await _db.Funcoes.OrderBy(f => f.Nome).Select(f => new FuncaoCadastroDto(f.Id, f.Nome)).ToListAsync(ct),
            MaxProbabilidade = maxP,
            MaxSeveridade = maxS,
        };
    }
}

public record ListarGeracoesPgrEapQuery(Guid? ObraId) : IRequest<List<ResumoGeracaoPgrEapDto>>;

public class ListarGeracoesPgrEapQueryHandler : IRequestHandler<ListarGeracoesPgrEapQuery, List<ResumoGeracaoPgrEapDto>>
{
    private readonly IAppDbContext _db;

    public ListarGeracoesPgrEapQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ResumoGeracaoPgrEapDto>> Handle(ListarGeracoesPgrEapQuery request, CancellationToken ct)
    {
        var obras = _db.ObrasNoEscopo;
        var q = _db.GeracoesPgrEap.Where(g => _db.EscopoObraGlobal || obras.Contains(g.ObraId));
        if (request.ObraId is { } obraId) q = q.Where(g => g.ObraId == obraId);
        var lista = await q.OrderByDescending(g => g.CreatedAtUtc)
            .Select(g => new { g.Id, g.Nome, g.ObraId, g.Status, g.Passo, g.CreatedAtUtc }).ToListAsync(ct);
        var nomes = await _db.Obras.Select(o => new { o.Id, o.Nome }).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        return lista.Select(g => new ResumoGeracaoPgrEapDto(g.Id, g.Nome, nomes.GetValueOrDefault(g.ObraId, ""), g.Status, g.Passo, g.CreatedAtUtc)).ToList();
    }
}

// Passo 1: recebe o arquivo, extrai o texto e a IA monta a árvore da EAP.
public record CriarGeracaoPgrEapCommand(
    Guid ObraId, string Nome, DateTime DataElaboracao, DateTime? DataProximaRevisao,
    string NomeArquivo, string ContentType, byte[] Conteudo, Guid? UsuarioId, string? NomeUsuario) : IRequest<Guid>;

public class CriarGeracaoPgrEapCommandValidator : AbstractValidator<CriarGeracaoPgrEapCommand>
{
    public CriarGeracaoPgrEapCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(x => x.Conteudo).NotEmpty().WithMessage("Envie o arquivo da EAP.")
            .Must(c => c.Length <= 20 * 1024 * 1024).WithMessage("O arquivo deve ter no máximo 20 MB.");
    }
}

public class CriarGeracaoPgrEapCommandHandler : IRequestHandler<CriarGeracaoPgrEapCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IExtratorTextoEap _extrator;
    private readonly IGeradorPgrIa _ia;

    public CriarGeracaoPgrEapCommandHandler(IAppDbContext db, IExtratorTextoEap extrator, IGeradorPgrIa ia)
    {
        _db = db;
        _extrator = extrator;
        _ia = ia;
    }

    public async Task<Guid> Handle(CriarGeracaoPgrEapCommand request, CancellationToken ct)
    {
        if (!_db.ObraNoEscopo(request.ObraId) || !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada.");

        var texto = _extrator.Extrair(request.Conteudo, request.NomeArquivo, request.ContentType);
        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("Não foi possível ler nenhum item no arquivo da EAP.");

        var itens = await _ia.LerEapAsync(texto, ct);
        if (itens.Count == 0)
            throw new InvalidOperationException("A IA não encontrou pacotes ou atividades na EAP enviada.");

        var g = new GeracaoPgrEap
        {
            ObraId = request.ObraId,
            Nome = request.Nome.Trim(),
            DataElaboracao = request.DataElaboracao,
            DataProximaRevisao = request.DataProximaRevisao,
            ElaboradoPorUsuarioId = request.UsuarioId,
            ElaboradoPorNome = await SerializacaoGeracao.NomeUsuarioAsync(_db, request.UsuarioId, request.NomeUsuario, ct),
            NomeArquivoEap = request.NomeArquivo,
            Passo = 2,
            EstruturaJson = SerializacaoGeracao.Gravar(new EstruturaGeracaoPgr { Itens = itens }),
        };
        _db.GeracoesPgrEap.Add(g);
        await _db.SaveChangesAsync(ct);
        return g.Id;
    }
}

// Passos 2 a 4: a tela devolve a estrutura editada e o passo em que está.
public record SalvarGeracaoPgrEapCommand(Guid Id, int Passo, EstruturaGeracaoPgr Estrutura) : IRequest;

public class SalvarGeracaoPgrEapCommandHandler : IRequestHandler<SalvarGeracaoPgrEapCommand>
{
    private readonly IAppDbContext _db;

    public SalvarGeracaoPgrEapCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(SalvarGeracaoPgrEapCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        g.EstruturaJson = SerializacaoGeracao.Gravar(request.Estrutura);
        g.Passo = Math.Clamp(request.Passo, 2, 5);
        await _db.SaveChangesAsync(ct);
    }
}

// Passo 3: a IA agrupa as atividades marcadas em GHEs, sugere as funções (preferindo as do cadastro)
// e escreve a descrição das atividades de cada função. Substitui os GHEs atuais do rascunho.
public record SugerirGhesGeracaoPgrCommand(Guid Id) : IRequest;

public class SugerirGhesGeracaoPgrCommandHandler : IRequestHandler<SugerirGhesGeracaoPgrCommand>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorPgrIa _ia;

    public SugerirGhesGeracaoPgrCommandHandler(IAppDbContext db, IGeradorPgrIa ia)
    {
        _db = db;
        _ia = ia;
    }

    public async Task Handle(SugerirGhesGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        var e = SerializacaoGeracao.Ler(g.EstruturaJson);

        var usadas = e.Itens.Where(i => i.Usar && i.Nivel >= 2).ToList();
        if (usadas.Count == 0) usadas = e.Itens.Where(i => i.Usar).ToList();
        if (usadas.Count == 0) throw new InvalidOperationException("Marque ao menos um item da EAP para entrar no PGR.");

        var funcoes = await _db.Funcoes.Select(f => new { f.Id, f.Nome }).ToListAsync(ct);
        var ghes = await _ia.SugerirGhesAsync(usadas, funcoes.Select(f => f.Nome).ToList(), ct);

        // Casa a função sugerida com o cadastro pelo nome normalizado (a IA recebeu a lista exata).
        var porNome = funcoes.GroupBy(f => FuncaoSstClassifier.Normalizar(f.Nome)).ToDictionary(x => x.Key, x => x.First().Id);
        var codigos = usadas.ToDictionary(i => i.Codigo, i => i.Nome);
        var numero = 1;
        foreach (var ghe in ghes)
        {
            ghe.Numero = numero++;
            foreach (var f in ghe.Funcoes)
                f.FuncaoId = porNome.TryGetValue(FuncaoSstClassifier.Normalizar(f.Nome), out var id) ? id : null;
            // Só atividades que existem na EAP marcada; o nome vem da EAP, não da IA.
            ghe.Atividades = ghe.Atividades.Where(a => codigos.ContainsKey(a.CodigoEap))
                .GroupBy(a => a.CodigoEap).Select(x => new AtividadeGerada { CodigoEap = x.Key, Nome = codigos[x.Key] }).ToList();
        }

        e.Ghes = ghes.Where(x => x.Funcoes.Count > 0 || x.Atividades.Count > 0).ToList();
        g.EstruturaJson = SerializacaoGeracao.Gravar(e);
        g.Passo = 3;
        await _db.SaveChangesAsync(ct);
    }
}

// Passo 4: riscos e medidas de uma atividade. A tela chama uma atividade por vez (cada chamada à IA
// leva alguns segundos). Refazer substitui os riscos ainda não revisados da atividade.
public record GerarRiscosAtividadeGeracaoPgrCommand(Guid Id, int GheNumero, string CodigoEap) : IRequest<int>;

public class GerarRiscosAtividadeGeracaoPgrCommandHandler : IRequestHandler<GerarRiscosAtividadeGeracaoPgrCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorPgrIa _ia;

    public GerarRiscosAtividadeGeracaoPgrCommandHandler(IAppDbContext db, IGeradorPgrIa ia)
    {
        _db = db;
        _ia = ia;
    }

    public async Task<int> Handle(GerarRiscosAtividadeGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        var e = SerializacaoGeracao.Ler(g.EstruturaJson);
        var ghe = e.Ghes.FirstOrDefault(x => x.Numero == request.GheNumero)
            ?? throw new KeyNotFoundException("GHE não encontrado no rascunho.");
        var atividade = ghe.Atividades.FirstOrDefault(a => a.CodigoEap == request.CodigoEap)
            ?? throw new KeyNotFoundException("Atividade não encontrada no GHE.");

        var matriz = await _db.MatrizRiscoConfigs.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhuma matriz de risco cadastrada.");
        // Só requisitos validados (Ativo) podem ser citados como base.
        var requisitos = await _db.RequisitosLegais.Where(q => q.Status == StatusRequisitoLegal.Ativo)
            .OrderBy(q => q.Norma).Select(q => q.Norma + " " + q.Artigo + " — " + q.Titulo).Take(80).ToListAsync(ct);

        var riscos = await _ia.GerarRiscosAsync(new ContextoRiscosAtividade(
            atividade.Nome, ghe.Nome, ghe.Ambiente, ghe.Funcoes.Select(f => f.Nome).ToList(),
            matriz.NumNiveisProbabilidade, matriz.NumNiveisSeveridade, requisitos), ct);

        var normasValidas = requisitos.Select(r => r.Split(' ')[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in riscos)
        {
            r.Probabilidade = Math.Clamp(r.Probabilidade, 1, matriz.NumNiveisProbabilidade);
            r.Severidade = Math.Clamp(r.Severidade, 1, matriz.NumNiveisSeveridade);
            r.Base = r.Base.Where(b => normasValidas.Contains(b.Split(' ')[0])).Distinct().ToList();
            r.Medidas = r.Medidas.Where(m => !string.IsNullOrWhiteSpace(m.Texto)).ToList();
            r.Revisado = false;
        }

        atividade.Riscos = atividade.Riscos.Where(r => r.Revisado && !r.Excluido).Concat(riscos).ToList();
        atividade.RiscosGerados = true;
        g.EstruturaJson = SerializacaoGeracao.Gravar(e);
        g.Passo = Math.Max(g.Passo, 4);
        await _db.SaveChangesAsync(ct);
        return riscos.Count;
    }
}

public record EnviarGeracaoPgrParaAprovacaoCommand(Guid Id) : IRequest;

public class EnviarGeracaoPgrParaAprovacaoCommandHandler : IRequestHandler<EnviarGeracaoPgrParaAprovacaoCommand>
{
    private readonly IAppDbContext _db;

    public EnviarGeracaoPgrParaAprovacaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EnviarGeracaoPgrParaAprovacaoCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        var e = SerializacaoGeracao.Ler(g.EstruturaJson);
        if (e.Ghes.Count == 0 || e.Ghes.All(x => x.Atividades.All(a => !a.RiscosGerados)))
            throw new InvalidOperationException("Gere o inventário de riscos antes de enviar para aprovação.");
        g.Status = StatusGeracaoPgrEap.AguardandoAprovacao;
        g.Passo = 5;
        g.MotivoDevolucao = null;
        await _db.SaveChangesAsync(ct);
    }
}

public record DevolverGeracaoPgrCommand(Guid Id, string Motivo) : IRequest;

public class DevolverGeracaoPgrCommandValidator : AbstractValidator<DevolverGeracaoPgrCommand>
{
    public DevolverGeracaoPgrCommandValidator() => RuleFor(x => x.Motivo).NotEmpty().MaximumLength(1000);
}

public class DevolverGeracaoPgrCommandHandler : IRequestHandler<DevolverGeracaoPgrCommand>
{
    private readonly IAppDbContext _db;

    public DevolverGeracaoPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DevolverGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        if (g.Status != StatusGeracaoPgrEap.AguardandoAprovacao)
            throw new InvalidOperationException("Só um PGR aguardando aprovação pode ser devolvido.");
        g.Status = StatusGeracaoPgrEap.Devolvido;
        g.MotivoDevolucao = request.Motivo.Trim();
        g.Passo = 4;
        await _db.SaveChangesAsync(ct);
    }
}

// Passo 5: o Engenheiro de Segurança aprova. Cria o PGR vigente da obra com GHEs, funções, uma
// atividade por item da EAP, riscos (avaliados pelo engenheiro) e as medidas aceitas (validadas).
// A estrutura anterior da obra sai de uso sem ser apagada, como na nova revisão por leitura do PDF.
public record AprovarGeracaoPgrCommand(Guid Id, Guid? UsuarioId, string? NomeUsuario) : IRequest<Guid>;

public class AprovarGeracaoPgrCommandHandler : IRequestHandler<AprovarGeracaoPgrCommand, Guid>
{
    private readonly IAppDbContext _db;

    public AprovarGeracaoPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AprovarGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        if (g.Status != StatusGeracaoPgrEap.AguardandoAprovacao)
            throw new InvalidOperationException("Só um PGR aguardando aprovação pode ser aprovado.");

        var e = SerializacaoGeracao.Ler(g.EstruturaJson);
        var matriz = await _db.MatrizRiscoConfigs.Include(c => c.Celulas).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhuma matriz de risco cadastrada.");
        var pendencias = PendenciasGeracaoPgr.Calcular(e, matriz.NumNiveisProbabilidade, matriz.NumNiveisSeveridade);
        if (pendencias.Count > 0)
            throw new InvalidOperationException("Ainda há pendências: " + string.Join(" ", pendencias.Take(5)));

        var agora = DateTime.UtcNow;
        var aprovador = await SerializacaoGeracao.NomeUsuarioAsync(_db, request.UsuarioId, request.NomeUsuario, ct);

        // Estrutura e PGR anteriores da obra saem de uso (não são apagados: APR/PT emitidas apontam para eles).
        foreach (var antigo in await _db.Ghes.Include(x => x.Funcoes).Where(x => x.ObraId == g.ObraId).ToListAsync(ct))
        {
            antigo.Ativo = false;
            foreach (var f in antigo.Funcoes) f.Ativo = false;
        }
        foreach (var vigente in await _db.Pgrs.Where(p => p.ObraId == g.ObraId && p.Status == StatusPgr.Vigente).ToListAsync(ct))
            vigente.Status = StatusPgr.Encerrado;

        var pgr = new Pgr
        {
            ObraId = g.ObraId,
            Nome = g.Nome,
            Descricao = $"Gerado pela EAP da obra ({g.NomeArquivoEap}) e aprovado por {aprovador}.",
            DataElaboracao = g.DataElaboracao,
            DataProximaRevisao = g.DataProximaRevisao,
            DataTermino = e.DataTermino,
            ResponsavelUsuarioId = request.UsuarioId,
            Status = StatusPgr.Vigente,
        };
        _db.Pgrs.Add(pgr);

        var funcoesPorId = await _db.Funcoes.ToDictionaryAsync(f => f.Id, ct);
        var funcoesPorNome = new Dictionary<string, Funcao>();
        foreach (var f in funcoesPorId.Values) funcoesPorNome.TryAdd(FuncaoSstClassifier.Normalizar(f.Nome), f);
        Funcao Resolver(FuncaoGerada f)
        {
            if (f.FuncaoId is { } id && funcoesPorId.TryGetValue(id, out var porId)) return porId;
            var chave = FuncaoSstClassifier.Normalizar(f.Nome);
            if (funcoesPorNome.TryGetValue(chave, out var existente)) return existente;
            var nova = new Funcao { Nome = f.Nome.Trim() };
            _db.Funcoes.Add(nova);
            funcoesPorNome[chave] = nova;
            return nova;
        }

        var perigos = await _db.Perigos.ToDictionaryAsync(p => p.Nome, p => p, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var gg in e.Ghes.OrderBy(x => x.Numero))
        {
            var ghe = new Ghe
            {
                ObraId = g.ObraId,
                Numero = gg.Numero,
                Setor = Limitar(gg.Nome, 200),
                JornadaTrabalho = gg.Jornada,
                DescricaoAmbiente = gg.Ambiente,
                AtividadesCriticas = string.Join("; ", gg.Atividades
                    .Where(a => a.Riscos.Any(r => !r.Excluido && Nivel(matriz, r) >= NivelRisco.Alto)).Select(a => a.Nome)),
            };
            _db.Ghes.Add(ghe);
            foreach (var f in gg.Funcoes)
                ghe.Funcoes.Add(new GheFuncao { Funcao = Resolver(f), DescricaoAtividades = f.Descricao?.Trim() });

            foreach (var a in gg.Atividades)
            {
                var atividade = new Atividade { ObraId = g.ObraId, Ghe = ghe, Nome = Limitar($"{a.CodigoEap} {a.Nome}".Trim(), 200) };
                _db.Atividades.Add(atividade);

                foreach (var r in a.Riscos.Where(r => !r.Excluido))
                {
                    var nomePerigo = Limitar(r.Perigo.Trim(), 200);
                    if (!perigos.TryGetValue(nomePerigo, out var perigo))
                    {
                        perigo = new Perigo { Nome = nomePerigo, Agente = r.Tipo };
                        _db.Perigos.Add(perigo);
                        perigos[nomePerigo] = perigo;
                    }

                    var risco = new Risco
                    {
                        Atividade = atividade,
                        Perigo = perigo,
                        Ambiente = gg.Ambiente,
                        Exposicao = string.IsNullOrWhiteSpace(r.Tipo) ? null : $"Tipo: {r.Tipo}",
                        Consequencia = r.Danos,
                        Probabilidade = r.Probabilidade,
                        Severidade = r.Severidade,
                        NivelRisco = Nivel(matriz, r),
                        ControlesAdicionais = r.Base.Count == 0 ? null : "Base legal: " + string.Join(", ", r.Base),
                        AvaliadoEmUtc = agora,
                        AvaliadoPorUsuarioId = request.UsuarioId,
                        AvaliadoPorNome = aprovador,
                    };
                    _db.Riscos.Add(risco);

                    var ordem = 0;
                    foreach (var m in r.Medidas.Where(m => m.Decisao == "Aceita").OrderBy(m => m.Fase))
                    {
                        _db.MedidasPrevencao.Add(new MedidaPrevencao
                        {
                            Risco = risco,
                            Fase = (FaseMedidaPrevencao)Math.Clamp(m.Fase, 1, 3),
                            Texto = Limitar(m.Texto.Trim(), 1000),
                            OrigemMedida = OrigemMedidaPrevencao.SugestaoIa,
                            Status = StatusMedidaPrevencao.Validada,
                            Ordem = ordem++,
                            ValidadoEmUtc = agora,
                            ValidadoPorUsuarioId = request.UsuarioId,
                            ValidadoPorNome = aprovador,
                        });
                    }
                }
            }
        }

        foreach (var item in e.AcoesPlano.Where(x => !string.IsNullOrWhiteSpace(x.Descricao)))
            _db.PlanoAcaoItens.Add(new PlanoAcaoItem
            {
                Pgr = pgr,
                Descricao = Limitar(item.Descricao.Trim(), 500),
                ResponsavelUsuarioId = item.ResponsavelUsuarioId,
                Prazo = item.Prazo,
                Status = StatusControleRisco.Pendente,
            });

        g.Status = StatusGeracaoPgrEap.Aprovado;
        g.AprovadoEmUtc = agora;
        g.AprovadoPorUsuarioId = request.UsuarioId;
        g.AprovadoPorNome = aprovador;
        g.Pgr = pgr;
        await _db.SaveChangesAsync(ct);
        // Cursos obrigatórios por função saem do PGR recém-aprovado, como sugestão para o engenheiro
        // revisar na obra (21/10). Só acrescenta: nada aprovado ou recusado antes é tocado.
        await CursosObrigatorios.GeradorCursosObrigatorios.GerarAsync(_db, g.ObraId, ct);
        return pgr.Id;
    }

    private static NivelRisco Nivel(MatrizRiscoConfig matriz, RiscoGerado r) =>
        matriz.Celulas.FirstOrDefault(c => c.Probabilidade == r.Probabilidade && c.Severidade == r.Severidade)?.NivelRisco
        ?? throw new InvalidOperationException($"A matriz de risco não tem célula para P={r.Probabilidade}/S={r.Severidade} ({r.Perigo}).");

    private static string Limitar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
