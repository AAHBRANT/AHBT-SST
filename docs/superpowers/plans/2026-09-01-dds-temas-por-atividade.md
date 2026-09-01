# DDS — 3 temas simultâneos por dia Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Substituir a escolha exclusiva de 1 tema por DDS (`OrigemTemaDds`) por temas
aditivos e simultâneos: um bloco de conteúdo (Perigo/Risco) por atividade marcada no dia,
mais um tema livre opcional do catálogo — e dar ao catálogo de temas livres uma aba de
administração própria.

**Architecture:** Nenhuma tabela nova. `DdsAtividade` (já existe, junção Dds↔Atividade)
ganha colunas de snapshot (Perigo/Descrição/Consequência/Controles), preenchidas na
criação a partir da mesma consulta de Risco que já monta o checklist. `Dds` troca
`TopicoPrincipal`/`OrigemTema` por `TemaLivreNome`/`TemaLivreDescricao` (snapshot do
catálogo, aditivo, não exclusivo). `CatalogoTemaDds` ganha um comando de atualização e uma
tela própria.

**Tech Stack:** .NET 8 / EF Core 8 / MediatR / FluentValidation (backend), React + Fluent
UI v9 + TypeScript (frontend), QuestPDF (documentos).

**Spec:** `docs/superpowers/specs/2026-09-01-dds-temas-por-atividade-design.md`

## Global Constraints

- Branch de trabalho: `integracao/deploy-treinamentos`, worktree
  `C:\Projetos\SST-APP\.worktrees\reformulacao-treinamentos` — é o que roda em hml, não a
  `master`.
- Sem campo novo em `Atividade`/`Risco`/`Perigo` — o conteúdo do tema automático é 100%
  derivado do que já está cadastrado (spec, seção 2 "Não entra").
- Tema livre é sempre opcional (nunca obrigatório para criar um DDS).
- Sem trava de quantidade de atividades marcadas por dia (spec, seção 4).
- Cópia de conteúdo (snapshot) na criação do `Dds`, nunca referência viva — mesmo
  princípio já usado em `DdsItemChecklist` (spec, seção 3.1).

---

## Task 1: Backend — modelo de dados, migration e criação do DDS

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Dds/Dds.cs`
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/DdsConfiguracoes.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Commands/CriarDdsCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/DdsDto.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/DdsSemanalDto.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Queries/ListarDdsQuery.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Queries/ObterDdsSemanalDetalheQuery.cs`
- Create: `tests/AAHBRANT.SST.Application.Tests/Dds/CriarDdsCommandHandlerTests.cs`
- Create: migration em `src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/` (gerada
  por `dotnet ef migrations add`, ver Passo 7)

**Interfaces:**
- Produz: `Dds.TemaLivreNome`, `Dds.TemaLivreDescricao` (string?); `DdsAtividade.PerigoNome`,
  `PerigoDescricao`, `Consequencia`, `ControlesExistentes`, `ControlesAdicionais` (todos
  string?); `DdsTemaAtividadeDto` (novo tipo, ver Passo 4) com os mesmos campos + `AtividadeId`
  (Guid) e `AtividadeNome` (string); `DdsDto.TemasAtividades` (List\<DdsTemaAtividadeDto\>),
  `DdsDto.TemaLivreNome`/`TemaLivreDescricao` (string?); `CriarDdsCommand(Guid DdsSemanalId,
  List<Guid> AtividadesIds, DateTime Data, Guid? CatalogoTemaDdsId)`. Tarefas 2-6 consomem
  esses nomes exatamente.
- Remove: `Dds.TopicoPrincipal`, `Dds.OrigemTema`, enum `OrigemTemaDds`,
  `CriarDdsCommand.OrigemTema` — nenhuma tarefa depois desta pode referenciá-los.

- [ ] **Passo 1: Ajustar a entidade `Dds`**

Em `src/AAHBRANT.SST.Domain/Entidades/Dds/Dds.cs`, na classe `Dds`, substituir:

```csharp
    // Snapshot gerado na criação — se o Risco for editado depois, o DDS já gerado não muda. A
    // origem (OrigemTema) registra COMO esse texto foi obtido; ver disclosure em OrigemTemaDds.
    public string TopicoPrincipal { get; set; } = string.Empty;
    public OrigemTemaDds OrigemTema { get; set; } = OrigemTemaDds.Livre;
    public Guid? CatalogoTemaDdsId { get; set; }
    public CatalogoTemaDds? CatalogoTemaDds { get; set; }
```

por:

```csharp
    // Tema livre (opcional, aditivo — não substitui os temas das atividades abaixo). Nome/
    // descrição são uma cópia do CatalogoTemaDds no momento da criação (mesmo princípio de
    // snapshot já usado nos itens de checklist): se o item do catálogo for editado ou excluído
    // depois, este DDS continua mostrando o que foi realmente apresentado naquele dia.
    public Guid? CatalogoTemaDdsId { get; set; }
    public CatalogoTemaDds? CatalogoTemaDds { get; set; }
    public string? TemaLivreNome { get; set; }
    public string? TemaLivreDescricao { get; set; }
```

Na classe `DdsAtividade` (mesmo arquivo), substituir:

```csharp
public class DdsAtividade : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public int Ordem { get; set; }
}
```

por:

```csharp
public class DdsAtividade : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public int Ordem { get; set; }

    // Snapshot do Risco de maior NivelRisco desta atividade, copiado na criação do Dds — mesmo
    // princípio de DdsItemChecklist (cópia, não referência viva). Tudo nullable: a atividade pode
    // não ter nenhum Risco cadastrado ainda.
    public string? PerigoNome { get; set; }
    public string? PerigoDescricao { get; set; }
    public string? Consequencia { get; set; }
    public string? ControlesExistentes { get; set; }
    public string? ControlesAdicionais { get; set; }
}
```

Atualizar o comentário acima de `DdsAtividade` (que hoje explica
`AutomaticoAtividade1/2`) para:

```csharp
// Atividades do dia selecionadas pelo gestor para este DDS — vínculo N:N materializado (mesmo
// padrão de RiscoTrabalhadorExposto). Cada atividade marcada contribui com seu próprio bloco de
// tema (snapshot do Risco de maior nível, ver campos abaixo) — não é mais só a 1ª/2ª da lista.
```

E o comentário acima de `CatalogoTemaDds` (que cita `OrigemTemaDds = Livre`) para:

```csharp
// Catálogo pré-cadastrado de temas de DDS (31/08) — tema livre opcional, adicionado por cima dos
// temas automáticos das atividades (ver Dds.TemaLivreNome). Cadastro simples (nome + descrição),
// mesmo espírito de CatalogoEpi: sem versionamento, edição in-place.
```

- [ ] **Passo 2: Remover o enum `OrigemTemaDds`**

