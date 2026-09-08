# Módulo Uniforme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar um módulo "Uniforme" (catálogo, matriz por função, tamanho por trabalhador, entrega travada, estoque em grade por tamanho) ao app de SST, seguindo o mesmo padrão arquitetural já usado pelo módulo EPI.

**Architecture:** CQRS com MediatR (Commands/Queries) sobre EF Core (`SstDbContext`), controllers ASP.NET Core finos delegando ao `IMediator`, frontend React/Vite/Fluent UI consumindo via `lib/api.ts`. Seis entidades novas (`CatalogoUniforme`, `EstoqueUniforme`, `MovimentacaoEstoqueUniforme`, `MatrizUniformeFuncao`, `TrabalhadorTamanhoUniforme`, `EntregaUniforme`) espelham 1:1 as entidades já existentes do EPI (`CatalogoEpi`, `EstoqueEpi`, `MovimentacaoEstoqueEpi`, `MatrizEpiFuncao`, `EntregaEpi`), com a diferença de que o estoque é segmentado por Obra **e** por Tamanho (`(CatalogoUniformeId, ObraId, Tamanho)`), e o tamanho de cada peça fica registrado por trabalhador (`TrabalhadorTamanhoUniforme`), resolvido automaticamente no momento da entrega — nunca escolhido manualmente.

**Tech Stack:** .NET 8, EF Core (SQL Server / InMemory em teste), MediatR, FluentValidation, xUnit, React + Vite + Fluent UI v9, TypeScript.

**Spec:** [docs/superpowers/specs/2026-09-07-modulo-uniforme-design.md](../specs/2026-09-07-modulo-uniforme-design.md)

## Global Constraints

- Todas as entidades novas herdam de `AuditableEntity` (`Id`, `CreatedAtUtc/By`, `UpdatedAtUtc/By`, `Origem`, `Ativo`, `RowVersion`) — mesma base de `CatalogoEpi`/`EntregaEpi`/etc.
- Toda entidade nova recebe `builder.Property(x => x.RowVersion).IsRowVersion()` na configuração EF **e** `builder.HasQueryFilter(x => x.Ativo)` — sem coluna varbinary legada, então `IsRowVersion()` já gera a coluna `rowversion` corretamente na primeira migration (nenhum gotcha de `AlterColumn` a evitar aqui, só em migrations que tocam colunas já existentes).
- `CatalogoUniforme` e `MatrizUniformeFuncao` são **globais** (sem `ObraId`) — só `EstoqueUniforme` é segmentado por Obra, mesmo padrão de `CatalogoEpi`/`EstoqueEpi`.
- Nenhuma entidade nova entra na lista de filtro RBAC Camada 3 (`SstDbContext.OnModelCreating`, bloco `Dds`/`Inspecao`/`Acidente`/`Pgr`/`Atividade`/`Setor`/`Trabalhador`/`AreaSst`) — `EstoqueEpi` também não está nessa lista hoje, então não adicionar `EstoqueUniforme` mantém paridade exata com o EPI (não é uma melhoria a fazer aqui, é consistência deliberada).
- Sem código de barras, sem foto de item, sem fluxo de devolução — decisões explícitas do brainstorming (ver spec, seção "Fora de escopo").
- Autorização via `[Authorize(Policy = "uniforme:ver"|"uniforme:criar"|"uniforme:editar")]` nos controllers próprios de Uniforme; endpoints que são sub-recurso de Função/Trabalhador usam as políticas já existentes desses módulos (`organizacional:*`, `trabalhador:*`) — nunca uma política nova para um sub-recurso que já pertence a outro módulo.
- Toda migration roda `dotnet ef migrations add <Nome> --project ../AAHBRANT.SST.Infrastructure --startup-project . --output-dir Persistencia/Migrations` a partir de `src/AAHBRANT.SST.Api`.
- Trabalhar num worktree isolado (branch nova a partir de `master`) — nunca direto na pasta principal.

---

### Task 1: Entidades de domínio e enums

**Files:**
- Create: `src/AAHBRANT.SST.Domain/Entidades/Uniforme.cs`
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs` (adicionar ao final do arquivo)
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs:61-66` (adicionar 2 novas coleções de navegação)
- Test: nenhum teste de unidade dedicado (entidades são POCOs — a lógica real é testada nos handlers das Tasks 4-7; mesmo padrão do EPI, que não tem teste de domínio para `Epi.cs`)

**Interfaces:**
- Produces: as classes `CatalogoUniforme`, `EstoqueUniforme`, `MovimentacaoEstoqueUniforme`, `MatrizUniformeFuncao`, `TrabalhadorTamanhoUniforme`, `EntregaUniforme` (todas em `AAHBRANT.SST.Domain.Entidades`) e os enums `MotivoEntregaUniforme`, `TipoMovimentacaoEstoqueUniforme` (em `AAHBRANT.SST.Domain.Enums`) — usados por todas as tasks seguintes.

- [ ] **Step 1: Criar o arquivo de entidades**

Criar `src/AAHBRANT.SST.Domain/Entidades/Uniforme.cs`:

```csharp
using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Módulo Uniforme — mesmo padrão arquitetural do EPI (docs/superpowers/specs/2026-09-07-modulo-
// uniforme-design.md), com uma diferença central: uniforme tem tamanho (camisa P/M/G/GG, calça/
// bota por numeração). CatalogoUniforme representa só a peça (ex.: "Camisa"), sem tamanho embutido;
// o tamanho vira uma dimensão do estoque (grade por Obra+Tamanho) e do trabalhador (tamanho
// individual), nunca do catálogo.
public class CatalogoUniforme : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Categoria { get; set; }

    public ICollection<EstoqueUniforme> Estoques { get; set; } = new List<EstoqueUniforme>();
    public ICollection<EntregaUniforme> Entregas { get; set; } = new List<EntregaUniforme>();
}

// Grade de estoque: uma linha por (CatalogoUniformeId, ObraId, Tamanho) — Tamanho é texto livre
// porque a granularidade varia por peça (P/M/G/GG para camisa, numeração para calça/bota), sem
// enum fixo. Mesmo princípio de segmentação por Obra já usado em EstoqueEpi (Fase 3 do EPI).
public class EstoqueUniforme : AuditableEntity
{
    public Guid CatalogoUniformeId { get; set; }
    public CatalogoUniforme? CatalogoUniforme { get; set; }
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public string Tamanho { get; set; } = string.Empty;
    public int Saldo { get; set; }

    public ICollection<MovimentacaoEstoqueUniforme> Movimentacoes { get; set; } = new List<MovimentacaoEstoqueUniforme>();
}

// Ledger append-only de movimentações — mesmo papel de MovimentacaoEstoqueEpi. Sem
// DevolucaoEntrada: não há fluxo de devolução de uniforme neste módulo (fora de escopo, ver spec).
public class MovimentacaoEstoqueUniforme : AuditableEntity
{
    public Guid EstoqueUniformeId { get; set; }
    public EstoqueUniforme? EstoqueUniforme { get; set; }
    public TipoMovimentacaoEstoqueUniforme Tipo { get; set; }
    public int Quantidade { get; set; }
    public int SaldoResultante { get; set; }
    public Guid? EntregaUniformeId { get; set; }
    public EntregaUniforme? EntregaUniforme { get; set; }
    public string? Observacao { get; set; }
}

// Autorização por função — aponta só para a peça, sem fixar tamanho (o tamanho vem do trabalhador
// que está recebendo, resolvido em tempo de entrega). Mesmo formato de MatrizEpiFuncao.
public class MatrizUniformeFuncao : AuditableEntity
{
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }
    public Guid CatalogoUniformeId { get; set; }
    public CatalogoUniforme? CatalogoUniforme { get; set; }
}

// Tamanho de cada trabalhador, por peça — tabela própria (não campos fixos tipo TamanhoCamisa no
// cadastro do trabalhador) para que uma peça nova no catálogo não exija alterar o cadastro do
// trabalhador de novo (decisão do brainstorming, 2026-09-07).
public class TrabalhadorTamanhoUniforme : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid CatalogoUniformeId { get; set; }
    public CatalogoUniforme? CatalogoUniforme { get; set; }
    public string Tamanho { get; set; } = string.Empty;
}

// Registro de entrega — mais enxuto que EntregaEpi (sem VistoConsorcioResponsavel/NR-6, que são
// campos específicos da ficha oficial de EPI; sem DataDevolucao/QuantidadeDevolucao, pois não há
// fluxo de devolução de uniforme). Tamanho é um snapshot no momento da entrega (não referência viva
// a TrabalhadorTamanhoUniforme) — se o trabalhador trocar de tamanho depois, o histórico de
// entregas antigas não muda retroativamente.
public class EntregaUniforme : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid CatalogoUniformeId { get; set; }
    public CatalogoUniforme? CatalogoUniforme { get; set; }
    public string Tamanho { get; set; } = string.Empty;
    public int Quantidade { get; set; } = 1;
    public DateTime DataEntrega { get; set; }
    public MotivoEntregaUniforme MotivoTipo { get; set; }
    public string? Observacoes { get; set; }
}
```

- [ ] **Step 2: Adicionar os enums**

Ao final de `src/AAHBRANT.SST.Domain/Enums/Enums.cs` (depois da última linha existente, que hoje é o `enum CategoriaRequisitoLegal`), adicionar:

```csharp

// Módulo Uniforme (docs/superpowers/specs/2026-09-07-modulo-uniforme-design.md) — motivos da
// entrega, equivalente a MotivoEntregaEpi mas sem "Vencimento" (uniforme não tem CA/validade
// certificada) e com "Desgaste" no lugar de "Dano" (linguagem mais natural para uniforme).
public enum MotivoEntregaUniforme
{
    Inicial,
    Desgaste,
    Extravio,
    TrocaDeFuncao,
}

// Classifica cada linha do ledger MovimentacaoEstoqueUniforme. Sem DevolucaoEntrada (sem fluxo de
// devolução neste módulo) — equivalente reduzido de TipoMovimentacaoEstoqueEpi.
public enum TipoMovimentacaoEstoqueUniforme
{
    EntradaManual = 0,
    SaidaEntrega = 1,
    AjusteManual = 2,
}
```

- [ ] **Step 3: Adicionar as coleções de navegação em Trabalhador**

Em `src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs`, na linha 63 (logo após `EntregasEpi`), adicionar:

```csharp
    public ICollection<EntregaEpi> EntregasEpi { get; set; } = new List<EntregaEpi>();
    public ICollection<EntregaUniforme> EntregasUniforme { get; set; } = new List<EntregaUniforme>();
    public ICollection<TrabalhadorTamanhoUniforme> TamanhosUniforme { get; set; } = new List<TrabalhadorTamanhoUniforme>();
```

(A primeira linha já existe — só confirme que ficou exatamente assim, com as duas linhas novas logo abaixo dela, antes de `RiscosExpostos`.)

- [ ] **Step 4: Compilar**

