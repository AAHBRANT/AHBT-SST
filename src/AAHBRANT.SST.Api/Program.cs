using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Middlewares;
using AAHBRANT.SST.Application;
using AAHBRANT.SST.Infrastructure;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Autenticação Entra ID: só é ativada se a seção "AzureAd" estiver configurada com um
// App Registration real (TenantId/ClientId). Provisionamento desse recurso no Azure
// depende de confirmação explícita do usuário — até lá, a API roda localmente sem auth.
var azureAdSection = builder.Configuration.GetSection("AzureAd");
var autenticacaoEntraIdHabilitada = azureAdSection.Exists() && !string.IsNullOrWhiteSpace(azureAdSection["TenantId"]);
if (autenticacaoEntraIdHabilitada)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(azureAdSection);
}

// Enforcement real de autorização (estrutura pronta — ver docs/RBAC-Matrix.md §4, camada 1). Qualquer
// [Authorize(Policy = "modulo:acao")] nos controllers é resolvido dinamicamente como um Permissao.Codigo
// pelo PermissaoAuthorizationHandler. Registrado SEMPRE (não só quando autenticacaoEntraIdHabilitada),
// porque o ASP.NET Core já registra por padrão um provider/serviço de autorização mínimos mesmo sem
// AddAuthorization() explícito (necessário para o [Authorize] funcionar em minimal hosting) — descoberto
// testando ao vivo: sem substituir o provider default, qualquer nome de policy desconhecido lança
// InvalidOperationException por request, mesmo com o Entra ID desligado. O próprio PermissaoAuthorizationHandler
// checa a mesma flag internamente e libera (Succeed) sem checagem real enquanto o TenantId não existir,
// então nada muda no comportamento observável da API hoje — só passa a existir a estrutura.
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissaoAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissaoAuthorizationHandler>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string PoliticaCorsDev = "TeamsTabDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(PoliticaCorsDev, policy =>
        // Qualquer porta em localhost é aceita em dev porque o Vite muda de porta
        // automaticamente quando a 5173 já está em uso (ex.: múltiplas sessões).
        policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<TratamentoDeExcecaoMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(PoliticaCorsDev);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (autenticacaoEntraIdHabilitada)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

await RbacSeeder.ExecutarAsync(app.Services);
await CpfLgpdBackfillSeeder.ExecutarAsync(app.Services);

app.MapControllers();

app.Run();
