# Assinatura Facial via Azure Face API — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar reconhecimento facial (Azure Face API) como um método adicional dentro do Motor de Assinatura Eletrônica já existente — cadastro (enrollment) na aba de assinatura do perfil do trabalhador, e assinatura (via `Identify`) no quiosque de assinatura já usado por DDS/Ficha de EPI/PT/Inspeção/Treinamento.

**Architecture:** Novo `IAutenticacaoFacialService` (Infrastructure), no mesmo papel de `IAutenticacaoBiometriaLocalService`/`FutronicAutenticacaoStrategy`, chamando a Azure Face API via `IHttpClientFactory` (mesmo estilo de `TelegramBotService`). Plugado no motor existente via `IRegistradorAssinaturaService.RegistrarAsync` — nenhuma tabela de assinatura nova. Offline: a assinatura via foto reaproveita `syncMutateMultipart` (motor de sincronização já existente), sem estrutura nova.

**Tech Stack:** .NET 8 (ASP.NET Core, EF Core, MediatR, `IHttpClientFactory`), React+TypeScript (Fluent UI), Azure Face API REST v1.0.

**Spec:** `docs/superpowers/specs/2026-09-04-assinatura-facial-azure-design.md` — leia antes de começar; este plano implementa exatamente as decisões lá registradas (método aditivo, sem substituir o Futronic; captura de foto única via `SeletorFotoCamera`, não webcam ao vivo; foto pendente até verificação quando offline; ASO e "Ordem de Serviço" fora de escopo).

## Global Constraints

- **Nunca substituir ou remover o Futronic** — `FutronicAutenticacaoStrategy`, `IAutenticacaoBiometriaLocalService`, `AssinaturaTab.tsx` (bloco de digital) e `AssinaturaQuiosque.tsx` (bloco de digital) permanecem exatamente como estão; o facial é um bloco **adicional**, nunca uma substituição condicional.
- Chaves da Azure Face API vêm de configuração (`appsettings`/variável de ambiente), nunca hardcoded — mesmo padrão de `Telegram:BotToken`/`Graph:ClientSecret`.
- O enrollment (cadastro da face) **não** passa pelo motor de sincronização offline (`syncMutateJson`/`syncMutateMultipart`) — é uma ação administrativa feita no perfil do trabalhador, sempre com internet, mesmo padrão de `cadastrarBiometriaLocal` hoje (usa `request<T>` simples).
- A assinatura em si (momento de identificar/assinar um documento) **usa** `syncMutateMultipart` — é a ação de campo que precisa funcionar offline.
- Endpoint versionado da Azure Face API (`face/v1.0/...`) deve ser confirmado contra a documentação atual da Azure no momento da implementação — os payloads deste plano seguem a API estável v1.0, mas confirme se a região/recurso provisionado usa essa versão antes de codificar.
- Migrations seguem o padrão do projeto: `dotnet ef migrations add <Name> --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`.

---

## Task 1: Enums + campos de entidade + migration

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs`
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs`
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Obra.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/OrganizacaoConfiguracoes.cs`
- Create: migration via `dotnet ef migrations add`

**Interfaces:**
- Produces: `MetodoAutenticacaoAssinatura.ReconhecimentoFacial = 6`, `MetodoAutenticacaoObra.ReconhecimentoFacial = 2`, `Trabalhador.AzureFacePersonId` (string?), `Obra.AzureFacePersonGroupId` (string?) — consumidos pela Task 2 em diante.

- [ ] **Step 1: Adicionar os dois valores de enum**

Em `src/AAHBRANT.SST.Domain/Enums/Enums.cs`, localize `MetodoAutenticacaoAssinatura` (perto da linha 624) e substitua:

```csharp
public enum MetodoAutenticacaoAssinatura
{
    Biometria = 1,
    // Assinatura em um clique do usuário logado (ex.: entregador de EPI assinando com a própria
    // sessão) — não é um método do "cardápio" por obra (MetodoAutenticacaoObra), pois não depende
    // de hardware/kiosque: está sempre disponível para quem já está autenticado no app.
    SessaoLogada = 5
}
```

com:

```csharp
public enum MetodoAutenticacaoAssinatura
{
    Biometria = 1,
    // Assinatura em um clique do usuário logado (ex.: entregador de EPI assinando com a própria
    // sessão) — não é um método do "cardápio" por obra (MetodoAutenticacaoObra), pois não depende
    // de hardware/kiosque: está sempre disponível para quem já está autenticado no app.
    SessaoLogada = 5,
    // Reconhecimento facial via Azure Face API (docs/superpowers/specs/2026-09-04-assinatura-facial-
    // azure-design.md) — método adicional ao Futronic, não o substitui. Diferente da Biometria (match
    // local no dispositivo), o match aqui acontece na nuvem (Face - Identify).
    ReconhecimentoFacial = 6
}
```

Depois, localize `MetodoAutenticacaoObra` (perto da linha 619) e substitua:

```csharp
// [Flags] em Obra.MetodosAutenticacaoHabilitados: cada obra decide se aceita assinatura (Biometria,
// via Futronic) ou não (Nenhum). CrachaPin/QrCodePin/WebAuthnCelular removidos em 31/08 junto com os
// métodos correspondentes (ver MetodoAutenticacaoAssinatura acima).
[Flags]
public enum MetodoAutenticacaoObra
{
    Nenhum = 0,
    Biometria = 1
}
```

com:

```csharp
// [Flags] em Obra.MetodosAutenticacaoHabilitados: cada obra decide se aceita assinatura (Biometria,
// via Futronic; ReconhecimentoFacial, via Azure Face API) ou não (Nenhum). CrachaPin/QrCodePin/
// WebAuthnCelular removidos em 31/08 junto com os métodos correspondentes (ver
// MetodoAutenticacaoAssinatura acima).
[Flags]
public enum MetodoAutenticacaoObra
{
    Nenhum = 0,
    Biometria = 1,
    ReconhecimentoFacial = 2
}
```

- [ ] **Step 2: Adicionar os campos nas entidades**

Em `src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs`, logo após `public DateTime? ConsentimentoBiometriaEm { get; set; }`:

```csharp
    public DateTime? TermoAceiteAssinaturaEletronicaEm { get; set; }
    public DateTime? ConsentimentoBiometriaEm { get; set; }

    // Id do Person no Azure Face API (PersonGroup da obra) — gerado no cadastro facial. Reaproveita
    // ConsentimentoBiometriaEm acima como consentimento LGPD (mesma categoria de dado biométrico
    // sensível — LGPD art. 5º II — não é um consentimento separado para "digital" vs. "facial").
    public string? AzureFacePersonId { get; set; }