Run: `dotnet build src/AAHBRANT.SST.Domain/AAHBRANT.SST.Domain.csproj`
Expected: build succeeded, 0 erros.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Uniforme.cs src/AAHBRANT.SST.Domain/Enums/Enums.cs src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs
git commit -m "feat: entidades de domínio do módulo Uniforme"
```

---

### Task 2: Configuração EF Core, DbContext e migration

**Files:**
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/UniformeConfiguracoes.cs`
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs:34` (adicionar 6 `DbSet` logo após a linha de `MovimentacoesEstoqueEpi`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs:42` (adicionar as 6 implementações `DbSet<X> Xs => Set<X>();` logo após a linha de `MovimentacoesEstoqueEpi`)
- Create: migration gerada por `dotnet ef migrations add`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests` (rodar a suíte completa — não precisa de teste novo, só confirmar que nada quebrou)

**Interfaces:**
- Consumes: as 6 entidades e 2 enums da Task 1.
- Produces: `IAppDbContext.CatalogoUniformes`, `.EstoquesUniforme`, `.MovimentacoesEstoqueUniforme`, `.MatrizUniformeFuncoes`, `.TrabalhadorTamanhosUniforme`, `.EntregasUniforme` (todos `DbSet<T>`) — usados por todas as tasks de Application seguintes.

- [ ] **Step 1: Criar as configurações EF Core**

Criar `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/UniformeConfiguracoes.cs`:

```csharp
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class CatalogoUniformeConfiguracao : IEntityTypeConfiguration<CatalogoUniforme>
{
    public void Configure(EntityTypeBuilder<CatalogoUniforme> builder)
    {
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Categoria).HasMaxLength(100);
        builder.HasQueryFilter(c => c.Ativo);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class EstoqueUniformeConfiguracao : IEntityTypeConfiguration<EstoqueUniforme>
{
    public void Configure(EntityTypeBuilder<EstoqueUniforme> builder)
    {
        builder.Property(e => e.Tamanho).IsRequired().HasMaxLength(20);
        builder.HasOne(e => e.CatalogoUniforme).WithMany(c => c.Estoques)
            .HasForeignKey(e => e.CatalogoUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Obra).WithMany()
            .HasForeignKey(e => e.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CatalogoUniformeId, e.ObraId, e.Tamanho }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class MovimentacaoEstoqueUniformeConfiguracao : IEntityTypeConfiguration<MovimentacaoEstoqueUniforme>
{
    public void Configure(EntityTypeBuilder<MovimentacaoEstoqueUniforme> builder)
    {
        builder.Property(m => m.Observacao).HasMaxLength(300);
        builder.HasOne(m => m.EstoqueUniforme).WithMany(e => e.Movimentacoes)
            .HasForeignKey(m => m.EstoqueUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.EntregaUniforme).WithMany()
            .HasForeignKey(m => m.EntregaUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.EstoqueUniformeId, m.CreatedAtUtc });
        builder.HasQueryFilter(m => m.Ativo);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}

public class MatrizUniformeFuncaoConfiguracao : IEntityTypeConfiguration<MatrizUniformeFuncao>
{
    public void Configure(EntityTypeBuilder<MatrizUniformeFuncao> builder)
    {
        builder.HasOne(m => m.Funcao).WithMany()
            .HasForeignKey(m => m.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.CatalogoUniforme).WithMany()
            .HasForeignKey(m => m.CatalogoUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.FuncaoId, m.CatalogoUniformeId }).IsUnique();
        builder.HasQueryFilter(m => m.Ativo);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}

public class TrabalhadorTamanhoUniformeConfiguracao : IEntityTypeConfiguration<TrabalhadorTamanhoUniforme>
{
    public void Configure(EntityTypeBuilder<TrabalhadorTamanhoUniforme> builder)
    {
        builder.Property(t => t.Tamanho).IsRequired().HasMaxLength(20);
        builder.HasOne(t => t.Trabalhador).WithMany(t => t.TamanhosUniforme)
            .HasForeignKey(t => t.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.CatalogoUniforme).WithMany()
            .HasForeignKey(t => t.CatalogoUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.TrabalhadorId, t.CatalogoUniformeId }).IsUnique();
        builder.HasQueryFilter(t => t.Ativo);
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}

public class EntregaUniformeConfiguracao : IEntityTypeConfiguration<EntregaUniforme>
{
    public void Configure(EntityTypeBuilder<EntregaUniforme> builder)
    {
        builder.Property(e => e.Tamanho).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Observacoes).HasMaxLength(300);
        builder.HasOne(e => e.Trabalhador).WithMany(t => t.EntregasUniforme)
            .HasForeignKey(e => e.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CatalogoUniforme).WithMany(c => c.Entregas)
            .HasForeignKey(e => e.CatalogoUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TrabalhadorId, e.DataEntrega });
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 2: Adicionar os DbSets em IAppDbContext**

Em `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`, logo após a linha `DbSet<MovimentacaoEstoqueEpi> MovimentacoesEstoqueEpi { get; }` (linha 34), adicionar:

```csharp
    DbSet<CatalogoUniforme> CatalogoUniformes { get; }
    DbSet<EstoqueUniforme> EstoquesUniforme { get; }
    DbSet<MovimentacaoEstoqueUniforme> MovimentacoesEstoqueUniforme { get; }
    DbSet<MatrizUniformeFuncao> MatrizUniformeFuncoes { get; }
    DbSet<TrabalhadorTamanhoUniforme> TrabalhadorTamanhosUniforme { get; }
    DbSet<EntregaUniforme> EntregasUniforme { get; }
```

- [ ] **Step 3: Implementar os DbSets em SstDbContext**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`, logo após a linha `public DbSet<MovimentacaoEstoqueEpi> MovimentacoesEstoqueEpi => Set<MovimentacaoEstoqueEpi>();` (linha 42), adicionar:

```csharp
    public DbSet<CatalogoUniforme> CatalogoUniformes => Set<CatalogoUniforme>();
    public DbSet<EstoqueUniforme> EstoquesUniforme => Set<EstoqueUniforme>();
    public DbSet<MovimentacaoEstoqueUniforme> MovimentacoesEstoqueUniforme => Set<MovimentacaoEstoqueUniforme>();
    public DbSet<MatrizUniformeFuncao> MatrizUniformeFuncoes => Set<MatrizUniformeFuncao>();
    public DbSet<TrabalhadorTamanhoUniforme> TrabalhadorTamanhosUniforme => Set<TrabalhadorTamanhoUniforme>();
    public DbSet<EntregaUniforme> EntregasUniforme => Set<EntregaUniforme>();
```

- [ ] **Step 4: Compilar**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure/AAHBRANT.SST.Infrastructure.csproj`
Expected: build succeeded, 0 erros. As 6 classes de configuração são descobertas automaticamente por `modelBuilder.ApplyConfigurationsFromAssembly(typeof(SstDbContext).Assembly)` (linha 127 de `SstDbContext.cs`, mesmo mecanismo que já aplica as configurações do EPI) — nenhum registro manual adicional é necessário.

- [ ] **Step 5: Gerar a migration**

Run (a partir de `src/AAHBRANT.SST.Api`):
```bash
cd src/AAHBRANT.SST.Api
dotnet ef migrations add CriarModuloUniforme --project ../AAHBRANT.SST.Infrastructure --startup-project . --output-dir Persistencia/Migrations
```
Expected: gera um novo arquivo de migration criando as 6 tabelas (`CatalogoUniformes`, `EstoquesUniforme`, `MovimentacoesEstoqueUniforme`, `MatrizUniformeFuncoes`, `TrabalhadorTamanhosUniforme`, `EntregasUniforme`) com colunas `rowversion` nativas (sem `AlterColumn` — são tabelas novas, não há coluna legada a corrigir).

- [ ] **Step 6: Revisar a migration gerada**

Abrir o arquivo de migration gerado e confirmar visualmente que:
- As 6 tabelas usam `rowversion` (não `varbinary(max)`) na coluna `RowVersion`.
- Os 3 índices únicos existem: `(CatalogoUniformeId, ObraId, Tamanho)` em `EstoquesUniforme`, `(FuncaoId, CatalogoUniformeId)` em `MatrizUniformeFuncoes`, `(TrabalhadorId, CatalogoUniformeId)` em `TrabalhadorTamanhosUniforme`.
- Nenhum `AlterColumn` aparece (só `CreateTable`) — se aparecer, pare e revise antes de continuar (gotcha de deploy documentado em `ONBOARDING.md` §7, item 6).

- [ ] **Step 7: Rodar a suíte de testes de Infrastructure**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests`
Expected: todos os testes passam (nenhuma regressão nas configurações existentes).

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/UniformeConfiguracoes.cs src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat: configuração EF Core e migration do módulo Uniforme"
```

---

### Task 3: Catálogo de Uniforme (CQRS + Controller + RBAC)

**Files:**
- Create: `src/AAHBRANT.SST.Application/CatalogosUniforme/CatalogoUniformeDto.cs`
- Create: `src/AAHBRANT.SST.Application/CatalogosUniforme/Commands/CriarCatalogoUniformeCommand.cs`
- Create: `src/AAHBRANT.SST.Application/CatalogosUniforme/Commands/AtualizarCatalogoUniformeCommand.cs`
- Create: `src/AAHBRANT.SST.Application/CatalogosUniforme/Commands/ExcluirCatalogoUniformeCommand.cs`
- Create: `src/AAHBRANT.SST.Application/CatalogosUniforme/Queries/ListarCatalogosUniformeQuery.cs`
- Create: `src/AAHBRANT.SST.Application/CatalogosUniforme/Queries/ObterCatalogoUniformePorIdQuery.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/CatalogosUniformeController.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs:81` (adicionar 3 linhas de permissão logo após o bloco `epi:*`)

**Interfaces:**
- Consumes: `IAppDbContext.CatalogoUniformes` (Task 2).
- Produces: `CatalogoUniformeDto(Guid Id, string Nome, string? Categoria)` — consumido pela Task 4 (Matriz), Task 5 (Tamanhos), Task 7 (Entrega) e pelo frontend (Task 8).

- [ ] **Step 1: DTO**

Criar `src/AAHBRANT.SST.Application/CatalogosUniforme/CatalogoUniformeDto.cs`:

```csharp
namespace AAHBRANT.SST.Application.CatalogosUniforme;

public record CatalogoUniformeDto(Guid Id, string Nome, string? Categoria);
```

- [ ] **Step 2: Criar**

Criar `src/AAHBRANT.SST.Application/CatalogosUniforme/Commands/CriarCatalogoUniformeCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Commands;

public record CriarCatalogoUniformeCommand(string Nome, string? Categoria) : IRequest<Guid>;

public class CriarCatalogoUniformeCommandValidator : AbstractValidator<CriarCatalogoUniformeCommand>
{
    public CriarCatalogoUniformeCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Categoria).MaximumLength(100);
    }
}

public class CriarCatalogoUniformeCommandHandler : IRequestHandler<CriarCatalogoUniformeCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCatalogoUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCatalogoUniformeCommand request, CancellationToken ct)
    {
        var item = new Domain.Entidades.CatalogoUniforme { Nome = request.Nome, Categoria = request.Categoria };
        _db.CatalogoUniformes.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
```

- [ ] **Step 3: Atualizar**

Criar `src/AAHBRANT.SST.Application/CatalogosUniforme/Commands/AtualizarCatalogoUniformeCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Commands;

public record AtualizarCatalogoUniformeCommand(Guid Id, string Nome, string? Categoria) : IRequest;

public class AtualizarCatalogoUniformeCommandValidator : AbstractValidator<AtualizarCatalogoUniformeCommand>
{
    public AtualizarCatalogoUniformeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Categoria).MaximumLength(100);
    }
}

public class AtualizarCatalogoUniformeCommandHandler : IRequestHandler<AtualizarCatalogoUniformeCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCatalogoUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCatalogoUniformeCommand request, CancellationToken ct)
    {
        var item = await _db.CatalogoUniformes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Peça de uniforme não encontrada.");
        item.Nome = request.Nome;
        item.Categoria = request.Categoria;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Excluir**

Criar `src/AAHBRANT.SST.Application/CatalogosUniforme/Commands/ExcluirCatalogoUniformeCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Commands;

public record ExcluirCatalogoUniformeCommand(Guid Id) : IRequest;

public class ExcluirCatalogoUniformeCommandHandler : IRequestHandler<ExcluirCatalogoUniformeCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirCatalogoUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCatalogoUniformeCommand request, CancellationToken ct)
    {
        var item = await _db.CatalogoUniformes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Peça de uniforme não encontrada.");
        item.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Listar e Obter por Id**

Criar `src/AAHBRANT.SST.Application/CatalogosUniforme/Queries/ListarCatalogosUniformeQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Queries;

public record ListarCatalogosUniformeQuery : IRequest<List<CatalogoUniformeDto>>;

public class ListarCatalogosUniformeQueryHandler : IRequestHandler<ListarCatalogosUniformeQuery, List<CatalogoUniformeDto>>
{
    private readonly IAppDbContext _db;
    public ListarCatalogosUniformeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoUniformeDto>> Handle(ListarCatalogosUniformeQuery request, CancellationToken ct)
        => await _db.CatalogoUniformes
            .OrderBy(x => x.Nome)
            .Select(x => new CatalogoUniformeDto(x.Id, x.Nome, x.Categoria))
            .ToListAsync(ct);
}
```

Criar `src/AAHBRANT.SST.Application/CatalogosUniforme/Queries/ObterCatalogoUniformePorIdQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Queries;

public record ObterCatalogoUniformePorIdQuery(Guid Id) : IRequest<CatalogoUniformeDto?>;

public class ObterCatalogoUniformePorIdQueryHandler : IRequestHandler<ObterCatalogoUniformePorIdQuery, CatalogoUniformeDto?>
{
    private readonly IAppDbContext _db;
    public ObterCatalogoUniformePorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<CatalogoUniformeDto?> Handle(ObterCatalogoUniformePorIdQuery request, CancellationToken ct)
        => await _db.CatalogoUniformes
            .Where(x => x.Id == request.Id)
            .Select(x => new CatalogoUniformeDto(x.Id, x.Nome, x.Categoria))
            .FirstOrDefaultAsync(ct);
}
```

- [ ] **Step 6: Controller**

Criar `src/AAHBRANT.SST.Api/Controllers/CatalogosUniformeController.cs`:

```csharp
using AAHBRANT.SST.Application.CatalogosUniforme.Commands;
using AAHBRANT.SST.Application.CatalogosUniforme.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogosUniformeController : ControllerBase
{
    private readonly IMediator _mediator;
    public CatalogosUniformeController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCatalogosUniformeQuery(), ct));

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var item = await _mediator.Send(new ObterCatalogoUniformePorIdQuery(id), ct);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Policy = "uniforme:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCatalogoUniformeCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "uniforme:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCatalogoUniformeCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "uniforme:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCatalogoUniformeCommand(id), ct);
        return NoContent();
    }
}
```

- [ ] **Step 7: RBAC — registrar as permissões**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs`, logo após a linha `("epi:editar", "Epi", "Editar", "Editar catálogo/entrega de EPI"),` (linha 81), adicionar:

```csharp
        ("uniforme:ver", "Uniforme", "Ver", "Ver catálogo/entregas de uniforme"),
        ("uniforme:criar", "Uniforme", "Criar", "Criar catálogo/entrega de uniforme"),
        ("uniforme:editar", "Uniforme", "Editar", "Editar catálogo/entrega de uniforme"),
```

- [ ] **Step 8: Compilar e rodar a suíte completa de Application/Api**

Run: `dotnet build && dotnet test tests/AAHBRANT.SST.Application.Tests tests/AAHBRANT.SST.Api.IntegrationTests`
Expected: build succeeded; todos os testes existentes continuam passando (nenhum teste novo nesta task — CRUD simples de catálogo não tem teste dedicado nem no EPI).

- [ ] **Step 9: Commit**

```bash
git add src/AAHBRANT.SST.Application/CatalogosUniforme src/AAHBRANT.SST.Api/Controllers/CatalogosUniformeController.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs
git commit -m "feat: catálogo de uniforme (CRUD + permissões)"
```

---

### Task 4: Matriz de Uniforme por Função

**Files:**
- Create: `src/AAHBRANT.SST.Application/Funcoes/Commands/DefinirMatrizUniformeFuncaoCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Funcoes/Queries/ListarUniformesPorFuncaoQuery.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/FuncoesController.cs:58` (adicionar 2 endpoints logo após `DefinirEpis`, e o `record DefinirUniformesRequest` no final do arquivo)
- Test: `tests/AAHBRANT.SST.Application.Tests/Funcoes/DefinirMatrizUniformeFuncaoCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.MatrizUniformeFuncoes`, `.Funcoes`, `CatalogoUniformeDto` (Task 3).
- Produces: `DefinirMatrizUniformeFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoUniformeIds)`, `ListarUniformesPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CatalogoUniformeDto>>` — consumidos pela Task 7 (Entrega, trava 1) e pelo frontend (Task 9, Task 11).

- [ ] **Step 1: Escrever o command (com validator e handler)**

Criar `src/AAHBRANT.SST.Application/Funcoes/Commands/DefinirMatrizUniformeFuncaoCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record DefinirMatrizUniformeFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoUniformeIds) : IRequest;

public class DefinirMatrizUniformeFuncaoCommandValidator : AbstractValidator<DefinirMatrizUniformeFuncaoCommand>
{
    public DefinirMatrizUniformeFuncaoCommandValidator()
    {
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.CatalogoUniformeIds).NotNull();
        RuleForEach(x => x.CatalogoUniformeIds).NotEmpty();
    }
}

