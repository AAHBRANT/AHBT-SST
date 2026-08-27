using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Auditoria;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Integracao;
using AAHBRANT.SST.Infrastructure.Integracao.Bot;
using AAHBRANT.SST.Infrastructure.Integracao.Teams;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Trabalhadores;
using Azure.Messaging.ServiceBus;
using Fido2NetLib;
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

        // Chave de criptografia do template biométrico (Futronic) — mesmo padrão acima. É uma chave
        // simétrica dedicada (não reaproveita a do CPF) porque também é distribuída, fora de banda,
        // para o AAHBRANT.SST.AgenteBiometria (processo Windows separado, fora deste container/App
        // Service) que precisa descriptografar os templates sincronizados; ver TemplateCacheService.
        var chaveCriptografiaBiometria = configuration["Lgpd:ChaveCriptografiaBiometriaBase64"];
        if (string.IsNullOrWhiteSpace(chaveCriptografiaBiometria))
            throw new InvalidOperationException("Configuração 'Lgpd:ChaveCriptografiaBiometriaBase64' não configurada.");
        TemplateBiometricoCriptografiaContexto.Configurar(Convert.FromBase64String(chaveCriptografiaBiometria));

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
        services.AddScoped<IFichaEpiPdfService, EntregaEpiPdfService>();
        services.AddScoped<IRelatorioFiscalizacaoPdfService, RelatorioFiscalizacaoPdfService>();

        // Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5, etapa 4) — crachá/QR
        // + PIN é o método de reserva e roda como principal temporário até o leitor biométrico FIDO2
        // ser confirmado; a estratégia biométrica (Fido2AutenticacaoStrategy) troca este registro
        // quando implementada, sem exigir mudança no restante da aplicação (depende só da abstração).
        services.AddScoped<IAutenticacaoAssinaturaService, CrachaPinAutenticacaoStrategy>();
        services.AddScoped<IPinHasher, PinHasherService>();
        services.AddScoped<ISegredoDispositivoHasher, SegredoDispositivoHasherService>();
        services.AddScoped<ITemplateBiometricoCriptografia, TemplateBiometricoCriptografiaService>();
        services.AddScoped<IDispositivoAgenteAutenticador, DispositivoAgenteAutenticador>();
        services.AddScoped<IAutenticacaoBiometriaLocalService, FutronicAutenticacaoStrategy>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();
        services.AddScoped<IDocumentoAssinaturaPdfService, DocumentoAssinaturaPdfService>();
        services.Configure<AssinaturaOptions>(configuration.GetSection("Assinatura"));
        services.AddScoped<IQrCodeDocumentoService, QrCodeDocumentoService>();
        services.AddScoped<IRegistradorAssinaturaService, RegistradorAssinaturaService>();

        // Estratégia biométrica (etapa 13) — ServerDomain/Origins ficam vazios até o domínio de
        // produção (e o leitor FIDO2 da obra) serem confirmados; Fido2AutenticacaoStrategy só falha
        // quando efetivamente usada, mesmo padrão de tolerância de GraphOptions/TelegramOptions.
        var fido2Options = configuration.GetSection("Fido2").Get<Fido2Options>() ?? new Fido2Options();
        services.Configure<Fido2Options>(configuration.GetSection("Fido2"));
        services.AddSingleton<IFido2>(sp => new Fido2NetLib.Fido2(new Fido2Configuration
        {
            ServerDomain = fido2Options.ServerDomain,
            ServerName = fido2Options.ServerName,
            Origins = fido2Options.Origins.ToHashSet(),
        }));
        services.AddScoped<IAutenticacaoWebAuthnService, Fido2AutenticacaoStrategy>();

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
