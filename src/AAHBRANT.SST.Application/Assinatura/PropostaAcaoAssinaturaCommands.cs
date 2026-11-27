using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Etapa 2 do agente engenheiro da assinatura (27/11): ação irreversível só com "Aprovar" do responsável no
// Telegram. O agente propõe (tipo + obra); o servidor decide os alvos, pede a aprovação, confere de novo e
// executa. Pedido vale 24 h.
public record PropostaAcaoAssinaturaDto(
    Guid Id,
    // Texto (não número) para o agente ler: "ExcluirPessoasFaciaisSoltas", "Pendente", "Executada"...
    string Tipo,
    Guid ObraId,
    string Status,
    int TotalAlvos,
    string Descricao,
    string? Motivo,
    DateTime CriadaEmUtc,
    DateTime ExpiraEmUtc,
    DateTime? DecididaEmUtc,
    string? Resultado,
    bool AprovacaoPedidaNoTelegram,
    // true = já havia um pedido igual aguardando: devolvido sem criar outro (nem mandar outra mensagem).
    bool Reaproveitada);

public static class PropostaAcaoAssinaturaRegras
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    public const string PrefixoBotao = "pa:";

    public static string DadoBotao(Guid propostaId, bool aprovar) => $"{PrefixoBotao}{propostaId:N}:{(aprovar ? "s" : "n")}";

    // "pa:<id>:s" ou "pa:<id>:n". Qualquer outra coisa: null.
    public static (Guid PropostaId, bool Aprovar)? LerBotao(string? dado)
    {
        if (dado is null || !dado.StartsWith(PrefixoBotao, StringComparison.Ordinal))
            return null;
        var partes = dado[PrefixoBotao.Length..].Split(':');
        if (partes.Length != 2 || !Guid.TryParseExact(partes[0], "N", out var id))
            return null;
        return partes[1] switch { "s" => (id, true), "n" => (id, false), _ => null };
    }

    public static string NomeStatus(StatusPropostaAcaoAssinatura status) => status switch
    {
        StatusPropostaAcaoAssinatura.Pendente => "aguardando decisão",
        StatusPropostaAcaoAssinatura.Aprovada => "aprovado e em execução",
        StatusPropostaAcaoAssinatura.Executada => "aprovado e executado",
        StatusPropostaAcaoAssinatura.Recusada => "recusado",
        StatusPropostaAcaoAssinatura.Expirada => "expirado",
        StatusPropostaAcaoAssinatura.Falhou => "aprovado, mas falhou",
        _ => status.ToString(),
    };

    internal static PropostaAcaoAssinaturaDto ParaDto(PropostaAcaoAssinatura p, bool reaproveitada = false) =>
        new(p.Id, p.Tipo.ToString(), p.ObraId, p.Status.ToString(), p.ListaAlvos().Count, p.Descricao, p.Motivo, p.CriadaEm, p.ExpiraEm,
            p.DecididaEm, p.Resultado, p.TelegramMensagemId is not null, reaproveitada);

    internal static string Hora(DateTime utc) => FusoBrasilia.ParaBrasilia(utc).ToString("dd/MM HH:mm");
}

public record CriarPropostaAcaoAssinaturaCommand(TipoAcaoAssinatura Tipo, Guid ObraId, string? Motivo) : IRequest<PropostaAcaoAssinaturaDto>;