```

Em `src/AAHBRANT.SST.Domain/Entidades/Obra.cs`, logo após `public MetodoAutenticacaoObra MetodosAutenticacaoHabilitados { get; set; } = MetodoAutenticacaoObra.Nenhum;`:

```csharp
    public MetodoAutenticacaoObra MetodosAutenticacaoHabilitados { get; set; } = MetodoAutenticacaoObra.Nenhum;

    // Id do PersonGroup no Azure Face API para esta obra — um grupo por obra (reduz o universo de
    // candidatos do Identify e evita falso positivo entre trabalhadores de obras diferentes).
    // Criado sob demanda no primeiro cadastro facial de um trabalhador desta obra (ver Task 3).
    public string? AzureFacePersonGroupId { get; set; }
```

- [ ] **Step 3: Configurar as colunas no EF Core**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/OrganizacaoConfiguracoes.cs`, em `ObraConfiguracao.Configure`, adicione após `builder.Property(o => o.Cnpj).HasMaxLength(18);`:

```csharp
        builder.Property(o => o.AzureFacePersonGroupId).HasMaxLength(64);
```

Em `TrabalhadorConfiguracao.Configure`, adicione após `builder.Property(t => t.Turno).HasMaxLength(50);`:

```csharp
        builder.Property(t => t.AzureFacePersonId).HasMaxLength(64);
```

- [ ] **Step 4: Gerar a migration**

Run: `dotnet ef migrations add AdicionarReconhecimentoFacial --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`
Expected: adiciona duas colunas nullable (`nvarchar(64)`) — `AzureFacePersonId` em `Trabalhadores`, `AzureFacePersonGroupId` em `Obras`. Nenhuma outra mudança de schema.

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: sem erros.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Enums/Enums.cs src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs src/AAHBRANT.SST.Domain/Entidades/Obra.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/OrganizacaoConfiguracoes.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat: adicionar enums e campos de entidade para reconhecimento facial (Azure Face API)"
```

---

## Task 2: `IAutenticacaoFacialService` (Azure Face API)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/IAutenticacaoFacialService.cs`
- Create: `src/AAHBRANT.SST.Infrastructure/Assinatura/AzureFaceAutenticacaoStrategy.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Assinatura/AssinaturaOptions.cs`
- Modify: `src/AAHBRANT.SST.Api/appsettings.json`
- Modify: `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/AzureFaceAutenticacaoStrategyTests.cs`

**Interfaces:**
- Consumes: `IHttpClientFactory` (já registrado via `services.AddHttpClient()`), `IAppDbContext`, `IOptions<AssinaturaOptions>`.
- Produces: `IAutenticacaoFacialService` com dois métodos: `CadastrarAsync` (enrollment) e `IdentificarAsync` (assinatura) — consumidos pelas Tasks 3 e 4.

- [ ] **Step 1: Definir o contrato**

Create `src/AAHBRANT.SST.Application/Assinatura/IAutenticacaoFacialService.cs`:

```csharp
namespace AAHBRANT.SST.Application.Assinatura;

// Motivos de rejeição distintos para mensagens específicas na UI (docs/superpowers/specs/2026-09-04-
// assinatura-facial-azure-design.md §3) — "nenhum rosto" e "confiança baixa" merecem textos
// diferentes de "múltiplos rostos".
public enum MotivoRejeicaoFacial
{
    NenhumRostoDetectado,
    MultiplosRostosDetectados,
    ConfiancaBaixa,
    RostoNaoReconhecido,
}

public record ResultadoIdentificacaoFacial(bool Aceito, ResultadoAutenticacaoAssinatura? Resultado, MotivoRejeicaoFacial? Motivo, double? Confianca);

public interface IAutenticacaoFacialService
{
    // Cadastra (ou atualiza) a face do trabalhador no Azure — cria o PersonGroup da obra se ainda não
    // existir, cria o Person se ainda não existir, adiciona a foto e dispara o treino, aguardando a
    // conclusão (síncrono — ação administrativa pontual, não precisa ser assíncrona).
    Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct);

    // Identifica quem está na foto dentro do PersonGroup da obra informada. Não recebe TrabalhadorId
    // — ao contrário do Futronic (que já resolveu o match localmente), aqui é o Azure quem descobre
    // quem é, a partir da foto.
    Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct);
}
```

- [ ] **Step 2: Adicionar as opções de configuração**

Em `src/AAHBRANT.SST.Infrastructure/Assinatura/AssinaturaOptions.cs`, substitua:

```csharp
namespace AAHBRANT.SST.Infrastructure.Assinatura;

public class AssinaturaOptions
{
    public string UrlBaseValidacaoPublica { get; set; } = "";
    public double LimiarConfiancaBiometriaLocal { get; set; } = 50;
}
```

com:

