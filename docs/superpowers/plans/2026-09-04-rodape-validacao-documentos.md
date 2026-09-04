# Rodapé de Rastreabilidade e Validação Digital — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every PDF the system generates (DDS, DDS Semanal, APR, PT, CIPA, Inspeção, Ficha de EPI, Certificado/Ata de Treinamento) gets a standardized footer: protocolo/número do documento, hash de integridade + QR/link de validação pública, nota de assinatura digital (só quando há signatário real), e paginação "Página X de Y".

**Architecture:** A new `IRegistradorRastreabilidadeService.GarantirAsync(entidadeTipo, entidadeId, ct)` generates/reaproveita hash+token+QR on a `DocumentoAssinatura` row **without ever touching `Status`/`FinalizadoEm`** — completely decoupled from `FinalizarDocumentoCommand`, which stays untouched. Each `Exportar...PdfQuery` calls it, then passes the result into a new shared `RodapeDocumentoPadrao.Desenhar(...)` QuestPDF component used by every `...PdfService`. Two document types (Ficha de EPI, Ata de Sessão de Treinamento) are aggregates with no single backing entity — they use a synthetic tracking key (`FichaEpiTrabalhador`/`TrabalhadorId`, `SessaoTreinamento`/`SessaoTreinamento.Id`).

**Tech Stack:** .NET / EF Core (InMemory provider in tests) / MediatR / QuestPDF / xUnit / React+TypeScript (Fluent UI) for the one frontend file touched.

**Spec:** `docs/superpowers/specs/2026-09-04-rodape-validacao-documentos-design.md` — read it before starting; this plan implements it exactly, including the two corrections recorded in sections 3–4 (rastreabilidade separada de finalização) and the PGR-removal note in section 1.

## Global Constraints

- Branch `integracao/deploy-treinamentos` in worktree `.worktrees/reformulacao-treinamentos` is **shared with another concurrent Claude Code session** — run `git fetch origin && git log HEAD..origin/integracao/deploy-treinamentos --oneline` before every commit.
- **Never run `az acr build` / `az containerapp update`** — deploy only when the user explicitly asks in that turn.
- **Never delete anything without asking first.**
- `FinalizarDocumentoCommand` and the biometric/session-signed signing flow are **not modified** by this plan — if any task's diff appears to touch that file, stop and re-read the spec section 3.
- New EF migrations follow the existing project convention: `dotnet ef migrations add <Name> --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`, run from the worktree root.
- All new/changed C# files keep the codebase's comment style (Portuguese, WHY not WHAT, only when non-obvious).

---

## Task 1: `DocumentoAssinatura.RastreadoEm` + migration

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Assinatura/DocumentoAssinatura.cs`
- Create: migration via `dotnet ef migrations add`

**Interfaces:**
- Produces: `DocumentoAssinatura.RastreadoEm` (`DateTime?`) — timestamp of first token generation, used later by Task 3 as the "emitido em" fallback for documents that never reach `Finalizado`.

- [ ] **Step 1: Add the field**

In `src/AAHBRANT.SST.Domain/Entidades/Assinatura/DocumentoAssinatura.cs`, right after the existing `FinalizadoEm` property:

```csharp
    public string? ConteudoHash { get; set; }
    public string? TokenValidacaoPublica { get; set; }
    public DateTime? FinalizadoEm { get; set; }

    // Timestamp de quando o token de validação pública foi gerado pela primeira vez (Task 2 —
    // IRegistradorRastreabilidadeService), independente de o documento chegar a ser Finalizado. Usado
    // como "emitido em" na página pública/rodapé para documentos que nunca finalizam (CIPA, DDS
    // Semanal — nunca assinam digitalmente).
    public DateTime? RastreadoEm { get; set; }
```

- [ ] **Step 2: Generate the migration**

Run: `dotnet ef migrations add AdicionarRastreadoEmDocumentoAssinatura --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`
Expected: a new migration pair (`*.cs` + `*.Designer.cs`) adding one nullable `datetime2` column `RastreadoEm` to `DocumentosAssinatura`, plus an updated `SstDbContextModelSnapshot.cs`. No other column changes should appear in the diff — if they do, another uncommitted domain change slipped in; stop and check `git status` before continuing.

- [ ] **Step 3: Build**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure` (or the full solution)
Expected: builds with no errors.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Assinatura/DocumentoAssinatura.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat: adicionar DocumentoAssinatura.RastreadoEm para rastreabilidade sem finalização"
```

---

## Task 2: `IRegistradorRastreabilidadeService`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/IRegistradorRastreabilidadeService.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Assinatura/RegistradorRastreabilidadeServiceTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.DocumentosAssinatura` (existing), `IQrCodeDocumentoService.Gerar(string token) -> QrCodeDocumentoResultado(byte[] Png, string UrlValidacao)` (existing), `TokenValidacaoPublicaGerador.Gerar() -> string` (existing), `HashConteudoDocumentoCalculador.Calcular(string, Guid, IEnumerable<DocumentoSignatarioDto>) -> string` (existing).
- Produces: `RastreabilidadeDocumentoResultado(string ConteudoHash, string UrlValidacaoPublica, byte[] QrCodePng, bool TemAssinatura)`, consumed by every rollout task (Tasks 6–14).

- [ ] **Step 1: Write the failing tests**

Create `tests/AAHBRANT.SST.Application.Tests/Assinatura/RegistradorRastreabilidadeServiceTests.cs`:

```csharp
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class QrCodeDocumentoServiceFalso : IQrCodeDocumentoService
{
    public QrCodeDocumentoResultado Gerar(string token) => new(new byte[] { 9, 9 }, $"https://fake/#/validar/{token}");
}

public class RegistradorRastreabilidadeServiceTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task GarantirAsync_DocumentoNovo_CriaComTokenHashETemAssinaturaFalse()
    {
        var db = CriarDb(nameof(GarantirAsync_DocumentoNovo_CriaComTokenHashETemAssinaturaFalse));
        var servico = new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso());
        var entidadeId = Guid.NewGuid();

        var resultado = await servico.GarantirAsync("Cipa", entidadeId, default);

        Assert.False(resultado.TemAssinatura);
        Assert.NotEmpty(resultado.ConteudoHash);
        Assert.Contains("/validar/", resultado.UrlValidacaoPublica);

        var documento = await db.DocumentosAssinatura.SingleAsync(d => d.EntidadeTipo == "Cipa" && d.EntidadeId == entidadeId);
        Assert.NotNull(documento.TokenValidacaoPublica);
        Assert.NotNull(documento.RastreadoEm);
        Assert.Equal(StatusDocumentoAssinatura.EmAndamento, documento.Status);
    }

    [Fact]
    public async Task GarantirAsync_ChamadoDuasVezesEmAndamento_MantemMesmoTokenERecalculaHash()
    {
        var db = CriarDb(nameof(GarantirAsync_ChamadoDuasVezesEmAndamento_MantemMesmoTokenERecalculaHash));
        var servico = new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso());
        var entidadeId = Guid.NewGuid();

        var primeiro = await servico.GarantirAsync("Dds", entidadeId, default);
        var tokenApósPrimeiraChamada = (await db.DocumentosAssinatura.SingleAsync(d => d.EntidadeId == entidadeId)).TokenValidacaoPublica;

        // Simula um signatário aparecendo entre as duas chamadas (ex.: presença biométrica registrada
        // depois do primeiro export do PDF) — o hash deve refletir isso na segunda chamada.
        var documento = await db.DocumentosAssinatura.SingleAsync(d => d.EntidadeId == entidadeId);
        documento.Signatarios.Add(new DocumentoSignatario
        {
            TrabalhadorId = Guid.NewGuid(),
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria,
            AssinadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var segundo = await servico.GarantirAsync("Dds", entidadeId, default);
        var tokenApósSegundaChamada = (await db.DocumentosAssinatura.SingleAsync(d => d.EntidadeId == entidadeId)).TokenValidacaoPublica;

        Assert.Equal(tokenApósPrimeiraChamada, tokenApósSegundaChamada);
        Assert.NotEqual(primeiro.ConteudoHash, segundo.ConteudoHash);
        Assert.True(segundo.TemAssinatura);
    }

    [Fact]
    public async Task GarantirAsync_DocumentoJaFinalizado_NaoAlteraHashNemToken()
    {
        var db = CriarDb(nameof(GarantirAsync_DocumentoJaFinalizado_NaoAlteraHashNemToken));
        var entidadeId = Guid.NewGuid();
        var documento = new DocumentoAssinatura
        {
            EntidadeTipo = "Treinamento",
            EntidadeId = entidadeId,
            Status = StatusDocumentoAssinatura.Finalizado,
            ConteudoHash = "HASHCONGELADO",
            TokenValidacaoPublica = "TOKENCONGELADO",
            FinalizadoEm = DateTime.UtcNow,
        };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso());
        var resultado = await servico.GarantirAsync("Treinamento", entidadeId, default);

        Assert.Equal("HASHCONGELADO", resultado.ConteudoHash);
        Assert.Contains("TOKENCONGELADO", resultado.UrlValidacaoPublica);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter RegistradorRastreabilidadeServiceTests`
Expected: FAIL — `RegistradorRastreabilidadeService`/`IRegistradorRastreabilidadeService` don't exist yet.

- [ ] **Step 3: Implement**

Create `src/AAHBRANT.SST.Application/Assinatura/IRegistradorRastreabilidadeService.cs`:

