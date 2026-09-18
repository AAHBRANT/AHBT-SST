# Módulo Terceirizado Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar o módulo "Terceirizado" (empresas terceirizadas, contratos recebidos do G-Juri via webhook, pessoas vinculadas via `Trabalhador`, automação de EPI/treinamento por função, bloqueio de liberação por pendência) ao app de SST.

**Architecture:** CQRS com MediatR (Commands/Queries) sobre EF Core (`SstDbContext`), controllers ASP.NET Core finos delegando ao `IMediator`, frontend React/Vite/Fluent UI consumindo via `lib/api.ts` (sem TanStack Query — o app não adota essa lib ainda, ver `feedback_ia_consolidada_por_pessoa`/exploração de código). Três entidades novas (`Empresa`, `Contrato`, `ContratoVagaFuncao`); pessoa terceirizada continua sendo um `Trabalhador` (`Vinculo = Terceirizado`) com duas FKs novas (`EmpresaId`, `ContratoId`). Reaproveita integralmente `MatrizEpiFuncao`, `EstoqueEpi`/`MovimentacaoEstoqueEpi`, `MatrizTreinamentoFuncao` e o entidade `Alerta` — nenhuma dessas é recriada.

**Tech Stack:** .NET 8, EF Core (SQL Server / InMemory em teste), MediatR, FluentValidation, xUnit, React + Vite + Fluent UI v9, TypeScript.

**Spec:** [docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md](../specs/2026-09-18-modulo-terceirizado-design.md)

## Global Constraints

- Toda entidade nova herda `AuditableEntity` e recebe `builder.Property(x => x.RowVersion).IsRowVersion()` + `builder.HasQueryFilter(x => x.Ativo)` na configuração EF — mesmo padrão de todas as entidades existentes (ver `ObraConfiguracao`/`FuncaoConfiguracao`/`TrabalhadorConfiguracao`).
- "Excluir" nunca é hard delete — é sempre `entidade.Ativo = false` (soft delete), consistente com o resto do app (query filters globais por `Ativo`).
- Toda migration roda: `dotnet ef migrations add <Nome> --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations` (a partir da raiz do repo).
- Registro de entidades novas no `SstDbContext` é automático via `modelBuilder.ApplyConfigurationsFromAssembly` — só é preciso criar a classe `IEntityTypeConfiguration<T>`, nunca registrá-la manualmente.
- Autorização dos controllers novos usa as políticas `terceirizado:ver` / `terceirizado:criar` / `terceirizado:editar` / `terceirizado:excluir` (seedadas no Task 5). Endpoints que só estendem um módulo já existente (EPI, Treinamento) reaproveitam a política dele (`epi:editar`, `treinamento:editar`) — nunca criar uma política nova para um sub-recurso que já pertence a outro módulo.
- O webhook do G-Juri autentica via Entra ID App Role client-credentials (mesmo padrão de `Grh.LerColaboradores`/`Sst.LerFotos` em `AppRolesReconhecidas.cs`) — não via `X-Api-Key` solto (isso é só como o G-Juri protege a própria API de busca de processos, não o padrão de entrada do SST).
- Todas as migrations deste plano são aditivas (tabelas novas ou colunas nullable/com default) — nenhuma altera coluna existente de forma destrutiva.
- Trabalhar num worktree isolado (branch nova a partir de `master`) — nunca direto na pasta principal.
- Sem TanStack Query no frontend — seguir o padrão atual (`useState`/`useEffect` + `api.ts`) já usado em `FuncoesTab.tsx`/`PessoasPage.tsx`.

---

### Task 1: Entidades de domínio — Empresa, Contrato, ContratoVagaFuncao

**Files:**
- Create: `src/AAHBRANT.SST.Domain/Entidades/Terceirizado.cs`
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs` (adicionar ao final)
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs` (adicionar 3 `DbSet`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs` (adicionar 3 `DbSet`)
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/TerceirizadoConfiguracoes.cs`
- Test: nenhum teste de unidade dedicado (entidades são POCOs — mesmo padrão do EPI/Uniforme, que não têm teste de domínio para a classe em si)

**Interfaces:**
- Produces: classes `Empresa`, `Contrato`, `ContratoVagaFuncao` (`AAHBRANT.SST.Domain.Entidades`) e enums `StatusEmpresa`, `StatusContrato` (`AAHBRANT.SST.Domain.Enums`) — usadas por todas as tasks seguintes.

- [ ] **Step 1: Criar o arquivo de entidades**

Criar `src/AAHBRANT.SST.Domain/Entidades/Terceirizado.cs`:

```csharp
using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md) — Empresa é
// cadastro enxuto (sem documentos obrigatórios na v1, só anexo livre via Evidencia). Pessoa
// terceirizada NÃO é uma entidade própria: continua sendo Trabalhador (Vinculo=Terceirizado), com
// EmpresaId/ContratoId novos (ver Task 2) — decisão do brainstorming para reaproveitar ASO/EPI/
// Treinamento sem duplicar nada.
public class Empresa : AuditableEntity
{
    public string RazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }
    public string Cnpj { get; set; } = string.Empty;
    public string? TipoServicoPrestado { get; set; }
    public string? ContatoNome { get; set; }
    public string? ContatoTelefone { get; set; }
    public string? ContatoEmail { get; set; }
    public StatusEmpresa Status { get; set; } = StatusEmpresa.Ativa;

    public ICollection<Contrato> Contratos { get; set; } = new List<Contrato>();
    public ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
}

// Um contrato cobre sempre uma única Obra (decisão do brainstorming, simplifica porque
// EstoqueEpi/MatrizEpiFuncao já resolvem por Obra). Nasce só via webhook do G-Juri
// (ContratoValidadoWebhookCommand, Task 10) — não existe tela de criação manual de Contrato.
public class Contrato : AuditableEntity
{
    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string NumeroContrato { get; set; } = string.Empty;
    public DateOnly DataInicioVigencia { get; set; }
    public DateOnly DataFimVigencia { get; set; }
    public StatusContrato Status { get; set; } = StatusContrato.Validado;

    // Chave de idempotência do webhook (Task 10/11) — único; um evento repetido do G-Juri nunca
    // duplica o Contrato.
    public string GJuriContratoId { get; set; } = string.Empty;

    public ICollection<ContratoVagaFuncao> Vagas { get; set; } = new List<ContratoVagaFuncao>();
    public ICollection<Trabalhador> Trabalhadores { get; set; } = new List<Trabalhador>();
}

// "5 pedreiros, 3 eletricistas" — o contrato chega do G-Juri só com quantidade por função, nunca
// pessoas nomeadas (decisão do brainstorming). QuantidadePreenchidas é incrementada por
// CadastrarPessoaTerceirizadaCommand (Task 8) a cada pessoa cadastrada nesta vaga.
public class ContratoVagaFuncao : AuditableEntity
{
    public Guid ContratoId { get; set; }
    public Contrato? Contrato { get; set; }

    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    public int QuantidadeVagas { get; set; }
    public int QuantidadePreenchidas { get; set; }
}
```

- [ ] **Step 2: Adicionar os enums**

Ao final de `src/AAHBRANT.SST.Domain/Enums/Enums.cs`, adicionar:

```csharp

// Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md).
public enum StatusEmpresa
{
    Ativa = 1,
    Inativa = 2
}

public enum StatusContrato
{
    Validado = 1,
    Encerrado = 2,
    Cancelado = 3
}
```

- [ ] **Step 3: Registrar os `DbSet` em `IAppDbContext`**

Em `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`, adicionar logo após `DbSet<SuporteIaSolicitacao> SuporteIaSolicitacoes { get; }` (linha 137):

```csharp
    DbSet<Empresa> Empresas { get; }
    DbSet<Contrato> Contratos { get; }
    DbSet<ContratoVagaFuncao> ContratoVagasFuncao { get; }
```

- [ ] **Step 4: Registrar os `DbSet` em `SstDbContext`**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`, adicionar (mesmo padrão de `public DbSet<Funcao> Funcoes => Set<Funcao>();`, linha 21):

```csharp
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<ContratoVagaFuncao> ContratoVagasFuncao => Set<ContratoVagaFuncao>();
```

- [ ] **Step 5: Criar a configuração EF**

Criar `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/TerceirizadoConfiguracoes.cs`:

```csharp
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class EmpresaConfiguracao : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.Property(e => e.RazaoSocial).IsRequired().HasMaxLength(200);
        builder.Property(e => e.NomeFantasia).HasMaxLength(200);
        builder.Property(e => e.Cnpj).IsRequired().HasMaxLength(18);
        builder.Property(e => e.TipoServicoPrestado).HasMaxLength(200);
        builder.Property(e => e.ContatoNome).HasMaxLength(200);
        builder.Property(e => e.ContatoTelefone).HasMaxLength(30);
        builder.Property(e => e.ContatoEmail).HasMaxLength(200);
        builder.HasIndex(e => e.Cnpj).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class ContratoConfiguracao : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> builder)
    {
        builder.Property(c => c.NumeroContrato).IsRequired().HasMaxLength(50);
        builder.Property(c => c.GJuriContratoId).IsRequired().HasMaxLength(100);
        builder.HasIndex(c => c.GJuriContratoId).IsUnique();

        builder.HasOne(c => c.Empresa).WithMany(e => e.Contratos)
            .HasForeignKey(c => c.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Obra).WithMany()
            .HasForeignKey(c => c.ObraId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => c.Ativo);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class ContratoVagaFuncaoConfiguracao : IEntityTypeConfiguration<ContratoVagaFuncao>
{
    public void Configure(EntityTypeBuilder<ContratoVagaFuncao> builder)
    {
        builder.HasOne(v => v.Contrato).WithMany(c => c.Vagas)
            .HasForeignKey(v => v.ContratoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.Funcao).WithMany()
            .HasForeignKey(v => v.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.ContratoId, v.FuncaoId }).IsUnique();

        builder.HasQueryFilter(v => v.Ativo);
        builder.Property(v => v.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 6: Gerar a migration**

Run: `dotnet ef migrations add CriarModuloTerceirizado --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`

Expected: gera `Migrations/<timestamp>_CriarModuloTerceirizado.cs` criando as tabelas `Empresas`, `Contratos`, `ContratoVagasFuncao` com os índices únicos (`Cnpj`, `GJuriContratoId`, `(ContratoId, FuncaoId)`).

- [ ] **Step 7: Build**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure`
Expected: build sem erros.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Terceirizado.cs src/AAHBRANT.SST.Domain/Enums/Enums.cs src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/TerceirizadoConfiguracoes.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat(terceirizado): entidades Empresa, Contrato e ContratoVagaFuncao"
```

---

### Task 2: Vínculo de Trabalhador com Empresa/Contrato

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs` (adicionar `EmpresaId`/`ContratoId`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/OrganizacaoConfiguracoes.cs` (`TrabalhadorConfiguracao`, adicionar os dois relacionamentos)
- Test: nenhum dedicado (mesma justificativa do Task 1)

**Interfaces:**
- Consumes: `Empresa`, `Contrato` (Task 1).
- Produces: `Trabalhador.EmpresaId`/`Trabalhador.Empresa`, `Trabalhador.ContratoId`/`Trabalhador.Contrato` (nullable) — usados pelo Task 8 em diante.

- [ ] **Step 1: Adicionar as duas FKs em `Trabalhador`**

Em `src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs`, logo após `public TipoVinculo Vinculo { get; set; } = TipoVinculo.Clt;` (linha 38), adicionar:

```csharp

    // Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md) — só
    // preenchidos quando Vinculo = Terceirizado; nulos para CLT/Autonomo/Estagiario. Preenchidos por
    // CadastrarPessoaTerceirizadaCommand (Task 8), nunca editados manualmente depois.
    public Guid? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public Guid? ContratoId { get; set; }
    public Contrato? Contrato { get; set; }
```

- [ ] **Step 2: Configurar os relacionamentos**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/OrganizacaoConfiguracoes.cs`, dentro de `TrabalhadorConfiguracao.Configure`, logo após o bloco `builder.HasOne(t => t.Funcao)...` (linha 123), adicionar:

```csharp
        builder.HasOne(t => t.Empresa).WithMany()
            .HasForeignKey(t => t.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Contrato).WithMany(c => c.Trabalhadores)
            .HasForeignKey(t => t.ContratoId).OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 3: Gerar a migration**

Run: `dotnet ef migrations add AdicionarVinculoTerceirizadoEmTrabalhador --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`

Expected: migration adicionando as colunas nullable `EmpresaId`/`ContratoId` em `Trabalhadores` + suas FKs.

- [ ] **Step 4: Build**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure`
Expected: build sem erros.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/OrganizacaoConfiguracoes.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat(terceirizado): vincula Trabalhador a Empresa/Contrato"
```

---

### Task 3: Confirmação de entrega pendente de EPI

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Epi.cs` (`EntregaEpi`, adicionar `Confirmada`/`DataConfirmacao`)
- Create: `src/AAHBRANT.SST.Application/EntregasEpi/Commands/ConfirmarEntregaEpiCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/EntregasEpiController.cs` (novo endpoint)
- Test: `tests/AAHBRANT.SST.Application.Tests/EntregasEpi/ConfirmarEntregaEpiCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `EntregaEpi` (existente).
- Produces: `EntregaEpi.Confirmada` (bool), `EntregaEpi.DataConfirmacao` (DateTime?), `ConfirmarEntregaEpiCommand(Guid EntregaEpiId) : IRequest` — usado pelo Task 8 (cria `EntregaEpi` com `Confirmada=false`) e pelo frontend (Task 15, botão "Confirmar entrega").

Este campo é a peça que falta para o módulo Terceirizado poder "reservar" um EPI (Task 8: cria a `EntregaEpi` e decrementa o estoque imediatamente, igual a uma entrega manual já decrementa hoje) sem afirmar que ele já foi fisicamente entregue — a confirmação física fica para uma ação separada, feita por uma pessoa.

- [ ] **Step 1: Adicionar os dois campos em `EntregaEpi`**

Em `src/AAHBRANT.SST.Domain/Entidades/Epi.cs`, dentro da classe `EntregaEpi`, logo após `public string? Observacoes { get; set; }` (linha 45), adicionar:

```csharp

    // Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md) — quando
    // esta entrega nasce da automação de EPI (CadastrarPessoaTerceirizadaCommand, Task 8), o estoque
    // já é decrementado no ato (mesma semântica de SaidaEntrega que uma entrega manual já tem hoje),
    // mas Confirmada nasce false: ninguém confirmou que o EPI foi de fato entregue fisicamente à
    // pessoa ainda. Default true para toda entrega criada pelo fluxo manual existente (linha nunca
    // fica "pendente" por acidente) — só a automação do Terceirizado cria com false.
    public bool Confirmada { get; set; } = true;
    public DateTime? DataConfirmacao { get; set; }
```

- [ ] **Step 2: Criar o command de confirmação**

Criar `src/AAHBRANT.SST.Application/EntregasEpi/Commands/ConfirmarEntregaEpiCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record ConfirmarEntregaEpiCommand(Guid EntregaEpiId) : IRequest;

public class ConfirmarEntregaEpiCommandValidator : AbstractValidator<ConfirmarEntregaEpiCommand>
{
    public ConfirmarEntregaEpiCommandValidator()
    {
        RuleFor(x => x.EntregaEpiId).NotEmpty();
    }
}

public class ConfirmarEntregaEpiCommandHandler : IRequestHandler<ConfirmarEntregaEpiCommand>
{
    private readonly IAppDbContext _db;
    public ConfirmarEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ConfirmarEntregaEpiCommand request, CancellationToken ct)
    {
        var entrega = await _db.EntregasEpi.FirstOrDefaultAsync(x => x.Id == request.EntregaEpiId, ct)
            ?? throw new KeyNotFoundException("Entrega de EPI não encontrada.");

        if (entrega.Confirmada)
            throw new InvalidOperationException("Esta entrega já está confirmada.");

        entrega.Confirmada = true;
        entrega.DataConfirmacao = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Expor o endpoint**

Em `src/AAHBRANT.SST.Api/Controllers/EntregasEpiController.cs`, adicionar (mesma política `epi:editar` já usada pelos outros endpoints de escrita deste controller):

```csharp
    [Authorize(Policy = "epi:editar")]
    [HttpPut("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirmar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ConfirmarEntregaEpiCommand(id), ct);
        return NoContent();
    }
```

- [ ] **Step 4: Escrever os testes**

Criar `tests/AAHBRANT.SST.Application.Tests/EntregasEpi/ConfirmarEntregaEpiCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

public class ConfirmarEntregaEpiCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_EntregaPendente_MarcaConfirmadaEPreenchDataConfirmacao()
    {
        var db = CriarDb(nameof(Handle_EntregaPendente_MarcaConfirmadaEPreenchDataConfirmacao));
        var entrega = new EntregaEpi
        {
            TrabalhadorId = Guid.NewGuid(),
            CatalogoEpiId = Guid.NewGuid(),
            DataEntrega = DateTime.UtcNow,
            Quantidade = 1,
            Confirmada = false,
        };
        db.EntregasEpi.Add(entrega);
        await db.SaveChangesAsync();
        var handler = new ConfirmarEntregaEpiCommandHandler(db);

        await handler.Handle(new ConfirmarEntregaEpiCommand(entrega.Id), default);

        var atualizada = await db.EntregasEpi.FirstAsync(x => x.Id == entrega.Id);
        Assert.True(atualizada.Confirmada);
        Assert.NotNull(atualizada.DataConfirmacao);
    }

    [Fact]
    public async Task Handle_EntregaJaConfirmada_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_EntregaJaConfirmada_LancaInvalidOperationException));
        var entrega = new EntregaEpi
        {
            TrabalhadorId = Guid.NewGuid(),
            CatalogoEpiId = Guid.NewGuid(),
            DataEntrega = DateTime.UtcNow,
            Quantidade = 1,
            Confirmada = true,
        };
        db.EntregasEpi.Add(entrega);
        await db.SaveChangesAsync();
        var handler = new ConfirmarEntregaEpiCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ConfirmarEntregaEpiCommand(entrega.Id), default));
    }

    [Fact]
    public async Task Handle_EntregaInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_EntregaInexistente_LancaKeyNotFoundException));
        var handler = new ConfirmarEntregaEpiCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ConfirmarEntregaEpiCommand(Guid.NewGuid()), default));
    }
}
```

- [ ] **Step 5: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ConfirmarEntregaEpiCommandHandlerTests`
Expected: 3 testes passando.