Em `src/AAHBRANT.SST.Domain/Enums/Enums.cs`, remover o bloco de comentário que antecede o
enum (o que cita "3 origens possíveis... para TopicoPrincipal, agora escopada a uma
atividade só") e o próprio enum:

```csharp
public enum OrigemTemaDds
{
    AutomaticoAtividade1 = 1,
    AutomaticoAtividade2 = 2,
    Livre = 3
}
```

- [ ] **Passo 3: Ajustar o mapeamento EF (`DdsConfiguracoes.cs`)**

Em `DdsConfiguracao.Configure`, remover a linha:

```csharp
        builder.Property(d => d.TopicoPrincipal).IsRequired().HasMaxLength(200);
```

e adicionar, no lugar:

```csharp
        builder.Property(d => d.TemaLivreNome).HasMaxLength(200);
        builder.Property(d => d.TemaLivreDescricao).HasMaxLength(500);
```

Em `DdsAtividadeConfiguracao.Configure`, adicionar (após a configuração de índice/filtro
já existente):

```csharp
        builder.Property(a => a.PerigoNome).HasMaxLength(200);
```

- [ ] **Passo 4: Atualizar `DdsDto.cs`**

Adicionar, antes da classe `DdsDto`:

```csharp
public class DdsTemaAtividadeDto
{
    public Guid AtividadeId { get; set; }
    public string AtividadeNome { get; set; } = string.Empty;
    public string? PerigoNome { get; set; }
    public string? PerigoDescricao { get; set; }
    public string? Consequencia { get; set; }
    public string? ControlesExistentes { get; set; }
    public string? ControlesAdicionais { get; set; }
}
```

Em `DdsDto`, substituir:

```csharp
    public string TopicoPrincipal { get; set; } = string.Empty;
    public OrigemTemaDds OrigemTema { get; set; }
    public Guid? CatalogoTemaDdsId { get; set; }
    public StatusDds Status { get; set; }
    public List<string> AtividadesNomes { get; set; } = new();
```

por:

```csharp
    public Guid? CatalogoTemaDdsId { get; set; }
    public string? TemaLivreNome { get; set; }
    public string? TemaLivreDescricao { get; set; }
    public StatusDds Status { get; set; }
    public List<DdsTemaAtividadeDto> TemasAtividades { get; set; } = new();
    // Conveniência derivada de TemasAtividades (mesmo dado, só os nomes) — usada onde só o nome
    // da atividade importa (ex.: DdsDetalhePage.tsx), sem repetir o objeto inteiro.
    public List<string> AtividadesNomes { get; set; } = new();
```

- [ ] **Passo 5: Atualizar `DdsSemanalDto.cs`**

Em `DdsSemanalDiaDto`, substituir `public string? TopicoPrincipal { get; set; }` por:

```csharp
    public List<string> AtividadesNomes { get; set; } = new();
    public string? TemaLivreNome { get; set; }
```

- [ ] **Passo 6: Atualizar `ListarDdsQuery.cs` e `ObterDdsSemanalDetalheQuery.cs`**

Em `ListarDdsQuery.cs`, no `MapearParaDto`, substituir:

```csharp
            TopicoPrincipal = dds.TopicoPrincipal,
            OrigemTema = dds.OrigemTema,
            CatalogoTemaDdsId = dds.CatalogoTemaDdsId,
            Status = dds.Status,
            AtividadesNomes = dds.Atividades.Where(a => a.Ativo).OrderBy(a => a.Ordem).Select(a => a.Atividade?.Nome ?? string.Empty).ToList(),
```

por:

```csharp
            CatalogoTemaDdsId = dds.CatalogoTemaDdsId,
            TemaLivreNome = dds.TemaLivreNome,
            TemaLivreDescricao = dds.TemaLivreDescricao,
            Status = dds.Status,
            TemasAtividades = dds.Atividades.Where(a => a.Ativo).OrderBy(a => a.Ordem).Select(a => new DdsTemaAtividadeDto
            {
                AtividadeId = a.AtividadeId,
                AtividadeNome = a.Atividade?.Nome ?? string.Empty,
                PerigoNome = a.PerigoNome,
                PerigoDescricao = a.PerigoDescricao,
                Consequencia = a.Consequencia,
                ControlesExistentes = a.ControlesExistentes,
                ControlesAdicionais = a.ControlesAdicionais,
            }).ToList(),
            AtividadesNomes = dds.Atividades.Where(a => a.Ativo).OrderBy(a => a.Ordem).Select(a => a.Atividade?.Nome ?? string.Empty).ToList(),
```

Em `ObterDdsSemanalDetalheQuery.cs`, o método `Handle` monta `dias` chamando
`registroDoDia?.TopicoPrincipal` — mas `registroDoDia` é a entidade `Dds` carregada via
`semanal.RegistrosDiarios` (sem `.Include(Atividades)`). Trocar o `Include` da query
principal:

```csharp
        var semanal = await _db.DdsSemanais
            .Include(s => s.Obra)
            .Include(s => s.ResponsavelUsuario)
            .Include(s => s.ResponsavelObraSstUsuario)
            .Include(s => s.RegistrosDiarios)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);
```

por:

```csharp
        var semanal = await _db.DdsSemanais
            .Include(s => s.Obra)
            .Include(s => s.ResponsavelUsuario)
            .Include(s => s.ResponsavelObraSstUsuario)
            .Include(s => s.RegistrosDiarios).ThenInclude(d => d.Atividades).ThenInclude(a => a.Atividade)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);
```

E, no laço que monta `dias`, substituir:

```csharp
                TopicoPrincipal = registroDoDia?.TopicoPrincipal,
```

por:

```csharp
                AtividadesNomes = registroDoDia?.Atividades.Where(a => a.Ativo).OrderBy(a => a.Ordem).Select(a => a.Atividade?.Nome ?? string.Empty).ToList() ?? new(),
                TemaLivreNome = registroDoDia?.TemaLivreNome,
```

- [ ] **Passo 7: Reescrever `CriarDdsCommand.cs`**

Substituir o arquivo inteiro por:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Registro diário dentro de uma DdsSemanal (31/08, reformulação para o modelo em papel do usuário;
// 01/09, temas simultâneos) — a partir das Atividades do dia selecionadas pelo gestor, cruza com os
// Riscos já cadastrados (Atividade → Risco → Perigo) para gerar o checklist do roteiro (inalterado)
// e um bloco de tema por atividade (snapshot do Risco de maior NivelRisco DELA, não do conjunto
// todo — mesmo raciocínio que já existia para o antigo "tema automático"). Tema livre (catálogo)
// é opcional e aditivo, nunca substitui os temas das atividades.
// ObraId/ResponsavelUsuarioId não são mais parâmetros — vêm da DdsSemanal (a obra e o responsável
// pelo DDS já são fixos pela semana inteira).
public record CriarDdsCommand(
    Guid DdsSemanalId,
    List<Guid> AtividadesIds,
    DateTime Data,
    Guid? CatalogoTemaDdsId) : IRequest<Guid>;

public class CriarDdsCommandValidator : AbstractValidator<CriarDdsCommand>
{
    public CriarDdsCommandValidator()
    {
        RuleFor(x => x.DdsSemanalId).NotEmpty();
        RuleFor(x => x.AtividadesIds).NotEmpty().WithMessage("Selecione ao menos uma atividade do dia.");
    }
}

public class CriarDdsCommandHandler : IRequestHandler<CriarDdsCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarDdsCommand request, CancellationToken ct)
    {
        var semanal = await _db.DdsSemanais.FirstOrDefaultAsync(s => s.Id == request.DdsSemanalId, ct)
            ?? throw new KeyNotFoundException($"DDS semanal {request.DdsSemanalId} não encontrado.");
        if (semanal.Status == StatusDdsSemanal.Concluida)
            throw new InvalidOperationException("Esta semana já foi encerrada — não é possível criar novos registros diários.");
        if (request.Data.Date < semanal.DataInicioSemana.Date || request.Data.Date > semanal.DataFimSemana.Date)
            throw new InvalidOperationException("A data do registro precisa estar dentro da semana selecionada (segunda a sexta).");

        var jaExisteNoDia = await _db.Dds.AnyAsync(d => d.DdsSemanalId == semanal.Id && d.Data.Date == request.Data.Date, ct);
        if (jaExisteNoDia)
            throw new InvalidOperationException("Já existe um registro de DDS para este dia da semana.");

        var atividadesIds = request.AtividadesIds.Distinct().ToList();
        var atividadesCarregadas = await _db.Atividades
            .Where(a => atividadesIds.Contains(a.Id) && a.ObraId == semanal.ObraId)
            .ToListAsync(ct);
        if (atividadesCarregadas.Count != atividadesIds.Count)
            throw new KeyNotFoundException("Uma ou mais atividades selecionadas não pertencem a esta obra ou não existem.");
        // Preserva a ordem de seleção do gestor (AtividadesIds), não a ordem de retorno do banco.
        var atividadesOrdenadas = atividadesIds.Select(id => atividadesCarregadas.First(a => a.Id == id)).ToList();

        var riscos = await _db.Riscos
            .Include(r => r.Perigo)
            .Where(r => atividadesIds.Contains(r.AtividadeId))
            .OrderByDescending(r => r.NivelRisco)
            .ToListAsync(ct);

        var dds = new Domain.Entidades.Dds
        {
            ObraId = semanal.ObraId,
            DdsSemanalId = semanal.Id,
            Data = request.Data.Date,
            ResponsavelUsuarioId = semanal.ResponsavelUsuarioId,
        };

        if (request.CatalogoTemaDdsId.HasValue)
        {
            var catalogo = await _db.CatalogosTemaDds.FirstOrDefaultAsync(c => c.Id == request.CatalogoTemaDdsId.Value, ct)
                ?? throw new KeyNotFoundException("Tema do catálogo não encontrado.");
            dds.CatalogoTemaDdsId = catalogo.Id;
            dds.TemaLivreNome = catalogo.Nome;
            dds.TemaLivreDescricao = catalogo.Descricao;
        }

        foreach (var (atividade, indice) in atividadesOrdenadas.Select((a, i) => (a, i)))
        {
            var maiorRisco = riscos.Where(r => r.AtividadeId == atividade.Id).OrderByDescending(r => r.NivelRisco).FirstOrDefault();
            dds.Atividades.Add(new Domain.Entidades.DdsAtividade
            {
                AtividadeId = atividade.Id,
                Ordem = indice + 1,
                PerigoNome = maiorRisco?.Perigo?.Nome,
                PerigoDescricao = maiorRisco?.Perigo?.Descricao,
                Consequencia = maiorRisco?.Consequencia,
                ControlesExistentes = maiorRisco?.ControlesExistentes,
                ControlesAdicionais = maiorRisco?.ControlesAdicionais,
            });
        }

        foreach (var risco in riscos)
        {
            foreach (var controle in ExtrairControles(risco.ControlesExistentes))
                dds.ItensChecklist.Add(new Domain.Entidades.DdsItemChecklist { RiscoId = risco.Id, Descricao = controle });
            foreach (var controle in ExtrairControles(risco.ControlesAdicionais))
                dds.ItensChecklist.Add(new Domain.Entidades.DdsItemChecklist { RiscoId = risco.Id, Descricao = controle });
        }

        _db.Dds.Add(dds);
        await _db.SaveChangesAsync(ct);
        return dds.Id;
    }

    // Controles são texto livre no cadastro de Risco — cada linha não vazia vira um item de
    // checklist independente para check-off na condução do DDS.
    private static IEnumerable<string> ExtrairControles(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) yield break;
        foreach (var linha in texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return linha;
    }
}
```

- [ ] **Passo 8: Escrever os testes de `CriarDdsCommandHandler`**

Criar `tests/AAHBRANT.SST.Application.Tests/Dds/CriarDdsCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class CriarDdsCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Obra Obra, Usuario Usuario, DdsSemanal Semanal, Atividade AtividadeComRisco, Atividade AtividadeSemRisco)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico Teste" };
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        var semanal = new DdsSemanal
        {
            ObraId = obra.Id,
            ResponsavelUsuarioId = usuario.Id,
            DataInicioSemana = new DateTime(2026, 9, 7),
            DataFimSemana = new DateTime(2026, 9, 11),
        };
        var atividadeComRisco = new Atividade { ObraId = obra.Id, Nome = "Montagem de andaime" };
        var atividadeSemRisco = new Atividade { ObraId = obra.Id, Nome = "Limpeza do canteiro" };
        db.DdsSemanais.Add(semanal);
        db.Atividades.AddRange(atividadeComRisco, atividadeSemRisco);
        await db.SaveChangesAsync();

        var perigo = new Perigo { Nome = "Queda de altura", Descricao = "Trabalho acima de 2m sem proteção de borda" };
        db.Perigos.Add(perigo);
        await db.SaveChangesAsync();

        db.Riscos.Add(new Risco
        {
            AtividadeId = atividadeComRisco.Id,
            PerigoId = perigo.Id,
            Consequencia = "Fratura, óbito",
            Probabilidade = 3,
            Severidade = 5,
            NivelRisco = NivelRisco.Alto,
            ControlesExistentes = "Uso de cinto tipo paraquedista\nAncoragem dupla",
            ControlesAdicionais = "Inspeção do cinto antes de cada uso",
        });
        await db.SaveChangesAsync();

        return (obra, usuario, semanal, atividadeComRisco, atividadeSemRisco);
    }

    [Fact]
    public async Task Handle_AtividadeComRisco_GravaSnapshotDoMaiorRiscoNaDdsAtividade()
    {
        var db = CriarDb(nameof(Handle_AtividadeComRisco_GravaSnapshotDoMaiorRiscoNaDdsAtividade));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db);

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, null), default);

        var ddsAtividade = await db.DdsAtividades.Include(a => a.Dds).FirstAsync(a => a.Dds!.Id == id);
        Assert.Equal("Queda de altura", ddsAtividade.PerigoNome);
        Assert.Equal("Trabalho acima de 2m sem proteção de borda", ddsAtividade.PerigoDescricao);
        Assert.Equal("Fratura, óbito", ddsAtividade.Consequencia);
        Assert.Equal("Uso de cinto tipo paraquedista\nAncoragem dupla", ddsAtividade.ControlesExistentes);
        Assert.Equal("Inspeção do cinto antes de cada uso", ddsAtividade.ControlesAdicionais);
    }

    [Fact]
    public async Task Handle_AtividadeSemRisco_GravaSnapshotNuloSemQuebrar()
    {
        var db = CriarDb(nameof(Handle_AtividadeSemRisco_GravaSnapshotNuloSemQuebrar));
        var (_, _, semanal, _, atividadeSemRisco) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db);

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeSemRisco.Id }, semanal.DataInicioSemana, null), default);

        var ddsAtividade = await db.DdsAtividades.Include(a => a.Dds).FirstAsync(a => a.Dds!.Id == id);
        Assert.Null(ddsAtividade.PerigoNome);
    }

    [Fact]
    public async Task Handle_DuasAtividades_GravaUmaDdsAtividadePorAtividadeMarcada()
    {
        var db = CriarDb(nameof(Handle_DuasAtividades_GravaUmaDdsAtividadePorAtividadeMarcada));
        var (_, _, semanal, atividadeComRisco, atividadeSemRisco) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db);

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id, atividadeSemRisco.Id }, semanal.DataInicioSemana, null), default);

        var ddsAtividades = await db.DdsAtividades.Where(a => a.DdsId == id).ToListAsync();
        Assert.Equal(2, ddsAtividades.Count);
    }

    [Fact]
    public async Task Handle_ComTemaLivre_CopiaNomeEDescricaoDoCatalogoParaODds()
    {
        var db = CriarDb(nameof(Handle_ComTemaLivre_CopiaNomeEDescricaoDoCatalogoParaODds));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var tema = new CatalogoTemaDds { Nome = "Outubro Amarelo", Descricao = "Prevenção ao suicídio" };
        db.CatalogosTemaDds.Add(tema);
        await db.SaveChangesAsync();
        var handler = new CriarDdsCommandHandler(db);

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, tema.Id), default);

        var dds = await db.Dds.FirstAsync(d => d.Id == id);
        Assert.Equal(tema.Id, dds.CatalogoTemaDdsId);
        Assert.Equal("Outubro Amarelo", dds.TemaLivreNome);
        Assert.Equal("Prevenção ao suicídio", dds.TemaLivreDescricao);
    }

    [Fact]
    public async Task Handle_SemTemaLivre_CriaDdsComTemaLivreNulo()
    {
        var db = CriarDb(nameof(Handle_SemTemaLivre_CriaDdsComTemaLivreNulo));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db);

        var id = await handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, null), default);

        var dds = await db.Dds.FirstAsync(d => d.Id == id);
        Assert.Null(dds.CatalogoTemaDdsId);
        Assert.Null(dds.TemaLivreNome);
    }

    [Fact]
    public async Task Handle_CatalogoTemaDdsInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_CatalogoTemaDdsInexistente_LancaKeyNotFoundException));
        var (_, _, semanal, atividadeComRisco, _) = await SemearAsync(db);
        var handler = new CriarDdsCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividadeComRisco.Id }, semanal.DataInicioSemana, Guid.NewGuid()), default));
    }
}
```

- [ ] **Passo 9: Rodar os testes novos e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter "FullyQualifiedName~CriarDdsCommandHandlerTests"`
Expected: 6 de 6 aprovados.