```csharp
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

public record RastreabilidadeDocumentoResultado(string ConteudoHash, string UrlValidacaoPublica, byte[] QrCodePng, bool TemAssinatura);

// Rastreabilidade (hash+token+QR) desacoplada de finalização — deliberadamente NÃO usa
// FinalizarDocumentoCommand (ver docs/superpowers/specs/2026-09-04-rodape-validacao-documentos-design.md
// §3): esse comando fecha o documento para novas assinaturas, o que travaria um DDS/APR/PT ainda em
// assinatura só por ter sido exportado em PDF uma vez. Aqui, hash/token nunca mexem em Status/FinalizadoEm.
public interface IRegistradorRastreabilidadeService
{
    Task<RastreabilidadeDocumentoResultado> GarantirAsync(string entidadeTipo, Guid entidadeId, CancellationToken ct);
}

public class RegistradorRastreabilidadeService : IRegistradorRastreabilidadeService
{
    private readonly IAppDbContext _db;
    private readonly IQrCodeDocumentoService _qrCode;

    public RegistradorRastreabilidadeService(IAppDbContext db, IQrCodeDocumentoService qrCode)
    {
        _db = db;
        _qrCode = qrCode;
    }

    public async Task<RastreabilidadeDocumentoResultado> GarantirAsync(string entidadeTipo, Guid entidadeId, CancellationToken ct)
    {
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
            .FirstOrDefaultAsync(d => d.EntidadeTipo == entidadeTipo && d.EntidadeId == entidadeId, ct);

        if (documento is null)
        {
            documento = new DocumentoAssinatura { EntidadeTipo = entidadeTipo, EntidadeId = entidadeId };
            _db.DocumentosAssinatura.Add(documento);
        }

        if (documento.TokenValidacaoPublica is null)
        {
            documento.TokenValidacaoPublica = TokenValidacaoPublicaGerador.Gerar();
            documento.RastreadoEm = DateTime.UtcNow;
        }

        // Congelado a partir da finalização real (FinalizarDocumentoCommand) — aqui só recalcula
        // enquanto o documento ainda está aceitando assinaturas, para o hash sempre refletir os
        // signatários atuais (inclusive zero) em cada novo export do PDF.
        if (documento.Status != StatusDocumentoAssinatura.Finalizado)
        {
            var signatariosParaHash = documento.Signatarios
                .Select(s => new DocumentoSignatarioDto(s.TrabalhadorId, string.Empty, s.MetodoAutenticacao, s.AssinadoEm))
                .ToList();
            documento.ConteudoHash = HashConteudoDocumentoCalculador.Calcular(entidadeTipo, entidadeId, signatariosParaHash);
        }

        await _db.SaveChangesAsync(ct);

        var qr = _qrCode.Gerar(documento.TokenValidacaoPublica);
        return new RastreabilidadeDocumentoResultado(documento.ConteudoHash!, qr.UrlValidacao, qr.Png, documento.Signatarios.Count > 0);
    }
}
```

- [ ] **Step 4: Register in DI**

In `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`, right after the existing line (around line 112):

```csharp
        services.AddScoped<IQrCodeDocumentoService, QrCodeDocumentoService>();
        services.AddScoped<IRegistradorAssinaturaService, RegistradorAssinaturaService>();
```

add:

```csharp
        services.AddScoped<IRegistradorRastreabilidadeService, RegistradorRastreabilidadeService>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter RegistradorRastreabilidadeServiceTests`
Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/IRegistradorRastreabilidadeService.cs src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs tests/AAHBRANT.SST.Application.Tests/Assinatura/RegistradorRastreabilidadeServiceTests.cs
git commit -m "feat: adicionar IRegistradorRastreabilidadeService (hash/token/QR sem finalizar documento)"
```

---

## Task 3: Página pública de validação resolve por token, não por Status

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Assinatura/Queries/ResolverDocumentoPublicoQuery.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Assinatura/ResolverDocumentoPublicoQueryTests.cs`

**Interfaces:**
- Produces: `DocumentoPublicoDto(string EntidadeTipo, DateTime EmitidoEm, string ConteudoHash, bool Assinado, List<DocumentoPublicoSignatarioDto> Signatarios)` — breaking rename from the old `FinalizadoEm`; Task 4 (frontend) depends on this exact shape.

- [ ] **Step 1: Write the failing tests**

Create `tests/AAHBRANT.SST.Application.Tests/Assinatura/ResolverDocumentoPublicoQueryTests.cs`:

```csharp
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class ResolverDocumentoPublicoQueryTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_DocumentoEmAndamentoSemFinalizadoEm_ResolvePorTokenUsandoRastreadoEmComoEmitidoEm()
    {
        var db = CriarDb(nameof(Handle_DocumentoEmAndamentoSemFinalizadoEm_ResolvePorTokenUsandoRastreadoEmComoEmitidoEm));
        var rastreadoEm = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
        db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = "Cipa",
            EntidadeId = Guid.NewGuid(),
            Status = StatusDocumentoAssinatura.EmAndamento,
            TokenValidacaoPublica = "TOKEN123",
            ConteudoHash = "HASHABC",
            RastreadoEm = rastreadoEm,
        });
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN123"), default);

        Assert.NotNull(resultado);
        Assert.Equal(rastreadoEm, resultado!.EmitidoEm);
        Assert.False(resultado.Assinado);
        Assert.Empty(resultado.Signatarios);
    }

    [Fact]
    public async Task Handle_DocumentoFinalizadoComSignatario_ResolveComAssinadoTrue()
    {
        var db = CriarDb(nameof(Handle_DocumentoFinalizadoComSignatario_ResolveComAssinadoTrue));
        var trabalhador = new Trabalhador { Nome = "Maria Teste", Cpf = "11122233344", DataAdmissao = DateTime.UtcNow };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var finalizadoEm = new DateTime(2026, 9, 3, 8, 0, 0, DateTimeKind.Utc);
        var documento = new DocumentoAssinatura
        {
            EntidadeTipo = "Dds",
            EntidadeId = Guid.NewGuid(),
            Status = StatusDocumentoAssinatura.Finalizado,
            TokenValidacaoPublica = "TOKEN456",
            ConteudoHash = "HASHDEF",
            FinalizadoEm = finalizadoEm,
        };
        documento.Signatarios.Add(new DocumentoSignatario { TrabalhadorId = trabalhador.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = finalizadoEm });
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN456"), default);

        Assert.NotNull(resultado);
        Assert.Equal(finalizadoEm, resultado!.EmitidoEm);
        Assert.True(resultado.Assinado);
        Assert.Single(resultado.Signatarios);
    }

    [Fact]
    public async Task Handle_TokenInexistente_RetornaNull()
    {
        var db = CriarDb(nameof(Handle_TokenInexistente_RetornaNull));
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("NAO-EXISTE"), default);

        Assert.Null(resultado);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ResolverDocumentoPublicoQueryTests`
Expected: FAIL — `resultado.EmitidoEm`/`resultado.Assinado` don't exist yet (current DTO has `FinalizadoEm` non-nullable, no `Assinado`), and the first test would 404 today (Status filter).

- [ ] **Step 3: Implement**

Replace the full content of `src/AAHBRANT.SST.Application/Assinatura/Queries/ResolverDocumentoPublicoQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record DocumentoPublicoSignatarioDto(string TrabalhadorNome, MetodoAutenticacaoAssinatura MetodoAutenticacao, DateTime AssinadoEm);

// DTO da página pública de validação (/#/validar/{token}). Deliberadamente sem DocumentoAssinaturaId/
// EntidadeId (ver comentário em DocumentoAssinatura.cs: "Nunca expor Id/EntidadeId/dado pessoal na
// página pública") — só o que o próprio token já revela: tipo do documento, quando foi emitido, hash
// de integridade, se tem assinatura registrada e quem assinou (se houver).
public record DocumentoPublicoDto(
    string EntidadeTipo,
    DateTime EmitidoEm,
    string ConteudoHash,
    bool Assinado,
    List<DocumentoPublicoSignatarioDto> Signatarios);

public record ResolverDocumentoPublicoQuery(string Token) : IRequest<DocumentoPublicoDto?>;

public class ResolverDocumentoPublicoQueryValidator : AbstractValidator<ResolverDocumentoPublicoQuery>
{
    public ResolverDocumentoPublicoQueryValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

public class ResolverDocumentoPublicoQueryHandler : IRequestHandler<ResolverDocumentoPublicoQuery, DocumentoPublicoDto?>
{
    private readonly IAppDbContext _db;

    public ResolverDocumentoPublicoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoPublicoDto?> Handle(ResolverDocumentoPublicoQuery request, CancellationToken ct)
    {
        // Resolve por token, independente de Status: rastreabilidade (Task 2) gera token/hash sem
        // exigir finalização, então um documento ainda EmAndamento (ou que nunca finaliza — CIPA,
        // DDS Semanal) também precisa ser validável publicamente.
        var documento = await _db.DocumentosAssinatura
            .Where(d => d.TokenValidacaoPublica == request.Token)
            .FirstOrDefaultAsync(ct);
        if (documento is null)
            return null;

        var signatarios = await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documento.Id)
            .Join(_db.Trabalhadores, s => s.TrabalhadorId, t => t.Id,
                (s, t) => new DocumentoPublicoSignatarioDto(t.Nome, s.MetodoAutenticacao, s.AssinadoEm))
            .OrderBy(s => s.AssinadoEm)
            .ToListAsync(ct);

        var emitidoEm = documento.FinalizadoEm ?? documento.RastreadoEm ?? documento.CreatedAtUtc;
        return new DocumentoPublicoDto(documento.EntidadeTipo, emitidoEm, documento.ConteudoHash!, signatarios.Count > 0, signatarios);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ResolverDocumentoPublicoQueryTests`
Expected: PASS (3/3).

- [ ] **Step 5: Full solution build check**

Run: `dotnet build`
Expected: any other file referencing the old `FinalizadoEm`/non-nullable shape on `DocumentoPublicoDto` now shows a compile error — there should be none outside `ValidacaoPublicaController.cs` (unaffected, just forwards the DTO) and the frontend (handled in Task 4). If the build shows other C# call sites, read them and adjust to the new DTO shape before moving on.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/Queries/ResolverDocumentoPublicoQuery.cs tests/AAHBRANT.SST.Application.Tests/Assinatura/ResolverDocumentoPublicoQueryTests.cs
git commit -m "fix: página pública de validação resolve por token, não mais só documentos Finalizados"
```

---

## Task 4: Frontend — `DocumentoPublico` type + `ValidarDocumentoPage`

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts:1895-1900`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/validacao/ValidarDocumentoPage.tsx`

**Interfaces:**
- Consumes: the `DocumentoPublicoDto` JSON shape produced by Task 3 (`entidadeTipo`, `emitidoEm`, `conteudoHash`, `assinado`, `signatarios`).

- [ ] **Step 1: Update the TypeScript interface**

In `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, replace:

```typescript
export interface DocumentoPublico {
  entidadeTipo: string;
  finalizadoEm: string;
  conteudoHash: string;
  signatarios: DocumentoPublicoSignatario[];
}
```

with:

```typescript
export interface DocumentoPublico {
  entidadeTipo: string;
  emitidoEm: string;
  conteudoHash: string;
  assinado: boolean;
  signatarios: DocumentoPublicoSignatario[];
}
```

- [ ] **Step 2: Update the page**

In `src/AAHBRANT.SST.TeamsApp/src/pages/validacao/ValidarDocumentoPage.tsx`, replace the header block (lines 93-108):

