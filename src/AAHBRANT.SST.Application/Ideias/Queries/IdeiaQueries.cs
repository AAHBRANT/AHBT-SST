using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ideias.Queries;

// §14 filtros + §15 pesquisa por palavra-chave. A pesquisa cobre ideias, requisitos e demandas.
public record ListarIdeiasQuery(
    string? Busca = null,
    StatusIdeia? Status = null,
    string? Modulo = null,
    string? Categoria = null,
    PrioridadeIdeia? Prioridade = null,
    string? Responsavel = null,
    string? Criador = null,
    DateTime? De = null,
    DateTime? Ate = null) : IRequest<List<IdeiaResumoDto>>;

public class ListarIdeiasQueryHandler : IRequestHandler<ListarIdeiasQuery, List<IdeiaResumoDto>>
{
    private readonly IAppDbContext _db;

    public ListarIdeiasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<IdeiaResumoDto>> Handle(ListarIdeiasQuery r, CancellationToken ct)
    {
        var q = _db.Ideias.Include(i => i.IdeiaPrincipal).AsQueryable();

        if (r.Status is not null) q = q.Where(i => i.Status == r.Status);
        if (r.Prioridade is not null) q = q.Where(i => i.Prioridade == r.Prioridade);
        if (!string.IsNullOrWhiteSpace(r.Modulo)) { var m = r.Modulo.Trim(); q = q.Where(i => i.Modulo == m); }
        if (!string.IsNullOrWhiteSpace(r.Categoria)) { var c = r.Categoria.Trim(); q = q.Where(i => i.Categoria == c); }
        if (!string.IsNullOrWhiteSpace(r.Responsavel))
        {
            var p = r.Responsavel.Trim();
            q = q.Where(i => (i.ResponsavelAnaliseNome != null && i.ResponsavelAnaliseNome.Contains(p))
                          || (i.ResponsavelDesenvolvimentoNome != null && i.ResponsavelDesenvolvimentoNome.Contains(p)));
        }
        if (!string.IsNullOrWhiteSpace(r.Criador))
        {
            var p = r.Criador.Trim();
            q = q.Where(i => (i.RegistradoPorNome != null && i.RegistradoPorNome.Contains(p))
                          || (i.TelegramUsuarioNome != null && i.TelegramUsuarioNome.Contains(p)));
        }
        if (r.De is not null) { var de = r.De.Value.Date; q = q.Where(i => i.CreatedAtUtc >= de); }
        if (r.Ate is not null) { var ate = r.Ate.Value.Date.AddDays(1); q = q.Where(i => i.CreatedAtUtc < ate); }

        if (!string.IsNullOrWhiteSpace(r.Busca))
        {
            var b = r.Busca.Trim();
            q = q.Where(i =>
                i.Codigo.Contains(b) || i.Titulo.Contains(b) || i.Descricao.Contains(b)
                || i.MensagemOriginal.Contains(b)
                || (i.ProblemaOportunidade != null && i.ProblemaOportunidade.Contains(b))
                || (i.SolucaoSugerida != null && i.SolucaoSugerida.Contains(b))
                || (i.Modulo != null && i.Modulo.Contains(b))
                || i.Requisitos.Any(x => x.Titulo.Contains(b) || x.Descricao.Contains(b))
                || _db.DemandasDesenvolvimento.Any(d => d.IdeiaId == i.Id && (d.Codigo.Contains(b) || d.Titulo.Contains(b))));
        }

        var itens = await q.OrderByDescending(i => i.Numero).Take(500).ToListAsync(ct);
        return itens.Select(IdeiaMapeador.ParaResumo).ToList();
    }
}

public record ObterIdeiaQuery(Guid Id) : IRequest<IdeiaDetalheDto>;

public class ObterIdeiaQueryHandler : IRequestHandler<ObterIdeiaQuery, IdeiaDetalheDto>
{
    private readonly IAppDbContext _db;

