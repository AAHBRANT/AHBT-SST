# Taxa de Gravidade (NBR 14280) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Desvio deliberado do TDD estrito:** este repositório não segue TDD por feature (ver `tests/` —
> só há um teste real de domínio, `CpfValidadorTests.cs`; o resto são stubs). Este plano segue o
> mesmo padrão: testes automatizados só para a única lógica pura de risco real (`TabelaDiasDebitados`
> — cálculo de Dias Debitados); o restante é verificado por build (`dotnet build`) e por navegador
> (Browser pane), igual ao histórico dos módulos anteriores (ver memória `project_sst_gsst_ia_aprovada`).

**Goal:** Adicionar o indicador Taxa de Gravidade (TG, NBR 14280) ao Painel Inicial, com card
destacado, badge de status vs. meta configurável e tooltip de detalhamento; e criar o CRUD de
lançamento mensal de HHT (Horas-Homem Trabalhadas) por obra que alimenta esse cálculo.

**Architecture:** Segue exatamente o padrão Clean Architecture + CQRS-lite com MediatR já usado em
todo o backend (Controller → `IMediator.Send` → Handler → `IAppDbContext`/EF Core), e o padrão
client-side de agregação de KPI já usado no frontend (fetch das listagens existentes + `useMemo`,
sem endpoint de agregação dedicado). Nenhuma tabela de agregação nova — TG é sempre calculada em
tempo real a partir de `Acidentes` + `RegistrosHhtMensais`.

**Tech Stack:** ASP.NET Core + MediatR + FluentValidation + EF Core (SQL Server) no backend; React
+ TypeScript + Vite + Fluent UI React Components + framer-motion no frontend.

**Spec:** Não há um arquivo de spec separado — o pedido original do usuário (formalizado nesta
conversa) e as decisões abaixo são a especificação completa deste plano.

## Global Constraints

- Fórmula oficial (NBR 14280): `TG = (Dias Perdidos + Dias Debitados) × 1.000.000 / HHT`. Quando
  `HHT = 0`, a TG não pode ser calculada — o card deve exibir "—" / mensagem, nunca dividir por zero.
- **Dias Debitados**: o Quadro III/Anexo III da NBR 14280 (tabela detalhada lesão × parte do corpo
  para Incapacidade Permanente Parcial) **não é reproduzido no sistema** — risco de fabricar dado
  normativo. Só os dois valores fixos e amplamente documentados (Óbito e Incapacidade Permanente
  Total = 6.000 dias) são calculados automaticamente; Incapacidade Permanente Parcial exige input
  manual do usuário, orientado a consultar a tabela oficial. **Decisão confirmada pelo usuário**
  ("Híbrido") em 26/08/2026.
- Cor do card: **não usar `#800020`** (não é a cor da marca) — usar os `designTokens` já existentes
  em `theme.ts` (`colorPrimary` = `#7B1E2B`, `colorSuccess`, `colorAlert`). **Decisão confirmada
  pelo usuário** ("Seguir o tema existente").
- Cálculo da TG é **client-side**, como todos os outros KPIs do app. **Confirmado pelo usuário.**
- Granularidade do lançamento de HHT: **mensal por obra**. **Confirmado pelo usuário.**
- Meta/limite da TG (para o badge verde/vermelho) é uma decisão de negócio que este projeto não
  pode inventar — é um valor **configurável pelo próprio usuário no card** (persistido em
  `localStorage`, sem default fabricado). Sem meta definida, o card mostra o valor mas não exibe
  badge.
- Não criar novo item de menu/pillar para HHT (regra de IA já registrada em memória
  `feedback_ia_consolidada_por_pessoa`) — o lançamento de HHT vira uma aba interna dentro de
  `AcidentesPage.tsx`, seguindo o padrão de `RiscosPage.tsx`.
- RBAC: novas permissões só precisam de tuplas em `RbacSeeder.cs` (nenhuma mudança em `Program.cs`).

---

## File Structure

**Backend — Domain:**
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs` — novo enum `GravidadeAcidente`
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Acidentes/Acidente.cs` — campos `Gravidade`, `DiasDebitados`
- Create: `src/AAHBRANT.SST.Domain/Entidades/Acidentes/TabelaDiasDebitados.cs` — helper puro de cálculo
- Create: `src/AAHBRANT.SST.Domain/Entidades/RegistroHhtMensal.cs` — nova entidade

**Backend — Infrastructure:**
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/RegistroHhtMensalConfiguracao.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs` — `DbSet<RegistroHhtMensal>`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs` — permissões `hht:*`
- Create (via `dotnet ef migrations add`): nova migration `AdicionarGravidadeEHhtMensal`

**Backend — Application:**
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs` — `DbSet<RegistroHhtMensal>`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/AcidenteDto.cs`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/Commands/CriarAcidenteCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/Commands/AtualizarAcidenteCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/Queries/ListarAcidentesQuery.cs`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/Queries/ObterAcidenteDetalheQuery.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/RegistroHhtMensalDto.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/CriarRegistroHhtMensalCommand.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/AtualizarRegistroHhtMensalCommand.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/ExcluirRegistroHhtMensalCommand.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Queries/ListarRegistrosHhtMensaisQuery.cs`

**Backend — Api:**
- Modify: `src/AAHBRANT.SST.Api/Controllers/AcidentesController.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/RegistrosHhtMensaisController.cs`

**Test:**
- Create: `tests/AAHBRANT.SST.Domain.Tests/Entidades/TabelaDiasDebitadosTests.cs`

**Frontend:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/acidentes/AcidentesPage.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/acidentes/HhtMensalTab.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/components/dashboard/TaxaGravidadeCard.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/DashboardPage.tsx`

---

### Task 1: Domain — GravidadeAcidente, TabelaDiasDebitados e campos em Acidente

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs`
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Acidentes/Acidente.cs`
- Create: `src/AAHBRANT.SST.Domain/Entidades/Acidentes/TabelaDiasDebitados.cs`
- Test: `tests/AAHBRANT.SST.Domain.Tests/Entidades/TabelaDiasDebitadosTests.cs`

**Interfaces:**
- Produces: `GravidadeAcidente` enum (`SemAfastamento=1, ComAfastamento=2,
  IncapacidadePermanenteParcial=3, IncapacidadePermanenteTotal=4, Obito=5`);
  `TabelaDiasDebitados.Calcular(GravidadeAcidente gravidade, int? diasDebitadosInformados) : int`;
  `Acidente.Gravidade : GravidadeAcidente`, `Acidente.DiasDebitados : int`.

- [ ] **Step 1: Adicionar o enum `GravidadeAcidente`**

Em `src/AAHBRANT.SST.Domain/Enums/Enums.cs`, logo após o enum `StatusAcidente` (antes de
`StatusRequisitoLegal`):

```csharp
// Classificação de gravidade do acidente, usada para calcular Dias Debitados na Taxa de
// Gravidade (NBR 14280, ver TabelaDiasDebitados). Vocabulário não citado literalmente na Base
// de Conhecimento — proposta própria, mesma natureza de StatusAcidente acima.
public enum GravidadeAcidente
{
    SemAfastamento = 1,
    ComAfastamento = 2,
    IncapacidadePermanenteParcial = 3,
    IncapacidadePermanenteTotal = 4,
    Obito = 5
}
```

- [ ] **Step 2: Criar `TabelaDiasDebitados`**

Create `src/AAHBRANT.SST.Domain/Entidades/Acidentes/TabelaDiasDebitados.cs`:

```csharp
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Regra de Dias Debitados da NBR 14280 (Anexo/Quadro III). Só os dois casos com valor fixo e
// amplamente documentado são calculados automaticamente (Óbito e Incapacidade Permanente Total =
// 6.000 dias). Incapacidade Permanente Parcial depende da tabela detalhada de lesão × parte do
// corpo do Quadro III, que não é reproduzida aqui — fica como valor informado manualmente pelo
// usuário no registro do acidente. Decisão registrada em conversa de 2026-08-26: não fabricar os
// valores tabelados sem a fonte normativa oficial em mãos.
public static class TabelaDiasDebitados
{
    public const int DiasObitoOuIncapacidadeTotal = 6000;