```tsx
        {!carregando && documento && (
          <>
            <div className={estilos.header}>
              <CheckmarkCircle24Regular color={tokens.colorPaletteGreenForeground1} />
              <div>
                <Text size={600} weight="semibold">
                  Documento válido
                </Text>
                <div>
                  <Text size={200}>
                    {documento.entidadeTipo} · finalizado em{' '}
                    {new Date(documento.finalizadoEm).toLocaleString('pt-BR')}
                  </Text>
                </div>
              </div>
            </div>

            <div className={estilos.secao}>
              <Text weight="semibold">Assinaturas registradas</Text>
              <ul className={estilos.listaSimples}>
                {documento.signatarios.map((s, i) => (
                  <li key={i}>
                    <Text>
                      {s.trabalhadorNome} —{' '}
                      <Badge appearance="tint" size="small">
                        {metodoAutenticacaoAssinaturaLabel[s.metodoAutenticacao] ?? 'Método desconhecido'}
                      </Badge>{' '}
                      em {new Date(s.assinadoEm).toLocaleString('pt-BR')}
                    </Text>
                  </li>
                ))}
              </ul>
            </div>
```

with:

```tsx
        {!carregando && documento && (
          <>
            <div className={estilos.header}>
              <CheckmarkCircle24Regular color={tokens.colorPaletteGreenForeground1} />
              <div>
                <Text size={600} weight="semibold">
                  {documento.assinado ? 'Documento válido' : 'Documento rastreável'}
                </Text>
                <div>
                  <Text size={200}>
                    {documento.entidadeTipo} · emitido em{' '}
                    {new Date(documento.emitidoEm).toLocaleString('pt-BR')}
                  </Text>
                </div>
              </div>
            </div>

            <div className={estilos.secao}>
              <Text weight="semibold">Assinaturas registradas</Text>
              {documento.signatarios.length === 0 ? (
                <Text as="p">Nenhuma assinatura eletrônica registrada até o momento.</Text>
              ) : (
                <ul className={estilos.listaSimples}>
                  {documento.signatarios.map((s, i) => (
                    <li key={i}>
                      <Text>
                        {s.trabalhadorNome} —{' '}
                        <Badge appearance="tint" size="small">
                          {metodoAutenticacaoAssinaturaLabel[s.metodoAutenticacao] ?? 'Método desconhecido'}
                        </Badge>{' '}
                        em {new Date(s.assinadoEm).toLocaleString('pt-BR')}
                      </Text>
                    </li>
                  ))}
                </ul>
              )}
            </div>
```

- [ ] **Step 3: Type-check the frontend**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit`
Expected: no errors referencing `DocumentoPublico`/`ValidarDocumentoPage`.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts src/AAHBRANT.SST.TeamsApp/src/pages/validacao/ValidarDocumentoPage.tsx
git commit -m "fix: página pública de validação reflete documento rastreável vs assinado"
```

---

## Task 5: `RodapeDocumentoPadrao` — componente compartilhado

**Files:**
- Create: `src/AAHBRANT.SST.Infrastructure/Documentos/RodapeDocumentoPadrao.cs`

**Interfaces:**
- Produces: `RodapeDocumentoPadrao.Desenhar(ColumnDescriptor coluna, string tituloDocumento, string? protocolo, int? revisao, string conteudoHash, string urlValidacaoPublica, byte[] qrCodePng, bool temAssinatura)`, called by every task from Task 6 onward as `pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(coluna, ...))`.

- [ ] **Step 1: Implement**

Create `src/AAHBRANT.SST.Infrastructure/Documentos/RodapeDocumentoPadrao.cs`:

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Rodapé padrão de rastreabilidade/validação digital aplicado a todo documento gerado — protocolo,
// hash+QR de validação pública (Motor de Assinatura Eletrônica, via IRegistradorRastreabilidadeService,
// que nunca depende de o documento estar Finalizado), nota de assinatura digital (só quando há
// signatário real) e paginação. Ver docs/superpowers/specs/2026-09-04-rodape-validacao-documentos-design.md.
internal static class RodapeDocumentoPadrao
{
    public static void Desenhar(
        ColumnDescriptor coluna,
        string tituloDocumento,
        string? protocolo,
        int? revisao,
        string conteudoHash,
        string urlValidacaoPublica,
        byte[] qrCodePng,
        bool temAssinatura)
    {
        coluna.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
        coluna.Item().PaddingTop(2).Row(linha =>
        {
            linha.RelativeItem().Column(textoColuna =>
            {
                textoColuna.Item().AlignCenter().Text(t =>
                {
                    t.Span("AAHBRANT SST").FontSize(7).SemiBold();
                    if (!string.IsNullOrEmpty(protocolo))
                        t.Span($" | {tituloDocumento} nº {protocolo}").FontSize(7);
                    if (revisao is not null)
                        t.Span($" — Revisão {revisao}").FontSize(7);
                });

                if (temAssinatura)
                {
                    textoColuna.Item().AlignCenter()
                        .Text("Documento assinado digitalmente conforme MP nº 2.200-2/2001 e Lei nº 14.063/2020.")
                        .FontSize(6.5f).Italic();
                }

                textoColuna.Item().AlignCenter().Text(t =>
                {
                    // Chave curta: atalho visual (8 primeiros caracteres do hash SHA-256, maiúsculos,
                    // formatado XXXX-XXXX) — a conferência de fato acontece pelo QR/link, que carrega
                    // o token completo, não pelo hash em si.
                    var chaveCurta = conteudoHash.Length >= 8 ? $"{conteudoHash[..4]}-{conteudoHash[4..8]}" : conteudoHash;
                    t.Span($"Validável em {urlValidacaoPublica} — chave {chaveCurta} | Emitido em ").FontSize(6.5f);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(6.5f);
                });

                textoColuna.Item().AlignCenter().Text(t =>
                {
                    t.Span("Página ").FontSize(6.5f);
                    t.CurrentPageNumber().FontSize(6.5f);
                    t.Span(" de ").FontSize(6.5f);
                    t.TotalPages().FontSize(6.5f);
                });
            });

            linha.ConstantItem(28).AlignRight().Image(qrCodePng).FitArea();
        });
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure`
Expected: builds with no errors — this confirms `TextSpanDescriptor.CurrentPageNumber()`/`.TotalPages()` (standard QuestPDF API) compile against the project's pinned QuestPDF version. If they don't exist under those exact names, check the installed QuestPDF version (`grep QuestPDF src/AAHBRANT.SST.Infrastructure/AAHBRANT.SST.Infrastructure.csproj`) and adjust to that version's page-number API before continuing — every later task depends on this file compiling.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Documentos/RodapeDocumentoPadrao.cs
git commit -m "feat: adicionar RodapeDocumentoPadrao compartilhado (protocolo+hash+QR+paginação)"
```

---

## Task 6: DDS (diário) — numeração + rodapé

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Dds/Dds.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/DdsConfiguracoes.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Commands/CriarDdsCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/IDdsPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/DdsPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Commands/EnviarDdsTelegramCommand.cs` (secondary caller of `ExportarDdsPdfQueryHandler.MontarModelo` — see Step 10)
- Modify: `tests/AAHBRANT.SST.Application.Tests/Dds/CriarDdsCommandHandlerTests.cs` (existing 6 tests need the new constructor arg)
- Test: add one new test to the same file

**Interfaces:**
- Consumes: `IGeradorNumeroDocumentoService.GerarAsync(string prefixo, CancellationToken ct) -> string` (existing), `IRegistradorRastreabilidadeService.GarantirAsync` (Task 2).
- Produces: `Dds.NumeroDocumento` (string?); `DdsPdfModelo` gains `Protocolo`, `ConteudoHash`, `UrlValidacaoPublica`, `QrCodePng`, `TemAssinatura`.

- [ ] **Step 1: Add the entity field**

In `src/AAHBRANT.SST.Domain/Entidades/Dds/Dds.cs`, in the `Dds` class, right after `public Guid ResponsavelUsuarioId { get; set; }` / `public Usuario? ResponsavelUsuario { get; set; }`:

```csharp
    public Guid ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    // Protocolo automático (prefixo "DDS-D" — "DDS" já pertence ao DdsSemanal), gerado uma única vez
    // na criação (CriarDdsCommand), mesmo padrão de DdsSemanal/Cipa/Pcmso/Certificado.
    public string? NumeroDocumento { get; set; }
```

- [ ] **Step 2: Configure the column**

In `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/DdsConfiguracoes.cs`, in `DdsConfiguracao.Configure`, add after `builder.Property(d => d.TemaLivreDescricao).HasMaxLength(500);`:

```csharp
        builder.Property(d => d.NumeroDocumento).HasMaxLength(50);
```

- [ ] **Step 3: Generate the migration**

Run: `dotnet ef migrations add AdicionarNumeroDocumentoDds --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`
Expected: adds one nullable `nvarchar(50)` column `NumeroDocumento` to the `Dds` table only.

- [ ] **Step 4: Wire numbering into creation**

In `src/AAHBRANT.SST.Application/Dds/Commands/CriarDdsCommand.cs`:

Replace the handler class declaration and constructor:

```csharp
public class CriarDdsCommandHandler : IRequestHandler<CriarDdsCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarDdsCommandHandler(IAppDbContext db) => _db = db;
```

with:

```csharp
public class CriarDdsCommandHandler : IRequestHandler<CriarDdsCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public CriarDdsCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }
```

Then, in the `dds` object construction:

```csharp
        var dds = new Domain.Entidades.Dds
        {
            ObraId = semanal.ObraId,
            DdsSemanalId = semanal.Id,
            Data = request.Data.Date,
            ResponsavelUsuarioId = semanal.ResponsavelUsuarioId,
        };
```

becomes:

```csharp
        var dds = new Domain.Entidades.Dds
        {
            ObraId = semanal.ObraId,
            DdsSemanalId = semanal.Id,
            Data = request.Data.Date,
            ResponsavelUsuarioId = semanal.ResponsavelUsuarioId,
            NumeroDocumento = await _geradorNumero.GerarAsync("DDS-D", ct),
        };
```

(add `using AAHBRANT.SST.Application.Common.Interfaces;` at the top if not already present — it is, since `IAppDbContext` already comes from there).

- [ ] **Step 5: Update existing tests' handler construction**

In `tests/AAHBRANT.SST.Application.Tests/Dds/CriarDdsCommandHandlerTests.cs`, every occurrence of:

```csharp
        var handler = new CriarDdsCommandHandler(db);
```

(there are 6 — one per `[Fact]`) becomes:

```csharp
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));
```

Add the using at the top of the file: `using AAHBRANT.SST.Infrastructure.Documentos;`

- [ ] **Step 6: Add the new test**

Append to the same test file, before the final closing `}`:

```csharp
    [Fact]
    public async Task Handle_CriaDds_GeraNumeroDocumentoComPrefixoDDS_D()
    {
        var db = CriarDb(nameof(Handle_CriaDds_GeraNumeroDocumentoComPrefixoDDS_D));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, null), default);

        var dds = await db.Dds.FirstAsync(d => d.Id == id);
        Assert.StartsWith("DDS-D-", dds.NumeroDocumento);
    }
```

- [ ] **Step 7: Run the DDS creation tests**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter CriarDdsCommandHandlerTests`
Expected: PASS (7/7 — 6 existing + 1 new).