public class DefinirMatrizUniformeFuncaoCommandHandler : IRequestHandler<DefinirMatrizUniformeFuncaoCommand>
{
    private readonly IAppDbContext _db;
    public DefinirMatrizUniformeFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirMatrizUniformeFuncaoCommand request, CancellationToken ct)
    {
        var funcaoExiste = await _db.Funcoes.AnyAsync(f => f.Id == request.FuncaoId, ct);
        if (!funcaoExiste)
            throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");

        // IgnoreQueryFilters: precisa enxergar também vínculos previamente desativados (Ativo=false)
        // para reativá-los em vez de tentar inserir duplicata e violar o índice único
        // (FuncaoId, CatalogoUniformeId). Mesmo padrão de DefinirMatrizEpiFuncaoCommand.
        var vinculosAtuais = await _db.MatrizUniformeFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == request.FuncaoId)
            .ToListAsync(ct);

        var idsDesejados = request.CatalogoUniformeIds.Distinct().ToHashSet();

        foreach (var vinculo in vinculosAtuais)
            vinculo.Ativo = idsDesejados.Contains(vinculo.CatalogoUniformeId);

        var idsExistentes = vinculosAtuais.Select(v => v.CatalogoUniformeId).ToHashSet();
        foreach (var catalogoUniformeId in idsDesejados.Where(id => !idsExistentes.Contains(id)))
        {
            _db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao
            {
                FuncaoId = request.FuncaoId,
                CatalogoUniformeId = catalogoUniformeId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Escrever a query**

Criar `src/AAHBRANT.SST.Application/Funcoes/Queries/ListarUniformesPorFuncaoQuery.cs`:

```csharp
using AAHBRANT.SST.Application.CatalogosUniforme;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ListarUniformesPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CatalogoUniformeDto>>;

public class ListarUniformesPorFuncaoQueryHandler : IRequestHandler<ListarUniformesPorFuncaoQuery, List<CatalogoUniformeDto>>
{
    private readonly IAppDbContext _db;
    public ListarUniformesPorFuncaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoUniformeDto>> Handle(ListarUniformesPorFuncaoQuery request, CancellationToken ct)
        => await _db.MatrizUniformeFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .OrderBy(m => m.CatalogoUniforme!.Nome)
            .Select(m => new CatalogoUniformeDto(m.CatalogoUniforme!.Id, m.CatalogoUniforme!.Nome, m.CatalogoUniforme!.Categoria))
            .ToListAsync(ct);
}
```

- [ ] **Step 3: Escrever os testes do handler**

Criar `tests/AAHBRANT.SST.Application.Tests/Funcoes/DefinirMatrizUniformeFuncaoCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class DefinirMatrizUniformeFuncaoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Funcao Funcao, CatalogoUniforme PecaA, CatalogoUniforme PecaB, CatalogoUniforme PecaC)> SemearAsync(IAppDbContext db)
    {
        var funcao = new Funcao { Nome = "Pedreiro" };
        var pecaA = new CatalogoUniforme { Nome = "Camisa" };
        var pecaB = new CatalogoUniforme { Nome = "Calça" };
        var pecaC = new CatalogoUniforme { Nome = "Bota" };

        db.Funcoes.Add(funcao);
        db.CatalogoUniformes.AddRange(pecaA, pecaB, pecaC);
        await db.SaveChangesAsync();

        return (funcao, pecaA, pecaB, pecaC);
    }

    [Fact]
    public async Task Handle_FuncaoSemVinculos_AdicionaTodasAsPecasInformadas()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemVinculos_AdicionaTodasAsPecasInformadas));
        var (funcao, pecaA, pecaB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizUniformeFuncaoCommandHandler(db);

        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id, pecaB.Id }), default);

        var vinculos = await db.MatrizUniformeFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.Contains(vinculos, v => v.CatalogoUniformeId == pecaA.Id);
        Assert.Contains(vinculos, v => v.CatalogoUniformeId == pecaB.Id);
    }

    [Fact]
    public async Task Handle_RemovePecaDaLista_DesativaVinculoExistente()
    {
        var db = CriarDb(nameof(Handle_RemovePecaDaLista_DesativaVinculoExistente));
        var (funcao, pecaA, pecaB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizUniformeFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id, pecaB.Id }), default);

        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id }), default);

        var vinculos = await db.MatrizUniformeFuncoes.IgnoreQueryFilters().Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.True(vinculos.Single(v => v.CatalogoUniformeId == pecaA.Id).Ativo);
        Assert.False(vinculos.Single(v => v.CatalogoUniformeId == pecaB.Id).Ativo);
    }

    [Fact]
    public async Task Handle_ReenviaPecaRemovidaAnteriormente_ReativaVinculoEmVezDeDuplicar()
    {
        var db = CriarDb(nameof(Handle_ReenviaPecaRemovidaAnteriormente_ReativaVinculoEmVezDeDuplicar));
        var (funcao, pecaA, pecaB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizUniformeFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id, pecaB.Id }), default);
        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id }), default);

        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id, pecaB.Id }), default);

        var vinculos = await db.MatrizUniformeFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.All(vinculos, v => Assert.True(v.Ativo));
    }

    [Fact]
    public async Task Handle_FuncaoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_FuncaoInexistente_LancaKeyNotFoundException));
        var handler = new DefinirMatrizUniformeFuncaoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new DefinirMatrizUniformeFuncaoCommand(Guid.NewGuid(), new List<Guid>()), default));
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~DefinirMatrizUniformeFuncaoCommandHandlerTests"`
Expected: 4 testes, todos aprovados.

- [ ] **Step 5: Adicionar os 2 endpoints em FuncoesController**

Em `src/AAHBRANT.SST.Api/Controllers/FuncoesController.cs`, logo após o método `DefinirEpis` (linha 66, antes de `ListarTreinamentosObrigatorios`), adicionar:

```csharp
    [Authorize(Policy = "organizacional:ver")]
    [HttpGet("{id:guid}/uniformes")]
    public async Task<IActionResult> ListarUniformes(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarUniformesPorFuncaoQuery(id), ct));

    [Authorize(Policy = "organizacional:editar")]
    [HttpPut("{id:guid}/uniformes")]
    public async Task<IActionResult> DefinirUniformes(Guid id, DefinirUniformesRequest request, CancellationToken ct)
    {
        await _mediator.Send(new DefinirMatrizUniformeFuncaoCommand(id, request.CatalogoUniformeIds), ct);
        return NoContent();
    }
```

E, no final do arquivo, logo após `public record DefinirTreinamentosObrigatoriosRequest(List<Guid> CursoTreinamentoIds);`, adicionar:

```csharp
public record DefinirUniformesRequest(List<Guid> CatalogoUniformeIds);
```

Adicionar também ao topo do arquivo (junto aos `using` já existentes) — o arquivo já tem `using AAHBRANT.SST.Application.Funcoes.Queries;` e `using AAHBRANT.SST.Application.Funcoes.Commands;`, que já cobrem os novos tipos (mesmo namespace); nenhum `using` novo é necessário.

- [ ] **Step 6: Compilar**

Run: `dotnet build`
Expected: build succeeded, 0 erros.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.Application/Funcoes/Commands/DefinirMatrizUniformeFuncaoCommand.cs src/AAHBRANT.SST.Application/Funcoes/Queries/ListarUniformesPorFuncaoQuery.cs src/AAHBRANT.SST.Api/Controllers/FuncoesController.cs tests/AAHBRANT.SST.Application.Tests/Funcoes/DefinirMatrizUniformeFuncaoCommandHandlerTests.cs
git commit -m "feat: matriz de uniforme por função"
```

---

### Task 5: Tamanho de Uniforme por Trabalhador

**Files:**
- Create: `src/AAHBRANT.SST.Application/Trabalhadores/TamanhoUniformeTrabalhadorDto.cs`
- Create: `src/AAHBRANT.SST.Application/Trabalhadores/Commands/DefinirTamanhosUniformeTrabalhadorCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Trabalhadores/Queries/ListarTamanhosUniformeTrabalhadorQuery.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs:96` (adicionar 2 endpoints logo após `CadastrarBiometriaLocal`, antes do fechamento da classe)
- Test: `tests/AAHBRANT.SST.Application.Tests/Trabalhadores/DefinirTamanhosUniformeTrabalhadorCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.TrabalhadorTamanhosUniforme`, `.Trabalhadores`, `CatalogoUniformeDto` (Task 3).
- Produces: `TamanhoUniformeTrabalhadorDto(Guid CatalogoUniformeId, string CatalogoUniformeNome, string Tamanho)`, `ItemTamanhoUniforme(Guid CatalogoUniformeId, string Tamanho)`, `DefinirTamanhosUniformeTrabalhadorCommand(Guid TrabalhadorId, List<ItemTamanhoUniforme> Itens)`, `ListarTamanhosUniformeTrabalhadorQuery(Guid TrabalhadorId) : IRequest<List<TamanhoUniformeTrabalhadorDto>>` — consumidos pela Task 7 (Entrega, trava 2) e pelo frontend (Task 9, Task 11).

- [ ] **Step 1: DTO**

Criar `src/AAHBRANT.SST.Application/Trabalhadores/TamanhoUniformeTrabalhadorDto.cs`:

```csharp
namespace AAHBRANT.SST.Application.Trabalhadores;

public record TamanhoUniformeTrabalhadorDto(Guid CatalogoUniformeId, string CatalogoUniformeNome, string Tamanho);
```

- [ ] **Step 2: Escrever o command (com validator e handler)**

Criar `src/AAHBRANT.SST.Application/Trabalhadores/Commands/DefinirTamanhosUniformeTrabalhadorCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record ItemTamanhoUniforme(Guid CatalogoUniformeId, string Tamanho);

public record DefinirTamanhosUniformeTrabalhadorCommand(Guid TrabalhadorId, List<ItemTamanhoUniforme> Itens) : IRequest;

public class DefinirTamanhosUniformeTrabalhadorCommandValidator : AbstractValidator<DefinirTamanhosUniformeTrabalhadorCommand>
{
    public DefinirTamanhosUniformeTrabalhadorCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Itens).NotNull();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.CatalogoUniformeId).NotEmpty();
            item.RuleFor(i => i.Tamanho).NotEmpty().MaximumLength(20);
        });
    }
}

