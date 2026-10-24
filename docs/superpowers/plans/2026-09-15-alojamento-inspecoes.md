# Alojamento em Inspeções — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar ao técnico de SST um atalho dedicado para inspecionar alojamentos — sub-aba "Alojamento" em Inspeções, com cards de obra → alojamento, criação/retomada automática de inspeção pré-preenchida, seletor de 3 estados por item e foto obrigatória.

**Architecture:** Segue o padrão CQRS/MediatR já usado no projeto (Command/Query + Handler + `IAppDbContext`, sem lógica de RBAC no handler — RBAC é `[Authorize(Policy="codigo")]` no Controller + registro em `RbacSeeder`). Três entidades novas (`Alojamento`, `AlojamentoMorador`, `ConfiguracaoAlojamento`) + um campo novo em `Inspecao`. Frontend reaproveita `InspecaoDetalhePage.tsx` (tela genérica) com um seletor de status novo, condicional ao tipo de inspeção.

**Tech Stack:** .NET 8 / EF Core / MediatR / FluentValidation / xUnit (backend). React + TypeScript + Fluent UI (`@ui`) (frontend).

**Spec:** `docs/superpowers/specs/2026-09-15-alojamento-inspecoes-design.md`

## Global Constraints

- RBAC nunca é validado dentro de Handlers — só via `[Authorize(Policy = "codigo")]` no Controller. Todo endpoint novo precisa de um código novo registrado em `RbacSeeder.CatalogoPermissoes`.
- Toda entidade nova herda `AuditableEntity` (dá `Id`, `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy`, `Origem` (enum `OrigemRegistro`), `Ativo`, `RowVersion` de graça — não duplicar esses campos).
- Toda FK usa `OnDelete(DeleteBehavior.Restrict)` (nunca Cascade). Toda entidade usa `HasQueryFilter(x => x.Ativo)` (soft delete global). Toda string tem `HasMaxLength` explícito.
- Migrações: `dotnet ef migrations add <Nome> --project ../AAHBRANT.SST.Infrastructure --startup-project . --output-dir Persistencia/Migrations`, rodado a partir de `src/AAHBRANT.SST.Api`. O `Program.cs` roda `Database.Migrate()` automaticamente no startup — uma migration ruim quebra o deploy direto, então cada migration deste plano precisa ser aplicada localmente (`dotnet run` na Api) antes do commit, não só gerada.
- Testes de backend: xUnit puro, sem mocks — `SstDbContext` real com `UseInMemoryDatabase`, um `[Fact]` por cenário, nome `Handle_<Cenário>_<Resultado>`.
- **Não existe hoje teste automatizado de comportamento de página no frontend** (só snapshot visual via Playwright, `tests-ui/galeria.spec.ts`). As tasks de frontend deste plano não seguem TDD — cada uma termina com um passo de verificação manual (rodar o dev server, navegar, conferir no navegador), não um teste automatizado. Isso é consistente com o resto do projeto, não uma lacuna deste plano.
- Índice único filtrado (convenção do projeto): `builder.HasIndex(x => x.Coluna).IsUnique().HasFilter("[Coluna] IS NOT NULL")` (ou outra condição SQL Server crua, com colchetes).

---

## Task 1: Entidades de domínio + configuração EF + migração

**Files:**
- Create: `src/AAHBRANT.SST.Domain/Entidades/Alojamento.cs`
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Inspecoes/Inspecao.cs` (campo `AlojamentoId`)
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AlojamentoConfiguracoes.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs` (novos `DbSet`)
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/ConfiguracaoAlojamentoSeeder.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/SeedRunner.cs` (ou onde os seeders são chamados — localizar arquivo real antes de editar, ex.: grep por `ChecklistAlojamentoSeeder.ExecutarAsync` para achar o ponto de chamada)
- Test: `tests/AAHBRANT.SST.Application.Tests/Alojamentos/AlojamentoEntidadeTests.cs`

**Interfaces:**
- Produces: `Alojamento` (Id, ObraId, Nome, Endereco, GrhAlojamentoId, DataUltimaSincronizacao — mais os campos de `AuditableEntity`), `AlojamentoMorador` (Id, AlojamentoId, TrabalhadorId, DataDesde, DataSaida), `ConfiguracaoAlojamento` (Id, DiasParaInspecaoAtrasada), `Inspecao.AlojamentoId` (Guid?).

- [ ] **Step 1: Escrever o teste de invariante (índice único de morador ativo)**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Alojamentos/AlojamentoEntidadeTests.cs
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class AlojamentoEntidadeTests
{
    private static SstDbContext CriarDbSqlite(string nomeArquivo)
    {
        // InMemory do EF Core não aplica índices/constraints reais — para testar um índice único
        // filtrado é preciso um provider relacional de verdade. Sqlite in-memory é o mais leve.
        var conexao = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        conexao.Open();
        var options = new DbContextOptionsBuilder<SstDbContext>().UseSqlite(conexao).Options;
        var db = new SstDbContext(options, new CurrentUserService());
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task SaveChanges_DoisVinculosAtivosParaMesmoTrabalhador_LancaExcecaoDeConstraint()
    {
        using var db = CriarDbSqlite(nameof(SaveChanges_DoisVinculosAtivosParaMesmoTrabalhador_LancaExcecaoDeConstraint));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var trabalhador = new Trabalhador { Nome = "Fulano", ObraId = obra.Id };
        var alojamentoA = new Alojamento { Nome = "Alojamento A", ObraId = obra.Id };
        var alojamentoB = new Alojamento { Nome = "Alojamento B", ObraId = obra.Id };
        db.Obras.Add(obra);
        db.Trabalhadores.Add(trabalhador);
        db.Alojamentos.AddRange(alojamentoA, alojamentoB);
        await db.SaveChangesAsync();

        db.AlojamentoMoradores.Add(new AlojamentoMorador { AlojamentoId = alojamentoA.Id, TrabalhadorId = trabalhador.Id, DataDesde = DateTime.UtcNow });
        await db.SaveChangesAsync();

        db.AlojamentoMoradores.Add(new AlojamentoMorador { AlojamentoId = alojamentoB.Id, TrabalhadorId = trabalhador.Id, DataDesde = DateTime.UtcNow });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
```

- [ ] **Step 2: Rodar o teste para confirmar que falha** (entidades ainda não existem)

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter AlojamentoEntidadeTests`
Expected: FAIL (erro de compilação — `Alojamento`/`AlojamentoMorador` não existem ainda)

- [ ] **Step 3: Criar as entidades de domínio**

```csharp
// src/AAHBRANT.SST.Domain/Entidades/Alojamento.cs
using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Alojamento cadastrado por obra — hoje só manual; GrhAlojamentoId/DataUltimaSincronizacao ficam
// nulos até a integração com o G-RH existir (ver docs/superpowers/2026-09-15-pedido-integracao-
// alojamento-grh.md). Quando a sync existir, Origem (herdado de AuditableEntity) marca se o
// registro veio de lá.
public class Alojamento : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public string? GrhAlojamentoId { get; set; }
    public DateTime? DataUltimaSincronizacao { get; set; }

    public ICollection<AlojamentoMorador> Moradores { get; set; } = new List<AlojamentoMorador>();
}

// Vínculo morador-alojamento. DataSaida nula = morador atual. Índice único (ver
// AlojamentoConfiguracoes.cs) garante que um mesmo TrabalhadorId nunca tenha dois vínculos ativos
// ao mesmo tempo — regra de negócio, não só otimização.
public class AlojamentoMorador : AuditableEntity
{
    public Guid AlojamentoId { get; set; }
    public Alojamento? Alojamento { get; set; }
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public DateTime DataDesde { get; set; }
    public DateTime? DataSaida { get; set; }
}