- [ ] **Passo 10: Gerar a migration**

Run (a partir da raiz da worktree):
```bash
dotnet ef migrations add TemasSimultaneosDds --project src/AAHBRANT.SST.Infrastructure/AAHBRANT.SST.Infrastructure.csproj --startup-project src/AAHBRANT.SST.Api/AAHBRANT.SST.Api.csproj
```
Expected: `Up()` contém `AddColumn` para `DdsAtividades.PerigoNome/PerigoDescricao/
Consequencia/ControlesExistentes/ControlesAdicionais` e `Dds.TemaLivreNome/
TemaLivreDescricao`, e `DropColumn` para `Dds.TopicoPrincipal`/`Dds.OrigemTema`. Se o
diff trouxer qualquer outra tabela não relacionada a DDS (drift pré-existente do
snapshot), reverta essa parte manualmente no arquivo gerado antes de prosseguir — não
misturar mudança não relacionada nesta migration.

- [ ] **Passo 11: Buildar a solução inteira**

Run: `dotnet build SST-APP.sln -c Debug`
Expected: `Compilação com êxito`, 0 erros. Isso confirma que nenhum consumidor de
`TopicoPrincipal`/`OrigemTema` ficou órfão fora dos arquivos já listados nesta tarefa —
se der erro de compilação em outro arquivo, ele precisa ser adicionado às Tarefas 2/3
antes de prosseguir.

