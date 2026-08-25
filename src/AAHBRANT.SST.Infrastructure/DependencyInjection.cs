using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Integracao;
using AAHBRANT.SST.Infrastructure.Integracao.Bot;
using AAHBRANT.SST.Infrastructure.Integracao.Teams;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool habilitarPollingTelegram = true)
    {
        var connectionString = configuration.GetConnectionString("SstDatabase")
            ?? throw new InvalidOperationException("Connection string 'SstDatabase' não configurada.");

        services.AddDbContext<SstDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<SstDbContext>());

        // Chaves de criptografia/hash do CPF (LGPD) — carregadas uma única vez aqui porque o
        // ValueConverter e a IEntityTypeConfiguration são instanciados por reflection pelo EF Core,
        // sem passar por injeção de dependência. Ver CpfCriptografiaConversor.
        var chaveCriptografiaCpf = configuration["Lgpd:ChaveCriptografiaCpfBase64"];
        var chaveHashCpf = configuration["Lgpd:ChaveHashCpfBase64"];
        if (string.IsNullOrWhiteSpace(chaveCriptografiaCpf))
            throw new InvalidOperationException("Configuração 'Lgpd:ChaveCriptografiaCpfBase64' não configurada.");
        if (string.IsNullOrWhiteSpace(chaveHashCpf))
            throw new InvalidOperationException("Configuração 'Lgpd:ChaveHashCpfBase64' não configurada.");
        CpfCriptografiaContexto.Configurar(Convert.FromBase64String(chaveCriptografiaCpf), Convert.FromBase64String(chaveHashCpf));

        // Integração Telegram (DDS Fase 3) — token/username ficam vazios até o usuário criar o
        // bot via @BotFather e preencher appsettings; ver disclosures em TelegramBotService/
        // TelegramUpdatesPollingService sobre o comportamento com a configuração vazia.
        services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
        services.AddHttpClient();
        services.AddScoped<ITelegramService, TelegramBotService>();

        // O Telegram getUpdates (long polling) só permite um consumidor simultâneo por bot token.
        // A Api já roda esse polling; o Worker (AlertaEngineWorker) chama AddInfrastructure com
        // habilitarPollingTelegram: false para não abrir um segundo consumidor e causar 409 Conflict.
        if (habilitarPollingTelegram)
            services.AddHostedService<TelegramUpdatesPollingService>();

        services.AddScoped<IDdsPdfService, DdsPdfService>();

        // Motor Central de Alertas, Etapa 4 — notificação de "sino" do Teams (Activity Feed) via
        // Microsoft Graph (POST /users/{aadObjectId}/teamwork/sendActivityNotification), sem Bot
        // Framework/Bot Channels Registration. TenantId/ClientId/ClientSecret ficam vazios até o App
        // Registration com a permissão de aplicativo TeamsActivity.Send ser provisionado no Entra ID —
        // ver GraphActivityNotificacaoTeamsService, que lança exceção graciosamente enquanto isso.
        services.Configure<GraphOptions>(configuration.GetSection("Graph"));
        services.AddScoped<INotificacaoTeamsService, GraphActivityNotificacaoTeamsService>();

        // Fila de retry para falhas de envio (PROJECT RULES.md §4). Usa Azure Service Bus quando
        // "ServiceBus:ConnectionString" estiver preenchida (recurso provisionado manualmente no
        // Azure); caso contrário, cai para um fallback local em memória — não bloqueia a aplicação
        // nem exige nenhum recurso externo para rodar em desenvolvimento/CI.
        services.Configure<ServiceBusOptions>(configuration.GetSection("ServiceBus"));
        var serviceBusConnectionString = configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(serviceBusConnectionString))
        {
            services.AddSingleton(sp => new ServiceBusClient(serviceBusConnectionString));
            services.AddSingleton<IFilaNotificacaoTeams, ServiceBusFilaNotificacaoTeams>();
            services.AddHostedService<ServiceBusNotificacaoTeamsProcessor>();
        }
        else
        {
            services.AddSingleton<InMemoryFilaNotificacaoTeams>();
            services.AddSingleton<IFilaNotificacaoTeams>(sp => sp.GetRequiredService<InMemoryFilaNotificacaoTeams>());
            services.AddHostedService<InMemoryNotificacaoTeamsProcessor>();
        }

        return services;
    }
}