```csharp
namespace AAHBRANT.SST.Infrastructure.Assinatura;

public class AssinaturaOptions
{
    public string UrlBaseValidacaoPublica { get; set; } = "";
    public double LimiarConfiancaBiometriaLocal { get; set; } = 50;

    // Azure Face API (docs/superpowers/specs/2026-09-04-assinatura-facial-azure-design.md) — tier F0
    // (gratuito): 20 chamadas/minuto, até 30.000 rostos. Migrar para S0 é só trocar a chave.
    public string AzureFaceApiEndpoint { get; set; } = "";
    public string AzureFaceApiKey { get; set; } = "";
    public double LimiarConfiancaFacial { get; set; } = 0.85;
    public double LimiarConfiancaFacialMinimo { get; set; } = 0.60;
}
```

Em `src/AAHBRANT.SST.Api/appsettings.json`, dentro do bloco `"Assinatura"`, substitua:

```json
  "Assinatura": {
    "UrlBaseValidacaoPublica": "https://sst-web-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io",
    "LimiarConfiancaBiometriaLocal": 50
  },
```

com:

```json
  "Assinatura": {
    "UrlBaseValidacaoPublica": "https://sst-web-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io",
    "LimiarConfiancaBiometriaLocal": 50,
    "AzureFaceApiEndpoint": "",
    "AzureFaceApiKey": "",
    "LimiarConfiancaFacial": 0.85,
    "LimiarConfiancaFacialMinimo": 0.60
  },
```

(chave/endpoint reais nunca vão para o `appsettings.json` do repo — ficam vazios aqui e são preenchidos via variável de ambiente/segredo no ambiente real, mesmo padrão de `Telegram:BotToken`.)

- [ ] **Step 3: Implementar o serviço**