    public static int Calcular(GravidadeAcidente gravidade, int? diasDebitadosInformados) => gravidade switch
    {
        GravidadeAcidente.Obito => DiasObitoOuIncapacidadeTotal,
        GravidadeAcidente.IncapacidadePermanenteTotal => DiasObitoOuIncapacidadeTotal,
        GravidadeAcidente.IncapacidadePermanenteParcial => diasDebitadosInformados ?? 0,
        _ => 0,
    };
}
```

- [ ] **Step 3: Adicionar campos em `Acidente`**

Em `src/AAHBRANT.SST.Domain/Entidades/Acidentes/Acidente.cs`, logo após `DiasAfastamento`:

```csharp
    public bool HouveAfastamento { get; set; }
    public int? DiasAfastamento { get; set; }

    // Gravidade e DiasDebitados alimentam a Taxa de Gravidade (NBR 14280) exibida no Painel
    // Inicial — ver TabelaDiasDebitados. DiasDebitados é sempre o valor final calculado/gravado
    // pelo handler (nunca aceito diretamente do cliente): fixo em 6.000 para Óbito/Incapacidade
    // Permanente Total, ou o valor informado manualmente para Incapacidade Permanente Parcial
    // (tabela detalhada do Quadro III não reproduzida no sistema).
    public GravidadeAcidente Gravidade { get; set; } = GravidadeAcidente.SemAfastamento;
    public int DiasDebitados { get; set; }
```

- [ ] **Step 4: Escrever o teste de `TabelaDiasDebitados`**

Create `tests/AAHBRANT.SST.Domain.Tests/Entidades/TabelaDiasDebitadosTests.cs`:

```csharp
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Xunit;

namespace AAHBRANT.SST.Domain.Tests.Entidades;

public class TabelaDiasDebitadosTests
{
    [Theory]
    [InlineData(GravidadeAcidente.Obito, null, 6000)]
    [InlineData(GravidadeAcidente.IncapacidadePermanenteTotal, null, 6000)]
    [InlineData(GravidadeAcidente.SemAfastamento, null, 0)]
    [InlineData(GravidadeAcidente.ComAfastamento, null, 0)]
    public void Calcular_CasosFixos_RetornaValorEsperado(GravidadeAcidente gravidade, int? informado, int esperado)
    {
        Assert.Equal(esperado, TabelaDiasDebitados.Calcular(gravidade, informado));
    }

    [Fact]
    public void Calcular_IncapacidadeParcial_UsaValorInformado()
    {
        Assert.Equal(180, TabelaDiasDebitados.Calcular(GravidadeAcidente.IncapacidadePermanenteParcial, 180));
    }

    [Fact]
    public void Calcular_IncapacidadeParcial_SemValorInformado_RetornaZero()
    {
        Assert.Equal(0, TabelaDiasDebitados.Calcular(GravidadeAcidente.IncapacidadePermanenteParcial, null));
    }
}
```

- [ ] **Step 5: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Domain.Tests --filter TabelaDiasDebitadosTests`
Expected: 4 testes (Theory com 4 casos + 2 Facts = 6 execuções) todos PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Enums/Enums.cs src/AAHBRANT.SST.Domain/Entidades/Acidentes/Acidente.cs src/AAHBRANT.SST.Domain/Entidades/Acidentes/TabelaDiasDebitados.cs tests/AAHBRANT.SST.Domain.Tests/Entidades/TabelaDiasDebitadosTests.cs
git commit -m "feat: adiciona GravidadeAcidente e cálculo de Dias Debitados (NBR 14280)"
```

---

### Task 2: Domain — entidade `RegistroHhtMensal`

**Files:**
- Create: `src/AAHBRANT.SST.Domain/Entidades/RegistroHhtMensal.cs`

**Interfaces:**
- Produces: `RegistroHhtMensal { Guid ObraId; Obra? Obra; int Ano; int Mes; int HorasHomemTrabalhadas; }`
  (mais campos de `AuditableEntity`: `Id`, `CreatedAtUtc`, `Ativo`, etc.)

- [ ] **Step 1: Criar a entidade**

Create `src/AAHBRANT.SST.Domain/Entidades/RegistroHhtMensal.cs`:

```csharp
using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Lançamento mensal de Horas-Homem Trabalhadas (HHT) por obra — insumo do cálculo da Taxa de
// Gravidade (NBR 14280, ver TabelaDiasDebitados) exibida no Painel Inicial. Vocabulário/
// granularidade sem citação literal na Base de Conhecimento — mensal por obra foi a opção
// escolhida pelo usuário entre as alternativas apresentadas em 2026-08-26.
public class RegistroHhtMensal : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public int Ano { get; set; }
    public int Mes { get; set; }
    public int HorasHomemTrabalhadas { get; set; }
}
```

- [ ] **Step 2: Compilar o projeto Domain**

Run: `dotnet build src/AAHBRANT.SST.Domain`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/RegistroHhtMensal.cs
git commit -m "feat: adiciona entidade RegistroHhtMensal"
```

---

### Task 3: Infrastructure — EF configuration, DbContext, RBAC e migration

**Files:**
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/RegistroHhtMensalConfiguracao.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs`
- Migration gerada por tooling (`dotnet ef migrations add`)

**Interfaces:**
- Consumes: `RegistroHhtMensal` (Task 2)
- Produces: `IAppDbContext.RegistrosHhtMensais : DbSet<RegistroHhtMensal>`; permissões
  `hht:ver`, `hht:criar`, `hht:editar`, `hht:excluir`; tabela `RegistrosHhtMensais` e colunas
  `Gravidade`/`DiasDebitados` em `Acidentes`.

- [ ] **Step 1: Criar a configuração EF**

Create `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/RegistroHhtMensalConfiguracao.cs`:

```csharp
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class RegistroHhtMensalConfiguracao : IEntityTypeConfiguration<RegistroHhtMensal>
{
    public void Configure(EntityTypeBuilder<RegistroHhtMensal> builder)
    {
        builder.HasOne(r => r.Obra).WithMany()
            .HasForeignKey(r => r.ObraId).OnDelete(DeleteBehavior.Restrict);

        // Um único lançamento de HHT por obra/mês — evita duplicidade/soma indevida no cálculo da TG.
        builder.HasIndex(r => new { r.ObraId, r.Ano, r.Mes }).IsUnique();
        builder.HasQueryFilter(r => r.Ativo);
    }
}
```

Esta classe é descoberta automaticamente por `ApplyConfigurationsFromAssembly` em
`SstDbContext.OnModelCreating` — nenhuma outra mudança de wiring é necessária.

- [ ] **Step 2: Adicionar o `DbSet` em `IAppDbContext`**

Em `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`, logo após a linha
`DbSet<Acidente> Acidentes { get; }`:

```csharp
    DbSet<Acidente> Acidentes { get; }
    DbSet<RegistroHhtMensal> RegistrosHhtMensais { get; }
```

- [ ] **Step 3: Adicionar o `DbSet` em `SstDbContext`**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`, logo após a linha
`public DbSet<Acidente> Acidentes => Set<Acidente>();`:

```csharp
    public DbSet<Acidente> Acidentes => Set<Acidente>();
    public DbSet<RegistroHhtMensal> RegistrosHhtMensais => Set<RegistroHhtMensal>();
```

- [ ] **Step 4: Adicionar as permissões `hht:*` no `RbacSeeder`**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs`, dentro do array
`CatalogoPermissoes`, logo após o bloco `acidente:*` (linhas 124-127):

```csharp
        ("acidente:ver", "Acidente", "Ver", "Ver acidentes/incidentes"),
        ("acidente:criar", "Acidente", "Criar", "Registrar acidente/incidente"),
        ("acidente:editar", "Acidente", "Editar", "Editar registro de acidente/investigação"),
        ("acidente:avancar_status", "Acidente", "AvancarStatus", "Avançar status do acidente"),

        ("hht:ver", "RegistroHht", "Ver", "Ver registros mensais de HHT por obra"),
        ("hht:criar", "RegistroHht", "Criar", "Lançar registro mensal de HHT"),
        ("hht:editar", "RegistroHht", "Editar", "Editar registro mensal de HHT"),
        ("hht:excluir", "RegistroHht", "Excluir", "Excluir registro mensal de HHT"),
```

- [ ] **Step 5: Compilar antes de gerar a migration**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure`
Expected: Build succeeded (confirma que a entidade/config/DbSet compilam antes de rodar o tooling do EF).

- [ ] **Step 6: Gerar a migration**

Run (a partir da raiz do repo):
```bash
dotnet ef migrations add AdicionarGravidadeEHhtMensal --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api
```

Expected: gera `Migrations/<timestamp>_AdicionarGravidadeEHhtMensal.cs` e atualiza
`SstDbContextModelSnapshot.cs`. A migration gerada deve conter, no `Up`:
- `AddColumn<int>("Gravidade", "Acidentes", nullable: false, defaultValue: 1)` (1 = `SemAfastamento`)
- `AddColumn<int>("DiasDebitados", "Acidentes", nullable: false, defaultValue: 0)`
- `CreateTable("RegistrosHhtMensais", ...)` com colunas `Id, ObraId, Ano, Mes,
  HorasHomemTrabalhadas` + as colunas de `AuditableEntity` (`CreatedAtUtc, CreatedBy,
  UpdatedAtUtc, UpdatedBy, Origem, Ativo, RowVersion`), FK `ObraId → Obras.Id` (Restrict), e
  `CreateIndex` único em `(ObraId, Ano, Mes)`.

Se o arquivo gerado divergir dessa forma (ex.: sem `defaultValue` nas colunas novas de
`Acidentes`, o que quebraria linhas já existentes na tabela), edite manualmente o método `Up` para
adicionar `defaultValue: 1` (Gravidade) e `defaultValue: 0` (DiasDebitados) antes de aplicar.

- [ ] **Step 7: Aplicar a migration localmente e validar**

Run: `dotnet ef database update --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api`
Expected: aplica sem erro no banco local/dev configurado em `appsettings.Development.json`.

**Não aplicar esta migration no ambiente hml/Azure sem confirmação explícita separada** — isso é
uma alteração de schema em ambiente compartilhado (ver memória `project_sst_deploy_teams_azure`).

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs
git commit -m "feat: adiciona RegistroHhtMensal, campos de gravidade em Acidente e permissões hht:*"
```

---

### Task 4: Application + Api — CRUD de `RegistroHhtMensal`

**Files:**
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/RegistroHhtMensalDto.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/CriarRegistroHhtMensalCommand.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/AtualizarRegistroHhtMensalCommand.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/ExcluirRegistroHhtMensalCommand.cs`
- Create: `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Queries/ListarRegistrosHhtMensaisQuery.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/RegistrosHhtMensaisController.cs`

**Interfaces:**
- Consumes: `IAppDbContext.RegistrosHhtMensais` (Task 3), `RegistroHhtMensal` (Task 2)
- Produces: `POST /api/registroshhtmensais`, `PUT /api/registroshhtmensais/{id}`,
  `DELETE /api/registroshhtmensais/{id}`, `GET /api/registroshhtmensais?obraId=&ano=`

- [ ] **Step 1: Criar o DTO**

Create `src/AAHBRANT.SST.Application/RegistrosHhtMensais/RegistroHhtMensalDto.cs`:

```csharp
namespace AAHBRANT.SST.Application.RegistrosHhtMensais;

public class RegistroHhtMensalDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string? ObraNome { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public int HorasHomemTrabalhadas { get; set; }
}
```

- [ ] **Step 2: Criar `CriarRegistroHhtMensalCommand`**

Create `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/CriarRegistroHhtMensalCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegistrosHhtMensais.Commands;

public record CriarRegistroHhtMensalCommand(Guid ObraId, int Ano, int Mes, int HorasHomemTrabalhadas)
    : IRequest<Guid>;

public class CriarRegistroHhtMensalCommandValidator : AbstractValidator<CriarRegistroHhtMensalCommand>
{
    public CriarRegistroHhtMensalCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Ano).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Mes).InclusiveBetween(1, 12);
        RuleFor(x => x.HorasHomemTrabalhadas).GreaterThanOrEqualTo(0);
    }
}

public class CriarRegistroHhtMensalCommandHandler : IRequestHandler<CriarRegistroHhtMensalCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarRegistroHhtMensalCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarRegistroHhtMensalCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (await _db.RegistrosHhtMensais.AnyAsync(
                r => r.ObraId == request.ObraId && r.Ano == request.Ano && r.Mes == request.Mes, ct))
            throw new InvalidOperationException("Já existe um registro de HHT para esta obra neste mês.");

        var registro = new RegistroHhtMensal
        {
            ObraId = request.ObraId,
            Ano = request.Ano,
            Mes = request.Mes,
            HorasHomemTrabalhadas = request.HorasHomemTrabalhadas,
        };

        _db.RegistrosHhtMensais.Add(registro);
        await _db.SaveChangesAsync(ct);
        return registro.Id;
    }
}
```

- [ ] **Step 3: Criar `AtualizarRegistroHhtMensalCommand`**

Create `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/AtualizarRegistroHhtMensalCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegistrosHhtMensais.Commands;

public record AtualizarRegistroHhtMensalCommand(Guid Id, Guid ObraId, int Ano, int Mes, int HorasHomemTrabalhadas)
    : IRequest;

public class AtualizarRegistroHhtMensalCommandValidator : AbstractValidator<AtualizarRegistroHhtMensalCommand>
{
    public AtualizarRegistroHhtMensalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Ano).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Mes).InclusiveBetween(1, 12);
        RuleFor(x => x.HorasHomemTrabalhadas).GreaterThanOrEqualTo(0);
    }
}

public class AtualizarRegistroHhtMensalCommandHandler : IRequestHandler<AtualizarRegistroHhtMensalCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarRegistroHhtMensalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarRegistroHhtMensalCommand request, CancellationToken ct)
    {
        var registro = await _db.RegistrosHhtMensais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Registro de HHT {request.Id} não encontrado.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (await _db.RegistrosHhtMensais.AnyAsync(
                r => r.Id != request.Id && r.ObraId == request.ObraId && r.Ano == request.Ano && r.Mes == request.Mes, ct))
            throw new InvalidOperationException("Já existe um registro de HHT para esta obra neste mês.");

        registro.ObraId = request.ObraId;
        registro.Ano = request.Ano;
        registro.Mes = request.Mes;
        registro.HorasHomemTrabalhadas = request.HorasHomemTrabalhadas;

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Criar `ExcluirRegistroHhtMensalCommand`**

Create `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Commands/ExcluirRegistroHhtMensalCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegistrosHhtMensais.Commands;

public record ExcluirRegistroHhtMensalCommand(Guid Id) : IRequest;