// Configuração global (linha única) — "quantos dias sem inspeção concluída até um alojamento virar
// 'atrasado' nos cards". Nunca hardcoded no front, editável via tela de Administração (Task 4).
public class ConfiguracaoAlojamento : AuditableEntity
{
    public int DiasParaInspecaoAtrasada { get; set; } = 30;
}
```

Modificar `Inspecao.cs` — adicionar logo abaixo de `AtividadeId`:

```csharp
public Guid? AlojamentoId { get; set; }
public Alojamento? Alojamento { get; set; }
```

- [ ] **Step 4: Configurar EF (mapeamento + índices)**

```csharp
// src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AlojamentoConfiguracoes.cs
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AlojamentoConfiguracao : IEntityTypeConfiguration<Alojamento>
{
    public void Configure(EntityTypeBuilder<Alojamento> builder)
    {
        builder.Property(a => a.Nome).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Endereco).HasMaxLength(300);
        builder.Property(a => a.GrhAlojamentoId).HasMaxLength(100);
        builder.HasOne(a => a.Obra).WithMany()
            .HasForeignKey(a => a.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.GrhAlojamentoId).IsUnique().HasFilter("[GrhAlojamentoId] IS NOT NULL");
        builder.HasQueryFilter(a => a.Ativo);
        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}

public class AlojamentoMoradorConfiguracao : IEntityTypeConfiguration<AlojamentoMorador>
{
    public void Configure(EntityTypeBuilder<AlojamentoMorador> builder)
    {
        builder.HasOne(m => m.Alojamento).WithMany(a => a.Moradores)
            .HasForeignKey(m => m.AlojamentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Trabalhador).WithMany()
            .HasForeignKey(m => m.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        // Um trabalhador só pode ter um vínculo ativo (DataSaida nula) por vez — em qualquer alojamento.
        builder.HasIndex(m => m.TrabalhadorId).IsUnique().HasFilter("[DataSaida] IS NULL");
        builder.HasQueryFilter(m => m.Ativo);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}

public class ConfiguracaoAlojamentoConfiguracao : IEntityTypeConfiguration<ConfiguracaoAlojamento>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoAlojamento> builder)
    {
        builder.HasQueryFilter(c => c.Ativo);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}
```

Modificar a configuração existente de `Inspecao` (localizar o arquivo real via
`grep -rl "class InspecaoConfiguracao" src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/`)
e acrescentar dentro do método `Configure`:

```csharp
builder.HasOne(i => i.Alojamento).WithMany()
    .HasForeignKey(i => i.AlojamentoId).OnDelete(DeleteBehavior.Restrict);
```

Registrar os `DbSet` em `SstDbContext.cs`, junto dos outros:

```csharp
public DbSet<Alojamento> Alojamentos => Set<Alojamento>();
public DbSet<AlojamentoMorador> AlojamentoMoradores => Set<AlojamentoMorador>();
public DbSet<ConfiguracaoAlojamento> ConfiguracoesAlojamento => Set<ConfiguracaoAlojamento>();
```

- [ ] **Step 5: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter AlojamentoEntidadeTests`
Expected: PASS

- [ ] **Step 6: Criar o seeder da configuração padrão**

```csharp
// src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/ConfiguracaoAlojamentoSeeder.cs
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Idempotente (mesmo padrão de ChecklistAlojamentoSeeder/RegraAlertaSeeder): garante que sempre
// exista exatamente uma linha de configuração, com o padrão de 30 dias caso ninguém tenha mexido.
public static class ConfiguracaoAlojamentoSeeder
{
    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var jaExiste = await db.ConfiguracoesAlojamento.IgnoreQueryFilters().AnyAsync(ct);
        if (jaExiste) return;

        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        await db.SaveChangesAsync(ct);
    }
}
```

Localizar onde `ChecklistAlojamentoSeeder.ExecutarAsync(...)` é chamado (grep no `Program.cs` da Api
ou num `SeedRunner`/`DbInitializer` — o nome exato do arquivo não foi confirmado no levantamento,
localizar antes de editar) e adicionar a chamada de `ConfiguracaoAlojamentoSeeder.ExecutarAsync(...)`
logo ao lado.

- [ ] **Step 7: Gerar e aplicar a migração**

Run (a partir de `src/AAHBRANT.SST.Api`):
```
dotnet ef migrations add CriarModuloAlojamento --project ../AAHBRANT.SST.Infrastructure --startup-project . --output-dir Persistencia/Migrations
```

Inspecionar o arquivo gerado — deve conter só `CreateTable` para `Alojamentos`, `AlojamentoMoradores`,
`ConfiguracoesAlojamento`, e `AddColumn` para `Inspecoes.AlojamentoId` + índice. Não deve haver
`AlterColumn` de `RowVersion` (esse gotcha só se aplica a colunas que já existiam). Depois:

Run: `dotnet run --project src/AAHBRANT.SST.Api` (deixa subir e aplicar a migração automaticamente, depois `Ctrl+C`)
Expected: sobe sem erro, log mostra a migração `CriarModuloAlojamento` aplicada.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Alojamento.cs \
        src/AAHBRANT.SST.Domain/Entidades/Inspecoes/Inspecao.cs \
        src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AlojamentoConfiguracoes.cs \
        src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs \
        src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/ConfiguracaoAlojamentoSeeder.cs \
        src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/ \
        tests/AAHBRANT.SST.Application.Tests/Alojamentos/AlojamentoEntidadeTests.cs
git commit -m "feat(alojamento): entidades de dominio, configuracao EF e migracao inicial

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 2: Cadastro manual de Alojamento (Command + Query + Controller)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Alojamentos/Commands/CriarAlojamentoCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Alojamentos/Queries/ListarAlojamentosQuery.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Alojamentos/CriarAlojamentoCommandHandlerTests.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Alojamentos/ListarAlojamentosQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `Alojamento`, `AlojamentoMorador`, `ConfiguracaoAlojamento` (Task 1).
- Produces: `CriarAlojamentoCommand(string Nome, string? Endereco, Guid ObraId) : IRequest<Guid>`;
  `ListarAlojamentosQuery(Guid? ObraId) : IRequest<List<AlojamentoResumoDto>>`;
  `record AlojamentoResumoDto(Guid Id, Guid ObraId, string Nome, string? Endereco, int TotalMoradores, string StatusUltimaInspecao, int? DiasDesdeUltimaInspecao)` — `StatusUltimaInspecao` é `"nunca" | "em-dia" | "atrasada"`, calculado com `ConfiguracaoAlojamento.DiasParaInspecaoAtrasada`.

- [ ] **Step 1: Escrever o teste do Command**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Alojamentos/CriarAlojamentoCommandHandlerTests.cs
using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class CriarAlojamentoCommandHandlerTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_ObraValida_CriaAlojamentoAtivo()
    {
        var db = CriarDb(nameof(Handle_ObraValida_CriaAlojamentoAtivo));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new CriarAlojamentoCommandHandler(db);
        var id = await handler.Handle(new CriarAlojamentoCommand("Alojamento 01", "Rua X, 100", obra.Id), default);

        var alojamento = await db.Alojamentos.FirstAsync(a => a.Id == id);
        Assert.Equal("Alojamento 01", alojamento.Nome);
        Assert.True(alojamento.Ativo);
    }

    [Fact]
    public async Task Handle_ObraInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_ObraInexistente_LancaKeyNotFoundException));
        var handler = new CriarAlojamentoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new CriarAlojamentoCommand("Alojamento 01", null, Guid.NewGuid()), default));
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter CriarAlojamentoCommandHandlerTests`
Expected: FAIL (namespace/classe não existe)

- [ ] **Step 3: Implementar o Command**

```csharp
// src/AAHBRANT.SST.Application/Alojamentos/Commands/CriarAlojamentoCommand.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record CriarAlojamentoCommand(string Nome, string? Endereco, Guid ObraId) : IRequest<Guid>;

public class CriarAlojamentoCommandValidator : AbstractValidator<CriarAlojamentoCommand>
{
    public CriarAlojamentoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Endereco).MaximumLength(300);
        RuleFor(x => x.ObraId).NotEmpty();
    }
}