Create `src/AAHBRANT.SST.Infrastructure/Assinatura/AzureFaceAutenticacaoStrategy.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Estratégia de autenticação facial via Azure Face API — mesmo papel de FutronicAutenticacaoStrategy,
// mas o match acontece na nuvem (Face - Identify), não no dispositivo. Chamadas REST cruas via
// IHttpClientFactory, mesmo estilo já usado por TelegramBotService — sem SDK do Azure como
// dependência nova. Confirme a versão da API (face/v1.0) contra a documentação da Azure no momento
// de rodar isto pela primeira vez contra um recurso real.
public class AzureFaceAutenticacaoStrategy : IAutenticacaoFacialService
{
    private readonly IAppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AssinaturaOptions _options;

    public AzureFaceAutenticacaoStrategy(IAppDbContext db, IHttpClientFactory httpClientFactory, IOptions<AssinaturaOptions> options)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null || trabalhador.ConsentimentoBiometriaEm is null)
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite ou o consentimento de biometria.");

        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct)
            ?? throw new KeyNotFoundException("Obra do trabalhador não encontrada.");

        using var cliente = CriarCliente();

        var personGroupId = obra.AzureFacePersonGroupId;
        if (personGroupId is null)
        {
            personGroupId = $"obra-{obra.Id:N}";
            await CriarPersonGroupSeNaoExistirAsync(cliente, personGroupId, obra.Nome, ct);
            obra.AzureFacePersonGroupId = personGroupId;
        }

        var personId = trabalhador.AzureFacePersonId;
        if (personId is null)
        {
            personId = await CriarPersonAsync(cliente, personGroupId, trabalhador.Nome, ct);
            trabalhador.AzureFacePersonId = personId;
        }

        await AdicionarFaceAsync(cliente, personGroupId, personId, fotoJpeg, ct);
        await _db.SaveChangesAsync(ct);

        await TreinarEAguardarAsync(cliente, personGroupId, ct);
    }

    public async Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == obraId, ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");

        if (!obra.MetodosAutenticacaoHabilitados.HasFlag(MetodoAutenticacaoObra.ReconhecimentoFacial))
            throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra deste trabalhador.");

        if (obra.AzureFacePersonGroupId is null)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, null);

        using var cliente = CriarCliente();

        var faceIds = await DetectarRostosAsync(cliente, fotoJpeg, ct);
        if (faceIds.Count == 0)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.NenhumRostoDetectado, null);
        if (faceIds.Count > 1)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.MultiplosRostosDetectados, null);

        var candidato = await IdentificarRostoAsync(cliente, obra.AzureFacePersonGroupId, faceIds[0], ct);
        if (candidato is null)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, null);

        if (candidato.Confidence < _options.LimiarConfiancaFacialMinimo)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, candidato.Confidence);
        if (candidato.Confidence < _options.LimiarConfiancaFacial)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.ConfiancaBaixa, candidato.Confidence);

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.AzureFacePersonId == candidato.PersonId && t.ObraId == obraId, ct);
        if (trabalhador is null)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, candidato.Confidence);

        var resultado = new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial);
        return new ResultadoIdentificacaoFacial(true, resultado, null, candidato.Confidence);
    }

    private HttpClient CriarCliente()
    {
        if (string.IsNullOrWhiteSpace(_options.AzureFaceApiEndpoint) || string.IsNullOrWhiteSpace(_options.AzureFaceApiKey))
            throw new InvalidOperationException("Azure Face API não está configurada (Assinatura:AzureFaceApiEndpoint/AzureFaceApiKey).");

        var cliente = _httpClientFactory.CreateClient();
        cliente.BaseAddress = new Uri(_options.AzureFaceApiEndpoint.TrimEnd('/') + "/");
        cliente.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _options.AzureFaceApiKey);
        return cliente;
    }

    private static async Task CriarPersonGroupSeNaoExistirAsync(HttpClient cliente, string personGroupId, string nomeObra, CancellationToken ct)
    {
        var resposta = await cliente.PutAsJsonAsync($"face/v1.0/persongroups/{personGroupId}", new { name = nomeObra }, ct);
        // 409 = já existe (ex.: outra instância criou entre a checagem e aqui) — tratado como sucesso.
        if (!resposta.IsSuccessStatusCode && resposta.StatusCode != System.Net.HttpStatusCode.Conflict)
            throw new InvalidOperationException($"Falha ao criar PersonGroup no Azure Face API: {resposta.StatusCode}");
    }

    private static async Task<string> CriarPersonAsync(HttpClient cliente, string personGroupId, string nomeTrabalhador, CancellationToken ct)
    {
        var resposta = await cliente.PostAsJsonAsync($"face/v1.0/persongroups/{personGroupId}/persons", new { name = nomeTrabalhador }, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao criar Person no Azure Face API: {resposta.StatusCode}");
        var corpo = await resposta.Content.ReadFromJsonAsync<PersonCriadoResposta>(cancellationToken: ct);
        return corpo!.PersonId;
    }

    private static async Task AdicionarFaceAsync(HttpClient cliente, string personGroupId, string personId, byte[] fotoJpeg, CancellationToken ct)
    {
        using var conteudo = new ByteArrayContent(fotoJpeg);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var resposta = await cliente.PostAsync($"face/v1.0/persongroups/{personGroupId}/persons/{personId}/persistedFaces", conteudo, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao adicionar foto ao Person no Azure Face API: {resposta.StatusCode}");
    }

    private static async Task TreinarEAguardarAsync(HttpClient cliente, string personGroupId, CancellationToken ct)
    {
        var respostaTreino = await cliente.PostAsync($"face/v1.0/persongroups/{personGroupId}/train", content: null, ct);
        if (!respostaTreino.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao disparar treino no Azure Face API: {respostaTreino.StatusCode}");

        // Treino é assíncrono no Azure — poll com backoff curto (ação administrativa pontual, ok
        // bloquear por alguns segundos). 10 tentativas de 1s cobre o caso comum (grupo pequeno).
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            await Task.Delay(1000, ct);
            var status = await cliente.GetFromJsonAsync<TreinoStatusResposta>($"face/v1.0/persongroups/{personGroupId}/training", ct);
            if (status?.Status == "succeeded") return;
            if (status?.Status == "failed")
                throw new InvalidOperationException($"Treino do PersonGroup falhou no Azure Face API: {status.Message}");
        }
        throw new InvalidOperationException("Treino do PersonGroup no Azure Face API não concluiu a tempo.");
    }

    // Retry curto só para os dois caminhos "quentes" de assinatura (Detect/Identify) — é aqui que o
    // limite de 20 chamadas/minuto do tier F0 pode ser atingido de verdade (várias pessoas assinando
    // em sequência rápida, ex.: DDS matinal). CadastrarAsync (enrollment) não precisa: é ação pontual
    // e já espera segundos no polling do treino.
    private static async Task<HttpResponseMessage> EnviarComRetry429Async(Func<Task<HttpResponseMessage>> enviar, CancellationToken ct)
    {
        for (var tentativa = 0; ; tentativa++)
        {
            var resposta = await enviar();
            if (resposta.StatusCode != (System.Net.HttpStatusCode)429 || tentativa >= 2)
                return resposta;
            resposta.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(1 + tentativa), ct);
        }
    }

    private static async Task<List<string>> DetectarRostosAsync(HttpClient cliente, byte[] fotoJpeg, CancellationToken ct)
    {
        var resposta = await EnviarComRetry429Async(() =>
        {
            using var conteudo = new ByteArrayContent(fotoJpeg);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return cliente.PostAsync("face/v1.0/detect?returnFaceId=true", conteudo, ct);
        }, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao detectar rostos no Azure Face API: {resposta.StatusCode}");
        var rostos = await resposta.Content.ReadFromJsonAsync<List<RostoDetectadoResposta>>(cancellationToken: ct);
        return rostos?.Select(r => r.FaceId).ToList() ?? new List<string>();
    }

    private static async Task<CandidatoIdentificacao?> IdentificarRostoAsync(HttpClient cliente, string personGroupId, string faceId, CancellationToken ct)
    {
        var corpo = new { personGroupId, faceIds = new[] { faceId }, maxNumOfCandidatesReturned = 1, confidenceThreshold = 0.5 };
        var resposta = await EnviarComRetry429Async(() => cliente.PostAsJsonAsync("face/v1.0/identify", corpo, ct), ct);
        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao identificar rosto no Azure Face API: {resposta.StatusCode}");
        var resultados = await resposta.Content.ReadFromJsonAsync<List<IdentificacaoResposta>>(cancellationToken: ct);
        var candidato = resultados?.FirstOrDefault()?.Candidates.FirstOrDefault();
        return candidato is null ? null : new CandidatoIdentificacao(candidato.PersonId, candidato.Confidence);
    }

    private record CandidatoIdentificacao(string PersonId, double Confidence);

    private class PersonCriadoResposta
    {
        [JsonPropertyName("personId")]
        public string PersonId { get; set; } = "";
    }

    private class TreinoStatusResposta
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private class RostoDetectadoResposta
    {
        [JsonPropertyName("faceId")]
        public string FaceId { get; set; } = "";
    }

    private class IdentificacaoResposta
    {
        [JsonPropertyName("candidates")]
        public List<CandidatoResposta> Candidates { get; set; } = new();
    }

    private class CandidatoResposta
    {
        [JsonPropertyName("personId")]
        public string PersonId { get; set; } = "";
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }
}
```

- [ ] **Step 4: Registrar no DI**

Em `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`, logo após `services.AddScoped<IAutenticacaoBiometriaLocalService, FutronicAutenticacaoStrategy>();`:

```csharp
        services.AddScoped<IAutenticacaoFacialService, AzureFaceAutenticacaoStrategy>();
```

(`services.AddHttpClient()` já está registrado mais acima no mesmo método — não duplicar.)

- [ ] **Step 5: Escrever os testes**

Create `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/AzureFaceAutenticacaoStrategyTests.cs`. Usa um `HttpMessageHandler` fake que roteia por URL (mesmo princípio de teste de integração HTTP sem rede real):

