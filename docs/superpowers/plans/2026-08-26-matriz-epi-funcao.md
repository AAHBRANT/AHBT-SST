# Matriz de EPI por Função — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Matriz de EPI por Função (Phase 1 of the EPI module reformulation): a new `MatrizEpiFuncao` association entity, endpoints to read/define which EPIs belong to a função, a checklist UI inside `FuncoesTab.tsx`, and a filter on the EPI select in `EntregasTab.tsx` that only shows EPIs linked to the worker's função.

**Architecture:** Standard CQRS + MediatR slice added to the existing `Funcoes` feature folder (Application layer), backed by a new EF Core entity/table/migration (Infrastructure layer), exposed via two new actions on the existing `FuncoesController` (Api layer), and consumed by two React/Fluent UI screens (TeamsApp). `MockObraSeeder` gets a new demo matrix so the filter has data to show out of the box.

**Tech Stack:** .NET 8, EF Core 8.0.11, MediatR, FluentValidation, xUnit + EF Core InMemory, React + TypeScript, Fluent UI v9.

**Spec:** [docs/superpowers/specs/2026-08-26-matriz-epi-funcao-design.md](../specs/2026-08-26-matriz-epi-funcao-design.md)

## Global Constraints

- This plan covers **Phase 1 only** (Matriz de EPI por Função). Ficha de EPI reformulada (Phase 2) and Estoque por obra (Phase 3) are explicitly out of scope — do not touch `EntregaEpi.Motivo` (stays free text), do not add quantity/periodicity fields to the matrix, do not segment `CatalogoEpi.SaldoEstoque` by obra.
- Matriz granularity is **obrigatoriedade only** — a row's existence means "this EPI is required for this função." No extra columns on `MatrizEpiFuncao` beyond the two FKs.
- The EPI select in `EntregasTab.tsx` must **filter** the list to only the função's linked EPIs — never show all EPIs and block on submit.
- The matriz management UI lives **inside `FuncoesTab.tsx`** (Pessoas → Funções tab). Do not create a new top-level EPI-module tab for it.
- GET policy: `organizacional:ver`. PUT policy: `organizacional:editar`. (Resolved: no combined-policy mechanism needed — both actions live on `FuncoesController`, which already uses `organizacional:*` exclusively.)
- Migration name: `AdicionarMatrizEpiFuncao`.
- Any new NuGet package version must match the version already pinned for that package family elsewhere in the solution — EF Core packages are pinned at **8.0.11** (see `AAHBRANT.SST.Infrastructure.csproj`); use that exact version for `Microsoft.EntityFrameworkCore.InMemory`.
- Integration tests (`WebApplicationFactory`-based) are **out of scope for this plan** — the repo has zero existing precedent (`Program.cs` has no `public partial class Program` marker, no `WebApplicationFactory` usage anywhere in the solution). Endpoint-level correctness for the GET/PUT actions is covered instead by the Application-layer handler unit tests (Task 2) plus the manual browser verification (Task 9), matching spec §7's explicit test scope.

---

### Task 1: Domain entity, EF configuration, and migration

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Epi.cs` (append `MatrizEpiFuncao` class)
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs:27-28` (insert `DbSet<MatrizEpiFuncao>`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs:32-34` (insert `DbSet<MatrizEpiFuncao>`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/ConformidadeConfiguracoes.cs` (append `MatrizEpiFuncaoConfiguracao` class)
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/<timestamp>_AdicionarMatrizEpiFuncao.cs` (+ `.Designer.cs`, scaffolded by the EF CLI, plus an update to `SstDbContextModelSnapshot.cs`)

**Interfaces:**
- Produces: `AAHBRANT.SST.Domain.Entidades.MatrizEpiFuncao { Guid Id; Guid FuncaoId; Funcao? Funcao; Guid CatalogoEpiId; CatalogoEpi? CatalogoEpi; }` (plus `AuditableEntity` base members: `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy`, `Origem`, `Ativo`, `RowVersion`). `IAppDbContext.MatrizEpiFuncoes : DbSet<MatrizEpiFuncao>` — this is what Task 2 and Task 3 depend on.

- [ ] **Step 1: Add the `MatrizEpiFuncao` entity**

Append to the end of `src/AAHBRANT.SST.Domain/Entidades/Epi.cs` (after the closing brace of `EntregaEpi`):

```csharp

public class MatrizEpiFuncao : AuditableEntity
{
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }
    public Guid CatalogoEpiId { get; set; }
    public CatalogoEpi? CatalogoEpi { get; set; }
}
```

- [ ] **Step 2: Register the DbSet on `IAppDbContext`**

In `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`, insert a new line after line 27 (`DbSet<EntregaEpi> EntregasEpi { get; }`) and before line 28 (`DbSet<Alerta> Alertas { get; }`):

```csharp
    DbSet<EntregaEpi> EntregasEpi { get; }
    DbSet<MatrizEpiFuncao> MatrizEpiFuncoes { get; }
    DbSet<Alerta> Alertas { get; }
```

- [ ] **Step 3: Register the DbSet on `SstDbContext`**

In `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`, insert a new line after line 32 (`public DbSet<EntregaEpi> EntregasEpi => Set<EntregaEpi>();`):

```csharp
    public DbSet<CatalogoEpi> CatalogoEpis => Set<CatalogoEpi>();
    public DbSet<EntregaEpi> EntregasEpi => Set<EntregaEpi>();
    public DbSet<MatrizEpiFuncao> MatrizEpiFuncoes => Set<MatrizEpiFuncao>();
