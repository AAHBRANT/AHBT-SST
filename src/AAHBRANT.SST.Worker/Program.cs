using AAHBRANT.SST.Application;
using AAHBRANT.SST.Infrastructure;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using AAHBRANT.SST.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// QuestPDF gera a imagem e o PDF dos relatorios: a licenca precisa estar definida em cada processo (a Api faz o mesmo).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<AlertaEngineWorker>();
builder.Services.AddHostedService<RelatoriosAgendadosWorker>();

var host = builder.Build();

await RegraAlertaSeeder.ExecutarAsync(host.Services);

host.Run();
