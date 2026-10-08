using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ideias.Commands;

// Origem Telegram: chat/mensagem/usuário de quem escreveu (o usuário do Telegram não é, por si só,
// um Usuario do sistema — guardamos o nome informado pelo Telegram).
public record OrigemTelegramIdeia(long ChatId, long MensagemId, long UsuarioId, string UsuarioNome);

public record RegistrarIdeiaCommand(
    string Mensagem,
    CanalIdeia Canal,
    AutorIdeia Autor,
    OrigemTelegramIdeia? Telegram = null) : IRequest<RegistroIdeiaResultado>;

public class RegistroIdeiaResultado
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Modulo { get; set; }
    public string? Categoria { get; set; }
    public StatusIdeia Status { get; set; }
    public TipoPerguntaIdeia Pergunta { get; set; }
    public string? IdeiaSemelhanteCodigo { get; set; }
    public string? IdeiaSemelhanteTitulo { get; set; }
}

public class RegistrarIdeiaCommandValidator : AbstractValidator<RegistrarIdeiaCommand>
{
    public RegistrarIdeiaCommandValidator()
    {
        RuleFor(x => x.Mensagem).NotEmpty().WithMessage("Escreva a ideia.").MaximumLength(4000);
    }
}

public class RegistrarIdeiaCommandHandler : IRequestHandler<RegistrarIdeiaCommand, RegistroIdeiaResultado>
{
    // Contador global (sem reinício anual): ContadorDocumento com Ano = 0.
    internal const string PrefixoContador = "IDEIA";
    private const int CandidatasParaSimilaridade = 500;

    private readonly IAppDbContext _db;
    private readonly IIdeiaEstruturacaoService _estruturacao;

    public RegistrarIdeiaCommandHandler(IAppDbContext db, IIdeiaEstruturacaoService estruturacao)
    {
        _db = db;
        _estruturacao = estruturacao;
    }