- [ ] **Step 8: Add the new fields to the PDF model**

In `src/AAHBRANT.SST.Application/Dds/IDdsPdfService.cs`, replace:

```csharp
public record DdsPdfModelo(
    string ObraNome,
    byte[]? ObraLogoConteudo,
    DateTime Data,
    string ResponsavelNome,
    IReadOnlyList<DdsPdfTemaModelo> Temas,
    string? TemaLivreNome,
    string? TemaLivreDescricao,
    IReadOnlyList<(string Descricao, bool Verificado)> ItensChecklist,
    IReadOnlyList<string> ParticipantesNomes);
```

with:

```csharp
public record DdsPdfModelo(
    string ObraNome,
    byte[]? ObraLogoConteudo,
    DateTime Data,
    string ResponsavelNome,
    IReadOnlyList<DdsPdfTemaModelo> Temas,
    string? TemaLivreNome,
    string? TemaLivreDescricao,
    IReadOnlyList<(string Descricao, bool Verificado)> ItensChecklist,
    IReadOnlyList<string> ParticipantesNomes,
    string? Protocolo,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);
```

- [ ] **Step 9: Wire rastreabilidade into the export query**

`ExportarDdsPdfQuery.cs`'s `MontarModelo` static method is reused elsewhere (`EnviarDdsTelegramCommandHandler`, per its own comment) purely to build the visual model — it must stay callable without a DB round-trip for rastreability. Keep `MontarModelo` producing everything BUT the 5 new fields, and assemble the final call only in `Handle`. Replace the full file content:

```csharp
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ExportarDdsPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarDdsPdfQueryHandler : IRequestHandler<ExportarDdsPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IDdsPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarDdsPdfQueryHandler(IMediator mediator, IAppDbContext db, IDdsPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarDdsPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        var dds = await _db.Dds.FirstAsync(d => d.Id == request.Id, ct);
        var logoConteudo = await _db.Obras.Where(o => o.Id == detalhe.Dds.ObraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct);
        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Domain.Entidades.Dds), request.Id, ct);

        return _pdf.Gerar(MontarModelo(detalhe, logoConteudo, dds.NumeroDocumento, rastreio));
    }

    // Reaproveitado por EnviarDdsTelegramCommandHandler para não duplicar a montagem do modelo do PDF.
    public static DdsPdfModelo MontarModelo(DdsDetalheDto detalhe, byte[]? obraLogoConteudo, string? protocolo, RastreabilidadeDocumentoResultado rastreio) => new(
        detalhe.Dds.ObraNome,
        obraLogoConteudo,
        detalhe.Dds.Data,
        detalhe.Dds.ResponsavelUsuarioNome,
        detalhe.Dds.TemasAtividades.Select(t => new DdsPdfTemaModelo(
            t.AtividadeNome, t.PerigoNome, t.PerigoDescricao, t.Consequencia, t.ControlesExistentes, t.ControlesAdicionais)).ToList(),
        detalhe.Dds.TemaLivreNome,
        detalhe.Dds.TemaLivreDescricao,
        detalhe.ItensChecklist.Select(i => (i.Descricao, i.Verificado)).ToList(),
        detalhe.Participantes.Select(p => p.TrabalhadorNome).ToList(),
        protocolo,
        rastreio.ConteudoHash,
        rastreio.UrlValidacaoPublica,
        rastreio.QrCodePng,
        rastreio.TemAssinatura);
}
```

- [ ] **Step 10: Fix the other caller of `MontarModelo`**

`src/AAHBRANT.SST.Application/Dds/Commands/EnviarDdsTelegramCommand.cs` also calls `ExportarDdsPdfQueryHandler.MontarModelo` to attach the PDF to a Telegram message — it needs the same rastreabilidade wiring so that PDF's QR is real too (not a fabricated/blank one). Add `using AAHBRANT.SST.Application.Assinatura;` and `using AAHBRANT.SST.Domain.Entidades;` at the top. Replace:

```csharp
public class EnviarDdsTelegramCommandHandler : IRequestHandler<EnviarDdsTelegramCommand, EnviarDdsTelegramResultado>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IDdsPdfService _pdf;
    private readonly ITelegramService _telegram;

    public EnviarDdsTelegramCommandHandler(IAppDbContext db, IMediator mediator, IDdsPdfService pdf, ITelegramService telegram)
    {
        _db = db;
        _mediator = mediator;
        _pdf = pdf;
        _telegram = telegram;
    }

    public async Task<EnviarDdsTelegramResultado> Handle(EnviarDdsTelegramCommand request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(request.Id), ct)
            ?? throw new KeyNotFoundException($"DDS {request.Id} não encontrado.");

        var trabalhadorIds = detalhe.Participantes.Select(p => p.TrabalhadorId).ToList();
        var vinculados = await _db.Trabalhadores
            .Where(t => trabalhadorIds.Contains(t.Id) && t.TelegramChatId != null)
            .Select(t => new { t.Id, ChatId = t.TelegramChatId!.Value })
            .ToListAsync(ct);

        var logoConteudo = await _db.Obras.Where(o => o.Id == detalhe.Dds.ObraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct);
        var pdfBytes = _pdf.Gerar(ExportarDdsPdfQueryHandler.MontarModelo(detalhe, logoConteudo));
```

with:

```csharp
public class EnviarDdsTelegramCommandHandler : IRequestHandler<EnviarDdsTelegramCommand, EnviarDdsTelegramResultado>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IDdsPdfService _pdf;
    private readonly ITelegramService _telegram;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public EnviarDdsTelegramCommandHandler(IAppDbContext db, IMediator mediator, IDdsPdfService pdf, ITelegramService telegram, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _mediator = mediator;
        _pdf = pdf;
        _telegram = telegram;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<EnviarDdsTelegramResultado> Handle(EnviarDdsTelegramCommand request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(request.Id), ct)
            ?? throw new KeyNotFoundException($"DDS {request.Id} não encontrado.");

        var trabalhadorIds = detalhe.Participantes.Select(p => p.TrabalhadorId).ToList();
        var vinculados = await _db.Trabalhadores
            .Where(t => trabalhadorIds.Contains(t.Id) && t.TelegramChatId != null)
            .Select(t => new { t.Id, ChatId = t.TelegramChatId!.Value })
            .ToListAsync(ct);

        var dds = await _db.Dds.FirstAsync(d => d.Id == request.Id, ct);
        var logoConteudo = await _db.Obras.Where(o => o.Id == detalhe.Dds.ObraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct);
        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Dds), request.Id, ct);
        var pdfBytes = _pdf.Gerar(ExportarDdsPdfQueryHandler.MontarModelo(detalhe, logoConteudo, dds.NumeroDocumento, rastreio));
```

(the rest of the method body — from `var nomeArquivo = ...` to the end — stays unchanged).

- [ ] **Step 11: Swap the footer in the PDF service**

In `src/AAHBRANT.SST.Infrastructure/Documentos/DdsPdfService.cs`, replace:

```csharp
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(9);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
```

with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "DDS", modelo.Protocolo, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
```

- [ ] **Step 12: Build**

Run: `dotnet build`
Expected: no errors. Pay attention to the Telegram handler from Step 10 — it's the one place most likely to still reference the old 2-arg `MontarModelo`.

- [ ] **Step 13: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Dds/Dds.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/DdsConfiguracoes.cs src/AAHBRANT.SST.Application/Dds/Commands/CriarDdsCommand.cs src/AAHBRANT.SST.Application/Dds/Commands/EnviarDdsTelegramCommand.cs src/AAHBRANT.SST.Application/Dds/IDdsPdfService.cs src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsPdfQuery.cs src/AAHBRANT.SST.Infrastructure/Documentos/DdsPdfService.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/ tests/AAHBRANT.SST.Application.Tests/Dds/CriarDdsCommandHandlerTests.cs
git commit -m "feat: numeração automática + rodapé de rastreabilidade no PDF do DDS diário"
```

---

## Task 7: DDS Semanal — rodapé (numeração já existe)

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsSemanalPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/IDdsSemanalPdfService.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/DdsSemanalPdfService.cs`

**Interfaces:**
- Consumes: `IRegistradorRastreabilidadeService.GarantirAsync` (Task 2). DDS Semanal never has real signers (no `DocumentoAssinatura` is ever created for `EntidadeTipo == nameof(DdsSemanal)` anywhere in the codebase) — `TemAssinatura` will always be `false`, which is correct: this is a pure rastreability document, never digitally signed.

- [ ] **Step 1: Add fields to the model**

In `src/AAHBRANT.SST.Application/Dds/IDdsSemanalPdfService.cs`, replace:

```csharp
public record DdsSemanalPdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string TipoLabel,
    string? EmpresaTerceirizada,
    string? NumeroDocumento,
    string? LocalFrenteServico,
    string ResponsavelNome,
    DateTime DataInicioSemana,
    DateTime DataFimSemana,
    IReadOnlyList<DdsSemanalPdfDiaModelo> Dias,
    IReadOnlyList<DdsSemanalPdfLinhaPresenca> Presencas,
    string? ResponsavelObraSstNome,
    string? ResponsavelEmpresaTerceirizadaNome,
    string? ResponsavelEmpresaTerceirizadaFuncao);
```

with:

```csharp
public record DdsSemanalPdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string TipoLabel,
    string? EmpresaTerceirizada,
    string? NumeroDocumento,
    string? LocalFrenteServico,
    string ResponsavelNome,
    DateTime DataInicioSemana,
    DateTime DataFimSemana,
    IReadOnlyList<DdsSemanalPdfDiaModelo> Dias,
    IReadOnlyList<DdsSemanalPdfLinhaPresenca> Presencas,
    string? ResponsavelObraSstNome,
    string? ResponsavelEmpresaTerceirizadaNome,
    string? ResponsavelEmpresaTerceirizadaFuncao,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);
```

- [ ] **Step 2: Wire rastreabilidade into the export query**

In `src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsSemanalPdfQuery.cs`, add `using AAHBRANT.SST.Application.Assinatura;` at the top. Replace:

```csharp
public class ExportarDdsSemanalPdfQueryHandler : IRequestHandler<ExportarDdsSemanalPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IDdsSemanalPdfService _pdf;

    public ExportarDdsSemanalPdfQueryHandler(IMediator mediator, IAppDbContext db, IDdsSemanalPdfService pdf)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
    }
```

with:

```csharp
public class ExportarDdsSemanalPdfQueryHandler : IRequestHandler<ExportarDdsSemanalPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IDdsSemanalPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarDdsSemanalPdfQueryHandler(IMediator mediator, IAppDbContext db, IDdsSemanalPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }
```

Then, right before `var modelo = new DdsSemanalPdfModelo(`, add:

```csharp
        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Domain.Entidades.DdsSemanal), request.Id, ct);