```csharp
using System.Net;
using System.Text.Json;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

// Fake de IHttpClientFactory que roteia por caminho da URL — simula as respostas do Azure Face API
// sem chamada real de rede, permitindo testar os thresholds e a lógica de detect→identify.
public class HttpClientFactoryFalso : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public HttpClientFactoryFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    private class HandlerFalso : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_responder(request));
    }

    public HttpClient CreateClient(string name = "") => new(new HandlerFalso(_responder));
}

public class AzureFaceAutenticacaoStrategyTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static IOptions<AssinaturaOptions> Opcoes() => Microsoft.Extensions.Options.Options.Create(new AssinaturaOptions
    {
        AzureFaceApiEndpoint = "https://fake.cognitiveservices.azure.com",
        AzureFaceApiKey = "chave-fake",
        LimiarConfiancaFacial = 0.85,
        LimiarConfiancaFacialMinimo = 0.60,
    });

    private static HttpResponseMessage Json(object corpo, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(corpo)) };

    [Fact]
    public async Task IdentificarAsync_ObraSemFacialHabilitado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(IdentificarAsync_ObraSemFacialHabilitado_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste", MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.Nenhum };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(_ => throw new InvalidOperationException("não deveria chamar a rede"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default));
    }

    [Fact]
    public async Task IdentificarAsync_NenhumRostoDetectado_RetornaMotivoNenhumRosto()
    {
        var db = CriarDb(nameof(IdentificarAsync_NenhumRostoDetectado_RetornaMotivoNenhumRosto));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/detect") ? Json(new List<object>()) : throw new InvalidOperationException("chamada inesperada"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.False(resultado.Aceito);
        Assert.Equal(MotivoRejeicaoFacial.NenhumRostoDetectado, resultado.Motivo);
    }

    [Fact]
    public async Task IdentificarAsync_ConfiancaAbaixoDoLimiarMinimo_RetornaRostoNaoReconhecido()
    {
        var db = CriarDb(nameof(IdentificarAsync_ConfiancaAbaixoDoLimiarMinimo_RetornaRostoNaoReconhecido));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/detect"))
                return Json(new[] { new { faceId = "face-1" } });
            if (req.RequestUri!.AbsolutePath.EndsWith("/identify"))
                return Json(new[] { new { faceId = "face-1", candidates = new[] { new { personId = "person-1", confidence = 0.40 } } } });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.False(resultado.Aceito);
        Assert.Equal(MotivoRejeicaoFacial.RostoNaoReconhecido, resultado.Motivo);
    }

    [Fact]
    public async Task IdentificarAsync_ConfiancaEntreLimiares_RetornaConfiancaBaixa()
    {
        var db = CriarDb(nameof(IdentificarAsync_ConfiancaEntreLimiares_RetornaConfiancaBaixa));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/detect"))
                return Json(new[] { new { faceId = "face-1" } });
            if (req.RequestUri!.AbsolutePath.EndsWith("/identify"))
                return Json(new[] { new { faceId = "face-1", candidates = new[] { new { personId = "person-1", confidence = 0.70 } } } });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.False(resultado.Aceito);
        Assert.Equal(MotivoRejeicaoFacial.ConfiancaBaixa, resultado.Motivo);
        Assert.Equal(0.70, resultado.Confianca);
    }

    [Fact]
    public async Task IdentificarAsync_ConfiancaAltaETrabalhadorEncontrado_Aceita()
    {
        var db = CriarDb(nameof(IdentificarAsync_ConfiancaAltaETrabalhadorEncontrado_Aceita));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow,
            AzureFacePersonId = "person-1",
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/detect"))
                return Json(new[] { new { faceId = "face-1" } });
            if (req.RequestUri!.AbsolutePath.EndsWith("/identify"))
                return Json(new[] { new { faceId = "face-1", candidates = new[] { new { personId = "person-1", confidence = 0.95 } } } });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.True(resultado.Aceito);
        Assert.Equal(trabalhador.Id, resultado.Resultado!.TrabalhadorId);
        Assert.Equal(MetodoAutenticacaoAssinatura.ReconhecimentoFacial, resultado.Resultado.Metodo);
    }

    [Fact]
    public async Task CadastrarAsync_TrabalhadorSemConsentimento_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(CadastrarAsync_TrabalhadorSemConsentimento_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(_ => throw new InvalidOperationException("não deveria chamar a rede"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.CadastrarAsync(trabalhador.Id, new byte[] { 1 }, default));
    }

    [Fact]
    public async Task CadastrarAsync_PrimeiroCadastroDaObra_CriaGrupoPessoaEPersisteAzureFacePersonId()
    {
        var db = CriarDb(nameof(CadastrarAsync_PrimeiroCadastroDaObra_CriaGrupoPessoaEPersisteAzureFacePersonId));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow,
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow, ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var chamadasTreino = 0;
        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Put && caminho.Contains("/persongroups/"))
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (caminho.EndsWith("/persons"))
                return Json(new { personId = "person-novo" });
            if (caminho.EndsWith("/persistedFaces"))
                return Json(new { persistedFaceId = "face-persistida-1" });
            if (caminho.EndsWith("/train"))
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            if (caminho.EndsWith("/training"))
            {
                chamadasTreino++;
                return Json(new { status = "succeeded" });
            }
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await servico.CadastrarAsync(trabalhador.Id, new byte[] { 1, 2, 3 }, default);

        var obraAtualizada = await db.Obras.FirstAsync(o => o.Id == obra.Id);
        var trabalhadorAtualizado = await db.Trabalhadores.FirstAsync(t => t.Id == trabalhador.Id);
        Assert.NotNull(obraAtualizada.AzureFacePersonGroupId);
        Assert.Equal("person-novo", trabalhadorAtualizado.AzureFacePersonId);
        Assert.True(chamadasTreino >= 1);
    }
}
```

Note: `CpfCriptografiaContexto.Configurar(...)` precisa estar configurado no processo de teste antes de qualquer `SaveChangesAsync` envolvendo `Trabalhador` (mesmo motivo documentado em `ExportarFichaEpiTrabalhadorQueryHandlerTests.cs`, projeto irmão `Application.Tests`) — adicione um construtor estático equivalente nesta classe se `AAHBRANT.SST.Infrastructure.Tests` ainda não tiver uma configuração compartilhada disso; confira antes de rodar (Step 6).

