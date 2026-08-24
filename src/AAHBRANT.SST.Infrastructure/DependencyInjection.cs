using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Integracao;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SstDatabase")
            ?? throw new InvalidOperationException("Connection string 'SstDatabase' não configurada.");

        services.AddDbContext<SstDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<SstDbContext>());

        // Precisa estar registrado aqui (não só na Api) — todo composition root que usa
        // SstDbContext depende disso, incluindo o Worker (sem HttpContext/usuário logado; ver
        // CurrentUserService sobre por que o padrão "acesso global" é o correto lá).
        services.AddScoped<ICurrentUserService, CurrentUserService>();

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
        // TelegramUpdatesPollingService sobre o comportamento com a configuração vazia. Enviar
        // mensagem (ITelegramService) é seguro em qualquer processo; só o LONG POLLING de updates
        // (AddPollingDeAtualizacoesTelegram, abaixo) não pode rodar em mais de um processo ao mesmo
        // tempo — por isso ficou separado, para o Worker de alertas poder usar AddInfrastructure
        // sem também herdar o polling que já roda na Api.
        services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
        services.AddHttpClient();
        services.AddScoped<ITelegramService, TelegramBotService>();

        services.AddScoped<IDdsPdfService, DdsPdfService>();

        return services;
    }

    // Long polling do Telegram (getUpdates) — só pode rodar em UM processo por vez (rodar em dois
    // ao mesmo tempo causa 409/updates perdidos na API do Telegram). Chamado só pela Api; o Worker
    // de alertas automáticos (AAHBRANT.SST.Worker) usa AddInfrastructure sem isto.
    public static IServiceCollection AddPollingDeAtualizacoesTelegram(this IServiceCollection services)
    {
        services.AddHostedService<TelegramUpdatesPollingService>();
        return services;
    }
}
