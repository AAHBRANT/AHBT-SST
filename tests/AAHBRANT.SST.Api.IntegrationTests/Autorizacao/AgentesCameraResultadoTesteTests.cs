using System.Text;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Api.Middlewares;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Rota anônima do resultado do "Testar conexão" (código do motivo, conflito tratado como reenvio) e o 409 global do
// TratamentoDeExcecaoMiddleware, que não pode devolver credencial cifrada, segredo do agente nem a miniatura do teste.
public class AgentesCameraResultadoTesteTests
{
    private const string Segredo = "segredo-certo";

    private sealed class Autenticador : IAgenteCameraAutenticador
    {
        public AgenteCamera Agente { get; } = new() { ObraId = Guid.NewGuid(), Nome = "PC" };
        public Task<AgenteCamera?> ValidarAsync(Guid agenteId, string? segredo, CancellationToken ct)
            => Task.FromResult<AgenteCamera?>(agenteId == Agente.Id && segredo == Segredo ? Agente : null);
    }

    private sealed class Mediator : IMediator
    {
        public Func<object, object?> Resposta { get; set; } = _ => true;
        public List<object> Comandos { get; } = new();

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Comandos.Add(request);
            return Task.FromResult((TResponse)Resposta(request)!);
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    private static (AgentesCameraController Controller, Autenticador Autenticador, Mediator Mediator) Montar(string json)
    {
        var autenticador = new Autenticador();
        var mediator = new Mediator();
        var controller = new AgentesCameraController(mediator, autenticador, new LimitadorAgenteCamera(), NullLogger<AgentesCameraController>.Instance);
        var http = new DefaultHttpContext();
        http.Request.ContentType = "application/json";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        http.Request.Headers[AgentesCameraController.CabecalhoSegredo] = Segredo;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, autenticador, mediator);
    }

    [Fact]
    public async Task Resultado_EncaminhaOCodigoDoAgenteAoComando()
    {
        var (controller, autenticador, mediator) = Montar("""{"sucesso":false,"codigo":"ffmpeg_ausente","motivo":"O agente não encontrou o ffmpeg","detalhe":"x"}""");

        var r = await controller.RegistrarResultadoTesteConexao(autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<NoContentResult>(r);
        var comando = Assert.IsType<RegistrarResultadoTesteConexaoCameraCommand>(Assert.Single(mediator.Comandos));
        Assert.Equal("ffmpeg_ausente", comando.Codigo);
        Assert.False(comando.Sucesso);
    }

    [Fact]
    public async Task Resultado_ConflitoDeGravacao_ViraReenvio204_SemCorpo()
    {
        var (controller, autenticador, mediator) = Montar("""{"sucesso":true,"miniaturaBase64":"/9gB"}""");
        mediator.Resposta = _ => throw new DbUpdateConcurrencyException("conflito");

        var r = await controller.RegistrarResultadoTesteConexao(autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<NoContentResult>(r);
    }

    // ---------- 409 global ----------
    // O 409 monta o dadosAtuais com TODAS as colunas da linha (GetDatabaseValues, que o provedor em memória não implementa):
    // por isso o teste exercita o filtro de colunas que o middleware aplica a esse conjunto, com as colunas reais de cada tipo.

    private static Dictionary<string, object?> ColunasDe(Type tipo, params (string Nome, object? Valor)[] colunas)
        => TratamentoDeExcecaoMiddleware.ColunasDoConflito(tipo, colunas.Select(c => new KeyValuePair<string, object?>(c.Nome, c.Valor)))
            .ToDictionary(c => c.Key, c => c.Value);

    [Fact]
    public void Conflito409_NaoDevolveCredencialCifrada_SegredoDoAgenteNemMiniatura()
    {
        var equipamento = ColunasDe(typeof(EquipamentoCamera),
            (nameof(EquipamentoCamera.Host), "10.0.0.5"), (nameof(EquipamentoCamera.SenhaCifrada), "SENHA-CIFRADA-XYZ"));
        var teste = ColunasDe(typeof(TesteConexaoCamera),
            (nameof(TesteConexaoCamera.Motivo), "ok"), (nameof(TesteConexaoCamera.Miniatura), new byte[] { 0xFF, 0xD8 }));
        var agente = ColunasDe(typeof(AgenteCamera),
            (nameof(AgenteCamera.Nome), "PC"), (nameof(AgenteCamera.SegredoHash), "HASH-DO-SEGREDO"));

        Assert.False(equipamento.ContainsKey(nameof(EquipamentoCamera.SenhaCifrada)));
        Assert.Equal("10.0.0.5", equipamento[nameof(EquipamentoCamera.Host)]); // o resto da linha continua saindo
        Assert.False(teste.ContainsKey(nameof(TesteConexaoCamera.Miniatura)));
        Assert.Equal("ok", teste[nameof(TesteConexaoCamera.Motivo)]);
        Assert.False(agente.ContainsKey(nameof(AgenteCamera.SegredoHash)));
        Assert.Equal("PC", agente[nameof(AgenteCamera.Nome)]);
    }

    [Fact]
    public void Conflito409_DeOutraEntidade_ContinuaDevolvendoTodasAsColunas()
    {
        var colunas = ColunasDe(typeof(Obra), (nameof(Obra.Nome), "Obra"), ("SenhaCifrada", "x"), ("Miniatura", "y"));

        Assert.Equal(3, colunas.Count);
    }
}
