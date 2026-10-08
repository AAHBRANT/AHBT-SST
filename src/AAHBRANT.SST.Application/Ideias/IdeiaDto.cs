using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Ideias;

// Quem está agindo. Vem do controller (usuário autenticado) ou do Telegram (nome de quem escreveu).
public record AutorIdeia(Guid? UsuarioId, string Nome);

public class IdeiaResumoDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public StatusIdeia Status { get; set; }
    public string? Modulo { get; set; }
    public string? Categoria { get; set; }
    public PrioridadeIdeia? Prioridade { get; set; }
    public int? Pontuacao { get; set; }
    public CanalIdeia Canal { get; set; }
    public string? RegistradoPorNome { get; set; }
    public string? ResponsavelAnaliseNome { get; set; }
    public string? ResponsavelDesenvolvimentoNome { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? IdeiaPrincipalId { get; set; }
    public string? IdeiaPrincipalCodigo { get; set; }
}

public class IdeiaDetalheDto : IdeiaResumoDto
{
    public string MensagemOriginal { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? ProblemaOportunidade { get; set; }
    public string? Objetivo { get; set; }
    public string? SolucaoSugerida { get; set; }
    public string? Submodulo { get; set; }
    public string? BeneficioEsperado { get; set; }
    public string? PossiveisImpactos { get; set; }
    public string? IntegracoesNecessarias { get; set; }
    public bool? NecessidadeIa { get; set; }
    public string? Dependencias { get; set; }
    public string? InformacoesFaltantes { get; set; }
    public string EstruturadoPor { get; set; } = string.Empty;

    public NivelIdeia? Impacto { get; set; }
    public NivelIdeia? Urgencia { get; set; }
    public NivelIdeia? Complexidade { get; set; }
    public NivelIdeia? Esforco { get; set; }
    public NivelIdeia? ValorNegocio { get; set; }
    public string? EsforcoEstimado { get; set; }
    public ViabilidadeIdeia ViabilidadeTecnica { get; set; }
    public ViabilidadeIdeia ViabilidadeOperacional { get; set; }
    public PrioridadeIdeia? PrioridadeSugerida { get; set; }

    public DecisaoIdeia? Decisao { get; set; }
    public string? Justificativa { get; set; }
    public string? DecididoPorNome { get; set; }
    public DateTime? DataAprovacaoUtc { get; set; }
    public DateTime? DataInicioUtc { get; set; }
    public DateTime? DataConclusaoUtc { get; set; }
    public DateTime? DataImplantacaoUtc { get; set; }
    public string? Observacoes { get; set; }

    public Guid? IdeiaSemelhanteId { get; set; }
    public string? IdeiaSemelhanteCodigo { get; set; }
    public string? TelegramUsuarioNome { get; set; }

    // Status para os quais a ideia pode ir a partir de agora (§6).
    public List<StatusIdeia> ProximosStatus { get; set; } = new();

    public List<IdeiaComentarioDto> Comentarios { get; set; } = new();
    public List<IdeiaHistoricoDto> Historico { get; set; } = new();
    public List<IdeiaAnexoDto> Anexos { get; set; } = new();
    public List<IdeiaRequisitoDto> Requisitos { get; set; } = new();
    public List<IdeiaResumoDto> IdeiasVinculadas { get; set; } = new();
}

public class IdeiaComentarioDto
{
    public Guid Id { get; set; }
    public string AutorNome { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class IdeiaHistoricoDto
{
    public Guid Id { get; set; }
    public TipoHistoricoIdeia Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string AutorNome { get; set; } = string.Empty;
    public DateTime OcorridoEmUtc { get; set; }
}

public class IdeiaAnexoDto
{
    public Guid Id { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public string? EnviadoPorNome { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class IdeiaRequisitoDto
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string CriteriosAceite { get; set; } = string.Empty;
    public StatusRequisitoIdeia Status { get; set; }
    public DateTime? AprovadoEmUtc { get; set; }
    public string? AprovadoPorNome { get; set; }
    public List<DemandaDesenvolvimentoDto> Demandas { get; set; } = new();
}

public class DemandaDesenvolvimentoDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public Guid IdeiaId { get; set; }
    public Guid RequisitoId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public StatusDemandaDesenvolvimento Status { get; set; }
    public string? ResponsavelNome { get; set; }
    public string? FuncionalidadeEntregue { get; set; }
    public DateTime? ConcluidaEmUtc { get; set; }
}

public class DashboardIdeiasDto
{
    public int Total { get; set; }
    public Dictionary<StatusIdeia, int> PorStatus { get; set; } = new();
    public List<ContagemIdeiaDto> PorModulo { get; set; } = new();
}

public record ContagemIdeiaDto(string Nome, int Quantidade);

public class SugestoesAnaliseIdeiaDto
{
    public int? Pontuacao { get; set; }
    public PrioridadeIdeia? PrioridadeSugerida { get; set; }
    public List<IdeiaResumoDto> IdeiasSemelhantes { get; set; } = new();
    public List<string> PerguntasAntesDeAprovar { get; set; } = new();
    public string RequisitoSugerido { get; set; } = string.Empty;
    public List<string> CriteriosAceiteSugeridos { get; set; } = new();
    public string Aviso { get; set; } = "Sugestões automáticas de apoio. A decisão final é sempre do gestor.";
}

internal static class IdeiaMapeador
{
    public static IdeiaResumoDto ParaResumo(Ideia i) => new()
    {
        Id = i.Id,
        Codigo = i.Codigo,
        Titulo = i.Titulo,
        Status = i.Status,
        Modulo = i.Modulo,
        Categoria = i.Categoria,
        Prioridade = i.Prioridade,
        Pontuacao = i.Pontuacao,
        Canal = i.Canal,
        RegistradoPorNome = i.RegistradoPorNome ?? i.TelegramUsuarioNome,
        ResponsavelAnaliseNome = i.ResponsavelAnaliseNome,
        ResponsavelDesenvolvimentoNome = i.ResponsavelDesenvolvimentoNome,
        CreatedAtUtc = i.CreatedAtUtc,
        IdeiaPrincipalId = i.IdeiaPrincipalId,
        IdeiaPrincipalCodigo = i.IdeiaPrincipal?.Codigo
    };

    public static void Historico(Microsoft.EntityFrameworkCore.DbSet<IdeiaHistorico> set, Ideia ideia,
        TipoHistoricoIdeia tipo, string descricao, AutorIdeia autor)
    {
        set.Add(new IdeiaHistorico
        {
            IdeiaId = ideia.Id,
            Tipo = tipo,
            Descricao = descricao.Length > 1000 ? descricao[..1000] : descricao,
            AutorUsuarioId = autor.UsuarioId,
            AutorNome = string.IsNullOrWhiteSpace(autor.Nome) ? "Sistema" : autor.Nome,
            OcorridoEmUtc = DateTime.UtcNow
        });
    }

    public static string? Limpar(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        return v.Length > max ? v[..max] : v;
    }
}