- [ ] **Step 6: Gerar a migration**

Run: `dotnet ef migrations add AdicionarConfirmacaoEntregaEpi --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`

Expected: migration adicionando `Confirmada` (bit, default 1) e `DataConfirmacao` (datetime2, nullable) em `EntregasEpi`.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Epi.cs src/AAHBRANT.SST.Application/EntregasEpi/Commands/ConfirmarEntregaEpiCommand.cs src/AAHBRANT.SST.Api/Controllers/EntregasEpiController.cs tests/AAHBRANT.SST.Application.Tests/EntregasEpi/ConfirmarEntregaEpiCommandHandlerTests.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat(epi): confirmação de entrega pendente (base para automação do Terceirizado)"
```

---

### Task 4: Marcador de curso "Integração de Segurança"

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Treinamento.cs` (`CursoTreinamento`, adicionar `EhIntegracaoSeguranca`)
- Modify: `src/AAHBRANT.SST.Application/CursosTreinamento/Commands/CriarCursoTreinamentoCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/CursosTreinamento/Commands/AtualizarCursoTreinamentoCommand.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/CursosTreinamento/AtualizarCursoTreinamentoCommandHandlerTests.cs`

**Interfaces:**
- Produces: `CursoTreinamento.EhIntegracaoSeguranca` (bool) — consumido pelo Task 9 (`CalculadoraLiberacaoTerceirizado`) para achar qual curso do catálogo representa a integração obrigatória fixa de todo terceirizado.

Regra de negócio: no máximo um curso pode ter `EhIntegracaoSeguranca = true` ao mesmo tempo — marcar um novo desmarca automaticamente o anterior (evita a UI precisar desmarcar manualmente antes de marcar outro).

- [ ] **Step 1: Adicionar o campo em `CursoTreinamento`**

Em `src/AAHBRANT.SST.Domain/Entidades/Treinamento.cs`, logo após `public string? ConteudoProgramatico { get; set; }` (linha 15), adicionar:

```csharp

    // Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md) — marca
    // qual curso do catálogo representa a "Integração de Segurança" obrigatória para TODO
    // terceirizado, independente da função (regra fixa do módulo, não depende de
    // MatrizTreinamentoFuncao). No máximo um curso com true por vez — ver
    // AtualizarCursoTreinamentoCommandHandler/CriarCursoTreinamentoCommandHandler.
    public bool EhIntegracaoSeguranca { get; set; }
```

- [ ] **Step 2: Expor no `CriarCursoTreinamentoCommand`**

Em `src/AAHBRANT.SST.Application/CursosTreinamento/Commands/CriarCursoTreinamentoCommand.cs`, adicionar o parâmetro `bool EhIntegracaoSeguranca = false` ao final do record, atribuir `EhIntegracaoSeguranca = request.EhIntegracaoSeguranca` na criação da entidade, e — antes de salvar — se `request.EhIntegracaoSeguranca`, desmarcar qualquer curso existente que já tenha a flag:

```csharp
        if (request.EhIntegracaoSeguranca)
        {
            var atual = await _db.CursosTreinamento.Where(c => c.EhIntegracaoSeguranca).ToListAsync(ct);
            foreach (var c in atual) c.EhIntegracaoSeguranca = false;
        }
```

(mesmo bloco antes do `_db.CursosTreinamento.Add(...)` / `await _db.SaveChangesAsync(ct)` já existentes no handler).

- [ ] **Step 3: Expor no `AtualizarCursoTreinamentoCommand`**

Em `src/AAHBRANT.SST.Application/CursosTreinamento/Commands/AtualizarCursoTreinamentoCommand.cs`, o record e o handler completos passam a ser:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosTreinamento.Commands;

public record AtualizarCursoTreinamentoCommand(
    Guid Id,
    string Nome,
    string? NormaReferencia,
    int CargaHorariaMinima,
    int ValidadeEmMeses,
    string? ConteudoProgramatico = null,
    bool EhIntegracaoSeguranca = false) : IRequest;

public class AtualizarCursoTreinamentoCommandValidator : AbstractValidator<AtualizarCursoTreinamentoCommand>
{
    public AtualizarCursoTreinamentoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.CargaHorariaMinima).GreaterThan(0);
        RuleFor(x => x.ValidadeEmMeses).GreaterThan(0);
    }
}

public class AtualizarCursoTreinamentoCommandHandler : IRequestHandler<AtualizarCursoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCursoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCursoTreinamentoCommand request, CancellationToken ct)
    {
        var curso = await _db.CursosTreinamento.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Curso de treinamento não encontrado.");

        curso.Nome = request.Nome;
        curso.NormaReferencia = request.NormaReferencia;
        curso.CargaHorariaMinima = request.CargaHorariaMinima;
        curso.ValidadeEmMeses = request.ValidadeEmMeses;
        curso.ConteudoProgramatico = request.ConteudoProgramatico;

        if (request.EhIntegracaoSeguranca && !curso.EhIntegracaoSeguranca)
        {
            var outrosMarcados = await _db.CursosTreinamento
                .Where(c => c.EhIntegracaoSeguranca && c.Id != curso.Id)
                .ToListAsync(ct);
            foreach (var c in outrosMarcados) c.EhIntegracaoSeguranca = false;
        }
        curso.EhIntegracaoSeguranca = request.EhIntegracaoSeguranca;

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Escrever os testes**

Criar `tests/AAHBRANT.SST.Application.Tests/CursosTreinamento/AtualizarCursoTreinamentoCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.CursosTreinamento.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.CursosTreinamento;

public class AtualizarCursoTreinamentoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_MarcaComoIntegracaoSeguranca_DesmarcaCursoAnteriorAutomaticamente()
    {
        var db = CriarDb(nameof(Handle_MarcaComoIntegracaoSeguranca_DesmarcaCursoAnteriorAutomaticamente));
        var cursoAntigo = new CursoTreinamento { Nome = "Integração antiga", CargaHorariaMinima = 4, ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        var cursoNovo = new CursoTreinamento { Nome = "Integração nova", CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.AddRange(cursoAntigo, cursoNovo);
        await db.SaveChangesAsync();
        var handler = new AtualizarCursoTreinamentoCommandHandler(db);

        await handler.Handle(new AtualizarCursoTreinamentoCommand(
            cursoNovo.Id, cursoNovo.Nome, null, 4, 12, EhIntegracaoSeguranca: true), default);

        Assert.True((await db.CursosTreinamento.FirstAsync(c => c.Id == cursoNovo.Id)).EhIntegracaoSeguranca);
        Assert.False((await db.CursosTreinamento.FirstAsync(c => c.Id == cursoAntigo.Id)).EhIntegracaoSeguranca);
    }

    [Fact]
    public async Task Handle_CursoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_CursoInexistente_LancaKeyNotFoundException));
        var handler = new AtualizarCursoTreinamentoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AtualizarCursoTreinamentoCommand(Guid.NewGuid(), "X", null, 4, 12), default));
    }
}
```

- [ ] **Step 5: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter AtualizarCursoTreinamentoCommandHandlerTests`
Expected: 2 testes passando.

- [ ] **Step 6: Gerar a migration**

Run: `dotnet ef migrations add AdicionarMarcadorIntegracaoSegurancaCurso --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`

Expected: migration adicionando `EhIntegracaoSeguranca` (bit, default 0) em `CursosTreinamento`.

- [ ] **Step 7: Expor o campo no frontend (`api.ts`)**

Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, dentro de `export interface CursoTreinamento { ... }` (linha 220), adicionar após `conteudoProgramatico?: string | null;` (linha 226):

```typescript
  ehIntegracaoSeguranca: boolean;
```

E, dentro do bloco `cursosTreinamento: { ... }` (linha 3538), trocar `criar: (curso: NovoCursoTreinamento) => ...` por (mesma chamada, só para deixar explícito que `NovoCursoTreinamento` já inclui o novo campo via `Omit<CursoTreinamento, 'id'>` — nenhuma mudança de código é necessária aqui além da interface acima).

- [ ] **Step 8: Adicionar o controle na tela de Cursos de Treinamento**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/CursosTreinamentoTab.tsx`:

1. Importar `Switch` de `@fluentui/react-components` e `StatusChip` de `@ui` no topo do arquivo.
2. Em `cursoVazio` (linha 23), adicionar `ehIntegracaoSeguranca: false,`.
3. No `FormGrid` do formulário de criação, logo após o `Campo` de "Conteúdo programático" (linha 161-169), adicionar:

```tsx
              <Campo span={12}>
                <Switch
                  label="Este é o curso de Integração de Segurança obrigatório para todo terceirizado"
                  checked={novoCurso.ehIntegracaoSeguranca}
                  onChange={(_, d) => setNovoCurso({ ...novoCurso, ehIntegracaoSeguranca: d.checked })}
                />
              </Campo>
```

4. Adicionar uma função para marcar um curso já existente como Integração de Segurança (reaproveita `api.cursosTreinamento.atualizar`, que já expõe todos os campos):

```tsx
  async function marcarComoIntegracaoSeguranca(curso: CursoTreinamento) {
    try {
      await api.cursosTreinamento.atualizar(curso.id, { ...curso, ehIntegracaoSeguranca: true });
      await carregar();
      sucessoToast(`"${curso.nome}" agora é o curso de Integração de Segurança.`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao marcar curso como Integração de Segurança.');
    }
  }
```

(logo após a função `excluir`, linha 90).

5. Na coluna `colunas` (linha 92), adicionar uma coluna de indicação e, no `acoesLinha` do `<DataTable>` (linha 191), o botão de marcar:

```tsx
    {
      chave: 'integracaoSeguranca',
      rotulo: 'Integração de Segurança',
      render: (c) => (c.ehIntegracaoSeguranca ? <StatusChip tom="ok">Sim</StatusChip> : '—'),
    },
```

```tsx
          acoesLinha={(c) => (
            <>
              {!c.ehIntegracaoSeguranca && (
                <Button appearance="subtle" onClick={() => marcarComoIntegracaoSeguranca(c)}>
                  Marcar como Integração de Segurança
                </Button>
              )}
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(c.id)} aria-label="Excluir" />
            </>
          )}
```

- [ ] **Step 9: Verificar no navegador**

Rodar o preview do frontend, ir em Pessoas → Treinamentos → Cursos (ou onde `CursosTreinamentoTab` estiver montada), marcar um curso como Integração de Segurança e confirmar que a coluna mostra "Sim" e que marcar outro curso desmarca o anterior (recarregar a lista após marcar o segundo).

- [ ] **Step 10: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Treinamento.cs src/AAHBRANT.SST.Application/CursosTreinamento/Commands/ tests/AAHBRANT.SST.Application.Tests/CursosTreinamento/AtualizarCursoTreinamentoCommandHandlerTests.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/ src/AAHBRANT.SST.TeamsApp/src/lib/api.ts src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/CursosTreinamentoTab.tsx
git commit -m "feat(treinamento): marcador de curso de Integração de Segurança (backend + UI)"
```

---

### Task 5: CRUD de Empresa + permissões RBAC

**Files:**
- Create: `src/AAHBRANT.SST.Application/Empresas/EmpresaDto.cs`
- Create: `src/AAHBRANT.SST.Application/Empresas/Commands/CriarEmpresaCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Empresas/Commands/AtualizarEmpresaCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Empresas/Commands/ExcluirEmpresaCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Empresas/Queries/ListarEmpresasQuery.cs`
- Create: `src/AAHBRANT.SST.Application/Empresas/Queries/ObterEmpresaPorIdQuery.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/EmpresasController.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs` (adicionar 4 permissões)
- Test: `tests/AAHBRANT.SST.Application.Tests/Empresas/CriarEmpresaCommandHandlerTests.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Empresas/ExcluirEmpresaCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Empresa` (Task 1).
- Produces: `EmpresaDto(Guid Id, string RazaoSocial, string? NomeFantasia, string Cnpj, string? TipoServicoPrestado, string? ContatoNome, string? ContatoTelefone, string? ContatoEmail, string Status)` — consumido pelo frontend (Task 13/14). Políticas `terceirizado:ver`/`terceirizado:criar`/`terceirizado:editar`/`terceirizado:excluir` — consumidas por todos os controllers deste módulo (Tasks 6, 8, 9, 11, 12).

- [ ] **Step 1: Criar o DTO**

Criar `src/AAHBRANT.SST.Application/Empresas/EmpresaDto.cs`:

```csharp
namespace AAHBRANT.SST.Application.Empresas;

public record EmpresaDto(
    Guid Id,
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? TipoServicoPrestado,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail,
    string Status);
```

- [ ] **Step 2: Criar `CriarEmpresaCommand`**

Criar `src/AAHBRANT.SST.Application/Empresas/Commands/CriarEmpresaCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Commands;

public record CriarEmpresaCommand(
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? TipoServicoPrestado,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail) : IRequest<Guid>;

public class CriarEmpresaCommandValidator : AbstractValidator<CriarEmpresaCommand>
{
    public CriarEmpresaCommandValidator()
    {
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cnpj).NotEmpty().Length(14).Matches("^[0-9]+$").WithMessage("Informe o CNPJ com 14 dígitos, sem pontuação.");
    }
}

public class CriarEmpresaCommandHandler : IRequestHandler<CriarEmpresaCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEmpresaCommand request, CancellationToken ct)
    {
        var cnpjEmUso = await _db.Empresas.AnyAsync(e => e.Cnpj == request.Cnpj, ct);
        if (cnpjEmUso)
            throw new InvalidOperationException("Já existe uma empresa cadastrada com este CNPJ.");

        var empresa = new Empresa
        {
            RazaoSocial = request.RazaoSocial,
            NomeFantasia = request.NomeFantasia,
            Cnpj = request.Cnpj,
            TipoServicoPrestado = request.TipoServicoPrestado,
            ContatoNome = request.ContatoNome,
            ContatoTelefone = request.ContatoTelefone,
            ContatoEmail = request.ContatoEmail,
        };
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync(ct);
        return empresa.Id;
    }
}
```

- [ ] **Step 3: Criar `AtualizarEmpresaCommand` e `ExcluirEmpresaCommand`**

Criar `src/AAHBRANT.SST.Application/Empresas/Commands/AtualizarEmpresaCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Commands;

public record AtualizarEmpresaCommand(
    Guid Id,
    string RazaoSocial,
    string? NomeFantasia,
    string? TipoServicoPrestado,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail,
    StatusEmpresa Status) : IRequest;

public class AtualizarEmpresaCommandValidator : AbstractValidator<AtualizarEmpresaCommand>
{
    public AtualizarEmpresaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarEmpresaCommandHandler : IRequestHandler<AtualizarEmpresaCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarEmpresaCommand request, CancellationToken ct)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        empresa.RazaoSocial = request.RazaoSocial;
        empresa.NomeFantasia = request.NomeFantasia;
        empresa.TipoServicoPrestado = request.TipoServicoPrestado;
        empresa.ContatoNome = request.ContatoNome;
        empresa.ContatoTelefone = request.ContatoTelefone;
        empresa.ContatoEmail = request.ContatoEmail;
        empresa.Status = request.Status;

        await _db.SaveChangesAsync(ct);
    }
}
```

Criar `src/AAHBRANT.SST.Application/Empresas/Commands/ExcluirEmpresaCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Commands;

public record ExcluirEmpresaCommand(Guid Id) : IRequest;

public class ExcluirEmpresaCommandValidator : AbstractValidator<ExcluirEmpresaCommand>
{
    public ExcluirEmpresaCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class ExcluirEmpresaCommandHandler : IRequestHandler<ExcluirEmpresaCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirEmpresaCommand request, CancellationToken ct)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        empresa.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Criar as queries**

Criar `src/AAHBRANT.SST.Application/Empresas/Queries/ListarEmpresasQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Queries;

public record ListarEmpresasQuery : IRequest<List<EmpresaDto>>;

public class ListarEmpresasQueryHandler : IRequestHandler<ListarEmpresasQuery, List<EmpresaDto>>
{
    private readonly IAppDbContext _db;
    public ListarEmpresasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EmpresaDto>> Handle(ListarEmpresasQuery request, CancellationToken ct)
        => await _db.Empresas
            .OrderBy(e => e.RazaoSocial)
            .Select(e => new EmpresaDto(
                e.Id, e.RazaoSocial, e.NomeFantasia, e.Cnpj, e.TipoServicoPrestado,
                e.ContatoNome, e.ContatoTelefone, e.ContatoEmail, e.Status.ToString()))
            .ToListAsync(ct);
}
```