```

and replace:

```csharp
        var modelo = new DdsSemanalPdfModelo(
            detalhe.Semanal.ObraNome,
            logoConteudo,
            detalhe.Semanal.Tipo == TipoDdsSemanal.Terceirizados ? "Empregados Terceirizados" : "Empregados Próprios",
            detalhe.Semanal.EmpresaTerceirizada,
            detalhe.Semanal.NumeroDocumento,
            detalhe.Semanal.LocalFrenteServico,
            detalhe.Semanal.ResponsavelUsuarioNome,
            detalhe.Semanal.DataInicioSemana,
            detalhe.Semanal.DataFimSemana,
            dias,
            presencas,
            detalhe.Semanal.ResponsavelObraSstNome,
            detalhe.Semanal.ResponsavelEmpresaTerceirizadaNome,
            detalhe.Semanal.ResponsavelEmpresaTerceirizadaFuncao);
```

with:

```csharp
        var modelo = new DdsSemanalPdfModelo(
            detalhe.Semanal.ObraNome,
            logoConteudo,
            detalhe.Semanal.Tipo == TipoDdsSemanal.Terceirizados ? "Empregados Terceirizados" : "Empregados Próprios",
            detalhe.Semanal.EmpresaTerceirizada,
            detalhe.Semanal.NumeroDocumento,
            detalhe.Semanal.LocalFrenteServico,
            detalhe.Semanal.ResponsavelUsuarioNome,
            detalhe.Semanal.DataInicioSemana,
            detalhe.Semanal.DataFimSemana,
            dias,
            presencas,
            detalhe.Semanal.ResponsavelObraSstNome,
            detalhe.Semanal.ResponsavelEmpresaTerceirizadaNome,
            detalhe.Semanal.ResponsavelEmpresaTerceirizadaFuncao,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            rastreio.TemAssinatura);
```

- [ ] **Step 3: Swap the footer**

In `src/AAHBRANT.SST.Infrastructure/Documentos/DdsSemanalPdfService.cs`, replace:

```csharp
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(8);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                });
```

with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "DDS Semanal", modelo.NumeroDocumento, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsSemanalPdfQuery.cs src/AAHBRANT.SST.Application/Dds/IDdsSemanalPdfService.cs src/AAHBRANT.SST.Infrastructure/Documentos/DdsSemanalPdfService.cs
git commit -m "feat: rodapé de rastreabilidade no PDF do DDS Semanal"
```

---

## Task 8: APR — rodapé (`TemAssinatura` vem de `AprAssinatura`, não do Motor de Assinatura)

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Aprs/IAprPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Aprs/Queries/ExportarAprPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/AprPdfService.cs`

**Interfaces:**
- Consumes: `IRegistradorRastreabilidadeService.GarantirAsync` (Task 2).
- **Important:** APR uses its own dedicated `AprAssinatura` table (`CriarAprAssinaturaCommand`), never `DocumentoAssinatura` — confirmed by grep, no `nameof(Apr)` anywhere against `DocumentosAssinatura`. `IRegistradorRastreabilidadeService.GarantirAsync("Apr", ...)` still works fine for hash/QR (it creates its own tracking `DocumentoAssinatura` row that will just always have 0 signers), but its `TemAssinatura` output must be **ignored** for the footer — use `detalhe.Assinaturas.Count > 0` (the real APR signature data, already loaded by `ObterAprDetalheQuery`) instead.

- [ ] **Step 1: Add fields to the model**

In `src/AAHBRANT.SST.Application/Aprs/IAprPdfService.cs`, replace:

```csharp
public record AprPdfModelo(
    string? NumeroApr,
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string AtividadeNome,
    string Local,
    string? MaquinasEquipamentos,
    string? PgrReferencia,
    DateTime Data,
    IReadOnlyList<AprPdfEnvolvido> Envolvidos,
    IReadOnlyList<AprPdfRiscoLinha> Riscos,
    AprPdfAssinatura Elaboracao,
    AprPdfAssinatura Supervisao);
```

with:

```csharp
public record AprPdfModelo(
    string? NumeroApr,
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string AtividadeNome,
    string Local,
    string? MaquinasEquipamentos,
    string? PgrReferencia,
    DateTime Data,
    IReadOnlyList<AprPdfEnvolvido> Envolvidos,
    IReadOnlyList<AprPdfRiscoLinha> Riscos,
    AprPdfAssinatura Elaboracao,
    AprPdfAssinatura Supervisao,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);
```

- [ ] **Step 2: Wire rastreabilidade into the export query**

In `src/AAHBRANT.SST.Application/Aprs/Queries/ExportarAprPdfQuery.cs`:

Add `using AAHBRANT.SST.Application.Assinatura;` at the top. Update the class:

```csharp
public class ExportarAprPdfQueryHandler : IRequestHandler<ExportarAprPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IAprPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAprPdfQueryHandler(IMediator mediator, IAppDbContext db, IAprPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarAprPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterAprDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        byte[]? logoConteudo = detalhe.Apr.ObraId is { } obraId
            ? await _db.Obras.Where(o => o.Id == obraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct)
            : null;

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Domain.Entidades.Apr), request.Id, ct);

        return _pdf.Gerar(MontarModelo(detalhe, logoConteudo, rastreio));
    }

    public static AprPdfModelo MontarModelo(AprDetalheDto detalhe, byte[]? obraLogoConteudo, RastreabilidadeDocumentoResultado rastreio)
    {
```

(keep the rest of the method body unchanged down to the `return new AprPdfModelo(...)` call), then replace:

```csharp
        return new AprPdfModelo(
            detalhe.Apr.NumeroApr,
            detalhe.Apr.ObraNome,
            obraLogoConteudo,
            detalhe.Apr.AtividadeNome,
            detalhe.Apr.Local,
            detalhe.Apr.MaquinasEquipamentos,
            detalhe.Apr.PgrReferencia,
            detalhe.Apr.Data,
            envolvidos,
            riscos,
            new AprPdfAssinatura(elaboracao?.TrabalhadorNome, null, elaboracao?.DataAssinatura),
            new AprPdfAssinatura(supervisao?.TrabalhadorNome, null, supervisao?.DataAssinatura));
    }
```

with:

```csharp
        return new AprPdfModelo(
            detalhe.Apr.NumeroApr,
            detalhe.Apr.ObraNome,
            obraLogoConteudo,
            detalhe.Apr.AtividadeNome,
            detalhe.Apr.Local,
            detalhe.Apr.MaquinasEquipamentos,
            detalhe.Apr.PgrReferencia,
            detalhe.Apr.Data,
            envolvidos,
            riscos,
            new AprPdfAssinatura(elaboracao?.TrabalhadorNome, null, elaboracao?.DataAssinatura),
            new AprPdfAssinatura(supervisao?.TrabalhadorNome, null, supervisao?.DataAssinatura),
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            // TemAssinatura vem da tabela própria AprAssinatura (Motor de Assinatura Eletrônica não é
            // usado pela APR) — rastreio.TemAssinatura é deliberadamente ignorado aqui.
            detalhe.Assinaturas.Count > 0);
    }
```

- [ ] **Step 3: Swap the footer**

In `src/AAHBRANT.SST.Infrastructure/Documentos/AprPdfService.cs`, replace:

```csharp
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(7);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7);
                });
```

with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "APR", modelo.NumeroApr, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Aprs/IAprPdfService.cs src/AAHBRANT.SST.Application/Aprs/Queries/ExportarAprPdfQuery.cs src/AAHBRANT.SST.Infrastructure/Documentos/AprPdfService.cs
git commit -m "feat: rodapé de rastreabilidade no PDF da APR"
```

---

## Task 9: PT (Permissão de Trabalho) — rodapé

**Files:**
- Modify: `src/AAHBRANT.SST.Application/PermissoesTrabalho/IPtPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/PermissoesTrabalho/Queries/ExportarPermissaoTrabalhoPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/PtPdfService.cs`

**Interfaces:**
- Consumes: `IRegistradorRastreabilidadeService.GarantirAsync` (Task 2). PT already uses `DocumentoAssinatura(nameof(PermissaoTrabalho))` for "ciência da equipe" — `rastreio.TemAssinatura` is accurate here (no override needed, unlike APR).

- [ ] **Step 1: Add fields to the model**

In `src/AAHBRANT.SST.Application/PermissoesTrabalho/IPtPdfService.cs`, append 4 parameters to `PtPdfModelo` (after `List<PtPdfEnvolvido> Envolvidos`):

```csharp
    List<PtPdfEnvolvido> Envolvidos,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);
```

- [ ] **Step 2: Wire rastreabilidade into the export query**

In `src/AAHBRANT.SST.Application/PermissoesTrabalho/Queries/ExportarPermissaoTrabalhoPdfQuery.cs`, add `using AAHBRANT.SST.Application.Assinatura;` at the top. Replace:

```csharp
public class ExportarPermissaoTrabalhoPdfQueryHandler : IRequestHandler<ExportarPermissaoTrabalhoPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IPtPdfService _pdf;

    public ExportarPermissaoTrabalhoPdfQueryHandler(IMediator mediator, IAppDbContext db, IPtPdfService pdf)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarPermissaoTrabalhoPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterPermissaoTrabalhoDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        // Ciência da equipe (§9) usa o Motor de Assinatura Eletrônica (DocumentoAssinatura), não um
        // campo próprio da PT — mesmo padrão já usado por Dds/EntregaEpi.
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
            .FirstOrDefaultAsync(d => d.EntidadeTipo == nameof(PermissaoTrabalho) && d.EntidadeId == request.Id, ct);
        var assinaram = documento?.Signatarios.Select(s => s.TrabalhadorId).ToHashSet() ?? new HashSet<Guid>();

        byte[]? logoConteudo = detalhe.PermissaoTrabalho.ObraId is { } obraId
            ? await _db.Obras.Where(o => o.Id == obraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct)
            : null;

        return _pdf.Gerar(MontarModelo(detalhe, assinaram, logoConteudo));
    }
```

with:

```csharp
public class ExportarPermissaoTrabalhoPdfQueryHandler : IRequestHandler<ExportarPermissaoTrabalhoPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IPtPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarPermissaoTrabalhoPdfQueryHandler(IMediator mediator, IAppDbContext db, IPtPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarPermissaoTrabalhoPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterPermissaoTrabalhoDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        // Ciência da equipe (§9) usa o Motor de Assinatura Eletrônica (DocumentoAssinatura), não um
        // campo próprio da PT — mesmo padrão já usado por Dds/EntregaEpi.
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
            .FirstOrDefaultAsync(d => d.EntidadeTipo == nameof(PermissaoTrabalho) && d.EntidadeId == request.Id, ct);
        var assinaram = documento?.Signatarios.Select(s => s.TrabalhadorId).ToHashSet() ?? new HashSet<Guid>();

        byte[]? logoConteudo = detalhe.PermissaoTrabalho.ObraId is { } obraId
            ? await _db.Obras.Where(o => o.Id == obraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct)
            : null;

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(PermissaoTrabalho), request.Id, ct);

        return _pdf.Gerar(MontarModelo(detalhe, assinaram, logoConteudo, rastreio));
    }
```