    public async Task<RegistroIdeiaResultado> Handle(RegistrarIdeiaCommand request, CancellationToken ct)
    {
        var mensagem = request.Mensagem.Trim();
        var e = await _estruturacao.EstruturarAsync(mensagem, ct);

        var numero = await ProximoNumeroAsync(PrefixoContador, ct);

        var ideia = new Ideia
        {
            Numero = numero,
            Codigo = $"IDEIA-{numero:D4}",
            Canal = request.Canal,
            MensagemOriginal = mensagem,
            RegistradoPorUsuarioId = request.Autor.UsuarioId,
            RegistradoPorNome = IdeiaMapeador.Limpar(request.Autor.Nome, 160),
            Titulo = IdeiaMapeador.Limpar(e.Titulo, 180) ?? "Ideia sem título",
            Descricao = IdeiaMapeador.Limpar(e.Descricao, 4000) ?? mensagem,
            ProblemaOportunidade = IdeiaMapeador.Limpar(e.ProblemaOportunidade, 2000),
            Objetivo = IdeiaMapeador.Limpar(e.Objetivo, 2000),
            SolucaoSugerida = IdeiaMapeador.Limpar(e.SolucaoSugerida, 2000),
            Modulo = IdeiaMapeador.Limpar(e.Modulo, 120),
            Categoria = IdeiaMapeador.Limpar(e.Categoria, 120),
            BeneficioEsperado = IdeiaMapeador.Limpar(e.BeneficioEsperado, 2000),
            PossiveisImpactos = IdeiaMapeador.Limpar(e.PossiveisImpactos, 2000),
            IntegracoesNecessarias = IdeiaMapeador.Limpar(e.IntegracoesNecessarias, 1000),
            NecessidadeIa = e.NecessidadeIa,
            InformacoesFaltantes = IdeiaMapeador.Limpar(e.InformacoesFaltantes, 2000),
            EstruturadoPor = e.EstruturadoPor,
            Status = StatusIdeia.NovaIdeia
        };

        if (request.Telegram is { } t)
        {
            ideia.TelegramChatId = t.ChatId;
            ideia.TelegramMensagemId = t.MensagemId;
            ideia.TelegramUsuarioId = t.UsuarioId;
            ideia.TelegramUsuarioNome = IdeiaMapeador.Limpar(t.UsuarioNome, 160);
        }

        // §16 — procura ideia parecida entre as que ainda não foram vinculadas a outra.
        var semelhante = await ProcurarSemelhanteAsync(ideia, ct);
        if (semelhante is not null)
        {
            ideia.IdeiaSemelhanteId = semelhante.Id;
            ideia.PerguntaPendente = TipoPerguntaIdeia.Duplicidade;
        }
        else if (ideia.Modulo is null)
        {
            ideia.PerguntaPendente = TipoPerguntaIdeia.Modulo;
        }

        _db.Ideias.Add(ideia);
        var canal = request.Canal == CanalIdeia.Telegram ? "pelo Telegram" : "pelo aplicativo";
        IdeiaMapeador.Historico(_db.IdeiaHistoricos, ideia, TipoHistoricoIdeia.Registro,
            $"{request.Autor.Nome} registrou a ideia {canal}.", request.Autor);
        IdeiaMapeador.Historico(_db.IdeiaHistoricos, ideia, TipoHistoricoIdeia.Classificacao,
            $"Estruturação automática ({e.EstruturadoPor}): módulo {ideia.Modulo ?? "não identificado"}, categoria {ideia.Categoria ?? "não identificada"}.",
            new AutorIdeia(null, "IA"));

        await _db.SaveChangesAsync(ct);

        return new RegistroIdeiaResultado
        {
            Id = ideia.Id,
            Codigo = ideia.Codigo,
            Titulo = ideia.Titulo,
            Modulo = ideia.Modulo,
            Categoria = ideia.Categoria,
            Status = ideia.Status,
            Pergunta = ideia.PerguntaPendente,
            IdeiaSemelhanteCodigo = semelhante?.Codigo,
            IdeiaSemelhanteTitulo = semelhante?.Titulo
        };
    }

    // Incrementa o contador na mesma transação da ideia; se o SaveChanges falhar o número não é gasto.
    internal static async Task<int> ProximoNumeroAsync(IAppDbContext db, string prefixo, CancellationToken ct)
    {
        var contador = await db.ContadoresDocumento.FirstOrDefaultAsync(c => c.Prefixo == prefixo && c.Ano == 0, ct);
        if (contador is null)
        {
            contador = new ContadorDocumento { Prefixo = prefixo, Ano = 0, UltimoNumero = 0 };
            db.ContadoresDocumento.Add(contador);
        }
        contador.UltimoNumero++;
        return contador.UltimoNumero;
    }

    private Task<int> ProximoNumeroAsync(string prefixo, CancellationToken ct) => ProximoNumeroAsync(_db, prefixo, ct);

    private async Task<Ideia?> ProcurarSemelhanteAsync(Ideia nova, CancellationToken ct)
    {
        var candidatas = await _db.Ideias
            .Where(i => i.IdeiaPrincipalId == null && i.Status != StatusIdeia.Descartada)
            .OrderByDescending(i => i.Numero)
            .Take(CandidatasParaSimilaridade)
            .ToListAsync(ct);

        var palavrasNova = SimilaridadeIdeias.Palavras($"{nova.Titulo} {nova.Descricao}");
        Ideia? melhor = null;
        var melhorNota = 0.0;
        foreach (var c in candidatas)
        {
            var nota = SimilaridadeIdeias.Calcular(palavrasNova, SimilaridadeIdeias.Palavras($"{c.Titulo} {c.Descricao}"));
            if (nota >= SimilaridadeIdeias.Limiar && nota > melhorNota) { melhor = c; melhorNota = nota; }
        }
        return melhor;
    }
}
