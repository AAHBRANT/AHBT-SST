using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Behaviors;
using AAHBRANT.SST.Application.Elegibilidade;
using AAHBRANT.SST.Application.Elegibilidade.Rules;
using AAHBRANT.SST.Domain.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<IEligibilityRule, AsoValidoRule>();
        services.AddScoped<IEligibilityRule, TreinamentoValidoRule>();
        services.AddScoped<IEligibilityRule, AprValidaRule>();
        services.AddScoped<IEligibilityRule, PermissaoTrabalhoValidaRule>();
        services.AddScoped<IEligibilityService, EligibilityService>();

        services.AddScoped<IAlertaOrigemProvider, AsoAlertaProvider>();
        services.AddScoped<IAlertaOrigemProvider, TreinamentoAlertaProvider>();
        services.AddScoped<IAlertaOrigemProvider, ExtintorAlertaProvider>();
        services.AddScoped<IAlertaOrigemProvider, EquipamentoAlertaProvider>();
        services.AddScoped<IAlertaOrigemProvider, EpiAlertaProvider>();
        services.AddScoped<IAlertaOrigemProvider, DocumentoAlertaProvider>();
        services.AddScoped<IAlertaOrigemProvider, AcaoPlanoAlertaProvider>();
        services.AddScoped<IAlertaEngineService, AlertaEngineService>();

        return services;
    }
}