Then update the `MontarModelo` signature and closing call:

```csharp
    private static PtPdfModelo MontarModelo(PermissaoTrabalhoDetalheDto detalhe, HashSet<Guid> assinaram, byte[]? obraLogoConteudo)
    {
        var pt = detalhe.PermissaoTrabalho;
        return new PtPdfModelo(
```

becomes:

```csharp
    private static PtPdfModelo MontarModelo(PermissaoTrabalhoDetalheDto detalhe, HashSet<Guid> assinaram, byte[]? obraLogoConteudo, RastreabilidadeDocumentoResultado rastreio)
    {
        var pt = detalhe.PermissaoTrabalho;
        return new PtPdfModelo(
```

and the final line of the `return new PtPdfModelo(...)` call:

```csharp
            detalhe.Responsaveis.Select(r => new PtPdfEnvolvido(r.TrabalhadorNome, r.TrabalhadorFuncaoNome, assinaram.Contains(r.TrabalhadorId))).ToList());
```

becomes:

```csharp
            detalhe.Responsaveis.Select(r => new PtPdfEnvolvido(r.TrabalhadorNome, r.TrabalhadorFuncaoNome, assinaram.Contains(r.TrabalhadorId))).ToList(),
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            rastreio.TemAssinatura);
```

- [ ] **Step 3: Swap the footer**

In `src/AAHBRANT.SST.Infrastructure/Documentos/PtPdfService.cs`, replace:

```csharp
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(7);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7);
                });
```

with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "PT", modelo.NumeroPt, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/PermissoesTrabalho/IPtPdfService.cs src/AAHBRANT.SST.Application/PermissoesTrabalho/Queries/ExportarPermissaoTrabalhoPdfQuery.cs src/AAHBRANT.SST.Infrastructure/Documentos/PtPdfService.cs
git commit -m "feat: rodapé de rastreabilidade no PDF da Permissão de Trabalho"
```

---

## Task 10: CIPA — Ata de Eleição + Ata de Reunião

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Cipa/ICipaPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Cipa/Queries/ExportarAtaCipaPdfQueries.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/CipaPdfService.cs`

**Interfaces:**
- Consumes: `IRegistradorRastreabilidadeService.GarantirAsync` (Task 2). CIPA never uses `DocumentoAssinatura` for either ata — `TemAssinatura` will always be `false`, correctly (these are printed-signature-line documents).

- [ ] **Step 1: Add fields to both models**

In `src/AAHBRANT.SST.Application/Cipa/ICipaPdfService.cs`, append to `AtaEleicaoCipaPdfModelo` (after `IReadOnlyList<AtaEleicaoCipaCandidatoModelo> Candidatos`):

```csharp
    IReadOnlyList<AtaEleicaoCipaCandidatoModelo> Candidatos,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng);
```

and to `AtaReuniaoCipaPdfModelo` (after `IReadOnlyList<AtaReuniaoCipaParticipanteModelo> Participantes`):

```csharp
    IReadOnlyList<AtaReuniaoCipaParticipanteModelo> Participantes,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng);
```

(No `TemAssinatura` param needed on either — both always render the "sem assinatura digital" branch of the footer, so `RodapeDocumentoPadrao.Desenhar` will be called with a literal `false`.)

- [ ] **Step 2: Wire rastreabilidade into both export queries**

Replace the full content of `src/AAHBRANT.SST.Application/Cipa/Queries/ExportarAtaCipaPdfQueries.cs`:

```csharp
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public record ExportarAtaEleicaoCipaPdfQuery(Guid ProcessoEleitoralId) : IRequest<byte[]?>;

public class ExportarAtaEleicaoCipaPdfQueryHandler : IRequestHandler<ExportarAtaEleicaoCipaPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly ICipaPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAtaEleicaoCipaPdfQueryHandler(IMediator mediator, IAppDbContext db, ICipaPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarAtaEleicaoCipaPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterProcessoEleitoralCipaDetalheQuery(request.ProcessoEleitoralId), ct);
        if (detalhe is null) return null;

        var logoConteudo = await _db.Obras
            .Where(o => o.Id == detalhe.Processo.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);

        var statusLabel = new Dictionary<StatusCandidatoCipa, string>
        {
            [StatusCandidatoCipa.Eleito] = "Eleito (titular)",
            [StatusCandidatoCipa.Suplente] = "Eleito (suplente)",
            [StatusCandidatoCipa.NaoEleito] = "Não eleito",
            [StatusCandidatoCipa.Deferido] = "Aguardando apuração",
            [StatusCandidatoCipa.Inscrito] = "Aguardando deferimento",
            [StatusCandidatoCipa.Indeferido] = "Inscrição indeferida",
        };

        var candidatos = detalhe.Candidatos
            .Select(c => new AtaEleicaoCipaCandidatoModelo(c.TrabalhadorNome, c.TrabalhadorMatricula, c.VotosRecebidos, statusLabel[c.Status]))
            .ToList();

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(ProcessoEleitoralCipa), request.ProcessoEleitoralId, ct);

        var modelo = new AtaEleicaoCipaPdfModelo(
            detalhe.Processo.ObraNome,
            logoConteudo,
            detalhe.Processo.NumeroDocumento,
            detalhe.Processo.DataConvocacao,
            detalhe.Processo.DataVotacao,
            detalhe.Processo.DataApuracao,
            candidatos,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng);

        return _pdf.GerarAtaEleicao(modelo);
    }
}

public record ExportarAtaReuniaoCipaPdfQuery(Guid ReuniaoId) : IRequest<byte[]?>;

public class ExportarAtaReuniaoCipaPdfQueryHandler : IRequestHandler<ExportarAtaReuniaoCipaPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly ICipaPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAtaReuniaoCipaPdfQueryHandler(IMediator mediator, IAppDbContext db, ICipaPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarAtaReuniaoCipaPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterReuniaoCipaDetalheQuery(request.ReuniaoId), ct);
        if (detalhe is null) return null;

        var logoConteudo = await _db.Obras
            .Where(o => o.Id == detalhe.Reuniao.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);

        var participantes = detalhe.Participantes
            .Select(p => new AtaReuniaoCipaParticipanteModelo(p.TrabalhadorNome, p.Presente))
            .ToList();

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(ReuniaoCipa), request.ReuniaoId, ct);

        var modelo = new AtaReuniaoCipaPdfModelo(
            detalhe.Reuniao.ObraNome,
            logoConteudo,
            detalhe.Reuniao.Tipo == TipoReuniaoCipa.Ordinaria ? "Reunião Ordinária" : "Reunião Extraordinária",
            detalhe.Reuniao.DataReuniao,
            detalhe.Reuniao.Pauta,
            detalhe.Reuniao.Deliberacoes,
            participantes,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng);

        return _pdf.GerarAtaReuniao(modelo);
    }
}
```

- [ ] **Step 3: Swap both footers**

In `src/AAHBRANT.SST.Infrastructure/Documentos/CipaPdfService.cs`, there are two `pagina.Footer().AlignCenter().Text(...)` blocks (one per `Gerar...` method, both currently identical "Gerado em..."). Replace the first (inside `GerarAtaEleicao`) with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ata de Eleição CIPA", modelo.NumeroDocumento, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
```

and the second (inside `GerarAtaReuniao`) with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ata de Reunião CIPA", protocolo: null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
```

(`AtaReuniaoCipaPdfModelo` has no `NumeroDocumento` field — pass `protocolo: null` explicitly so the footer just omits that part of the line, per spec.)

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Cipa/ICipaPdfService.cs src/AAHBRANT.SST.Application/Cipa/Queries/ExportarAtaCipaPdfQueries.cs src/AAHBRANT.SST.Infrastructure/Documentos/CipaPdfService.cs
git commit -m "feat: rodapé de rastreabilidade nas atas de Eleição e Reunião da CIPA"
```

---

## Task 11: Inspeção/Patrulha — numeração + rodapé

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Inspecoes/Inspecao.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/InspecoesConfiguracoes.cs`
- Modify: `src/AAHBRANT.SST.Application/Inspecoes/Commands/CriarInspecaoCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Inspecoes/IInspecaoPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Inspecoes/Queries/ExportarInspecaoPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/InspecaoPdfService.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Inspecoes/CriarInspecaoCommandHandlerTests.cs` (new file — none existed before)

**Interfaces:**
- Consumes: `IGeradorNumeroDocumentoService`, `IRegistradorRastreabilidadeService`. Inspeção already uses `DocumentoAssinatura(nameof(Inspecao))` (single-signer rule in `RegistradorAssinaturaService`) — `rastreio.TemAssinatura` is accurate, no override needed.

- [ ] **Step 1: Add the entity field**

In `src/AAHBRANT.SST.Domain/Entidades/Inspecoes/Inspecao.cs`, in the `Inspecao` class, add after `public ChecklistModelo? ChecklistModelo { get; set; }`:

```csharp
    public ChecklistModelo? ChecklistModelo { get; set; }

    // Protocolo automático (prefixo "INSP"), gerado uma única vez na criação (CriarInspecaoCommand).
    public string? NumeroDocumento { get; set; }
```

- [ ] **Step 2: Configure the column**

In `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/InspecoesConfiguracoes.cs`, in `InspecaoConfiguracao.Configure`, add as the first line:

```csharp
        builder.Property(i => i.NumeroDocumento).HasMaxLength(50);
```

- [ ] **Step 3: Generate the migration**

Run: `dotnet ef migrations add AdicionarNumeroDocumentoInspecao --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`
Expected: adds one nullable `nvarchar(50)` column to the `Inspecoes` table only.

- [ ] **Step 4: Wire numbering into creation**

In `src/AAHBRANT.SST.Application/Inspecoes/Commands/CriarInspecaoCommand.cs`, replace:

```csharp
public class CriarInspecaoCommandHandler : IRequestHandler<CriarInspecaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarInspecaoCommandHandler(IAppDbContext db) => _db = db;
```

with:

```csharp
public class CriarInspecaoCommandHandler : IRequestHandler<CriarInspecaoCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public CriarInspecaoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }
```

and:

```csharp
        var inspecao = new Inspecao
        {
            TipoInspecao = checklist.TipoInspecao,
            ObraId = request.ObraId,
            AtividadeId = request.AtividadeId,
            ChecklistModeloId = checklist.Id,
            Data = request.Data,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
        };
```

