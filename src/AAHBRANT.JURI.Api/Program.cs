using System.Text.Encodings.Web;
using System.Text.Json;
using AAHBRANT.JURI.Api.Modelos;
using AAHBRANT.JURI.Api.Servicos;
using Microsoft.AspNetCore.Mvc;

// G-JURI — API de busca de processos (MVP, 2026-09-15).
// Duas fontes públicas do CNJ combinadas:
//   • DJEN  (comunicaapi.pje.jus.br)  → DESCOBRE processos por nome da parte e traz intimações.
//   • DataJud (api-publica.datajud.cnj.jus.br) → DETALHA um processo conhecido pelo número CNJ.
// Sem banco, sem autenticação e sem agendamento nesta versão: só as rotas de consulta.

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DjenOptions>(builder.Configuration.GetSection("Djen"));
builder.Services.Configure<DataJudOptions>(builder.Configuration.GetSection("DataJud"));
builder.Services.Configure<List<ParteMonitoradaConfig>>(builder.Configuration.GetSection("PartesMonitoradas"));

builder.Services.AddHttpClient<DjenClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(90);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("AAHBRANT-GJURI/0.1");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddHttpClient<DataJudClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(60);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("AAHBRANT-GJURI/0.1");
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping; // acentos legíveis no JSON
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "G-JURI — Busca de Processos", Version = "v1" }));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.RoutePrefix = string.Empty; // Swagger na raiz: http://localhost:5210/
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "G-JURI v1"); // caminho absoluto: com prefixo vazio o relativo vira /v1/swagger.json (404)
});
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok", agora = DateTime.UtcNow }));

var grupo = app.MapGroup("/api/processos");

// 1) Busca livre por nome da parte (DJEN). O DJEN é um único diário NACIONAL — a busca já cobre
// todos os tribunais do Brasil (TJs, TRTs, TRFs, TREs, STJ, STF) numa chamada só, sem precisar de
// conector por estado/tribunal. justica: todas (padrão) | trabalhista | estadual | federal | eleitoral.
grupo.MapGet("/buscar", async (
        [FromQuery] string nome,
        [FromQuery] string? justica,
        [FromQuery] string? tribunal,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? ate,
        [FromQuery] bool? incluirComunicacoes,
        [FromQuery] bool? incluirTexto,
        DjenClient djen,
        CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length < 3)
            return Results.BadRequest(new { erro = "Informe 'nome' com pelo menos 3 caracteres." });

        var comunicacoes = await djen.BuscarAsync(nome.Trim(), tribunal, null, desde, ate, ct);
        return Results.Ok(DjenClient.Agrupar(comunicacoes, nome.Trim(), justica ?? "todas", incluirComunicacoes ?? false, incluirTexto ?? false));
    })
    .WithName("BuscarProcessosPorNome")
    .WithSummary("Descobre processos por nome da parte (DJEN/CNJ), agrupados por número CNJ.");

// 2) Processos das partes configuradas em appsettings (AAHBRANT + consórcios).
grupo.MapGet("/monitorados", async (
        [FromQuery] string? justica,
        [FromQuery] DateOnly? desde,
        [FromQuery] bool? incluirComunicacoes,
        DjenClient djen,
        Microsoft.Extensions.Options.IOptions<List<ParteMonitoradaConfig>> partes,
        CancellationToken ct) =>
    {
        var resultado = new List<ParteMonitoradaResultado>();

        foreach (var parte in partes.Value)
        {
            var item = new ParteMonitoradaResultado { Nome = parte.Nome, Cnpj = parte.Cnpj, Termos = parte.Termos };
            try
            {
                var todas = new List<DjenComunicacao>();
                foreach (var termo in parte.Termos.Count > 0 ? parte.Termos : new List<string> { parte.Nome })
                    todas.AddRange(await djen.BuscarAsync(termo, null, null, desde, null, ct));

                var agrupado = DjenClient.Agrupar(
                    todas.DistinctBy(c => c.Id), parte.Nome, justica ?? "todas", incluirComunicacoes ?? false, false);
                item.Processos = agrupado.Processos;
                item.TotalProcessos = agrupado.TotalProcessos;
                item.PorTribunal = agrupado.PorTribunal;
            }
            catch (Exception ex)
            {
                item.Erro = ex.Message;
            }
            resultado.Add(item);
        }

        return Results.Ok(new
        {
            justica = justica ?? "todas",
            totalProcessos = resultado.Sum(r => r.TotalProcessos),
            partes = resultado,
        });
    })
    .WithName("ProcessosDasPartesMonitoradas")
    .WithSummary("Todos os processos (Brasil inteiro, todas as justiças) das partes configuradas em PartesMonitoradas.");

// 3) Detalhe de um processo por número CNJ: DataJud (movimentos) + DJEN (intimações).
grupo.MapGet("/{numero}", async (
        string numero,
        [FromQuery] int? maxMovimentos,
        [FromQuery] bool? incluirTexto,
        DjenClient djen,
        DataJudClient dataJud,
        CancellationToken ct) =>
    {
        if (!NumeroCnj.EhValido(numero))
            return Results.BadRequest(new { erro = "Número CNJ inválido: esperado 20 dígitos (NNNNNNN-DD.AAAA.J.TR.OOOO)." });

        var limpo = NumeroCnj.Limpar(numero);
        var tribunal = NumeroCnj.ResolverTribunal(limpo);
        var detalhe = new ProcessoDetalheDto
        {
            Numero = limpo,
            NumeroFormatado = NumeroCnj.Formatar(limpo),
            Tribunal = tribunal?.Sigla,
            AliasDataJud = tribunal?.Alias,
            Justica = NumeroCnj.Justica(limpo),
        };

        var tarefaDataJud = tribunal is null
            ? Task.FromResult((new List<ProcessoDataJudDto>(), (string?)"Segmento da Justiça não mapeado para alias DataJud."))
            : dataJud.ConsultarPorNumeroAsync(limpo, tribunal.Value.Alias, maxMovimentos ?? 0, ct);

        var tarefaDjen = djen.BuscarAsync(null, null, limpo, null, null, ct);

        try
        {
            var (processos, erro) = await tarefaDataJud;
            detalhe.DataJud = processos;
            detalhe.DataJudErro = erro;
        }
        catch (Exception ex) { detalhe.DataJudErro = ex.Message; }

        try
        {
            var comunicacoes = await tarefaDjen;
            detalhe.Djen = comunicacoes.Count > 0 ? DjenClient.MontarResumo(limpo, comunicacoes, true, incluirTexto ?? false) : null;
            if (comunicacoes.Count == 0) detalhe.DjenErro = "Nenhuma comunicação publicada no DJEN para este número.";
        }
        catch (Exception ex) { detalhe.DjenErro = ex.Message; }

        if (detalhe.DataJud.Count == 0 && detalhe.Djen is null)
            return Results.NotFound(detalhe);

        return Results.Ok(detalhe);
    })
    .WithName("DetalharProcesso")
    .WithSummary("Detalha um processo pelo número CNJ: movimentos (DataJud) e intimações (DJEN).");

app.Run();