- [ ] **Passo 12: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Dds/Dds.cs \
  src/AAHBRANT.SST.Domain/Enums/Enums.cs \
  src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/DdsConfiguracoes.cs \
  src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/ \
  src/AAHBRANT.SST.Application/Dds/Commands/CriarDdsCommand.cs \
  src/AAHBRANT.SST.Application/Dds/DdsDto.cs \
  src/AAHBRANT.SST.Application/Dds/DdsSemanalDto.cs \
  src/AAHBRANT.SST.Application/Dds/Queries/ListarDdsQuery.cs \
  src/AAHBRANT.SST.Application/Dds/Queries/ObterDdsSemanalDetalheQuery.cs \
  tests/AAHBRANT.SST.Application.Tests/Dds/CriarDdsCommandHandlerTests.cs
git commit -m "feat: DDS passa a ter 3 temas simultâneos por dia (atividades + tema livre opcional)"
```

---

## Task 2: Backend — PDF (diário e semanal) e mensagem do Telegram

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Dds/IDdsPdfService.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/DdsPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/IDdsSemanalPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsSemanalPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Application/Dds/Commands/EnviarDdsTelegramCommand.cs`

**Interfaces:**
- Consome: `DdsDto.TemasAtividades`, `TemaLivreNome`, `TemaLivreDescricao` (Task 1).
- Produz: `DdsPdfModelo` sem `TopicoPrincipal`/`AtividadesNomes`, com
  `IReadOnlyList<DdsPdfTemaModelo> Temas` e `string? TemaLivreNome`/`TemaLivreDescricao`
  (novo tipo `DdsPdfTemaModelo`, ver Passo 1).

- [ ] **Passo 1: Reescrever `IDdsPdfService.cs`**

Substituir o conteúdo por:

```csharp
namespace AAHBRANT.SST.Application.Dds;

public record DdsPdfTemaModelo(
    string AtividadeNome,
    string? PerigoNome,
    string? PerigoDescricao,
    string? Consequencia,
    string? ControlesExistentes,
    string? ControlesAdicionais);

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

public interface IDdsPdfService
{
    byte[] Gerar(DdsPdfModelo modelo);
}
```

- [ ] **Passo 2: Reescrever o corpo de `DdsPdfService.cs`**

Substituir o `pagina.Content()` inteiro (dentro de `Gerar`) por:

```csharp
                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Row(linha =>
                    {
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Obra: ").SemiBold();
                            t.Span(modelo.ObraNome);
                        });
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Data: ").SemiBold();
                            t.Span(modelo.Data.ToString("dd/MM/yyyy"));
                        });
                    });

                    coluna.Item().Text(t =>
                    {
                        t.Span("Responsável: ").SemiBold();
                        t.Span(modelo.ResponsavelNome);
                    });

                    coluna.Item().PaddingTop(8).Text("Temas do dia").FontSize(13).Bold();
                    foreach (var tema in modelo.Temas)
                    {
                        coluna.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(bloco =>
                        {
                            bloco.Spacing(2);
                            bloco.Item().Text(tema.AtividadeNome).Bold().FontColor(CorMarca);
                            if (tema.PerigoNome is null)
                            {
                                bloco.Item().Text("Nenhum risco cadastrado para esta atividade — revisar Matriz de Riscos.");
                            }
                            else
                            {
                                bloco.Item().Text(t => { t.Span("Perigo: ").SemiBold(); t.Span(tema.PerigoNome); });
                                if (!string.IsNullOrWhiteSpace(tema.PerigoDescricao))
                                    bloco.Item().Text(t => { t.Span("Descrição: ").SemiBold(); t.Span(tema.PerigoDescricao); });
                                if (!string.IsNullOrWhiteSpace(tema.Consequencia))
                                    bloco.Item().Text(t => { t.Span("Consequência: ").SemiBold(); t.Span(tema.Consequencia); });
                                if (!string.IsNullOrWhiteSpace(tema.ControlesExistentes))
                                    bloco.Item().Text(t => { t.Span("Controles existentes: ").SemiBold(); t.Span(tema.ControlesExistentes); });
                                if (!string.IsNullOrWhiteSpace(tema.ControlesAdicionais))
                                    bloco.Item().Text(t => { t.Span("Controles adicionais: ").SemiBold(); t.Span(tema.ControlesAdicionais); });
                            }
                        });
                    }

                    if (modelo.TemaLivreNome is not null)
                    {
                        coluna.Item().PaddingTop(4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(bloco =>
                        {
                            bloco.Item().Text(t => { t.Span("Tema livre: ").SemiBold().FontColor(CorMarca); t.Span(modelo.TemaLivreNome); });
                            if (!string.IsNullOrWhiteSpace(modelo.TemaLivreDescricao))
                                bloco.Item().Text(modelo.TemaLivreDescricao);
                        });
                    }

                    coluna.Item().PaddingTop(8).Text("Checklist de verificação").FontSize(13).Bold();
                    foreach (var item in modelo.ItensChecklist)
                    {
                        coluna.Item().Text(t =>
                        {
                            t.Span(item.Verificado ? "[X] " : "[ ] ").FontColor(CorMarca).Bold();
                            t.Span(item.Descricao);
                        });
                    }

                    coluna.Item().PaddingTop(8).Text("Participantes").FontSize(13).Bold();
                    foreach (var nome in modelo.ParticipantesNomes)
                    {
                        coluna.Item().Text($"• {nome}");
                    }
                });
```

(A definição de `CorMarca` e o restante do método `Gerar` — cabeçalho, rodapé — não mudam.)

- [ ] **Passo 3: Atualizar `ExportarDdsPdfQuery.cs`**

Substituir o método `MontarModelo` por:

```csharp
    public static DdsPdfModelo MontarModelo(DdsDetalheDto detalhe, byte[]? obraLogoConteudo = null) => new(
        detalhe.Dds.ObraNome,
        obraLogoConteudo,
        detalhe.Dds.Data,
        detalhe.Dds.ResponsavelUsuarioNome,
        detalhe.Dds.TemasAtividades.Select(t => new DdsPdfTemaModelo(
            t.AtividadeNome, t.PerigoNome, t.PerigoDescricao, t.Consequencia, t.ControlesExistentes, t.ControlesAdicionais)).ToList(),
        detalhe.Dds.TemaLivreNome,
        detalhe.Dds.TemaLivreDescricao,
        detalhe.ItensChecklist.Select(i => (i.Descricao, i.Verificado)).ToList(),
        detalhe.Participantes.Select(p => p.TrabalhadorNome).ToList());
```

