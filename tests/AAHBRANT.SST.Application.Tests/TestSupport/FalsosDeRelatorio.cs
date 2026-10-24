using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;

namespace AAHBRANT.SST.Application.Tests.TestSupport;

// IMediator controlado pelo teste: a função recebe a requisição e devolve a resposta.
public sealed class MediatorSimulado : IMediator
{
    private readonly Func<object, object?> _responder;
    public List<object> Requisicoes { get; } = new();

    public MediatorSimulado(Func<object, object?> responder) => _responder = responder;

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Requisicoes.Add(request);
        return Task.FromResult((TResponse)_responder(request)!);
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
}

public sealed class TelegramSimulado : ITelegramResumoService
{
    public List<string> Textos { get; } = new();
    public List<(byte[] Png, string Legenda)> Imagens { get; } = new();
    public List<(byte[] Arquivo, string Nome, string Legenda)> Documentos { get; } = new();
    public bool ImagemFunciona { get; init; } = true;
    public bool DocumentoFunciona { get; init; } = true;
    public bool LancarExcecao { get; init; }

    public Task EnviarAsync(string mensagem, CancellationToken ct = default)
    {
        if (LancarExcecao) throw new InvalidOperationException("Telegram fora do ar");
        Textos.Add(mensagem);
        return Task.CompletedTask;
    }

    public Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default)
    {
        if (LancarExcecao) throw new InvalidOperationException("Telegram fora do ar");
        if (!ImagemFunciona) return Task.FromResult(false);
        Imagens.Add((imagemPng, legenda));
        return Task.FromResult(true);
    }

    public Task<bool> EnviarDocumentoAsync(byte[] arquivo, string nomeArquivo, string legenda, CancellationToken ct = default)
    {
        if (!DocumentoFunciona) return Task.FromResult(false);
        Documentos.Add((arquivo, nomeArquivo, legenda));
        return Task.FromResult(true);
    }
}

public sealed class SininhoSimulado : INotificacaoTeamsService
{
    public List<(Guid UsuarioId, string Titulo, string? Descricao)> Enviadas { get; } = new();
    public HashSet<Guid> FalhaPara { get; } = new();

    public Task<bool> EnviarAsync(Guid usuarioId, string titulo, string? descricao, CancellationToken ct = default)
    {
        if (FalhaPara.Contains(usuarioId)) throw new InvalidOperationException("Usuário sem AzureAdObjectId.");
        Enviadas.Add((usuarioId, titulo, descricao));
        return Task.FromResult(true);
    }
}
