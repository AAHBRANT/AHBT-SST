using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Cbo;
using AAHBRANT.SST.Application.MatrizRisco;
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

    // EPIs do catálogo referenciados nas medidas do rascunho, com o CA de hoje.
    public static async Task<Dictionary<Guid, CatalogoEpiInfo>> CarregarEpisAsync(IAppDbContext db, EstruturaGeracaoPgr e, CancellationToken ct)
    {
        var ids = e.Ghes.SelectMany(g => g.Atividades).SelectMany(a => a.Riscos).SelectMany(r => r.Medidas)
            .Where(m => m.CatalogoEpiId is not null).Select(m => m.CatalogoEpiId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await db.CatalogoEpis.Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new CatalogoEpiInfo(x.Nome, x.CertificadoAprovacaoNumero, x.CertificadoAprovacaoValidade), ct);
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
    // Itens da EAP marcados que entram nos GHEs (mesma regra da sugestão de GHEs). Item incluído à mão entra
    // mesmo como pacote: não tem atividade abaixo dele que o represente (29/10/2026).
    public static List<ItemEapGerado> AtividadesDaEap(EstruturaGeracaoPgr e)
    {
        var usadas = e.Itens.Where(i => i.Usar && (i.Nivel >= 2 || i.Manual)).ToList();
        return usadas.Count > 0 ? usadas : e.Itens.Where(i => i.Usar).ToList();
    }

    // epis: EPIs do catálogo usados nas medidas, para conferir o CA (NR-06). Sem o dicionário, o CA não é conferido.
    public static List<string> Calcular(EstruturaGeracaoPgr e, int maxP, int maxS, IReadOnlyDictionary<Guid, CatalogoEpiInfo>? epis = null)
    {
        var hoje = DateTime.UtcNow.Date;
        var p = new List<string>();
        if (e.DataTermino is null) p.Add("Informe o término da vigência do PGR.");
        var comp = e.Complementares;
        if (string.IsNullOrWhiteSpace(comp.Cnae)) p.Add("Informe o CNAE da obra.");
        if (comp.GrauRisco is not (>= 1 and <= 4)) p.Add("Informe o grau de risco da obra (1 a 4, NR-04).");
        if (string.IsNullOrWhiteSpace(comp.ResponsavelTecnicoRegistro)) p.Add("Informe o registro profissional (CREA) do responsável técnico.");
        if (string.IsNullOrWhiteSpace(comp.ResponsavelImplantacaoNome)) p.Add("Informe o responsável pela implantação do PGR.");
        if (string.IsNullOrWhiteSpace(comp.ContatosEmergencia.FirstOrDefault(x => x.Chave == "hospital")?.Telefone))
            p.Add("Informe o telefone do hospital de referência no plano de emergência.");
        foreach (var a in e.AcoesPlano.Where(a => !string.IsNullOrWhiteSpace(a.Descricao)))
        {
            var resumo = a.Descricao.Length <= 40 ? a.Descricao : a.Descricao[..40] + "…";
            if (a.ResponsavelUsuarioId is null && string.IsNullOrWhiteSpace(a.ResponsavelNome)) p.Add($"Plano de ação \"{resumo}\" sem responsável.");
            if (a.Prazo is null) p.Add($"Plano de ação \"{resumo}\" sem prazo.");
        }
        if (e.Ghes.Count == 0) p.Add("Nenhum GHE definido.");

        foreach (var dup in e.Ghes.GroupBy(g => g.Numero).Where(x => x.Count() > 1))
            p.Add($"Número de GHE repetido: {dup.Key:00}.");
        foreach (var dup in e.Ghes.Where(g => !string.IsNullOrWhiteSpace(g.Nome)).GroupBy(g => FuncaoSstClassifier.Normalizar(g.Nome)).Where(x => x.Count() > 1))
            p.Add($"GHE com nome repetido: {dup.First().Nome}. Junte ou renomeie.");
        foreach (var dup in e.Ghes.SelectMany(g => g.Atividades.Select(a => (g.Numero, a))).GroupBy(x => x.a.CodigoEap).Where(x => x.Count() > 1))
            p.Add($"Atividade {dup.Key} {dup.First().a.Nome} está em mais de um GHE ({string.Join(", ", dup.Select(y => $"{y.Numero:00}").Distinct())}).");
        if (e.Ghes.Count > 0)
        {
            var noGhe = e.Ghes.SelectMany(g => g.Atividades.Select(a => a.CodigoEap)).ToHashSet();
            foreach (var i in AtividadesDaEap(e).Where(i => !noGhe.Contains(i.Codigo)))
                p.Add($"Atividade da EAP sem GHE: {i.Codigo} {i.Nome}.");
        }
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
            // Medida sem decisão não trava mais: vale como aceita (regra do padrão, 29/10/2026).
            var semCategoria = riscos.SelectMany(r => r.Medidas).Count(m => m.EntraNoPgr() && !CategoriasMedida.Valida(m.Categoria));
            if (semCategoria > 0) p.Add($"{nome}: {semCategoria} medida(s) sem classificação (eliminação, EPC, administrativa ou EPI).");
            foreach (var r in riscos.Where(r => r.Probabilidade < 1 || r.Probabilidade > maxP || r.Severidade < 1 || r.Severidade > maxS))
                p.Add($"{nome}: {r.Perigo} com probabilidade/severidade fora da matriz.");

            foreach (var r in riscos)
            {
                var exp = r.Exposicao ?? new ExposicaoRisco();
                if (exp.Criterio == CriteriosExposicao.Quantitativo && exp.Medicoes.Count == 0)
                    p.Add($"{nome}: {r.Perigo} é quantitativo, mas não tem medição.");
                if (exp.Medicoes.Any(m => m.Data is null || m.Valor is null || string.IsNullOrWhiteSpace(m.Unidade)))
                    p.Add($"{nome}: medição de {r.Perigo} incompleta (informe data, valor e unidade).");

                if (epis is null) continue;
                foreach (var m in r.Medidas.Where(m => m.EntraNoPgr() && m.CatalogoEpiId is not null))
                {
                    if (!epis.TryGetValue(m.CatalogoEpiId!.Value, out var epi))
                        p.Add($"{nome}: o EPI escolhido para {r.Perigo} não existe mais no catálogo.");
                    else if (string.IsNullOrWhiteSpace(epi.Ca))
                        p.Add($"{nome}: o EPI {epi.Nome} está sem número de CA no catálogo.");
                    else if (epi.CaValidade is { } validade && validade.Date < hoje)
                        p.Add($"{nome}: o CA do EPI {epi.Nome} venceu em {validade:dd/MM/yyyy}.");
                }
            }
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
        var matriz = await _db.MatrizRiscoConfigs.VigenteMaisRecente().FirstOrDefaultAsync(ct);
        var maxP = matriz?.NumNiveisProbabilidade ?? 5;
        var maxS = matriz?.NumNiveisSeveridade ?? 5;
        var catalogo = await CatalogoCbo.CarregarAsync(_db, ct);
        await CboFuncoesGeracao.CompletarAsync(_db, estrutura, catalogo, ct);
        var pendencias = PendenciasGeracaoPgr.Calcular(estrutura, maxP, maxS, await SerializacaoGeracao.CarregarEpisAsync(_db, estrutura, ct));
        pendencias.AddRange(CboFuncoesGeracao.Pendencias(estrutura, catalogo));

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
            PendenciasAprovacao = pendencias,
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

        // Dados da obra (emergência, CNAE, CREA...) já preenchidos na última geração aprovada seguem para esta.
        var estrutura = new EstruturaGeracaoPgr { Itens = itens };
        ItemAdministrativoEap.Garantir(estrutura);
        var anterior = await _db.GeracoesPgrEap
            .Where(x => x.ObraId == request.ObraId && x.Status == StatusGeracaoPgrEap.Aprovado)
            .OrderByDescending(x => x.AprovadoEmUtc)
            .Select(x => x.EstruturaJson)
            .FirstOrDefaultAsync(ct);
        if (anterior is not null) estrutura.Complementares = SerializacaoGeracao.Ler(anterior).Complementares;

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
            EstruturaJson = SerializacaoGeracao.Gravar(estrutura),
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

        var usadas = PendenciasGeracaoPgr.AtividadesDaEap(e);
        if (usadas.Count == 0) throw new InvalidOperationException("Marque ao menos um item da EAP para entrar no PGR.");

        var funcoes = await _db.Funcoes.Select(f => new { f.Id, f.Nome }).ToListAsync(ct);
        var ghes = await _ia.SugerirGhesAsync(usadas, funcoes.Select(f => f.Nome).ToList(), ct);

        // Casa a função sugerida com o cadastro pelo nome normalizado (a IA recebeu a lista exata).
        var porNome = funcoes.GroupBy(f => FuncaoSstClassifier.Normalizar(f.Nome)).ToDictionary(x => x.Key, x => x.First().Id);
        var codigos = usadas.ToDictionary(i => i.Codigo, i => i.Nome);
        foreach (var ghe in ghes)
        {
            foreach (var f in ghe.Funcoes)
                f.FuncaoId = porNome.TryGetValue(FuncaoSstClassifier.Normalizar(f.Nome), out var id) ? id : null;
            // Só atividades que existem na EAP marcada; o nome vem da EAP, não da IA.
            ghe.Atividades = ghe.Atividades.Where(a => codigos.ContainsKey(a.CodigoEap))
                .GroupBy(a => a.CodigoEap).Select(x => new AtividadeGerada { CodigoEap = x.Key, Nome = codigos[x.Key] }).ToList();
        }

        // Cada atividade da EAP fica em um único GHE (vale a primeira ocorrência): a IA pode repetir, e a
        // repetição geraria o mesmo risco duas vezes no PGR.
        var vistas = new HashSet<string>();
        foreach (var ghe in ghes) ghe.Atividades = ghe.Atividades.Where(a => vistas.Add(a.CodigoEap)).ToList();

        // GHEs com o mesmo nome viram um só, e a mesma função não entra duas vezes no GHE.
        var unicos = new List<GheGerado>();
        foreach (var ghe in ghes)
        {
            var chave = FuncaoSstClassifier.Normalizar(ghe.Nome);
            var existente = chave.Length == 0 ? null : unicos.FirstOrDefault(x => FuncaoSstClassifier.Normalizar(x.Nome) == chave);
            if (existente is null) { unicos.Add(ghe); continue; }
            existente.Atividades.AddRange(ghe.Atividades);
            existente.Funcoes.AddRange(ghe.Funcoes);
        }
        foreach (var ghe in unicos)
        {
            var funcoesVistas = new HashSet<string>();
            ghe.Funcoes = ghe.Funcoes.Where(f => funcoesVistas.Add(FuncaoSstClassifier.Normalizar(f.Nome))).ToList();
        }

        // Administrativo fica sempre sozinho no GHE Administrativo, mesmo quando a IA o juntou a uma frente de
        // obra ou o deixou de fora (item incluído à mão com outro código, 29/10/2026).
        var administrativas = usadas.Where(ItemAdministrativoEap.EhAdministrativo).Select(i => i.Codigo).ToHashSet();
        if (administrativas.Count > 0)
        {
            // O GHE que a IA já montou só com administrativo, ou um de nome administrativo; senão, um novo.
            var adm = unicos.FirstOrDefault(x => x.Atividades.Count > 0 && x.Atividades.All(a => administrativas.Contains(a.CodigoEap)))
                ?? unicos.FirstOrDefault(x => FuncaoSstClassifier.Normalizar(x.Nome).StartsWith("administrativ"));
            if (adm is null)
            {
                adm = new GheGerado { Nome = ItemAdministrativoEap.NomeGhe, Ambiente = "Escritório e almoxarifado da obra" };
                unicos.Add(adm);
            }
            foreach (var ghe in unicos.Where(x => x != adm)) ghe.Atividades.RemoveAll(a => administrativas.Contains(a.CodigoEap));
            foreach (var codigo in administrativas.Where(c => adm.Atividades.All(a => a.CodigoEap != c)))
                adm.Atividades.Add(new AtividadeGerada { CodigoEap = codigo, Nome = codigos[codigo] });
        }

        // Item marcado que a IA não colocou em nenhum GHE não pode sumir: vai para um GHE a conferir, que trava a
        // aprovação até ganhar funções ou ter as atividades movidas.
        var agrupadas = unicos.SelectMany(x => x.Atividades.Select(a => a.CodigoEap)).ToHashSet();
        var soltas = usadas.Where(i => !agrupadas.Contains(i.Codigo)).ToList();
        if (soltas.Count > 0)
            unicos.Add(new GheGerado
            {
                Nome = "Itens a conferir (a IA não agrupou)",
                Atividades = soltas.Select(i => new AtividadeGerada { CodigoEap = i.Codigo, Nome = i.Nome }).ToList(),
            });

        var numero = 1;
        foreach (var ghe in unicos) ghe.Numero = numero++;
        ghes = unicos;

        e.Ghes = ghes.Where(x => x.Funcoes.Count > 0 || x.Atividades.Count > 0).ToList();

        var catalogo = await CatalogoCbo.CarregarAsync(_db, ct);
        await CboFuncoesGeracao.CompletarAsync(_db, e, catalogo, ct);
        await SugerirCboAsync(e, catalogo, ct);

        g.EstruturaJson = SerializacaoGeracao.Gravar(e);
        g.Passo = 3;
        await _db.SaveChangesAsync(ct);
    }

    // CBO das funções que ficaram sem código da tabela oficial (29/10/2026): o servidor separa os candidatos da tabela pelo
    // nome e a IA só escolhe entre eles. Falha da IA não derruba os GHEs: a função fica sem CBO para o técnico escolher.
    private async Task SugerirCboAsync(EstruturaGeracaoPgr e, CatalogoCbo catalogo, CancellationToken ct)
    {
        if (catalogo.Vazio) return;
        var semCbo = e.Ghes.SelectMany(x => x.Funcoes).Where(f => !catalogo.Existe(f.Cbo)).ToList();
        var pedidos = semCbo
            .GroupBy(f => FuncaoSstClassifier.Normalizar(f.Nome))
            .Select(x => (Chave: x.Key, Funcao: x.First(), Candidatos: catalogo.Candidatos(x.First().Nome)))
            .Where(x => x.Candidatos.Count > 0)
            .ToList();
        if (pedidos.Count == 0) return;

        List<CboSugeridoIa> sugestoes;
        try
        {
            sugestoes = await _ia.SugerirCboAsync(pedidos.Select(p => new FuncaoParaCbo(p.Funcao.Nome, p.Funcao.Descricao,
                p.Candidatos.Select(c => new CandidatoCbo(c.Codigo, c.Titulo, c.Sinonimo)).ToList())).ToList(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return;
        }

        foreach (var s in sugestoes)
        {
            var pedido = pedidos.FirstOrDefault(p => p.Chave == FuncaoSstClassifier.Normalizar(s.Funcao));
            var codigo = CodigoCbo.Normalizar(s.Codigo);
            if (pedido.Funcao is null || codigo is null || pedido.Candidatos.All(c => c.Codigo != codigo)) continue;
            foreach (var f in semCbo.Where(f => FuncaoSstClassifier.Normalizar(f.Nome) == pedido.Chave))
            {
                f.Cbo = codigo;
                f.CboTitulo = catalogo.TituloOficial(codigo);
                f.CboSugeridoPelaIa = true;
            }
        }
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

        var matriz = await _db.MatrizRiscoConfigs.VigenteMaisRecente().FirstOrDefaultAsync(ct)
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

        // A IA não vê o que já foi revisado e repete perigos: fica um só por perigo, o revisado ganha.
        var mantidos = atividade.Riscos.Where(r => r.Revisado && !r.Excluido).ToList();
        var perigosVistos = mantidos.Select(r => FuncaoSstClassifier.Normalizar(r.Perigo)).ToHashSet();
        riscos = riscos.Where(r => perigosVistos.Add(FuncaoSstClassifier.Normalizar(r.Perigo))).ToList();
        riscos = PerigosPadrao.LimitarPorAtividade(riscos, PerigosPadrao.MaxRiscosPorAtividade - mantidos.Count);
        atividade.Riscos = mantidos.Concat(riscos).ToList();
        atividade.RiscosGerados = true;
        g.EstruturaJson = SerializacaoGeracao.Gravar(e);
        g.Passo = Math.Max(g.Passo, 4);
        await _db.SaveChangesAsync(ct);
        return riscos.Count;
    }
}

// UsuarioId é quem está enviando; precisa ser o técnico escolhido quando ele assina no sistema.
public record EnviarGeracaoPgrParaAprovacaoCommand(Guid Id, Guid? UsuarioId = null) : IRequest;

public class EnviarGeracaoPgrParaAprovacaoCommandHandler : IRequestHandler<EnviarGeracaoPgrParaAprovacaoCommand>
{
    private readonly IAppDbContext _db;
    private readonly IRegistradorAssinaturaService? _registrador;

    public EnviarGeracaoPgrParaAprovacaoCommandHandler(IAppDbContext db, IRegistradorAssinaturaService? registrador = null)
    {
        _db = db;
        _registrador = registrador;
    }

    public async Task Handle(EnviarGeracaoPgrParaAprovacaoCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        var e = SerializacaoGeracao.Ler(g.EstruturaJson);
        if (e.Ghes.Count == 0 || e.Ghes.All(x => x.Atividades.All(a => !a.RiscosGerados)))
            throw new InvalidOperationException("Gere o inventário de riscos antes de enviar para aprovação.");

        // Toda função precisa de CBO da tabela oficial antes do envio (decisão do usuário, 29/10/2026).
        var catalogo = await CatalogoCbo.CarregarAsync(_db, ct);
        await CboFuncoesGeracao.CompletarAsync(_db, e, catalogo, ct);
        var semCbo = CboFuncoesGeracao.Pendencias(e, catalogo);
        if (semCbo.Count > 0)
            throw new InvalidOperationException("Escolha o CBO da tabela oficial no passo Funções e GHE. " + string.Join(" ", semCbo.Take(5)));
        g.EstruturaJson = SerializacaoGeracao.Gravar(e);

        // Quem assina o PGR precisa estar escolhido antes de enviar; o técnico, se assina no sistema, assina agora.
        var comp = e.Complementares ?? new DadosComplementaresPgr();
        AssinaturaGeracaoPgr.ConferirEscolha(comp.Tecnico, AssinaturaGeracaoPgr.RotuloTecnico);
        AssinaturaGeracaoPgr.ConferirEscolha(comp.Engenheiro, AssinaturaGeracaoPgr.RotuloEngenheiro);
        if (comp.Tecnico is { AMao: false } tecnico)
        {
            if (_registrador is null)
                throw new InvalidOperationException("A assinatura eletrônica não está disponível.");
            var trabalhadorId = await AssinaturaGeracaoPgr.ConferirUsuarioAsync(_db, tecnico, AssinaturaGeracaoPgr.RotuloTecnico, request.UsuarioId, ct);
            await AssinaturaGeracaoPgr.AssinarAsync(_db, _registrador, g.Id, trabalhadorId, PapelAssinatura.TecnicoSegurancaPgr, ct);
        }

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

        // O conteúdo vai mudar: a assinatura que o técnico deu sobre a versão anterior não vale para a nova, que ele assina de novo
        // ao reenviar. A trilha de auditoria guarda que ele assinou.
        var documento = await _db.DocumentosAssinatura.FirstOrDefaultAsync(
            d => d.EntidadeTipo == AssinaturaGeracaoPgr.EntidadeTipo && d.EntidadeId == g.Id && d.Status == StatusDocumentoAssinatura.EmAndamento, ct);
        if (documento is not null)
            _db.DocumentoSignatarios.RemoveRange(await _db.DocumentoSignatarios.Where(s => s.DocumentoAssinaturaId == documento.Id).ToListAsync(ct));

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
    private readonly IRegistradorAssinaturaService? _registrador;
    private readonly IMediator? _mediator;

    public AprovarGeracaoPgrCommandHandler(IAppDbContext db, IRegistradorAssinaturaService? registrador = null, IMediator? mediator = null)
    {
        _db = db;
        _registrador = registrador;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(AprovarGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        if (g.Status != StatusGeracaoPgrEap.AguardandoAprovacao)
            throw new InvalidOperationException("Só um PGR aguardando aprovação pode ser aprovado.");

        var e = SerializacaoGeracao.Ler(g.EstruturaJson);
        var matriz = await _db.MatrizRiscoConfigs.Include(c => c.Celulas).VigenteMaisRecente().FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhuma matriz de risco cadastrada.");
        var pendencias = PendenciasGeracaoPgr.Calcular(e, matriz.NumNiveisProbabilidade, matriz.NumNiveisSeveridade,
            await SerializacaoGeracao.CarregarEpisAsync(_db, e, ct));
        var catalogo = await CatalogoCbo.CarregarAsync(_db, ct);
        await CboFuncoesGeracao.CompletarAsync(_db, e, catalogo, ct);
        pendencias.AddRange(CboFuncoesGeracao.Pendencias(e, catalogo));
        if (pendencias.Count > 0)
            throw new InvalidOperationException("Ainda há pendências: " + string.Join(" ", pendencias.Take(5)));

        // Assinatura eletrônica: o técnico já assinou ao enviar; o engenheiro assina agora, antes de a aprovação ser gravada, para
        // nunca existir PGR aprovado sem a assinatura combinada. Rascunho antigo, sem escolha de quem assina, segue como antes.
        var comp = e.Complementares ?? new DadosComplementaresPgr();
        if (comp.Tecnico is { AMao: false } tecnicoEsperado)
        {
            var assinouTecnico = await _db.DocumentoSignatarios.AnyAsync(
                s => s.Papel == PapelAssinatura.TecnicoSegurancaPgr
                    && _db.DocumentosAssinatura.Any(d => d.Id == s.DocumentoAssinaturaId && d.EntidadeTipo == AssinaturaGeracaoPgr.EntidadeTipo && d.EntidadeId == g.Id), ct);
            if (!assinouTecnico)
                throw new InvalidOperationException(
                    $"O {AssinaturaGeracaoPgr.RotuloTecnico} ({tecnicoEsperado.Nome}) ainda não assinou. Devolva o PGR para ajustes, para ele assinar ao reenviar.");
        }
        if (comp.Engenheiro is { AMao: false } engenheiro)
        {
            if (_registrador is null)
                throw new InvalidOperationException("A assinatura eletrônica não está disponível.");
            AssinaturaGeracaoPgr.ConferirEscolha(engenheiro, AssinaturaGeracaoPgr.RotuloEngenheiro);
            var trabalhadorEngenheiro = await AssinaturaGeracaoPgr.ConferirUsuarioAsync(
                _db, engenheiro, AssinaturaGeracaoPgr.RotuloEngenheiro, request.UsuarioId, ct);
            await AssinaturaGeracaoPgr.AssinarAsync(_db, _registrador, g.Id, trabalhadorEngenheiro, PapelAssinatura.EngenheiroSegurancaPgr, ct);
        }

        var agora = DateTime.UtcNow;
        var aprovador = await SerializacaoGeracao.NomeUsuarioAsync(_db, request.UsuarioId, request.NomeUsuario, ct);

        // Estrutura e PGR anteriores da obra saem de uso (não são apagados: APR/PT emitidas apontam para eles).
        foreach (var antigo in await _db.Ghes.Include(x => x.Funcoes).Where(x => x.ObraId == g.ObraId).ToListAsync(ct))
        {
            antigo.Ativo = false;
            foreach (var f in antigo.Funcoes) f.Ativo = false;
        }
        // Obra que já tem PGR vigente: a nova geração entra como REVISÃO dele (mesmo PGR, com histórico), em vez de
        // um PGR separado. Se houver mais de um vigente (não deveria), revisa o mais recente e encerra os outros.
        var vigentes = (await _db.Pgrs.Where(p => p.ObraId == g.ObraId && p.Status == StatusPgr.Vigente).ToListAsync(ct))
            .OrderByDescending(p => p.DataElaboracao).ToList();
        var pgr = vigentes.FirstOrDefault();
        foreach (var outro in vigentes.Skip(1)) outro.Status = StatusPgr.Encerrado;

        if (pgr is null)
        {
            pgr = new Pgr
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
        }
        else
        {
            await RegistrarRevisaoAsync(pgr, g, aprovador, request.UsuarioId, agora, ct);
            pgr.DataProximaRevisao = g.DataProximaRevisao;
            pgr.DataTermino = e.DataTermino;
            pgr.ResponsavelUsuarioId = request.UsuarioId;
        }

        var funcoesPorId = await _db.Funcoes.ToDictionaryAsync(f => f.Id, ct);
        var funcoesPorNome = new Dictionary<string, Funcao>();
        foreach (var f in funcoesPorId.Values) funcoesPorNome.TryAdd(FuncaoSstClassifier.Normalizar(f.Nome), f);
        // O CBO escolhido no PGR (tabela oficial) vai para o cadastro da função, nova ou existente (29/10/2026).
        Funcao Resolver(FuncaoGerada f)
        {
            var cbo = catalogo.Existe(f.Cbo) ? CodigoCbo.Formatar(CodigoCbo.Normalizar(f.Cbo)!) : null;
            if (f.FuncaoId is { } id && funcoesPorId.TryGetValue(id, out var porId))
            {
                if (cbo is not null) porId.CboCodigo = cbo;
                return porId;
            }
            var chave = FuncaoSstClassifier.Normalizar(f.Nome);
            if (funcoesPorNome.TryGetValue(chave, out var existente))
            {
                if (cbo is not null) existente.CboCodigo = cbo;
                return existente;
            }
            var nova = new Funcao { Nome = f.Nome.Trim(), CboCodigo = cbo };
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
                    foreach (var m in r.Medidas.Where(m => m.EntraNoPgr()).OrderBy(m => m.Fase))
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

        // Numa revisão o plano de ação que já existe fica (pode estar em andamento); só entram as ações que ainda não estão lá.
        var descricoesDoPlano = (await _db.PlanoAcaoItens.Where(x => x.PgrId == pgr.Id).Select(x => x.Descricao).ToListAsync(ct))
            .Select(FuncaoSstClassifier.Normalizar).ToHashSet();
        foreach (var item in e.AcoesPlano.Where(x => !string.IsNullOrWhiteSpace(x.Descricao)
                     && descricoesDoPlano.Add(FuncaoSstClassifier.Normalizar(Limitar(x.Descricao.Trim(), 500)))))
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

        // Com assinatura eletrônica, o documento do motor é finalizado agora: o PDF assinado (carimbos, QR e código de verificação)
        // é gerado e guardado, e o download passa a entregar sempre esse arquivo.
        if (_mediator is not null && (comp.Tecnico is { AMao: false } || comp.Engenheiro is { AMao: false }))
        {
            var documentoId = await _db.DocumentosAssinatura
                .Where(d => d.EntidadeTipo == AssinaturaGeracaoPgr.EntidadeTipo && d.EntidadeId == g.Id)
                .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
            if (documentoId is { } id)
                await _mediator.Send(new FinalizarDocumentoCommand(id), ct);
        }

        return pgr.Id;
    }

    // Cria a próxima revisão do PGR vigente. O PDF anexado antes (consultoria) não se perde: vai para a revisão
    // anterior, como faz "Nova revisão". O PDF desta geração é gerado sob demanda pela própria geração, então a
    // revisão nova não guarda arquivo.
    private async Task RegistrarRevisaoAsync(Pgr pgr, GeracaoPgrEap g, string aprovador, Guid? usuarioId, DateTime agora, CancellationToken ct)
    {
        var existentes = await _db.PgrRevisoes.Where(r => r.PgrId == pgr.Id)
            .Select(r => new { r.NumeroRevisao, TemPdf = r.DocumentoConteudo != null }).ToListAsync(ct);
        var numero = existentes.Count == 0 ? 1 : existentes.Max(r => r.NumeroRevisao) + 1;

        if (pgr.DocumentoConteudo is { Length: > 0 } && !existentes.Any(r => r.TemPdf))
        {
            var numeroAnterior = RevisaoDocumento.NumeroLivreAbaixo(numero, existentes.Select(r => r.NumeroRevisao))
                ?? throw new InvalidOperationException("O PDF atual do PGR precisa ser guardado como revisão anterior, mas não há número livre.");
            _db.PgrRevisoes.Add(new PgrRevisao
            {
                PgrId = pgr.Id,
                NumeroRevisao = numeroAnterior,
                DataRevisao = pgr.DataElaboracao,
                Motivo = RevisaoDocumento.MotivoDocumentoAnterior,
                DocumentoConteudo = pgr.DocumentoConteudo,
                DocumentoContentType = pgr.DocumentoContentType,
                DocumentoNomeArquivo = RevisaoDocumento.NomeDocumentoAnterior,
            });
            pgr.DocumentoConteudo = null;
            pgr.DocumentoContentType = null;
        }

        _db.PgrRevisoes.Add(new PgrRevisao
        {
            PgrId = pgr.Id,
            NumeroRevisao = numero,
            DataRevisao = agora,
            Motivo = $"Nova geração pela EAP da obra ({g.NomeArquivoEap}), aprovada por {aprovador}.",
            ResponsavelUsuarioId = usuarioId,
        });
    }

    private static NivelRisco Nivel(MatrizRiscoConfig matriz, RiscoGerado r) =>
        matriz.Celulas.FirstOrDefault(c => c.Probabilidade == r.Probabilidade && c.Severidade == r.Severidade)?.NivelRisco
        ?? throw new InvalidOperationException($"A matriz de risco não tem célula para P={r.Probabilidade}/S={r.Severidade} ({r.Perigo}).");

    private static string Limitar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
