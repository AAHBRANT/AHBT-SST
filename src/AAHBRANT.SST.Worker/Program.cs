using AAHBRANT.SST.Application;
using AAHBRANT.SST.Infrastructure;
using AAHBRANT.SST.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// Sem AddPollingDeAtualizacoesTelegram() de propósito: esse long polling já roda na Api e não
// pode rodar em dois processos ao mesmo tempo (ver Infrastructure/DependencyInjection.cs).

builder.Services.Configure<AlertasOptions>(builder.Configuration.GetSection("Alertas"));
builder.Services.AddHostedService<VerificacaoAlertasBackgroundService>();

var host = builder.Build();
host.Run();