```

- [ ] **Step 4: Add the EF configuration**

Append to the end of `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/ConformidadeConfiguracoes.cs` (after the closing brace of `TrilhaAuditoriaConfiguracao`):

```csharp

public class MatrizEpiFuncaoConfiguracao : IEntityTypeConfiguration<MatrizEpiFuncao>
{
    public void Configure(EntityTypeBuilder<MatrizEpiFuncao> builder)
    {
        builder.HasOne(m => m.Funcao).WithMany()
            .HasForeignKey(m => m.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.CatalogoEpi).WithMany()
            .HasForeignKey(m => m.CatalogoEpiId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.FuncaoId, m.CatalogoEpiId }).IsUnique();
        builder.HasQueryFilter(m => m.Ativo);

        // Entidade nova, sem coluna varbinary legada — diferente do resto do arquivo (que corrige
        // um bug retroativo), IsRowVersion() aqui já deve gerar coluna "rowversion" na primeira
        // migration (ver verificação no Passo 6 abaixo).
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}
```

No new `using` is needed — the file already has `using AAHBRANT.SST.Domain.Entidades;`, `using Microsoft.EntityFrameworkCore;`, and `using Microsoft.EntityFrameworkCore.Metadata.Builders;`. `SstDbContext.OnModelCreating` calls `modelBuilder.ApplyConfigurationsFromAssembly(typeof(SstDbContext).Assembly)`, so this class is picked up automatically — no manual registration.

- [ ] **Step 5: Build to confirm the model compiles**

Run:
```bash
dotnet build src/AAHBRANT.SST.Infrastructure/AAHBRANT.SST.Infrastructure.csproj
```
Expected: Build succeeded, no errors.

- [ ] **Step 6: Scaffold the migration and verify the `RowVersion` column type**

Run (from the repo root):
```bash
dotnet ef migrations add AdicionarMatrizEpiFuncao --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api
```

Then open the generated `src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/<timestamp>_AdicionarMatrizEpiFuncao.cs` and check the `CreateTable` call. Expected column set, in this exact order (matches `AuditableEntity` + the two FKs, following the ordering convention seen in every prior `CreateTable` migration in this project):

```csharp
columns: table => new
{
    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
    CatalogoEpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
    Origem = table.Column<int>(type: "int", nullable: false),
    Ativo = table.Column<bool>(type: "bit", nullable: false),
    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
},
```

**Verify:** the `RowVersion` line must read `type: "rowversion", rowVersion: true` (not `type: "varbinary(max)"`).

**If it does NOT** (i.e. EF scaffolded `varbinary(max)` instead): this means a brand-new entity's `.IsRowVersion()` does not get picked up as expected by `CreateTable`. In that case:
1. Delete the just-generated migration: `dotnet ef migrations remove --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api`
2. Re-add it, then follow up with a second "Corrigir"-style migration matching the existing pattern in `20260826195749_CorrigirRowVersionCatalogoEpiCursoTreinamentoNaoConformidade.cs`:
   ```csharp
   migrationBuilder.DropColumn(name: "RowVersion", table: "MatrizEpiFuncoes");
   migrationBuilder.AddColumn<byte[]>(
       name: "RowVersion",
       table: "MatrizEpiFuncoes",
       type: "rowversion",
       rowVersion: true,
       nullable: true);
   ```
   Name it `CorrigirRowVersionMatrizEpiFuncao`.

Also verify the rest of the scaffolded migration matches this shape (constraints and indexes — EF should generate this automatically, shown here for reference only, not to be hand-typed):

```csharp
constraints: table =>
{
    table.PrimaryKey("PK_MatrizEpiFuncoes", x => x.Id);
    table.ForeignKey(
        name: "FK_MatrizEpiFuncoes_CatalogoEpis_CatalogoEpiId",
        column: x => x.CatalogoEpiId,
        principalTable: "CatalogoEpis",
        principalColumn: "Id",
        onDelete: ReferentialAction.Restrict);
    table.ForeignKey(
        name: "FK_MatrizEpiFuncoes_Funcoes_FuncaoId",
        column: x => x.FuncaoId,
        principalTable: "Funcoes",
        principalColumn: "Id",
        onDelete: ReferentialAction.Restrict);
});

migrationBuilder.CreateIndex(
    name: "IX_MatrizEpiFuncoes_CatalogoEpiId",
    table: "MatrizEpiFuncoes",
    column: "CatalogoEpiId");

migrationBuilder.CreateIndex(
    name: "IX_MatrizEpiFuncoes_FuncaoId_CatalogoEpiId",
    table: "MatrizEpiFuncoes",
    columns: new[] { "FuncaoId", "CatalogoEpiId" },
    unique: true);
```

- [ ] **Step 7: Build the whole solution to confirm the migration compiles**

Run:
```bash
dotnet build
```
Expected: Build succeeded, no errors.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Epi.cs src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/ConformidadeConfiguracoes.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat: adiciona entidade e migration da Matriz de EPI por Função"
```

---

### Task 2: `DefinirMatrizEpiFuncaoCommand` (Application layer, TDD)