public class CriarAlojamentoCommandHandler : IRequestHandler<CriarAlojamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarAlojamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAlojamentoCommand request, CancellationToken ct)
    {
        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var alojamento = new Alojamento
        {
            Nome = request.Nome,
            Endereco = request.Endereco,
            ObraId = request.ObraId,
        };
        _db.Alojamentos.Add(alojamento);
        await _db.SaveChangesAsync(ct);
        return alojamento.Id;
    }
}
```

- [ ] **Step 4: Rodar e confirmar passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter CriarAlojamentoCommandHandlerTests`
Expected: PASS

- [ ] **Step 5: Escrever o teste da Query de listagem**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Alojamentos/ListarAlojamentosQueryHandlerTests.cs
using AAHBRANT.SST.Application.Alojamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class ListarAlojamentosQueryHandlerTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_SemInspecaoNenhuma_StatusNunca()
    {
        var db = CriarDb(nameof(Handle_SemInspecaoNenhuma_StatusNunca));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        db.Alojamentos.Add(alojamento);
        await db.SaveChangesAsync();

        var handler = new ListarAlojamentosQueryHandler(db);
        var resultado = await handler.Handle(new ListarAlojamentosQuery(obra.Id), default);

        var dto = Assert.Single(resultado);
        Assert.Equal("nunca", dto.StatusUltimaInspecao);
        Assert.Equal(0, dto.TotalMoradores);
    }
}
```

- [ ] **Step 6: Rodar e confirmar falha, depois implementar a Query**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ListarAlojamentosQueryHandlerTests`
Expected: FAIL

```csharp
// src/AAHBRANT.SST.Application/Alojamentos/Queries/ListarAlojamentosQuery.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Queries;

public record ListarAlojamentosQuery(Guid? ObraId) : IRequest<List<AlojamentoResumoDto>>;

public record AlojamentoResumoDto(
    Guid Id,
    Guid ObraId,
    string Nome,
    string? Endereco,
    int TotalMoradores,
    string StatusUltimaInspecao,
    int? DiasDesdeUltimaInspecao);

public class ListarAlojamentosQueryHandler : IRequestHandler<ListarAlojamentosQuery, List<AlojamentoResumoDto>>
{
    private readonly IAppDbContext _db;
    public ListarAlojamentosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AlojamentoResumoDto>> Handle(ListarAlojamentosQuery request, CancellationToken ct)
    {
        var diasParaAtraso = (await _db.ConfiguracoesAlojamento.FirstOrDefaultAsync(ct))?.DiasParaInspecaoAtrasada ?? 30;
        var hoje = DateTime.UtcNow;

        var query = _db.Alojamentos.AsNoTracking();
        if (request.ObraId is { } obraId) query = query.Where(a => a.ObraId == obraId);

        var alojamentos = await query
            .Select(a => new
            {
                a.Id,
                a.ObraId,
                a.Nome,
                a.Endereco,
                TotalMoradores = a.Moradores.Count(m => m.DataSaida == null),
                UltimaInspecaoData = _db.Inspecoes
                    .Where(i => i.AlojamentoId == a.Id && i.Status == StatusInspecao.Concluida)
                    .OrderByDescending(i => i.Data)
                    .Select(i => (DateTime?)i.Data)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return alojamentos.Select(a =>
        {
            if (a.UltimaInspecaoData is not { } ultima)
                return new AlojamentoResumoDto(a.Id, a.ObraId, a.Nome, a.Endereco, a.TotalMoradores, "nunca", null);

            var dias = (int)(hoje - ultima).TotalDays;
            var status = dias > diasParaAtraso ? "atrasada" : "em-dia";
            return new AlojamentoResumoDto(a.Id, a.ObraId, a.Nome, a.Endereco, a.TotalMoradores, status, dias);
        }).ToList();
    }
}
```

Ajustar o nome do enum `StatusInspecao.Concluida` para o valor real (confirmar em
`src/AAHBRANT.SST.Domain/Enums/Enums.cs` antes de compilar — o levantamento não confirmou os
valores exatos desse enum específico).

- [ ] **Step 7: Rodar e confirmar passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "CriarAlojamentoCommandHandlerTests|ListarAlojamentosQueryHandlerTests"`
Expected: PASS (todos)

- [ ] **Step 8: Registrar permissões RBAC novas**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs`, dentro de `CatalogoPermissoes`,
adicionar (mesmo formato das entradas de `inspecao:*`):

```csharp
("alojamento:ver", "Alojamento", "Ver", "Ver alojamentos cadastrados e seus moradores"),
("alojamento:criar", "Alojamento", "Criar", "Cadastrar alojamento"),
("alojamento:gerenciar-moradores", "Alojamento", "GerenciarMoradores", "Adicionar/remover morador de alojamento"),
```

- [ ] **Step 9: Criar o Controller**

```csharp
// src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs
using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Alojamentos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlojamentosController : ControllerBase
{
    private readonly IMediator _mediator;
    public AlojamentosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "alojamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
    {
        var resultado = await _mediator.Send(new ListarAlojamentosQuery(obraId), ct);
        return Ok(resultado);
    }

    [Authorize(Policy = "alojamento:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAlojamentoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { obraId = command.ObraId }, new { id });
    }
}
```

- [ ] **Step 10: Rodar a suíte completa do backend**

Run: `dotnet test`
Expected: PASS (nenhuma regressão)

- [ ] **Step 11: Commit**

```bash
git add src/AAHBRANT.SST.Application/Alojamentos/ \
        src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs \
        src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs \
        tests/AAHBRANT.SST.Application.Tests/Alojamentos/CriarAlojamentoCommandHandlerTests.cs \
        tests/AAHBRANT.SST.Application.Tests/Alojamentos/ListarAlojamentosQueryHandlerTests.cs
git commit -m "feat(alojamento): cadastro manual e listagem com status de inspecao

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 3: Gestão de moradores (adicionar/remover)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Alojamentos/Commands/AdicionarMoradorAlojamentoCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Alojamentos/Commands/RemoverMoradorAlojamentoCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Alojamentos/AdicionarMoradorAlojamentoCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Alojamento`, `AlojamentoMorador` (Task 1).
- Produces: `AdicionarMoradorAlojamentoCommand(Guid AlojamentoId, Guid TrabalhadorId) : IRequest<Guid>`;
  `RemoverMoradorAlojamentoCommand(Guid AlojamentoMoradorId) : IRequest`.

- [ ] **Step 1: Escrever o teste — impedir vínculo duplicado ativo**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Alojamentos/AdicionarMoradorAlojamentoCommandHandlerTests.cs
using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class AdicionarMoradorAlojamentoCommandHandlerTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_TrabalhadorJaTemVinculoAtivo_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_TrabalhadorJaTemVinculoAtivo_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var trabalhador = new Trabalhador { Nome = "Fulano", ObraId = obra.Id };
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        db.AddRange(obra, trabalhador, alojamento);
        await db.SaveChangesAsync();

        var handler = new AdicionarMoradorAlojamentoCommandHandler(db);
        await handler.Handle(new AdicionarMoradorAlojamentoCommand(alojamento.Id, trabalhador.Id), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AdicionarMoradorAlojamentoCommand(alojamento.Id, trabalhador.Id), default));
    }
}
```

Nota: o InMemory provider do EF Core não aplica índices únicos reais — por isso o handler precisa
checar a regra explicitamente antes de inserir (não depender só do índice do banco para esse
teste específico; o índice do banco é a segunda linha de defesa contra corrida de concorrência,
verificada de outra forma no Task 1).

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter AdicionarMoradorAlojamentoCommandHandlerTests`
Expected: FAIL

- [ ] **Step 3: Implementar os Commands**