Criar `src/AAHBRANT.SST.Application/Empresas/Queries/ObterEmpresaPorIdQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Queries;

public record ObterEmpresaPorIdQuery(Guid Id) : IRequest<EmpresaDto?>;

public class ObterEmpresaPorIdQueryHandler : IRequestHandler<ObterEmpresaPorIdQuery, EmpresaDto?>
{
    private readonly IAppDbContext _db;
    public ObterEmpresaPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EmpresaDto?> Handle(ObterEmpresaPorIdQuery request, CancellationToken ct)
        => await _db.Empresas
            .Where(e => e.Id == request.Id)
            .Select(e => new EmpresaDto(
                e.Id, e.RazaoSocial, e.NomeFantasia, e.Cnpj, e.TipoServicoPrestado,
                e.ContatoNome, e.ContatoTelefone, e.ContatoEmail, e.Status.ToString()))
            .FirstOrDefaultAsync(ct);
}
```

- [ ] **Step 5: Criar o controller**

Criar `src/AAHBRANT.SST.Api/Controllers/EmpresasController.cs`:

```csharp
using AAHBRANT.SST.Application.Empresas.Commands;
using AAHBRANT.SST.Application.Empresas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmpresasController : ControllerBase
{
    private readonly IMediator _mediator;
    public EmpresasController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEmpresasQuery(), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var empresa = await _mediator.Send(new ObterEmpresaPorIdQuery(id), ct);
        return empresa is null ? NotFound() : Ok(empresa);
    }

    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarEmpresaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarEmpresaCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "terceirizado:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirEmpresaCommand(id), ct);
        return NoContent();
    }
}
```

- [ ] **Step 6: Seedar as permissões**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs`, adicionar ao final do array `CatalogoPermissoes` (antes do `};` que o fecha):

```csharp

        ("terceirizado:ver", "Terceirizado", "Ver", "Ver empresas terceirizadas, contratos e pessoas vinculadas"),
        ("terceirizado:criar", "Terceirizado", "Criar", "Cadastrar empresa terceirizada e pessoas em vagas de contrato"),
        ("terceirizado:editar", "Terceirizado", "Editar", "Editar empresa terceirizada"),
        ("terceirizado:excluir", "Terceirizado", "Excluir", "Inativar empresa terceirizada"),
```

- [ ] **Step 7: Escrever os testes**

Criar `tests/AAHBRANT.SST.Application.Tests/Empresas/CriarEmpresaCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Empresas.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Empresas;

public class CriarEmpresaCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_DadosValidos_CriaEmpresa()
    {
        var db = CriarDb(nameof(Handle_DadosValidos_CriaEmpresa));
        var handler = new CriarEmpresaCommandHandler(db);

        var id = await handler.Handle(
            new CriarEmpresaCommand("Construtora XPTO Ltda", "XPTO", "12345678000199", "Elétrica", "João", "11999990000", "joao@xpto.com"),
            default);

        var empresa = await db.Empresas.FirstAsync(e => e.Id == id);
        Assert.Equal("Construtora XPTO Ltda", empresa.RazaoSocial);
        Assert.Equal("12345678000199", empresa.Cnpj);
    }

    [Fact]
    public async Task Handle_CnpjJaCadastrado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_CnpjJaCadastrado_LancaInvalidOperationException));
        db.Empresas.Add(new Empresa { RazaoSocial = "Já existe", Cnpj = "12345678000199" });
        await db.SaveChangesAsync();
        var handler = new CriarEmpresaCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CriarEmpresaCommand("Outra", null, "12345678000199", null, null, null, null), default));
    }
}
```

Criar `tests/AAHBRANT.SST.Application.Tests/Empresas/ExcluirEmpresaCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Empresas.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Empresas;

public class ExcluirEmpresaCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_EmpresaExistente_MarcaAtivoFalse()
    {
        var db = CriarDb(nameof(Handle_EmpresaExistente_MarcaAtivoFalse));
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
        var handler = new ExcluirEmpresaCommandHandler(db);

        await handler.Handle(new ExcluirEmpresaCommand(empresa.Id), default);

        var atualizada = await db.Empresas.IgnoreQueryFilters().FirstAsync(e => e.Id == empresa.Id);
        Assert.False(atualizada.Ativo);
    }
}
```

- [ ] **Step 8: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter FullyQualifiedName~Empresas`
Expected: 3 testes passando.

- [ ] **Step 9: Build completo e migration**

Run: `dotnet build`
Expected: build sem erros (nenhuma migration nova nesta task — Empresa já existe desde o Task 1).

- [ ] **Step 10: Commit**

```bash
git add src/AAHBRANT.SST.Application/Empresas/ src/AAHBRANT.SST.Api/Controllers/EmpresasController.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs tests/AAHBRANT.SST.Application.Tests/Empresas/
git commit -m "feat(terceirizado): CRUD de Empresa + permissões RBAC"
```

---

### Task 6: Consultas de Contrato (lista por empresa + detalhe com vagas)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Contratos/ContratoDto.cs`
- Create: `src/AAHBRANT.SST.Application/Contratos/Queries/ListarContratosPorEmpresaQuery.cs`
- Create: `src/AAHBRANT.SST.Application/Contratos/Queries/ObterContratoDetalheQuery.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/ContratosController.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Contratos/ObterContratoDetalheQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `Contrato`, `ContratoVagaFuncao` (Task 1); política `terceirizado:ver` (Task 5).
- Produces: `ContratoDto`, `ContratoDetalheDto`, `VagaFuncaoDto` — consumidos pelo frontend (Task 14) e por `CadastrarPessoaTerceirizadaCommand` (Task 8, que usa as entidades diretamente, não este DTO). Não há `CriarContratoCommand` manual — Contrato só nasce via webhook (Task 10).

- [ ] **Step 1: Criar os DTOs**

Criar `src/AAHBRANT.SST.Application/Contratos/ContratoDto.cs`:

```csharp
namespace AAHBRANT.SST.Application.Contratos;

public record ContratoDto(
    Guid Id,
    Guid EmpresaId,
    Guid ObraId,
    string ObraNome,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    string Status);

public record VagaFuncaoDto(
    Guid Id,
    Guid FuncaoId,
    string FuncaoNome,
    int QuantidadeVagas,
    int QuantidadePreenchidas);

public record ContratoDetalheDto(
    Guid Id,
    Guid EmpresaId,
    string EmpresaRazaoSocial,
    Guid ObraId,
    string ObraNome,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    string Status,
    List<VagaFuncaoDto> Vagas);
```

- [ ] **Step 2: Criar as queries**

Criar `src/AAHBRANT.SST.Application/Contratos/Queries/ListarContratosPorEmpresaQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Contratos.Queries;

public record ListarContratosPorEmpresaQuery(Guid EmpresaId) : IRequest<List<ContratoDto>>;

public class ListarContratosPorEmpresaQueryHandler : IRequestHandler<ListarContratosPorEmpresaQuery, List<ContratoDto>>
{
    private readonly IAppDbContext _db;
    public ListarContratosPorEmpresaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ContratoDto>> Handle(ListarContratosPorEmpresaQuery request, CancellationToken ct)
        => await _db.Contratos
            .Where(c => c.EmpresaId == request.EmpresaId)
            .OrderByDescending(c => c.DataInicioVigencia)
            .Select(c => new ContratoDto(
                c.Id, c.EmpresaId, c.ObraId, c.Obra!.Nome, c.NumeroContrato,
                c.DataInicioVigencia, c.DataFimVigencia, c.Status.ToString()))
            .ToListAsync(ct);
}
```

Criar `src/AAHBRANT.SST.Application/Contratos/Queries/ObterContratoDetalheQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Contratos.Queries;

public record ObterContratoDetalheQuery(Guid Id) : IRequest<ContratoDetalheDto?>;

public class ObterContratoDetalheQueryHandler : IRequestHandler<ObterContratoDetalheQuery, ContratoDetalheDto?>
{
    private readonly IAppDbContext _db;
    public ObterContratoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ContratoDetalheDto?> Handle(ObterContratoDetalheQuery request, CancellationToken ct)
    {
        var contrato = await _db.Contratos
            .Where(c => c.Id == request.Id)
            .Select(c => new
            {
                c.Id, c.EmpresaId, EmpresaRazaoSocial = c.Empresa!.RazaoSocial,
                c.ObraId, ObraNome = c.Obra!.Nome, c.NumeroContrato,
                c.DataInicioVigencia, c.DataFimVigencia, c.Status,
            })
            .FirstOrDefaultAsync(ct);
        if (contrato is null) return null;

        var vagas = await _db.ContratoVagasFuncao
            .Where(v => v.ContratoId == request.Id)
            .OrderBy(v => v.Funcao!.Nome)
            .Select(v => new VagaFuncaoDto(v.Id, v.FuncaoId, v.Funcao!.Nome, v.QuantidadeVagas, v.QuantidadePreenchidas))
            .ToListAsync(ct);

        return new ContratoDetalheDto(
            contrato.Id, contrato.EmpresaId, contrato.EmpresaRazaoSocial, contrato.ObraId, contrato.ObraNome,
            contrato.NumeroContrato, contrato.DataInicioVigencia, contrato.DataFimVigencia,
            contrato.Status.ToString(), vagas);
    }
}
```

- [ ] **Step 3: Criar o controller**

Criar `src/AAHBRANT.SST.Api/Controllers/ContratosController.cs`:

```csharp
using AAHBRANT.SST.Application.Contratos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContratosController : ControllerBase
{
    private readonly IMediator _mediator;
    public ContratosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet]
    public async Task<IActionResult> ListarPorEmpresa([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarContratosPorEmpresaQuery(empresaId), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var contrato = await _mediator.Send(new ObterContratoDetalheQuery(id), ct);
        return contrato is null ? NotFound() : Ok(contrato);
    }
}
```

- [ ] **Step 4: Escrever o teste**

Criar `tests/AAHBRANT.SST.Application.Tests/Contratos/ObterContratoDetalheQueryHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Contratos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Contratos;

public class ObterContratoDetalheQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_ContratoComVagas_RetornaDetalheComVagas()
    {
        var db = CriarDb(nameof(Handle_ContratoComVagas_RetornaDetalheComVagas));
        var obra = new Obra { Codigo = "OBRA1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var contrato = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            GJuriContratoId = "gjuri-1",
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();
        db.ContratoVagasFuncao.Add(new ContratoVagaFuncao { ContratoId = contrato.Id, FuncaoId = funcao.Id, QuantidadeVagas = 5, QuantidadePreenchidas = 2 });
        await db.SaveChangesAsync();

        var handler = new ObterContratoDetalheQueryHandler(db);
        var resultado = await handler.Handle(new ObterContratoDetalheQuery(contrato.Id), default);

        Assert.NotNull(resultado);
        Assert.Equal("XPTO", resultado!.EmpresaRazaoSocial);
        Assert.Single(resultado.Vagas);
        Assert.Equal(5, resultado.Vagas[0].QuantidadeVagas);
        Assert.Equal(2, resultado.Vagas[0].QuantidadePreenchidas);
    }

    [Fact]
    public async Task Handle_ContratoInexistente_RetornaNull()
    {
        var db = CriarDb(nameof(Handle_ContratoInexistente_RetornaNull));
        var handler = new ObterContratoDetalheQueryHandler(db);

        var resultado = await handler.Handle(new ObterContratoDetalheQuery(Guid.NewGuid()), default);

        Assert.Null(resultado);
    }
}
```

- [ ] **Step 5: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter FullyQualifiedName~Contratos`
Expected: 2 testes passando.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Contratos/ src/AAHBRANT.SST.Api/Controllers/ContratosController.cs tests/AAHBRANT.SST.Application.Tests/Contratos/
git commit -m "feat(terceirizado): consultas de Contrato (lista por empresa e detalhe com vagas)"
```

---

### Task 7: Serviço de técnicos de segurança por Obra (helper reutilizável)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Alertas/ITecnicosSegurancaPorObraService.cs`
- Modify: `src/AAHBRANT.SST.Application/DependencyInjection.cs` (registrar o serviço)
- Test: `tests/AAHBRANT.SST.Application.Tests/Alertas/TecnicosSegurancaPorObraServiceTests.cs`

**Interfaces:**
- Consumes: `UsuarioPerfilObra`, `PerfilAcesso` (`Tipo == TipoPerfilAcesso.TecnicoSeguranca`), `Usuario` (existentes).
- Produces: `ITecnicosSegurancaPorObraService.ObterUsuarioIdsAsync(Guid obraId, CancellationToken ct)` — usado pelo Task 8 (alerta de estoque insuficiente) e pelo Task 11 (alerta de contrato encerrado) para achar quem notificar.

É um serviço simples (interface + implementação, não um `IRequestHandler` do MediatR) — mesmo padrão de `IEligibilityService`/`IAlertaEngineService` em `Application/DependencyInjection.cs`, registrado via `AddScoped`. Isso evita que os Tasks 8 e 11 precisem injetar `IMediator` só para chamar uma consulta interna — este repositório não usa nenhuma biblioteca de mocking (`tests/**/Fakes.cs` só tem test doubles escritos à mão), então manter a dependência como uma interface pequena e direta é o que torna os testes dos Tasks 8/11 simples de montar.

Um usuário com perfil Técnico de Segurança pode estar vinculado a uma Obra específica (`UsuarioPerfilObra.ObraId` preenchido) ou ter escopo global (`ObraId = null`, ver comentário em `UsuarioPerfilObra.cs`) — os dois casos devem ser notificados para uma Obra específica.

- [ ] **Step 1: Criar a interface e a implementação**

Criar `src/AAHBRANT.SST.Application/Alertas/ITecnicosSegurancaPorObraService.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas;

// Helper reutilizável (módulo Terceirizado, docs/superpowers/specs/2026-09-18-modulo-terceirizado-
// design.md §8) — acha quem notificar quando um evento pontual da Obra precisa de atenção do
// Técnico de Segurança (falta de estoque de EPI, contrato encerrado com gente ainda ativa). Não usa
// o Motor Central de Alertas (AlertaEngineService/IAlertaOrigemProvider) porque esses eventos não
// são "vencimento" — são disparados no ato, não numa varredura periódica.
public interface ITecnicosSegurancaPorObraService
{
    Task<List<Guid>> ObterUsuarioIdsAsync(Guid obraId, CancellationToken ct = default);
}

public class TecnicosSegurancaPorObraService : ITecnicosSegurancaPorObraService
{
    private readonly IAppDbContext _db;
    public TecnicosSegurancaPorObraService(IAppDbContext db) => _db = db;

    public async Task<List<Guid>> ObterUsuarioIdsAsync(Guid obraId, CancellationToken ct = default)
        => await _db.UsuariosPerfilObra
            .Where(v => v.PerfilAcesso!.Tipo == TipoPerfilAcesso.TecnicoSeguranca)
            .Where(v => v.ObraId == null || v.ObraId == obraId)
            .Where(v => v.Usuario!.Status == StatusUsuario.Ativo)
            .Select(v => v.UsuarioId)
            .Distinct()
            .ToListAsync(ct);
}
```

- [ ] **Step 2: Registrar no DI**

Em `src/AAHBRANT.SST.Application/DependencyInjection.cs`, adicionar o `using AAHBRANT.SST.Application.Alertas;` no topo e, junto das outras linhas `services.AddScoped<...>` (logo após `services.AddScoped<ISuporteIaTriagemService, HeuristicaSuporteIaTriagemService>();`):

```csharp
        services.AddScoped<ITecnicosSegurancaPorObraService, TecnicosSegurancaPorObraService>();
```

- [ ] **Step 3: Escrever o teste**

Criar `tests/AAHBRANT.SST.Application.Tests/Alertas/TecnicosSegurancaPorObraServiceTests.cs`:

```csharp
using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class TecnicosSegurancaPorObraServiceTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task ObterUsuarioIdsAsync_TecnicoDaObraETecnicoGlobal_RetornaAmbos_IgnoraTecnicoDeOutraObra()
    {
        var db = CriarDb(nameof(ObterUsuarioIdsAsync_TecnicoDaObraETecnicoGlobal_RetornaAmbos_IgnoraTecnicoDeOutraObra));
        var obraA = new Obra { Codigo = "A", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "B", Nome = "Obra B" };
        var perfilTecnico = new PerfilAcesso { Tipo = TipoPerfilAcesso.TecnicoSeguranca, Nome = "Técnico de Segurança" };
        var perfilEncarregado = new PerfilAcesso { Tipo = TipoPerfilAcesso.Encarregado, Nome = "Encarregado" };
        var tecnicoDaObraA = new Usuario { Nome = "Téc A", Email = "a@x.com" };
        var tecnicoGlobal = new Usuario { Nome = "Téc Global", Email = "g@x.com" };
        var tecnicoDaObraB = new Usuario { Nome = "Téc B", Email = "b@x.com" };
        var encarregadoDaObraA = new Usuario { Nome = "Encarregado A", Email = "e@x.com" };
        db.Obras.AddRange(obraA, obraB);
        db.PerfisAcesso.AddRange(perfilTecnico, perfilEncarregado);
        db.Usuarios.AddRange(tecnicoDaObraA, tecnicoGlobal, tecnicoDaObraB, encarregadoDaObraA);
        await db.SaveChangesAsync();

        db.UsuariosPerfilObra.AddRange(
            new UsuarioPerfilObra { UsuarioId = tecnicoDaObraA.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = obraA.Id },
            new UsuarioPerfilObra { UsuarioId = tecnicoGlobal.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = null },
            new UsuarioPerfilObra { UsuarioId = tecnicoDaObraB.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = obraB.Id },
            new UsuarioPerfilObra { UsuarioId = encarregadoDaObraA.Id, PerfilAcessoId = perfilEncarregado.Id, ObraId = obraA.Id });
        await db.SaveChangesAsync();

        var servico = new TecnicosSegurancaPorObraService(db);
        var resultado = await servico.ObterUsuarioIdsAsync(obraA.Id);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(tecnicoDaObraA.Id, resultado);
        Assert.Contains(tecnicoGlobal.Id, resultado);
        Assert.DoesNotContain(tecnicoDaObraB.Id, resultado);
        Assert.DoesNotContain(encarregadoDaObraA.Id, resultado);
    }
}
```