    public ObterIdeiaQueryHandler(IAppDbContext db) => _db = db;

    public Task<IdeiaDetalheDto> Handle(ObterIdeiaQuery r, CancellationToken ct)
        => IdeiaDetalheLoader.CarregarAsync(_db, r.Id, ct);
}

public record ObterAnexoIdeiaQuery(Guid IdeiaId, Guid AnexoId) : IRequest<AnexoIdeiaConteudo>;

public record AnexoIdeiaConteudo(string NomeArquivo, string ContentType, byte[] Conteudo);

public class ObterAnexoIdeiaQueryHandler : IRequestHandler<ObterAnexoIdeiaQuery, AnexoIdeiaConteudo>
{
    private readonly IAppDbContext _db;

    public ObterAnexoIdeiaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AnexoIdeiaConteudo> Handle(ObterAnexoIdeiaQuery r, CancellationToken ct)
    {
        var a = await _db.IdeiaAnexos.FirstOrDefaultAsync(x => x.Id == r.AnexoId && x.IdeiaId == r.IdeiaId, ct)
            ?? throw new KeyNotFoundException("Anexo não encontrado.");
        return new AnexoIdeiaConteudo(a.NomeArquivo, a.ContentType, a.Conteudo);
    }
}

// §14 — indicadores. Ideias vinculadas a outra (duplicadas) não entram na contagem, para não inflar o total.
public record DashboardIdeiasQuery : IRequest<DashboardIdeiasDto>;

public class DashboardIdeiasQueryHandler : IRequestHandler<DashboardIdeiasQuery, DashboardIdeiasDto>
{
    private readonly IAppDbContext _db;

    public DashboardIdeiasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DashboardIdeiasDto> Handle(DashboardIdeiasQuery r, CancellationToken ct)
    {
        var porStatus = await _db.Ideias
            .Where(i => i.IdeiaPrincipalId == null)
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Qtde = g.Count() })
            .ToListAsync(ct);

        var porModulo = await _db.Ideias
            .Where(i => i.IdeiaPrincipalId == null)
            .GroupBy(i => i.Modulo)
            .Select(g => new { Modulo = g.Key, Qtde = g.Count() })
            .ToListAsync(ct);

        var dto = new DashboardIdeiasDto { Total = porStatus.Sum(x => x.Qtde) };
        foreach (var s in Enum.GetValues<StatusIdeia>())
            dto.PorStatus[s] = porStatus.FirstOrDefault(x => x.Status == s)?.Qtde ?? 0;
        dto.PorModulo = porModulo
            .Select(x => new ContagemIdeiaDto(x.Modulo ?? "Não classificado", x.Qtde))
            .OrderByDescending(x => x.Quantidade).ToList();
        return dto;
    }
}

// §8 — apoio da IA à análise (nunca decide). Aqui, em modo heurístico: ideias semelhantes,
// sugestão de prioridade pela pontuação, perguntas a responder antes da aprovação e modelo de
// requisito/critérios de aceite para a equipe refinar.
public record SugerirAnaliseIdeiaQuery(Guid IdeiaId) : IRequest<SugestoesAnaliseIdeiaDto>;

public class SugerirAnaliseIdeiaQueryHandler : IRequestHandler<SugerirAnaliseIdeiaQuery, SugestoesAnaliseIdeiaDto>
{
    private readonly IAppDbContext _db;

    public SugerirAnaliseIdeiaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<SugestoesAnaliseIdeiaDto> Handle(SugerirAnaliseIdeiaQuery r, CancellationToken ct)
    {
        var i = await _db.Ideias.FirstOrDefaultAsync(x => x.Id == r.IdeiaId, ct)
            ?? throw new KeyNotFoundException($"Ideia {r.IdeiaId} não encontrada.");

        var palavras = SimilaridadeIdeias.Palavras($"{i.Titulo} {i.Descricao}");
        var outras = await _db.Ideias.Include(x => x.IdeiaPrincipal)
            .Where(x => x.Id != i.Id).OrderByDescending(x => x.Numero).Take(500).ToListAsync(ct);
        var semelhantes = outras
            .Select(o => new { Ideia = o, Nota = SimilaridadeIdeias.Calcular(palavras, SimilaridadeIdeias.Palavras($"{o.Titulo} {o.Descricao}")) })
            .Where(x => x.Nota >= SimilaridadeIdeias.Limiar)
            .OrderByDescending(x => x.Nota).Take(5)
            .Select(x => IdeiaMapeador.ParaResumo(x.Ideia)).ToList();

        var perguntas = new List<string>();
        if (i.Modulo is null) perguntas.Add("Qual módulo do sistema será afetado?");
        if (string.IsNullOrWhiteSpace(i.ProblemaOportunidade)) perguntas.Add("Qual problema ou oportunidade esta ideia resolve hoje?");
        if (string.IsNullOrWhiteSpace(i.BeneficioEsperado)) perguntas.Add("Qual benefício mensurável se espera (tempo, custo, risco, conformidade)?");
        if (i.Impacto is null || i.Urgencia is null || i.ValorNegocio is null) perguntas.Add("Qual o impacto, a urgência e o valor para o negócio?");
        if (i.Esforco is null || i.Complexidade is null) perguntas.Add("Qual o esforço e a complexidade segundo a Tecnologia?");
        if (i.ViabilidadeTecnica == ViabilidadeIdeia.NaoAvaliada) perguntas.Add("É tecnicamente viável? Há dependências ou integrações?");
        if (i.ViabilidadeOperacional == ViabilidadeIdeia.NaoAvaliada) perguntas.Add("É operacionalmente viável para as equipes de campo e escritório?");
        if (!string.IsNullOrWhiteSpace(i.InformacoesFaltantes)) perguntas.Add($"Informações faltantes apontadas no registro: {i.InformacoesFaltantes}");

        var acao = IdeiaMapeador.Limpar(i.SolucaoSugerida, 600) ?? i.Titulo;
        var modulo = i.Modulo ?? "módulo a definir";
        return new SugestoesAnaliseIdeiaDto
        {
            Pontuacao = i.Pontuacao,
            PrioridadeSugerida = PontuacaoIdeia.SugerirPrioridade(i.Pontuacao),
            IdeiasSemelhantes = semelhantes,
            PerguntasAntesDeAprovar = perguntas,
            RequisitoSugerido = $"O sistema deverá, no {modulo}: {acao}",
            CriteriosAceiteSugeridos =
            [
                $"A funcionalidade está disponível no {modulo} para os perfis autorizados.",
                "O comportamento descrito no requisito foi validado com o solicitante da ideia.",
                "Há registro de histórico/auditoria das ações realizadas pela funcionalidade.",
                "A funcionalidade foi testada em ambiente de homologação antes da implantação."
            ]
        };
    }
}

// Telegram: acha a ideia cuja mensagem de origem (ou pergunta do bot) é a mensagem respondida —
// usado para anexar arquivo enviado como resposta.
public record LocalizarIdeiaPorMensagemTelegramQuery(long ChatId, long MensagemId) : IRequest<Guid?>;

public class LocalizarIdeiaPorMensagemTelegramQueryHandler : IRequestHandler<LocalizarIdeiaPorMensagemTelegramQuery, Guid?>
{
    private readonly IAppDbContext _db;

    public LocalizarIdeiaPorMensagemTelegramQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Guid?> Handle(LocalizarIdeiaPorMensagemTelegramQuery r, CancellationToken ct)
        => await _db.Ideias
            .Where(i => i.TelegramChatId == r.ChatId && (i.TelegramMensagemId == r.MensagemId || i.PerguntaMensagemId == r.MensagemId))
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);
}