```csharp
// src/AAHBRANT.SST.Application/Alojamentos/Commands/AdicionarMoradorAlojamentoCommand.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record AdicionarMoradorAlojamentoCommand(Guid AlojamentoId, Guid TrabalhadorId) : IRequest<Guid>;

public class AdicionarMoradorAlojamentoCommandHandler : IRequestHandler<AdicionarMoradorAlojamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public AdicionarMoradorAlojamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AdicionarMoradorAlojamentoCommand request, CancellationToken ct)
    {
        var alojamentoExiste = await _db.Alojamentos.AnyAsync(a => a.Id == request.AlojamentoId, ct);
        if (!alojamentoExiste)
            throw new KeyNotFoundException($"Alojamento {request.AlojamentoId} não encontrado.");

        var jaTemVinculoAtivo = await _db.AlojamentoMoradores
            .AnyAsync(m => m.TrabalhadorId == request.TrabalhadorId && m.DataSaida == null, ct);
        if (jaTemVinculoAtivo)
            throw new InvalidOperationException("Este trabalhador já tem um vínculo de alojamento ativo.");

        var morador = new AlojamentoMorador
        {
            AlojamentoId = request.AlojamentoId,
            TrabalhadorId = request.TrabalhadorId,
            DataDesde = DateTime.UtcNow,
        };
        _db.AlojamentoMoradores.Add(morador);
        await _db.SaveChangesAsync(ct);
        return morador.Id;
    }
}
```

```csharp
// src/AAHBRANT.SST.Application/Alojamentos/Commands/RemoverMoradorAlojamentoCommand.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record RemoverMoradorAlojamentoCommand(Guid AlojamentoMoradorId) : IRequest;

public class RemoverMoradorAlojamentoCommandHandler : IRequestHandler<RemoverMoradorAlojamentoCommand>
{
    private readonly IAppDbContext _db;
    public RemoverMoradorAlojamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverMoradorAlojamentoCommand request, CancellationToken ct)
    {
        var morador = await _db.AlojamentoMoradores.FirstOrDefaultAsync(m => m.Id == request.AlojamentoMoradorId, ct)
            ?? throw new KeyNotFoundException($"Vínculo {request.AlojamentoMoradorId} não encontrado.");

        morador.DataSaida = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar e confirmar passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter AdicionarMoradorAlojamentoCommandHandlerTests`
Expected: PASS

- [ ] **Step 5: Acrescentar as rotas no Controller**

```csharp
[Authorize(Policy = "alojamento:gerenciar-moradores")]
[HttpPost("{alojamentoId:guid}/moradores")]
public async Task<IActionResult> AdicionarMorador(Guid alojamentoId, [FromBody] Guid trabalhadorId, CancellationToken ct)
{
    var id = await _mediator.Send(new AdicionarMoradorAlojamentoCommand(alojamentoId, trabalhadorId), ct);
    return CreatedAtAction(nameof(Listar), null, new { id });
}

[Authorize(Policy = "alojamento:gerenciar-moradores")]
[HttpDelete("moradores/{alojamentoMoradorId:guid}")]
public async Task<IActionResult> RemoverMorador(Guid alojamentoMoradorId, CancellationToken ct)
{
    await _mediator.Send(new RemoverMoradorAlojamentoCommand(alojamentoMoradorId), ct);
    return NoContent();
}
```

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Alojamentos/Commands/AdicionarMoradorAlojamentoCommand.cs \
        src/AAHBRANT.SST.Application/Alojamentos/Commands/RemoverMoradorAlojamentoCommand.cs \
        src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs \
        tests/AAHBRANT.SST.Application.Tests/Alojamentos/AdicionarMoradorAlojamentoCommandHandlerTests.cs
git commit -m "feat(alojamento): adicionar e remover morador, com regra de vinculo unico ativo

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 4: Configuração de dias-para-atraso (admin)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Alojamentos/Queries/ObterConfiguracaoAlojamentoQuery.cs`
- Create: `src/AAHBRANT.SST.Application/Alojamentos/Commands/AtualizarConfiguracaoAlojamentoCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Alojamentos/AtualizarConfiguracaoAlojamentoCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ConfiguracaoAlojamento` (Task 1).
- Produces: `ObterConfiguracaoAlojamentoQuery : IRequest<int>` (retorna `DiasParaInspecaoAtrasada`);
  `AtualizarConfiguracaoAlojamentoCommand(int DiasParaInspecaoAtrasada) : IRequest`.

- [ ] **Step 1: Escrever o teste**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Alojamentos/AtualizarConfiguracaoAlojamentoCommandHandlerTests.cs
using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class AtualizarConfiguracaoAlojamentoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ConfiguracaoExistente_AtualizaDias()
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nameof(Handle_ConfiguracaoExistente_AtualizaDias)).Options;
        var db = new SstDbContext(options, new CurrentUserService());
        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        await db.SaveChangesAsync();

        var handler = new AtualizarConfiguracaoAlojamentoCommandHandler(db);
        await handler.Handle(new AtualizarConfiguracaoAlojamentoCommand(45), default);

        var config = await db.ConfiguracoesAlojamento.FirstAsync();
        Assert.Equal(45, config.DiasParaInspecaoAtrasada);
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter AtualizarConfiguracaoAlojamentoCommandHandlerTests`
Expected: FAIL

- [ ] **Step 3: Implementar Query + Command**

```csharp
// src/AAHBRANT.SST.Application/Alojamentos/Queries/ObterConfiguracaoAlojamentoQuery.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Queries;

public record ObterConfiguracaoAlojamentoQuery : IRequest<int>;

public class ObterConfiguracaoAlojamentoQueryHandler : IRequestHandler<ObterConfiguracaoAlojamentoQuery, int>
{
    private readonly IAppDbContext _db;
    public ObterConfiguracaoAlojamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(ObterConfiguracaoAlojamentoQuery request, CancellationToken ct) =>
        (await _db.ConfiguracoesAlojamento.FirstOrDefaultAsync(ct))?.DiasParaInspecaoAtrasada ?? 30;
}
```

```csharp
// src/AAHBRANT.SST.Application/Alojamentos/Commands/AtualizarConfiguracaoAlojamentoCommand.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record AtualizarConfiguracaoAlojamentoCommand(int DiasParaInspecaoAtrasada) : IRequest;

public class AtualizarConfiguracaoAlojamentoCommandValidator : AbstractValidator<AtualizarConfiguracaoAlojamentoCommand>
{
    public AtualizarConfiguracaoAlojamentoCommandValidator()
    {
        RuleFor(x => x.DiasParaInspecaoAtrasada).GreaterThan(0).LessThanOrEqualTo(365);
    }
}

public class AtualizarConfiguracaoAlojamentoCommandHandler : IRequestHandler<AtualizarConfiguracaoAlojamentoCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarConfiguracaoAlojamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarConfiguracaoAlojamentoCommand request, CancellationToken ct)
    {
        var config = await _db.ConfiguracoesAlojamento.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Configuração de Alojamento não foi seedada.");
        config.DiasParaInspecaoAtrasada = request.DiasParaInspecaoAtrasada;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar e confirmar passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter AtualizarConfiguracaoAlojamentoCommandHandlerTests`
Expected: PASS

- [ ] **Step 5: Registrar permissão e rotas**

`RbacSeeder.CatalogoPermissoes`:
```csharp
("alojamento:configurar", "Alojamento", "Configurar", "Editar configuração global de Alojamento"),
```

Controller:
```csharp
[Authorize(Policy = "alojamento:ver")]
[HttpGet("configuracao")]
public async Task<IActionResult> ObterConfiguracao(CancellationToken ct) =>
    Ok(new { diasParaInspecaoAtrasada = await _mediator.Send(new ObterConfiguracaoAlojamentoQuery(), ct) });

[Authorize(Policy = "alojamento:configurar")]
[HttpPut("configuracao")]
public async Task<IActionResult> AtualizarConfiguracao(AtualizarConfiguracaoAlojamentoCommand command, CancellationToken ct)
{
    await _mediator.Send(command, ct);
    return NoContent();
}
```

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Alojamentos/Queries/ObterConfiguracaoAlojamentoQuery.cs \
        src/AAHBRANT.SST.Application/Alojamentos/Commands/AtualizarConfiguracaoAlojamentoCommand.cs \
        src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs \
        src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs \
        tests/AAHBRANT.SST.Application.Tests/Alojamentos/AtualizarConfiguracaoAlojamentoCommandHandlerTests.cs