public class CriarPropostaAcaoAssinaturaCommandHandler : IRequestHandler<CriarPropostaAcaoAssinaturaCommand, PropostaAcaoAssinaturaDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _facial;
    private readonly ITelegramAlertaService _telegram;

    public CriarPropostaAcaoAssinaturaCommandHandler(IAppDbContext db, IAutenticacaoFacialService facial, ITelegramAlertaService telegram)
    {
        _db = db;
        _facial = facial;
        _telegram = telegram;
    }

    public async Task<PropostaAcaoAssinaturaDto> Handle(CriarPropostaAcaoAssinaturaCommand request, CancellationToken ct)
    {
        if (request.Tipo != TipoAcaoAssinatura.ExcluirPessoasFaciaisSoltas)
            throw new InvalidOperationException("Tipo de ação não permitido.");

        var obra = await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => new { o.Nome }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");

        var agora = DateTime.UtcNow;
        var aberta = await _db.PropostasAcaoAssinatura
            .Where(p => p.ObraId == request.ObraId && p.Tipo == request.Tipo
                && p.Status == StatusPropostaAcaoAssinatura.Pendente && p.ExpiraEm > agora)
            .OrderByDescending(p => p.CriadaEm)
            .FirstOrDefaultAsync(ct);
        if (aberta is not null)
            return PropostaAcaoAssinaturaRegras.ParaDto(aberta, reaproveitada: true);

        var diagnostico = await _facial.DiagnosticarVinculosAsync(request.ObraId, ct);
        var soltas = diagnostico.PessoasSemVinculo;
        if (soltas.Count == 0)
            throw new InvalidOperationException("Não há pessoas soltas no grupo de rostos desta obra: nada a excluir.");

        // Quantas soltas têm o mesmo nome de alguém da obra que já tem cadastro facial válido (cópia velha).
        // Os nomes ficam só aqui, para a conta: não vão para a proposta nem para o Telegram.
        var nomesComCadastro = (await _db.Trabalhadores
                .Where(t => t.ObraId == request.ObraId && t.AzureFacePersonId != null)
                .Select(t => t.Nome)
                .ToListAsync(ct))
            .Select(Normalizar)
            .ToHashSet();
        var copias = soltas.Count(s => nomesComCadastro.Contains(Normalizar(s.NomeNoAzure)));

        var proposta = new PropostaAcaoAssinatura
        {
            Tipo = request.Tipo,
            ObraId = request.ObraId,
            Alvos = string.Join(',', soltas.Select(s => s.PersonIdAzure)),
            Descricao = $"Excluir {soltas.Count} pessoa(s) do grupo de rostos da obra {obra.Nome} que não estão ligadas a nenhum funcionário " +
                        $"({copias} delas são cópias de funcionários que já têm cadastro facial válido). Depois o grupo é retreinado. " +
                        "Não tem volta no Azure; ninguém perde o próprio cadastro.",
            Motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim()[..Math.Min(request.Motivo.Trim().Length, 1500)],
            CriadaEm = agora,
            ExpiraEm = agora + PropostaAcaoAssinaturaRegras.Validade,
        };
        _db.PropostasAcaoAssinatura.Add(proposta);
        await _db.SaveChangesAsync(ct);

        var texto = $"⚠️ Aprovação necessária\n\n{proposta.Descricao}" +
                    (proposta.Motivo is null ? "" : $"\n\nMotivo do agente: {proposta.Motivo}") +
                    $"\n\nVale até {PropostaAcaoAssinaturaRegras.Hora(proposta.ExpiraEm)}.";
        proposta.TelegramMensagemId = await _telegram.EnviarComBotoesAsync(texto,
        [
            new BotaoTelegram("✅ Aprovar", PropostaAcaoAssinaturaRegras.DadoBotao(proposta.Id, aprovar: true)),
            new BotaoTelegram("❌ Recusar", PropostaAcaoAssinaturaRegras.DadoBotao(proposta.Id, aprovar: false)),
        ], ct);
        await _db.SaveChangesAsync(ct);

        return PropostaAcaoAssinaturaRegras.ParaDto(proposta);
    }

    private static string Normalizar(string nome) =>
        string.Join(' ', nome.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

// Clique no botão do Telegram (o controller já conferiu que veio do responsável). Devolve o aviso curto
// mostrado no próprio Telegram.
public record DecidirPropostaAcaoAssinaturaCommand(Guid PropostaId, bool Aprovar) : IRequest<string>;

public class DecidirPropostaAcaoAssinaturaCommandHandler : IRequestHandler<DecidirPropostaAcaoAssinaturaCommand, string>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _facial;
    private readonly ITelegramAlertaService _telegram;

    public DecidirPropostaAcaoAssinaturaCommandHandler(IAppDbContext db, IAutenticacaoFacialService facial, ITelegramAlertaService telegram)
    {
        _db = db;
        _facial = facial;
        _telegram = telegram;
    }

    public async Task<string> Handle(DecidirPropostaAcaoAssinaturaCommand request, CancellationToken ct)
    {
        var proposta = await _db.PropostasAcaoAssinatura.FirstOrDefaultAsync(p => p.Id == request.PropostaId, ct);
        if (proposta is null)
            return "Pedido não encontrado.";
        if (proposta.Status != StatusPropostaAcaoAssinatura.Pendente)
            return $"Este pedido já está {PropostaAcaoAssinaturaRegras.NomeStatus(proposta.Status)}.";

        var agora = DateTime.UtcNow;
        if (agora > proposta.ExpiraEm)
        {
            proposta.Status = StatusPropostaAcaoAssinatura.Expirada;
            await _db.SaveChangesAsync(ct);
            await AtualizarMensagemAsync(proposta, "⌛ Expirou sem decisão. O agente pode pedir de novo.", ct);
            return "Este pedido expirou.";
        }

        proposta.DecididaEm = agora;
        if (!request.Aprovar)
        {
            proposta.Status = StatusPropostaAcaoAssinatura.Recusada;
            await _db.SaveChangesAsync(ct);
            await AtualizarMensagemAsync(proposta, $"❌ Recusado em {PropostaAcaoAssinaturaRegras.Hora(agora)}. Nada foi alterado.", ct);
            return "Recusado. Nada foi alterado.";
        }

        // Grava "Aprovada" ANTES de executar: um segundo clique (ou o Telegram reenviando o mesmo clique) bate
        // no controle de concorrência ou encontra o pedido já decidido, e não executa de novo.
        proposta.Status = StatusPropostaAcaoAssinatura.Aprovada;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return "Este pedido já está sendo executado.";
        }

        var total = proposta.ListaAlvos().Count;
        try
        {
            var excluidas = await _facial.ExcluirPessoasSemVinculoAsync(proposta.ObraId, proposta.ListaAlvos(), ct);
            proposta.Status = StatusPropostaAcaoAssinatura.Executada;
            proposta.Resultado = $"{excluidas} de {total} pessoa(s) excluída(s) e grupo retreinado." +
                                 (excluidas < total ? " As demais já tinham saído ou ganharam vínculo depois do pedido." : "");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            proposta.Status = StatusPropostaAcaoAssinatura.Falhou;
            proposta.Resultado = ex.Message.Length > 1500 ? ex.Message[..1500] : ex.Message;
        }
        await _db.SaveChangesAsync(ct);

        var icone = proposta.Status == StatusPropostaAcaoAssinatura.Executada ? "✅" : "❗";
        await AtualizarMensagemAsync(proposta,
            $"{icone} Aprovado em {PropostaAcaoAssinaturaRegras.Hora(agora)} — {PropostaAcaoAssinaturaRegras.NomeStatus(proposta.Status)}.\n{proposta.Resultado}", ct);
        return proposta.Status == StatusPropostaAcaoAssinatura.Executada ? "Feito." : "Falhou: veja a mensagem.";
    }

    private async Task AtualizarMensagemAsync(PropostaAcaoAssinatura proposta, string desfecho, CancellationToken ct)
    {
        if (proposta.TelegramMensagemId is { } mensagemId and > 0)
            await _telegram.EditarAsync(mensagemId, $"{proposta.Descricao}\n\n{desfecho}", ct);
    }
}

public record ObterPropostaAcaoAssinaturaQuery(Guid PropostaId) : IRequest<PropostaAcaoAssinaturaDto?>;

public class ObterPropostaAcaoAssinaturaQueryHandler : IRequestHandler<ObterPropostaAcaoAssinaturaQuery, PropostaAcaoAssinaturaDto?>
{
    private readonly IAppDbContext _db;

    public ObterPropostaAcaoAssinaturaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PropostaAcaoAssinaturaDto?> Handle(ObterPropostaAcaoAssinaturaQuery request, CancellationToken ct)
    {
        var proposta = await _db.PropostasAcaoAssinatura.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PropostaId, ct);
        return proposta is null ? null : PropostaAcaoAssinaturaRegras.ParaDto(proposta);
    }
}