with:

```csharp
        var inspecao = new Inspecao
        {
            TipoInspecao = checklist.TipoInspecao,
            ObraId = request.ObraId,
            AtividadeId = request.AtividadeId,
            ChecklistModeloId = checklist.Id,
            Data = request.Data,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            NumeroDocumento = await _geradorNumero.GerarAsync("INSP", ct),
        };
```

- [ ] **Step 5: Write the new test file**

Create `tests/AAHBRANT.SST.Application.Tests/Inspecoes/CriarInspecaoCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

public class CriarInspecaoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_CriaInspecao_GeraNumeroDocumentoComPrefixoINSP()
    {
        var db = CriarDb(nameof(Handle_CriaInspecao_GeraNumeroDocumentoComPrefixoINSP));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "responsavel@aahbrant.com", Nome = "Responsável Teste" };
        var checklist = new ChecklistModelo { Nome = "Checklist Andaimes", TipoInspecao = Domain.Enums.TipoInspecao.Andaimes };
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);
        db.ChecklistModelos.Add(checklist);
        await db.SaveChangesAsync();

        var handler = new CriarInspecaoCommandHandler(db, new GeradorNumeroDocumentoService(db));
        var id = await handler.Handle(new CriarInspecaoCommand(checklist.Id, obra.Id, null, DateTime.UtcNow, usuario.Id), default);

        var inspecao = await db.Inspecoes.FirstAsync(i => i.Id == id);
        Assert.StartsWith("INSP-", inspecao.NumeroDocumento);
    }
}
```

- [ ] **Step 6: Run the test**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter CriarInspecaoCommandHandlerTests`
Expected: PASS (1/1). If `ChecklistModelo`'s constructor requirements differ from what's assumed here (e.g. more required properties), read `src/AAHBRANT.SST.Domain/Entidades/Inspecoes/ChecklistModelo.cs` and adjust the seed.

- [ ] **Step 7: Add fields to the PDF model**

In `src/AAHBRANT.SST.Application/Inspecoes/IInspecaoPdfService.cs`, append to `InspecaoPdfModelo` (after `IReadOnlyList<InspecaoPdfItemModelo> Itens`):

```csharp
    IReadOnlyList<InspecaoPdfItemModelo> Itens,
    string? Protocolo,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);
```

- [ ] **Step 8: Wire rastreabilidade into the export query**

In `src/AAHBRANT.SST.Application/Inspecoes/Queries/ExportarInspecaoPdfQuery.cs`, add `using AAHBRANT.SST.Application.Assinatura;` and `using AAHBRANT.SST.Domain.Entidades;`, add `IRegistradorRastreabilidadeService _rastreabilidade` to the constructor, add:

```csharp
        var inspecao = await _db.Inspecoes.FirstAsync(i => i.Id == request.Id, ct);
        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Inspecao), request.Id, ct);
```

right after the `fotosPorResposta` block, and append `inspecao.NumeroDocumento, rastreio.ConteudoHash, rastreio.UrlValidacaoPublica, rastreio.QrCodePng, rastreio.TemAssinatura` to the `new InspecaoPdfModelo(...)` call.

- [ ] **Step 9: Swap the footer**

`InspecaoPdfService.cs` has its own custom header (doesn't use `CabecalhoDocumentoPadrao`) but the footer replacement is independent of that. Replace:

```csharp
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(8);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                });
```

with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Inspeção", modelo.Protocolo, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
```

- [ ] **Step 10: Build**

Run: `dotnet build`
Expected: no errors.

- [ ] **Step 11: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Inspecoes/Inspecao.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/InspecoesConfiguracoes.cs src/AAHBRANT.SST.Application/Inspecoes/Commands/CriarInspecaoCommand.cs src/AAHBRANT.SST.Application/Inspecoes/IInspecaoPdfService.cs src/AAHBRANT.SST.Application/Inspecoes/Queries/ExportarInspecaoPdfQuery.cs src/AAHBRANT.SST.Infrastructure/Documentos/InspecaoPdfService.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/ tests/AAHBRANT.SST.Application.Tests/Inspecoes/
git commit -m "feat: numeração automática + rodapé de rastreabilidade no PDF de Inspeção"
```

---

## Task 12: Ficha de EPI — chave sintética por trabalhador, sem protocolo

**Files:**
- Modify: `src/AAHBRANT.SST.Application/EntregasEpi/IFichaEpiPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/EntregasEpi/Queries/ExportarFichaEpiTrabalhadorQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/EntregaEpiPdfService.cs`
- Modify: `tests/AAHBRANT.SST.Application.Tests/EntregasEpi/ExportarFichaEpiTrabalhadorQueryHandlerTests.cs` (existing 2 tests need the new constructor arg)

**Interfaces:**
- Consumes: `IRegistradorRastreabilidadeService.GarantirAsync("FichaEpiTrabalhador", TrabalhadorId, ct)` — synthetic tracking key (per spec §"Ficha de EPI agregada"): the Ficha is a rollup of many `EntregaEpi` rows, each individually tracked already; this key is only for "this printed PDF wasn't tampered with", not a signature. `TemAssinatura` is always `false` for this key (nothing signs `FichaEpiTrabalhador` itself — each row already shows its own signature status in the table).

- [ ] **Step 1: Add fields to the model**

In `src/AAHBRANT.SST.Application/EntregasEpi/IFichaEpiPdfService.cs`, replace:

```csharp
public record FichaEpiPdfModelo(
    string ObraNome,
    string? ObraCliente,
    string? ObraCnpj,
    byte[]? ObraLogoConteudo,
    string? ObraLogoContentType,
    string TrabalhadorNome,
    string TrabalhadorCpfMascarado,
    string TrabalhadorMatricula,
    string TrabalhadorFuncaoNome,
    string? TrabalhadorTurno,
    DateTime TrabalhadorDataAdmissao,
    List<LinhaEntregaEpiPdf> Entregas,
    List<LinhaDevolucaoEpiPdf> Devolucoes);
```

with:

```csharp
public record FichaEpiPdfModelo(
    string ObraNome,
    string? ObraCliente,
    string? ObraCnpj,
    byte[]? ObraLogoConteudo,
    string? ObraLogoContentType,
    string TrabalhadorNome,
    string TrabalhadorCpfMascarado,
    string TrabalhadorMatricula,
    string TrabalhadorFuncaoNome,
    string? TrabalhadorTurno,
    DateTime TrabalhadorDataAdmissao,
    List<LinhaEntregaEpiPdf> Entregas,
    List<LinhaDevolucaoEpiPdf> Devolucoes,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng);
```

- [ ] **Step 2: Wire rastreabilidade into the export query**

In `src/AAHBRANT.SST.Application/EntregasEpi/Queries/ExportarFichaEpiTrabalhadorQuery.cs`, add `using AAHBRANT.SST.Application.Assinatura;`. Update the constructor:

```csharp
public class ExportarFichaEpiTrabalhadorQueryHandler : IRequestHandler<ExportarFichaEpiTrabalhadorQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IFichaEpiPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarFichaEpiTrabalhadorQueryHandler(IAppDbContext db, IFichaEpiPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }
```

Right before the `var modelo = new FichaEpiPdfModelo(...)` call, add:

```csharp
        var rastreio = await _rastreabilidade.GarantirAsync("FichaEpiTrabalhador", request.TrabalhadorId, ct);
```

and append `rastreio.ConteudoHash, rastreio.UrlValidacaoPublica, rastreio.QrCodePng` to the `new FichaEpiPdfModelo(...)` call.

- [ ] **Step 3: Update existing tests**

In `tests/AAHBRANT.SST.Application.Tests/EntregasEpi/ExportarFichaEpiTrabalhadorQueryHandlerTests.cs`, add `using AAHBRANT.SST.Application.Assinatura;` and `using AAHBRANT.SST.Infrastructure.Assinatura;` at the top. Add this fake near `FichaEpiPdfServiceFake`:

```csharp
    private class QrCodeDocumentoServiceFalso : IQrCodeDocumentoService
    {
        public QrCodeDocumentoResultado Gerar(string token) => new(new byte[] { 9 }, $"https://fake/#/validar/{token}");
    }
```

Both occurrences of:

```csharp
        var handler = new ExportarFichaEpiTrabalhadorQueryHandler(db, pdf);
```

become:

```csharp
        var handler = new ExportarFichaEpiTrabalhadorQueryHandler(db, pdf, new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso()));
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ExportarFichaEpiTrabalhadorQueryHandlerTests`
Expected: PASS (2/2).

- [ ] **Step 5: Swap the footer**

In `src/AAHBRANT.SST.Infrastructure/Documentos/EntregaEpiPdfService.cs`, replace:

```csharp
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(8);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                });
```

with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ficha de EPI", protocolo: null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
```

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: no errors.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.Application/EntregasEpi/IFichaEpiPdfService.cs src/AAHBRANT.SST.Application/EntregasEpi/Queries/ExportarFichaEpiTrabalhadorQuery.cs src/AAHBRANT.SST.Infrastructure/Documentos/EntregaEpiPdfService.cs tests/AAHBRANT.SST.Application.Tests/EntregasEpi/ExportarFichaEpiTrabalhadorQueryHandlerTests.cs
git commit -m "feat: rodapé de rastreabilidade na Ficha de EPI (chave sintética por trabalhador)"
```

---

## Task 13: Certificado de Treinamento — substituir lógica ad-hoc por `GarantirAsync`

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Treinamentos/ICertificadoTreinamentoPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Treinamentos/Queries/ExportarCertificadoTreinamentoQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/CertificadoTreinamentoPdfService.cs`

**Interfaces:**
- Consumes: `IRegistradorRastreabilidadeService.GarantirAsync(nameof(Treinamento), TreinamentoId, ct)`. Today the certificate's QR is dead code (`TokenValidacaoPublica` is only ever set by `FinalizarDocumentoCommand`, which nothing calls for `Treinamento`) — this task makes it actually work, generating token/hash/QR on first export regardless of whether both signers have completed yet.

- [ ] **Step 1: Add/replace fields on the model**

In `src/AAHBRANT.SST.Application/Treinamentos/ICertificadoTreinamentoPdfService.cs`, `CertificadoTreinamentoPdfModelo` already has `QrCodeValidacaoPng` (byte[]?) — keep it (still used by the body of the PDF, per its existing comment) but add the new footer fields after `byte[]? FotoTurma`:

```csharp
    IReadOnlyList<CertificadoTreinamentoPdfSignatarioModelo> Signatarios,
    byte[]? QrCodeValidacaoPng,
    byte[]? FotoTurma,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);
```

- [ ] **Step 2: Replace the ad-hoc QR logic with `GarantirAsync`**