public class DefinirTamanhosUniformeTrabalhadorCommandHandler : IRequestHandler<DefinirTamanhosUniformeTrabalhadorCommand>
{
    private readonly IAppDbContext _db;
    public DefinirTamanhosUniformeTrabalhadorCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirTamanhosUniformeTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhadorExiste = await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct);
        if (!trabalhadorExiste)
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        // Mesmo padrão de upsert-com-desativação de DefinirMatrizUniformeFuncaoCommand, com uma
        // diferença: aqui cada vínculo também carrega um valor (Tamanho) que pode mudar entre
        // duas chamadas, não só um vínculo booleano — reenviar a mesma peça com tamanho diferente
        // atualiza o tamanho do vínculo existente em vez de criar um novo.
        var vinculosAtuais = await _db.TrabalhadorTamanhosUniforme.IgnoreQueryFilters()
            .Where(t => t.TrabalhadorId == request.TrabalhadorId)
            .ToListAsync(ct);

        var desejados = request.Itens.ToDictionary(i => i.CatalogoUniformeId, i => i.Tamanho);

        foreach (var vinculo in vinculosAtuais)
        {
            if (desejados.TryGetValue(vinculo.CatalogoUniformeId, out var tamanho))
            {
                vinculo.Ativo = true;
                vinculo.Tamanho = tamanho;
            }
            else
            {
                vinculo.Ativo = false;
            }
        }

        var idsExistentes = vinculosAtuais.Select(v => v.CatalogoUniformeId).ToHashSet();
        foreach (var item in request.Itens.Where(i => !idsExistentes.Contains(i.CatalogoUniformeId)))
        {
            _db.TrabalhadorTamanhosUniforme.Add(new TrabalhadorTamanhoUniforme
            {
                TrabalhadorId = request.TrabalhadorId,
                CatalogoUniformeId = item.CatalogoUniformeId,
                Tamanho = item.Tamanho,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Escrever a query**

Criar `src/AAHBRANT.SST.Application/Trabalhadores/Queries/ListarTamanhosUniformeTrabalhadorQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public record ListarTamanhosUniformeTrabalhadorQuery(Guid TrabalhadorId) : IRequest<List<TamanhoUniformeTrabalhadorDto>>;

public class ListarTamanhosUniformeTrabalhadorQueryHandler : IRequestHandler<ListarTamanhosUniformeTrabalhadorQuery, List<TamanhoUniformeTrabalhadorDto>>
{
    private readonly IAppDbContext _db;
    public ListarTamanhosUniformeTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TamanhoUniformeTrabalhadorDto>> Handle(ListarTamanhosUniformeTrabalhadorQuery request, CancellationToken ct)
        => await _db.TrabalhadorTamanhosUniforme
            .Where(t => t.TrabalhadorId == request.TrabalhadorId)
            .OrderBy(t => t.CatalogoUniforme!.Nome)
            .Select(t => new TamanhoUniformeTrabalhadorDto(t.CatalogoUniformeId, t.CatalogoUniforme!.Nome, t.Tamanho))
            .ToListAsync(ct);
}
```

- [ ] **Step 4: Escrever os testes do handler**

Criar `tests/AAHBRANT.SST.Application.Tests/Trabalhadores/DefinirTamanhosUniformeTrabalhadorCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

public class DefinirTamanhosUniformeTrabalhadorCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Trabalhador Trabalhador, CatalogoUniforme Camisa, CatalogoUniforme Calca)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Nome = "Obra Teste" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var camisa = new CatalogoUniforme { Nome = "Camisa" };
        var calca = new CatalogoUniforme { Nome = "Calça" };
        var trabalhador = new Trabalhador
        {
            Nome = "Bruno Silva Santos",
            Matricula = "00427",
            Cpf = "12345678900",
            Obra = obra,
            Funcao = funcao,
            DataAdmissao = DateTime.UtcNow,
        };

        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.CatalogoUniformes.AddRange(camisa, calca);
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        return (trabalhador, camisa, calca);
    }

    [Fact]
    public async Task Handle_TrabalhadorSemTamanhos_AdicionaTodosOsItensInformados()
    {
        var db = CriarDb(nameof(Handle_TrabalhadorSemTamanhos_AdicionaTodosOsItensInformados));
        var (trabalhador, camisa, calca) = await SemearAsync(db);
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db);

        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme>
        {
            new(camisa.Id, "M"),
            new(calca.Id, "42"),
        }), default);

        var vinculos = await db.TrabalhadorTamanhosUniforme.Where(t => t.TrabalhadorId == trabalhador.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.Equal("M", vinculos.Single(v => v.CatalogoUniformeId == camisa.Id).Tamanho);
        Assert.Equal("42", vinculos.Single(v => v.CatalogoUniformeId == calca.Id).Tamanho);
    }

    [Fact]
    public async Task Handle_ReenviaMesmaPecaComTamanhoDiferente_AtualizaOTamanhoDoVinculoExistente()
    {
        var db = CriarDb(nameof(Handle_ReenviaMesmaPecaComTamanhoDiferente_AtualizaOTamanhoDoVinculoExistente));
        var (trabalhador, camisa, _) = await SemearAsync(db);
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db);
        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "M") }), default);

        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "G") }), default);

        var vinculos = await db.TrabalhadorTamanhosUniforme.Where(t => t.TrabalhadorId == trabalhador.Id).ToListAsync();
        Assert.Single(vinculos);
        Assert.Equal("G", vinculos[0].Tamanho);
    }

    [Fact]
    public async Task Handle_RemovePecaDaLista_DesativaVinculoExistente()
    {
        var db = CriarDb(nameof(Handle_RemovePecaDaLista_DesativaVinculoExistente));
        var (trabalhador, camisa, calca) = await SemearAsync(db);
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db);
        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme>
        {
            new(camisa.Id, "M"),
            new(calca.Id, "42"),
        }), default);

        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "M") }), default);

        var vinculos = await db.TrabalhadorTamanhosUniforme.IgnoreQueryFilters().Where(t => t.TrabalhadorId == trabalhador.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.True(vinculos.Single(v => v.CatalogoUniformeId == camisa.Id).Ativo);
        Assert.False(vinculos.Single(v => v.CatalogoUniformeId == calca.Id).Ativo);
    }

    [Fact]
    public async Task Handle_TrabalhadorInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_TrabalhadorInexistente_LancaKeyNotFoundException));
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(Guid.NewGuid(), new List<ItemTamanhoUniforme>()), default));
    }
}
```

- [ ] **Step 5: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~DefinirTamanhosUniformeTrabalhadorCommandHandlerTests"`
Expected: 4 testes, todos aprovados.

- [ ] **Step 6: Adicionar os 2 endpoints em TrabalhadoresController**

Em `src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs`, adicionar aos `using` do topo:

```csharp
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
```

E, logo após o método `CadastrarBiometriaLocal` (linha 126, antes do fechamento `}` da classe), adicionar:

```csharp
    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}/uniformes")]
    public async Task<IActionResult> ListarTamanhosUniforme(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTamanhosUniformeTrabalhadorQuery(id), ct));

    [Authorize(Policy = "trabalhador:editar")]
    [HttpPut("{id:guid}/uniformes")]
    public async Task<IActionResult> DefinirTamanhosUniforme(Guid id, DefinirTamanhosUniformeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new DefinirTamanhosUniformeTrabalhadorCommand(id, request.Itens), ct);
        return NoContent();
    }
```

E, no final do arquivo, logo após `public class AnexarFotoTrabalhadorRequestBody { ... }`, adicionar:

```csharp
public record DefinirTamanhosUniformeRequest(List<ItemTamanhoUniforme> Itens);
```

- [ ] **Step 7: Compilar**

Run: `dotnet build`
Expected: build succeeded, 0 erros.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Application/Trabalhadores/TamanhoUniformeTrabalhadorDto.cs src/AAHBRANT.SST.Application/Trabalhadores/Commands/DefinirTamanhosUniformeTrabalhadorCommand.cs src/AAHBRANT.SST.Application/Trabalhadores/Queries/ListarTamanhosUniformeTrabalhadorQuery.cs src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs tests/AAHBRANT.SST.Application.Tests/Trabalhadores/DefinirTamanhosUniformeTrabalhadorCommandHandlerTests.cs
git commit -m "feat: tamanho de uniforme por trabalhador"
```

---

### Task 6: Estoque de Uniforme (grade por Obra + Tamanho)

**Files:**
- Create: `src/AAHBRANT.SST.Application/EstoquesUniforme/EstoqueUniformeDto.cs`
- Create: `src/AAHBRANT.SST.Application/EstoquesUniforme/Commands/RegistrarEntradaEstoqueUniformeCommand.cs`
- Create: `src/AAHBRANT.SST.Application/EstoquesUniforme/Commands/AjustarEstoqueUniformeCommand.cs`
- Create: `src/AAHBRANT.SST.Application/EstoquesUniforme/Queries/ListarEstoqueUniformePorObraQuery.cs`
- Create: `src/AAHBRANT.SST.Application/EstoquesUniforme/Queries/ListarMovimentacoesEstoqueUniformeQuery.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/EstoquesUniformeController.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/EstoquesUniforme/RegistrarEntradaEstoqueUniformeCommandHandlerTests.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/EstoquesUniforme/AjustarEstoqueUniformeCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.EstoquesUniforme`, `.MovimentacoesEstoqueUniforme`, `.CatalogoUniformes`, `.Obras` (Tasks 1-3).
- Produces: `EstoqueUniformePorObraDto(Guid CatalogoUniformeId, string CatalogoUniformeNome, string Tamanho, int Saldo)`, `MovimentacaoEstoqueUniformeDto(...)` — consumidos pela Task 7 (Entrega, trava 3) e pelo frontend (Task 10).

- [ ] **Step 1: DTOs**

Criar `src/AAHBRANT.SST.Application/EstoquesUniforme/EstoqueUniformeDto.cs`:

```csharp
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EstoquesUniforme;

public record EstoqueUniformePorObraDto(
    Guid CatalogoUniformeId,
    string CatalogoUniformeNome,
    string Tamanho,
    int Saldo);

public record MovimentacaoEstoqueUniformeDto(
    Guid Id,
    TipoMovimentacaoEstoqueUniforme Tipo,
    int Quantidade,
    int SaldoResultante,
    DateTime CreatedAtUtc,
    string? Observacao,
    Guid? EntregaUniformeId);
```

- [ ] **Step 2: Entrada manual**

Criar `src/AAHBRANT.SST.Application/EstoquesUniforme/Commands/RegistrarEntradaEstoqueUniformeCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Commands;

// Entrada manual de estoque (compra/reposição) numa Obra, para um tamanho específico da peça —
// cria a linha de EstoqueUniforme se ainda não existir (primeiro tamanho desse item nessa Obra).
public record RegistrarEntradaEstoqueUniformeCommand(
    Guid CatalogoUniformeId,
    Guid ObraId,
    string Tamanho,
    int Quantidade,
    string? Observacao) : IRequest;

public class RegistrarEntradaEstoqueUniformeCommandValidator : AbstractValidator<RegistrarEntradaEstoqueUniformeCommand>
{
    public RegistrarEntradaEstoqueUniformeCommandValidator()
    {
        RuleFor(x => x.CatalogoUniformeId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Tamanho).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.Observacao).MaximumLength(300);
    }
}

public class RegistrarEntradaEstoqueUniformeCommandHandler : IRequestHandler<RegistrarEntradaEstoqueUniformeCommand>
{
    private readonly IAppDbContext _db;
    public RegistrarEntradaEstoqueUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarEntradaEstoqueUniformeCommand request, CancellationToken ct)
    {
        if (!await _db.CatalogoUniformes.AnyAsync(c => c.Id == request.CatalogoUniformeId, ct))
            throw new KeyNotFoundException($"Peça de uniforme {request.CatalogoUniformeId} não encontrada.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var estoque = await _db.EstoquesUniforme
            .FirstOrDefaultAsync(x => x.CatalogoUniformeId == request.CatalogoUniformeId
                && x.ObraId == request.ObraId
                && x.Tamanho == request.Tamanho, ct);
        if (estoque is null)
        {
            estoque = new EstoqueUniforme
            {
                CatalogoUniformeId = request.CatalogoUniformeId,
                ObraId = request.ObraId,
                Tamanho = request.Tamanho,
                Saldo = 0,
            };
            _db.EstoquesUniforme.Add(estoque);
        }

        estoque.Saldo += request.Quantidade;
        _db.MovimentacoesEstoqueUniforme.Add(new MovimentacaoEstoqueUniforme
        {
            EstoqueUniformeId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueUniforme.EntradaManual,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            Observacao = request.Observacao,
        });

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Ajuste de saldo**

Criar `src/AAHBRANT.SST.Application/EstoquesUniforme/Commands/AjustarEstoqueUniformeCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Commands;

// Correção manual de estoque (ex.: divergência de inventário) para um tamanho específico numa
// Obra — recebe o saldo final desejado (não um delta); o handler calcula a diferença e registra a
// movimentação com o delta com sinal (pode ser negativo). Observação é obrigatória, mesmo padrão
// de AjustarEstoqueEpiCommand.
public record AjustarEstoqueUniformeCommand(
    Guid CatalogoUniformeId,
    Guid ObraId,
    string Tamanho,
    int NovoSaldo,
    string Observacao) : IRequest;

public class AjustarEstoqueUniformeCommandValidator : AbstractValidator<AjustarEstoqueUniformeCommand>
{
    public AjustarEstoqueUniformeCommandValidator()
    {
        RuleFor(x => x.CatalogoUniformeId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Tamanho).NotEmpty().MaximumLength(20);
        RuleFor(x => x.NovoSaldo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Observacao).NotEmpty().MaximumLength(300);
    }
}

public class AjustarEstoqueUniformeCommandHandler : IRequestHandler<AjustarEstoqueUniformeCommand>
{
    private readonly IAppDbContext _db;
    public AjustarEstoqueUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AjustarEstoqueUniformeCommand request, CancellationToken ct)
    {
        if (!await _db.CatalogoUniformes.AnyAsync(c => c.Id == request.CatalogoUniformeId, ct))
            throw new KeyNotFoundException($"Peça de uniforme {request.CatalogoUniformeId} não encontrada.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var estoque = await _db.EstoquesUniforme
            .FirstOrDefaultAsync(x => x.CatalogoUniformeId == request.CatalogoUniformeId
                && x.ObraId == request.ObraId
                && x.Tamanho == request.Tamanho, ct);
        if (estoque is null)
        {
            estoque = new EstoqueUniforme
            {
                CatalogoUniformeId = request.CatalogoUniformeId,
                ObraId = request.ObraId,
                Tamanho = request.Tamanho,
                Saldo = 0,
            };
            _db.EstoquesUniforme.Add(estoque);
        }

        var delta = request.NovoSaldo - estoque.Saldo;
        if (delta != 0)
        {
            estoque.Saldo = request.NovoSaldo;
            _db.MovimentacoesEstoqueUniforme.Add(new MovimentacaoEstoqueUniforme
            {
                EstoqueUniformeId = estoque.Id,
                Tipo = TipoMovimentacaoEstoqueUniforme.AjusteManual,
                Quantidade = delta,
                SaldoResultante = estoque.Saldo,
                Observacao = request.Observacao,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Queries de listagem**

Criar `src/AAHBRANT.SST.Application/EstoquesUniforme/Queries/ListarEstoqueUniformePorObraQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Queries;

// Diferente de ListarEstoqueEpiPorObraQuery (que parte do catálogo fixo e mostra saldo=0 pros
// itens sem linha ainda): aqui não existe uma lista fechada de "todos os tamanhos possíveis" pra
// enumerar zerados — cada linha de EstoqueUniforme É um tamanho que já existe. Partir de
// EstoquesUniforme é o correto aqui.
public record ListarEstoqueUniformePorObraQuery(Guid ObraId) : IRequest<List<EstoqueUniformePorObraDto>>;

public class ListarEstoqueUniformePorObraQueryHandler : IRequestHandler<ListarEstoqueUniformePorObraQuery, List<EstoqueUniformePorObraDto>>
{
    private readonly IAppDbContext _db;
    public ListarEstoqueUniformePorObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EstoqueUniformePorObraDto>> Handle(ListarEstoqueUniformePorObraQuery request, CancellationToken ct)
        => await _db.EstoquesUniforme
            .Where(e => e.ObraId == request.ObraId)
            .OrderBy(e => e.CatalogoUniforme!.Nome).ThenBy(e => e.Tamanho)
            .Select(e => new EstoqueUniformePorObraDto(e.CatalogoUniformeId, e.CatalogoUniforme!.Nome, e.Tamanho, e.Saldo))
            .ToListAsync(ct);
}
```

Criar `src/AAHBRANT.SST.Application/EstoquesUniforme/Queries/ListarMovimentacoesEstoqueUniformeQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Queries;

public record ListarMovimentacoesEstoqueUniformeQuery(Guid CatalogoUniformeId, Guid ObraId, string Tamanho)
    : IRequest<List<MovimentacaoEstoqueUniformeDto>>;

public class ListarMovimentacoesEstoqueUniformeQueryHandler
    : IRequestHandler<ListarMovimentacoesEstoqueUniformeQuery, List<MovimentacaoEstoqueUniformeDto>>
{
    private readonly IAppDbContext _db;
    public ListarMovimentacoesEstoqueUniformeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<MovimentacaoEstoqueUniformeDto>> Handle(ListarMovimentacoesEstoqueUniformeQuery request, CancellationToken ct)
        => await _db.MovimentacoesEstoqueUniforme
            .Where(m => m.EstoqueUniforme!.CatalogoUniformeId == request.CatalogoUniformeId
                && m.EstoqueUniforme!.ObraId == request.ObraId
                && m.EstoqueUniforme!.Tamanho == request.Tamanho)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new MovimentacaoEstoqueUniformeDto(m.Id, m.Tipo, m.Quantidade, m.SaldoResultante, m.CreatedAtUtc, m.Observacao, m.EntregaUniformeId))
            .ToListAsync(ct);
}
```

- [ ] **Step 5: Escrever os testes dos 2 commands**

Criar `tests/AAHBRANT.SST.Application.Tests/EstoquesUniforme/RegistrarEntradaEstoqueUniformeCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EstoquesUniforme.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EstoquesUniforme;

public class RegistrarEntradaEstoqueUniformeCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(CatalogoUniforme Peca, Obra Obra)> SemearAsync(IAppDbContext db)
    {
        var peca = new CatalogoUniforme { Nome = "Camisa" };
        var obra = new Obra { Nome = "Obra Teste" };
        db.CatalogoUniformes.Add(peca);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        return (peca, obra);
    }

    [Fact]
    public async Task Handle_PrimeiraEntradaDoTamanho_CriaEstoqueComSaldoIgualAQuantidade()
    {
        var db = CriarDb(nameof(Handle_PrimeiraEntradaDoTamanho_CriaEstoqueComSaldoIgualAQuantidade));
        var (peca, obra) = await SemearAsync(db);
        var handler = new RegistrarEntradaEstoqueUniformeCommandHandler(db);

        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, "Compra inicial"), default);

        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        Assert.Equal(10, estoque.Saldo);
        var movimentacao = await db.MovimentacoesEstoqueUniforme.SingleAsync(m => m.EstoqueUniformeId == estoque.Id);
        Assert.Equal(TipoMovimentacaoEstoqueUniforme.EntradaManual, movimentacao.Tipo);
        Assert.Equal(10, movimentacao.SaldoResultante);
    }

    [Fact]
    public async Task Handle_EntradaAdicionalNoMesmoTamanho_SomaAoSaldoExistente()
    {
        var db = CriarDb(nameof(Handle_EntradaAdicionalNoMesmoTamanho_SomaAoSaldoExistente));
        var (peca, obra) = await SemearAsync(db);
        var handler = new RegistrarEntradaEstoqueUniformeCommandHandler(db);
        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, null), default);

        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 5, null), default);

        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        Assert.Equal(15, estoque.Saldo);
    }

    [Fact]
    public async Task Handle_TamanhoDiferenteDaMesmaPeca_CriaBucketSeparado()
    {
        var db = CriarDb(nameof(Handle_TamanhoDiferenteDaMesmaPeca_CriaBucketSeparado));
        var (peca, obra) = await SemearAsync(db);
        var handler = new RegistrarEntradaEstoqueUniformeCommandHandler(db);
        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, null), default);

        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "G", 7, null), default);

        var estoques = await db.EstoquesUniforme.Where(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id).ToListAsync();
        Assert.Equal(2, estoques.Count);
        Assert.Equal(10, estoques.Single(e => e.Tamanho == "M").Saldo);
        Assert.Equal(7, estoques.Single(e => e.Tamanho == "G").Saldo);
    }
}
```

Criar `tests/AAHBRANT.SST.Application.Tests/EstoquesUniforme/AjustarEstoqueUniformeCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EstoquesUniforme.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EstoquesUniforme;

public class AjustarEstoqueUniformeCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(CatalogoUniforme Peca, Obra Obra)> SemearAsync(IAppDbContext db)
    {
        var peca = new CatalogoUniforme { Nome = "Camisa" };
        var obra = new Obra { Nome = "Obra Teste" };
        db.CatalogoUniformes.Add(peca);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        return (peca, obra);
    }

    [Fact]
    public async Task Handle_AjusteParaBaixo_RegistraDeltaNegativo()
    {
        var db = CriarDb(nameof(Handle_AjusteParaBaixo_RegistraDeltaNegativo));
        var (peca, obra) = await SemearAsync(db);
        await new RegistrarEntradaEstoqueUniformeCommandHandler(db)
            .Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, null), default);
        var handler = new AjustarEstoqueUniformeCommandHandler(db);

        await handler.Handle(new AjustarEstoqueUniformeCommand(peca.Id, obra.Id, "M", 6, "Divergência de inventário"), default);

        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        Assert.Equal(6, estoque.Saldo);
        var movimentacao = await db.MovimentacoesEstoqueUniforme.OrderByDescending(m => m.CreatedAtUtc).FirstAsync(m => m.EstoqueUniformeId == estoque.Id);
        Assert.Equal(-4, movimentacao.Quantidade);
    }

    [Fact]
    public async Task Handle_SaldoIgualAoAtual_NaoRegistraMovimentacao()
    {
        var db = CriarDb(nameof(Handle_SaldoIgualAoAtual_NaoRegistraMovimentacao));
        var (peca, obra) = await SemearAsync(db);
        await new RegistrarEntradaEstoqueUniformeCommandHandler(db)
            .Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, null), default);
        var handler = new AjustarEstoqueUniformeCommandHandler(db);

        await handler.Handle(new AjustarEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, "Conferência sem divergência"), default);

        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        var totalMovimentacoes = await db.MovimentacoesEstoqueUniforme.CountAsync(m => m.EstoqueUniformeId == estoque.Id);
        Assert.Equal(1, totalMovimentacoes); // só a entrada manual inicial, o ajuste não gerou uma 2ª linha
    }
}
```

- [ ] **Step 6: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~EstoquesUniforme"`
Expected: 5 testes, todos aprovados.

- [ ] **Step 7: Controller**

Criar `src/AAHBRANT.SST.Api/Controllers/EstoquesUniformeController.cs`:

```csharp
using AAHBRANT.SST.Application.EstoquesUniforme.Commands;
using AAHBRANT.SST.Application.EstoquesUniforme.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EstoquesUniformeController : ControllerBase
{
    private readonly IMediator _mediator;
    public EstoquesUniformeController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("obra/{obraId:guid}")]
    public async Task<IActionResult> ListarPorObra(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEstoqueUniformePorObraQuery(obraId), ct));

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("obra/{obraId:guid}/peca/{catalogoUniformeId:guid}/tamanho/{tamanho}/movimentacoes")]
    public async Task<IActionResult> ListarMovimentacoes(Guid obraId, Guid catalogoUniformeId, string tamanho, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMovimentacoesEstoqueUniformeQuery(catalogoUniformeId, obraId, tamanho), ct));

    [Authorize(Policy = "uniforme:criar")]
    [HttpPost("entrada")]
    public async Task<IActionResult> RegistrarEntrada(RegistrarEntradaEstoqueUniformeCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "uniforme:editar")]
    [HttpPost("ajuste")]
    public async Task<IActionResult> Ajustar(AjustarEstoqueUniformeCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
```

- [ ] **Step 8: Compilar**

Run: `dotnet build`
Expected: build succeeded, 0 erros.

- [ ] **Step 9: Commit**

```bash
git add src/AAHBRANT.SST.Application/EstoquesUniforme src/AAHBRANT.SST.Api/Controllers/EstoquesUniformeController.cs tests/AAHBRANT.SST.Application.Tests/EstoquesUniforme
git commit -m "feat: estoque de uniforme (grade por obra e tamanho)"
```

---

### Task 7: Entrega de Uniforme (travada pela matriz + tamanho + estoque)

Esta é a task central do módulo: nenhuma entrega acontece sem que a peça esteja na matriz da
função do trabalhador, o trabalhador tenha um tamanho cadastrado para ela, e haja estoque
suficiente no bucket (peça+tamanho) na Obra do trabalhador — sem válvula de escape, mesmo
princípio já usado em `CriarEntregaEpiCommand` para CA vencido/estoque insuficiente.

**Files:**
- Create: `src/AAHBRANT.SST.Application/EntregasUniforme/EntregaUniformeDto.cs`
- Create: `src/AAHBRANT.SST.Application/EntregasUniforme/Commands/CriarEntregaUniformeCommand.cs`
- Create: `src/AAHBRANT.SST.Application/EntregasUniforme/Queries/ListarEntregasUniformeQuery.cs`
- Create: `src/AAHBRANT.SST.Application/EntregasUniforme/Queries/ObterEntregaUniformePorIdQuery.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/EntregasUniformeController.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/EntregasUniforme/CriarEntregaUniformeCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.EntregasUniforme`, `.MatrizUniformeFuncoes`, `.TrabalhadorTamanhosUniforme`, `.EstoquesUniforme`, `.MovimentacoesEstoqueUniforme`, `.Trabalhadores` (Tasks 1-6).
- Produces: `EntregaUniformeDto(...)`, `CriarEntregaUniformeCommand(...) : IRequest<Guid>` — consumidos pelo frontend (Task 11).

- [ ] **Step 1: DTO**

Criar `src/AAHBRANT.SST.Application/EntregasUniforme/EntregaUniformeDto.cs`:

```csharp
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EntregasUniforme;

public record EntregaUniformeDto(
    Guid Id,
    Guid TrabalhadorId,
    Guid CatalogoUniformeId,
    string Tamanho,
    int Quantidade,
    DateTime DataEntrega,
    MotivoEntregaUniforme MotivoTipo,
    string? Observacoes);
```

- [ ] **Step 2: Escrever o command (validator + handler com os 3 bloqueios)**

Criar `src/AAHBRANT.SST.Application/EntregasUniforme/Commands/CriarEntregaUniformeCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasUniforme.Commands;

public record CriarEntregaUniformeCommand(
    Guid TrabalhadorId,
    Guid CatalogoUniformeId,
    int Quantidade,
    DateTime DataEntrega,
    MotivoEntregaUniforme MotivoTipo,
    string? Observacoes) : IRequest<Guid>;

public class CriarEntregaUniformeCommandValidator : AbstractValidator<CriarEntregaUniformeCommand>
{
    public CriarEntregaUniformeCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CatalogoUniformeId).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.DataEntrega).NotEmpty();
        RuleFor(x => x.MotivoTipo).IsInEnum();
        RuleFor(x => x.Observacoes).MaximumLength(300);
    }
}

public class CriarEntregaUniformeCommandHandler : IRequestHandler<CriarEntregaUniformeCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarEntregaUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEntregaUniformeCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(x => x.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        // Trava 1: a peça precisa estar na matriz de uniforme da função do trabalhador — sem
        // válvula de escape (mesmo princípio de CriarEntregaEpiCommand para CA vencido/estoque).
        var itensDaMatriz = await _db.MatrizUniformeFuncoes
            .Where(m => m.FuncaoId == trabalhador.FuncaoId)
            .Select(m => m.CatalogoUniformeId)
            .ToListAsync(ct);
        if (itensDaMatriz.Count == 0)
            throw new InvalidOperationException("A matriz de uniforme da função deste trabalhador ainda não foi cadastrada — cadastre a matriz da função antes de registrar entregas.");
        if (!itensDaMatriz.Contains(request.CatalogoUniformeId))
            throw new InvalidOperationException("Esta peça não faz parte da matriz de uniforme da função deste trabalhador — ajuste a matriz da função se esta peça deveria estar nela.");

        // Trava 2: o trabalhador precisa ter um tamanho cadastrado para esta peça — o tamanho
        // nunca é escolhido manualmente na entrega, só resolvido a partir do cadastro.
        var tamanho = await _db.TrabalhadorTamanhosUniforme
            .Where(t => t.TrabalhadorId == request.TrabalhadorId && t.CatalogoUniformeId == request.CatalogoUniformeId)
            .Select(t => t.Tamanho)
            .FirstOrDefaultAsync(ct);
        if (tamanho is null)
            throw new InvalidOperationException("Este trabalhador não tem um tamanho cadastrado para esta peça — cadastre o tamanho antes de registrar a entrega.");

        // Trava 3: estoque do bucket (peça + tamanho) na Obra do trabalhador precisa ter saldo
        // suficiente — mesmo princípio de bloqueio de estoque insuficiente do EPI.
        var estoque = await _db.EstoquesUniforme
            .FirstOrDefaultAsync(x => x.CatalogoUniformeId == request.CatalogoUniformeId
                && x.ObraId == trabalhador.ObraId
                && x.Tamanho == tamanho, ct);
        var saldoAtual = estoque?.Saldo ?? 0;
        if (saldoAtual < request.Quantidade)
            throw new InvalidOperationException($"Estoque insuficiente para esta peça no tamanho {tamanho} nesta obra (saldo atual: {saldoAtual}).");

        var entrega = new EntregaUniforme
        {
            TrabalhadorId = request.TrabalhadorId,
            CatalogoUniformeId = request.CatalogoUniformeId,
            Tamanho = tamanho,
            Quantidade = request.Quantidade,
            DataEntrega = request.DataEntrega,
            MotivoTipo = request.MotivoTipo,
            Observacoes = request.Observacoes,
        };
        _db.EntregasUniforme.Add(entrega);

        estoque!.Saldo -= request.Quantidade;
        _db.MovimentacoesEstoqueUniforme.Add(new MovimentacaoEstoqueUniforme
        {
            EstoqueUniformeId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueUniforme.SaidaEntrega,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            EntregaUniformeId = entrega.Id,
        });

        await _db.SaveChangesAsync(ct);
        return entrega.Id;
    }
}
```

- [ ] **Step 3: Queries de listagem**

Criar `src/AAHBRANT.SST.Application/EntregasUniforme/Queries/ListarEntregasUniformeQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasUniforme.Queries;

public record ListarEntregasUniformeQuery(Guid? TrabalhadorId) : IRequest<List<EntregaUniformeDto>>;

public class ListarEntregasUniformeQueryHandler : IRequestHandler<ListarEntregasUniformeQuery, List<EntregaUniformeDto>>
{
    private readonly IAppDbContext _db;
    public ListarEntregasUniformeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EntregaUniformeDto>> Handle(ListarEntregasUniformeQuery request, CancellationToken ct)
        => await _db.EntregasUniforme
            .Where(e => request.TrabalhadorId == null || e.TrabalhadorId == request.TrabalhadorId)
            .OrderByDescending(e => e.DataEntrega)
            .Select(e => new EntregaUniformeDto(e.Id, e.TrabalhadorId, e.CatalogoUniformeId, e.Tamanho, e.Quantidade, e.DataEntrega, e.MotivoTipo, e.Observacoes))
            .ToListAsync(ct);
}
```

Criar `src/AAHBRANT.SST.Application/EntregasUniforme/Queries/ObterEntregaUniformePorIdQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasUniforme.Queries;

public record ObterEntregaUniformePorIdQuery(Guid Id) : IRequest<EntregaUniformeDto?>;

public class ObterEntregaUniformePorIdQueryHandler : IRequestHandler<ObterEntregaUniformePorIdQuery, EntregaUniformeDto?>
{
    private readonly IAppDbContext _db;
    public ObterEntregaUniformePorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EntregaUniformeDto?> Handle(ObterEntregaUniformePorIdQuery request, CancellationToken ct)
        => await _db.EntregasUniforme
            .Where(e => e.Id == request.Id)
            .Select(e => new EntregaUniformeDto(e.Id, e.TrabalhadorId, e.CatalogoUniformeId, e.Tamanho, e.Quantidade, e.DataEntrega, e.MotivoTipo, e.Observacoes))
            .FirstOrDefaultAsync(ct);
}
```

- [ ] **Step 4: Escrever os testes dos 3 bloqueios + caminho feliz**

Criar `tests/AAHBRANT.SST.Application.Tests/EntregasUniforme/CriarEntregaUniformeCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EntregasUniforme.Commands;
using AAHBRANT.SST.Application.EstoquesUniforme.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasUniforme;

public class CriarEntregaUniformeCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Trabalhador Trabalhador, CatalogoUniforme Camisa, Obra Obra, Funcao Funcao)> SemearBaseAsync(IAppDbContext db)
    {
        var obra = new Obra { Nome = "Obra Teste" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var camisa = new CatalogoUniforme { Nome = "Camisa" };
        var trabalhador = new Trabalhador
        {
            Nome = "Bruno Silva Santos",
            Matricula = "00427",
            Cpf = "12345678900",
            Obra = obra,
            Funcao = funcao,
            DataAdmissao = DateTime.UtcNow,
        };

        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.CatalogoUniformes.Add(camisa);
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        return (trabalhador, camisa, obra, funcao);
    }

    [Fact]
    public async Task Handle_MatrizDaFuncaoVazia_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_MatrizDaFuncaoVazia_LancaInvalidOperationException));
        var (trabalhador, camisa, _, _) = await SemearBaseAsync(db);
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 1, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null), default));
        Assert.Contains("matriz de uniforme da função", ex.Message);
    }

    [Fact]
    public async Task Handle_PecaForaDaMatrizDaFuncao_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_PecaForaDaMatrizDaFuncao_LancaInvalidOperationException));
        var (trabalhador, camisa, _, funcao) = await SemearBaseAsync(db);
        var calca = new CatalogoUniforme { Nome = "Calça" };
        db.CatalogoUniformes.Add(calca);
        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = calca.Id });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 1, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null), default));
        Assert.Contains("não faz parte da matriz", ex.Message);
    }

    [Fact]
    public async Task Handle_TrabalhadorSemTamanhoCadastrado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_TrabalhadorSemTamanhoCadastrado_LancaInvalidOperationException));
        var (trabalhador, camisa, _, funcao) = await SemearBaseAsync(db);
        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = camisa.Id });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 1, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null), default));
        Assert.Contains("tamanho cadastrado", ex.Message);
    }

    [Fact]
    public async Task Handle_EstoqueInsuficienteNoTamanhoResolvido_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_EstoqueInsuficienteNoTamanhoResolvido_LancaInvalidOperationException));
        var (trabalhador, camisa, obra, funcao) = await SemearBaseAsync(db);
        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = camisa.Id });
        db.TrabalhadorTamanhosUniforme.Add(new TrabalhadorTamanhoUniforme { TrabalhadorId = trabalhador.Id, CatalogoUniformeId = camisa.Id, Tamanho = "M" });
        // Estoque só existe no tamanho "G", não no "M" resolvido pelo cadastro do trabalhador —
        // confirma que a trava verifica o bucket (peça+tamanho) certo, não a peça em qualquer tamanho.
        await new RegistrarEntradaEstoqueUniformeCommandHandler(db)
            .Handle(new RegistrarEntradaEstoqueUniformeCommand(camisa.Id, obra.Id, "G", 10, null), default);
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 1, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null), default));
        Assert.Contains("Estoque insuficiente", ex.Message);
    }

    [Fact]
    public async Task Handle_TudoAutorizadoComEstoque_RegistraEntregaEBaixaEstoqueDoTamanhoCorreto()
    {
        var db = CriarDb(nameof(Handle_TudoAutorizadoComEstoque_RegistraEntregaEBaixaEstoqueDoTamanhoCorreto));
        var (trabalhador, camisa, obra, funcao) = await SemearBaseAsync(db);
        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = camisa.Id });
        db.TrabalhadorTamanhosUniforme.Add(new TrabalhadorTamanhoUniforme { TrabalhadorId = trabalhador.Id, CatalogoUniformeId = camisa.Id, Tamanho = "M" });
        await db.SaveChangesAsync();
        await new RegistrarEntradaEstoqueUniformeCommandHandler(db)
            .Handle(new RegistrarEntradaEstoqueUniformeCommand(camisa.Id, obra.Id, "M", 10, null), default);
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var id = await handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 2, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, "Kit inicial"), default);

        var entrega = await db.EntregasUniforme.SingleAsync(e => e.Id == id);
        Assert.Equal("M", entrega.Tamanho);
        Assert.Equal(2, entrega.Quantidade);
        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == camisa.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        Assert.Equal(8, estoque.Saldo);
        var movimentacao = await db.MovimentacoesEstoqueUniforme.OrderByDescending(m => m.CreatedAtUtc).FirstAsync(m => m.EstoqueUniformeId == estoque.Id);
        Assert.Equal(TipoMovimentacaoEstoqueUniforme.SaidaEntrega, movimentacao.Tipo);
        Assert.Equal(id, movimentacao.EntregaUniformeId);
    }
}
```

- [ ] **Step 5: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~CriarEntregaUniformeCommandHandlerTests"`
Expected: 5 testes, todos aprovados.

- [ ] **Step 6: Controller**

Criar `src/AAHBRANT.SST.Api/Controllers/EntregasUniformeController.cs`:

```csharp
using AAHBRANT.SST.Application.EntregasUniforme.Commands;
using AAHBRANT.SST.Application.EntregasUniforme.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EntregasUniformeController : ControllerBase
{
    private readonly IMediator _mediator;
    public EntregasUniformeController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEntregasUniformeQuery(trabalhadorId), ct));

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var entrega = await _mediator.Send(new ObterEntregaUniformePorIdQuery(id), ct);
        return entrega is null ? NotFound() : Ok(entrega);
    }

    [Authorize(Policy = "uniforme:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarEntregaUniformeCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }
}
```

- [ ] **Step 7: Compilar e rodar a suíte completa de Application**

Run: `dotnet build && dotnet test tests/AAHBRANT.SST.Application.Tests`
Expected: build succeeded; todos os testes (novos e antigos) passam.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Application/EntregasUniforme src/AAHBRANT.SST.Api/Controllers/EntregasUniformeController.cs tests/AAHBRANT.SST.Application.Tests/EntregasUniforme
git commit -m "feat: entrega de uniforme travada por matriz, tamanho e estoque"
```

---

### Task 8: Tipos TypeScript e métodos do client (`lib/api.ts`)

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts` (só adições — nenhuma linha existente é removida ou alterada)

**Interfaces:**
- Consumes: nenhuma (é a camada de tipagem/transporte; espelha os DTOs das Tasks 3-7).
- Produces: tipos `CatalogoUniforme`, `NovoCatalogoUniforme`, `AtualizarCatalogoUniforme`, `MotivoEntregaUniforme`, `motivoEntregaUniformeLabel`, `EntregaUniforme`, `NovaEntregaUniforme`, `EstoqueUniformePorObra`, `TipoMovimentacaoEstoqueUniforme`, `tipoMovimentacaoEstoqueUniformeLabel`, `MovimentacaoEstoqueUniforme`, `TamanhoUniformeTrabalhador`, `ItemTamanhoUniforme`, e os métodos `api.catalogosUniforme.*`, `api.entregasUniforme.*`, `api.estoquesUniforme.*`, `api.funcoes.listarUniformes/definirUniformes`, `api.trabalhadores.listarTamanhosUniforme/definirTamanhosUniforme` — consumidos pelas Tasks 9-11.

- [ ] **Step 1: Adicionar os tipos, logo após o bloco de `EntregaEpi`/`NovaEntregaEpi`/`AtualizarEntregaEpi` (linha 424 de `api.ts`)**

```typescript
export interface CatalogoUniforme {
  id: string;
  nome: string;
  categoria?: string | null;
}

export type NovoCatalogoUniforme = Omit<CatalogoUniforme, 'id'>;
export type AtualizarCatalogoUniforme = CatalogoUniforme;

export const MotivoEntregaUniforme = {
  Inicial: 0,
  Desgaste: 1,
  Extravio: 2,
  TrocaDeFuncao: 3,
} as const;

export const motivoEntregaUniformeLabel: Record<number, string> = {
  0: 'Entrega inicial',
  1: 'Desgaste',
  2: 'Extravio',
  3: 'Troca de função',
};

export interface EntregaUniforme {
  id: string;
  trabalhadorId: string;
  catalogoUniformeId: string;
  tamanho: string;
  quantidade: number;
  dataEntrega: string;
  motivoTipo: number;
  observacoes?: string | null;
}

export type NovaEntregaUniforme = Omit<EntregaUniforme, 'id' | 'tamanho'>;

export const TipoMovimentacaoEstoqueUniforme = {
  EntradaManual: 0,
  SaidaEntrega: 1,
  AjusteManual: 2,
} as const;

export const tipoMovimentacaoEstoqueUniformeLabel: Record<number, string> = {
  0: 'Entrada manual',
  1: 'Saída (entrega)',
  2: 'Ajuste manual',
};

export interface EstoqueUniformePorObra {
  catalogoUniformeId: string;
  catalogoUniformeNome: string;
  tamanho: string;
  saldo: number;
}

export interface MovimentacaoEstoqueUniforme {
  id: string;
  tipo: number;
  quantidade: number;
  saldoResultante: number;
  createdAtUtc: string;
  observacao?: string | null;
  entregaUniformeId?: string | null;
}

export interface TamanhoUniformeTrabalhador {
  catalogoUniformeId: string;
  catalogoUniformeNome: string;
  tamanho: string;
}

export interface ItemTamanhoUniforme {
  catalogoUniformeId: string;
  tamanho: string;
}
```

Nota: `NovaEntregaUniforme` omite `tamanho` porque o backend resolve o tamanho automaticamente a
partir do cadastro do trabalhador (`CriarEntregaUniformeCommand` não recebe `Tamanho` — ver Task
7) — o frontend nunca envia esse campo na criação.

- [ ] **Step 2: Adicionar os métodos `api.catalogosUniforme` e `api.entregasUniforme`, logo após o bloco `catalogosEpi` (linha 3086 de `api.ts`, antes de `estoquesEpi`)**