**Files:**
- Modify: `tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj` (add EF Core InMemory package + Infrastructure project reference)
- Create: `src/AAHBRANT.SST.Application/Funcoes/Commands/DefinirMatrizEpiFuncaoCommand.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Funcoes/DefinirMatrizEpiFuncaoCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.MatrizEpiFuncoes`, `IAppDbContext.Funcoes` (Task 1). `AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext(DbContextOptions<SstDbContext>)` constructor (for the InMemory-backed test fixture).
- Produces: `record DefinirMatrizEpiFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoEpiIds) : IRequest;` and `class DefinirMatrizEpiFuncaoCommandHandler(IAppDbContext db) : IRequestHandler<DefinirMatrizEpiFuncaoCommand>` — this is what Task 4 (controller) depends on.

- [ ] **Step 1: Add the test project's missing dependencies**

Edit `tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AAHBRANT.SST.Application\AAHBRANT.SST.Application.csproj" />
    <ProjectReference Include="..\..\src\AAHBRANT.SST.Infrastructure\AAHBRANT.SST.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing tests**

Create `tests/AAHBRANT.SST.Application.Tests/Funcoes/DefinirMatrizEpiFuncaoCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class DefinirMatrizEpiFuncaoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options);
    }

    private static async Task<(Funcao Funcao, CatalogoEpi EpiA, CatalogoEpi EpiB, CatalogoEpi EpiC)> SemearAsync(IAppDbContext db)
    {
        var funcao = new Funcao { Nome = "Soldador" };
        var epiA = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        var epiB = new CatalogoEpi { Nome = "Luva", VidaUtilEmMeses = 6 };
        var epiC = new CatalogoEpi { Nome = "Óculos", VidaUtilEmMeses = 12 };

        db.Funcoes.Add(funcao);
        db.CatalogoEpis.AddRange(epiA, epiB, epiC);
        await db.SaveChangesAsync();

        return (funcao, epiA, epiB, epiC);
    }

    [Fact]
    public async Task Handle_FuncaoSemVinculos_AdicionaTodosOsEpisInformados()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemVinculos_AdicionaTodosOsEpisInformados));
        var (funcao, epiA, epiB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);

        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        var vinculos = await db.MatrizEpiFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.Contains(vinculos, v => v.CatalogoEpiId == epiA.Id);
        Assert.Contains(vinculos, v => v.CatalogoEpiId == epiB.Id);
    }

    [Fact]
    public async Task Handle_RemoveEpiDaLista_DesativaVinculoExistente()
    {
        var db = CriarDb(nameof(Handle_RemoveEpiDaLista_DesativaVinculoExistente));
        var (funcao, epiA, epiB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id }), default);

        var vinculos = await db.MatrizEpiFuncoes.IgnoreQueryFilters().Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.True(vinculos.Single(v => v.CatalogoEpiId == epiA.Id).Ativo);
        Assert.False(vinculos.Single(v => v.CatalogoEpiId == epiB.Id).Ativo);
    }

    [Fact]
    public async Task Handle_ReenviaEpiRemovidoAnteriormente_ReativaVinculoEmVezDeDuplicar()
    {
        var db = CriarDb(nameof(Handle_ReenviaEpiRemovidoAnteriormente_ReativaVinculoEmVezDeDuplicar));
        var (funcao, epiA, epiB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);
        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id }), default);

        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        var vinculos = await db.MatrizEpiFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.All(vinculos, v => Assert.True(v.Ativo));
    }

    [Fact]
    public async Task Handle_ReenviaMesmaLista_EhIdempotente()
    {
        var db = CriarDb(nameof(Handle_ReenviaMesmaLista_EhIdempotente));
        var (funcao, epiA, epiB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        var vinculos = await db.MatrizEpiFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
    }

    [Fact]
    public async Task Handle_FuncaoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_FuncaoInexistente_LancaKeyNotFoundException));
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new DefinirMatrizEpiFuncaoCommand(Guid.NewGuid(), new List<Guid>()), default));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:
```bash
dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter DefinirMatrizEpiFuncaoCommandHandlerTests
```
Expected: build error — `DefinirMatrizEpiFuncaoCommand` and `DefinirMatrizEpiFuncaoCommandHandler` do not exist yet.

- [ ] **Step 4: Implement the command**

Create `src/AAHBRANT.SST.Application/Funcoes/Commands/DefinirMatrizEpiFuncaoCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record DefinirMatrizEpiFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoEpiIds) : IRequest;

public class DefinirMatrizEpiFuncaoCommandValidator : AbstractValidator<DefinirMatrizEpiFuncaoCommand>
{
    public DefinirMatrizEpiFuncaoCommandValidator()
    {
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.CatalogoEpiIds).NotNull();
        RuleForEach(x => x.CatalogoEpiIds).NotEmpty();
    }
}

public class DefinirMatrizEpiFuncaoCommandHandler : IRequestHandler<DefinirMatrizEpiFuncaoCommand>
{
    private readonly IAppDbContext _db;

    public DefinirMatrizEpiFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirMatrizEpiFuncaoCommand request, CancellationToken ct)
    {
        var funcaoExiste = await _db.Funcoes.AnyAsync(f => f.Id == request.FuncaoId, ct);
        if (!funcaoExiste)
            throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");

        // IgnoreQueryFilters: precisa enxergar também vínculos previamente desativados (Ativo=false)
        // para reativá-los em vez de tentar inserir duplicata e violar o índice único
        // (FuncaoId, CatalogoEpiId).
        var vinculosAtuais = await _db.MatrizEpiFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == request.FuncaoId)
            .ToListAsync(ct);

        var idsDesejados = request.CatalogoEpiIds.Distinct().ToHashSet();

        foreach (var vinculo in vinculosAtuais)
            vinculo.Ativo = idsDesejados.Contains(vinculo.CatalogoEpiId);

        var idsExistentes = vinculosAtuais.Select(v => v.CatalogoEpiId).ToHashSet();
        foreach (var catalogoEpiId in idsDesejados.Where(id => !idsExistentes.Contains(id)))
        {
            _db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao
            {
                FuncaoId = request.FuncaoId,
                CatalogoEpiId = catalogoEpiId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter DefinirMatrizEpiFuncaoCommandHandlerTests
```
Expected: all 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj tests/AAHBRANT.SST.Application.Tests/Funcoes/ src/AAHBRANT.SST.Application/Funcoes/Commands/DefinirMatrizEpiFuncaoCommand.cs
git commit -m "feat: adiciona DefinirMatrizEpiFuncaoCommand com testes de sincronizacao"
```

---

### Task 3: `ListarEpisPorFuncaoQuery` (Application layer)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Funcoes/Queries/ListarEpisPorFuncaoQuery.cs`