public class ExcluirRegistroHhtMensalCommandValidator : AbstractValidator<ExcluirRegistroHhtMensalCommand>
{
    public ExcluirRegistroHhtMensalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirRegistroHhtMensalCommandHandler : IRequestHandler<ExcluirRegistroHhtMensalCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirRegistroHhtMensalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirRegistroHhtMensalCommand request, CancellationToken ct)
    {
        var registro = await _db.RegistrosHhtMensais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Registro de HHT {request.Id} não encontrado.");

        _db.RegistrosHhtMensais.Remove(registro);
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Criar `ListarRegistrosHhtMensaisQuery`**

Create `src/AAHBRANT.SST.Application/RegistrosHhtMensais/Queries/ListarRegistrosHhtMensaisQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegistrosHhtMensais.Queries;

public record ListarRegistrosHhtMensaisQuery(Guid? ObraId, int? Ano) : IRequest<List<RegistroHhtMensalDto>>;

public class ListarRegistrosHhtMensaisQueryHandler
    : IRequestHandler<ListarRegistrosHhtMensaisQuery, List<RegistroHhtMensalDto>>
{
    private readonly IAppDbContext _db;

    public ListarRegistrosHhtMensaisQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RegistroHhtMensalDto>> Handle(ListarRegistrosHhtMensaisQuery request, CancellationToken ct)
    {
        var query = _db.RegistrosHhtMensais.AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(r => r.ObraId == request.ObraId.Value);

        if (request.Ano.HasValue)
            query = query.Where(r => r.Ano == request.Ano.Value);

        return await query
            .Include(r => r.Obra)
            .OrderByDescending(r => r.Ano).ThenByDescending(r => r.Mes)
            .Select(r => new RegistroHhtMensalDto
            {
                Id = r.Id,
                ObraId = r.ObraId,
                ObraNome = r.Obra != null ? r.Obra.Nome : null,
                Ano = r.Ano,
                Mes = r.Mes,
                HorasHomemTrabalhadas = r.HorasHomemTrabalhadas,
            })
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 6: Criar o controller**

Create `src/AAHBRANT.SST.Api/Controllers/RegistrosHhtMensaisController.cs`:

```csharp
using AAHBRANT.SST.Application.RegistrosHhtMensais.Commands;
using AAHBRANT.SST.Application.RegistrosHhtMensais.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrosHhtMensaisController : ControllerBase
{
    private readonly IMediator _mediator;

    public RegistrosHhtMensaisController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "hht:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, [FromQuery] int? ano, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarRegistrosHhtMensaisQuery(obraId, ano), ct));

    [Authorize(Policy = "hht:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarRegistroHhtMensalCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { id }, new { id });
    }

    [Authorize(Policy = "hht:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarRegistroHhtMensalRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarRegistroHhtMensalCommand(
            id, body.ObraId, body.Ano, body.Mes, body.HorasHomemTrabalhadas), ct);
        return NoContent();
    }

    [Authorize(Policy = "hht:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirRegistroHhtMensalCommand(id), ct);
        return NoContent();
    }
}

public record AtualizarRegistroHhtMensalRequestBody(Guid ObraId, int Ano, int Mes, int HorasHomemTrabalhadas);
```

- [ ] **Step 7: Compilar**

Run: `dotnet build src/AAHBRANT.SST.Api`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Application/RegistrosHhtMensais src/AAHBRANT.SST.Api/Controllers/RegistrosHhtMensaisController.cs
git commit -m "feat: adiciona CRUD de RegistroHhtMensal (backend)"
```

---

### Task 5: Application + Api — Gravidade/DiasDebitados em Acidente

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Acidentes/AcidenteDto.cs`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/Commands/CriarAcidenteCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/Commands/AtualizarAcidenteCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/Queries/ListarAcidentesQuery.cs`
- Modify: `src/AAHBRANT.SST.Application/Acidentes/Queries/ObterAcidenteDetalheQuery.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/AcidentesController.cs`

**Interfaces:**
- Consumes: `TabelaDiasDebitados.Calcular` (Task 1), `GravidadeAcidente` (Task 1)
- Produces: `AcidenteDto.Gravidade`, `AcidenteDto.DiasDebitados`; `CriarAcidenteCommand` e
  `AtualizarAcidenteCommand` ganham `Gravidade` e `DiasDebitadosInformados`.

- [ ] **Step 1: Adicionar campos no `AcidenteDto`**

Em `src/AAHBRANT.SST.Application/Acidentes/AcidenteDto.cs`, logo após `NumeroCat`:

```csharp
    public bool HouveAfastamento { get; set; }
    public int? DiasAfastamento { get; set; }
    public string? NumeroCat { get; set; }

    public GravidadeAcidente Gravidade { get; set; }
    public int DiasDebitados { get; set; }
```

(É necessário `using AAHBRANT.SST.Domain.Enums;` — já presente no arquivo.)

- [ ] **Step 2: Atualizar `CriarAcidenteCommand`**

Em `src/AAHBRANT.SST.Application/Acidentes/Commands/CriarAcidenteCommand.cs`, adicionar
`using AAHBRANT.SST.Domain.Entidades;` (para `TabelaDiasDebitados`) e alterar o record/validator/handler:

```csharp
public record CriarAcidenteCommand(
    TipoOcorrencia Tipo,
    Guid ObraId,
    Guid? TrabalhadorId,
    Guid? AtividadeId,
    string Local,
    DateTime Data,
    TimeSpan? Hora,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    MetodologiaInvestigacao? MetodologiaInvestigacao,
    string? Causas,
    GravidadeAcidente Gravidade,
    int? DiasDebitadosInformados) : IRequest<Guid>;

public class CriarAcidenteCommandValidator : AbstractValidator<CriarAcidenteCommand>
{
    public CriarAcidenteCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Lesao).MaximumLength(500);
        RuleFor(x => x.Consequencia).MaximumLength(500);
        RuleFor(x => x.Atendimento).MaximumLength(500);
        RuleFor(x => x.NumeroCat).MaximumLength(50);
        RuleFor(x => x.Causas).MaximumLength(2000);
        RuleFor(x => x.DiasAfastamento).GreaterThanOrEqualTo(0).When(x => x.DiasAfastamento.HasValue);
        RuleFor(x => x.DiasDebitadosInformados)
            .NotNull().WithMessage("Informe os Dias Debitados consultando o Quadro III da NBR 14280.")
            .GreaterThan(0)
            .When(x => x.Gravidade == GravidadeAcidente.IncapacidadePermanenteParcial);
    }
}

public class CriarAcidenteCommandHandler : IRequestHandler<CriarAcidenteCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAcidenteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAcidenteCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.TrabalhadorId.HasValue &&
            !await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct))
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        if (request.AtividadeId.HasValue &&
            !await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct))
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        var acidente = new Acidente
        {
            Tipo = request.Tipo,
            ObraId = request.ObraId,
            TrabalhadorId = request.TrabalhadorId,
            AtividadeId = request.AtividadeId,
            Local = request.Local,
            Data = request.Data,
            Hora = request.Hora,
            Descricao = request.Descricao,
            Lesao = request.Lesao,
            Consequencia = request.Consequencia,
            Atendimento = request.Atendimento,
            HouveAfastamento = request.HouveAfastamento,
            DiasAfastamento = request.DiasAfastamento,
            NumeroCat = request.NumeroCat,
            MetodologiaInvestigacao = request.MetodologiaInvestigacao,
            Causas = request.Causas,
            Gravidade = request.Gravidade,
            DiasDebitados = TabelaDiasDebitados.Calcular(request.Gravidade, request.DiasDebitadosInformados),
        };

        _db.Acidentes.Add(acidente);
        await _db.SaveChangesAsync(ct);
        return acidente.Id;
    }
}
```

- [ ] **Step 3: Atualizar `AtualizarAcidenteCommand`**

Mesmas mudanças de Step 2, aplicadas a
`src/AAHBRANT.SST.Application/Acidentes/Commands/AtualizarAcidenteCommand.cs`: adicionar
`GravidadeAcidente Gravidade, int? DiasDebitadosInformados` ao record (após `Causas`), a mesma
regra do validator, e no handler:

```csharp
        acidente.Causas = request.Causas;
        acidente.Gravidade = request.Gravidade;
        acidente.DiasDebitados = TabelaDiasDebitados.Calcular(request.Gravidade, request.DiasDebitadosInformados);

        await _db.SaveChangesAsync(ct);
```

(adicionar `using AAHBRANT.SST.Domain.Entidades;` para `TabelaDiasDebitados`.)

- [ ] **Step 4: Atualizar `ListarAcidentesQuery`**

Em `src/AAHBRANT.SST.Application/Acidentes/Queries/ListarAcidentesQuery.cs`, no `Select`, logo
após `NumeroCat = a.NumeroCat,`:

```csharp
                NumeroCat = a.NumeroCat,
                Gravidade = a.Gravidade,
                DiasDebitados = a.DiasDebitados,
```

- [ ] **Step 5: Atualizar `ObterAcidenteDetalheQuery`**

Em `src/AAHBRANT.SST.Application/Acidentes/Queries/ObterAcidenteDetalheQuery.cs`, no `Select` de
`AcidenteDto` (mesma posição, após `NumeroCat = a.NumeroCat,`):

```csharp
                NumeroCat = a.NumeroCat,
                Gravidade = a.Gravidade,
                DiasDebitados = a.DiasDebitados,
```

- [ ] **Step 6: Atualizar `AcidentesController`**

Em `src/AAHBRANT.SST.Api/Controllers/AcidentesController.cs`:

```csharp
    [Authorize(Policy = "acidente:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAcidenteCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAcidenteRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarAcidenteCommand(
            id, body.Tipo, body.ObraId, body.TrabalhadorId, body.AtividadeId, body.Local, body.Data,
            body.Hora, body.Descricao, body.Lesao, body.Consequencia, body.Atendimento,
            body.HouveAfastamento, body.DiasAfastamento, body.NumeroCat, body.MetodologiaInvestigacao,
            body.Causas, body.Gravidade, body.DiasDebitadosInformados), ct);
        return NoContent();
    }
```

E o record no final do arquivo:

```csharp
public record AtualizarAcidenteRequestBody(
    TipoOcorrencia Tipo,
    Guid ObraId,
    Guid? TrabalhadorId,
    Guid? AtividadeId,
    string Local,
    DateTime Data,
    TimeSpan? Hora,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    MetodologiaInvestigacao? MetodologiaInvestigacao,
    string? Causas,
    GravidadeAcidente Gravidade,
    int? DiasDebitadosInformados);
```

- [ ] **Step 7: Compilar toda a solução**

Run: `dotnet build`
Expected: Build succeeded, sem erros (confirma que `Criar` no controller ainda compila — o
`CriarAcidenteCommand` é bindado direto do body em `Criar(CriarAcidenteCommand command, ...)`,
então o novo campo `Gravidade`/`DiasDebitadosInformados` é resolvido automaticamente pelo model
binding do ASP.NET Core).

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Application/Acidentes src/AAHBRANT.SST.Api/Controllers/AcidentesController.cs
git commit -m "feat: adiciona Gravidade/DiasDebitados ao fluxo de Acidente"
```

---

### Task 6: Frontend — `lib/api.ts`

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`

**Interfaces:**
- Consumes: rotas de Task 4/5 (`/api/registroshhtmensais`, `/api/acidentes`)
- Produces: `GravidadeAcidente`/`gravidadeAcidenteLabel`, campos `gravidade`/`diasDebitados`/
  `diasDebitadosInformados` em `Acidente`/`NovoAcidente`, `RegistroHhtMensal`/`NovoRegistroHhtMensal`,
  cliente `api.registrosHht`.

- [ ] **Step 1: Adicionar `GravidadeAcidente` e labels**

Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, logo após o bloco `statusAcidenteLabel` (antes de
`export interface Acidente`):

```typescript
// Vocabulário não citado literalmente na Base de Conhecimento — mesma decisão do backend
// (ver Domain/Enums/Enums.cs GravidadeAcidente).
export const GravidadeAcidente = {
  SemAfastamento: 1,
  ComAfastamento: 2,
  IncapacidadePermanenteParcial: 3,
  IncapacidadePermanenteTotal: 4,
  Obito: 5,
} as const;

export const gravidadeAcidenteLabel: Record<number, string> = {
  1: 'Sem afastamento',
  2: 'Com afastamento',
  3: 'Incapacidade permanente parcial',
  4: 'Incapacidade permanente total',
  5: 'Óbito',
};
```

- [ ] **Step 2: Adicionar campos em `Acidente`/`NovoAcidente`**

No mesmo arquivo, em `export interface Acidente { ... }`, adicionar após `numeroCat`:

```typescript
  numeroCat?: string | null;
  gravidade: number;
  diasDebitados: number;
```

Em `export interface NovoAcidente { ... }`, adicionar após `numeroCat`:

```typescript
  numeroCat?: string | null;
  gravidade: number;
  diasDebitadosInformados?: number | null;
```

- [ ] **Step 3: Criar `RegistroHhtMensal`/`NovoRegistroHhtMensal`**

Logo após a interface `Acidente`/`NovoAcidente`/`AcidenteDetalhe` (após a linha
`export type AtualizarAcidentePayload = NovoAcidente;` e antes de `export interface AcidenteDetalhe`,
ou logo depois dela):

```typescript
export interface RegistroHhtMensal {
  id: string;
  obraId: string;
  obraNome?: string | null;
  ano: number;
  mes: number;
  horasHomemTrabalhadas: number;
}

export type NovoRegistroHhtMensal = Omit<RegistroHhtMensal, 'id' | 'obraNome'>;
export type AtualizarRegistroHhtMensalPayload = NovoRegistroHhtMensal;
```

- [ ] **Step 4: Adicionar `api.registrosHht`**

No objeto `export const api = { ... }`, logo após o bloco `acidentes: { ... },` (fechamento na
linha correspondente a `avancarStatus`):

```typescript
  registrosHht: {
    listar: (filtros?: { obraId?: string; ano?: number }) => {
      const params = new URLSearchParams();
      if (filtros?.obraId) params.set('obraId', filtros.obraId);
      if (filtros?.ano) params.set('ano', String(filtros.ano));
      const query = params.toString();
      return request<RegistroHhtMensal[]>(`/api/registroshhtmensais${query ? `?${query}` : ''}`);
    },
    criar: (registro: NovoRegistroHhtMensal) =>
      request<{ id: string }>('/api/registroshhtmensais', { method: 'POST', body: JSON.stringify(registro) }),
    atualizar: (id: string, registro: AtualizarRegistroHhtMensalPayload) =>
      request<void>(`/api/registroshhtmensais/${id}`, { method: 'PUT', body: JSON.stringify(registro) }),
    excluir: (id: string) => request<void>(`/api/registroshhtmensais/${id}`, { method: 'DELETE' }),
  },
```

- [ ] **Step 5: Verificar o build TypeScript**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit`
Expected: nenhum erro de tipo (confirma que os novos campos obrigatórios em `NovoAcidente`/
`RegistroHhtMensal` não quebraram nenhum uso existente — o Step 7 da Task 7 é o que corrige
`AcidentesPage.tsx`, então rode este check de novo ao final da Task 7).

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat: adiciona tipos/cliente de RegistroHhtMensal e Gravidade em lib/api.ts"
```

---

### Task 7: Frontend — `AcidentesPage.tsx` (campo Gravidade + aba de HHT mensal)

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/acidentes/AcidentesPage.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/acidentes/HhtMensalTab.tsx`

**Interfaces:**
- Consumes: `api.acidentes`, `api.registrosHht`, `GravidadeAcidente`, `gravidadeAcidenteLabel` (Task 6)

- [ ] **Step 1: Criar `HhtMensalTab.tsx`**

Create `src/AAHBRANT.SST.TeamsApp/src/pages/acidentes/HhtMensalTab.tsx`:

```typescript
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
import { AddCircle24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type NovoRegistroHhtMensal, type Obra, type RegistroHhtMensal } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const nomesMes = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

function novoInicial(): NovoRegistroHhtMensal {
  const agora = new Date();
  return { obraId: '', ano: agora.getFullYear(), mes: agora.getMonth() + 1, horasHomemTrabalhadas: 0 };
}

export function HhtMensalTab({ obras }: { obras: Obra[] }) {
  const estilos = usePageStyles();
  const [registros, setRegistros] = useState<RegistroHhtMensal[]>([]);
  const [novo, setNovo] = useState<NovoRegistroHhtMensal>(novoInicial());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setRegistros(await api.registrosHht.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar registros de HHT.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!novo.obraId) {
      setErro('Selecione a obra.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.registrosHht.criar(novo);
      setNovo(novoInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar HHT.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      setErro(null);
      await api.registrosHht.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir registro.');
    }
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Lançar HHT do mês</Text>
        </div>
        <div className={estilos.form}>
          <Field label="Obra" required>
            <Select value={novo.obraId} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Ano" required>
            <Input
              type="number"
              value={String(novo.ano)}
              onChange={(_, d) => setNovo({ ...novo, ano: Number(d.value) || novo.ano })}
            />
          </Field>
          <Field label="Mês" required>
            <Select value={String(novo.mes)} onChange={(_, d) => setNovo({ ...novo, mes: Number(d.value) })}>
              {nomesMes.map((nome, indice) => (
                <option key={nome} value={indice + 1}>
                  {nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Horas-Homem Trabalhadas (HHT)" required>
            <Input
              type="number"
              min={0}
              value={String(novo.horasHomemTrabalhadas)}
              onChange={(_, d) => setNovo({ ...novo, horasHomemTrabalhadas: Number(d.value) || 0 })}
            />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
            Lançar
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Histórico de HHT por obra</Text>
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Ano</TableHeaderCell>
              <TableHeaderCell>Mês</TableHeaderCell>
              <TableHeaderCell>HHT</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {registros.map((registro) => (
              <TableRow key={registro.id}>
                <TableCell>{registro.obraNome ?? '—'}</TableCell>
                <TableCell>{registro.ano}</TableCell>
                <TableCell>{nomesMes[registro.mes - 1]}</TableCell>
                <TableCell>{registro.horasHomemTrabalhadas.toLocaleString('pt-BR')}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => excluir(registro.id)}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Adicionar abas internas em `AcidentesPage.tsx`**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/acidentes/AcidentesPage.tsx`, os imports:

```typescript
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Field,
  Input,
  Select,
  Tab,
  TabList,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
  type SelectTabData,
  type SelectTabEvent,
} from '@fluentui/react-components';
import { AddCircle24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
import {
  api,
  GravidadeAcidente,
  gravidadeAcidenteLabel,
  statusAcidenteLabel,
  tipoOcorrenciaLabel,
  type Acidente,
  type Atividade,
  type NovoAcidente,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { HhtMensalTab } from './HhtMensalTab';
```

- [ ] **Step 3: Adicionar `gravidade`/`diasDebitadosInformados` em `novaInicial`**

```typescript
function novaInicial(): NovoAcidente {
  return {
    tipo: 1,
    obraId: '',
    trabalhadorId: '',
    atividadeId: '',
    local: '',
    data: '',
    hora: '',
    descricao: '',
    lesao: '',
    consequencia: '',
    atendimento: '',
    houveAfastamento: false,
    diasAfastamento: undefined,
    numeroCat: '',
    causas: '',
    gravidade: GravidadeAcidente.SemAfastamento,
    diasDebitadosInformados: undefined,
  };
}
```

- [ ] **Step 4: Adicionar estado de aba e envolver o conteúdo atual**

No corpo de `AcidentesPage`, logo após `const [carregando, setCarregando] = useState(false);`:

```typescript
  const [aba, setAba] = useState<'ocorrencias' | 'hht'>('ocorrencias');
```

Envolver o `return (...)` existente: manter o `{erro && ...}` fora, adicionar a `TabList` antes do
formulário, e condicionar a renderização do formulário+tabela atuais a `aba === 'ocorrencias'`,
com `<HhtMensalTab obras={obras} />` quando `aba === 'hht'`:

```typescript
  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, d: SelectTabData) => setAba(d.value as 'ocorrencias' | 'hht')}
        style={{ marginBottom: 16 }}
      >
        <Tab value="ocorrencias">Acidentes & Incidentes</Tab>
        <Tab value="hht">HHT Mensal</Tab>
      </TabList>

      {aba === 'hht' && <HhtMensalTab obras={obras} />}

      {aba === 'ocorrencias' && (
        <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
            {/* ... conteúdo do formulário existente, sem mudanças de estrutura ... */}
          </div>

          <div className={estilos.card}>
            {/* ... conteúdo da tabela existente, sem mudanças de estrutura ... */}
          </div>
        </>
      )}
    </div>
  );
```

(O `{erro && ...}` deixa de ficar duplicado — como já é renderizado por `HhtMensalTab` também, é
aceitável manter os dois: cada aba trata seu próprio erro local; o `erro` de nível de página
continua cobrindo a carga inicial de `obras`/`trabalhadores`/`atividades`/`acidentes`.)

- [ ] **Step 5: Adicionar o campo "Gravidade" no formulário**

Dentro do `<div className={estilos.form}>` existente, logo após o `Field` de "Houve
afastamento?"/"Dias de afastamento" e antes do `Field` de "Número da CAT":

```typescript
          <Field label="Gravidade" required>
            <Select
              value={String(nova.gravidade)}
              onChange={(_, d) =>
                setNova({ ...nova, gravidade: Number(d.value), diasDebitadosInformados: undefined })
              }
            >
              {Object.entries(gravidadeAcidenteLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          {nova.gravidade === GravidadeAcidente.IncapacidadePermanenteParcial && (
            <Field
              label="Dias Debitados (consultar Quadro III da NBR 14280)"
              required
              hint="Valor não calculado automaticamente pelo sistema — consulte a tabela oficial de Dias Debitados por lesão/parte do corpo."
            >
              <Input
                type="number"
                min={1}
                value={nova.diasDebitadosInformados?.toString() ?? ''}
                onChange={(_, d) =>
                  setNova({ ...nova, diasDebitadosInformados: d.value ? Number(d.value) : undefined })
                }
              />
            </Field>
          )}
          {(nova.gravidade === GravidadeAcidente.Obito ||
            nova.gravidade === GravidadeAcidente.IncapacidadePermanenteTotal) && (
            <Field label="Dias Debitados">
              <Text>6.000 dias (fixo, calculado automaticamente)</Text>
            </Field>
          )}
```

- [ ] **Step 6: Adicionar a coluna "Gravidade" na tabela e exibir na criação**

No `TableHeader`, após `<TableHeaderCell>Status</TableHeaderCell>`:

```typescript
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Gravidade</TableHeaderCell>
```

No `TableBody`, após a célula de status:

```typescript
                <TableCell>
                  <Badge appearance="tint">{statusAcidenteLabel[acidente.status]}</Badge>
                </TableCell>
                <TableCell>{gravidadeAcidenteLabel[acidente.gravidade]}</TableCell>
```

- [ ] **Step 7: Verificar o build TypeScript**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit`
Expected: nenhum erro de tipo.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/acidentes
git commit -m "feat: adiciona campo Gravidade e aba de HHT mensal em Acidentes"
```

---

### Task 8: Frontend — Card de Taxa de Gravidade no Painel Inicial

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/components/dashboard/TaxaGravidadeCard.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/DashboardPage.tsx`

**Interfaces:**
- Consumes: `api.acidentes.listar()`, `api.registrosHht.listar()` (Task 6), `designTokens` (theme.ts)
- Produces: `<TaxaGravidadeCard acidentes={...} registrosHht={...} />`

- [ ] **Step 1: Criar `TaxaGravidadeCard.tsx`**

Create `src/AAHBRANT.SST.TeamsApp/src/components/dashboard/TaxaGravidadeCard.tsx`:

```typescript
import { useEffect, useMemo, useState } from 'react';
import { motion } from 'framer-motion';
import { Badge, Input, Text, Tooltip } from '@fluentui/react-components';
import type { Acidente, RegistroHhtMensal } from '../../lib/api';
import { usePageStyles } from '../../pages/pageStyles';
import { useDashboardStyles } from './dashboardStyles';
import { designTokens } from '../../theme';

const CHAVE_META_LOCALSTORAGE = 'sst.tg.metaTaxaGravidade';

interface TaxaGravidadeCardProps {
  acidentes: Acidente[];
  registrosHht: RegistroHhtMensal[];
}

// TG = (Dias Perdidos + Dias Debitados) × 1.000.000 / HHT — NBR 14280. Cálculo client-side,
// consistente com todos os outros KPIs do app (nenhum endpoint de agregação dedicado).
// A meta de comparação é um valor de negócio que este sistema não pode inventar — fica salva em
// localStorage, definida pelo próprio usuário no card (decisão de 2026-08-26).
export function TaxaGravidadeCard({ acidentes, registrosHht }: TaxaGravidadeCardProps) {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();
  const [meta, setMeta] = useState<number | null>(null);
  const [editandoMeta, setEditandoMeta] = useState(false);
  const [rascunhoMeta, setRascunhoMeta] = useState('');

  useEffect(() => {
    try {
      const salvo = window.localStorage.getItem(CHAVE_META_LOCALSTORAGE);
      if (salvo) setMeta(Number(salvo));
    } catch {
      // localStorage indisponível (ex.: modo privado) — segue sem meta salva.
    }
  }, []);

  function salvarMeta() {
    const valor = Number(rascunhoMeta);
    if (!rascunhoMeta || Number.isNaN(valor) || valor <= 0) {
      setEditandoMeta(false);
      return;
    }
    setMeta(valor);
    try {
      window.localStorage.setItem(CHAVE_META_LOCALSTORAGE, String(valor));
    } catch {
      // Segue apenas em memória se localStorage indisponível.
    }
    setEditandoMeta(false);
  }

  const { diasPerdidos, diasDebitados, hht, taxaGravidade } = useMemo(() => {
    const perdidos = acidentes.reduce((soma, a) => soma + (a.diasAfastamento ?? 0), 0);
    const debitados = acidentes.reduce((soma, a) => soma + (a.diasDebitados ?? 0), 0);
    const horas = registrosHht.reduce((soma, r) => soma + r.horasHomemTrabalhadas, 0);
    const tg = horas > 0 ? ((perdidos + debitados) * 1_000_000) / horas : null;
    return { diasPerdidos: perdidos, diasDebitados: debitados, hht: horas, taxaGravidade: tg };
  }, [acidentes, registrosHht]);

  const dentroDaMeta = meta !== null && taxaGravidade !== null ? taxaGravidade <= meta : null;

  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
      className={estilosPagina.card}
    >
      <Tooltip
        content={
          hht > 0
            ? `HHT: ${hht.toLocaleString('pt-BR')} h · Dias perdidos: ${diasPerdidos} · Dias debitados: ${diasDebitados}`
            : 'Sem lançamento de HHT — não é possível calcular a Taxa de Gravidade.'
        }
        relationship="description"
      >
        <div>
          <div className={estilos.kpiValor} style={{ color: designTokens.colorPrimary }}>
            {taxaGravidade !== null ? taxaGravidade.toFixed(2) : '—'}
          </div>
          <div className={estilos.kpiRotulo}>Taxa de Gravidade (NBR 14280)</div>
        </div>
      </Tooltip>

      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8, flexWrap: 'wrap' }}>
        {renderBadgeMeta(dentroDaMeta)}

        {editandoMeta ? (
          <Input
            size="small"
            type="number"
            min={1}
            autoFocus
            value={rascunhoMeta}
            onChange={(_, d) => setRascunhoMeta(d.value)}
            onBlur={salvarMeta}
            onKeyDown={(e) => e.key === 'Enter' && salvarMeta()}
            style={{ width: 90 }}
          />
        ) : (
          <Text
            size={200}
            style={{ color: designTokens.colorNeutralMedium, cursor: 'pointer', textDecoration: 'underline' }}
            onClick={() => {
              setRascunhoMeta(meta !== null ? String(meta) : '');
              setEditandoMeta(true);
            }}
          >
            {meta !== null ? `Meta: ${meta}` : 'Definir meta'}
          </Text>
        )}
      </div>
    </motion.div>
  );
}

function renderBadgeMeta(dentroDaMeta: boolean | null) {
  if (dentroDaMeta === null) return null;
  return dentroDaMeta ? (
    <Badge appearance="filled" color="success">
      Dentro da meta
    </Badge>
  ) : (
    <Badge appearance="filled" color="danger">
      Acima da meta
    </Badge>
  );
}
```

- [ ] **Step 2: Integrar no `DashboardPage.tsx`**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/DashboardPage.tsx`, adicionar o import e o novo fetch:

```typescript
import { useEffect, useState, type ReactElement } from 'react';
import { Text, Title3 } from '@fluentui/react-components';
import {
  BuildingBank24Regular,
  Person24Regular,
  Warning24Regular,
  DocumentLock24Regular,
  ClipboardTaskListLtr24Regular,
} from '@fluentui/react-icons';
import { api, StatusApr, StatusPt, type Acidente, type RegistroHhtMensal } from '../lib/api';
import { CardGrid } from '../layout/AppShell';
import { usePageStyles } from './pageStyles';
import { designTokens } from '../theme';
import { TaxaGravidadeCard } from '../components/dashboard/TaxaGravidadeCard';
```

No corpo do componente, adicionar estado para os dados da TG:

```typescript
export function DashboardPage() {
  const estilos = usePageStyles();
  const [kpis, setKpis] = useState<Kpi[]>(kpisIniciais);
  const [acidentes, setAcidentes] = useState<Acidente[]>([]);
  const [registrosHht, setRegistrosHht] = useState<RegistroHhtMensal[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      api.obras.listar(),
      api.trabalhadores.listar(),
      api.asos.listar(),
      api.treinamentos.listar(),
      api.permissoesTrabalho.listar(),
      api.aprs.listar(),
      api.acidentes.listar(),
      api.registrosHht.listar(),
    ])
      .then(([obras, trabalhadores, asos, treinamentos, pts, aprs, acidentesLista, registrosHhtLista]) => {
        const hoje = new Date().toISOString().slice(0, 10);
        const asosVencidos = asos.filter((a) => a.dataValidade < hoje).length;
        const treinamentosVencidos = treinamentos.filter((t) => t.dataValidade < hoje).length;
        const ptsAbertas = pts.filter((pt) => pt.status !== StatusPt.Encerrada).length;
        const aprsAguardando = aprs.filter((apr) => apr.status === StatusApr.AguardandoAprovacao).length;

        setKpis([
          { rotulo: 'Obras ativas', valor: obras.length, icone: <BuildingBank24Regular /> },
          { rotulo: 'Trabalhadores ativos', valor: trabalhadores.length, icone: <Person24Regular /> },
          { rotulo: 'ASOs vencidos', valor: asosVencidos, icone: <Warning24Regular /> },
          { rotulo: 'Treinamentos vencidos', valor: treinamentosVencidos, icone: <Warning24Regular /> },
          { rotulo: 'PTs abertas', valor: ptsAbertas, icone: <DocumentLock24Regular /> },
          { rotulo: 'APRs aguardando aprovação', valor: aprsAguardando, icone: <ClipboardTaskListLtr24Regular /> },
        ]);
        setAcidentes(acidentesLista);
        setRegistrosHht(registrosHhtLista);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar indicadores.'));
  }, []);

  return (
    <div>
      {erro && (
        <Text className={estilos.erro}>
          Não foi possível conectar à API ({erro}). Verifique se o backend está rodando localmente.
        </Text>
      )}
      <CardGrid>
        {kpis.map((kpi) => (
          <div key={kpi.rotulo} className={estilos.card} style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <div style={{ color: designTokens.colorPrimary }}>{kpi.icone}</div>
            <Title3>{kpi.valor ?? '—'}</Title3>
            <Text style={{ color: designTokens.colorNeutralMedium }}>{kpi.rotulo}</Text>
          </div>
        ))}
        <TaxaGravidadeCard acidentes={acidentes} registrosHht={registrosHht} />
      </CardGrid>
    </div>
  );
}
```

- [ ] **Step 3: Verificar o build TypeScript**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc --noEmit`
Expected: nenhum erro de tipo.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/components/dashboard/TaxaGravidadeCard.tsx src/AAHBRANT.SST.TeamsApp/src/pages/DashboardPage.tsx
git commit -m "feat: adiciona card de Taxa de Gravidade (NBR 14280) ao Painel Inicial"
```

---

### Task 9: Verificação end-to-end

**Files:** nenhum (só build + verificação manual)

- [ ] **Step 1: Build completo do backend**

Run: `dotnet build`
Expected: Build succeeded, 0 erros.

- [ ] **Step 2: Rodar toda a suíte de testes**

Run: `dotnet test`
Expected: todos os testes existentes continuam passando + os novos `TabelaDiasDebitadosTests`.

- [ ] **Step 3: Subir a API localmente e confirmar o seed do RBAC**

Run a API local (`dotnet run --project src/AAHBRANT.SST.Api`) e confirmar nos logs/õu numa consulta
que os códigos `hht:ver`, `hht:criar`, `hht:editar`, `hht:excluir` foram inseridos em `Permissoes`
pelo `RbacSeeder` no start.

- [ ] **Step 4: Verificação visual no navegador (Browser pane)**

Com a API e o frontend (`npm run dev` no TeamsApp) no ar:
1. Abrir `AcidentesPage`, criar um acidente com Gravidade = "Com afastamento" e Dias de afastamento
   preenchido — confirmar que a linha na tabela mostra a Gravidade correta.
2. Criar um acidente com Gravidade = "Incapacidade permanente parcial" sem preencher Dias
   Debitados — confirmar que a validação bloqueia o envio.
3. Preencher Dias Debitados e reenviar — confirmar sucesso.
4. Ir na aba "HHT Mensal", lançar um registro de HHT para uma obra/mês.
5. Abrir o Painel Inicial (`DashboardPage`) — confirmar que o card "Taxa de Gravidade" aparece,
   mostra um valor numérico (não "—", já que há HHT lançado), e que o tooltip ao passar o mouse
   mostra HHT/Dias perdidos/Dias debitados.
6. Clicar em "Definir meta", digitar um valor, confirmar (Enter ou clicar fora) — confirmar que o
   badge "Dentro da meta"/"Acima da meta" aparece com a cor certa, e que recarregar a página
   mantém a meta salva (localStorage).

- [ ] **Step 5: Commit final (se houver ajustes da verificação)**

```bash
git add -A
git commit -m "fix: ajustes pós-verificação da Taxa de Gravidade"
```

(Só criar este commit se a verificação do Step 4 exigir correções — caso contrário, não há nada
a commitar aqui.)

---

## Self-Review

**1. Cobertura da especificação:**
- Viabilidade dos dados (HHT/Dias Perdidos/Dias Debitados) → Task 1 (Dias Debitados) + Task 2/3/4
  (HHT) + reuso de `Acidente.DiasAfastamento` já existente para Dias Perdidos.
- Localização no código (Painel Inicial) → Task 8, `DashboardPage.tsx` confirmado como o arquivo
  correto (não um novo `AcidentesDashboardTab.tsx`).
- Fórmula NBR 14280 + divisão por zero → `TaxaGravidadeCard.tsx` (Task 8, Step 1): `hht > 0 ? ... :
  null`, renderizado como "—" quando `null`.
- Card destacado + cor do tema → Task 8 (`designTokens.colorPrimary`).
- Badge verde/vermelho vs. meta → Task 8 (`dentouDaMetaBadge`, meta em localStorage).
- Tooltip com HHT e Dias Perdidos → Task 8 (`Tooltip` com `diasPerdidos`/`diasDebitados`/`hht`).
- Endpoints/schemas para registrar dados mensais de SST por obra → Task 2/3/4 (`RegistroHhtMensal`
  CRUD completo) + Task 1/5 (`Gravidade`/`DiasDebitados` em `Acidente`, que já tinha o CRUD de
  incidentes/dias de afastamento — só precisava dos campos novos).

**2. Placeholder scan:** nenhum "TBD"/"implementar depois" encontrado; todos os steps de código têm
o código completo, sem trechos "similar à Task N" sem repetir o conteúdo (a única exceção
deliberada e sinalizada é o Step 3 da Task 5, que diz explicitamente qual é a mudança e onde, por
ser idêntica à Step 2 exceto por um campo a mais no record — mas ainda assim mostra o snippet
exato do handler).

**3. Consistência de tipos:**
- `GravidadeAcidente` (backend, `int` enum) ⇄ `gravidade: number` (frontend) — consistente com o
  padrão já usado para `StatusAcidente`/`TipoOcorrencia` no mesmo arquivo.
- `DiasDebitadosInformados` (nome usado em `CriarAcidenteCommand`/`AtualizarAcidenteCommand`/
  `AtualizarAcidenteRequestBody`) ⇄ `diasDebitadosInformados` (frontend, `NovoAcidente`) — mesmo
  nome em todas as camadas.
- `DiasDebitados` (campo final calculado, `AcidenteDto`/`Acidente`) ⇄ `diasDebitados` (frontend,
  somente leitura em `Acidente`, nunca enviado em `NovoAcidente`) — consistente.
- `RegistroHhtMensal`/`NovoRegistroHhtMensal` (frontend) espelham exatamente
  `RegistroHhtMensalDto`/`CriarRegistroHhtMensalCommand` (backend): `obraId, ano, mes,
  horasHomemTrabalhadas`.
- `api.registrosHht.*` (Task 6) é consumido em `HhtMensalTab.tsx` (Task 7) e `DashboardPage.tsx`
  (Task 8) com a mesma assinatura.

Nenhum gap encontrado.