git commit -m "feat(alojamento): configuracao global de dias para inspecao atrasada

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 5: Criar/retomar inspeção de alojamento (endpoint atômico)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Alojamentos/Commands/ObterOuCriarInspecaoAlojamentoCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AlojamentoConfiguracoes.cs` (ou a
  configuração de `Inspecao` — localizar arquivo real) — índice único parcial garantindo só uma
  inspeção em andamento por alojamento
- Test: `tests/AAHBRANT.SST.Application.Tests/Alojamentos/ObterOuCriarInspecaoAlojamentoCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Alojamento` (Task 1), `CriarInspecaoCommand`-style creation logic (padrão já existente
  em `CriarInspecaoCommandHandler`, reaproveitado aqui, não chamado via `IMediator` — a criação é
  feita direto no handler novo para manter tudo na mesma transação).
- Produces: `ObterOuCriarInspecaoAlojamentoCommand(Guid AlojamentoId, Guid UsuarioAtualId) : IRequest<InspecaoAtualDto>`;
  `record InspecaoAtualDto(Guid InspecaoId, bool FoiCriadaAgora, Guid ResponsavelUsuarioId, DateTime CriadaEm)`.

- [ ] **Step 1: Escrever o índice único parcial (garantia no banco)**

Em `Inspecao`, a condição "em andamento" depende do enum `StatusInspecao` real — confirmar valores
em `src/AAHBRANT.SST.Domain/Enums/Enums.cs` antes de escrever o filtro SQL (ex.: se o valor for
`StatusInspecao.EmAndamento = 1`, o filtro fica `"[Status] = 1"`). Na configuração de `Inspecao`:

```csharp
// Só uma inspeção em andamento por alojamento — garantia no banco, não só na aplicação (evita
// duas inspeções abertas se dois técnicos clicarem ao mesmo tempo no mesmo alojamento).
builder.HasIndex(i => i.AlojamentoId)
    .IsUnique()
    .HasFilter("[AlojamentoId] IS NOT NULL AND [Status] = 1"); // ajustar o valor de Status conforme o enum real
```

- [ ] **Step 2: Escrever o teste do Command (cenário "cria nova")**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Alojamentos/ObterOuCriarInspecaoAlojamentoCommandHandlerTests.cs
using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class ObterOuCriarInspecaoAlojamentoCommandHandlerTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(SstDbContext db, Alojamento alojamento, Usuario usuario, ChecklistModelo checklist)> PrepararCenario(string nomeBanco)
    {
        var db = CriarDb(nomeBanco);
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico Teste" };
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        var checklist = new ChecklistModelo { Nome = "Checklist de Alojamento", TipoInspecao = TipoInspecao.Alojamento, Versao = 1 };
        db.AddRange(obra, usuario, alojamento, checklist);
        await db.SaveChangesAsync();
        return (db, alojamento, usuario, checklist);
    }

    [Fact]
    public async Task Handle_SemInspecaoEmAndamento_CriaNova()
    {
        var (db, alojamento, usuario, _) = await PrepararCenario(nameof(Handle_SemInspecaoEmAndamento_CriaNova));
        var handler = new ObterOuCriarInspecaoAlojamentoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var resultado = await handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, usuario.Id), default);

        Assert.True(resultado.FoiCriadaAgora);
        var inspecao = await db.Inspecoes.FirstAsync(i => i.Id == resultado.InspecaoId);
        Assert.Equal(alojamento.Id, inspecao.AlojamentoId);
        Assert.Equal(alojamento.ObraId, inspecao.ObraId);
    }

    [Fact]
    public async Task Handle_ComInspecaoEmAndamento_RetornaExistenteSemCriarOutra()
    {
        var (db, alojamento, usuario, _) = await PrepararCenario(nameof(Handle_ComInspecaoEmAndamento_RetornaExistenteSemCriarOutra));
        var handler = new ObterOuCriarInspecaoAlojamentoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var primeira = await handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, usuario.Id), default);
        var segunda = await handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, usuario.Id), default);

        Assert.Equal(primeira.InspecaoId, segunda.InspecaoId);
        Assert.False(segunda.FoiCriadaAgora);
        Assert.Equal(1, await db.Inspecoes.CountAsync(i => i.AlojamentoId == alojamento.Id));
    }

    [Fact]
    public async Task Handle_AlojamentoSemChecklistAlojamentoCadastrado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_AlojamentoSemChecklistAlojamentoCadastrado_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico Teste" };
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        db.AddRange(obra, usuario, alojamento);
        await db.SaveChangesAsync();
        var handler = new ObterOuCriarInspecaoAlojamentoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, usuario.Id), default));
    }
}
```

- [ ] **Step 3: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ObterOuCriarInspecaoAlojamentoCommandHandlerTests`
Expected: FAIL

- [ ] **Step 4: Implementar o Command**

```csharp
// src/AAHBRANT.SST.Application/Alojamentos/Commands/ObterOuCriarInspecaoAlojamentoCommand.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record ObterOuCriarInspecaoAlojamentoCommand(Guid AlojamentoId, Guid UsuarioAtualId) : IRequest<InspecaoAtualDto>;

public record InspecaoAtualDto(Guid InspecaoId, bool FoiCriadaAgora, Guid ResponsavelUsuarioId, DateTime CriadaEm);

public class ObterOuCriarInspecaoAlojamentoCommandHandler : IRequestHandler<ObterOuCriarInspecaoAlojamentoCommand, InspecaoAtualDto>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public ObterOuCriarInspecaoAlojamentoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<InspecaoAtualDto> Handle(ObterOuCriarInspecaoAlojamentoCommand request, CancellationToken ct)
    {
        var alojamento = await _db.Alojamentos.FirstOrDefaultAsync(a => a.Id == request.AlojamentoId, ct)
            ?? throw new KeyNotFoundException($"Alojamento {request.AlojamentoId} não encontrado.");

        // Confirmar o valor real de "em andamento" no enum StatusInspecao antes de fechar esta task.
        var existente = await _db.Inspecoes
            .Where(i => i.AlojamentoId == alojamento.Id && i.Status == StatusInspecao.EmAndamento)
            .OrderByDescending(i => i.Data)
            .FirstOrDefaultAsync(ct);

        if (existente is not null)
            return new InspecaoAtualDto(existente.Id, false, existente.ResponsavelUsuarioId, existente.CreatedAtUtc);

        var checklist = await _db.ChecklistModelos
            .Include(c => c.Itens)
            .Where(c => c.TipoInspecao == TipoInspecao.Alojamento)
            .OrderByDescending(c => c.Versao)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhum checklist de Alojamento cadastrado.");

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = alojamento.ObraId,
            AlojamentoId = alojamento.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = request.UsuarioAtualId,
            NumeroDocumento = await _geradorNumero.GerarAsync("INSP", ct),
        };
        foreach (var item in checklist.Itens.Where(i => i.Ativo))
            inspecao.Respostas.Add(new InspecaoItemResposta { ChecklistModeloItemId = item.Id });

        _db.Inspecoes.Add(inspecao);
        await _db.SaveChangesAsync(ct);
        return new InspecaoAtualDto(inspecao.Id, true, inspecao.ResponsavelUsuarioId, inspecao.CreatedAtUtc);
    }
}
```

Confirmar o nome exato do valor "em andamento" em `StatusInspecao` (grep
`src/AAHBRANT.SST.Domain/Enums/Enums.cs`) e ajustar a comparação antes de compilar.

- [ ] **Step 5: Rodar e confirmar passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ObterOuCriarInspecaoAlojamentoCommandHandlerTests`
Expected: PASS