**Interfaces:**
- Consumes: `IAppDbContext.MatrizEpiFuncoes` (Task 1), `AAHBRANT.SST.Application.CatalogosEpi.CatalogoEpiDto(Guid Id, string Nome, string? Fabricante, string? CertificadoAprovacaoNumero, DateTime? CertificadoAprovacaoValidade, int VidaUtilEmMeses, int SaldoEstoque)` (existing).
- Produces: `record ListarEpisPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CatalogoEpiDto>>;` and `class ListarEpisPorFuncaoQueryHandler(IAppDbContext db) : IRequestHandler<ListarEpisPorFuncaoQuery, List<CatalogoEpiDto>>` — this is what Task 4 (controller) depends on.

No dedicated unit test for this task — spec §7 scopes automated testing to the command handler only; this query's behavior (empty list for a função with no matrix, correct list after `PUT`) is covered by the manual verification in Task 9. This mirrors the existing convention in this codebase, where read-only listing queries (e.g. `ListarCatalogosEpiQuery`, `ListarFuncoesQuery`) have no dedicated unit tests either.

- [ ] **Step 1: Implement the query**

Create `src/AAHBRANT.SST.Application/Funcoes/Queries/ListarEpisPorFuncaoQuery.cs`:

```csharp
using AAHBRANT.SST.Application.CatalogosEpi;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ListarEpisPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CatalogoEpiDto>>;

public class ListarEpisPorFuncaoQueryHandler : IRequestHandler<ListarEpisPorFuncaoQuery, List<CatalogoEpiDto>>
{
    private readonly IAppDbContext _db;
    public ListarEpisPorFuncaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoEpiDto>> Handle(ListarEpisPorFuncaoQuery request, CancellationToken ct)
        => await _db.MatrizEpiFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .OrderBy(m => m.CatalogoEpi!.Nome)
            .Select(m => new CatalogoEpiDto(
                m.CatalogoEpi!.Id,
                m.CatalogoEpi!.Nome,
                m.CatalogoEpi!.Fabricante,
                m.CatalogoEpi!.CertificadoAprovacaoNumero,
                m.CatalogoEpi!.CertificadoAprovacaoValidade,
                m.CatalogoEpi!.VidaUtilEmMeses,
                m.CatalogoEpi!.SaldoEstoque))
            .ToListAsync(ct);
}
```

- [ ] **Step 2: Build to confirm it compiles**

Run:
```bash
dotnet build src/AAHBRANT.SST.Application/AAHBRANT.SST.Application.csproj
```
Expected: Build succeeded, no errors.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.Application/Funcoes/Queries/ListarEpisPorFuncaoQuery.cs
git commit -m "feat: adiciona ListarEpisPorFuncaoQuery"
```

---

### Task 4: `FuncoesController` endpoints

**Files:**
- Modify: `src/AAHBRANT.SST.Api/Controllers/FuncoesController.cs`

**Interfaces:**
- Consumes: `DefinirMatrizEpiFuncaoCommand` (Task 2), `ListarEpisPorFuncaoQuery` (Task 3).
- Produces: `GET /api/funcoes/{id}/epis` → `200 OK` with `List<CatalogoEpiDto>`. `PUT /api/funcoes/{id}/epis` (body: `DefinirEpisRequest { List<Guid> CatalogoEpiIds }`) → `204 No Content` or `404` (via `KeyNotFoundException`, already handled by the existing global exception middleware the way `ExcluirFuncaoCommand`'s `KeyNotFoundException` is). This is what Task 6 (`api.ts`) depends on for the exact request/response shape.

- [ ] **Step 1: Add the two actions and the request DTO**

In `src/AAHBRANT.SST.Api/Controllers/FuncoesController.cs`, insert two new actions after `Excluir` (before the closing `}` of the class) and add a small request record after the class:

```csharp
    [Authorize(Policy = "organizacional:ver")]
    [HttpGet("{id:guid}/epis")]
    public async Task<IActionResult> ListarEpis(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEpisPorFuncaoQuery(id), ct));

    [Authorize(Policy = "organizacional:editar")]
    [HttpPut("{id:guid}/epis")]
    public async Task<IActionResult> DefinirEpis(Guid id, DefinirEpisRequest request, CancellationToken ct)
    {
        await _mediator.Send(new DefinirMatrizEpiFuncaoCommand(id, request.CatalogoEpiIds), ct);
        return NoContent();
    }
}

