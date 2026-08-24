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
        services.AddHostedService<TelegramUpdatesPollingService>();

        services.AddScoped<IDdsPdfService, DdsPdfService>();

        return services;
    }
}