- [ ] **Passo 4: Atualizar `IDdsSemanalPdfService.cs`**

`DdsSemanalPdfDiaModelo` hoje é `record DdsSemanalPdfDiaModelo(DayOfWeek DiaSemana,
DateTime Data, string? Tema);` — usado só na grade resumida Seg-Sex do PDF semanal (não
precisa do detalhe completo, só um rótulo curto por dia). Substituir por:

```csharp
public record DdsSemanalPdfDiaModelo(DayOfWeek DiaSemana, DateTime Data, IReadOnlyList<string> AtividadesNomes, string? TemaLivreNome);
```

- [ ] **Passo 5: Atualizar `ExportarDdsSemanalPdfQuery.cs`**

Substituir:

```csharp
        var dias = detalhe.Dias
            .Select(d => new DdsSemanalPdfDiaModelo(d.DiaSemana, d.Data, d.TopicoPrincipal))
            .ToList();
```

por:

```csharp
        var dias = detalhe.Dias
            .Select(d => new DdsSemanalPdfDiaModelo(d.DiaSemana, d.Data, d.AtividadesNomes, d.TemaLivreNome))
            .ToList();
```

- [ ] **Passo 6: Ajustar `DdsSemanalPdfService.cs` para o novo shape de `Tema`**

Em `src/AAHBRANT.SST.Infrastructure/Documentos/DdsSemanalPdfService.cs`, dentro do método
que desenha a grade Seg-Sex, substituir:

```csharp
                    c.Item().PaddingTop(2).MinHeight(24).Text(dia?.Tema ?? "—").FontSize(8);
```

por:

```csharp
                    c.Item().PaddingTop(2).MinHeight(24).Text(TextoResumoTema(dia)).FontSize(8);
```

E adicionar, como método privado estático na mesma classe (perto dos demais métodos
auxiliares de desenho):

```csharp
    private static string TextoResumoTema(DdsSemanalPdfDiaModelo? dia)
    {
        if (dia is null) return "—";
        var partes = new List<string>(dia.AtividadesNomes);
        if (!string.IsNullOrWhiteSpace(dia.TemaLivreNome))
            partes.Add(dia.TemaLivreNome);
        return partes.Count > 0 ? string.Join(", ", partes) : "—";
    }
```

- [ ] **Passo 7: Atualizar a legenda do Telegram**

Em `EnviarDdsTelegramCommand.cs`, substituir:

```csharp
        var legenda = $"DDS — {detalhe.Dds.TopicoPrincipal} ({detalhe.Dds.ObraNome}, {detalhe.Dds.Data:dd/MM/yyyy})";
```

por:

```csharp
        var nomesAtividades = string.Join(", ", detalhe.Dds.AtividadesNomes);
        var legenda = $"DDS — {nomesAtividades} ({detalhe.Dds.ObraNome}, {detalhe.Dds.Data:dd/MM/yyyy})";
```

- [ ] **Passo 8: Buildar e rodar a suíte de testes inteira**

Run: `dotnet build SST-APP.sln -c Debug && dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj`
Expected: build com êxito, todos os testes (incluindo os da Tarefa 1) passando.

- [ ] **Passo 9: Commit**

```bash
git add src/AAHBRANT.SST.Application/Dds/IDdsPdfService.cs \
  src/AAHBRANT.SST.Infrastructure/Documentos/DdsPdfService.cs \
  src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsPdfQuery.cs \
  src/AAHBRANT.SST.Application/Dds/IDdsSemanalPdfService.cs \
  src/AAHBRANT.SST.Application/Dds/Queries/ExportarDdsSemanalPdfQuery.cs \
  src/AAHBRANT.SST.Infrastructure/Documentos/DdsSemanalPdfService.cs \
  src/AAHBRANT.SST.Application/Dds/Commands/EnviarDdsTelegramCommand.cs
git commit -m "feat: PDF e Telegram do DDS mostram o bloco completo de tema por atividade"
```

---

## Task 3: Backend — administração do catálogo de temas (editar)

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Dds/Commands/CriarCatalogoTemaDdsCommand.cs`
  (só o comentário de topo)
- Create: `src/AAHBRANT.SST.Application/Dds/Commands/AtualizarCatalogoTemaDdsCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/CatalogoTemasDdsController.cs`
- Create: `tests/AAHBRANT.SST.Application.Tests/Dds/AtualizarCatalogoTemaDdsCommandHandlerTests.cs`

**Interfaces:**
- Produz: `AtualizarCatalogoTemaDdsCommand(Guid Id, string Nome, string? Descricao) : IRequest`
  — consumido pela Tarefa 4 (`api.catalogoTemasDds.atualizar`).

- [ ] **Passo 1: Corrigir o comentário de `CriarCatalogoTemaDdsCommand.cs`**

Em `src/AAHBRANT.SST.Application/Dds/Commands/CriarCatalogoTemaDdsCommand.cs`,
substituir o comentário de topo (cita a antiga escolha exclusiva "Tema 3 (Livre)" removida
na Tarefa 1):

```csharp
// Catálogo pré-cadastrado de temas de DDS (31/08) — usado quando o técnico escolhe o "Tema 3
// (Livre)" em vez dos dois automáticos (cruzados com a 1ª/2ª atividade do dia). Cadastro simples,
// sem versionamento — mesmo espírito de CatalogoEpi.
```

por:

```csharp
// Catálogo pré-cadastrado de temas de DDS (31/08) — tema livre opcional, somado aos temas
// automáticos das atividades do dia (01/09), nunca os substitui. Cadastro simples, sem
// versionamento — mesmo espírito de CatalogoEpi.
```

- [ ] **Passo 2: Criar `AtualizarCatalogoTemaDdsCommand.cs`**

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record AtualizarCatalogoTemaDdsCommand(Guid Id, string Nome, string? Descricao) : IRequest;

public class AtualizarCatalogoTemaDdsCommandValidator : AbstractValidator<AtualizarCatalogoTemaDdsCommand>
{
    public AtualizarCatalogoTemaDdsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).MaximumLength(500);
    }
}

public class AtualizarCatalogoTemaDdsCommandHandler : IRequestHandler<AtualizarCatalogoTemaDdsCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarCatalogoTemaDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCatalogoTemaDdsCommand request, CancellationToken ct)
    {
        var tema = await _db.CatalogosTemaDds.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Tema de DDS não encontrado.");

        tema.Nome = request.Nome;
        tema.Descricao = request.Descricao;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Passo 3: Escrever o teste do handler**

Criar `tests/AAHBRANT.SST.Application.Tests/Dds/AtualizarCatalogoTemaDdsCommandHandlerTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class AtualizarCatalogoTemaDdsCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_TemaExistente_AtualizaNomeEDescricao()
    {
        var db = CriarDb(nameof(Handle_TemaExistente_AtualizaNomeEDescricao));
        var tema = new CatalogoTemaDds { Nome = "Nome antigo", Descricao = "Descrição antiga" };
        db.CatalogosTemaDds.Add(tema);
        await db.SaveChangesAsync();
        var handler = new AtualizarCatalogoTemaDdsCommandHandler(db);

        await handler.Handle(new AtualizarCatalogoTemaDdsCommand(tema.Id, "Outubro Amarelo", "Prevenção ao suicídio"), default);

        var atualizado = await db.CatalogosTemaDds.FirstAsync(x => x.Id == tema.Id);
        Assert.Equal("Outubro Amarelo", atualizado.Nome);
        Assert.Equal("Prevenção ao suicídio", atualizado.Descricao);
    }

    [Fact]
    public async Task Handle_TemaInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_TemaInexistente_LancaKeyNotFoundException));
        var handler = new AtualizarCatalogoTemaDdsCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AtualizarCatalogoTemaDdsCommand(Guid.NewGuid(), "Nome", null), default));
    }
}
```

- [ ] **Passo 4: Rodar os testes novos**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter "FullyQualifiedName~AtualizarCatalogoTemaDdsCommandHandlerTests"`
Expected: 2 de 2 aprovados.