public record DefinirEpisRequest(List<Guid> CatalogoEpiIds);
```

(Note the extra closing `}` above — it closes the `FuncoesController` class; `DefinirEpisRequest` is declared at namespace level right after it, same as every other command/query record in this codebase.) The file's existing `using AAHBRANT.SST.Application.Funcoes.Commands;` and `using AAHBRANT.SST.Application.Funcoes.Queries;` already cover both new types — no new `using` needed.

- [ ] **Step 2: Build to confirm it compiles**

Run:
```bash
dotnet build src/AAHBRANT.SST.Api/AAHBRANT.SST.Api.csproj
```
Expected: Build succeeded, no errors.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.Api/Controllers/FuncoesController.cs
git commit -m "feat: expoe GET/PUT /api/funcoes/{id}/epis"
```

---

### Task 5: Demo data in `MockObraSeeder`

**Files:**
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.DadosEstaticos.cs` (add `MatrizEpiPorFuncao` static table)
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.MatrizEpiFuncao.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.cs:49` (wire the new list into `ExecutarAsync`)

**Interfaces:**
- Consumes: `MatrizEpiFuncao` entity (Task 1), the `funcoes` and `catalogosEpi` locals already built inside `MockObraSeeder.ExecutarAsync` (existing).
- Produces: nothing consumed by later tasks — this is demo data only, verified manually in Task 9.

This is demo/mock data for the `OBRA-MOCK-AURORA` seeder (Development-only, per the class-level comment in `MockObraSeeder.cs`) — flagged here explicitly as such, per spec §5's Pendência 2 (resolved in favor of including it).

- [ ] **Step 1: Add the static função→EPIs mapping**

In `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.DadosEstaticos.cs`, insert this new static field after the `CatalogoEpisPadrao` field (after its closing `};`, before `DistribuicaoNaoConformidades`):

```csharp
    // Dados de demonstração da Matriz de EPI por Função (Fase 1) — cada obra real define sua
    // própria matriz depois do deploy; isto só preenche o seeder de obra mocada para a tela e o
    // filtro terem o que mostrar. Funções com NR-35 (trabalho em altura) em DistribuicaoFuncoes
    // recebem também o cinto de segurança.
    public static readonly (string Funcao, string[] Epis)[] MatrizEpiPorFuncao =
    {
        ("Servente", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta" }),
        ("Pedreiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão", "Cinto de Segurança Tipo Paraquedista" }),
        ("Armador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão" }),
        ("Carpinteiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Protetor Auricular Tipo Plug", "Cinto de Segurança Tipo Paraquedista" }),
        ("Eletricista", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão", "Cinto de Segurança Tipo Paraquedista" }),
        ("Encanador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta" }),
        ("Pintor", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Máscara Respiratória PFF2", "Óculos de Proteção Ampla Visão", "Cinto de Segurança Tipo Paraquedista" }),
        ("Soldador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão", "Protetor Auricular Tipo Plug" }),
        ("Operador de Grua/Betoneira", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Protetor Auricular Tipo Plug" }),
        ("Mestre de Obras", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Cinto de Segurança Tipo Paraquedista" }),
        (FuncaoEncarregado, new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Cinto de Segurança Tipo Paraquedista" }),
        ("Técnico de Segurança do Trabalho", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Engenheiro Civil", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Almoxarife", new[] { "Bota de Segurança com Bico de Aço", "Luva de Vaqueta" }),
        ("Vigia/Porteiro", new[] { "Bota de Segurança com Bico de Aço" }),
    };
```

- [ ] **Step 2: Add the helper that builds the `MatrizEpiFuncao` list**

Create `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.MatrizEpiFuncao.cs`:

```csharp
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    private static List<MatrizEpiFuncao> ConstruirMatrizEpiFuncao(List<Funcao> funcoes, List<CatalogoEpi> catalogosEpi)
    {
        var matriz = new List<MatrizEpiFuncao>();
        foreach (var (nomeFuncao, nomesEpis) in MatrizEpiPorFuncao)
        {
            var funcao = funcoes.Single(f => f.Nome == nomeFuncao);
            foreach (var nomeEpi in nomesEpis)
            {
                var epi = catalogosEpi.Single(c => c.Nome == nomeEpi);
                matriz.Add(new MatrizEpiFuncao { Funcao = funcao, CatalogoEpi = epi });
            }
        }
        return matriz;
    }
}
```

- [ ] **Step 3: Wire it into `ExecutarAsync`**

In `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.cs`, change:

```csharp
        db.CatalogoEpis.AddRange(catalogosEpi);
        db.EntregasEpi.AddRange(entregasEpi);
```

to:

```csharp
        db.CatalogoEpis.AddRange(catalogosEpi);
        db.EntregasEpi.AddRange(entregasEpi);
        db.MatrizEpiFuncoes.AddRange(ConstruirMatrizEpiFuncao(funcoes, catalogosEpi));
```

(`funcoes` and `catalogosEpi` are already in scope at this point in `ExecutarAsync` — `funcoes` comes from `ConstruirEstruturaOrganizacional`, `catalogosEpi` from `ConstruirNaoConformidadesEEpi`.) This uses navigation-property fix-up (`Funcao = funcao`, `CatalogoEpi = epi`) the same way `NovaEntrega` already does with `Trabalhador = trabalhador` — no two-phase save needed, since (unlike `Equipe`↔`Trabalhador`) this is a one-directional reference from a new entity to two other already-in-the-same-batch new entities.