Note: `CpfCriptografiaContexto.Configurar(...)` precisa ser chamado uma vez no processo antes de salvar um `Trabalhador` (ver `DbContextFactory.cs`/outros testes que já gravam `Trabalhador`) — se `AAHBRANT.SST.Infrastructure.Tests` ainda não tem essa configuração de teste estática em algum lugar compartilhado, adicione um construtor estático na classe de teste igual ao já usado em `ExportarFichaEpiTrabalhadorQueryHandlerTests.cs` (`Application.Tests`, projeto irmão) — confirme antes de rodar.

- [ ] **Step 6: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests --filter AzureFaceAutenticacaoStrategyTests`
Expected: PASS (7/7).

- [ ] **Step 7: Build completo**

Run: `dotnet build`
Expected: sem erros.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/IAutenticacaoFacialService.cs src/AAHBRANT.SST.Infrastructure/Assinatura/AzureFaceAutenticacaoStrategy.cs src/AAHBRANT.SST.Infrastructure/Assinatura/AssinaturaOptions.cs src/AAHBRANT.SST.Api/appsettings.json src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/AzureFaceAutenticacaoStrategyTests.cs
git commit -m "feat: adicionar IAutenticacaoFacialService (Azure Face API) — cadastro e identificação"
```

---

## Task 3: Cadastro (enrollment) — backend + frontend

**Files:**
- Create: `src/AAHBRANT.SST.Application/Trabalhadores/Commands/CadastrarFacialCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/AssinaturaTab.tsx`

**Interfaces:**
- Consumes: `IAutenticacaoFacialService.CadastrarAsync` (Task 2).
- Produces: `POST /api/trabalhadores/{id}/assinatura/facial/cadastro` (multipart, `IFormFile Foto`), `api.trabalhadores.cadastrarFacial(id, foto: File)`.

- [ ] **Step 1: Command**

Create `src/AAHBRANT.SST.Application/Trabalhadores/Commands/CadastrarFacialCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Assinatura;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record CadastrarFacialCommand(Guid TrabalhadorId, byte[] FotoJpeg) : IRequest;

public class CadastrarFacialCommandValidator : AbstractValidator<CadastrarFacialCommand>
{
    public CadastrarFacialCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.FotoJpeg).NotEmpty();
    }
}

public class CadastrarFacialCommandHandler : IRequestHandler<CadastrarFacialCommand>
{
    private readonly IAutenticacaoFacialService _autenticacaoFacial;

    public CadastrarFacialCommandHandler(IAutenticacaoFacialService autenticacaoFacial) => _autenticacaoFacial = autenticacaoFacial;

    public async Task Handle(CadastrarFacialCommand request, CancellationToken ct)
    {
        await _autenticacaoFacial.CadastrarAsync(request.TrabalhadorId, request.FotoJpeg, ct);
    }
}
```

- [ ] **Step 2: Endpoint**

Em `src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs`, logo após o endpoint `CadastrarBiometriaLocal` (que termina com `return NoContent(); }` por volta da linha 126), adicione:

```csharp
    public class CadastrarFacialRequestBody
    {
        public IFormFile Foto { get; set; } = null!;
    }

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/facial/cadastro")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> CadastrarFacial(Guid id, [FromForm] CadastrarFacialRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);
        await _mediator.Send(new CadastrarFacialCommand(id, stream.ToArray()), ct);
        return NoContent();
    }
```

(Confirme o nome exato da policy de autorização `"trabalhador:assinatura"` já usada por `CadastrarBiometriaLocal` — reaproveitar a mesma, sem criar uma nova.)

- [ ] **Step 3: Cliente da API (frontend)**

`request<T>` (definida por volta da linha 2396) sempre define `Content-Type: application/json` incondicionalmente — **não é seguro usá-la com `FormData`** (quebraria o parsing multipart no `[FromForm]` do backend, que depende do boundary que o próprio `Content-Type` de FormData carrega). O padrão real já usado no projeto para upload de arquivo é `fetch()` direto, sem `request<T>` (ver `criar obra`/`atualizarLogo`, por volta da linha 2733/2748: `headers: await montarHeadersAuth()` — sem `Content-Type` explícito, o browser define o boundary sozinho).

Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, logo após o método `cadastrarBiometriaLocal` (por volta da linha 2818), adicione:

```typescript
    cadastrarFacial: async (id: string, foto: File): Promise<void> => {
      const formData = new FormData();
      formData.append('foto', foto);
      const response = await fetch(`${API_BASE_URL}/api/trabalhadores/${id}/assinatura/facial/cadastro`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
    },
```

- [ ] **Step 4: UI**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/AssinaturaTab.tsx`, adicione um segundo card, análogo ao da digital, usando `SeletorFotoCamera`. Adicione os imports:

```tsx
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
```

Adicione o estado (junto aos já existentes `cadastrandoBiometriaLocal`/`erroBiometriaLocal`/`biometriaLocalCadastrada`):

```tsx
  const [erroFacial, setErroFacial] = useState<string | null>(null);
  const [facialCadastrada, setFacialCadastrada] = useState(false);

  async function cadastrarFacial(arquivo: File) {
    try {
      setErroFacial(null);
      setFacialCadastrada(false);
      await api.trabalhadores.cadastrarFacial(trabalhadorId, arquivo);
      setFacialCadastrada(true);
    } catch (e) {
      setErroFacial(extrairMensagemErro(e, 'Falha ao cadastrar a face.'));
    }
  }