```typescript
  catalogosUniforme: {
    listar: () => request<CatalogoUniforme[]>('/api/catalogosuniforme'),
    criar: (item: NovoCatalogoUniforme) =>
      request<{ id: string }>('/api/catalogosuniforme', { method: 'POST', body: JSON.stringify(item) }),
    atualizar: (item: AtualizarCatalogoUniforme) =>
      request<void>(`/api/catalogosuniforme/${item.id}`, { method: 'PUT', body: JSON.stringify(item) }),
    excluir: (id: string) => request<void>(`/api/catalogosuniforme/${id}`, { method: 'DELETE' }),
  },
  entregasUniforme: {
    listar: (trabalhadorId?: string) =>
      request<EntregaUniforme[]>(`/api/entregasuniforme${trabalhadorId ? `?trabalhadorId=${trabalhadorId}` : ''}`),
    obterPorId: (id: string) => request<EntregaUniforme>(`/api/entregasuniforme/${id}`),
    criar: (dados: NovaEntregaUniforme) =>
      request<{ id: string }>('/api/entregasuniforme', { method: 'POST', body: JSON.stringify(dados) }),
  },
```

- [ ] **Step 3: Adicionar `api.estoquesUniforme`, logo após o bloco `estoquesEpi` inteiro**

```typescript
  estoquesUniforme: {
    listarPorObra: (obraId: string) => request<EstoqueUniformePorObra[]>(`/api/estoquesuniforme/obra/${obraId}`),
    listarMovimentacoes: (obraId: string, catalogoUniformeId: string, tamanho: string) =>
      request<MovimentacaoEstoqueUniforme[]>(
        `/api/estoquesuniforme/obra/${obraId}/peca/${catalogoUniformeId}/tamanho/${encodeURIComponent(tamanho)}/movimentacoes`,
      ),
    registrarEntrada: (dados: { catalogoUniformeId: string; obraId: string; tamanho: string; quantidade: number; observacao?: string | null }) =>
      request<void>('/api/estoquesuniforme/entrada', { method: 'POST', body: JSON.stringify(dados) }),
    ajustar: (dados: { catalogoUniformeId: string; obraId: string; tamanho: string; novoSaldo: number; observacao: string }) =>
      request<void>('/api/estoquesuniforme/ajuste', { method: 'POST', body: JSON.stringify(dados) }),
  },
```

- [ ] **Step 4: Adicionar `listarUniformes`/`definirUniformes` dentro do bloco `funcoes` já existente (logo após `definirEpis`, linha 2927)**

```typescript
    listarUniformes: (funcaoId: string) => request<CatalogoUniforme[]>(`/api/funcoes/${funcaoId}/uniformes`),
    definirUniformes: (funcaoId: string, catalogoUniformeIds: string[]) =>
      request<void>(`/api/funcoes/${funcaoId}/uniformes`, {
        method: 'PUT',
        body: JSON.stringify({ catalogoUniformeIds }),
      }),
```

- [ ] **Step 5: Adicionar `listarTamanhosUniforme`/`definirTamanhosUniforme` dentro do bloco `trabalhadores` já existente**

```typescript
    listarTamanhosUniforme: (id: string) => request<TamanhoUniformeTrabalhador[]>(`/api/trabalhadores/${id}/uniformes`),
    definirTamanhosUniforme: (id: string, itens: ItemTamanhoUniforme[]) =>
      request<void>(`/api/trabalhadores/${id}/uniformes`, { method: 'PUT', body: JSON.stringify({ itens }) }),
```

- [ ] **Step 6: Compilar o frontend**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: build limpo, sem erros de TypeScript.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat: tipos e client de API do módulo Uniforme no frontend"
```

---

### Task 9: Frontend — Catálogo, Matriz por Função e Tamanhos por Trabalhador

Cria o `UniformePage` (container de abas) já com 3 das 5 abas — Estoque (Task 10) e Entrega
(Task 11) entram depois, cada uma mantendo o módulo buildável e íntegro nesse ponto intermediário.

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/UniformePage.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/CatalogoUniformeTab.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/MatrizUniformeTab.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/TamanhosUniformeTab.tsx`

**Interfaces:**
- Consumes: `api.catalogosUniforme.*`, `api.funcoes.listarUniformes/definirUniformes`, `api.trabalhadores.listar/listarTamanhosUniforme/definirTamanhosUniforme`, `api.funcoes.listar` (Task 8).
- Produces: componente `UniformePage` — consumido pela Task 12 (wiring em `OperacaoPage.tsx`).

- [ ] **Step 1: `CatalogoUniformeTab.tsx`**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/CatalogoUniformeTab.tsx`:

```tsx
import { useEffect, useState } from 'react';
import {
  Button,
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
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoUniforme, type NovoCatalogoUniforme } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const itemVazio: NovoCatalogoUniforme = { nome: '', categoria: '' };

// Catálogo de Uniforme (peça em si, sem tamanho embutido — o tamanho é uma dimensão do estoque e
// do cadastro do trabalhador, ver EstoqueUniformeTab.tsx e TamanhosUniformeTab.tsx). Mesmo padrão
// de CatalogoTab.tsx (EPI), sem foto de item (não pedido para uniforme).
export function CatalogoUniformeTab() {
  const estilos = usePageStyles();
  const [itens, setItens] = useState<CatalogoUniforme[]>([]);
  const [novoItem, setNovoItem] = useState<NovoCatalogoUniforme>(itemVazio);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoUniforme | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setItens(await api.catalogosUniforme.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de uniforme.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosUniforme.criar(novoItem);
      setNovoItem(itemVazio);
      await carregar();
      sucessoToast('Peça de uniforme cadastrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar peça de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarEdicao(item: CatalogoUniforme) {
    setEdicaoId(item.id);
    setEdicao({ ...item });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosUniforme.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Peça de uniforme atualizada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar peça de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta peça do catálogo de uniforme? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosUniforme.excluir(id);
      await carregar();
      sucessoToast('Peça de uniforme excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir peça de uniforme.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Catálogo de Uniforme</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Peça</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col6}>
          <Field label="Nome (ex.: Camisa, Calça, Bota)">
            <Input value={novoItem.nome} onChange={(_, d) => setNovoItem({ ...novoItem, nome: d.value })} />
          </Field>
        </div>
        <div className={estilos.col6}>
          <Field label="Categoria (opcional)">
            <Input
              value={novoItem.categoria ?? ''}
              onChange={(_, d) => setNovoItem({ ...novoItem, categoria: d.value })}
            />
          </Field>
        </div>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !novoItem.nome.trim()}>
          Adicionar peça
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : itens.length === 0 ? (
        <EstadoVazio mensagem="Nenhuma peça cadastrada no catálogo de uniforme ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Categoria</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {itens.map((item) =>
            edicaoId === item.id && edicao ? (
              <TableRow key={item.id}>
                <TableCell>
                  <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
                </TableCell>
                <TableCell>
                  <Input
                    value={edicao.categoria ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, categoria: d.value })}
                  />
                </TableCell>
                <TableCell>
                  <Button appearance="subtle" icon={<Save24Regular />} onClick={salvarEdicao} disabled={carregando} aria-label="Salvar" />
                </TableCell>
              </TableRow>
            ) : (
              <TableRow key={item.id} onClick={() => iniciarEdicao(item)} style={{ cursor: 'pointer' }}>
                <TableCell>{item.nome}</TableCell>
                <TableCell>{item.categoria}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(e) => {
                      e.stopPropagation();
                      excluir(item.id);
                    }}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
            ),
          )}
        </TableBody>
      </Table>
      )}
      <Text size={200} style={{ display: 'block', marginTop: 8 }}>
        Clique em uma linha para editar. O estoque (por obra e tamanho) é controlado na aba Estoque.
      </Text>
    </div>
  );
}
```

- [ ] **Step 2: `MatrizUniformeTab.tsx`**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/MatrizUniformeTab.tsx`:

```tsx
import { Fragment, useEffect, useState } from 'react';
import {
  Checkbox,
  Button,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { api, type CatalogoUniforme, type Funcao } from '../../lib/api';
import { usePageStyles, useCheckboxChipStyles } from '../pageStyles';

// Matriz de uniforme por função — mesmo padrão de MatrizEpiTab.tsx. Define quais peças são
// obrigatórias para cada função; o tamanho de cada peça vem do cadastro do trabalhador (aba
// Tamanhos), nunca daqui.
export function MatrizUniformeTab() {
  const estilos = usePageStyles();
  const estilosChip = useCheckboxChipStyles();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [expandidoId, setExpandidoId] = useState<string | null>(null);
  const [vinculosSelecionados, setVinculosSelecionados] = useState<string[]>([]);
  const [salvandoMatriz, setSalvandoMatriz] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaFuncoes, listaItens] = await Promise.all([api.funcoes.listar(), api.catalogosUniforme.listar()]);
      setFuncoes(listaFuncoes);
      setItensCatalogo(listaItens);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function alternarExpansao(funcao: Funcao) {
    if (expandidoId === funcao.id) {
      setExpandidoId(null);
      return;
    }
    try {
      setErro(null);
      const vinculados = await api.funcoes.listarUniformes(funcao.id);
      setVinculosSelecionados(vinculados.map((i) => i.id));
      setExpandidoId(funcao.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de uniforme da função.');
    }
  }

  function alternarItem(catalogoUniformeId: string, marcado: boolean) {
    setVinculosSelecionados((atual) =>
      marcado ? [...atual, catalogoUniformeId] : atual.filter((id) => id !== catalogoUniformeId)
    );
  }

  async function salvarMatriz(funcaoId: string) {
    try {
      setSalvandoMatriz(true);
      setErro(null);
      await api.funcoes.definirUniformes(funcaoId, vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de uniforme.');
    } finally {
      setSalvandoMatriz(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Matriz de uniforme por função</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Text size={200}>
        Clique numa função para editar quais peças de uniforme são obrigatórias para ela. Novas funções são
        cadastradas em Operação → Pessoas → Funções.
      </Text>

      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>CBO</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {funcoes.map((funcao) => (
            <Fragment key={funcao.id}>
              <TableRow onClick={() => alternarExpansao(funcao)} style={{ cursor: 'pointer' }}>
                <TableCell>{funcao.nome}</TableCell>
                <TableCell>{funcao.cboCodigo}</TableCell>
                <TableCell>{funcao.descricao}</TableCell>
              </TableRow>
              {expandidoId === funcao.id && (
                <TableRow>
                  <TableCell colSpan={3}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: '8px 0' }}>
                      <Text weight="semibold">Matriz de uniforme — {funcao.nome}</Text>
                      {itensCatalogo.length === 0 ? (
                        <Text>Nenhuma peça cadastrada no catálogo ainda.</Text>
                      ) : (
                        itensCatalogo.map((item) => (
                          <Checkbox
                            key={item.id}
                            className={estilosChip.chip}
                            label={item.categoria ? `${item.nome} (${item.categoria})` : item.nome}
                            checked={vinculosSelecionados.includes(item.id)}
                            onChange={(_, d) => alternarItem(item.id, !!d.checked)}
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

- [ ] **Step 3: `TamanhosUniformeTab.tsx`**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/TamanhosUniformeTab.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { Button, Field, Input, Select, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoUniforme, type ItemTamanhoUniforme, type Trabalhador } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Tamanho de uniforme por trabalhador — nova tela (não tem equivalente no EPI). Busca um
// trabalhador, lista todas as peças do catálogo e permite registrar o tamanho de cada uma; peças
// deixadas em branco não geram vínculo (o trabalhador simplesmente não tem tamanho cadastrado
// para elas ainda, e a Entrega bloqueia até que seja cadastrado).
export function TamanhosUniformeTab() {
  const estilos = usePageStyles();
  const sucessoToast = useSucessoToast();
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [trabalhadorId, setTrabalhadorId] = useState('');
  const [tamanhos, setTamanhos] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const [listaTrabalhadores, listaItens] = await Promise.all([api.trabalhadores.listar(), api.catalogosUniforme.listar()]);
        setTrabalhadores(listaTrabalhadores);
        setItensCatalogo(listaItens);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar trabalhadores e catálogo de uniforme.');
      }
    })();
  }, []);

  useEffect(() => {
    if (!trabalhadorId) {
      setTamanhos({});
      return;
    }
    (async () => {
      try {
        setErro(null);
        const lista = await api.trabalhadores.listarTamanhosUniforme(trabalhadorId);
        const mapa: Record<string, string> = {};
        for (const item of lista) mapa[item.catalogoUniformeId] = item.tamanho;
        setTamanhos(mapa);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar tamanhos do trabalhador.');
      }
    })();
  }, [trabalhadorId]);

  async function salvar() {
    if (!trabalhadorId) return;
    try {
      setCarregando(true);
      setErro(null);
      const itens: ItemTamanhoUniforme[] = Object.entries(tamanhos)
        .filter(([, tamanho]) => tamanho.trim() !== '')
        .map(([catalogoUniformeId, tamanho]) => ({ catalogoUniformeId, tamanho: tamanho.trim() }));
      await api.trabalhadores.definirTamanhosUniforme(trabalhadorId, itens);
      sucessoToast('Tamanhos de uniforme salvos com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar tamanhos de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Tamanhos de uniforme por trabalhador</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Field label="Trabalhador">
        <Select value={trabalhadorId} onChange={(_, d) => setTrabalhadorId(d.value)}>
          <option value="">Selecione</option>
          {trabalhadores.map((t) => (
            <option key={t.id} value={t.id}>
              {t.nome} ({t.matricula})
            </option>
          ))}
        </Select>
      </Field>

      {trabalhadorId && (
        <>
          <Table noNativeElements style={{ marginTop: 12 }}>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Peça</TableHeaderCell>
                <TableHeaderCell>Tamanho</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {itensCatalogo.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>{item.nome}</TableCell>
                  <TableCell>
                    <Input
                      value={tamanhos[item.id] ?? ''}
                      onChange={(_, d) => setTamanhos({ ...tamanhos, [item.id]: d.value })}
                      placeholder="ex.: M, 42..."
                      style={{ width: 100 }}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <div className={estilos.formActions}>
            <Button appearance="primary" icon={<Save24Regular />} onClick={salvar} disabled={carregando}>
              Salvar tamanhos
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 4: `UniformePage.tsx` (parte 1 — 3 abas)**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/UniformePage.tsx`:

```tsx
import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { CatalogoUniformeTab } from './CatalogoUniformeTab';
import { MatrizUniformeTab } from './MatrizUniformeTab';
import { TamanhosUniformeTab } from './TamanhosUniformeTab';

type AbaUniforme = 'catalogo' | 'matriz' | 'tamanhos';

// Módulo Uniforme — mesmo padrão arquitetural do EPI (docs/superpowers/specs/2026-09-07-modulo-
// uniforme-design.md). Vive como aba dentro de Operação, ao lado de EPI/EPC (não item de 1º nível
// na sidebar — decisão do usuário, 2026-09-07). Entrega e Estoque entram nas Tasks 10/11.
export function UniformePage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaUniforme>('catalogo');
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            Uniforme
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaUniforme)}
        className={estilosAba.lista}
      >
        <Tab value="catalogo">Catálogo</Tab>
        <Tab value="matriz">Matriz por Função</Tab>
        <Tab value="tamanhos">Tamanhos</Tab>
      </TabList>

      {aba === 'catalogo' && <CatalogoUniformeTab />}
      {aba === 'matriz' && <MatrizUniformeTab />}
      {aba === 'tamanhos' && <TamanhosUniformeTab />}
    </div>
  );
}
```

- [ ] **Step 5: Compilar o frontend**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: build limpo, sem erros de TypeScript.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/uniforme
git commit -m "feat: telas de catálogo, matriz e tamanhos do módulo Uniforme"
```

---

### Task 10: Frontend — Estoque de Uniforme

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/EstoqueUniformeTab.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/UniformePage.tsx` (adicionar a aba)

**Interfaces:**
- Consumes: `api.estoquesUniforme.*`, `api.obras.listar`, `api.catalogosUniforme.listar` (Task 8).
- Produces: componente `EstoqueUniformeTab`, integrado ao `UniformePage`.