- [ ] **Passo 5: Adicionar o endpoint no controller**

Em `src/AAHBRANT.SST.Api/Controllers/CatalogoTemasDdsController.cs`, primeiro corrigir o
comentário de topo (cita o enum `OrigemTemaDds`, removido na Tarefa 1) — substituir:

```csharp
// Catálogo pré-cadastrado de temas de DDS (31/08) — usado quando OrigemTemaDds = Livre. Reaproveita
// as policies do próprio módulo DDS (sem RBAC novo) para minimizar escopo.
```

por:

```csharp
// Catálogo pré-cadastrado de temas de DDS (31/08) — tema livre opcional, somado aos temas
// automáticos das atividades do dia (01/09). Reaproveita as policies do próprio módulo DDS (sem
// RBAC novo) para minimizar escopo.
```

Depois, adicionar (após o método `Criar`):

```csharp
    [Authorize(Policy = "dds:criar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCatalogoTemaDdsRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarCatalogoTemaDdsCommand(id, body.Nome, body.Descricao), ct);
        return NoContent();
    }
```

E, no final do arquivo (fora da classe `CatalogoTemasDdsController`), adicionar:

```csharp
public record AtualizarCatalogoTemaDdsRequestBody(string Nome, string? Descricao);
```

- [ ] **Passo 6: Buildar a API**

Run: `dotnet build src/AAHBRANT.SST.Api/AAHBRANT.SST.Api.csproj -c Debug`
Expected: `Compilação com êxito`.

- [ ] **Passo 7: Commit**

```bash
git add src/AAHBRANT.SST.Application/Dds/Commands/CriarCatalogoTemaDdsCommand.cs \
  src/AAHBRANT.SST.Application/Dds/Commands/AtualizarCatalogoTemaDdsCommand.cs \
  src/AAHBRANT.SST.Api/Controllers/CatalogoTemasDdsController.cs \
  tests/AAHBRANT.SST.Application.Tests/Dds/AtualizarCatalogoTemaDdsCommandHandlerTests.cs
git commit -m "feat: adiciona endpoint de atualizar tema do catálogo de DDS"
```

---

## Task 4: Frontend — tipos de API e cliente HTTP

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`

**Interfaces:**
- Consome: shape de `DdsDto`/`DdsSemanalDiaDto` da Tarefa 1, endpoint `PUT
  /api/catalogotemasdds/{id}` da Tarefa 3.
- Produz: `Dds.temasAtividades: DdsTemaAtividade[]`, `Dds.temaLivreNome`/
  `temaLivreDescricao: string | null`, `NovaDds` sem `origemTema`,
  `api.catalogoTemasDds.atualizar(id, nome, descricao?)`. Tarefas 5-7 consomem esses
  nomes.
- Remove: `OrigemTemaDds`, `origemTemaDdsLabel`, `Dds.topicoPrincipal`,
  `Dds.origemTema`, `NovaDds.origemTema`, `DdsSemanalDia.topicoPrincipal`.

- [ ] **Passo 1: Remover o enum e o label de origem do tema**

Remover, em `lib/api.ts`:

```typescript
// Reformulação 31/08 — DDS passou a ser um registro DIÁRIO dentro de uma DdsSemanal (ver abaixo). O
// "Tema do DDS" tem 3 origens possíveis (ver OrigemTemaDds), em vez de texto livre digitado na hora.
export const OrigemTemaDds = {
  AutomaticoAtividade1: 1,
  AutomaticoAtividade2: 2,
  Livre: 3,
} as const;

export const origemTemaDdsLabel: Record<number, string> = {
  1: 'Automático — 1ª atividade do dia',
  2: 'Automático — 2ª atividade do dia',
  3: 'Livre (catálogo)',
};
```

- [ ] **Passo 2: Atualizar `interface Dds` e `NovaDds`**

Substituir:

```typescript
export interface Dds {
  id: string;
  obraId: string;
  obraNome: string;
  ddsSemanalId?: string | null;
  data: string;
  responsavelUsuarioId: string;
  responsavelUsuarioNome: string;
  topicoPrincipal: string;
  origemTema: number;
  catalogoTemaDdsId?: string | null;
  status: number;
  atividadesNomes: string[];
  totalItensChecklist: number;
  itensVerificados: number;
  totalParticipantes: number;
  totalFotosEvidencia: number;
}

export interface NovaDds {
  ddsSemanalId: string;
  atividadesIds: string[];
  data: string;
  origemTema: number;
  catalogoTemaDdsId?: string | null;
}
```

por:

```typescript
export interface DdsTemaAtividade {
  atividadeId: string;
  atividadeNome: string;
  perigoNome?: string | null;
  perigoDescricao?: string | null;
  consequencia?: string | null;
  controlesExistentes?: string | null;
  controlesAdicionais?: string | null;
}

export interface Dds {
  id: string;
  obraId: string;
  obraNome: string;
  ddsSemanalId?: string | null;
  data: string;
  responsavelUsuarioId: string;
  responsavelUsuarioNome: string;
  catalogoTemaDdsId?: string | null;
  temaLivreNome?: string | null;
  temaLivreDescricao?: string | null;
  status: number;
  temasAtividades: DdsTemaAtividade[];
  atividadesNomes: string[];
  totalItensChecklist: number;
  itensVerificados: number;
  totalParticipantes: number;
  totalFotosEvidencia: number;
}

export interface NovaDds {
  ddsSemanalId: string;
  atividadesIds: string[];
  data: string;
  catalogoTemaDdsId?: string | null;
}
```

- [ ] **Passo 3: Atualizar `interface DdsSemanalDia`**

Substituir `topicoPrincipal?: string | null;` por:

```typescript
  atividadesNomes: string[];
  temaLivreNome?: string | null;
```

- [ ] **Passo 4: Adicionar `atualizar` ao cliente `catalogoTemasDds`**

Em `api.catalogoTemasDds`, adicionar (após `criar`):

```typescript
    atualizar: (id: string, nome: string, descricao?: string | null) =>
      request<void>(`/api/catalogotemasdds/${id}`, { method: 'PUT', body: JSON.stringify({ nome, descricao }) }),
```

- [ ] **Passo 5: Type-check**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc -b`
Expected: erros nos arquivos consumidores (`DashboardPage.tsx`, `AssinarDdsPage.tsx`,
`DdsDetalhePage.tsx`, `DdsSemanalDetalhePage.tsx`) — esperado, corrigidos nas Tarefas 5-7.
Confirmar que os erros são SÓ nesses 4 arquivos (nenhum outro consumidor inesperado de
`topicoPrincipal`/`origemTema`).

- [ ] **Passo 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat: tipos de API do DDS refletem temas simultâneos por atividade"
```

---

## Task 5: Frontend — usos pequenos de `topicoPrincipal`

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/DashboardPage.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/dds/AssinarDdsPage.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/dds/DdsDetalhePage.tsx`

**Interfaces:**
- Consome: `Dds.atividadesNomes: string[]`, `Dds.temaLivreNome?: string | null` (Task 4).

- [ ] **Passo 1: `DashboardPage.tsx`**

Run: `grep -n "topicoPrincipal" src/AAHBRANT.SST.TeamsApp/src/pages/DashboardPage.tsx`
para confirmar a linha exata (linha 338 no momento deste plano):

```typescript
        titulo: `DDS registrado — ${registro.topicoPrincipal}`,
```

Substituir por:

```typescript
        titulo: `DDS registrado — ${registro.atividadesNomes.join(', ')}`,
```

- [ ] **Passo 2: `AssinarDdsPage.tsx`**

Substituir:

```typescript
          Assinatura eletrônica — {dds?.topicoPrincipal ?? 'Carregando...'}
```