- [ ] **Step 4: Rodar o teste**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter TecnicosSegurancaPorObraServiceTests`
Expected: 1 teste passando.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Alertas/ITecnicosSegurancaPorObraService.cs src/AAHBRANT.SST.Application/DependencyInjection.cs tests/AAHBRANT.SST.Application.Tests/Alertas/
git commit -m "feat(alertas): serviço de técnicos de segurança por obra"
```

---

### Task 8: Cadastro de pessoa terceirizada na vaga + automação de EPI

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs` (adicionar `TipoAlerta.EpiEstoqueInsuficiente = 26`)
- Create: `src/AAHBRANT.SST.Application/Terceirizados/Commands/CadastrarPessoaTerceirizadaCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/ContratosController.cs` (novo endpoint)
- Test: `tests/AAHBRANT.SST.Application.Tests/Terceirizados/CadastrarPessoaTerceirizadaCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Contrato`/`ContratoVagaFuncao` (Task 1), `Trabalhador.EmpresaId/ContratoId` (Task 2), `EntregaEpi.Confirmada` (Task 3), `ITecnicosSegurancaPorObraService` (Task 7), `MatrizEpiFuncao`/`EstoqueEpi`/`MovimentacaoEstoqueEpi` (existentes).
- Produces: `CadastrarPessoaTerceirizadaCommand(Guid ContratoId, Guid FuncaoId, string Nome, string Matricula, string Cpf, DateTime DataAdmissao) : IRequest<Guid>` — usado pelo frontend (Task 14, botão "Cadastrar pessoa" na vaga). Cria o `Trabalhador` e, em seguida, para cada EPI obrigatório da função: reserva estoque + `EntregaEpi` pendente, ou alerta os técnicos de segurança da Obra se não há saldo.

- [ ] **Step 1: Adicionar o novo `TipoAlerta`**

Em `src/AAHBRANT.SST.Domain/Enums/Enums.cs`, dentro de `enum TipoAlerta`, logo após `SuporteIaDemandaTecnica = 25` (linha 127), adicionar:

```csharp
    ,
    // Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md §6) —
    // disparado no ato (não pelo Motor Central de Alertas periódico) quando não há saldo de
    // EstoqueEpi suficiente para reservar um EPI obrigatório no cadastro de uma pessoa terceirizada.
    EpiEstoqueInsuficiente = 26
```

- [ ] **Step 2: Criar o command**

Criar `src/AAHBRANT.SST.Application/Terceirizados/Commands/CadastrarPessoaTerceirizadaCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Commands;

public record CadastrarPessoaTerceirizadaCommand(
    Guid ContratoId,
    Guid FuncaoId,
    string Nome,
    string Matricula,
    string Cpf,
    DateTime DataAdmissao) : IRequest<Guid>;

public class CadastrarPessoaTerceirizadaCommandValidator : AbstractValidator<CadastrarPessoaTerceirizadaCommand>
{
    public CadastrarPessoaTerceirizadaCommandValidator()
    {
        RuleFor(x => x.ContratoId).NotEmpty();
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Matricula).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Cpf).NotEmpty().Length(11).Matches("^[0-9]+$")
            .Must(CpfValidador.EhValido).WithMessage("CPF inválido.");
        RuleFor(x => x.DataAdmissao).NotEmpty();
    }
}

public class CadastrarPessoaTerceirizadaCommandHandler : IRequestHandler<CadastrarPessoaTerceirizadaCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ITecnicosSegurancaPorObraService _tecnicosSegurancaPorObra;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;

    public CadastrarPessoaTerceirizadaCommandHandler(
        IAppDbContext db, ITecnicosSegurancaPorObraService tecnicosSegurancaPorObra, IFilaNotificacaoTeams filaNotificacaoTeams)
    {
        _db = db;
        _tecnicosSegurancaPorObra = tecnicosSegurancaPorObra;
        _filaNotificacaoTeams = filaNotificacaoTeams;
    }

    public async Task<Guid> Handle(CadastrarPessoaTerceirizadaCommand request, CancellationToken ct)
    {
        var contrato = await _db.Contratos.FirstOrDefaultAsync(c => c.Id == request.ContratoId, ct)
            ?? throw new KeyNotFoundException("Contrato não encontrado.");
        if (contrato.Status != StatusContrato.Validado)
            throw new InvalidOperationException("Este contrato não está com status Validado.");

        var vaga = await _db.ContratoVagasFuncao
            .FirstOrDefaultAsync(v => v.ContratoId == request.ContratoId && v.FuncaoId == request.FuncaoId, ct)
            ?? throw new KeyNotFoundException("Não há vaga cadastrada para esta função neste contrato.");
        if (vaga.QuantidadePreenchidas >= vaga.QuantidadeVagas)
            throw new InvalidOperationException("Não há vagas disponíveis para esta função neste contrato.");

        var funcao = await _db.Funcoes.FirstOrDefaultAsync(f => f.Id == request.FuncaoId, ct)
            ?? throw new KeyNotFoundException("Função não encontrada.");

        var trabalhador = new Trabalhador
        {
            ObraId = contrato.ObraId,
            FuncaoId = request.FuncaoId,
            Nome = request.Nome,
            Matricula = request.Matricula,
            Cpf = request.Cpf,
            Vinculo = TipoVinculo.Terceirizado,
            DataAdmissao = request.DataAdmissao,
            EmpresaId = contrato.EmpresaId,
            ContratoId = contrato.Id,
        };
        _db.Trabalhadores.Add(trabalhador);
        vaga.QuantidadePreenchidas += 1;

        var episObrigatorios = await _db.MatrizEpiFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .Select(m => m.CatalogoEpiId)
            .ToListAsync(ct);

        var alertasEstoqueInsuficiente = new List<Alerta>();
        foreach (var catalogoEpiId in episObrigatorios)
        {
            var estoque = await _db.EstoquesEpi
                .FirstOrDefaultAsync(e => e.CatalogoEpiId == catalogoEpiId && e.ObraId == contrato.ObraId, ct);
            var saldoAtual = estoque?.Saldo ?? 0;

            if (saldoAtual >= 1)
            {
                var entrega = new EntregaEpi
                {
                    TrabalhadorId = trabalhador.Id,
                    CatalogoEpiId = catalogoEpiId,
                    DataEntrega = DateTime.UtcNow,
                    Quantidade = 1,
                    MotivoTipo = MotivoEntregaEpi.Inicial,
                    Confirmada = false,
                };
                _db.EntregasEpi.Add(entrega);

                estoque!.Saldo -= 1;
                _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
                {
                    EstoqueEpiId = estoque.Id,
                    Tipo = TipoMovimentacaoEstoqueEpi.SaidaEntrega,
                    Quantidade = 1,
                    SaldoResultante = estoque.Saldo,
                    EntregaEpiId = entrega.Id,
                });
            }
            else
            {
                var catalogo = await _db.CatalogoEpis.FirstAsync(c => c.Id == catalogoEpiId, ct);
                var tecnicos = await _tecnicosSegurancaPorObra.ObterUsuarioIdsAsync(contrato.ObraId, ct);
                foreach (var usuarioId in tecnicos)
                {
                    var alerta = new Alerta
                    {
                        Tipo = TipoAlerta.EpiEstoqueInsuficiente,
                        Severidade = SeveridadeAlerta.Critico,
                        Titulo = $"Estoque insuficiente de {catalogo.Nome} para {trabalhador.Nome}",
                        Descricao = $"Saldo atual: {saldoAtual}. Necessário: 1 unidade para liberar {trabalhador.Nome} ({funcao.Nome}).",
                        EntidadeOrigemTipo = "Trabalhador",
                        EntidadeOrigemId = trabalhador.Id,
                        TrabalhadorId = trabalhador.Id,
                        ObraId = contrato.ObraId,
                        DestinatarioUsuarioId = usuarioId,
                    };
                    _db.Alertas.Add(alerta);
                    alertasEstoqueInsuficiente.Add(alerta);
                }
            }
        }

        await TratamentoCpfDuplicado.SalvarAsync(_db, ct);

        // Envio proativo no Teams — enfileira e segue em frente, mesmo princípio de AlertaEngineService.
        foreach (var alerta in alertasEstoqueInsuficiente)
        {
            await _filaNotificacaoTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao),
                ct);
        }

        return trabalhador.Id;
    }
}
```

- [ ] **Step 3: Expor o endpoint**

Em `src/AAHBRANT.SST.Api/Controllers/ContratosController.cs`, adicionar o `using AAHBRANT.SST.Application.Terceirizados.Commands;` no topo e o método:

```csharp
    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("{contratoId:guid}/vagas/{funcaoId:guid}/pessoas")]
    public async Task<IActionResult> CadastrarPessoaNaVaga(
        Guid contratoId, Guid funcaoId, CadastrarPessoaNaVagaRequest request, CancellationToken ct)
    {
        var trabalhadorId = await _mediator.Send(
            new CadastrarPessoaTerceirizadaCommand(contratoId, funcaoId, request.Nome, request.Matricula, request.Cpf, request.DataAdmissao),
            ct);
        return Ok(new { trabalhadorId });
    }
```

E, ao final do arquivo (fora da classe do controller):

```csharp
public record CadastrarPessoaNaVagaRequest(string Nome, string Matricula, string Cpf, DateTime DataAdmissao);
```

- [ ] **Step 4: Criar o fake de `ITecnicosSegurancaPorObraService`**

Criar `tests/AAHBRANT.SST.Application.Tests/Terceirizados/Fakes.cs` (mesmo padrão de `tests/AAHBRANT.SST.Application.Tests/Alertas/Fakes.cs` — este repositório não usa biblioteca de mocking):

```csharp
using AAHBRANT.SST.Application.Alertas;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class TecnicosSegurancaPorObraServiceFalso : ITecnicosSegurancaPorObraService
{
    public List<Guid> UsuarioIdsARetornar { get; set; } = new();

    public Task<List<Guid>> ObterUsuarioIdsAsync(Guid obraId, CancellationToken ct = default) =>
        Task.FromResult(UsuarioIdsARetornar);
}
```

- [ ] **Step 5: Escrever os testes**

Criar `tests/AAHBRANT.SST.Application.Tests/Terceirizados/CadastrarPessoaTerceirizadaCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class CadastrarPessoaTerceirizadaCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Obra Obra, Empresa Empresa, Funcao Funcao, Contrato Contrato, ContratoVagaFuncao Vaga, CatalogoEpi Epi)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        db.CatalogoEpis.Add(epi);
        await db.SaveChangesAsync();

        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = epi.Id });
        var contrato = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            GJuriContratoId = "gjuri-1", Status = StatusContrato.Validado,
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();
        var vaga = new ContratoVagaFuncao { ContratoId = contrato.Id, FuncaoId = funcao.Id, QuantidadeVagas = 2, QuantidadePreenchidas = 0 };
        db.ContratoVagasFuncao.Add(vaga);
        await db.SaveChangesAsync();

        return (obra, empresa, funcao, contrato, vaga, epi);
    }

    private static CadastrarPessoaTerceirizadaCommandHandler CriarHandler(
        IAppDbContext db, TecnicosSegurancaPorObraServiceFalso? tecnicos = null, FilaNotificacaoTeamsFalsa? fila = null) =>
        new(db, tecnicos ?? new TecnicosSegurancaPorObraServiceFalso(), fila ?? new FilaNotificacaoTeamsFalsa());

    [Fact]
    public async Task Handle_ComEstoqueSuficiente_CriaTrabalhadorEEntregaEpiPendente()
    {
        var db = CriarDb(nameof(Handle_ComEstoqueSuficiente_CriaTrabalhadorEEntregaEpiPendente));
        var (obra, _, funcao, contrato, vaga, epi) = await SemearAsync(db);
        db.EstoquesEpi.Add(new EstoqueEpi { CatalogoEpiId = epi.Id, ObraId = obra.Id, Saldo = 5 });
        await db.SaveChangesAsync();

        var handler = CriarHandler(db);

        var trabalhadorId = await handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "João Pedreiro", "MAT-1", "52998224725", DateTime.UtcNow),
            default);

        var trabalhador = await db.Trabalhadores.FirstAsync(t => t.Id == trabalhadorId);
        Assert.Equal(TipoVinculo.Terceirizado, trabalhador.Vinculo);
        Assert.Equal(contrato.EmpresaId, trabalhador.EmpresaId);
        Assert.Equal(contrato.Id, trabalhador.ContratoId);

        var vagaAtualizada = await db.ContratoVagasFuncao.FirstAsync(v => v.Id == vaga.Id);
        Assert.Equal(1, vagaAtualizada.QuantidadePreenchidas);

        var entrega = await db.EntregasEpi.SingleAsync(e => e.TrabalhadorId == trabalhadorId);
        Assert.False(entrega.Confirmada);

        var estoqueAtualizado = await db.EstoquesEpi.FirstAsync(e => e.CatalogoEpiId == epi.Id && e.ObraId == obra.Id);
        Assert.Equal(4, estoqueAtualizado.Saldo);
    }

    [Fact]
    public async Task Handle_SemEstoque_NaoCriaEntregaEAlertaTecnicos()
    {
        var db = CriarDb(nameof(Handle_SemEstoque_NaoCriaEntregaEAlertaTecnicos));
        var (_, _, funcao, contrato, _, epi) = await SemearAsync(db);
        var tecnicoId = Guid.NewGuid();

        var tecnicos = new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = new List<Guid> { tecnicoId } };
        var fila = new FilaNotificacaoTeamsFalsa();
        var handler = CriarHandler(db, tecnicos, fila);

        var trabalhadorId = await handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "Maria Pedreira", "MAT-2", "11144477735", DateTime.UtcNow),
            default);

        Assert.False(await db.EntregasEpi.AnyAsync(e => e.TrabalhadorId == trabalhadorId));
        var alerta = await db.Alertas.SingleAsync(a => a.TrabalhadorId == trabalhadorId);
        Assert.Equal(TipoAlerta.EpiEstoqueInsuficiente, alerta.Tipo);
        Assert.Equal(tecnicoId, alerta.DestinatarioUsuarioId);
        Assert.Single(fila.Mensagens);
    }

    [Fact]
    public async Task Handle_VagaSemDisponibilidade_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_VagaSemDisponibilidade_LancaInvalidOperationException));
        var (_, _, funcao, contrato, vaga, _) = await SemearAsync(db);
        vaga.QuantidadePreenchidas = vaga.QuantidadeVagas;
        await db.SaveChangesAsync();
        var handler = CriarHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "X", "MAT-3", "52998224725", DateTime.UtcNow), default));
    }

    [Fact]
    public async Task Handle_ContratoNaoValidado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_ContratoNaoValidado_LancaInvalidOperationException));
        var (_, _, funcao, contrato, _, _) = await SemearAsync(db);
        var contratoEntidade = await db.Contratos.FirstAsync(c => c.Id == contrato.Id);
        contratoEntidade.Status = StatusContrato.Encerrado;
        await db.SaveChangesAsync();
        var handler = CriarHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "X", "MAT-4", "52998224725", DateTime.UtcNow), default));
    }
}
```

- [ ] **Step 6: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter FullyQualifiedName~Terceirizados.CadastrarPessoaTerceirizadaCommandHandlerTests`
Expected: 4 testes passando.

- [ ] **Step 7: Build completo**