- [ ] **Step 1: `EstoqueUniformeTab.tsx`**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/EstoqueUniformeTab.tsx`:

```tsx
import { Fragment, useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
} from '@fluentui/react-components';
import {
  api,
  tipoMovimentacaoEstoqueUniformeLabel,
  type CatalogoUniforme,
  type EstoqueUniformePorObra,
  type MovimentacaoEstoqueUniforme,
  type Obra,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// Estoque de Uniforme — grade por Obra + Tamanho (mesmo princípio de segmentação por Obra do
// EstoqueTab.tsx do EPI, com uma dimensão a mais: o tamanho). Entrada é sempre manual (sem código
// de barras — decisão do brainstorming, 2026-09-07): escolhe a peça, digita o tamanho (cria um
// tamanho novo se ainda não existir nessa peça+obra) e a quantidade recebida.
export function EstoqueUniformeTab() {
  const estilos = usePageStyles();
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [saldos, setSaldos] = useState<EstoqueUniformePorObra[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  const [linhaSelecionada, setLinhaSelecionada] = useState<{ catalogoUniformeId: string; tamanho: string } | null>(null);
  const [movimentacoes, setMovimentacoes] = useState<MovimentacaoEstoqueUniforme[]>([]);

  const [entradaCatalogoUniformeId, setEntradaCatalogoUniformeId] = useState('');
  const [entradaTamanho, setEntradaTamanho] = useState('');
  const [entradaQuantidade, setEntradaQuantidade] = useState('1');
  const [entradaObservacao, setEntradaObservacao] = useState('');

  const [ajusteCatalogoUniformeId, setAjusteCatalogoUniformeId] = useState('');
  const [ajusteTamanho, setAjusteTamanho] = useState('');
  const [ajusteNovoSaldo, setAjusteNovoSaldo] = useState('0');
  const [ajusteObservacao, setAjusteObservacao] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const [listaObras, listaItens] = await Promise.all([api.obras.listar(), api.catalogosUniforme.listar()]);
        setObras(listaObras);
        setItensCatalogo(listaItens);
        if (listaObras.length > 0) setObraId(listaObras[0].id);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar obras e catálogo de uniforme.');
      }
    })();
  }, []);

  async function carregarSaldos() {
    if (!obraId) return;
    try {
      setErro(null);
      setSaldos(await api.estoquesUniforme.listarPorObra(obraId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar estoque da obra.');
    }
  }

  useEffect(() => {
    carregarSaldos();
    setLinhaSelecionada(null);
    setMovimentacoes([]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obraId]);

  async function alternarHistorico(catalogoUniformeId: string, tamanho: string) {
    if (linhaSelecionada?.catalogoUniformeId === catalogoUniformeId && linhaSelecionada.tamanho === tamanho) {
      setLinhaSelecionada(null);
      return;
    }
    try {
      setErro(null);
      setMovimentacoes(await api.estoquesUniforme.listarMovimentacoes(obraId, catalogoUniformeId, tamanho));
      setLinhaSelecionada({ catalogoUniformeId, tamanho });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar histórico de movimentações.');
    }
  }

  async function registrarEntrada() {
    if (!obraId || !entradaCatalogoUniformeId || !entradaTamanho.trim()) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.estoquesUniforme.registrarEntrada({
        obraId,
        catalogoUniformeId: entradaCatalogoUniformeId,
        tamanho: entradaTamanho.trim(),
        quantidade: Number(entradaQuantidade),
        observacao: entradaObservacao || null,
      });
      setEntradaCatalogoUniformeId('');
      setEntradaTamanho('');
      setEntradaQuantidade('1');
      setEntradaObservacao('');
      await carregarSaldos();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar entrada de estoque.');
    } finally {
      setCarregando(false);
    }
  }

  async function ajustarSaldo() {
    if (!obraId || !ajusteCatalogoUniformeId || !ajusteTamanho.trim() || !ajusteObservacao.trim()) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.estoquesUniforme.ajustar({
        obraId,
        catalogoUniformeId: ajusteCatalogoUniformeId,
        tamanho: ajusteTamanho.trim(),
        novoSaldo: Number(ajusteNovoSaldo),
        observacao: ajusteObservacao,
      });
      setAjusteCatalogoUniformeId('');
      setAjusteTamanho('');
      setAjusteNovoSaldo('0');
      setAjusteObservacao('');
      await carregarSaldos();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao ajustar estoque.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Estoque de Uniforme por Obra</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <Field label="Obra">
          <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
            <option value="">Selecione</option>
            {obras.map((o) => (
              <option key={o.id} value={o.id}>
                {o.nome}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      {obraId && (
        <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Entrada manual (reposição)</Text>
            </div>
            <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Entrada</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col4}>
                <Field label="Peça">
                  <Select value={entradaCatalogoUniformeId} onChange={(_, d) => setEntradaCatalogoUniformeId(d.value)}>
                    <option value="">Selecione</option>
                    {itensCatalogo.map((i) => (
                      <option key={i.id} value={i.id}>
                        {i.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div className={estilos.col2}>
                <Field label="Tamanho">
                  <Input value={entradaTamanho} onChange={(_, d) => setEntradaTamanho(d.value)} placeholder="ex.: M, 42..." />
                </Field>
              </div>
              <div className={estilos.col2}>
                <Field label="Quantidade">
                  <Input type="number" value={entradaQuantidade} onChange={(_, d) => setEntradaQuantidade(d.value)} />
                </Field>
              </div>
              <div className={estilos.col4}>
                <Field label="Observação (opcional)">
                  <Input value={entradaObservacao} onChange={(_, d) => setEntradaObservacao(d.value)} />
                </Field>
              </div>
            </div>
            <div className={estilos.formActions}>
              <Button
                appearance="primary"
                onClick={registrarEntrada}
                disabled={carregando || !entradaCatalogoUniformeId || !entradaTamanho.trim() || Number(entradaQuantidade) <= 0}
              >
                Registrar entrada
              </Button>
            </div>
          </div>

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Ajuste de saldo (correção de inventário)</Text>
            </div>
            <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados do Ajuste</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col4}>
                <Field label="Peça">
                  <Select value={ajusteCatalogoUniformeId} onChange={(_, d) => setAjusteCatalogoUniformeId(d.value)}>
                    <option value="">Selecione</option>
                    {itensCatalogo.map((i) => (
                      <option key={i.id} value={i.id}>
                        {i.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div className={estilos.col2}>
                <Field label="Tamanho">
                  <Input value={ajusteTamanho} onChange={(_, d) => setAjusteTamanho(d.value)} placeholder="ex.: M, 42..." />
                </Field>
              </div>
              <div className={estilos.col2}>
                <Field label="Novo saldo">
                  <Input type="number" value={ajusteNovoSaldo} onChange={(_, d) => setAjusteNovoSaldo(d.value)} />
                </Field>
              </div>
              <div className={estilos.col4}>
                <Field label="Observação (obrigatória)">
                  <Textarea value={ajusteObservacao} onChange={(_, d) => setAjusteObservacao(d.value)} />
                </Field>
              </div>
            </div>
            <div className={estilos.formActions}>
              <Button
                appearance="primary"
                onClick={ajustarSaldo}
                disabled={carregando || !ajusteCatalogoUniformeId || !ajusteTamanho.trim() || !ajusteObservacao.trim() || Number(ajusteNovoSaldo) < 0}
              >
                Ajustar saldo
              </Button>
            </div>
          </div>

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Saldo atual</Text>
            </div>
            <Table noNativeElements>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Peça</TableHeaderCell>
                  <TableHeaderCell>Tamanho</TableHeaderCell>
                  <TableHeaderCell>Saldo</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {saldos.map((s) => {
                  const chave = `${s.catalogoUniformeId}-${s.tamanho}`;
                  const selecionada = linhaSelecionada?.catalogoUniformeId === s.catalogoUniformeId && linhaSelecionada.tamanho === s.tamanho;
                  return (
                    <Fragment key={chave}>
                      <TableRow onClick={() => alternarHistorico(s.catalogoUniformeId, s.tamanho)} style={{ cursor: 'pointer' }}>
                        <TableCell>{s.catalogoUniformeNome}</TableCell>
                        <TableCell>{s.tamanho}</TableCell>
                        <TableCell>{s.saldo}</TableCell>
                      </TableRow>
                      {selecionada && (
                        <TableRow key={`${chave}-historico`}>
                          <TableCell colSpan={3}>
                            <div style={{ padding: '8px 0' }}>
                              <Text weight="semibold">
                                Histórico de movimentações — {s.catalogoUniformeNome} ({s.tamanho})
                              </Text>
                              {movimentacoes.length === 0 ? (
                                <Text as="p" size={200}>
                                  Nenhuma movimentação registrada.
                                </Text>
                              ) : (
                                <Table noNativeElements>
                                  <TableHeader>
                                    <TableRow>
                                      <TableHeaderCell>Data</TableHeaderCell>
                                      <TableHeaderCell>Tipo</TableHeaderCell>
                                      <TableHeaderCell>Quantidade</TableHeaderCell>
                                      <TableHeaderCell>Saldo resultante</TableHeaderCell>
                                      <TableHeaderCell>Observação</TableHeaderCell>
                                    </TableRow>
                                  </TableHeader>
                                  <TableBody>
                                    {movimentacoes.map((m) => (
                                      <TableRow key={m.id}>
                                        <TableCell>{new Date(m.createdAtUtc).toLocaleString('pt-BR')}</TableCell>
                                        <TableCell>{tipoMovimentacaoEstoqueUniformeLabel[m.tipo]}</TableCell>
                                        <TableCell>{m.quantidade}</TableCell>
                                        <TableCell>{m.saldoResultante}</TableCell>
                                        <TableCell>{m.observacao}</TableCell>
                                      </TableRow>
                                    ))}
                                  </TableBody>
                                </Table>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      )}
                    </Fragment>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Integrar a aba em `UniformePage.tsx`**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/UniformePage.tsx`, aplicar estas alterações:

```tsx
import { CatalogoUniformeTab } from './CatalogoUniformeTab';
import { EstoqueUniformeTab } from './EstoqueUniformeTab';
import { MatrizUniformeTab } from './MatrizUniformeTab';
import { TamanhosUniformeTab } from './TamanhosUniformeTab';

type AbaUniforme = 'catalogo' | 'estoque' | 'matriz' | 'tamanhos';
```

E, na `TabList`, adicionar `<Tab value="estoque">Estoque</Tab>` logo após `<Tab value="catalogo">Catálogo</Tab>`; e no bloco de renderização condicional, adicionar `{aba === 'estoque' && <EstoqueUniformeTab />}` logo após a linha do catálogo.

- [ ] **Step 3: Compilar o frontend**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: build limpo, sem erros de TypeScript.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/uniforme
git commit -m "feat: tela de estoque do módulo Uniforme (grade por obra e tamanho)"
```

---

### Task 11: Frontend — Entrega de Uniforme + assinatura

A peça central do módulo do lado do usuário: buscar o trabalhador, escolher só entre as peças que
a matriz da função dele autoriza, ver o tamanho resolvido automaticamente (somente leitura — nunca
digitado), registrar a entrega e assinar.

**⚠️ Atenção jurídica:** o texto do "Termo de Recebimento" abaixo é um rascunho razoável, não uma
transcrição de um modelo oficial (diferente do Termo de EPI, que é transcrito literalmente do
modelo institucional AHBT-FIC-SSO-XXX-00 — ver `AssinaturaEntregaEpiDialog.tsx`). **Antes de usar
este texto em produção, validar com QSMS/jurídico** — mesma recomendação já registrada na spec.

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaEntregaUniformeDialog.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/EntregaUniformeTab.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/UniformePage.tsx` (adicionar a aba, como padrão)

**Interfaces:**
- Consumes: `api.entregasUniforme.*`, `api.funcoes.listarUniformes`, `api.trabalhadores.listar/listarTamanhosUniforme`, `api.assinatura.criar/obter/assinarComSessao` (Task 8, mais o client de assinatura já existente).
- Produces: componentes `AssinaturaEntregaUniformeDialog`, `EntregaUniformeTab`, integrados ao `UniformePage`.

- [ ] **Step 1: `AssinaturaEntregaUniformeDialog.tsx`**

Criar `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaEntregaUniformeDialog.tsx`:

```tsx
import { useEffect, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Text,
  tokens,
} from '@fluentui/react-components';
import { Checkmark24Filled, PersonBoard24Regular } from '@fluentui/react-icons';
import { api, MetodoAutenticacaoAssinatura, type DocumentoAssinatura } from '../../lib/api';
import { usePageStyles } from '../../pages/pageStyles';
import { AssinaturaQuiosque } from './AssinaturaQuiosque';

function extrairMensagemErro(e: unknown, fallback: string): string {
  if (!(e instanceof Error)) return fallback;
  const trechoJson = e.message.match(/\{.*\}$/);
  if (trechoJson) {
    try {
      const corpo = JSON.parse(trechoJson[0]) as { erro?: string };
      if (typeof corpo.erro === 'string') return corpo.erro;
    } catch {
      // corpo não era JSON — cai no fallback abaixo
    }
  }
  return e.message || fallback;
}

export interface AssinaturaEntregaUniformeDialogProps {
  open: boolean;
  onClose: () => void;
  entregaId: string;
  trabalhadorNome: string;
  pecaNome: string;
  tamanho: string;
  quantidade: number;
  dataEntrega: string;
}

// Popup de assinatura disparado logo após "Registrar entrega" em EntregaUniformeTab.tsx — mesma
// estrutura de AssinaturaEntregaEpiDialog.tsx (assinatura do entregador em 1 clique via sessão
// logada; assinatura do receptor via AssinaturaQuiosque, crachá/PIN ou biometria). Termo de
// Recebimento é um rascunho próprio (ver aviso jurídico no topo da Task 11 do plano de
// implementação) — não uma transcrição de modelo oficial como o do EPI.
export function AssinaturaEntregaUniformeDialog({
  open,
  onClose,
  entregaId,
  trabalhadorNome,
  pecaNome,
  tamanho,
  quantidade,
  dataEntrega,
}: AssinaturaEntregaUniformeDialogProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [assinandoEntregador, setAssinandoEntregador] = useState(false);
  const [erroEntregador, setErroEntregador] = useState<string | null>(null);

  async function carregarDocumento() {
    try {
      await api.assinatura.criar('EntregaUniforme', entregaId);
      const doc = await api.assinatura.obter('EntregaUniforme', entregaId);
      setDocumento(doc);
    } catch (e) {
      setErroEntregador(extrairMensagemErro(e, 'Falha ao preparar a assinatura.'));
    }
  }

  useEffect(() => {
    if (!open) return;
    setDocumento(null);
    setErroEntregador(null);
    carregarDocumento();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, entregaId]);

  const entregadorJaAssinou = documento?.signatarios.some(
    (s) => s.metodoAutenticacao === MetodoAutenticacaoAssinatura.SessaoLogada
  );

  async function assinarComoEntregador() {
    if (!documento) return;
    try {
      setAssinandoEntregador(true);
      setErroEntregador(null);
      await api.assinatura.assinarComSessao(documento.id);
      await carregarDocumento();
    } catch (e) {
      setErroEntregador(extrairMensagemErro(e, 'Falha ao assinar como entregador.'));
    } finally {
      setAssinandoEntregador(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura da entrega de uniforme</DialogTitle>
          <DialogContent>
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Item entregue
              </Text>
              <Text style={{ display: 'block' }}>Funcionário: {trabalhadorNome}</Text>
              <Text style={{ display: 'block' }}>
                Peça: {pecaNome} — tamanho {tamanho}
              </Text>
              <Text style={{ display: 'block' }}>Quantidade: {quantidade}</Text>
              <Text style={{ display: 'block' }}>
                Data de entrega: {dataEntrega.slice(0, 10).split('-').reverse().join('/')}
              </Text>
            </div>

            <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                Assinatura do entregador
              </Text>
              {erroEntregador && <Text className={estilos.erro}>{erroEntregador}</Text>}
              {entregadorJaAssinou ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                  <Text>Assinado.</Text>
                </div>
              ) : (
                <Button
                  appearance="primary"
                  icon={<PersonBoard24Regular />}
                  onClick={assinarComoEntregador}
                  disabled={assinandoEntregador || !documento}
                >
                  Assinar como entregador
                </Button>
              )}
            </div>

            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Termo de Recebimento de Uniforme
              </Text>
              <ol style={{ margin: 0, paddingLeft: 20 }}>
                <li style={{ marginBottom: 6 }}>
                  <Text size={200}>
                    Declaro ter recebido a(s) peça(s) de uniforme relacionada(s) nesta ficha, na quantidade e
                    tamanho indicados, em condições adequadas de uso.
                  </Text>
                </li>
                <li style={{ marginBottom: 6 }}>
                  <Text size={200}>
                    Comprometo-me a utilizar o uniforme conforme o padrão da obra/empresa durante o horário de
                    trabalho, zelando por sua guarda, conservação e higienização adequadas.
                  </Text>
                </li>
                <li style={{ marginBottom: 6 }}>
                  <Text size={200}>
                    Comprometo-me a comunicar imediatamente ao responsável qualquer dano ou extravio que torne a
                    peça imprópria para uso, e a devolvê-la sempre que solicitado, inclusive em caso de
                    desligamento.
                  </Text>
                </li>
              </ol>
            </div>

            <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
              Assinatura do receptor
            </Text>
            <AssinaturaQuiosque entidadeTipo="EntregaUniforme" entidadeId={entregaId} />
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              Fechar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
```

- [ ] **Step 2: `EntregaUniformeTab.tsx`**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/EntregaUniformeTab.tsx`:

```tsx
import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaUniformeLabel,
  MotivoEntregaUniforme,
  type CatalogoUniforme,
  type EntregaUniforme,
  type NovaEntregaUniforme,
  type TamanhoUniformeTrabalhador,
  type Trabalhador,
} from '../../lib/api';
import { AssinaturaEntregaUniformeDialog } from '../../components/assinatura/AssinaturaEntregaUniformeDialog';
import { usePageStyles } from '../pageStyles';

function entregaVazia(): NovaEntregaUniforme {
  return {
    trabalhadorId: '',
    catalogoUniformeId: '',
    dataEntrega: new Date().toISOString().slice(0, 10),
    quantidade: 1,
    motivoTipo: MotivoEntregaUniforme.Inicial,
    observacoes: '',
  };
}

interface EntregaUniformeTabProps {
  aoNavegarParaMatriz: () => void;
  aoNavegarParaTamanhos: () => void;
}

// Entrega de Uniforme — travada pela matriz da função (mesmo princípio de bloqueio via dropdown
// filtrado já usado em EntregasTab.tsx do EPI: só aparecem peças que a matriz autoriza) e pelo
// tamanho cadastrado do trabalhador (resolvido automaticamente, somente leitura — nunca digitado
// nem escolhido manualmente). O backend (CriarEntregaUniformeCommand) revalida tudo de novo e
// bloqueia sem válvula de escape se algo estiver faltando; os erros retornados são exibidos como
// vieram, mesmo padrão do resto do frontend.
export function EntregaUniformeTab({ aoNavegarParaMatriz, aoNavegarParaTamanhos }: EntregaUniformeTabProps) {
  const estilos = usePageStyles();
  const [entregas, setEntregas] = useState<EntregaUniforme[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [itensPermitidos, setItensPermitidos] = useState<CatalogoUniforme[]>([]);
  const [tamanhosTrabalhador, setTamanhosTrabalhador] = useState<TamanhoUniformeTrabalhador[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaEntrega, setNovaEntrega] = useState<NovaEntregaUniforme>(entregaVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [entregaParaAssinar, setEntregaParaAssinar] = useState<EntregaUniforme | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaItens, listaTrabalhadores] = await Promise.all([
        api.entregasUniforme.listar(),
        api.catalogosUniforme.listar(),
        api.trabalhadores.listar(),
      ]);
      setEntregas(lista);
      setItensCatalogo(listaItens);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de uniforme.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  useEffect(() => {
    let cancelado = false;
    async function carregarItensPermitidosETamanhos() {
      const trabalhador = trabalhadores.find((t) => t.id === novaEntrega.trabalhadorId);
      if (!trabalhador) {
        setItensPermitidos([]);
        setTamanhosTrabalhador([]);
        return;
      }
      try {
        const [permitidos, tamanhos] = await Promise.all([
          api.funcoes.listarUniformes(trabalhador.funcaoId),
          api.trabalhadores.listarTamanhosUniforme(trabalhador.id),
        ]);
        if (!cancelado) {
          setItensPermitidos(permitidos);
          setTamanhosTrabalhador(tamanhos);
        }
      } catch {
        if (!cancelado) {
          setItensPermitidos([]);
          setTamanhosTrabalhador([]);
        }
      }
    }
    carregarItensPermitidosETamanhos();
    return () => {
      cancelado = true;
    };
  }, [novaEntrega.trabalhadorId, trabalhadores]);

  function nomeItem(id: string) {
    return itensCatalogo.find((i) => i.id === id)?.nome ?? id;
  }

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  const tamanhoResolvido = tamanhosTrabalhador.find((t) => t.catalogoUniformeId === novaEntrega.catalogoUniformeId)?.tamanho ?? null;
  const pecaSelecionadaSemTamanho = !!novaEntrega.catalogoUniformeId && tamanhoResolvido === null;

  async function criar() {
    if (!novaEntrega.trabalhadorId || !novaEntrega.catalogoUniformeId || !novaEntrega.dataEntrega || novaEntrega.quantidade < 1) {
      setErro('Preencha funcionário, peça, data de entrega e quantidade.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.entregasUniforme.criar(novaEntrega);
      setEntregaParaAssinar({ ...novaEntrega, id, tamanho: tamanhoResolvido ?? '' });
      setNovaEntrega(entregaVazia());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar entrega de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova entrega de uniforme</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Entrega</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="Funcionário">
              <Select
                value={novaEntrega.trabalhadorId}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, trabalhadorId: d.value, catalogoUniformeId: '' })}
              >
                <option value="">Selecione</option>
                {trabalhadores.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.nome} ({t.matricula})
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col4}>
            <Field label="Peça">
              <Select
                value={novaEntrega.catalogoUniformeId}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoUniformeId: d.value })}
                disabled={!novaEntrega.trabalhadorId || itensPermitidos.length === 0}
              >
                <option value="">Selecione</option>
                {itensPermitidos.map((i) => (
                  <option key={i.id} value={i.id}>
                    {i.nome}
                  </option>
                ))}
              </Select>
              {novaEntrega.trabalhadorId && itensPermitidos.length === 0 && (
                <Text size={200}>
                  Esta função não tem peças cadastradas na matriz.{' '}
                  <Button appearance="transparent" size="small" onClick={aoNavegarParaMatriz}>
                    Cadastrar em Matriz por Função
                  </Button>
                </Text>
              )}
            </Field>
          </div>
          <div className={estilos.col2}>
            <Field label="Tamanho (resolvido automaticamente)">
              <Input value={tamanhoResolvido ?? ''} readOnly disabled placeholder="—" />
              {pecaSelecionadaSemTamanho && (
                <Text size={200} className={estilos.erro}>
                  Trabalhador sem tamanho cadastrado para esta peça.{' '}
                  <Button appearance="transparent" size="small" onClick={aoNavegarParaTamanhos}>
                    Cadastrar em Tamanhos
                  </Button>
                </Text>
              )}
            </Field>
          </div>
          <div className={estilos.col2}>
            <Field label="Quantidade">
              <Input
                type="number"
                value={String(novaEntrega.quantidade)}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, quantidade: Number(d.value) })}
              />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Data de entrega">
              <CampoData
                value={novaEntrega.dataEntrega}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataEntrega: d.value })}
              />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Motivo">
              <Select
                value={novaEntrega.motivoTipo}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, motivoTipo: Number(d.value) })}
              >
                {Object.entries(motivoEntregaUniformeLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col6}>
            <Field label="Observações (opcional)">
              <Input
                value={novaEntrega.observacoes ?? ''}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, observacoes: d.value })}
              />
            </Field>
          </div>
        </div>
        <div className={estilos.formActions}>
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={criar}
            disabled={carregando || pecaSelecionadaSemTamanho}
          >
            Registrar entrega
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Entregas registradas</Text>
        </div>

        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Funcionário</TableHeaderCell>
              <TableHeaderCell>Peça</TableHeaderCell>
              <TableHeaderCell>Tamanho</TableHeaderCell>
              <TableHeaderCell>Qtd.</TableHeaderCell>
              <TableHeaderCell>Entrega</TableHeaderCell>
              <TableHeaderCell>Motivo</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entregas.map((entrega) => (
              <TableRow key={entrega.id}>
                <TableCell>{nomeTrabalhador(entrega.trabalhadorId)}</TableCell>
                <TableCell>{nomeItem(entrega.catalogoUniformeId)}</TableCell>
                <TableCell>{entrega.tamanho}</TableCell>
                <TableCell>{entrega.quantidade}</TableCell>
                <TableCell>{entrega.dataEntrega?.slice(0, 10)}</TableCell>
                <TableCell>{motivoEntregaUniformeLabel[entrega.motivoTipo]}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {entregaParaAssinar && (
        <AssinaturaEntregaUniformeDialog
          open={!!entregaParaAssinar}
          onClose={() => setEntregaParaAssinar(null)}
          entregaId={entregaParaAssinar.id}
          trabalhadorNome={nomeTrabalhador(entregaParaAssinar.trabalhadorId)}
          pecaNome={nomeItem(entregaParaAssinar.catalogoUniformeId)}
          tamanho={entregaParaAssinar.tamanho}
          quantidade={entregaParaAssinar.quantidade}
          dataEntrega={entregaParaAssinar.dataEntrega}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 3: Integrar a aba em `UniformePage.tsx` (como padrão da tela, mesmo critério de `EpiPage.tsx`)**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/uniforme/UniformePage.tsx`, aplicar estas alterações:

```tsx
import { CatalogoUniformeTab } from './CatalogoUniformeTab';
import { EntregaUniformeTab } from './EntregaUniformeTab';
import { EstoqueUniformeTab } from './EstoqueUniformeTab';
import { MatrizUniformeTab } from './MatrizUniformeTab';
import { TamanhosUniformeTab } from './TamanhosUniformeTab';

type AbaUniforme = 'entrega' | 'catalogo' | 'estoque' | 'matriz' | 'tamanhos';
```

Trocar `useState<AbaUniforme>('catalogo')` por `useState<AbaUniforme>('entrega')` (mesmo padrão de
`EpiPage.tsx`, cujo estado inicial é `'entregas'` — a aba de entrega é a que o usuário mais visita
no dia a dia, então fica em primeiro).

Na `TabList`, adicionar `<Tab value="entrega">Entrega</Tab>` como primeira aba (antes de
`catalogo`); no bloco de renderização condicional, adicionar:

```tsx
{aba === 'entrega' && (
  <EntregaUniformeTab aoNavegarParaMatriz={() => setAba('matriz')} aoNavegarParaTamanhos={() => setAba('tamanhos')} />
)}
```

logo antes da linha `{aba === 'catalogo' && <CatalogoUniformeTab />}`.

- [ ] **Step 4: Compilar o frontend**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: build limpo, sem erros de TypeScript.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/uniforme src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaEntregaUniformeDialog.tsx
git commit -m "feat: tela de entrega de uniforme travada + assinatura"
```

---

### Task 12: Integração na sidebar (`OperacaoPage`) e verificação manual completa

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/operacao/OperacaoPage.tsx`

**Interfaces:**
- Consumes: `UniformePage` (Task 11).
- Produces: aba "Uniforme" visível e navegável dentro de Operação, ao lado de "EPI / EPC".

- [ ] **Step 1: Wiring em `OperacaoPage.tsx`**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/operacao/OperacaoPage.tsx`, aplicar estas alterações:

```tsx
import { EpiPage } from '../epi/EpiPage';
import { UniformePage } from '../uniforme/UniformePage';
import { DdsPage } from '../dds/DdsPage';

type SecaoOperacao = 'apr' | 'pt' | 'inspecoes' | 'cipa' | 'epi' | 'uniforme' | 'dds' | 'identificacao';

const SECOES_VALIDAS: SecaoOperacao[] = ['apr', 'pt', 'inspecoes', 'cipa', 'epi', 'uniforme', 'dds', 'identificacao'];
```

Na `TabList`, adicionar `<Tab value="uniforme">Uniforme</Tab>` logo após `<Tab value="epi">EPI / EPC</Tab>`.

No bloco de renderização condicional, adicionar `{secao === 'uniforme' && <UniformePage mostrarTitulo={false} />}` logo após a linha `{secao === 'epi' && <EpiPage mostrarTitulo={false} />}`.

Substituir o comentário acima da função (linhas 17-20) por:

```tsx
// Item "Operação" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta virou uma
// única entrada de menu — APR, PT, Inspeções e Identificação (rotulada "Outros controles
// operacionais", mesmo nome já usado na sidebar) viraram abas aqui. CIPA, EPI/EPC e DDS entraram
// aqui em 03/09 (pedido do usuário) — saíram de Gestão de SST, ver GestaoSstPage.tsx. Uniforme
// entrou em 2026-09-07 (módulo novo, mesmo padrão do EPI) como aba própria ao lado de EPI/EPC.
```

- [ ] **Step 2: Compilar o frontend**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: build limpo, sem erros de TypeScript.

- [ ] **Step 3: Rodar a suíte completa de backend**

Run: `dotnet build && dotnet test`
Expected: build succeeded; todos os testes (novos e antigos, de todos os projetos) passam.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/operacao/OperacaoPage.tsx
git commit -m "feat: integra o módulo Uniforme na aba Operação"
```

- [ ] **Step 5: Verificação manual end-to-end no navegador**

Com o backend e o frontend rodando localmente (`dotnet run` em `src/AAHBRANT.SST.Api`, `npm run
dev` em `src/AAHBRANT.SST.TeamsApp`), navegar até Operação → Uniforme e confirmar visualmente,
nesta ordem (cada passo depende do anterior):

1. **Catálogo**: cadastrar 2 peças (ex.: "Camisa", "Calça").
2. **Matriz por Função**: abrir uma função existente (ou criar uma nova em Pessoas → Funções) e
   marcar as 2 peças como obrigatórias; salvar.
3. **Tamanhos**: escolher um trabalhador que tenha essa função, cadastrar um tamanho para cada
   peça (ex.: Camisa=M, Calça=42); salvar.
4. **Estoque**: escolher a Obra do trabalhador, registrar entrada de 5 unidades de cada peça, no
   tamanho cadastrado no passo 3; confirmar que o saldo aparece na tabela.
5. **Entrega**:
   - Selecionar o trabalhador — confirmar que só as 2 peças da matriz aparecem no select de peça
     (não qualquer peça do catálogo).
   - Selecionar uma peça — confirmar que o campo "Tamanho" preenche sozinho, somente leitura, com
     o valor cadastrado no passo 3 (nunca editável).
   - Registrar a entrega — confirmar que o diálogo de assinatura abre, mostrando peça/tamanho/
     quantidade corretos.
   - Assinar como entregador (clique único) e como receptor (via `AssinaturaQuiosque`, mesmo fluxo
     já usado no EPI) — confirmar que ambas ficam marcadas como assinadas.
   - Fechar o diálogo e confirmar que a entrega aparece na tabela "Entregas registradas", com o
     tamanho correto.
   - Voltar em Estoque e confirmar que o saldo do tamanho entregue baixou exatamente a quantidade
     entregue.
6. **Bloqueios** (testar cada um isoladamente, criando um 2º trabalhador/função sem os cadastros
   correspondentes):
   - Trabalhador cuja função não tem matriz cadastrada → o select de peça deve ficar vazio/
     desabilitado, com a mensagem e o link para "Cadastrar em Matriz por Função".
   - Trabalhador cuja função tem matriz mas ele não tem tamanho cadastrado para a peça → ao
     selecionar a peça, deve aparecer a mensagem de tamanho ausente com o link para "Cadastrar em
     Tamanhos", e o botão "Registrar entrega" deve ficar desabilitado.
   - Peça com estoque zerado no tamanho do trabalhador → tentar registrar a entrega e confirmar
     que o backend recusa com a mensagem "Estoque insuficiente...".

Reportar ao usuário, com prints de tela ou descrição do que foi visto em cada passo, antes de
considerar o módulo pronto — nenhum passo desta lista deve ser pulado nem assumido como
funcionando sem essa checagem visual real (consistente com a prática já seguida neste projeto para
toda mudança de frontend).