- [ ] **Step 4: Build to confirm it compiles**

Run:
```bash
dotnet build src/AAHBRANT.SST.Infrastructure/AAHBRANT.SST.Infrastructure.csproj
```
Expected: Build succeeded, no errors.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.DadosEstaticos.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.MatrizEpiFuncao.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.cs
git commit -m "feat: seeder de obra mocada popula matriz de EPI por funcao"
```

---

### Task 6: `api.ts` client methods

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts:1837-1842`

**Interfaces:**
- Consumes: `GET/PUT /api/funcoes/{id}/epis` (Task 4).
- Produces: `api.funcoes.listarEpis(funcaoId: string): Promise<CatalogoEpi[]>` and `api.funcoes.definirEpis(funcaoId: string, catalogoEpiIds: string[]): Promise<void>` — this is what Task 7 and Task 8 depend on.

- [ ] **Step 1: Add the two methods**

In `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, change the `funcoes` object (lines 1837-1842) from:

```ts
  funcoes: {
    listar: () => request<Funcao[]>('/api/funcoes'),
    criar: (funcao: NovaFuncao) =>
      request<{ id: string }>('/api/funcoes', { method: 'POST', body: JSON.stringify(funcao) }),
    excluir: (id: string) => request<void>(`/api/funcoes/${id}`, { method: 'DELETE' }),
  },
```

to:

```ts
  funcoes: {
    listar: () => request<Funcao[]>('/api/funcoes'),
    criar: (funcao: NovaFuncao) =>
      request<{ id: string }>('/api/funcoes', { method: 'POST', body: JSON.stringify(funcao) }),
    excluir: (id: string) => request<void>(`/api/funcoes/${id}`, { method: 'DELETE' }),
    listarEpis: (funcaoId: string) => request<CatalogoEpi[]>(`/api/funcoes/${funcaoId}/epis`),
    definirEpis: (funcaoId: string, catalogoEpiIds: string[]) =>
      request<void>(`/api/funcoes/${funcaoId}/epis`, {
        method: 'PUT',
        body: JSON.stringify({ catalogoEpiIds }),
      }),
  },
```

`CatalogoEpi` is already declared in this same file (line 201) — no new import needed.

- [ ] **Step 2: Typecheck**

Run:
```bash
cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit
```
Expected: no new type errors.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat: adiciona listarEpis/definirEpis ao cliente de funcoes"
```

---

### Task 7: `FuncoesTab.tsx` — matriz checklist UI

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/FuncoesTab.tsx`

**Interfaces:**
- Consumes: `api.funcoes.listarEpis`, `api.funcoes.definirEpis` (Task 6), `api.catalogosEpi.listar(): Promise<CatalogoEpi[]>` (existing, already used by `EntregasTab.tsx`).

- [ ] **Step 1: Replace the full file content**

Replace the entire content of `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/FuncoesTab.tsx` with:

```tsx
import { Fragment, useEffect, useState } from 'react';
import {
  Button,
  Checkbox,
  Field,
  Input,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type CatalogoEpi, type Funcao, type NovaFuncao } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const funcaoVazia: NovaFuncao = { nome: '', cboCodigo: '', descricao: '' };