- [ ] **Step 6: Gerar migração do índice único de inspeção em andamento**

Run:
```
dotnet ef migrations add IndiceUnicoInspecaoAlojamentoEmAndamento --project ../AAHBRANT.SST.Infrastructure --startup-project . --output-dir Persistencia/Migrations
```
Conferir que o SQL gerado bate com o filtro do Step 1 antes de aplicar (`dotnet run` na Api).

- [ ] **Step 7: Rota no Controller**

```csharp
[Authorize(Policy = "inspecao:criar")]
[HttpPost("{alojamentoId:guid}/inspecao-atual")]
public async Task<IActionResult> ObterOuCriarInspecaoAtual(Guid alojamentoId, CancellationToken ct)
{
    var usuarioAtualId = User.ObterIdUsuarioAtual(); // usar o mesmo helper/extensão já usado nos
                                                       // outros controllers para extrair o id do
                                                       // usuário logado do ClaimsPrincipal — localizar
                                                       // o nome real (grep "ObterIdUsuarioAtual" ou
                                                       // equivalente em outro controller) antes de
                                                       // fechar esta task.
    var resultado = await _mediator.Send(new ObterOuCriarInspecaoAlojamentoCommand(alojamentoId, usuarioAtualId), ct);
    return Ok(resultado);
}
```

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Application/Alojamentos/Commands/ObterOuCriarInspecaoAlojamentoCommand.cs \
        src/AAHBRANT.SST.Api/Controllers/AlojamentosController.cs \
        src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/ \
        src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/ \
        tests/AAHBRANT.SST.Application.Tests/Alojamentos/ObterOuCriarInspecaoAlojamentoCommandHandlerTests.cs
git commit -m "feat(alojamento): endpoint atomico de criar ou retomar inspecao

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 6: Foto obrigatória nos 30 itens do checklist

**Files:**
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/ChecklistAlojamentoSeeder.cs`
- Create: migração de dados (via `dotnet ef migrations add`, gerada em branco e editada manualmente)
- Modify: local onde a inspeção é concluída (localizar `grep -rl "EncerrarInspecaoCommand\|ConcluirInspecaoCommand"` — nome exato não confirmado no levantamento)
- Test: `tests/AAHBRANT.SST.Application.Tests/Inspecoes/ConcluirInspecaoComItemSemFotoObrigatoriaTests.cs` (nome do arquivo de teste depende do Command real encontrado)

**Interfaces:**
- Consumes: `ChecklistModeloItem.ExigeFotografia` (já existe).

- [ ] **Step 1: Atualizar o seeder (ambientes novos)**

Em `ChecklistAlojamentoSeeder.cs`, trocar `ExigeFotografia = false` por `ExigeFotografia = true` nas
3 ocorrências dentro do loop de criação dos itens (ou, se preferir manter o array `Itens` como
tupla de 2 valores, adicionar um terceiro elemento — decisão de estilo do implementador, manter
consistente com o restante do arquivo).

- [ ] **Step 2: Escrever a migração de dados para ambientes já seedados**

Run:
```
dotnet ef migrations add ExigirFotoChecklistAlojamento --project ../AAHBRANT.SST.Infrastructure --startup-project . --output-dir Persistencia/Migrations
```

Isso vai gerar um arquivo vazio (nenhuma mudança de schema). Editar manualmente o método `Up`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        UPDATE cmi
        SET cmi.ExigeFotografia = 1
        FROM ChecklistModeloItens cmi
        INNER JOIN ChecklistModelos cm ON cm.Id = cmi.ChecklistModeloId
        WHERE cm.TipoInspecao = 14;
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        UPDATE cmi
        SET cmi.ExigeFotografia = 0
        FROM ChecklistModeloItens cmi
        INNER JOIN ChecklistModelos cm ON cm.Id = cmi.ChecklistModeloId
        WHERE cm.TipoInspecao = 14;
    ");
}
```

Confirmar o nome real da tabela (`ChecklistModeloItens` é uma suposição pelo nome da classe — checar
no `SstDbContextModelSnapshot.cs` antes de aplicar) e que `14` é o valor real de
`TipoInspecao.Alojamento` (já confirmado no spec, mas reconferir no enum antes de rodar em produção).

- [ ] **Step 3: Aplicar e verificar manualmente**

Run: `dotnet run --project src/AAHBRANT.SST.Api` (aplica a migração)
Verificar: consultar `SELECT ExigeFotografia FROM ChecklistModeloItens cmi INNER JOIN ChecklistModelos cm ON cm.Id = cmi.ChecklistModeloId WHERE cm.TipoInspecao = 14` — todas as linhas devem retornar `1`.

- [ ] **Step 4: Localizar e ajustar a validação de conclusão da inspeção**

Localizar o Command que conclui uma inspeção (grep `"class.*InspecaoCommand.*Concluir\|Encerrar"` em
`src/AAHBRANT.SST.Application/Inspecoes/Commands/`). Dentro do handler, antes de marcar a inspeção
como concluída, adicionar a validação (adaptar nomes de propriedade ao Command real encontrado):

```csharp
var itensSemFotoObrigatoria = inspecao.Respostas
    .Where(r => r.ChecklistModeloItem!.ExigeFotografia && r.FotoConteudo == null)
    .Select(r => r.ChecklistModeloItem!.Descricao)
    .ToList();

if (itensSemFotoObrigatoria.Count > 0)
    throw new InvalidOperationException(
        $"Os seguintes itens exigem foto antes de concluir: {string.Join("; ", itensSemFotoObrigatoria)}");
```

(Confirmar se `Respostas` já vem com `.Include(r => r.ChecklistModeloItem)` carregado nesse handler
— se não vier, adicionar o Include antes desta checagem.)

- [ ] **Step 5: Escrever o teste dessa validação**

Nomear o arquivo/classe de teste conforme o Command real localizado no Step 4 (ex.:
`ConcluirInspecaoCommandHandlerTests`, método `Handle_ItemExigeFotoSemFoto_LancaInvalidOperationException`).
Seguir o mesmo padrão xUnit + `SstDbContext` in-memory dos testes anteriores deste plano.

- [ ] **Step 6: Rodar suíte completa**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/ChecklistAlojamentoSeeder.cs \
        src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/ \
        src/AAHBRANT.SST.Application/Inspecoes/Commands/
git commit -m "feat(alojamento): exige foto em todos os itens do checklist de alojamento

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 7: Cliente de API no frontend

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`

**Interfaces:**
- Consumes: endpoints das Tasks 2-5 (`GET/POST /api/alojamentos`, `POST /api/alojamentos/{id}/moradores`,
  `DELETE /api/alojamentos/moradores/{id}`, `GET/PUT /api/alojamentos/configuracao`,
  `POST /api/alojamentos/{id}/inspecao-atual`).
- Produces: objeto `api.alojamentos` com os métodos tipados, seguindo o padrão já usado por
  `api.inspecoes`/`api.trabalhadores` no mesmo arquivo (localizar um bloco existente, ex.
  `api.inspecoes = { listar, criar, ... }`, e replicar a mesma forma exata de `fetch`/tratamento de
  erro/tipagem de retorno — não inventar um padrão novo).

- [ ] **Step 1: Ler o bloco `api.inspecoes` existente**

Abrir `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts` e localizar o objeto `api.inspecoes` (ou
`api.trabalhadores`) para copiar exatamente a forma de cada método (helper de fetch usado, como
`obraId` opcional vira query string, como erros HTTP viram exceção JS).

- [ ] **Step 2: Adicionar os tipos e o objeto `api.alojamentos`**

```typescript
export interface AlojamentoResumo {
  id: string;
  obraId: string;
  nome: string;
  endereco: string | null;
  totalMoradores: number;
  statusUltimaInspecao: 'nunca' | 'em-dia' | 'atrasada';
  diasDesdeUltimaInspecao: number | null;
}

export interface InspecaoAtual {
  inspecaoId: string;
  foiCriadaAgora: boolean;
  responsavelUsuarioId: string;
  criadaEm: string;
}