Run: `dotnet build`
Expected: build sem erros.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Enums/Enums.cs src/AAHBRANT.SST.Application/Terceirizados/Commands/CadastrarPessoaTerceirizadaCommand.cs src/AAHBRANT.SST.Api/Controllers/ContratosController.cs tests/AAHBRANT.SST.Application.Tests/Terceirizados/
git commit -m "feat(terceirizado): cadastro de pessoa na vaga + automação de EPI (reserva/alerta de estoque)"
```

---

### Task 9: Status de liberação e listagem de pessoas terceirizadas

**Files:**
- Create: `src/AAHBRANT.SST.Application/Terceirizados/CalculadoraLiberacaoTerceirizado.cs`
- Create: `src/AAHBRANT.SST.Application/Terceirizados/PessoaTerceirizadaDto.cs`
- Create: `src/AAHBRANT.SST.Application/Terceirizados/Queries/ObterStatusLiberacaoTrabalhadorQuery.cs`
- Create: `src/AAHBRANT.SST.Application/Terceirizados/Queries/ListarPessoasTerceirizadasQuery.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/TerceirizadosController.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Terceirizados/ListarPessoasTerceirizadasQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `Aso`, `Treinamento`, `MatrizTreinamentoFuncao`, `MatrizEpiFuncao`, `EntregaEpi.Confirmada` (Task 3), `CursoTreinamento.EhIntegracaoSeguranca` (Task 4), `Trabalhador.EmpresaId/ContratoId` (Task 2).
- Produces: `CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(IAppDbContext, Guid trabalhadorId, Guid funcaoId, CancellationToken)` — usado pelas duas queries deste task e reaproveitável por qualquer tela futura que precise do mesmo cálculo. `StatusLiberacaoTerceirizado` (enum), `PessoaTerceirizadaDto`, `StatusLiberacaoTerceirizadoDto` — consumidos pelo frontend (Task 15) e pelo painel de pendências (Task 11/12).

A regra de bloqueio (spec §8): pessoa é **Liberada** só quando ASO válido + Integração de Segurança válida + todos os treinamentos obrigatórios da matriz da função válidos + todos os EPIs obrigatórios da função com `EntregaEpi.Confirmada = true`. Qualquer pendência mantém a pessoa **Pendente**. A implementação abaixo faz uma consulta por pendência possível (aceitável no volume de uma obra — não é uma tabela de milhões de linhas); se o volume crescer a ponto de importar, é candidato a uma consulta única com `GROUP BY`, mas isso é otimização prematura para a v1.

- [ ] **Step 1: Criar a calculadora de pendências**

Criar `src/AAHBRANT.SST.Application/Terceirizados/CalculadoraLiberacaoTerceirizado.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados;

// Regra de bloqueio de liberação do módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-
// terceirizado-design.md §8) — lista vazia significa "sem pendência" (pessoa Liberada).
public static class CalculadoraLiberacaoTerceirizado
{
    public static async Task<List<string>> ObterPendenciasAsync(
        IAppDbContext db, Guid trabalhadorId, Guid funcaoId, CancellationToken ct = default)
    {
        var pendencias = new List<string>();
        var hoje = DateTime.UtcNow.Date;

        var asoValido = await db.Asos.AnyAsync(a => a.TrabalhadorId == trabalhadorId
            && (a.ResultadoStatus == ResultadoAso.Apto || a.ResultadoStatus == ResultadoAso.AptoComRestricao)
            && a.DataValidade.Date >= hoje, ct);
        if (!asoValido) pendencias.Add("ASO válido pendente");

        var cursoIntegracao = await db.CursosTreinamento.FirstOrDefaultAsync(c => c.EhIntegracaoSeguranca, ct);
        if (cursoIntegracao is null)
        {
            pendencias.Add("Curso de Integração de Segurança não configurado no catálogo");
        }
        else
        {
            var integracaoValida = await db.Treinamentos.AnyAsync(t => t.TrabalhadorId == trabalhadorId
                && t.CursoTreinamentoId == cursoIntegracao.Id && t.DataValidade.Date >= hoje, ct);
            if (!integracaoValida) pendencias.Add("Integração de Segurança pendente");
        }

        var treinamentosObrigatorios = await db.MatrizTreinamentoFuncoes
            .Where(m => m.FuncaoId == funcaoId)
            .Select(m => new { m.CursoTreinamentoId, Nome = m.CursoTreinamento!.Nome })
            .ToListAsync(ct);
        foreach (var curso in treinamentosObrigatorios)
        {
            var valido = await db.Treinamentos.AnyAsync(t => t.TrabalhadorId == trabalhadorId
                && t.CursoTreinamentoId == curso.CursoTreinamentoId && t.DataValidade.Date >= hoje, ct);
            if (!valido) pendencias.Add($"Treinamento obrigatório pendente: {curso.Nome}");
        }

        var episObrigatorios = await db.MatrizEpiFuncoes
            .Where(m => m.FuncaoId == funcaoId)
            .Select(m => new { m.CatalogoEpiId, Nome = m.CatalogoEpi!.Nome })
            .ToListAsync(ct);
        foreach (var epi in episObrigatorios)
        {
            var confirmada = await db.EntregasEpi.AnyAsync(e => e.TrabalhadorId == trabalhadorId
                && e.CatalogoEpiId == epi.CatalogoEpiId && e.Confirmada, ct);
            if (confirmada) continue;

            var reservada = await db.EntregasEpi.AnyAsync(e => e.TrabalhadorId == trabalhadorId
                && e.CatalogoEpiId == epi.CatalogoEpiId && !e.Confirmada, ct);
            pendencias.Add(reservada
                ? $"EPI reservado, aguardando confirmação de entrega: {epi.Nome}"
                : $"EPI não reservado (sem estoque): {epi.Nome}");
        }

        return pendencias;
    }
}
```

- [ ] **Step 2: Criar os DTOs**

Criar `src/AAHBRANT.SST.Application/Terceirizados/PessoaTerceirizadaDto.cs`:

```csharp
namespace AAHBRANT.SST.Application.Terceirizados;

public enum StatusLiberacaoTerceirizado
{
    Pendente,
    Liberada
}

public record StatusLiberacaoTerceirizadoDto(
    Guid TrabalhadorId,
    StatusLiberacaoTerceirizado Status,
    List<string> Pendencias);

public record PessoaTerceirizadaDto(
    Guid TrabalhadorId,
    string Nome,
    string? Matricula,
    Guid EmpresaId,
    string EmpresaRazaoSocial,
    Guid ContratoId,
    string NumeroContrato,
    Guid FuncaoId,
    string FuncaoNome,
    StatusLiberacaoTerceirizado Status,
    List<string> Pendencias);
```

- [ ] **Step 3: Criar as queries**

Criar `src/AAHBRANT.SST.Application/Terceirizados/Queries/ObterStatusLiberacaoTrabalhadorQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Queries;

public record ObterStatusLiberacaoTrabalhadorQuery(Guid TrabalhadorId) : IRequest<StatusLiberacaoTerceirizadoDto>;

public class ObterStatusLiberacaoTrabalhadorQueryHandler
    : IRequestHandler<ObterStatusLiberacaoTrabalhadorQuery, StatusLiberacaoTerceirizadoDto>
{
    private readonly IAppDbContext _db;
    public ObterStatusLiberacaoTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<StatusLiberacaoTerceirizadoDto> Handle(ObterStatusLiberacaoTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, trabalhador.Id, trabalhador.FuncaoId, ct);
        var status = pendencias.Count == 0 ? StatusLiberacaoTerceirizado.Liberada : StatusLiberacaoTerceirizado.Pendente;

        return new StatusLiberacaoTerceirizadoDto(trabalhador.Id, status, pendencias);
    }
}
```

Criar `src/AAHBRANT.SST.Application/Terceirizados/Queries/ListarPessoasTerceirizadasQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Queries;

public record ListarPessoasTerceirizadasQuery(Guid? EmpresaId, Guid? ContratoId) : IRequest<List<PessoaTerceirizadaDto>>;

public class ListarPessoasTerceirizadasQueryHandler : IRequestHandler<ListarPessoasTerceirizadasQuery, List<PessoaTerceirizadaDto>>
{
    private readonly IAppDbContext _db;
    public ListarPessoasTerceirizadasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PessoaTerceirizadaDto>> Handle(ListarPessoasTerceirizadasQuery request, CancellationToken ct)
    {
        var query = _db.Trabalhadores.Where(t => t.Vinculo == TipoVinculo.Terceirizado);
        if (request.EmpresaId.HasValue) query = query.Where(t => t.EmpresaId == request.EmpresaId);
        if (request.ContratoId.HasValue) query = query.Where(t => t.ContratoId == request.ContratoId);

        var trabalhadores = await query
            .OrderBy(t => t.Nome)
            .Select(t => new
            {
                t.Id, t.Nome, t.Matricula,
                EmpresaId = t.EmpresaId!.Value, EmpresaRazaoSocial = t.Empresa!.RazaoSocial,
                ContratoId = t.ContratoId!.Value, NumeroContrato = t.Contrato!.NumeroContrato,
                t.FuncaoId, FuncaoNome = t.Funcao!.Nome,
            })
            .ToListAsync(ct);

        var resultado = new List<PessoaTerceirizadaDto>();
        foreach (var t in trabalhadores)
        {
            var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, t.Id, t.FuncaoId, ct);
            var status = pendencias.Count == 0 ? StatusLiberacaoTerceirizado.Liberada : StatusLiberacaoTerceirizado.Pendente;
            resultado.Add(new PessoaTerceirizadaDto(
                t.Id, t.Nome, t.Matricula, t.EmpresaId, t.EmpresaRazaoSocial, t.ContratoId, t.NumeroContrato,
                t.FuncaoId, t.FuncaoNome, status, pendencias));
        }
        return resultado;
    }
}
```

- [ ] **Step 4: Criar o controller**

Criar `src/AAHBRANT.SST.Api/Controllers/TerceirizadosController.cs`:

```csharp
using AAHBRANT.SST.Application.Terceirizados.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/terceirizados")]
public class TerceirizadosController : ControllerBase
{
    private readonly IMediator _mediator;
    public TerceirizadosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pessoas")]
    public async Task<IActionResult> ListarPessoas([FromQuery] Guid? empresaId, [FromQuery] Guid? contratoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPessoasTerceirizadasQuery(empresaId, contratoId), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pessoas/{trabalhadorId:guid}/status")]
    public async Task<IActionResult> ObterStatus(Guid trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterStatusLiberacaoTrabalhadorQuery(trabalhadorId), ct));
}
```

- [ ] **Step 5: Escrever os testes**

Criar `tests/AAHBRANT.SST.Application.Tests/Terceirizados/ListarPessoasTerceirizadasQueryHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados;
using AAHBRANT.SST.Application.Terceirizados.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class ListarPessoasTerceirizadasQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Empresa Empresa, Contrato Contrato, Funcao Funcao, CursoTreinamento Integracao, Trabalhador Pessoa)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var integracao = new CursoTreinamento { Nome = "Integração de Segurança", CargaHorariaMinima = 4, ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.Add(integracao);
        await db.SaveChangesAsync();

        var contrato = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            GJuriContratoId = "gjuri-1",
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();

        var pessoa = new Trabalhador
        {
            ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "João", Matricula = "MAT-1", Cpf = "52998224725",
            Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow,
            EmpresaId = empresa.Id, ContratoId = contrato.Id,
        };
        db.Trabalhadores.Add(pessoa);
        await db.SaveChangesAsync();

        return (empresa, contrato, funcao, integracao, pessoa);
    }

    [Fact]
    public async Task Handle_SemAsoESemIntegracao_RetornaStatusPendenteComAsPendencias()
    {
        var db = CriarDb(nameof(Handle_SemAsoESemIntegracao_RetornaStatusPendenteComAsPendencias));
        var (empresa, _, _, _, pessoa) = await SemearAsync(db);
        var handler = new ListarPessoasTerceirizadasQueryHandler(db);

        var resultado = await handler.Handle(new ListarPessoasTerceirizadasQuery(empresa.Id, null), default);

        var dto = Assert.Single(resultado);
        Assert.Equal(pessoa.Id, dto.TrabalhadorId);
        Assert.Equal(StatusLiberacaoTerceirizado.Pendente, dto.Status);
        Assert.Contains("ASO válido pendente", dto.Pendencias);
        Assert.Contains("Integração de Segurança pendente", dto.Pendencias);
    }

    [Fact]
    public async Task Handle_ComAsoValidoEIntegracaoValida_SemMaisExigencias_RetornaLiberada()
    {
        var db = CriarDb(nameof(Handle_ComAsoValidoEIntegracaoValida_SemMaisExigencias_RetornaLiberada));
        var (empresa, _, _, integracao, pessoa) = await SemearAsync(db);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = pessoa.Id, Tipo = TipoExameAso.Admissional,
            DataExame = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddMonths(11),
            ResultadoStatus = ResultadoAso.Apto,
        });
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = pessoa.Id, CursoTreinamentoId = integracao.Id,
            DataRealizacao = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddMonths(11), CargaHorariaRealizada = 4,
        });
        await db.SaveChangesAsync();

        var handler = new ListarPessoasTerceirizadasQueryHandler(db);
        var resultado = await handler.Handle(new ListarPessoasTerceirizadasQuery(empresa.Id, null), default);

        var dto = Assert.Single(resultado);
        Assert.Equal(StatusLiberacaoTerceirizado.Liberada, dto.Status);
        Assert.Empty(dto.Pendencias);
    }
}
```

- [ ] **Step 6: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter FullyQualifiedName~Terceirizados.ListarPessoasTerceirizadasQueryHandlerTests`
Expected: 2 testes passando.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.Application/Terceirizados/ src/AAHBRANT.SST.Api/Controllers/TerceirizadosController.cs tests/AAHBRANT.SST.Application.Tests/Terceirizados/
git commit -m "feat(terceirizado): status de liberação e listagem de pessoas terceirizadas"
```

---

### Task 10: Webhook G-Juri — contrato validado

**Files:**
- Modify: `src/AAHBRANT.SST.Api/Autorizacao/AppRolesReconhecidas.cs` (nova App Role)
- Create: `src/AAHBRANT.SST.Application/Terceirizados/Commands/ContratoValidadoWebhookCommand.cs`
- Create: `src/AAHBRANT.SST.Api/Controllers/IntegracaoGJuriController.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Terceirizados/ContratoValidadoWebhookCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Empresa`, `Contrato`, `ContratoVagaFuncao` (Task 1).
- Produces: `ContratoValidadoWebhookCommand(...) : IRequest<Guid>` (retorna o `Contrato.Id`) — chamado pelo `IntegracaoGJuriController`, autenticado via Entra ID App Role `Sst.ReceberContratosGJuri`.

Autenticação: mesmo padrão de `Grh.LerColaboradores`/`Sst.LerFotos` (`AppRolesReconhecidas.cs`) — não `X-Api-Key` solto. Idempotência: um evento repetido do G-Juri (mesmo `GJuriContratoId`) nunca cria um segundo `Contrato` — a chamada repetida simplesmente retorna o `Id` já existente, sem alterar nada.

Resolução do ponto técnico aberto na spec (§10, "formato de resolução de FuncaoId"): o payload traz o `Guid` do `Funcao.Id` **do próprio SST** diretamente — ou seja, o G-Juri precisa conhecer/armazenar o Id da Função do SST ao montar o contrato (um mapeamento a ser resolvido do lado do G-Juri, fora do escopo deste plano). Se isso não for viável quando o G-Juri for implementado, o ajuste fica isolado no `ContratoValidadoWebhookCommand` (troca de `Guid FuncaoId` por um código/nome que este handler resolve para o `Funcao.Id` correspondente) — não afeta nenhuma outra parte do módulo.

- [ ] **Step 1: Registrar a nova App Role**

Em `src/AAHBRANT.SST.Api/Autorizacao/AppRolesReconhecidas.cs`, adicionar ao dicionário `PermissoesPorAppRole`:

```csharp
        // App Role definida no app registration do SST, concedida ao service principal do G-Juri —
        // dispara o webhook de contrato validado/encerrado (ver IntegracaoGJuriController). Mapeada
        // para "terceirizado:criar" porque processar o webhook cria/atualiza Empresa e Contrato,
        // mesma natureza de permissão que o cadastro manual via tela.
        ["Sst.ReceberContratosGJuri"] = new[] { "terceirizado:criar" },
```

- [ ] **Step 2: Criar o command**

