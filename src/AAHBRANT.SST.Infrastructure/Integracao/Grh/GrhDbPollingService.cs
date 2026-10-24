using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Asos.Commands;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Sincronização periódica de Colaborador/Alojamento/ASO lendo direto do G-RH (Integração G-RH,
// 2026-09-16, Colaborador incluído em 22/09 — ver auditoria abaixo). Não é tempo real de verdade:
// cada rodada relê o estado atual inteiro do lado do G-RH (mesmo comando de upsert usado pela
// importação manual — ImportarColaboradoresGrhCommand/ImportarAlojamentosGrhCommand/
// ImportarAsoGrhCommand, todos idempotentes por CPF/CNPJ), portanto o atraso máximo pra uma mudança
// aparecer no SST é o intervalo configurado (GrhDb:IntervaloPollingMinutos, padrão 5 minutos).
//
// Colaborador (22/09): até aqui, a ÚNICA forma de um colaborador novo entrar automaticamente era o
// evento de Service Bus (ServiceBusColaboradorGrhProcessor) — sem nenhuma rede de segurança. Um
// evento que falhasse (ex.: Obra/Situação/Cargo com valor que SincronizarColaboradorGrhCommand não
// reconhece) era só reenfileirado (AbandonMessageAsync) até estourar o limite de tentativas e cair
// no dead-letter, SEM NINGUÉM SER AVISADO — foi exatamente assim que um colaborador (Hamilton Costa
// Gomes) ficou de fora até alguém notar manualmente. Colaborador entra aqui pra ganhar a mesma rede
// de segurança que Alojamento/ASO já tinham: mesmo se o evento se perder, esta rodada corrige
// sozinha em até IntervaloPollingMinutos. Roda primeiro (antes de Alojamento/ASO), porque os dois
// vinculam por CPF a um Trabalhador que precisa existir.
public class GrhDbPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GrhDbOptions _opcoes;
    private readonly ILogger<GrhDbPollingService> _logger;

    public GrhDbPollingService(
        IServiceScopeFactory scopeFactory,
        IOptions<GrhDbOptions> opcoes,
        ILogger<GrhDbPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(Math.Max(1, _opcoes.IntervaloPollingMinutos));
        using var timer = new PeriodicTimer(intervalo);

        do
        {
            await SincronizarUmaRodadaAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SincronizarUmaRodadaAsync(CancellationToken ct)
    {
        using var escopo = _scopeFactory.CreateScope();
        var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var resultadoColaboradores = await mediator.Send(new ImportarColaboradoresGrhCommand(), ct);
            if (resultadoColaboradores.Erros.Count > 0)
                _logger.LogWarning(
                    "Polling G-RH (Colaborador): {Sincronizados}/{Recebidos} sincronizados, {Erros} erro(s).",
                    resultadoColaboradores.TotalSincronizados, resultadoColaboradores.TotalRecebidos, resultadoColaboradores.Erros.Count);
        }
        catch (Exception ex)
        {
            // Grh:ClientSecret vazio (integração de Colaborador ainda não provisionada) cai aqui
            // também — ColaboradorGrhClient lança a mesma InvalidOperationException graciosa que
            // AlojamentoGrhDbClient/AsoGrhDbClient já lançam pra ConnectionString vazia.
            _logger.LogError(ex, "Falha na rodada de polling do G-RH (Colaborador).");
        }

        try
        {
            var resultadoAlojamentos = await mediator.Send(new ImportarAlojamentosGrhCommand(), ct);
            if (resultadoAlojamentos.Erros.Count > 0)
                _logger.LogWarning(
                    "Polling G-RH (Alojamento): {Sincronizados}/{Recebidos} sincronizados, {Erros} erro(s).",
                    resultadoAlojamentos.TotalSincronizados, resultadoAlojamentos.TotalRecebidos, resultadoAlojamentos.Erros.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na rodada de polling do G-RH (Alojamento).");
        }

        try
        {
            var resultadoAsos = await mediator.Send(new ImportarAsoGrhCommand(), ct);
            if (resultadoAsos.Erros.Count > 0)
                _logger.LogWarning(
                    "Polling G-RH (ASO): {Sincronizados}/{Recebidos} sincronizados, {Erros} erro(s) (esperado enquanto o cadastro de validade no G-RH está em andamento).",
                    resultadoAsos.TotalSincronizados, resultadoAsos.TotalRecebidos, resultadoAsos.Erros.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na rodada de polling do G-RH (ASO).");
        }
    }
}