// api.alojamentos: mesma forma de fetch/erro que api.inspecoes (copiar o helper real usado ali,
// ex. `fetchApi<T>(path, options)` — nome a confirmar no arquivo antes de implementar).
export const alojamentos = {
  listar: (obraId?: string) =>
    fetchApi<AlojamentoResumo[]>(`/api/alojamentos${obraId ? `?obraId=${obraId}` : ''}`),
  criar: (nome: string, endereco: string | null, obraId: string) =>
    fetchApi<{ id: string }>('/api/alojamentos', { method: 'POST', body: JSON.stringify({ nome, endereco, obraId }) }),
  adicionarMorador: (alojamentoId: string, trabalhadorId: string) =>
    fetchApi<{ id: string }>(`/api/alojamentos/${alojamentoId}/moradores`, { method: 'POST', body: JSON.stringify(trabalhadorId) }),
  removerMorador: (alojamentoMoradorId: string) =>
    fetchApi<void>(`/api/alojamentos/moradores/${alojamentoMoradorId}`, { method: 'DELETE' }),
  obterConfiguracao: () => fetchApi<{ diasParaInspecaoAtrasada: number }>('/api/alojamentos/configuracao'),
  atualizarConfiguracao: (diasParaInspecaoAtrasada: number) =>
    fetchApi<void>('/api/alojamentos/configuracao', { method: 'PUT', body: JSON.stringify({ diasParaInspecaoAtrasada }) }),
  obterOuCriarInspecaoAtual: (alojamentoId: string) =>
    fetchApi<InspecaoAtual>(`/api/alojamentos/${alojamentoId}/inspecao-atual`, { method: 'POST' }),
};
```

Adaptar `fetchApi` para o nome real do helper encontrado no Step 1, e registrar `alojamentos` dentro
do objeto `api` exportado (mesmo padrão de `api.inspecoes = inspecoes` já existente).

- [ ] **Step 3: Verificação manual**

Run: `npx tsc -b --noEmit` (a partir de `src/AAHBRANT.SST.TeamsApp`)
Expected: sem erros de tipo

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat(alojamento): cliente de api tipado para alojamentos e inspecao atual

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 8: Sub-aba "Alojamento" — cards de obra e alojamento

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/AlojamentoTab.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/InspecoesPage.tsx`

**Interfaces:**
- Consumes: `api.obras.listar()` (já existe), `api.alojamentos.listar(obraId?)` (Task 7).
- Produces: componente `AlojamentoTab` exportado, sem props (busca os próprios dados).

- [ ] **Step 1: Implementar `AlojamentoTab.tsx`**

Seguir o mesmo padrão visual/estrutural de `TrabalhadoresTab.tsx` (grade de obra → seleciona →
grade de sub-itens), adaptando os campos exibidos:

```tsx
import { useEffect, useMemo, useState } from 'react';
import { Card, Text } from '@ui';
import { api, type Obra, type AlojamentoResumo } from '../../lib/api';

export function AlojamentoTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [alojamentos, setAlojamentos] = useState<AlojamentoResumo[]>([]);
  const [obraSelecionadaId, setObraSelecionadaId] = useState<string | null>(null);

  useEffect(() => {
    api.obras.listar().then(setObras).catch(() => setObras([]));
    api.alojamentos.listar().then(setAlojamentos).catch(() => setAlojamentos([]));
  }, []);

  const resumoPorObra = useMemo(
    () =>
      obras.map((obra) => {
        const doAlojamentos = alojamentos.filter((a) => a.obraId === obra.id);
        const totalMoradores = doAlojamentos.reduce((soma, a) => soma + a.totalMoradores, 0);
        const pendentes = doAlojamentos.filter((a) => a.statusUltimaInspecao !== 'em-dia').length;
        return { obra, alojamentos: doAlojamentos, totalMoradores, pendentes };
      }),
    [obras, alojamentos],
  );

  if (obraSelecionadaId) {
    const obraAtual = resumoPorObra.find((r) => r.obra.id === obraSelecionadaId);
    if (!obraAtual) return null;
    return (
      <div>
        <button onClick={() => setObraSelecionadaId(null)}>&larr; Alojamento</button>
        <h1>{obraAtual.obra.nome}</h1>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))', gap: 14 }}>
          {obraAtual.alojamentos.length === 0 && <Text>Nenhum alojamento cadastrado para esta obra ainda.</Text>}
          {obraAtual.alojamentos.map((a) => (
            <Card key={a.id} onClick={() => abrirInspecao(a.id)}>
              <Text weight="semibold">{a.nome}</Text>
              {a.endereco && <Text size={200}>{a.endereco}</Text>}
              <Text>{a.totalMoradores} morador(es)</Text>
              <Text>{rotuloStatus(a.statusUltimaInspecao, a.diasDesdeUltimaInspecao)}</Text>
            </Card>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div>
      <h1>Alojamento</h1>
      <Text>Selecione uma obra para ver os alojamentos cadastrados nela.</Text>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))', gap: 14 }}>
        {resumoPorObra.map(({ obra, alojamentos: doAlojamentos, totalMoradores, pendentes }) => (
          <Card key={obra.id} onClick={() => setObraSelecionadaId(obra.id)}>
            <Text weight="semibold">{obra.nome}</Text>
            <Text>{doAlojamentos.length} alojamento(s)</Text>
            <Text>{totalMoradores} morador(es)</Text>
            <Text>
              {doAlojamentos.length === 0 ? 'Sem alojamento' : pendentes === 0 ? 'Todos em dia' : `${pendentes} pendente(s)`}
            </Text>
          </Card>
        ))}
      </div>
    </div>
  );

  function rotuloStatus(status: AlojamentoResumo['statusUltimaInspecao'], dias: number | null) {
    if (status === 'nunca') return 'Nunca inspecionado';
    if (status === 'atrasada') return `Atrasada · há ${dias} dias`;
    return `Em dia · há ${dias} dias`;
  }

  function abrirInspecao(alojamentoId: string) {
    // implementado na Task 9 — por enquanto, no-op ou console.log para não quebrar o build
    console.log('abrir inspecao', alojamentoId);
  }
}
```

Ajustar imports (`Card`/`Text` de `@ui` — confirmar exports reais do módulo antes de compilar; usar
os mesmos componentes que `TrabalhadoresTab.tsx` já importa) e estilos (usar `makeStyles`/classes já
padronizadas do projeto em vez de `style={{ ... }}` inline, se essa for a convenção real observada
em `TrabalhadoresTab.tsx` — confirmar antes de implementar).

- [ ] **Step 2: Adicionar a aba em `InspecoesPage.tsx`**

Modificar a linha `const ABAS = ['execucoes', 'checklists', 'dashboard'] as const;` para incluir
`'alojamento'`, adicionar o rótulo "Alojamento" no array de abas do componente `<Abas>`, e renderizar
`{aba === 'alojamento' && <AlojamentoTab />}` junto dos demais.

- [ ] **Step 3: Verificação manual**

Run: `npx tsc -b --noEmit`
Expected: sem erros

Iniciar o dev server (`sst-api` + `sst-web` via `.claude/launch.json`), navegar até
Operação → Inspeções → aba "Alojamento", confirmar visualmente:
- grade de obras aparece com contagens corretas;
- clicar numa obra revela a grade de alojamentos dela (ou mensagem de "nenhum cadastrado");
- botão de voltar retorna à grade de obras.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/AlojamentoTab.tsx \
        src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/InspecoesPage.tsx
git commit -m "feat(alojamento): sub-aba com cards de obra e alojamento

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 9: Clique no alojamento → abrir/retomar inspeção

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/AlojamentoTab.tsx`

**Interfaces:**
- Consumes: `api.alojamentos.obterOuCriarInspecaoAtual(alojamentoId)` (Task 7), `useNavigate` do
  `react-router-dom` (já usado em outras páginas do projeto, ex. `InspecoesTab.tsx`).

- [ ] **Step 1: Implementar `abrirInspecao` de verdade**

Substituir o `console.log` da Task 8 por:

```tsx
import { useNavigate } from 'react-router-dom';
// ...
const navigate = useNavigate();
const [erro, setErro] = useState<string | null>(null);

