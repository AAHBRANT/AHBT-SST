using AAHBRANT.SST.Application;
using AAHBRANT.SST.Infrastructure;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using AAHBRANT.SST.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<AlertaEngineWorker>();

var host = builder.Build();

await RegraAlertaSeeder.ExecutarAsync(host.Services);

host.Run();