por:

```typescript
          Assinatura eletrônica — {dds ? dds.atividadesNomes.join(', ') : 'Carregando...'}
```

- [ ] **Passo 3: `DdsDetalhePage.tsx`**

Run: `grep -n "topicoPrincipal" src/AAHBRANT.SST.TeamsApp/src/pages/dds/DdsDetalhePage.tsx`
para localizar a linha (linha 271 no momento deste plano — verificar contexto ao redor
antes de editar, pode já estar próxima da linha 280 que usa `atividadesNomes`).
Substituir `{dds.topicoPrincipal}` por um bloco que lista os temas + tema livre:

```typescript
              {dds.temasAtividades.map((tema) => (
                <Text key={tema.atividadeId} style={{ display: 'block' }}>
                  {tema.atividadeNome}
                  {tema.perigoNome ? ` — ${tema.perigoNome}` : ''}
                </Text>
              ))}
              {dds.temaLivreNome && <Text style={{ display: 'block' }}>Tema livre: {dds.temaLivreNome}</Text>}
```

- [ ] **Passo 4: Type-check**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc -b`
Expected: sem erros nesses 3 arquivos (erros restantes só em `DdsSemanalDetalhePage.tsx`,
corrigido na Tarefa 6).

- [ ] **Passo 5: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/DashboardPage.tsx \
  src/AAHBRANT.SST.TeamsApp/src/pages/dds/AssinarDdsPage.tsx \
  src/AAHBRANT.SST.TeamsApp/src/pages/dds/DdsDetalhePage.tsx
git commit -m "feat: telas de DDS mostram atividades do dia em vez do tema único"
```

---

## Task 6: Frontend — formulário de criação do DDS do dia

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/dds/DdsSemanalDetalhePage.tsx`

**Interfaces:**
- Consome: `NovaDds` sem `origemTema` (Task 4).

- [ ] **Passo 1: Remover o import de `OrigemTemaDds`/`origemTemaDdsLabel` e os componentes não mais usados**

Substituir:

```typescript
import {
  Badge,
  Button,
  Checkbox,
  Field,
  Input,
  Radio,
  RadioGroup,
  Select,
  Text,
} from '@fluentui/react-components';
```

por (remove `Radio`/`RadioGroup`, que só eram usados pelo seletor de origem):

```typescript
import {
  Badge,
  Button,
  Checkbox,
  Field,
  Input,
  Select,
  Text,
} from '@fluentui/react-components';
```

Substituir:

```typescript
import {
  api,
  origemTemaDdsLabel,
  OrigemTemaDds,
  StatusDds,
  statusDdsLabel,
  StatusDdsSemanal,
  statusDdsSemanalLabel,
  tipoDdsSemanalLabel,
  TipoDdsSemanal,
  type Atividade,
  type CatalogoTemaDds,
  type DdsSemanalDetalhe,
} from '../../lib/api';
```

por:

```typescript
import {
  api,
  StatusDds,
  statusDdsLabel,
  StatusDdsSemanal,
  statusDdsSemanalLabel,
  tipoDdsSemanalLabel,
  TipoDdsSemanal,
  type Atividade,
  type CatalogoTemaDds,
  type DdsSemanalDetalhe,
} from '../../lib/api';
```

- [ ] **Passo 2: Simplificar `novoDiaVazio`**

Substituir:

```typescript
function novoDiaVazio() {
  return { atividadesIds: [] as string[], origemTema: OrigemTemaDds.AutomaticoAtividade1 as number, catalogoTemaDdsId: '' };
}
```

por:

```typescript
function novoDiaVazio() {
  return { atividadesIds: [] as string[], catalogoTemaDdsId: '' };
}
```

- [ ] **Passo 3: Simplificar `criarRegistroDia`**

Substituir:

```typescript
  async function criarRegistroDia() {
    if (!id || !diaEmCriacao || novoDia.atividadesIds.length === 0) {
      setErro('Selecione ao menos uma atividade do dia.');
      return;
    }
    if (novoDia.origemTema === OrigemTemaDds.Livre && !novoDia.catalogoTemaDdsId) {
      setErro('Selecione um tema do catálogo.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.criar({
        ddsSemanalId: id,
        atividadesIds: novoDia.atividadesIds,
        data: diaEmCriacao,
        origemTema: novoDia.origemTema,
        catalogoTemaDdsId: novoDia.origemTema === OrigemTemaDds.Livre ? novoDia.catalogoTemaDdsId : null,
      });
      setDiaEmCriacao(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar o registro do dia.');
    } finally {
      setProcessando(false);
    }
  }
```

por:

```typescript
  async function criarRegistroDia() {
    if (!id || !diaEmCriacao || novoDia.atividadesIds.length === 0) {
      setErro('Selecione ao menos uma atividade do dia.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.criar({
        ddsSemanalId: id,
        atividadesIds: novoDia.atividadesIds,
        data: diaEmCriacao,
        catalogoTemaDdsId: novoDia.catalogoTemaDdsId || null,
      });
      setDiaEmCriacao(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar o registro do dia.');
    } finally {
      setProcessando(false);
    }
  }
```

- [ ] **Passo 4: Trocar o rádio de origem por um seletor único de tema livre opcional**

Substituir (o bloco inteiro do `Field label="Origem do tema"` até o fechamento do `Field
label="Tema do catálogo"`, incluindo o mini-formulário inline de criar tema):

```typescript
                <Field label="Origem do tema">
                  <RadioGroup
                    value={String(novoDia.origemTema)}
                    onChange={(_, d) => setNovoDia((atual) => ({ ...atual, origemTema: Number(d.value) }))}
                  >
                    <Radio value={String(OrigemTemaDds.AutomaticoAtividade1)} label={origemTemaDdsLabel[OrigemTemaDds.AutomaticoAtividade1]} />
                    <Radio value={String(OrigemTemaDds.AutomaticoAtividade2)} label={origemTemaDdsLabel[OrigemTemaDds.AutomaticoAtividade2]} />
                    <Radio value={String(OrigemTemaDds.Livre)} label={origemTemaDdsLabel[OrigemTemaDds.Livre]} />
                  </RadioGroup>
                </Field>

                {novoDia.origemTema === OrigemTemaDds.Livre && (
                  <Field label="Tema do catálogo">
                    <Select
                      value={novoDia.catalogoTemaDdsId}
                      onChange={(_, d) => setNovoDia((atual) => ({ ...atual, catalogoTemaDdsId: d.value }))}
                    >
                      <option value="">Selecione</option>
                      {catalogoTemas.map((tema) => (
                        <option key={tema.id} value={tema.id}>
                          {tema.nome}
                        </option>
                      ))}
                    </Select>
                    <div style={{ display: 'flex', gap: 4, marginTop: 4 }}>
                      <Input
                        placeholder="Novo tema..."
                        value={novoTemaNome}
                        onChange={(_, d) => setNovoTemaNome(d.value)}
                      />
                      <Button size="small" onClick={adicionarTemaAoCatalogo} disabled={processando || !novoTemaNome.trim()}>
                        Adicionar
                      </Button>
                    </div>
                  </Field>
                )}