async function abrirInspecao(alojamentoId: string) {
  try {
    const resultado = await api.alojamentos.obterOuCriarInspecaoAtual(alojamentoId);
    navigate(`/prevencao/inspecoes/${resultado.inspecaoId}`, {
      state: resultado.foiCriadaAgora
        ? undefined
        : { avisoRetomada: `Inspeção em andamento, criada em ${new Date(resultado.criadaEm).toLocaleString('pt-BR')}` },
    });
  } catch (e) {
    setErro(e instanceof Error ? e.message : 'Não foi possível abrir a inspeção deste alojamento.');
  }
}
```

Renderizar `erro` na tela (mesmo padrão de mensagem de erro já usado em `InspecoesTab.tsx`, ex.
`FeedbackInline`/componente equivalente — confirmar nome real antes de usar).

- [ ] **Step 2: Mostrar o aviso de retomada em `InspecaoDetalhePage.tsx`**

Ler `location.state?.avisoRetomada` (via `useLocation` do `react-router-dom`) no topo de
`InspecaoDetalhePage.tsx` e, se presente, renderizar um banner informativo simples (mesmo
componente de feedback usado em outras telas do módulo) logo abaixo do cabeçalho.

- [ ] **Step 3: Verificação manual**

Rodar o dev server, clicar duas vezes seguidas no mesmo alojamento (em duas abas do navegador ou
recarregando entre os cliques): a segunda vez deve navegar para a MESMA inspeção e mostrar o
banner de retomada, não criar uma nova.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/AlojamentoTab.tsx \
        src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/InspecaoDetalhePage.tsx
git commit -m "feat(alojamento): clique no card cria ou retoma inspecao automaticamente

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 10: Seletor de 3 bolinhas (escopado a Alojamento)

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/components/inspecoes/SeletorStatusItemChecklist.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/InspecaoDetalhePage.tsx`

**Interfaces:**
- Consumes: `StatusItemChecklist` enum (já existe em `api.ts`), `perfil.tipoInspecao` (já disponível
  na tela de detalhe, confirmar o nome exato do campo antes de usar).
- Produces: `SeletorStatusItemChecklist` (props: `value: StatusItemChecklist | null`,
  `onChange: (v: StatusItemChecklist) => void`, `disabled?: boolean`).

- [ ] **Step 1: Implementar o componente**

```tsx
// src/AAHBRANT.SST.TeamsApp/src/components/inspecoes/SeletorStatusItemChecklist.tsx
import { StatusItemChecklist } from '../../lib/api';

const OPCOES: Array<{ valor: StatusItemChecklist; rotulo: string; cor: string }> = [
  { valor: StatusItemChecklist.Conforme, rotulo: 'Conforme', cor: 'var(--good, #2f9e58)' },
  { valor: StatusItemChecklist.NaoConforme, rotulo: 'Não conforme', cor: 'var(--bad, #c93b31)' },
  { valor: StatusItemChecklist.NaoAplicavel, rotulo: 'Não aplicável', cor: 'var(--napp, #7c8985)' },
];

export interface SeletorStatusItemChecklistProps {
  value: StatusItemChecklist | null;
  onChange: (valor: StatusItemChecklist) => void;
  disabled?: boolean;
}

export function SeletorStatusItemChecklist({ value, onChange, disabled }: SeletorStatusItemChecklistProps) {
  return (
    <div style={{ display: 'flex', gap: 14 }} role="radiogroup">
      {OPCOES.map((opcao) => (
        <label key={opcao.valor} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
          <input
            type="radio"
            checked={value === opcao.valor}
            disabled={disabled}
            onChange={() => onChange(opcao.valor)}
            style={{ accentColor: opcao.cor }}
          />
          <span style={{ fontSize: 10, color: opcao.cor }}>{opcao.rotulo}</span>
        </label>
      ))}
    </div>
  );
}
```

(Ajustar para usar `makeStyles`/tokens reais do projeto em vez de `style={{ }}` inline e variáveis
CSS inventadas (`var(--good, ...)`) — usar `designTokens` de `@ui`, mesmo padrão de cores usado em
`StatusChip`/`AppShell.tsx`, confirmando os nomes de token reais antes de fechar esta task.)

- [ ] **Step 2: Usar condicionalmente em `InspecaoDetalhePage.tsx`**

Localizar o `<Select>` atual de status por item (linha ~409-423 por levantamento anterior) e trocar
por:

```tsx
{inspecao?.tipoInspecao === TipoInspecao.Alojamento ? (
  <SeletorStatusItemChecklist
    value={resposta.statusItem}
    onChange={(v) => atualizarStatusItem(resposta.id, v)}
  />
) : (
  <Select /* ...select atual, sem mudanças... */ />
)}
```

Confirmar o nome real da função que hoje atualiza o status ao mudar o `<Select>` (provavelmente já
existe, ex. `atualizarStatusItem`/`aoMudarStatus`) e reaproveitar a mesma, só trocando o controle
visual que a dispara.

- [ ] **Step 3: Verificação manual**

Rodar o dev server, abrir uma inspeção de Alojamento: deve mostrar as 3 bolinhas. Abrir uma
inspeção de outro tipo (ex. Máquinas): deve continuar mostrando o `<Select>` de antes, sem
mudança visual nenhuma.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/components/inspecoes/SeletorStatusItemChecklist.tsx \
        src/AAHBRANT.SST.TeamsApp/src/pages/inspecoes/InspecaoDetalhePage.tsx
git commit -m "feat(alojamento): seletor de 3 estados por item, escopado a inspecao de alojamento

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Self-Review (feito ao escrever este plano)

1. **Cobertura do spec**: modelo de dados (Task 1), cadastro manual (Task 2), moradores (Task 3),
   configuração global (Task 4), endpoint atômico (Task 5), foto obrigatória (Task 6), navegação
   (Task 8), fluxo de clique (Task 9), seletor de 3 bolinhas escopado (Task 10). Cliente de API
   (Task 7) foi adicionado como task própria porque várias tasks de frontend dependem dele — não
   estava explícito no spec, mas é pré-requisito óbvio. Integração com G-RH fica de fora
   propositalmente (fora de escopo, seção 2 do spec).
2. **Placeholders**: nenhum "TBD"/"implementar depois" — os poucos pontos marcados como "confirmar
   antes de compilar" (valores exatos de enum `StatusInspecao`, nome do helper `fetchApi`, nome da
   função que atualiza status no front) são casos onde o levantamento não confirmou o dado exato
   por não ser o foco da pergunta feita ao agente explorador — são checagens de 1 minuto via grep,
   não decisões em aberto.
3. **Consistência de tipos**: `AlojamentoResumoDto`/`AlojamentoResumo` (back/front),
   `InspecaoAtualDto`/`InspecaoAtual` (back/front) usam os mesmos campos nos dois lados (id em
   camelCase no JSON, igual ao restante do projeto). `StatusItemChecklist` reaproveitado sem
   mudança do enum existente.
4. **Escopo**: fechado numa única entrega coerente (Alojamento em Inspeções). Sincronização G-RH,
   rollout do seletor para outros tipos de inspeção, e storage externo de fotos ficam
   explicitamente fora, cada um já registrado no spec como próximo passo separado.

---

**Plan complete and saved to `docs/superpowers/plans/2026-09-15-alojamento-inspecoes.md`.** Duas opções de execução:

**1. Subagent-Driven (recomendado)** — eu despacho um subagente novo por task, com revisão entre elas, iteração rápida.

**2. Execução Inline** — executo as tasks nesta mesma sessão via executing-plans, em lote com checkpoints pra revisão.

Qual prefere?