export function FuncoesTab() {
  const estilos = usePageStyles();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [novaFuncao, setNovaFuncao] = useState<NovaFuncao>(funcaoVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [episCatalogo, setEpisCatalogo] = useState<CatalogoEpi[]>([]);
  const [expandidoId, setExpandidoId] = useState<string | null>(null);
  const [vinculosSelecionados, setVinculosSelecionados] = useState<string[]>([]);
  const [salvandoMatriz, setSalvandoMatriz] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaFuncoes, listaEpis] = await Promise.all([api.funcoes.listar(), api.catalogosEpi.listar()]);
      setFuncoes(listaFuncoes);
      setEpisCatalogo(listaEpis);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.funcoes.criar(novaFuncao);
      setNovaFuncao(funcaoVazia);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar função.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.funcoes.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir função.');
    }
  }

  async function alternarExpansao(funcao: Funcao) {
    if (expandidoId === funcao.id) {
      setExpandidoId(null);
      return;
    }
    try {
      setErro(null);
      const vinculados = await api.funcoes.listarEpis(funcao.id);
      setVinculosSelecionados(vinculados.map((e) => e.id));
      setExpandidoId(funcao.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de EPI da função.');
    }
  }

  function alternarEpi(catalogoEpiId: string, marcado: boolean) {
    setVinculosSelecionados((atual) =>
      marcado ? [...atual, catalogoEpiId] : atual.filter((id) => id !== catalogoEpiId)
    );
  }

  async function salvarMatriz(funcaoId: string) {
    try {
      setSalvandoMatriz(true);
      setErro(null);
      await api.funcoes.definirEpis(funcaoId, vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de EPI.');
    } finally {
      setSalvandoMatriz(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Funções cadastradas</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={novaFuncao.nome} onChange={(_, d) => setNovaFuncao({ ...novaFuncao, nome: d.value })} />
        </Field>
        <Field label="Código CBO">
          <Input
            value={novaFuncao.cboCodigo ?? ''}
            onChange={(_, d) => setNovaFuncao({ ...novaFuncao, cboCodigo: d.value })}
          />
        </Field>
        <Field label="Descrição">
          <Input
            value={novaFuncao.descricao ?? ''}
            onChange={(_, d) => setNovaFuncao({ ...novaFuncao, descricao: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar função
        </Button>
      </div>

      <Text size={200}>Clique numa linha para editar a matriz de EPI daquela função.</Text>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>CBO</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {funcoes.map((funcao) => (
            <Fragment key={funcao.id}>
              <TableRow onClick={() => alternarExpansao(funcao)} style={{ cursor: 'pointer' }}>
                <TableCell>{funcao.nome}</TableCell>
                <TableCell>{funcao.cboCodigo}</TableCell>
                <TableCell>{funcao.descricao}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(e) => {
                      e.stopPropagation();
                      excluir(funcao.id);
                    }}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
              {expandidoId === funcao.id && (
                <TableRow>
                  <TableCell colSpan={4}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: '8px 0' }}>
                      <Text weight="semibold">Matriz de EPI — {funcao.nome}</Text>
                      {episCatalogo.length === 0 ? (
                        <Text>Nenhum EPI cadastrado no catálogo ainda.</Text>
                      ) : (
                        episCatalogo.map((epi) => (
                          <Checkbox
                            key={epi.id}
                            label={epi.fabricante ? `${epi.nome} (${epi.fabricante})` : epi.nome}
                            checked={vinculosSelecionados.includes(epi.id)}
                            onChange={(_, d) => alternarEpi(epi.id, !!d.checked)}
                          />
                        ))
                      )}
                      <div>
                        <Button appearance="primary" onClick={() => salvarMatriz(funcao.id)} disabled={salvandoMatriz}>
                          Salvar matriz
                        </Button>
                      </div>
                    </div>
                  </TableCell>
                </TableRow>
              )}
            </Fragment>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
```

- [ ] **Step 2: Typecheck**

Run:
```bash
cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit
```
Expected: no new type errors.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/FuncoesTab.tsx
git commit -m "feat: adiciona checklist de matriz de EPI em FuncoesTab"
```

---

### Task 8: `EntregasTab.tsx` — filtered EPI select

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/epi/EntregasTab.tsx`

**Interfaces:**
- Consumes: `api.funcoes.listarEpis` (Task 6), `Trabalhador.funcaoId` (existing, `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts:81`).

- [ ] **Step 1: Add the `episPermitidos` state**

In `src/AAHBRANT.SST.TeamsApp/src/pages/epi/EntregasTab.tsx`, change:

```tsx
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
```

to:

```tsx
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [episPermitidos, setEpisPermitidos] = useState<CatalogoEpi[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
```

(`epis` stays — it is still used by `nomeEpi()` to render the já-registradas table.)

- [ ] **Step 2: Add the effect that fetches the função's allowed EPIs**

Right after the existing `useEffect(() => { carregar(); }, []);` block, add:

```tsx
  useEffect(() => {
    let cancelado = false;
    async function carregarEpisPermitidos() {
      const trabalhador = trabalhadores.find((t) => t.id === novaEntrega.trabalhadorId);
      if (!trabalhador) {
        setEpisPermitidos([]);
        return;
      }
      try {
        const lista = await api.funcoes.listarEpis(trabalhador.funcaoId);
        if (!cancelado) setEpisPermitidos(lista);
      } catch {
        if (!cancelado) setEpisPermitidos([]);
      }
    }
    carregarEpisPermitidos();
    return () => {
      cancelado = true;
    };
  }, [novaEntrega.trabalhadorId, trabalhadores]);
```

- [ ] **Step 3: Reset the EPI selection when the trabalhador changes**

Change the trabalhador `<Select>`'s `onChange`:

```tsx
            <Select
              value={novaEntrega.trabalhadorId}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, trabalhadorId: d.value })}
            >
```

to:

```tsx
            <Select
              value={novaEntrega.trabalhadorId}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, trabalhadorId: d.value, catalogoEpiId: '' })}
            >
```

- [ ] **Step 4: Filter the EPI select and add the empty-state shortcut**

Change:

```tsx
          <Field label="EPI">
            <Select
              value={novaEntrega.catalogoEpiId}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoEpiId: d.value })}
            >
              <option value="">Selecione</option>
              {epis.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.nome} (estoque: {e.saldoEstoque})
                </option>
              ))}
            </Select>
          </Field>
```

to:

```tsx
          <Field label="EPI">
            <Select
              value={novaEntrega.catalogoEpiId}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoEpiId: d.value })}
              disabled={!novaEntrega.trabalhadorId || episPermitidos.length === 0}
            >
              <option value="">Selecione</option>
              {episPermitidos.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.nome} (estoque: {e.saldoEstoque})
                </option>
              ))}
            </Select>
            {novaEntrega.trabalhadorId && episPermitidos.length === 0 && (
              <Text size={200}>
                Esta função não tem EPIs cadastrados na matriz.{' '}
                <Button appearance="transparent" size="small" onClick={() => navigate('/operacao/pessoas')}>
                  Cadastrar em Pessoas → Funções
                </Button>
              </Text>
            )}
          </Field>