```

E, dentro do `return`, logo após o `</div>` que fecha o card da digital (linha 79 do arquivo atual), adicione um segundo card:

```tsx
      <div className={estilos.card} style={{ maxWidth: 480 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>
          Reconhecimento Facial (Azure)
        </Text>
        <Text style={{ display: 'block', marginBottom: 12, color: 'var(--colorNeutralForeground3)' }}>
          Método adicional ao leitor de digital — exige Termo de Aceite e consentimento de biometria já
          registrados para este trabalhador.
        </Text>
        {erroFacial && <Text className={estilos.erro}>{erroFacial}</Text>}
        {facialCadastrada && <Text style={{ display: 'block', marginBottom: 8 }}>Face cadastrada com sucesso.</Text>}
        <SeletorFotoCamera aoSelecionarArquivo={cadastrarFacial} rotulo="Capturar foto do rosto" />
      </div>
```

- [ ] **Step 5: Type-check + build**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit`
Run: `dotnet build`
Expected: ambos sem erros.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Trabalhadores/Commands/CadastrarFacialCommand.cs src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs src/AAHBRANT.SST.TeamsApp/src/lib/api.ts src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/AssinaturaTab.tsx
git commit -m "feat: cadastro (enrollment) de reconhecimento facial no perfil do trabalhador"
```

---

## Task 4: Assinatura via reconhecimento facial — backend + frontend (offline-aware)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarAssinaturaFacialCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/AssinaturaController.cs`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaQuiosque.tsx`

**Interfaces:**
- Consumes: `IAutenticacaoFacialService.IdentificarAsync` (Task 2), `IRegistradorAssinaturaService.RegistrarAsync` (já existe), `syncMutateMultipart` (já existe, `src/lib/offline/syncEngine.ts`).
- Produces: `POST /api/assinatura/{id}/autenticacao/facial` (multipart), `api.assinatura.autenticarFacial(documentoAssinaturaId, obraId, foto)`.

- [ ] **Step 1: Command**

Create `src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarAssinaturaFacialCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Assinatura.Queries;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Diferente de RegistrarAssinaturaBiometriaLocalCommand: não recebe TrabalhadorId — quem está na
// foto é descoberto pelo Azure (Identify), não resolvido antes pelo cliente.
public record RegistrarAssinaturaFacialCommand(Guid DocumentoAssinaturaId, Guid ObraId, byte[] FotoJpeg, string? IpAddress = null) : IRequest<DocumentoSignatarioDto>;

public class RegistrarAssinaturaFacialCommandValidator : AbstractValidator<RegistrarAssinaturaFacialCommand>
{
    public RegistrarAssinaturaFacialCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.FotoJpeg).NotEmpty();
    }
}

// Mensagem de erro de negócio distinta por motivo (spec §3) — o controller devolve isso como corpo
// do 400 para a UI mostrar o texto certo (nenhum rosto / múltiplos rostos / confiança baixa / não
// reconhecido), em vez de um genérico "falha na autenticação".
public class RejeicaoFacialException : Exception
{
    public MotivoRejeicaoFacial Motivo { get; }
    public RejeicaoFacialException(MotivoRejeicaoFacial motivo, string mensagem) : base(mensagem) => Motivo = motivo;
}

public class RegistrarAssinaturaFacialCommandHandler : IRequestHandler<RegistrarAssinaturaFacialCommand, DocumentoSignatarioDto>
{
    private readonly IAutenticacaoFacialService _autenticacaoFacial;
    private readonly IRegistradorAssinaturaService _registrador;

    public RegistrarAssinaturaFacialCommandHandler(IAutenticacaoFacialService autenticacaoFacial, IRegistradorAssinaturaService registrador)
    {
        _autenticacaoFacial = autenticacaoFacial;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(RegistrarAssinaturaFacialCommand request, CancellationToken ct)
    {
        var identificacao = await _autenticacaoFacial.IdentificarAsync(request.ObraId, request.FotoJpeg, ct);
        if (!identificacao.Aceito)
        {
            var mensagem = identificacao.Motivo switch
            {
                MotivoRejeicaoFacial.NenhumRostoDetectado => "Nenhum rosto detectado na foto.",
                MotivoRejeicaoFacial.MultiplosRostosDetectados => "Mais de uma pessoa detectada na câmera — aproxime-se sozinho.",
                MotivoRejeicaoFacial.ConfiancaBaixa => "Rosto reconhecido com baixa confiança — tente novamente com melhor iluminação.",
                _ => "Rosto não reconhecido.",
            };
            throw new RejeicaoFacialException(identificacao.Motivo!.Value, mensagem);
        }

        return await _registrador.RegistrarAsync(request.DocumentoAssinaturaId, identificacao.Resultado!, request.IpAddress, ct);
    }
}
```

- [ ] **Step 2: Endpoint**

Em `src/AAHBRANT.SST.Api/Controllers/AssinaturaController.cs`, logo após o endpoint `AutenticarBiometriaLocal` (linhas 67-76 do arquivo atual), adicione:

```csharp
    public class AutenticarFacialRequestBody
    {
        public Guid ObraId { get; set; }
        public IFormFile Foto { get; set; } = null!;
    }

    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost("{id:guid}/autenticacao/facial")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<DocumentoSignatarioDto>> AutenticarFacial(Guid id, [FromForm] AutenticarFacialRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);
        try
        {
            var resultado = await _mediator.Send(
                new RegistrarAssinaturaFacialCommand(id, body.ObraId, stream.ToArray(), ObterIpCliente()), ct);
            return Ok(resultado);
        }
        catch (RejeicaoFacialException ex)
        {
            return BadRequest(new { erro = ex.Message, motivo = ex.Motivo.ToString() });
        }
    }