Criar `src/AAHBRANT.SST.Application/Terceirizados/Commands/ContratoValidadoWebhookCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Commands;

public record EmpresaWebhookDto(
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? TipoServicoPrestado,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail);

public record VagaFuncaoWebhookDto(Guid FuncaoId, int Quantidade);

public record ContratoValidadoWebhookCommand(
    string GJuriContratoId,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    Guid ObraId,
    EmpresaWebhookDto Empresa,
    List<VagaFuncaoWebhookDto> Vagas) : IRequest<Guid>;

public class ContratoValidadoWebhookCommandValidator : AbstractValidator<ContratoValidadoWebhookCommand>
{
    public ContratoValidadoWebhookCommandValidator()
    {
        RuleFor(x => x.GJuriContratoId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NumeroContrato).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Empresa.RazaoSocial).NotEmpty();
        RuleFor(x => x.Empresa.Cnpj).NotEmpty().Length(14).Matches("^[0-9]+$");
        RuleFor(x => x.Vagas).NotEmpty().WithMessage("O contrato precisa vir com ao menos uma vaga.");
        RuleForEach(x => x.Vagas).ChildRules(v =>
        {
            v.RuleFor(x => x.FuncaoId).NotEmpty();
            v.RuleFor(x => x.Quantidade).GreaterThan(0);
        });
    }
}

public class ContratoValidadoWebhookCommandHandler : IRequestHandler<ContratoValidadoWebhookCommand, Guid>
{
    private readonly IAppDbContext _db;
    public ContratoValidadoWebhookCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(ContratoValidadoWebhookCommand request, CancellationToken ct)
    {
        var contratoExistente = await _db.Contratos.FirstOrDefaultAsync(c => c.GJuriContratoId == request.GJuriContratoId, ct);
        if (contratoExistente is not null)
            return contratoExistente.Id; // idempotente — evento repetido do G-Juri não duplica nada.

        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var funcaoIds = request.Vagas.Select(v => v.FuncaoId).Distinct().ToList();
        var funcoesEncontradas = await _db.Funcoes.Where(f => funcaoIds.Contains(f.Id)).Select(f => f.Id).ToListAsync(ct);
        var funcaoFaltante = funcaoIds.FirstOrDefault(id => !funcoesEncontradas.Contains(id));
        if (funcaoFaltante != Guid.Empty)
            throw new KeyNotFoundException($"Função {funcaoFaltante} não encontrada.");

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Cnpj == request.Empresa.Cnpj, ct);
        if (empresa is null)
        {
            empresa = new Empresa
            {
                RazaoSocial = request.Empresa.RazaoSocial,
                NomeFantasia = request.Empresa.NomeFantasia,
                Cnpj = request.Empresa.Cnpj,
                TipoServicoPrestado = request.Empresa.TipoServicoPrestado,
                ContatoNome = request.Empresa.ContatoNome,
                ContatoTelefone = request.Empresa.ContatoTelefone,
                ContatoEmail = request.Empresa.ContatoEmail,
            };
            _db.Empresas.Add(empresa);
        }

        var contrato = new Contrato
        {
            EmpresaId = empresa.Id,
            ObraId = request.ObraId,
            NumeroContrato = request.NumeroContrato,
            DataInicioVigencia = request.DataInicioVigencia,
            DataFimVigencia = request.DataFimVigencia,
            GJuriContratoId = request.GJuriContratoId,
        };
        _db.Contratos.Add(contrato);

        foreach (var vaga in request.Vagas)
        {
            _db.ContratoVagasFuncao.Add(new ContratoVagaFuncao
            {
                ContratoId = contrato.Id,
                FuncaoId = vaga.FuncaoId,
                QuantidadeVagas = vaga.Quantidade,
                QuantidadePreenchidas = 0,
            });
        }

        await _db.SaveChangesAsync(ct);
        return contrato.Id;
    }
}
```

- [ ] **Step 3: Criar o controller**

Criar `src/AAHBRANT.SST.Api/Controllers/IntegracaoGJuriController.cs`:

```csharp
using AAHBRANT.SST.Application.Terceirizados.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Canal novo, unidirecional G-Juri -> SST (docs/superpowers/specs/2026-09-18-modulo-terceirizado-
// design.md §5) — o G-Juri chama este webhook quando um contrato de terceirizada é validado ou
// encerrado. Autenticação via Entra ID App Role client-credentials (Sst.ReceberContratosGJuri, ver
// AppRolesReconhecidas), mesmo mecanismo já usado por IntegracaoGrhController.
[ApiController]
[Route("api/integracoes/gjuri")]
public class IntegracaoGJuriController : ControllerBase
{
    private readonly IMediator _mediator;
    public IntegracaoGJuriController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("contratos/validados")]
    public async Task<IActionResult> ContratoValidado(ContratoValidadoWebhookCommand command, CancellationToken ct)
    {
        var contratoId = await _mediator.Send(command, ct);
        return Ok(new { contratoId });
    }
}
```

- [ ] **Step 4: Escrever os testes**

Criar `tests/AAHBRANT.SST.Application.Tests/Terceirizados/ContratoValidadoWebhookCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class ContratoValidadoWebhookCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Obra Obra, Funcao Funcao)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();
        return (obra, funcao);
    }

    private static ContratoValidadoWebhookCommand CriarCommand(Guid obraId, Guid funcaoId, string gjuriContratoId, string cnpj = "12345678000199") =>
        new(gjuriContratoId, "CT-001",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            obraId,
            new EmpresaWebhookDto("Construtora XPTO", "XPTO", cnpj, "Elétrica", "João", "11999990000", "joao@xpto.com"),
            new List<VagaFuncaoWebhookDto> { new(funcaoId, 5) });

    [Fact]
    public async Task Handle_ContratoNovo_CriaEmpresaContratoEVagas()
    {
        var db = CriarDb(nameof(Handle_ContratoNovo_CriaEmpresaContratoEVagas));
        var (obra, funcao) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);

        var contratoId = await handler.Handle(CriarCommand(obra.Id, funcao.Id, "gjuri-1"), default);

        var contrato = await db.Contratos.FirstAsync(c => c.Id == contratoId);
        Assert.Equal("CT-001", contrato.NumeroContrato);
        var empresa = await db.Empresas.FirstAsync(e => e.Id == contrato.EmpresaId);
        Assert.Equal("12345678000199", empresa.Cnpj);
        var vaga = await db.ContratoVagasFuncao.SingleAsync(v => v.ContratoId == contratoId);
        Assert.Equal(5, vaga.QuantidadeVagas);
    }

    [Fact]
    public async Task Handle_CnpjJaCadastrado_ReaproveitaEmpresaExistente()
    {
        var db = CriarDb(nameof(Handle_CnpjJaCadastrado_ReaproveitaEmpresaExistente));
        var (obra, funcao) = await SemearAsync(db);
        var empresaExistente = new Empresa { RazaoSocial = "XPTO Já Cadastrada", Cnpj = "12345678000199" };
        db.Empresas.Add(empresaExistente);
        await db.SaveChangesAsync();
        var handler = new ContratoValidadoWebhookCommandHandler(db);

        var contratoId = await handler.Handle(CriarCommand(obra.Id, funcao.Id, "gjuri-2"), default);

        var contrato = await db.Contratos.FirstAsync(c => c.Id == contratoId);
        Assert.Equal(empresaExistente.Id, contrato.EmpresaId);
        Assert.Equal(1, await db.Empresas.CountAsync());
    }

    [Fact]
    public async Task Handle_MesmoGJuriContratoIdChamadoDuasVezes_EhIdempotente()
    {
        var db = CriarDb(nameof(Handle_MesmoGJuriContratoIdChamadoDuasVezes_EhIdempotente));
        var (obra, funcao) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);
        var primeiroId = await handler.Handle(CriarCommand(obra.Id, funcao.Id, "gjuri-3"), default);

        var segundoId = await handler.Handle(CriarCommand(obra.Id, funcao.Id, "gjuri-3"), default);

        Assert.Equal(primeiroId, segundoId);
        Assert.Equal(1, await db.Contratos.CountAsync());
        Assert.Equal(1, await db.ContratoVagasFuncao.CountAsync());
    }

    [Fact]
    public async Task Handle_ObraInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_ObraInexistente_LancaKeyNotFoundException));
        var (_, funcao) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(CriarCommand(Guid.NewGuid(), funcao.Id, "gjuri-4"), default));
    }
}
```

- [ ] **Step 5: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter FullyQualifiedName~Terceirizados.ContratoValidadoWebhookCommandHandlerTests`
Expected: 4 testes passando.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Api/Autorizacao/AppRolesReconhecidas.cs src/AAHBRANT.SST.Application/Terceirizados/Commands/ContratoValidadoWebhookCommand.cs src/AAHBRANT.SST.Api/Controllers/IntegracaoGJuriController.cs tests/AAHBRANT.SST.Application.Tests/Terceirizados/
git commit -m "feat(terceirizado): webhook G-Juri de contrato validado (idempotente)"
```

---

### Task 11: Webhook G-Juri — contrato encerrado + alerta

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs` (adicionar `TipoAlerta.ContratoTerceirizadoEncerrado = 27`)
- Create: `src/AAHBRANT.SST.Application/Terceirizados/Commands/ContratoEncerradoWebhookCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/IntegracaoGJuriController.cs` (novo endpoint)
- Test: `tests/AAHBRANT.SST.Application.Tests/Terceirizados/ContratoEncerradoWebhookCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Contrato` (Task 1), `ITecnicosSegurancaPorObraService` (Task 7).
- Produces: `ContratoEncerradoWebhookCommand(string GJuriContratoId, DateOnly DataEncerramento) : IRequest` — chamado pelo mesmo `IntegracaoGJuriController` do Task 10. Spec §8: "só alerta, não desliga ninguém" — nenhum `Trabalhador` é desativado automaticamente.

- [ ] **Step 1: Adicionar o novo `TipoAlerta`**

Em `src/AAHBRANT.SST.Domain/Enums/Enums.cs`, dentro de `enum TipoAlerta`, logo após `EpiEstoqueInsuficiente = 26` (adicionado no Task 8):

```csharp
    ,
    // Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md §8) —
    // contrato encerrado no G-Juri com pessoas ainda ativas no SST. Spec: "só alerta, não desliga
    // ninguém automaticamente" — o desligamento continua manual.
    ContratoTerceirizadoEncerrado = 27
```

- [ ] **Step 2: Criar o command**

Criar `src/AAHBRANT.SST.Application/Terceirizados/Commands/ContratoEncerradoWebhookCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Commands;

public record ContratoEncerradoWebhookCommand(string GJuriContratoId, DateOnly DataEncerramento) : IRequest;

public class ContratoEncerradoWebhookCommandValidator : AbstractValidator<ContratoEncerradoWebhookCommand>
{
    public ContratoEncerradoWebhookCommandValidator()
    {
        RuleFor(x => x.GJuriContratoId).NotEmpty();
    }
}

public class ContratoEncerradoWebhookCommandHandler : IRequestHandler<ContratoEncerradoWebhookCommand>
{
    private readonly IAppDbContext _db;
    private readonly ITecnicosSegurancaPorObraService _tecnicosSegurancaPorObra;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;

    public ContratoEncerradoWebhookCommandHandler(
        IAppDbContext db, ITecnicosSegurancaPorObraService tecnicosSegurancaPorObra, IFilaNotificacaoTeams filaNotificacaoTeams)
    {
        _db = db;
        _tecnicosSegurancaPorObra = tecnicosSegurancaPorObra;
        _filaNotificacaoTeams = filaNotificacaoTeams;
    }

    public async Task Handle(ContratoEncerradoWebhookCommand request, CancellationToken ct)
    {
        var contrato = await _db.Contratos.FirstOrDefaultAsync(c => c.GJuriContratoId == request.GJuriContratoId, ct)
            ?? throw new KeyNotFoundException($"Contrato com GJuriContratoId '{request.GJuriContratoId}' não encontrado.");

        if (contrato.Status == StatusContrato.Encerrado)
            return; // idempotente — evento repetido do G-Juri não gera alerta duplicado.

        contrato.Status = StatusContrato.Encerrado;

        var pessoasAtivas = await _db.Trabalhadores
            .Where(t => t.ContratoId == contrato.Id && t.DataDemissao == null)
            .ToListAsync(ct);

        var alertasCriados = new List<Alerta>();
        if (pessoasAtivas.Count > 0)
        {
            var tecnicos = await _tecnicosSegurancaPorObra.ObterUsuarioIdsAsync(contrato.ObraId, ct);
            foreach (var usuarioId in tecnicos)
            {
                var alerta = new Alerta
                {
                    Tipo = TipoAlerta.ContratoTerceirizadoEncerrado,
                    Severidade = SeveridadeAlerta.Atencao,
                    Titulo = $"Contrato {contrato.NumeroContrato} encerrado com {pessoasAtivas.Count} pessoa(s) ainda ativa(s)",
                    Descricao = "Revise se essas pessoas devem ser desligadas no SST — o encerramento do contrato no G-Juri não desliga ninguém automaticamente.",
                    EntidadeOrigemTipo = "Contrato",
                    EntidadeOrigemId = contrato.Id,
                    ObraId = contrato.ObraId,
                    DestinatarioUsuarioId = usuarioId,
                };
                _db.Alertas.Add(alerta);
                alertasCriados.Add(alerta);
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var alerta in alertasCriados)
        {
            await _filaNotificacaoTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao),
                ct);
        }
    }
}
```

- [ ] **Step 3: Expor o endpoint**

Em `src/AAHBRANT.SST.Api/Controllers/IntegracaoGJuriController.cs`, adicionar:

```csharp
    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("contratos/encerrados")]
    public async Task<IActionResult> ContratoEncerrado(ContratoEncerradoWebhookCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
```

- [ ] **Step 4: Escrever os testes**

Criar `tests/AAHBRANT.SST.Application.Tests/Terceirizados/ContratoEncerradoWebhookCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class ContratoEncerradoWebhookCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<Contrato> SemearContratoComPessoaAtivaAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var contrato = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            GJuriContratoId = "gjuri-1", Status = StatusContrato.Validado,
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador
        {
            ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "João", Matricula = "MAT-1", Cpf = "52998224725",
            Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow,
            EmpresaId = empresa.Id, ContratoId = contrato.Id,
        });
        await db.SaveChangesAsync();

        return contrato;
    }

    [Fact]
    public async Task Handle_ComPessoasAtivas_MarcaEncerradoEAlertaTecnicos()
    {
        var db = CriarDb(nameof(Handle_ComPessoasAtivas_MarcaEncerradoEAlertaTecnicos));
        var contrato = await SemearContratoComPessoaAtivaAsync(db);
        var tecnicoId = Guid.NewGuid();
        var tecnicos = new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = new List<Guid> { tecnicoId } };
        var fila = new FilaNotificacaoTeamsFalsa();
        var handler = new ContratoEncerradoWebhookCommandHandler(db, tecnicos, fila);

        await handler.Handle(new ContratoEncerradoWebhookCommand(contrato.GJuriContratoId, DateOnly.FromDateTime(DateTime.UtcNow)), default);

        var contratoAtualizado = await db.Contratos.FirstAsync(c => c.Id == contrato.Id);
        Assert.Equal(StatusContrato.Encerrado, contratoAtualizado.Status);
        var alerta = await db.Alertas.SingleAsync(a => a.EntidadeOrigemId == contrato.Id);
        Assert.Equal(TipoAlerta.ContratoTerceirizadoEncerrado, alerta.Tipo);
        Assert.Equal(tecnicoId, alerta.DestinatarioUsuarioId);
        Assert.Single(fila.Mensagens);

        var pessoa = await db.Trabalhadores.FirstAsync(t => t.ContratoId == contrato.Id);
        Assert.Null(pessoa.DataDemissao); // spec: só alerta, não desliga ninguém.
    }

    [Fact]
    public async Task Handle_ChamadoDuasVezes_EhIdempotente_NaoDuplicaAlerta()
    {
        var db = CriarDb(nameof(Handle_ChamadoDuasVezes_EhIdempotente_NaoDuplicaAlerta));
        var contrato = await SemearContratoComPessoaAtivaAsync(db);
        var tecnicos = new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = new List<Guid> { Guid.NewGuid() } };
        var handler = new ContratoEncerradoWebhookCommandHandler(db, tecnicos, new FilaNotificacaoTeamsFalsa());
        await handler.Handle(new ContratoEncerradoWebhookCommand(contrato.GJuriContratoId, DateOnly.FromDateTime(DateTime.UtcNow)), default);

        await handler.Handle(new ContratoEncerradoWebhookCommand(contrato.GJuriContratoId, DateOnly.FromDateTime(DateTime.UtcNow)), default);

        Assert.Equal(1, await db.Alertas.CountAsync(a => a.EntidadeOrigemId == contrato.Id));
    }

    [Fact]
    public async Task Handle_ContratoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_ContratoInexistente_LancaKeyNotFoundException));
        var handler = new ContratoEncerradoWebhookCommandHandler(
            db, new TecnicosSegurancaPorObraServiceFalso(), new FilaNotificacaoTeamsFalsa());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ContratoEncerradoWebhookCommand("inexistente", DateOnly.FromDateTime(DateTime.UtcNow)), default));
    }
}
```

- [ ] **Step 5: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter FullyQualifiedName~Terceirizados.ContratoEncerradoWebhookCommandHandlerTests`
Expected: 3 testes passando.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Enums/Enums.cs src/AAHBRANT.SST.Application/Terceirizados/Commands/ContratoEncerradoWebhookCommand.cs src/AAHBRANT.SST.Api/Controllers/IntegracaoGJuriController.cs tests/AAHBRANT.SST.Application.Tests/Terceirizados/
git commit -m "feat(terceirizado): webhook G-Juri de contrato encerrado + alerta"
```

---

### Task 12: Painel de pendências consolidado

**Files:**
- Create: `src/AAHBRANT.SST.Application/Terceirizados/PainelPendenciasTerceirizadoDto.cs`
- Create: `src/AAHBRANT.SST.Application/Terceirizados/Queries/ListarPendenciasTerceirizadoQuery.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/TerceirizadosController.cs` (novo endpoint)
- Test: `tests/AAHBRANT.SST.Application.Tests/Terceirizados/ListarPendenciasTerceirizadoQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `CalculadoraLiberacaoTerceirizado` (Task 9), `Contrato`/`Trabalhador.DataDemissao` (existentes), `Alerta`/`TipoAlerta.EpiEstoqueInsuficiente` (Task 8).
- Produces: `PainelPendenciasTerceirizadoDto` — três listas: pessoas bloqueadas (e por quê), contratos encerrados com gente ainda ativa, e alertas de estoque insuficiente ainda abertos. Consumido pelo frontend (Task 15).

- [ ] **Step 1: Criar os DTOs**

Criar `src/AAHBRANT.SST.Application/Terceirizados/PainelPendenciasTerceirizadoDto.cs`:

```csharp
namespace AAHBRANT.SST.Application.Terceirizados;

public record PendenciaPessoaDto(
    Guid TrabalhadorId,
    string Nome,
    Guid EmpresaId,
    string EmpresaRazaoSocial,
    List<string> Pendencias);

public record ContratoEncerradoComPessoasAtivasDto(
    Guid ContratoId,
    string NumeroContrato,
    string EmpresaRazaoSocial,
    int QuantidadePessoasAtivas);

public record AlertaEstoqueInsuficienteDto(
    Guid AlertaId,
    string Titulo,
    string? Descricao,
    DateTime CriadoEmUtc);

public record PainelPendenciasTerceirizadoDto(
    List<PendenciaPessoaDto> PessoasBloqueadas,
    List<ContratoEncerradoComPessoasAtivasDto> ContratosEncerradosComPessoasAtivas,
    List<AlertaEstoqueInsuficienteDto> AlertasEstoqueInsuficiente);
```

- [ ] **Step 2: Criar a query**

Criar `src/AAHBRANT.SST.Application/Terceirizados/Queries/ListarPendenciasTerceirizadoQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Queries;

public record ListarPendenciasTerceirizadoQuery : IRequest<PainelPendenciasTerceirizadoDto>;

public class ListarPendenciasTerceirizadoQueryHandler : IRequestHandler<ListarPendenciasTerceirizadoQuery, PainelPendenciasTerceirizadoDto>
{
    private readonly IAppDbContext _db;
    public ListarPendenciasTerceirizadoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PainelPendenciasTerceirizadoDto> Handle(ListarPendenciasTerceirizadoQuery request, CancellationToken ct)
    {
        var terceirizados = await _db.Trabalhadores
            .Where(t => t.Vinculo == TipoVinculo.Terceirizado)
            .Select(t => new { t.Id, t.Nome, t.FuncaoId, EmpresaId = t.EmpresaId!.Value, EmpresaRazaoSocial = t.Empresa!.RazaoSocial })
            .ToListAsync(ct);

        var pessoasBloqueadas = new List<PendenciaPessoaDto>();
        foreach (var t in terceirizados)
        {
            var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, t.Id, t.FuncaoId, ct);
            if (pendencias.Count > 0)
                pessoasBloqueadas.Add(new PendenciaPessoaDto(t.Id, t.Nome, t.EmpresaId, t.EmpresaRazaoSocial, pendencias));
        }

        var contratosEncerrados = await _db.Contratos
            .Where(c => c.Status == StatusContrato.Encerrado)
            .Select(c => new ContratoEncerradoComPessoasAtivasDto(
                c.Id, c.NumeroContrato, c.Empresa!.RazaoSocial,
                c.Trabalhadores.Count(t => t.DataDemissao == null)))
            .Where(c => c.QuantidadePessoasAtivas > 0)
            .ToListAsync(ct);

        var alertasEstoqueInsuficiente = await _db.Alertas
            .Where(a => a.Tipo == TipoAlerta.EpiEstoqueInsuficiente && a.Status == StatusAlerta.Aberto)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AlertaEstoqueInsuficienteDto(a.Id, a.Titulo, a.Descricao, a.CreatedAtUtc))
            .ToListAsync(ct);

        return new PainelPendenciasTerceirizadoDto(pessoasBloqueadas, contratosEncerrados, alertasEstoqueInsuficiente);
    }
}
```

- [ ] **Step 3: Expor o endpoint**

Em `src/AAHBRANT.SST.Api/Controllers/TerceirizadosController.cs`, adicionar:

```csharp
    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pendencias")]
    public async Task<IActionResult> ListarPendencias(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPendenciasTerceirizadoQuery(), ct));
```

- [ ] **Step 4: Escrever o teste**

Criar `tests/AAHBRANT.SST.Application.Tests/Terceirizados/ListarPendenciasTerceirizadoQueryHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class ListarPendenciasTerceirizadoQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_CenarioCompleto_RetornaAsTresListas()
    {
        var db = CriarDb(nameof(Handle_CenarioCompleto_RetornaAsTresListas));
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var contratoAtivo = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow), DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            GJuriContratoId = "gjuri-1", Status = StatusContrato.Validado,
        };
        var contratoEncerrado = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-002",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-8)), DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            GJuriContratoId = "gjuri-2", Status = StatusContrato.Encerrado,
        };
        db.Contratos.AddRange(contratoAtivo, contratoEncerrado);
        await db.SaveChangesAsync();

        db.Trabalhadores.AddRange(
            new Trabalhador
            {
                ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "Pendente", Matricula = "MAT-1", Cpf = "52998224725",
                Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow, EmpresaId = empresa.Id, ContratoId = contratoAtivo.Id,
            },
            new Trabalhador
            {
                ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "Ainda ativo no encerrado", Matricula = "MAT-2", Cpf = "11144477735",
                Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow, EmpresaId = empresa.Id, ContratoId = contratoEncerrado.Id,
            });
        await db.SaveChangesAsync();

        var catalogo = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.CatalogoEpis.Add(catalogo);
        await db.SaveChangesAsync();
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.EpiEstoqueInsuficiente, Severidade = SeveridadeAlerta.Critico, Status = StatusAlerta.Aberto,
            Titulo = "Estoque insuficiente de Capacete", EntidadeOrigemTipo = "Trabalhador", EntidadeOrigemId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var handler = new ListarPendenciasTerceirizadoQueryHandler(db);
        var painel = await handler.Handle(new ListarPendenciasTerceirizadoQuery(), default);

        Assert.Equal(2, painel.PessoasBloqueadas.Count); // ambas sem ASO/integração cadastrados.
        Assert.Single(painel.ContratosEncerradosComPessoasAtivas);
        Assert.Equal("CT-002", painel.ContratosEncerradosComPessoasAtivas[0].NumeroContrato);
        Assert.Single(painel.AlertasEstoqueInsuficiente);
    }
}
```

- [ ] **Step 5: Rodar o teste**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ListarPendenciasTerceirizadoQueryHandlerTests`
Expected: 1 teste passando.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Terceirizados/ src/AAHBRANT.SST.Api/Controllers/TerceirizadosController.cs tests/AAHBRANT.SST.Application.Tests/Terceirizados/
git commit -m "feat(terceirizado): painel de pendências consolidado"
```

---

### Task 13: Frontend — tipos e client (`api.ts`)

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`

**Interfaces:**
- Consumes: todos os endpoints dos Tasks 3, 5, 6, 8, 9, 12.
- Produces: interfaces `Empresa`, `NovaEmpresa`, `Contrato`, `ContratoDetalhe`, `VagaFuncao`, `PessoaTerceirizada`, `PainelPendenciasTerceirizado` e `api.terceirizados.*` — consumidos pelos Tasks 14 e 15. `EntregaEpi.confirmada`/`dataConfirmacao` e `api.entregasEpi.confirmar` — consumidos pelo Task 15 (botão "Confirmar entrega").

Este projeto não usa TanStack Query — os componentes chamam `api.*` diretamente dentro de `useEffect`/handlers (ver `FuncoesTab.tsx`), então este task só adiciona tipos e funções de chamada, sem nenhuma lib nova.

- [ ] **Step 1: Estender `EntregaEpi` com o campo de confirmação (Task 3)**

Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, dentro de `export interface EntregaEpi { ... }` (linha 487), adicionar após `dataTreinamentoNr6?: string | null;` (linha 501):

```typescript
  confirmada: boolean;
  dataConfirmacao?: string | null;
```

- [ ] **Step 2: Adicionar `confirmar` em `api.entregasEpi`**

Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, dentro do bloco `entregasEpi: { ... }` (linha 3737), adicionar após `excluir: (id: string) => request<void>(\`/api/entregasepi/${id}\`, { method: 'DELETE' }),` (linha 3750):

```typescript
    confirmar: (id: string) => request<void>(`/api/entregasepi/${id}/confirmar`, { method: 'PUT' }),
```

- [ ] **Step 3: Adicionar os tipos do módulo Terceirizado**

Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, logo após `export type NovaFuncao = Omit<Funcao, 'id'>;` (linha 195), adicionar:

```typescript

export interface Empresa {
  id: string;
  razaoSocial: string;
  nomeFantasia?: string | null;
  cnpj: string;
  tipoServicoPrestado?: string | null;
  contatoNome?: string | null;
  contatoTelefone?: string | null;
  contatoEmail?: string | null;
  status: 'Ativa' | 'Inativa';
}
export type NovaEmpresa = Omit<Empresa, 'id' | 'status'>;
export type AtualizarEmpresa = Omit<Empresa, 'razaoSocial'> & { razaoSocial: string };

export interface Contrato {
  id: string;
  empresaId: string;
  obraId: string;
  obraNome: string;
  numeroContrato: string;
  dataInicioVigencia: string;
  dataFimVigencia: string;
  status: 'Validado' | 'Encerrado' | 'Cancelado';
}

export interface VagaFuncao {
  id: string;
  funcaoId: string;
  funcaoNome: string;
  quantidadeVagas: number;
  quantidadePreenchidas: number;
}

export interface ContratoDetalhe extends Contrato {
  empresaRazaoSocial: string;
  vagas: VagaFuncao[];
}

export interface PessoaTerceirizada {
  trabalhadorId: string;
  nome: string;
  matricula?: string | null;
  empresaId: string;
  empresaRazaoSocial: string;
  contratoId: string;
  numeroContrato: string;
  funcaoId: string;
  funcaoNome: string;
  status: 'Pendente' | 'Liberada';
  pendencias: string[];
}

export interface StatusLiberacaoTrabalhador {
  trabalhadorId: string;
  status: 'Pendente' | 'Liberada';
  pendencias: string[];
}

export interface PendenciaPessoa {
  trabalhadorId: string;
  nome: string;
  empresaId: string;
  empresaRazaoSocial: string;
  pendencias: string[];
}

export interface ContratoEncerradoComPessoasAtivas {
  contratoId: string;
  numeroContrato: string;
  empresaRazaoSocial: string;
  quantidadePessoasAtivas: number;
}

export interface AlertaEstoqueInsuficiente {
  alertaId: string;
  titulo: string;
  descricao?: string | null;
  criadoEmUtc: string;
}

export interface PainelPendenciasTerceirizado {
  pessoasBloqueadas: PendenciaPessoa[];
  contratosEncerradosComPessoasAtivas: ContratoEncerradoComPessoasAtivas[];
  alertasEstoqueInsuficiente: AlertaEstoqueInsuficiente[];
}
```

- [ ] **Step 4: Adicionar o client `api.terceirizados`**

Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, no final do objeto `api` — a linha 4761 (`  },`) fecha o último grupo (`cipa`) antes do `};` da linha 4762. Adicionar uma vírgula ao final da linha 4761 e, antes do `};`, o novo bloco:

```typescript
  terceirizados: {
    empresas: {
      listar: () => request<Empresa[]>('/api/empresas'),
      obterPorId: (id: string) => request<Empresa>(`/api/empresas/${id}`),
      criar: (empresa: NovaEmpresa) =>
        request<{ id: string }>('/api/empresas', { method: 'POST', body: JSON.stringify(empresa) }),
      atualizar: (empresa: AtualizarEmpresa) =>
        request<void>(`/api/empresas/${empresa.id}`, { method: 'PUT', body: JSON.stringify(empresa) }),
      excluir: (id: string) => request<void>(`/api/empresas/${id}`, { method: 'DELETE' }),
    },
    contratos: {
      listarPorEmpresa: (empresaId: string) => request<Contrato[]>(`/api/contratos?empresaId=${empresaId}`),
      obterDetalhe: (id: string) => request<ContratoDetalhe>(`/api/contratos/${id}`),
      cadastrarPessoaNaVaga: (
        contratoId: string,
        funcaoId: string,
        dados: { nome: string; matricula: string; cpf: string; dataAdmissao: string },
      ) =>
        request<{ trabalhadorId: string }>(`/api/contratos/${contratoId}/vagas/${funcaoId}/pessoas`, {
          method: 'POST',
          body: JSON.stringify(dados),
        }),
    },
    pessoas: {
      listar: (filtro: { empresaId?: string; contratoId?: string } = {}) => {
        const params = new URLSearchParams();
        if (filtro.empresaId) params.set('empresaId', filtro.empresaId);
        if (filtro.contratoId) params.set('contratoId', filtro.contratoId);
        const query = params.toString();
        return request<PessoaTerceirizada[]>(`/api/terceirizados/pessoas${query ? `?${query}` : ''}`);
      },
      obterStatus: (trabalhadorId: string) =>
        request<StatusLiberacaoTrabalhador>(`/api/terceirizados/pessoas/${trabalhadorId}/status`),
    },
    pendencias: {
      listar: () => request<PainelPendenciasTerceirizado>('/api/terceirizados/pendencias'),
    },
  },
```

- [ ] **Step 5: Build do frontend**

Run: `npm --prefix src/AAHBRANT.SST.TeamsApp run build`
Expected: build TypeScript sem erros (confirma que os tipos novos e o objeto `api.terceirizados` estão sintaticamente corretos e não colidem com nada existente).

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat(terceirizado): tipos e client de api.ts para Empresa/Contrato/Pessoa/Pendências"
```

---

### Task 14: Frontend — sidebar, página do módulo e CRUD de Empresas

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx` (novo item na sidebar)
- Modify: `src/AAHBRANT.SST.TeamsApp/src/App.tsx` (nova rota)
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/TerceirizadoPage.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/EmpresasTab.tsx`

**Interfaces:**
- Consumes: `api.terceirizados.empresas.*` (Task 13).
- Produces: rota `/terceirizados` na sidebar, componente `<EmpresasTab />` — consumido pelo Task 15 (que adiciona as demais abas de `TerceirizadoPage`).

Decisão do usuário (brainstorming, seção 1 da spec): "Terceirizado" quebra deliberadamente a convenção atual de "todo módulo novo é aba de um pilar existente" e ganha item próprio na sidebar.

- [ ] **Step 1: Adicionar o item na sidebar**

Em `src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx`, importar o ícone `Buildings24Regular` de `@fluentui/react-icons` junto dos demais ícones já importados no topo do arquivo, e adicionar ao array `itensPilares` (linha 528):

```typescript
  { rota: '/terceirizados', rotulo: 'Terceirizado', icone: Buildings24Regular },
```

(imediatamente após a linha `{ rota: '/ocorrencias', rotulo: 'Ocorrências', icone: BriefcaseMedical24Regular },`).

- [ ] **Step 2: Adicionar a rota**

Em `src/AAHBRANT.SST.TeamsApp/src/App.tsx`, importar `TerceirizadoPage` de `./pages/terceirizados/TerceirizadoPage` e adicionar, junto das demais rotas de pilar (próximo de `/operacao`/`/pessoas`):

```typescript
<Route path="/terceirizados" element={<TerceirizadoPage />} />
```

- [ ] **Step 3: Criar a página do módulo**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/TerceirizadoPage.tsx`:

```tsx
import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { EmpresasTab } from './EmpresasTab';

// Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md) — item
// próprio na sidebar (decisão explícita do usuário, quebra a convenção de módulo-como-aba-de-pilar
// usada pelos demais). PessoasTab/PendenciasTab entram no Task 15.
const ABAS = ['empresas', 'pessoas', 'pendencias'] as const;
type AbaTerceirizado = (typeof ABAS)[number];

export function TerceirizadoPage() {
  const [aba, setAba] = useAbaNaUrl<AbaTerceirizado>('aba', ABAS, 'empresas');

  return (
    <div>
      <PageHeader titulo="Terceirizado" />

      <Abas
        nivel="pilar"
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Terceirizado"
        abas={[
          { valor: 'empresas', rotulo: 'Empresas' },
          { valor: 'pessoas', rotulo: 'Pessoas' },
          { valor: 'pendencias', rotulo: 'Pendências' },
        ]}
      />

      {aba === 'empresas' && <EmpresasTab />}
    </div>
  );
}
```

- [ ] **Step 4: Criar o CRUD de Empresas**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/EmpresasTab.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  FeedbackInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Eye24Regular } from '@fluentui/react-icons';
import { api, type Empresa, type NovaEmpresa } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const empresaVazia: NovaEmpresa = {
  razaoSocial: '', nomeFantasia: '', cnpj: '', tipoServicoPrestado: '',
  contatoNome: '', contatoTelefone: '', contatoEmail: '',
};

