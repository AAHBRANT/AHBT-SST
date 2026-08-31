using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Middlewares;
using AAHBRANT.SST.Application;
using AAHBRANT.SST.Infrastructure;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPollingDeAtualizacoesTelegram();

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

    // Sem isso, o ASP.NET Core renomeia claims curtas do token ("oid", "sub") para URIs longas
    // (ClaimTypes.*) por padrão, então VinculoAzureAdMiddleware.FindFirst("oid") nunca encontra
    // nada e cai no fallback (sub), que não é GUID e estoura a coluna AzureAdObjectId (nvarchar(36)) —
    // descoberto ao vivo: toda request autenticada quebrava com DbUpdateException/truncation.
    builder.Services.Configure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
        JwtBearerDefaults.AuthenticationScheme,
        options => options.MapInboundClaims = false);
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
// ICurrentUserService (camada 3 do RBAC) é registrado em AddInfrastructure — ver EscopoPorObraMiddleware.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string PoliticaCorsDev = "TeamsTabDev";
const string PoliticaCorsProd = "TeamsTabProd";
var origemPermitidaProd = builder.Configuration["Cors:AllowedOrigin"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(PoliticaCorsDev, policy =>
        // Qualquer porta em localhost é aceita em dev porque o Vite muda de porta
        // automaticamente quando a 5173 já está em uso (ex.: múltiplas sessões).
        policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
              .AllowAnyHeader()
              .AllowAnyMethod());

    if (!string.IsNullOrWhiteSpace(origemPermitidaProd))
    {
        options.AddPolicy(PoliticaCorsProd, policy =>
            policy.WithOrigins(origemPermitidaProd)
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    }
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
    if (!string.IsNullOrWhiteSpace(origemPermitidaProd))
    {
        app.UseCors(PoliticaCorsProd);
    }
}

if (autenticacaoEntraIdHabilitada)
{
    app.UseAuthentication();
    app.UseMiddleware<VinculoAzureAdMiddleware>();
    app.UseAuthorization();
}

// Depois da autenticação, antes de qualquer controller: resolve o escopo por obra do usuário da
// requisição (camada 3 do RBAC — ver SstDbContext) para que o filtro global já esteja pronto
// quando o primeiro DbSet for consultado.
app.UseMiddleware<EscopoPorObraMiddleware>();

// Depois da autenticação de propósito: a chave de idempotência (sincronização offline) só deve
// devolver uma resposta em cache para quem já passou pelo crivo de auth da requisição original —
// caso contrário um Idempotency-Key adivinhado (improvável, é um GUID, mas por princípio) poderia
// vazar a resposta de outro usuário sem autenticação nenhuma.
app.UseMiddleware<IdempotenciaMiddleware>();

// Aplica migrations pendentes automaticamente no start — antes não existia isso no código
// (schema do banco de homologação era atualizado manualmente a cada nova migration).
using (var escopoMigracao = app.Services.CreateScope())
{
    var db = escopoMigracao.ServiceProvider.GetRequiredService<SstDbContext>();
    await db.Database.MigrateAsync();
}

await RbacSeeder.ExecutarAsync(app.Services);
await CpfLgpdBackfillSeeder.ExecutarAsync(app.Services);
await RegraAlertaSeeder.ExecutarAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    await MockObraSeeder.ExecutarAsync(app.Services);
}

app.MapControllers();

app.Run();
