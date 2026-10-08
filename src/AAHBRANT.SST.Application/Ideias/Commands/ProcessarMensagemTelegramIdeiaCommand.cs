using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ideias.Commands;

// Uma mensagem recebida no grupo/bot do Telegram (§2). O controller só faz a ponte HTTP; toda a
// decisão (registrar, responder pergunta pendente, ajuda, consulta de status) mora aqui.
public record ProcessarMensagemTelegramIdeiaCommand(
    long ChatId,
    long MensagemId,
    long? RespondeMensagemId,
    long UsuarioId,
    string UsuarioNome,
    string Texto) : IRequest<RespostaTelegramIdeia>;

public class RespostaTelegramIdeia
{
    // null = nada a responder.
    public string? Texto { get; set; }
    // Ideia registrada/atualizada por esta mensagem (para anexar foto/arquivo e para ligar a pergunta pendente).
    public Guid? IdeiaId { get; set; }
    // true = a resposta enviada é uma pergunta; o controller deve gravar o id da mensagem do bot.
    public bool AguardaResposta { get; set; }
}

public class ProcessarMensagemTelegramIdeiaCommandHandler
    : IRequestHandler<ProcessarMensagemTelegramIdeiaCommand, RespostaTelegramIdeia>
{
    internal const string Ajuda =
        "Banco de Ideias — é só escrever sua ideia aqui, sem formulário.\n\n" +
        "Exemplo: \"O módulo de compras deveria avisar quando o material recebido for diferente do pedido.\"\n\n" +
        "Comandos:\n/status IDEIA-0037 — ver a situação de uma ideia\n/ajuda — esta mensagem";

    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IIdeiaEstruturacaoService _estruturacao;

    public ProcessarMensagemTelegramIdeiaCommandHandler(IAppDbContext db, IMediator mediator, IIdeiaEstruturacaoService estruturacao)
    {
        _db = db;
        _mediator = mediator;
        _estruturacao = estruturacao;
    }

    public async Task<RespostaTelegramIdeia> Handle(ProcessarMensagemTelegramIdeiaCommand r, CancellationToken ct)
    {
        var texto = r.Texto.Trim();
        if (texto.Length == 0) return new RespostaTelegramIdeia();

        if (texto.StartsWith('/')) return await TratarComandoAsync(texto, ct);

        var autor = new AutorIdeia(null, string.IsNullOrWhiteSpace(r.UsuarioNome) ? "Telegram" : r.UsuarioNome);

        // Resposta a uma pergunta do bot (módulo ou duplicidade).
        if (r.RespondeMensagemId is { } respondida)
        {
            var pendente = await _db.Ideias.FirstOrDefaultAsync(i =>
                i.TelegramChatId == r.ChatId && i.PerguntaMensagemId == respondida
                && i.PerguntaPendente != TipoPerguntaIdeia.Nenhuma, ct);
            if (pendente is not null) return await ResponderPerguntaAsync(pendente, texto, autor, ct);
        }

        if (texto.Length < 15)
            return new RespostaTelegramIdeia
            {
                Texto = "Conte um pouco mais sobre a ideia (o que você gostaria que o sistema fizesse?). Para ver as instruções, envie /ajuda."
            };

        var resultado = await _mediator.Send(new RegistrarIdeiaCommand(texto, CanalIdeia.Telegram, autor,
            new OrigemTelegramIdeia(r.ChatId, r.MensagemId, r.UsuarioId, autor.Nome)), ct);

        var resposta = $"Ideia registrada!\n\nID: {resultado.Codigo}\nTítulo: {resultado.Titulo}\n" +
                       $"Módulo: {resultado.Modulo ?? "a definir"}\nCategoria: {resultado.Categoria ?? "a definir"}\n" +
                       $"Status: {AlterarStatusIdeiaCommandHandler.Rotulo(resultado.Status)}\n\n" +
                       "A ideia foi armazenada para análise posterior.";

        switch (resultado.Pergunta)
        {
            case TipoPerguntaIdeia.Duplicidade:
                resposta += $"\n\nFoi encontrada uma ideia semelhante: {resultado.IdeiaSemelhanteCodigo} — {resultado.IdeiaSemelhanteTitulo}.\n" +
                            "Responda esta mensagem com \"vincular\" para unir sua sugestão a essa ideia ou \"nova\" para manter como ideia nova.";
                break;
            case TipoPerguntaIdeia.Modulo:
                resposta += "\n\nEssa ideia está relacionada a qual módulo do sistema (ex.: SST, EPI, Qualidade, Compras)? Responda esta mensagem com o nome do módulo.";
                break;
        }

        return new RespostaTelegramIdeia
        {
            Texto = resposta,
            IdeiaId = resultado.Id,
            AguardaResposta = resultado.Pergunta != TipoPerguntaIdeia.Nenhuma
        };
    }

    private async Task<RespostaTelegramIdeia> TratarComandoAsync(string texto, CancellationToken ct)
    {
        var partes = texto.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var comando = partes[0].Split('@')[0].ToLowerInvariant();

        if (comando == "/status" && partes.Length == 2)
        {
            var codigo = partes[1].Trim().ToUpperInvariant();
            var ideia = await _db.Ideias.FirstOrDefaultAsync(i => i.Codigo == codigo, ct);
            if (ideia is null) return new RespostaTelegramIdeia { Texto = $"Não encontrei a ideia {codigo}." };
            var linha = $"{ideia.Codigo} — {ideia.Titulo}\nStatus: {AlterarStatusIdeiaCommandHandler.Rotulo(ideia.Status)}";
            if (ideia.Prioridade is not null) linha += $"\nPrioridade: {ideia.Prioridade}";
            return new RespostaTelegramIdeia { Texto = linha, IdeiaId = ideia.Id };
        }

        return new RespostaTelegramIdeia { Texto = Ajuda };
    }

    private async Task<RespostaTelegramIdeia> ResponderPerguntaAsync(Ideia ideia, string texto, AutorIdeia autor, CancellationToken ct)
    {
        if (ideia.PerguntaPendente == TipoPerguntaIdeia.Modulo)
        {
            // Tenta reconhecer o módulo pela mesma heurística; se não reconhecer, usa o que a pessoa escreveu.
            var reconhecido = (await _estruturacao.EstruturarAsync(texto, ct)).Modulo;
            ideia.Modulo = IdeiaMapeador.Limpar(reconhecido ?? texto, 120);
            ideia.PerguntaPendente = TipoPerguntaIdeia.Nenhuma;
            ideia.PerguntaMensagemId = null;
            IdeiaMapeador.Historico(_db.IdeiaHistoricos, ideia, TipoHistoricoIdeia.Classificacao,
                $"{autor.Nome} informou o módulo: {ideia.Modulo}.", autor);
            await _db.SaveChangesAsync(ct);
            return new RespostaTelegramIdeia { Texto = $"Obrigado! {ideia.Codigo} agora está no módulo {ideia.Modulo}.", IdeiaId = ideia.Id };
        }

        var resposta = HeuristicaIdeiaEstruturacaoService.Normalizar(texto).Trim();
        if (resposta is "vincular" or "sim" or "s" or "1")
        {
            if (ideia.IdeiaSemelhanteId is not { } principalId)
                return new RespostaTelegramIdeia { Texto = "Não encontrei mais a ideia semelhante para vincular." };
            await VinculoIdeia.AplicarAsync(_db, ideia.Id, principalId, autor, ct);
            await _db.SaveChangesAsync(ct);
            return new RespostaTelegramIdeia { Texto = $"Pronto, {ideia.Codigo} foi vinculada à ideia semelhante.", IdeiaId = ideia.Id };
        }
        if (resposta is "nova" or "nao" or "n" or "2")
        {
            ideia.IdeiaSemelhanteId = null;
            ideia.PerguntaPendente = TipoPerguntaIdeia.Nenhuma;
            ideia.PerguntaMensagemId = null;
            IdeiaMapeador.Historico(_db.IdeiaHistoricos, ideia, TipoHistoricoIdeia.Vinculo,
                $"{autor.Nome} manteve a ideia como nova (sugestão de duplicidade recusada).", autor);
            await _db.SaveChangesAsync(ct);
            return new RespostaTelegramIdeia { Texto = $"Certo, {ideia.Codigo} segue como ideia nova.", IdeiaId = ideia.Id };
        }

        return new RespostaTelegramIdeia
        {
            Texto = "Não entendi. Responda esta mensagem com \"vincular\" ou \"nova\".",
            IdeiaId = ideia.Id,
            AguardaResposta = false
        };
    }
}

// O Telegram só informa o id da mensagem do bot depois do envio; este comando liga a pergunta à ideia.
public record RegistrarMensagemPerguntaIdeiaCommand(Guid IdeiaId, long MensagemId) : IRequest;

public class RegistrarMensagemPerguntaIdeiaCommandHandler : IRequestHandler<RegistrarMensagemPerguntaIdeiaCommand>
{
    private readonly IAppDbContext _db;

    public RegistrarMensagemPerguntaIdeiaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarMensagemPerguntaIdeiaCommand r, CancellationToken ct)
    {
        var ideia = await _db.Ideias.FirstOrDefaultAsync(i => i.Id == r.IdeiaId, ct);
        if (ideia is null || ideia.PerguntaPendente == TipoPerguntaIdeia.Nenhuma) return;
        ideia.PerguntaMensagemId = r.MensagemId;
        await _db.SaveChangesAsync(ct);
    }
}