// CRUD de Empresa terceirizada — mesmo padrão de FuncoesTab.tsx (PainelCriacaoInline em vez de
// drawer). Contratos e pessoas vinculadas ficam na ficha da empresa (EmpresaDetalhePage, Task 15).
export function EmpresasTab() {
  const navigate = useNavigate();
  const [empresas, setEmpresas] = useState<Empresa[]>([]);
  const [novaEmpresa, setNovaEmpresa] = useState<NovaEmpresa>(empresaVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setEmpresas(await api.terceirizados.empresas.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar empresas.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.terceirizados.empresas.criar(novaEmpresa);
      setNovaEmpresa(empresaVazia);
      await carregar();
      sucessoToast('Empresa cadastrada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao cadastrar empresa.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Inativar esta empresa? Ela deixa de aparecer para novos contratos.'))) return;
    try {
      await api.terceirizados.empresas.excluir(id);
      await carregar();
      sucessoToast('Empresa inativada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao inativar empresa.');
    }
  }

  const colunas: Coluna<Empresa>[] = [
    { chave: 'razaoSocial', rotulo: 'Razão social' },
    { chave: 'cnpj', rotulo: 'CNPJ' },
    { chave: 'tipoServicoPrestado', rotulo: 'Serviço prestado' },
    { chave: 'status', rotulo: 'Status' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Empresas terceirizadas"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-empresa"
          >
            {painelAberto ? 'Fechar' : 'Cadastrar empresa'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <div id="painel-nova-empresa">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova empresa terceirizada">
          <FormSection titulo="Dados da empresa" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Razão social">
                  <Input value={novaEmpresa.razaoSocial} onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, razaoSocial: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Nome fantasia">
                  <Input
                    value={novaEmpresa.nomeFantasia ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, nomeFantasia: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="CNPJ (só números)">
                  <Input value={novaEmpresa.cnpj} onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, cnpj: d.value })} />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Tipo de serviço prestado">
                  <Input
                    value={novaEmpresa.tipoServicoPrestado ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, tipoServicoPrestado: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Contato — nome">
                  <Input
                    value={novaEmpresa.contatoNome ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, contatoNome: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Contato — telefone">
                  <Input
                    value={novaEmpresa.contatoTelefone ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, contatoTelefone: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Contato — e-mail">
                  <Input
                    value={novaEmpresa.contatoEmail ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, contatoEmail: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Cadastrar empresa
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>
      <Card>
        <DataTable
          aria-label="Empresas terceirizadas cadastradas"
          colunas={colunas}
          linhas={empresas}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma empresa terceirizada cadastrada ainda',
            acao: { rotulo: 'Cadastrar empresa', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(e) => (
            <>
              <Button
                appearance="subtle"
                icon={<Eye24Regular />}
                onClick={() => navigate(`/terceirizados/empresas/${e.id}`)}
                aria-label="Ver detalhes"
              />
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(e.id)} aria-label="Inativar" />
            </>
          )}
        />
      </Card>
    </div>
  );
}
```

- [ ] **Step 5: Verificar no navegador**

Rodar o preview do frontend, navegar até `/terceirizados`, confirmar que o item "Terceirizado" aparece na sidebar, a aba "Empresas" carrega, e é possível cadastrar uma empresa de teste e vê-la na lista.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx src/AAHBRANT.SST.TeamsApp/src/App.tsx src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/
git commit -m "feat(terceirizado): sidebar, página do módulo e CRUD de Empresas"
```

---

### Task 15: Frontend — ficha da empresa, contratos, pessoas, pendências e confirmação de EPI

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/EmpresaDetalhePage.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/ContratoDetalhePage.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/PessoasTab.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/PendenciasTab.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/TerceirizadoPage.tsx` (renderizar as duas abas novas)
- Modify: `src/AAHBRANT.SST.TeamsApp/src/App.tsx` (2 rotas novas)
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/EntregasEpiTab.tsx` (coluna de status + ação "Confirmar")
- Test: verificação manual no navegador (frontend deste repositório não tem testes automatizados de componente — mesmo padrão dos demais módulos, ver Tasks anteriores de UI)

**Interfaces:**
- Consumes: `api.terceirizados.*`, `api.entregasEpi.confirmar` (Task 13).
- Produces: fluxo completo de drill-down Empresa → Contrato → vagas → cadastro de pessoa; painel de pendências; confirmação de entrega pendente de EPI.

- [ ] **Step 1: Criar a ficha da empresa**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/EmpresaDetalhePage.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Card, PageHeader, DataTable, FeedbackInline, StatusChip, type Coluna } from '@ui';
import { Open24Regular } from '@fluentui/react-icons';
import { api, type Empresa, type Contrato } from '../../lib/api';

export function EmpresaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [empresa, setEmpresa] = useState<Empresa | null>(null);
  const [contratos, setContratos] = useState<Contrato[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setCarregando(true);
      const [dadosEmpresa, listaContratos] = await Promise.all([
        api.terceirizados.empresas.obterPorId(id),
        api.terceirizados.contratos.listarPorEmpresa(id),
      ]);
      setEmpresa(dadosEmpresa);
      setContratos(listaContratos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a empresa.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const colunas: Coluna<Contrato>[] = [
    { chave: 'numeroContrato', rotulo: 'Nº do contrato' },
    { chave: 'obraNome', rotulo: 'Obra' },
    { chave: 'vigencia', rotulo: 'Vigência', render: (c) => `${c.dataInicioVigencia} a ${c.dataFimVigencia}` },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (c) => (
        <StatusChip tom={c.status === 'Validado' ? 'ok' : c.status === 'Encerrado' ? 'atencao' : 'alerta'}>
          {c.status}
        </StatusChip>
      ),
    },
  ];

  if (!empresa) {
    return (
      <div>
        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader titulo={empresa.razaoSocial} subtitulo={`CNPJ ${empresa.cnpj} — ${empresa.status}`} />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Contratos">
        <DataTable
          aria-label="Contratos da empresa"
          colunas={colunas}
          linhas={contratos}
          chaveLinha={(c) => c.id}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum contrato recebido do G-Juri ainda para esta empresa' }}
          acoesLinha={(c) => (
            <Button
              appearance="subtle"
              icon={<Open24Regular />}
              onClick={() => navigate(`/terceirizados/contratos/${c.id}`)}
              aria-label="Ver vagas do contrato"
            />
          )}
        />
      </Card>
    </div>
  );
}
```

- [ ] **Step 2: Criar a ficha do contrato (vagas + cadastro de pessoa)**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/ContratoDetalhePage.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Button,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  Field,
  Input,
  FeedbackInline,
  type Coluna,
} from '@ui';
import { PersonAdd24Regular } from '@fluentui/react-icons';
import { api, type ContratoDetalhe, type VagaFuncao } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const novaPessoaVazia = { nome: '', matricula: '', cpf: '', dataAdmissao: '' };

// Cadastro de pessoa terceirizada numa vaga do contrato (docs/superpowers/specs/2026-09-18-modulo-
// terceirizado-design.md §9) — dispara a automação de EPI no backend (reserva/alerta de estoque);
// aqui só mostramos o resultado (a vaga preenchida sobe de contagem) e um aviso de sucesso.
export function ContratoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [contrato, setContrato] = useState<ContratoDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [salvando, setSalvando] = useState(false);
  const [vagaSelecionada, setVagaSelecionada] = useState<VagaFuncao | null>(null);
  const [novaPessoa, setNovaPessoa] = useState(novaPessoaVazia);
  const sucessoToast = useSucessoToast();

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setCarregando(true);
      setContrato(await api.terceirizados.contratos.obterDetalhe(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar o contrato.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function fecharPainel() {
    setVagaSelecionada(null);
    setNovaPessoa(novaPessoaVazia);
    setErroPainel(null);
  }

  async function cadastrarPessoa() {
    if (!id || !vagaSelecionada) return;
    try {
      setSalvando(true);
      setErroPainel(null);
      await api.terceirizados.contratos.cadastrarPessoaNaVaga(id, vagaSelecionada.funcaoId, novaPessoa);
      await carregar();
      sucessoToast('Pessoa cadastrada — verifique EPI/treinamentos pendentes na aba Pessoas.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao cadastrar pessoa nesta vaga.');
    } finally {
      setSalvando(false);
    }
  }

  const colunas: Coluna<VagaFuncao>[] = [
    { chave: 'funcaoNome', rotulo: 'Função' },
    {
      chave: 'vagas',
      rotulo: 'Vagas',
      render: (v) => `${v.quantidadePreenchidas} / ${v.quantidadeVagas}`,
    },
  ];

  if (!contrato) {
    return <div>{erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo={`Contrato ${contrato.numeroContrato}`}
        subtitulo={`${contrato.empresaRazaoSocial} — Obra ${contrato.obraNome} — ${contrato.status}`}
      />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      {vagaSelecionada && (
        <PainelCriacaoInline aberto titulo={`Cadastrar pessoa — ${vagaSelecionada.funcaoNome}`}>
          <FormSection titulo="Dados da pessoa" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Nome">
                  <Input value={novaPessoa.nome} onChange={(_, d) => setNovaPessoa({ ...novaPessoa, nome: d.value })} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Matrícula">
                  <Input value={novaPessoa.matricula} onChange={(_, d) => setNovaPessoa({ ...novaPessoa, matricula: d.value })} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="CPF (só números)">
                  <Input value={novaPessoa.cpf} onChange={(_, d) => setNovaPessoa({ ...novaPessoa, cpf: d.value })} />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Data de admissão">
                  <Input
                    type="date"
                    value={novaPessoa.dataAdmissao}
                    onChange={(_, d) => setNovaPessoa({ ...novaPessoa, dataAdmissao: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape info="EPI e treinamento obrigatórios da função são verificados automaticamente após o cadastro.">
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={cadastrarPessoa} disabled={salvando}>
                Cadastrar pessoa
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      )}

      <Card titulo="Vagas por função">
        <DataTable
          aria-label="Vagas do contrato"
          colunas={colunas}
          linhas={contrato.vagas}
          chaveLinha={(v) => v.id}
          carregando={carregando}
          vazio={{ titulo: 'Este contrato não tem vagas cadastradas' }}
          acoesLinha={(v) => (
            <Button
              appearance="subtle"
              icon={<PersonAdd24Regular />}
              disabled={v.quantidadePreenchidas >= v.quantidadeVagas}
              onClick={() => setVagaSelecionada(v)}
              aria-label="Cadastrar pessoa nesta vaga"
            >
              Cadastrar pessoa
            </Button>
          )}
        />
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Criar a aba de Pessoas**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/PessoasTab.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { Card, DataTable, StatusChip, FeedbackInline, type Coluna } from '@ui';
import { api, type PessoaTerceirizada } from '../../lib/api';

export function PessoasTab() {
  const [pessoas, setPessoas] = useState<PessoaTerceirizada[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        setPessoas(await api.terceirizados.pessoas.listar());
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar pessoas terceirizadas.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const colunas: Coluna<PessoaTerceirizada>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'empresaRazaoSocial', rotulo: 'Empresa' },
    { chave: 'funcaoNome', rotulo: 'Função' },
    { chave: 'numeroContrato', rotulo: 'Contrato' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (p) => <StatusChip tom={p.status === 'Liberada' ? 'ok' : 'alerta'}>{p.status}</StatusChip>,
    },
    {
      chave: 'pendencias',
      rotulo: 'Pendências',
      render: (p) => (p.pendencias.length === 0 ? '—' : p.pendencias.join('; ')),
    },
  ];

  return (
    <Card titulo="Pessoas terceirizadas">
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <DataTable
        aria-label="Pessoas terceirizadas"
        colunas={colunas}
        linhas={pessoas}
        chaveLinha={(p) => p.trabalhadorId}
        carregando={carregando}
        vazio={{ titulo: 'Nenhuma pessoa terceirizada cadastrada ainda' }}
      />
    </Card>
  );
}
```

- [ ] **Step 4: Criar a aba de Pendências**

Criar `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/PendenciasTab.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { Card, DataTable, FeedbackInline, type Coluna } from '@ui';
import {
  api,
  type PendenciaPessoa,
  type ContratoEncerradoComPessoasAtivas,
  type AlertaEstoqueInsuficiente,
} from '../../lib/api';

export function PendenciasTab() {
  const [pessoasBloqueadas, setPessoasBloqueadas] = useState<PendenciaPessoa[]>([]);
  const [contratosEncerrados, setContratosEncerrados] = useState<ContratoEncerradoComPessoasAtivas[]>([]);
  const [alertasEstoque, setAlertasEstoque] = useState<AlertaEstoqueInsuficiente[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const painel = await api.terceirizados.pendencias.listar();
        setPessoasBloqueadas(painel.pessoasBloqueadas);
        setContratosEncerrados(painel.contratosEncerradosComPessoasAtivas);
        setAlertasEstoque(painel.alertasEstoqueInsuficiente);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar pendências.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const colunasPessoas: Coluna<PendenciaPessoa>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'empresaRazaoSocial', rotulo: 'Empresa' },
    { chave: 'pendencias', rotulo: 'Pendências', render: (p) => p.pendencias.join('; ') },
  ];

  const colunasContratos: Coluna<ContratoEncerradoComPessoasAtivas>[] = [
    { chave: 'numeroContrato', rotulo: 'Contrato' },
    { chave: 'empresaRazaoSocial', rotulo: 'Empresa' },
    { chave: 'quantidadePessoasAtivas', rotulo: 'Pessoas ainda ativas', alinhar: 'direita' },
  ];

  const colunasAlertas: Coluna<AlertaEstoqueInsuficiente>[] = [
    { chave: 'titulo', rotulo: 'Alerta' },
    { chave: 'descricao', rotulo: 'Detalhe' },
    { chave: 'criadoEmUtc', rotulo: 'Data', render: (a) => a.criadoEmUtc.slice(0, 10) },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Pessoas bloqueadas">
        <DataTable
          aria-label="Pessoas bloqueadas"
          colunas={colunasPessoas}
          linhas={pessoasBloqueadas}
          chaveLinha={(p) => p.trabalhadorId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhuma pessoa bloqueada' }}
        />
      </Card>

      <Card titulo="Contratos encerrados com gente ainda ativa">
        <DataTable
          aria-label="Contratos encerrados com pessoas ativas"
          colunas={colunasContratos}
          linhas={contratosEncerrados}
          chaveLinha={(c) => c.contratoId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum contrato encerrado com gente ainda ativa' }}
        />
      </Card>

      <Card titulo="Falta de estoque de EPI">
        <DataTable
          aria-label="Alertas de estoque insuficiente"
          colunas={colunasAlertas}
          linhas={alertasEstoque}
          chaveLinha={(a) => a.alertaId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum alerta de estoque em aberto' }}
        />
      </Card>
    </div>
  );
}
```

- [ ] **Step 5: Ligar as duas abas em `TerceirizadoPage`**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/TerceirizadoPage.tsx`, importar `PessoasTab` e `PendenciasTab` e substituir a última linha do JSX (`{aba === 'empresas' && <EmpresasTab />}`) por:

```tsx
      {aba === 'empresas' && <EmpresasTab />}
      {aba === 'pessoas' && <PessoasTab />}
      {aba === 'pendencias' && <PendenciasTab />}
```

- [ ] **Step 6: Adicionar as rotas de drill-down**

Em `src/AAHBRANT.SST.TeamsApp/src/App.tsx`, importar `EmpresaDetalhePage` e `ContratoDetalhePage` e adicionar, junto da rota `/terceirizados` (Task 14):

```tsx
<Route path="/terceirizados/empresas/:id" element={<EmpresaDetalhePage />} />
<Route path="/terceirizados/contratos/:id" element={<ContratoDetalhePage />} />
```

- [ ] **Step 7: Adicionar a confirmação de entrega pendente em `EntregasEpiTab`**

Em `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/EntregasEpiTab.tsx`:

1. Importar `StatusChip` já está importado; adicionar ao array `colunas` (logo após a coluna `devolucao`, linha 81):

```tsx
    {
      chave: 'confirmacao',
      rotulo: 'Confirmação',
      render: (e) =>
        e.confirmada ? (
          <StatusChip tom="ok">Confirmada</StatusChip>
        ) : (
          <StatusChip tom="atencao">Pendente</StatusChip>
        ),
    },
```

2. Adicionar a função de confirmação e o prop `acoesLinha` no `<DataTable>`:

```tsx
  async function confirmar(id: string) {
    try {
      await api.entregasEpi.confirmar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao confirmar a entrega.');
    }
  }
```

(logo após a função `baixarFicha`, antes da definição de `colunas`) e, no `<DataTable ... />`, adicionar:

```tsx
        acoesLinha={(e) =>
          !e.confirmada && (
            <Button appearance="subtle" onClick={() => confirmar(e.id)}>
              Confirmar entrega
            </Button>
          )
        }
```

- [ ] **Step 8: Verificar no navegador**

Rodar o preview do frontend e percorrer o fluxo completo: `/terceirizados` → abrir uma empresa → abrir um contrato → cadastrar pessoa numa vaga → confirmar na aba "Pessoas" que ela aparece como "Pendente" com as pendências corretas (ASO, Integração de Segurança, EPI) → na ficha da pessoa (`/pessoas/trabalhadores/:id`), aba "EPI & Matriz", confirmar a entrega pendente e ver o status mudar para "Confirmada".

- [ ] **Step 9: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/terceirizados/ src/AAHBRANT.SST.TeamsApp/src/App.tsx src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/EntregasEpiTab.tsx
git commit -m "feat(terceirizado): ficha de empresa/contrato, pessoas, pendências e confirmação de EPI"
```

---

## Resumo de verificação final

Depois de completar todos os Tasks, antes de considerar o módulo pronto:

- [ ] `dotnet build` limpo em todos os projetos.
- [ ] `dotnet test` verde em `tests/AAHBRANT.SST.Application.Tests` (todos os testes deste plano, ~24 no total entre os Tasks 3–12).
- [ ] `npm --prefix src/AAHBRANT.SST.TeamsApp run build` sem erros.
- [ ] Fluxo ponta a ponta verificado no navegador (Task 15, Step 8).
- [ ] Nenhuma migration aplicada em produção/homologação sem autorização explícita do usuário (ver `feedback_deploy_so_quando_mandar`/`feedback_mostrar_antes_de_deploy` — mostrar o resultado local e esperar validação antes de qualquer `az acr build`/`containerapp update`).