In `src/AAHBRANT.SST.Application/Treinamentos/Queries/ExportarCertificadoTreinamentoQuery.cs`, replace the constructor:

```csharp
public class ExportarCertificadoTreinamentoQueryHandler : IRequestHandler<ExportarCertificadoTreinamentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly ICertificadoTreinamentoPdfService _pdf;
    private readonly IQrCodeDocumentoService _qrCode;

    public ExportarCertificadoTreinamentoQueryHandler(IAppDbContext db, ICertificadoTreinamentoPdfService pdf, IQrCodeDocumentoService qrCode)
    {
        _db = db;
        _pdf = pdf;
        _qrCode = qrCode;
    }
```

with:

```csharp
public class ExportarCertificadoTreinamentoQueryHandler : IRequestHandler<ExportarCertificadoTreinamentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly ICertificadoTreinamentoPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarCertificadoTreinamentoQueryHandler(IAppDbContext db, ICertificadoTreinamentoPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }
```

Then replace the whole "Um DocumentoAssinatura por treinamento..." block:

```csharp
        // Um DocumentoAssinatura por treinamento (EntidadeTipo="Treinamento", EntidadeId=Treinamento.Id) —
        // ver docs/Motor-Assinatura-Eletronica.md, mesmo padrão de ExportarFichaEpiTrabalhadorQuery.
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
                .ThenInclude(s => s.Trabalhador)
            .Where(d => d.EntidadeTipo == "Treinamento" && d.EntidadeId == request.TreinamentoId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var signatarios = documento?.Signatarios
            .Select(s => new CertificadoTreinamentoPdfSignatarioModelo(s.Trabalhador?.Nome ?? string.Empty, s.AssinadoEm))
            .ToList() ?? new List<CertificadoTreinamentoPdfSignatarioModelo>();

        // QR de verificação (item 6 da proposta do usuário, 04/09) — mesmo token público já usado no
        // comprovante genérico de assinatura (DocumentoAssinaturaPdfService); só é gerado se o
        // documento já tiver sido finalizado (token só existe a partir daí).
        var qrCodePng = !string.IsNullOrEmpty(documento?.TokenValidacaoPublica)
            ? _qrCode.Gerar(documento.TokenValidacaoPublica).Png
            : null;
```

with:

```csharp
        // Um DocumentoAssinatura por treinamento (EntidadeTipo="Treinamento", EntidadeId=Treinamento.Id) —
        // ver docs/Motor-Assinatura-Eletronica.md. Signatários vêm direto da tabela (não do resultado
        // de GarantirAsync) para exibir nome+método+data no corpo do certificado.
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
                .ThenInclude(s => s.Trabalhador)
            .Where(d => d.EntidadeTipo == "Treinamento" && d.EntidadeId == request.TreinamentoId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var signatarios = documento?.Signatarios
            .Select(s => new CertificadoTreinamentoPdfSignatarioModelo(s.Trabalhador?.Nome ?? string.Empty, s.AssinadoEm))
            .ToList() ?? new List<CertificadoTreinamentoPdfSignatarioModelo>();

        // Rastreabilidade sempre disponível a partir do primeiro export (Task 2) — antes disto, o QR
        // só existia depois de uma finalização que, para Treinamento, nada nunca dispara.
        var rastreio = await _rastreabilidade.GarantirAsync("Treinamento", request.TreinamentoId, ct);
        var qrCodePng = rastreio.QrCodePng;
```

Then append `rastreio.ConteudoHash, rastreio.UrlValidacaoPublica, rastreio.QrCodePng, rastreio.TemAssinatura` to the final `new CertificadoTreinamentoPdfModelo(...)` call (after the existing `fotoTurma`).

- [ ] **Step 3: Swap the footer**

In `src/AAHBRANT.SST.Infrastructure/Documentos/CertificadoTreinamentoPdfService.cs`, replace the `Rodape` method body:

```csharp
    private static void Rodape(PageDescriptor pagina)
    {
        pagina.Footer().AlignCenter().Text(t =>
        {
            t.Span("Gerado em ").FontSize(7).FontColor(Colors.Grey.Darken1);
            t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }
```

with:

```csharp
    private static void Rodape(PageDescriptor pagina, CertificadoTreinamentoPdfModelo modelo)
    {
        pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
            coluna, "Certificado", modelo.NumeroCertificado, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
    }
```

Find the call site (`Rodape(pagina)`, inside the `Document.Create` lambda where `modelo` is in scope) and update it to `Rodape(pagina, modelo);`.

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: no errors. If `IQrCodeDocumentoService` is no longer used anywhere else in this file, confirm no unused-using warning breaks the build (it won't fail the build, just check).

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Treinamentos/ICertificadoTreinamentoPdfService.cs src/AAHBRANT.SST.Application/Treinamentos/Queries/ExportarCertificadoTreinamentoQuery.cs src/AAHBRANT.SST.Infrastructure/Documentos/CertificadoTreinamentoPdfService.cs
git commit -m "fix: certificado de treinamento gera QR de validação a partir do primeiro export (antes nunca disparava)"
```

---

## Task 14: Ata de Sessão de Treinamento — chave sintética pela Turma

**Files:**
- Modify: `src/AAHBRANT.SST.Application/SessoesTreinamento/IAtaSessaoTreinamentoPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/SessoesTreinamento/Queries/ExportarAtaSessaoTreinamentoQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/AtaSessaoTreinamentoPdfService.cs`

**Interfaces:**
- Consumes: `IRegistradorRastreabilidadeService.GarantirAsync("SessaoTreinamento", SessaoTreinamentoId, ct)` — synthetic key, same reasoning as Ficha de EPI: the Ata aggregates N participants, each already individually signed via their own `Treinamento`-keyed `DocumentoAssinatura` (Task 13); nothing signs the aggregate Ata itself, so `TemAssinatura` is always `false` here too.

- [ ] **Step 1: Add fields to the model**

In `src/AAHBRANT.SST.Application/SessoesTreinamento/IAtaSessaoTreinamentoPdfService.cs`, append to `AtaSessaoTreinamentoPdfModelo` (after `IReadOnlyList<byte[]> Fotos`):

```csharp
    IReadOnlyList<byte[]> Fotos,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng);
```

- [ ] **Step 2: Wire rastreabilidade into the export query**

Replace the full content of `src/AAHBRANT.SST.Application/SessoesTreinamento/Queries/ExportarAtaSessaoTreinamentoQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

public record ExportarAtaSessaoTreinamentoQuery(Guid SessaoTreinamentoId) : IRequest<byte[]?>;

public class ExportarAtaSessaoTreinamentoQueryHandler : IRequestHandler<ExportarAtaSessaoTreinamentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IAtaSessaoTreinamentoPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAtaSessaoTreinamentoQueryHandler(IAppDbContext db, IAtaSessaoTreinamentoPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarAtaSessaoTreinamentoQuery request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.Obra)
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.SessaoTreinamentoId, ct);
        if (sessao is null || sessao.Obra is null || sessao.CursoTreinamento is null) return null;

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .Include(p => p.Trabalhador)
            .OrderBy(p => p.Trabalhador!.Nome)
            .Select(p => new AtaSessaoTreinamentoPdfParticipanteModelo(
                p.Trabalhador!.Nome, p.Trabalhador.Matricula, p.PresencaConfirmadaEm))
            .ToListAsync(ct);

        var fotos = await _db.FotosEvidenciaSessaoTreinamento
            .Where(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .Select(f => f.FotoConteudo)
            .ToListAsync(ct);

        // Chave sintética "SessaoTreinamento"/SessaoTreinamentoId: a Ata agrega N participantes, cada
        // um já individualmente assinado via seu próprio DocumentoAssinatura("Treinamento", Id) —
        // ninguém assina a Ata em si, então TemAssinatura nunca é usado aqui (RodapeDocumentoPadrao
        // recebe temAssinatura: false diretamente no PdfService).
        var rastreio = await _rastreabilidade.GarantirAsync("SessaoTreinamento", sessao.Id, ct);

        var modelo = new AtaSessaoTreinamentoPdfModelo(
            sessao.Obra.Nome,
            sessao.Obra.LogoConteudo,
            sessao.CursoTreinamento.Nome,
            sessao.CursoTreinamento.NormaReferencia,
            sessao.DataRealizacao,
            sessao.CargaHorariaRealizada,
            sessao.InstituicaoInstrutor,
            sessao.NumeroCertificado,
            sessao.DataEncerramento,
            participantes,
            fotos,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng);

        return _pdf.Gerar(modelo);
    }
}
```

- [ ] **Step 3: Swap the footer**

In `src/AAHBRANT.SST.Infrastructure/Documentos/AtaSessaoTreinamentoPdfService.cs`, replace:

```csharp
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
```

with:

```csharp
                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ata de Sessão de Treinamento", modelo.NumeroCertificado, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/SessoesTreinamento/IAtaSessaoTreinamentoPdfService.cs src/AAHBRANT.SST.Application/SessoesTreinamento/Queries/ExportarAtaSessaoTreinamentoQuery.cs src/AAHBRANT.SST.Infrastructure/Documentos/AtaSessaoTreinamentoPdfService.cs
git commit -m "feat: rodapé de rastreabilidade na Ata de Sessão de Treinamento (chave sintética pela turma)"
```

---

## Task 15: Verificação final — build completo, testes, exportação visual

**Files:** none (verification only)

- [ ] **Step 1: Full solution build**

Run: `dotnet build`
Expected: 0 errors across all projects.

- [ ] **Step 2: Full test suite**

Run: `dotnet test`
Expected: all tests pass, including every test added/updated in Tasks 2, 3, 6, 11, 12.

- [ ] **Step 3: Frontend type-check**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 4: Visual verification — export one PDF of each of the 4 newly-numbered/wired types**

Using the dev server (`preview_start`, never raw `dotnet run` per project convention) and the pypdf text-extraction technique already used earlier in this same session (no Poppler available), export and read the text of:
- One DDS diário PDF — confirm `DDS-D-2026-NNNN` appears in the footer, plus "Página 1 de N" and a validation URL line.
- One Inspeção PDF — confirm `INSP-2026-NNNN` and the footer layout.
- One Ficha de EPI PDF — confirm the footer shows hash/QR/date but no "nº" line.
- One Certificado de Treinamento PDF — confirm the QR that was previously always blank now renders, and the MP 2.200-2 note appears if the certificate already has signers, or is absent if not.

Expected: all four show a consistent 3-4 line rodapé with the elements from spec §8, no layout overlap with existing content, no C# exceptions during export.

- [ ] **Step 5: Report to the user**

Summarize what was verified (or not — say explicitly if visual verification was skipped and why, per this project's stated verification standards) before considering the feature done. Do not deploy — deploy only if the user explicitly asks in that turn.
