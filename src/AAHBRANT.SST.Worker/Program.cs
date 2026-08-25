using AAHBRANT.SST.Application;
using AAHBRANT.SST.Infrastructure;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using AAHBRANT.SST.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
// habilitarPollingTelegram: false — a Api já roda o long polling do Telegram; ver
// AAHBRANT.SST.Infrastructure.DependencyInjection para o motivo.
builder.Services.AddInfrastructure(builder.Configuration, habilitarPollingTelegram: false);

builder.Services.AddHostedService<AlertaEngineWorker>();

var host = builder.Build();

await RegraAlertaSeeder.ExecutarAsync(host.Services);

host.Run();