```

por:

```typescript
                <Text size={200} style={{ display: 'block' }}>
                  Cada atividade marcada acima entra automaticamente como um tema do dia
                  (perigo, consequência e controles já cadastrados na Matriz de Riscos dela).
                </Text>

                <Field label="Tema livre (opcional)">
                  <Select
                    value={novoDia.catalogoTemaDdsId}
                    onChange={(_, d) => setNovoDia((atual) => ({ ...atual, catalogoTemaDdsId: d.value }))}
                  >
                    <option value="">Nenhum</option>
                    {catalogoTemas.map((tema) => (
                      <option key={tema.id} value={tema.id}>
                        {tema.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
```

- [ ] **Passo 5: Remover o estado e a função de criação inline do tema (movidos para a nova aba, Tarefa 7)**

Remover a linha `const [novoTemaNome, setNovoTemaNome] = useState('');` e a função
inteira `adicionarTemaAoCatalogo` (Passos 98-113 do arquivo original). Em
`abrirCriacaoDia`, remover a linha `setNovoTemaNome('');`.

- [ ] **Passo 6: Corrigir a exibição do dia já criado na grade (fora do formulário de criação)**

O mesmo arquivo também mostra o tema de um dia JÁ CRIADO (quando `dia.ddsId` existe, fora
do fluxo de criação tratado nos passos acima). Substituir:

```typescript
                <Text style={{ display: 'block', marginBottom: 4 }}>{dia.topicoPrincipal}</Text>
```

por:

```typescript
                <Text style={{ display: 'block', marginBottom: 4 }}>
                  {dia.atividadesNomes.join(', ')}
                  {dia.temaLivreNome ? ` + ${dia.temaLivreNome}` : ''}
                </Text>
```

- [ ] **Passo 7: Type-check**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc -b`
Expected: 0 erros em todo o projeto.

- [ ] **Passo 8: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/dds/DdsSemanalDetalhePage.tsx
git commit -m "feat: criação do DDS do dia usa tema automático por atividade + tema livre opcional"
```

---

## Task 7: Frontend — nova aba "Temas de DDS"

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/dds/CatalogoTemasDdsPage.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/App.tsx`

**Interfaces:**
- Consome: `api.catalogoTemasDds.{listar,criar,atualizar,excluir}` (Tasks 3-4).

- [ ] **Passo 1: Criar `CatalogoTemasDdsPage.tsx`**

```typescript
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
  Textarea,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular, Dismiss24Regular, Edit24Regular } from '@fluentui/react-icons';
import { api, type CatalogoTemaDds } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

// Catálogo de temas livres de DDS (ex.: "Outubro Amarelo") — administração própria, separada da
// tela de conduzir o DDS do dia (DdsSemanalDetalhePage só lista/seleciona um tema já cadastrado).
export function CatalogoTemasDdsPage() {
  const estilos = usePageStyles();
  const [temas, setTemas] = useState<CatalogoTemaDds[]>([]);
  const [nome, setNome] = useState('');
  const [descricao, setDescricao] = useState('');
  const [editandoId, setEditandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setTemas(await api.catalogoTemasDds.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os temas de DDS.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function limparFormulario() {
    setNome('');
    setDescricao('');
    setEditandoId(null);
  }

  function iniciarEdicao(tema: CatalogoTemaDds) {
    setEditandoId(tema.id);
    setNome(tema.nome);
    setDescricao(tema.descricao ?? '');
  }

  async function salvar() {
    if (!nome.trim()) {
      setErro('Informe o nome do tema.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      if (editandoId) {
        await api.catalogoTemasDds.atualizar(editandoId, nome.trim(), descricao.trim() || null);
        sucessoToast('Tema atualizado com sucesso.');
      } else {
        await api.catalogoTemasDds.criar(nome.trim(), descricao.trim() || null);
        sucessoToast('Tema criado com sucesso.');
      }
      limparFormulario();
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar o tema.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este tema de DDS? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogoTemasDds.excluir(id);
      await carregar();
      sucessoToast('Tema excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir o tema.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">{editandoId ? 'Editar tema' : 'Novo tema de DDS'}</Text>
        {editandoId && (
          <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={limparFormulario}>
            Cancelar edição
          </Button>
        )}
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={nome} onChange={(_, d) => setNome(d.value)} />
        </Field>
        <Field label="Descrição">
          <Textarea value={descricao} onChange={(_, d) => setDescricao(d.value)} resize="vertical" />
        </Field>
      </div>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={salvar} disabled={carregando}>
          {editandoId ? 'Salvar alterações' : 'Criar tema'}
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : temas.length === 0 ? (
        <EstadoVazio mensagem="Nenhum tema de DDS cadastrado ainda." />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Descrição</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {temas.map((tema) => (
              <TableRow key={tema.id}>
                <TableCell>{tema.nome}</TableCell>
                <TableCell>{tema.descricao}</TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    <Button appearance="subtle" icon={<Edit24Regular />} onClick={() => iniciarEdicao(tema)} aria-label="Editar" />
                    <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(tema.id)} aria-label="Excluir" />
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
```

- [ ] **Passo 2: Registrar a rota e a aba em `App.tsx`**

Run: `grep -n "InspecaoDetalhePage\|prefixo=\"prevencao\"" src/AAHBRANT.SST.TeamsApp/src/App.tsx`
para confirmar os números de linha atuais antes de editar (podem ter mudado desde a
Tarefa de assinatura da Inspeção).

Adicionar o import (junto aos outros de `pages/dds/`):

```typescript
import { CatalogoTemasDdsPage } from './pages/dds/CatalogoTemasDdsPage';
```

No array `abas` do `PillarLayout` com `prefixo="prevencao"`, substituir:

```typescript
                  abas={[
                    { valor: 'pgr', rotulo: 'PGR' },
                    { valor: 'inspecoes', rotulo: 'Inspeções' },
                    { valor: 'dds', rotulo: 'DDS' },
                  ]}
```

por:

```typescript
                  abas={[
                    { valor: 'pgr', rotulo: 'PGR' },
                    { valor: 'inspecoes', rotulo: 'Inspeções' },
                    { valor: 'dds', rotulo: 'DDS' },
                    { valor: 'temas-dds', rotulo: 'Temas de DDS' },
                  ]}
```

E, no grupo de `<Route>` filhos desse `PillarLayout` (após a rota
`dds/dia/:id/assinar`), adicionar:

```typescript
              <Route path="temas-dds" element={<CatalogoTemasDdsPage />} />
```

- [ ] **Passo 3: Type-check**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc -b`
Expected: 0 erros.

- [ ] **Passo 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/dds/CatalogoTemasDdsPage.tsx src/AAHBRANT.SST.TeamsApp/src/App.tsx
git commit -m "feat: adiciona aba de administração do catálogo de temas de DDS"
```

---

## Task 8: Verificação final ponta a ponta

**Files:** nenhum (só validação — sem alterar código).

- [ ] **Passo 1: Build completo do backend**

Run: `dotnet build SST-APP.sln -c Debug`
Expected: `Compilação com êxito`, 0 erros, 0 avisos novos.

- [ ] **Passo 2: Suíte de testes completa do backend**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj`
Expected: todos os testes aprovados (os 8 novos desta feature + os já existentes).

- [ ] **Passo 3: Type-check completo do frontend**

Run: `cd src/AAHBRANT.SST.TeamsApp && npx tsc -b --force`
Expected: 0 erros.

- [ ] **Passo 4: Verificação visual no navegador**

Com o dev server rodando (API + Web desta worktree, ver
`feedback_dev_server_multiworktree` na memória do projeto para configurar portas sem
colidir com outros worktrees):
1. Abrir uma obra com pelo menos uma Atividade que tenha Risco cadastrado.
2. Ir em Procedimentos & Planos → Temas de DDS → criar um tema (ex.: "Outubro Amarelo").
3. Editar esse tema (confirmar que o botão de editar preenche o formulário e salva).
4. Ir em DDS → abrir/criar uma semana → criar o registro de um dia marcando 2 atividades
   (uma com Risco cadastrado, outra sem) + selecionar o tema livre criado no passo 2.
5. Abrir o registro do dia e conferir que aparecem os 2 blocos de atividade (um com
   Perigo/Descrição/Consequência/Controles, o outro com o aviso de "nenhum risco
   cadastrado") e o tema livre.
6. Baixar o PDF do dia e conferir visualmente o mesmo conteúdo.
7. Excluir o tema livre usado no passo 4 e reabrir o mesmo registro do dia — confirmar
   que o tema livre snapshotado continua aparecendo (não desaparece por o catálogo ter
   sido excluído).

- [ ] **Passo 5: Commit final (se algum ajuste tiver sido feito na verificação)**

Se o Passo 4 não exigir nenhuma correção, não há o que commitar aqui — esta tarefa é só
verificação.