```

`navigate` and `useNavigate` are already imported and in use in this file (for the "assinar" shortcut) — no new import needed. `PessoasPage` is mounted at `/operacao/pessoas` (`src/AAHBRANT.SST.TeamsApp/src/App.tsx:134`); since `PessoasPage.tsx` selects its tab via local component state (no URL param), this is a generic navigation to the Pessoas page, not a deep link straight into the Funções tab — the button label says so explicitly.

- [ ] **Step 5: Typecheck**

Run:
```bash
cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit
```
Expected: no new type errors.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/epi/EntregasTab.tsx
git commit -m "feat: filtra select de EPI pela matriz da funcao do trabalhador"
```

---

### Task 9: Manual end-to-end browser verification

**Files:** none (verification only).

**Interfaces:** none — this task exercises Tasks 1-8 together through the running app.

This replaces the integration-test layer that spec §7 calls for (`GET /api/funcoes/{id}/epis` empty-for-no-matrix / correct-after-PUT) — per the Global Constraints note, there is no `WebApplicationFactory` harness in this repo to build one on, so this is done by hand against the real running stack, which also happens to be spec §7's third bullet.

- [ ] **Step 1: Start the API and frontend**

Run the API (Development environment, so `MockObraSeeder` runs on startup):
```bash
dotnet run --project src/AAHBRANT.SST.Api
```

Run the frontend dev server (separate terminal):
```bash
cd src/AAHBRANT.SST.TeamsApp && npm run dev
```

- [ ] **Step 2: Verify the seeded demo matrix**

Open the app, navigate to **Operação → Pessoas → Funções**. Click the row for "Soldador". Confirm the expanded panel shows a checklist of all catalog EPIs, with **Capacete de Segurança Classe B**, **Bota de Segurança com Bico de Aço**, **Luva de Vaqueta**, **Óculos de Proteção Ampla Visão**, and **Protetor Auricular Tipo Plug** pre-checked (per the `MatrizEpiPorFuncao` seed table from Task 5), and the rest unchecked.

- [ ] **Step 3: Verify saving the matrix**

Uncheck one EPI (e.g. "Protetor Auricular Tipo Plug"), click "Salvar matriz". Confirm the panel closes without error. Re-open the "Soldador" row and confirm the unchecked EPI stayed unchecked (persisted) and the rest stayed checked. Re-check it and save again, to leave the demo data as originally seeded.

- [ ] **Step 4: Verify a função with no matrix shows an empty checklist**

Add a new função via the form at the top (e.g. "Função Teste Sem Matriz"). Click its row. Confirm the checklist appears with **all EPIs unchecked** (this is the "GET returns empty for função with no matrix" case from spec §7).

- [ ] **Step 5: Verify the EntregasTab filter**

Navigate to the EPI module's **Entregas** tab. In "Nova entrega de EPI", select a trabalhador whose função is "Soldador". Confirm the EPI select now shows **only** the 5 EPIs linked to Soldador (not the full catalog) — open the dropdown and count the options.

- [ ] **Step 6: Verify the empty-state shortcut**

In the same form, select a trabalhador whose função is "Função Teste Sem Matriz" (create one via Pessoas → Funções if none exists among the seeded 200 workers — or temporarily change one seeded worker's função via the Pessoas tab, then change it back after this check). Confirm the EPI select becomes disabled/empty and the "Cadastrar em Pessoas → Funções" message and button appear. Click the button and confirm it navigates to `/operacao/pessoas`.

- [ ] **Step 7: Clean up**

Delete the "Função Teste Sem Matriz" função created in Step 4 (via the trash icon in FuncoesTab), so it doesn't pollute the demo data. Revert any worker função changes made in Step 6, if applicable.

- [ ] **Step 8: Run the full automated test suite one more time**

```bash
dotnet test
```
Expected: all tests pass (including the 5 new ones from Task 2).

---

## Self-Review Notes

- **Spec coverage:** §4.1 entity → Task 1. §4.2 command/query → Tasks 2-3. §4.3 endpoints/policies → Task 4. §4.4 UI (`FuncoesTab`, `EntregasTab`, `api.ts`) → Tasks 6-8. §5 migration → Task 1. §6 Pendência 1 (combined GET policy) → resolved in Global Constraints (no combined policy needed, both actions already sit on the `organizacional:*`-only `FuncoesController`). §6 Pendência 2 (seed) → Task 5. §7 tests → Task 2 (unit), Task 9 (manual, replacing the integration-test bullet per the documented no-`WebApplicationFactory`-precedent rationale).
- **Placeholder scan:** no TBD/TODO, every step has literal code, no "similar to Task N" references — checked.
- **Type consistency:** `DefinirMatrizEpiFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoEpiIds)` (Task 2) matches `DefinirEpisRequest(List<Guid> CatalogoEpiIds)` + route `id` (Task 4) matches `api.funcoes.definirEpis(funcaoId: string, catalogoEpiIds: string[])` (Task 6) matches the `{ catalogoEpiIds }` body shape. `ListarEpisPorFuncaoQuery` returns `List<CatalogoEpiDto>` (Task 3), matching `api.funcoes.listarEpis` returning `Promise<CatalogoEpi[]>` (Task 6) field-for-field (`id`, `nome`, `fabricante`, `certificadoAprovacaoNumero`, `certificadoAprovacaoValidade`, `vidaUtilEmMeses`, `saldoEstoque`). `IAppDbContext.MatrizEpiFuncoes` (Task 1) is the exact name used in Task 2's handler, Task 3's handler, and Task 5's seeder wiring — checked.