```

- [ ] **Step 3: Cliente da API (frontend)**

Mesmo padrão exato já usado por `anexarFotoEvidencia` (DDS, por volta da linha 3364 de `api.ts`) — `montarHeadersAuth()` é a mesma função usada em toda a chamada síncrona/offline-aware do arquivo, não existe uma variante "Sync" separada. `syncMutateMultipart`/`syncMutateJson` já estão importados no topo do arquivo (linha 3: `import { syncFetchBlob, syncFetchJson, syncMutateJson, syncMutateMultipart } from './offline/syncEngine';`).

Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, no objeto `assinatura` (perto de `autenticarBiometriaLocal`), adicione:

```typescript
    autenticarFacial: async (documentoAssinaturaId: string, obraId: string, foto: File) => {
      const formData = new FormData();
      formData.append('obraId', obraId);
      formData.append('foto', foto);
      const authHeaders = await montarHeadersAuth();
      return syncMutateMultipart<DocumentoSignatarioDto>(
        `/api/assinatura/${documentoAssinaturaId}/autenticacao/facial`, formData, authHeaders,
      );
    },
```

- [ ] **Step 4: UI do quiosque**

Em `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaQuiosque.tsx`, adicione um segundo bloco de assinatura, sempre visível (não condicionado a `agenteLocalDisponivel`), com sua própria captura de foto via `SeletorFotoCamera`, tratamento de erro (usando o `motivo`/`erro` retornado pelo endpoint), e indicação de "pendente de sincronização" quando `syncMutateMultipart` lançar `MutacaoEnfileiradaOfflineError` (offline).

Adicione os imports:

```tsx
import { SeletorFotoCamera } from '../SeletorFotoCamera';
import { MutacaoEnfileiradaOfflineError } from '../../lib/offline/syncEngine';
```

Adicione o estado (junto aos já existentes):

```tsx
  const [pendenteFacial, setPendenteFacial] = useState(false);
```

`AssinaturaQuiosque` hoje só recebe `entidadeTipo`/`entidadeId`, sem a obra — confirmado que `AssinarDdsPage.tsx` (linha 56: `<AssinaturaQuiosque entidadeTipo="Dds" entidadeId={id} />`) não passa `obraId` hoje. Há 6 callers ao todo: `AssinarDdsPage.tsx`, `AssinarPtPage.tsx`, `AssinarEntregaEpiPage.tsx`, e os componentes `AssinaturaEntregaEpiDialog.tsx`/`AssinaturaDevolucaoEpiDialog.tsx` (que provavelmente renderizam `AssinaturaQuiosque` internamente, ou o padrão equivalente — confirme). Adicione `obraId: string` como nova prop obrigatória de `AssinaturaQuiosqueProps` e propague em todos os 6 pontos de uso (cada página/diálogo já carrega o registro que está sendo assinado — resolva `obraId` a partir do DTO já carregado ali, ex.: `dds.obraId` se o DTO tiver esse campo; se não tiver, adicione-o à query/DTO correspondente antes de propagar).

Adicione a função:

```tsx
  async function assinarComFacial(arquivo: File) {
    if (!documento) return;
    try {
      setErro(null);
      setPendenteFacial(false);
      setUltimoAssinante(null);
      const signatario = await api.assinatura.autenticarFacial(documento.id, obraId, arquivo);
      setUltimoAssinante(signatario.trabalhadorNome);
      const doc = await api.assinatura.obter(entidadeTipo, entidadeId);
      setDocumento(doc);
    } catch (e) {
      if (e instanceof MutacaoEnfileiradaOfflineError) {
        setPendenteFacial(true);
        return;
      }
      setErro(extrairMensagemErro(e, 'Falha na autenticação facial.'));
    }
  }
```

E, no `return`, adicione um card irmão do bloco Futronic (fora do `if (agenteLocalDisponivel...)`, sempre renderizado):

```tsx
      <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
          Reconhecimento Facial (Azure)
        </Text>
        {pendenteFacial && (
          <Text style={{ display: 'block', marginBottom: 8 }}>
            Sem internet — a foto foi salva neste dispositivo e será verificada assim que a conexão voltar.
          </Text>
        )}
        <SeletorFotoCamera aoSelecionarArquivo={assinarComFacial} rotulo="Assinar com reconhecimento facial" desabilitado={!documento} />
      </div>
```

- [ ] **Step 5: Type-check + build**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit`
Run: `dotnet build`
Expected: ambos sem erros. Se a resolução de `obraId` (Step 4) exigir uma nova prop, atualize também os componentes pai que renderizam `<AssinaturaQuiosque />` (localizados via `grep -rl "<AssinaturaQuiosque"` no diretório `src/AAHBRANT.SST.TeamsApp/src/pages`).

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarAssinaturaFacialCommand.cs src/AAHBRANT.SST.Api/Controllers/AssinaturaController.cs src/AAHBRANT.SST.TeamsApp/src/lib/api.ts src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaQuiosque.tsx
git commit -m "feat: assinatura via reconhecimento facial no quiosque (offline-aware)"
```

(Se o Step 4 exigiu mudar componentes pai para propagar `obraId`, inclua-os também no `git add`.)

---

## Task 5: Verificação final

**Files:** nenhum (só verificação)

- [ ] **Step 1: Build completo**

Run: `dotnet build`
Expected: 0 erros.

- [ ] **Step 2: Testes completos**

Run: `dotnet test`
Expected: todos os testes passam, incluindo os 5 novos de `AzureFaceAutenticacaoStrategyTests`.

- [ ] **Step 3: Type-check do frontend**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit`
Expected: sem erros.

- [ ] **Step 4: Nota de limitação (esperada, não é bug)**

Sem uma chave real do Azure Face API configurada, não é possível testar o fluxo ponta-a-ponta contra o serviço de verdade (cadastro real + identify real). Isso é esperado — comunique isso ao usuário explicitamente ao reportar a conclusão, e recomende testar manualmente assim que uma chave de desenvolvimento (tier F0) estiver disponível.

- [ ] **Step 5: Relatar ao usuário**

Resuma o que foi verificado (build, testes automatizados, type-check) e deixe claro que o fluxo real contra a Azure precisa de uma chave de API antes de ser testado de ponta a ponta. Não faça deploy — só quando o usuário pedir explicitamente.
