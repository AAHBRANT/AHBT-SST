# Câmeras — detecção de EPI (backend) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** API do módulo Câmeras: cadastro de câmeras por obra, análise de um quadro JPEG pelo Claude (capacete e colete), registro da ocorrência e alerta com foto no grupo do Telegram.

**Architecture:** Segue o padrão do módulo Veículos: entidades em Domain, comandos/consultas MediatR em `Application/Cameras`, EF Core em Infrastructure, um controller fino. A IA fica atrás de `IAnalisadorEpiImagem` (implementada com a Messages API do Claude, imagem em base64 e saída `json_schema`). O alerta usa `ITelegramResumoService`, que ganha um método para foto JPEG.

**Tech Stack:** .NET 8, MediatR, FluentValidation, EF Core (SQL Server; InMemory nos testes), xUnit.

**Spec:** `docs/superpowers/specs/2026-11-09-cameras-epi-design.md`

**Fora deste plano:** telas (Câmeras, Monitorar, Ocorrências), detector de movimento no navegador e entrada no `NovidadesSeeder`. Vão no plano de frontend, escrito depois que o mockup HTML das telas for aprovado pelo usuário (regra do projeto). O contrato da API para esse plano está no fim deste documento.

## Global Constraints

- Trabalhar em worktree próprio, branch `feature/cameras-epi` criada a partir de `origin/master`. Não usar `C:\Projetos\SST-APP` (está na branch do uniforme com alterações pendentes de outro trabalho).
- EPIs detectados: somente **capacete** e **colete**. Luvas fora.
- IA somente Claude (`ClaudeOptions`); não usar Azure OpenAI.
- Fotos sem irregularidade não são armazenadas.
- Teto de análise por câmera aplicado no servidor: mínimo de 25 s entre análises (o navegador envia no máximo uma a cada 30 s; 25 s tolera variação do temporizador).
- Intervalo mínimo entre alertas é por câmera, em minutos; `0` = sem limite (valor inicial).
- Escopo por obra pelo filtro global do `SstDbContext`. Exclusão só Administrador (`PoliticasAutorizacao.SomenteAdministrador`).
- Permissões: reaproveitar `inspecao:ver` e `inspecao:criar` (mesma decisão do módulo Veículos); não criar módulo novo no RBAC.
- Não excluir nem renomear código existente fora do que o plano lista. Não resetar banco local. Não fazer deploy.
- Mensagens ao usuário em português, sem a palavra "vencido".
- Commits terminam com `Co-Authored-By: Claude <noreply@anthropic.com>`.
- Rodar testes: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~Cameras"` e `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests --filter "FullyQualifiedName~Cameras"`.

## File Structure

| Arquivo | Responsabilidade |
| --- | --- |
| `src/AAHBRANT.SST.Domain/Enums/Enums.cs` (modificar) | `TipoCamera`, `SituacaoOcorrenciaEpi`, `ConfiancaAnaliseEpi` |
| `src/AAHBRANT.SST.Domain/Entidades/Camera.cs` (criar) | Entidades `Camera` e `OcorrenciaEpiCamera` |
| `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/CamerasConfiguracoes.cs` (criar) | Mapeamento EF |
| `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`, `.../Persistencia/SstDbContext.cs` (modificar) | DbSets e filtro por obra |
| `src/AAHBRANT.SST.Application/Cameras/Commands/*.cs`, `Queries/*.cs` (criar) | Cadastro, análise de quadro, ocorrências |
| `src/AAHBRANT.SST.Application/Cameras/IAnalisadorEpiImagem.cs` (criar) | Interface da IA e `ResultadoAnaliseEpi` |
| `src/AAHBRANT.SST.Application/Cameras/LegendaAlertaEpi.cs` (criar) | Texto do alerta |
| `src/AAHBRANT.SST.Infrastructure/Integracao/Cameras/ClaudeAnalisadorEpiImagem.cs` (criar) | Implementação com Claude |
| `src/AAHBRANT.SST.Application/Common/Interfaces/ITelegramResumoService.cs`, `.../Telegram/TelegramResumoService.cs` (modificar) | Envio de foto JPEG |
| `src/AAHBRANT.SST.Api/Controllers/CamerasController.cs` (criar) | Endpoints |

---

### Task 1: Domínio e persistência

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Enums/Enums.cs` (acrescentar no fim do namespace)
- Create: `src/AAHBRANT.SST.Domain/Entidades/Camera.cs`
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/CamerasConfiguracoes.cs`
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs` (junto de `DbSet<Veiculo> Veiculos`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs` (DbSets junto de `Veiculos`, linha ~103; filtros logo após o filtro de `Veiculo`, linha ~264)
- Create: migration `CriarModuloCameras`
- Test: `tests/AAHBRANT.SST.Application.Tests/Cameras/CamerasPersistenciaTests.cs`

**Interfaces:**
- Produces: `Camera`, `OcorrenciaEpiCamera`, enums `TipoCamera`, `SituacaoOcorrenciaEpi`, `ConfiancaAnaliseEpi`; `IAppDbContext.Cameras`, `IAppDbContext.OcorrenciasEpiCamera`.

- [ ] **Step 1: Escrever o teste que falha**

```csharp
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class CamerasPersistenciaTests
{
    internal static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task GravaCameraEOcorrencia_ComValoresPadrao()
    {
        var db = CriarDb(nameof(GravaCameraEOcorrencia_ComValoresPadrao));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira", Tipo = TipoCamera.Canteiro };
        var ocorrencia = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id,
            ObraId = obra.Id,
            CapturadaEmUtc = DateTime.UtcNow,
            RecebidaEmUtc = DateTime.UtcNow,
            Foto = new byte[] { 0xFF, 0xD8, 0x00 },
            PessoasNaCena = 2,
            SemCapacete = 1,
            Confianca = ConfiancaAnaliseEpi.Alta,
        };
        db.AddRange(obra, camera, ocorrencia);
        await db.SaveChangesAsync();

        var lida = await db.Cameras.SingleAsync();
        Assert.True(lida.Habilitada);
        Assert.Equal(0, lida.IntervaloMinimoAlertasMinutos);
        var ocorrenciaLida = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(SituacaoOcorrenciaEpi.Nova, ocorrenciaLida.Situacao);
        Assert.False(ocorrenciaLida.AlertaEnviado);
    }
}
```

- [ ] **Step 2: Rodar e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~Cameras"`
Expected: erro de compilação (`Camera` não existe).

- [ ] **Step 3: Enums**

Acrescentar em `Enums.cs`:

```csharp
public enum TipoCamera
{
    Canteiro = 1,
    Portaria = 2,
}

public enum SituacaoOcorrenciaEpi
{
    Nova = 1,
    Conferida = 2,
    FalsoAlerta = 3,
}

public enum ConfiancaAnaliseEpi
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
}
```

- [ ] **Step 4: Entidades**

`src/AAHBRANT.SST.Domain/Entidades/Camera.cs`:

```csharp
using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Câmera de monitoramento de EPI cadastrada por obra. Na fase 1 a imagem vem da câmera do
// tablet (tela Monitorar); a câmera fixa da obra entra depois como outra fonte, sem mudar o cadastro.
public class Camera : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid? AreaSstId { get; set; }
    public AreaSst? AreaSst { get; set; }

    public TipoCamera Tipo { get; set; } = TipoCamera.Canteiro;

    // Desligar o monitoramento sem excluir o cadastro (Ativo é a exclusão lógica).
    public bool Habilitada { get; set; } = true;

    // 0 = sem limite: alerta a cada ocorrência detectada.
    public int IntervaloMinimoAlertasMinutos { get; set; }

    // Controle do teto de análises no servidor e do intervalo entre alertas.
    public DateTime? UltimaAnaliseEmUtc { get; set; }
    public DateTime? UltimoAlertaEmUtc { get; set; }
}

// Pessoa sem capacete e/ou sem colete detectada numa foto. Só existe quando há irregularidade:
// fotos de cena regular não são gravadas.
public class OcorrenciaEpiCamera : AuditableEntity
{
    public Guid CameraId { get; set; }
    public Camera? Camera { get; set; }

    // Copiado da câmera para o filtro de escopo por obra não depender de navegação.
    public Guid ObraId { get; set; }

    public DateTime CapturadaEmUtc { get; set; }
    public DateTime RecebidaEmUtc { get; set; }

    public byte[] Foto { get; set; } = Array.Empty<byte>();

    public int PessoasNaCena { get; set; }
    public int SemCapacete { get; set; }
    public int SemColete { get; set; }
    public ConfiancaAnaliseEpi Confianca { get; set; }

    public bool AlertaEnviado { get; set; }
    public DateTime? AlertaEnviadoEmUtc { get; set; }

    // Vínculo manual feito pelo técnico (fase 1 não identifica o trabalhador automaticamente).
    public Guid? TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid? VinculadoPorUsuarioId { get; set; }
    public DateTime? VinculadoEmUtc { get; set; }

    public SituacaoOcorrenciaEpi Situacao { get; set; } = SituacaoOcorrenciaEpi.Nova;
}
```

- [ ] **Step 5: Mapeamento EF, DbSets e filtros**

`CamerasConfiguracoes.cs`:

```csharp
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class CameraConfiguracao : IEntityTypeConfiguration<Camera>
{
    public void Configure(EntityTypeBuilder<Camera> builder)
    {
        builder.ToTable("Cameras");
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(100);
        builder.HasOne(c => c.Obra).WithMany()
            .HasForeignKey(c => c.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.AreaSst).WithMany()
            .HasForeignKey(c => c.AreaSstId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.ObraId, c.Nome });
    }
}

public class OcorrenciaEpiCameraConfiguracao : IEntityTypeConfiguration<OcorrenciaEpiCamera>
{
    public void Configure(EntityTypeBuilder<OcorrenciaEpiCamera> builder)
    {
        builder.ToTable("OcorrenciasEpiCamera");
        builder.Property(o => o.Foto).IsRequired();
        builder.HasOne(o => o.Camera).WithMany()
            .HasForeignKey(o => o.CameraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Trabalhador).WithMany()
            .HasForeignKey(o => o.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.ObraId, o.RecebidaEmUtc });
        builder.HasIndex(o => new { o.CameraId, o.RecebidaEmUtc });
    }
}
```

`IAppDbContext.cs`, logo após `DbSet<Veiculo> Veiculos { get; }`:

```csharp
    DbSet<Camera> Cameras { get; }
    DbSet<OcorrenciaEpiCamera> OcorrenciasEpiCamera { get; }
```

`SstDbContext.cs`, logo após `public DbSet<Veiculo> Veiculos => Set<Veiculo>();`:

```csharp
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<OcorrenciaEpiCamera> OcorrenciasEpiCamera => Set<OcorrenciaEpiCamera>();
```

`SstDbContext.cs`, logo após o `HasQueryFilter` de `Veiculo`:

```csharp
        modelBuilder.Entity<Camera>().HasQueryFilter(c =>
            c.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(c.ObraId)));
        modelBuilder.Entity<OcorrenciaEpiCamera>().HasQueryFilter(o =>
            o.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(o.ObraId)));
```

- [ ] **Step 6: Rodar o teste**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~Cameras"`
Expected: PASS (1 teste).

- [ ] **Step 7: Gerar a migration e conferir**

```bash
dotnet ef migrations add CriarModuloCameras --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api
```

Abrir o arquivo gerado em `src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/` e conferir que o `Up` contém **apenas** `CreateTable` de `Cameras` e `OcorrenciasEpiCamera` com seus índices. Se aparecer `DropTable`, `DropColumn`, `AlterColumn` (inclusive o padrão `AlterColumn<byte[]>(..., type: "rowversion"` descrito em `ONBOARDING.md` linha ~364) ou qualquer tabela de outro módulo, **parar e reportar** — não editar à mão nem aplicar.

Run: `dotnet ef migrations has-pending-model-changes --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api`
Expected: sem alterações pendentes.

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Domain src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia tests/AAHBRANT.SST.Application.Tests/Cameras
git commit -m "feat(cameras): entidades e persistência de câmeras e ocorrências de EPI"
```

---

### Task 2: Cadastro de câmeras

**Files:**
- Create: `src/AAHBRANT.SST.Application/Cameras/Commands/CriarCameraCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Cameras/Commands/AtualizarCameraCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Cameras/Commands/ExcluirCameraCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Cameras/Queries/ListarCamerasQuery.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Cameras/CamerasCadastroTests.cs`

**Interfaces:**
- Consumes: `Camera`, `IAppDbContext.Cameras` (Task 1).
- Produces: `CriarCameraCommand(Guid ObraId, string Nome, Guid? AreaSstId, TipoCamera Tipo, bool Habilitada, int IntervaloMinimoAlertasMinutos) : IRequest<Guid>`; `AtualizarCameraCommand(Guid Id, string Nome, Guid? AreaSstId, TipoCamera Tipo, bool Habilitada, int IntervaloMinimoAlertasMinutos) : IRequest`; `ExcluirCameraCommand(Guid Id) : IRequest`; `ListarCamerasQuery(Guid? ObraId) : IRequest<List<CameraDto>>`; `CameraDto(Guid Id, Guid ObraId, string ObraNome, string Nome, Guid? AreaSstId, string? AreaNome, TipoCamera Tipo, bool Habilitada, int IntervaloMinimoAlertasMinutos, DateTime? UltimaAnaliseEmUtc, int OcorrenciasHoje)`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class CamerasCadastroTests
{
    [Fact]
    public async Task Criar_GravaCameraNaObra()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_GravaCameraNaObra));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Add(obra);
        await db.SaveChangesAsync();

        var id = await new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(obra.Id, "  Betoneira ", null, TipoCamera.Canteiro, true, 0), default);

        var camera = await db.Cameras.SingleAsync(c => c.Id == id);
        Assert.Equal("Betoneira", camera.Nome);
        Assert.Equal(obra.Id, camera.ObraId);
    }

    [Fact]
    public async Task Criar_NomeRepetidoNaMesmaObra_LancaInvalidOperationException()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_NomeRepetidoNaMesmaObra_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.AddRange(obra, new Camera { ObraId = obra.Id, Nome = "Betoneira" });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(obra.Id, "Betoneira", null, TipoCamera.Canteiro, true, 0), default));
    }

    [Fact]
    public async Task Criar_ObraInexistente_LancaKeyNotFoundException()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_ObraInexistente_LancaKeyNotFoundException));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(Guid.NewGuid(), "Betoneira", null, TipoCamera.Canteiro, true, 0), default));
    }

    [Fact]
    public async Task Atualizar_AlteraCamposSemMudarObra()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Atualizar_AlteraCamposSemMudarObra));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        db.AddRange(obra, camera);
        await db.SaveChangesAsync();

        await new AtualizarCameraCommandHandler(db).Handle(
            new AtualizarCameraCommand(camera.Id, "Portaria", null, TipoCamera.Portaria, false, 10), default);

        var lida = await db.Cameras.SingleAsync();
        Assert.Equal("Portaria", lida.Nome);
        Assert.Equal(TipoCamera.Portaria, lida.Tipo);
        Assert.False(lida.Habilitada);
        Assert.Equal(10, lida.IntervaloMinimoAlertasMinutos);
        Assert.Equal(obra.Id, lida.ObraId);
    }

    [Fact]
    public async Task Excluir_FazExclusaoLogicaEPreservaOcorrencias()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Excluir_FazExclusaoLogicaEPreservaOcorrencias));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        var ocorrencia = new OcorrenciaEpiCamera { CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 1 }, SemCapacete = 1, PessoasNaCena = 1 };
        db.AddRange(obra, camera, ocorrencia);
        await db.SaveChangesAsync();

        await new ExcluirCameraCommandHandler(db).Handle(new ExcluirCameraCommand(camera.Id), default);

        Assert.Empty(await db.Cameras.ToListAsync());
        Assert.Single(await db.OcorrenciasEpiCamera.ToListAsync());
    }

    [Fact]
    public async Task Listar_FiltraPorObraEContaOcorrenciasDeHoje()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Listar_FiltraPorObraEContaOcorrenciasDeHoje));
        var obra1 = new Obra { Codigo = "OB1", Nome = "Obra Um" };
        var obra2 = new Obra { Codigo = "OB2", Nome = "Obra Dois" };
        var camera1 = new Camera { ObraId = obra1.Id, Nome = "Betoneira" };
        var camera2 = new Camera { ObraId = obra2.Id, Nome = "Portaria" };
        db.AddRange(obra1, obra2, camera1, camera2,
            new OcorrenciaEpiCamera { CameraId = camera1.Id, ObraId = obra1.Id, Foto = new byte[] { 1 }, RecebidaEmUtc = DateTime.UtcNow },
            new OcorrenciaEpiCamera { CameraId = camera1.Id, ObraId = obra1.Id, Foto = new byte[] { 1 }, RecebidaEmUtc = DateTime.UtcNow.AddDays(-3) });
        await db.SaveChangesAsync();

        var lista = await new ListarCamerasQueryHandler(db).Handle(new ListarCamerasQuery(obra1.Id), default);

        var item = Assert.Single(lista);
        Assert.Equal("Betoneira", item.Nome);
        Assert.Equal("Obra Um", item.ObraNome);
        Assert.Equal(1, item.OcorrenciasHoje);
    }

    [Fact]
    public void Validador_RecusaNomeVazioEIntervaloNegativo()
    {
        var validador = new CriarCameraCommandValidator();

        Assert.False(validador.Validate(new CriarCameraCommand(Guid.NewGuid(), "", null, TipoCamera.Canteiro, true, 0)).IsValid);
        Assert.False(validador.Validate(new CriarCameraCommand(Guid.NewGuid(), "Betoneira", null, TipoCamera.Canteiro, true, -1)).IsValid);
        Assert.True(validador.Validate(new CriarCameraCommand(Guid.NewGuid(), "Betoneira", null, TipoCamera.Canteiro, true, 0)).IsValid);
    }
}
```

- [ ] **Step 2: Rodar e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~CamerasCadastro"`
Expected: erro de compilação (`CriarCameraCommand` não existe).

- [ ] **Step 3: Implementar**

`CriarCameraCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

public record CriarCameraCommand(
    Guid ObraId,
    string Nome,
    Guid? AreaSstId,
    TipoCamera Tipo,
    bool Habilitada,
    int IntervaloMinimoAlertasMinutos) : IRequest<Guid>;

public class CriarCameraCommandValidator : AbstractValidator<CriarCameraCommand>
{
    public CriarCameraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.IntervaloMinimoAlertasMinutos).InclusiveBetween(0, 1440);
    }
}

public class CriarCameraCommandHandler : IRequestHandler<CriarCameraCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCameraCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.AreaSstId is { } areaId && !await _db.AreasSst.AnyAsync(a => a.Id == areaId && a.ObraId == request.ObraId, ct))
            throw new KeyNotFoundException("Área não encontrada nesta obra.");

        var nome = request.Nome.Trim();
        if (await _db.Cameras.AnyAsync(c => c.ObraId == request.ObraId && c.Nome == nome, ct))
            throw new InvalidOperationException($"Já existe uma câmera chamada \"{nome}\" nesta obra.");

        var camera = new Camera
        {
            ObraId = request.ObraId,
            Nome = nome,
            AreaSstId = request.AreaSstId,
            Tipo = request.Tipo,
            Habilitada = request.Habilitada,
            IntervaloMinimoAlertasMinutos = request.IntervaloMinimoAlertasMinutos,
        };
        _db.Cameras.Add(camera);
        await _db.SaveChangesAsync(ct);
        return camera.Id;
    }
}
```

`AtualizarCameraCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// A obra não muda na edição: as ocorrências já gravadas carregam o ObraId da câmera.
public record AtualizarCameraCommand(
    Guid Id,
    string Nome,
    Guid? AreaSstId,
    TipoCamera Tipo,
    bool Habilitada,
    int IntervaloMinimoAlertasMinutos) : IRequest;

public class AtualizarCameraCommandValidator : AbstractValidator<AtualizarCameraCommand>
{
    public AtualizarCameraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.IntervaloMinimoAlertasMinutos).InclusiveBetween(0, 1440);
    }
}

public class AtualizarCameraCommandHandler : IRequestHandler<AtualizarCameraCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCameraCommand request, CancellationToken ct)
    {
        var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Câmera {request.Id} não encontrada.");

        if (request.AreaSstId is { } areaId && !await _db.AreasSst.AnyAsync(a => a.Id == areaId && a.ObraId == camera.ObraId, ct))
            throw new KeyNotFoundException("Área não encontrada nesta obra.");

        var nome = request.Nome.Trim();
        if (await _db.Cameras.AnyAsync(c => c.Id != camera.Id && c.ObraId == camera.ObraId && c.Nome == nome, ct))
            throw new InvalidOperationException($"Já existe uma câmera chamada \"{nome}\" nesta obra.");

        camera.Nome = nome;
        camera.AreaSstId = request.AreaSstId;
        camera.Tipo = request.Tipo;
        camera.Habilitada = request.Habilitada;
        camera.IntervaloMinimoAlertasMinutos = request.IntervaloMinimoAlertasMinutos;
        await _db.SaveChangesAsync(ct);
    }
}
```

`ExcluirCameraCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Exclusão lógica (Ativo=false): as ocorrências já registradas continuam no banco. A política
// "só Administrador" é aplicada no controller.
public record ExcluirCameraCommand(Guid Id) : IRequest;

public class ExcluirCameraCommandHandler : IRequestHandler<ExcluirCameraCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCameraCommand request, CancellationToken ct)
    {
        var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Câmera {request.Id} não encontrada.");

        camera.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
```

`ListarCamerasQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

public record ListarCamerasQuery(Guid? ObraId) : IRequest<List<CameraDto>>;

public record CameraDto(
    Guid Id,
    Guid ObraId,
    string ObraNome,
    string Nome,
    Guid? AreaSstId,
    string? AreaNome,
    TipoCamera Tipo,
    bool Habilitada,
    int IntervaloMinimoAlertasMinutos,
    DateTime? UltimaAnaliseEmUtc,
    int OcorrenciasHoje);

public class ListarCamerasQueryHandler : IRequestHandler<ListarCamerasQuery, List<CameraDto>>
{
    private readonly IAppDbContext _db;
    public ListarCamerasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CameraDto>> Handle(ListarCamerasQuery request, CancellationToken ct)
    {
        // "Hoje" aproximado pelas últimas 24 h: evita depender de fuso na consulta.
        var desde = DateTime.UtcNow.AddHours(-24);

        var query = _db.Cameras.AsNoTracking();
        if (request.ObraId is { } obraId) query = query.Where(c => c.ObraId == obraId);

        return await query
            .OrderBy(c => c.Nome)
            .Select(c => new CameraDto(
                c.Id,
                c.ObraId,
                c.Obra!.Nome,
                c.Nome,
                c.AreaSstId,
                c.AreaSst != null ? c.AreaSst.Nome : null,
                c.Tipo,
                c.Habilitada,
                c.IntervaloMinimoAlertasMinutos,
                c.UltimaAnaliseEmUtc,
                _db.OcorrenciasEpiCamera.Count(o => o.CameraId == c.Id && o.RecebidaEmUtc >= desde)))
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~Cameras"`
Expected: PASS (8 testes).

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Cameras tests/AAHBRANT.SST.Application.Tests/Cameras
git commit -m "feat(cameras): cadastro de câmeras por obra"
```

---

### Task 3: Analisador de EPI com Claude

**Files:**
- Create: `src/AAHBRANT.SST.Application/Cameras/IAnalisadorEpiImagem.cs`
- Create: `src/AAHBRANT.SST.Infrastructure/Integracao/Cameras/ClaudeAnalisadorEpiImagem.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs` (logo após `services.AddScoped<ClaudeChatJsonCliente>();`, linha ~84)
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Cameras/ClaudeAnalisadorEpiImagemTests.cs`

**Interfaces:**
- Produces: `IAnalisadorEpiImagem.AnalisarAsync(byte[] imagemJpeg, CancellationToken ct) : Task<ResultadoAnaliseEpi>`; `record ResultadoAnaliseEpi(int Pessoas, int SemCapacete, int SemColete, ConfiancaAnaliseEpi Confianca)`. Lança `InvalidOperationException` quando a IA falha, não está configurada ou devolve números incoerentes.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
using System.Net;
using System.Text.Json;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.Cameras;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Cameras;

public class ClaudeAnalisadorEpiImagemTests
{
    private sealed class HandlerFalso : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _corpoResposta;
        public string? CorpoEnviado { get; private set; }
        public HttpRequestMessage? Requisicao { get; private set; }

        public HandlerFalso(HttpStatusCode status, string corpoResposta)
        {
            _status = status;
            _corpoResposta = corpoResposta;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requisicao = request;
            CorpoEnviado = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status) { Content = new StringContent(_corpoResposta) };
        }
    }

    private sealed class FabricaFalsa : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FabricaFalsa(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static string RespostaClaude(string jsonInterno)
        => JsonSerializer.Serialize(new { content = new object[] { new { type = "text", text = jsonInterno } } });

    private static ClaudeAnalisadorEpiImagem Criar(HandlerFalso handler, string? apiKey = "chave-de-teste")
    {
        var cliente = new ClaudeChatJsonCliente(
            new FabricaFalsa(handler),
            Options.Create(new AzureOpenAiOptions()),
            Options.Create(new ClaudeOptions { ApiKey = apiKey }),
            NullLogger<ClaudeChatJsonCliente>.Instance);
        return new ClaudeAnalisadorEpiImagem(cliente);
    }

    [Fact]
    public async Task Analisar_DevolveContagensEEnviaImagemEmBase64()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK,
            RespostaClaude("{\"pessoas\":2,\"semCapacete\":1,\"semColete\":0,\"confianca\":\"alta\"}"));
        var imagem = new byte[] { 0xFF, 0xD8, 0x01, 0x02 };

        var resultado = await Criar(handler).AnalisarAsync(imagem, default);

        Assert.Equal(2, resultado.Pessoas);
        Assert.Equal(1, resultado.SemCapacete);
        Assert.Equal(0, resultado.SemColete);
        Assert.Equal(ConfiancaAnaliseEpi.Alta, resultado.Confianca);
        Assert.Contains(Convert.ToBase64String(imagem), handler.CorpoEnviado);
        Assert.Contains("image/jpeg", handler.CorpoEnviado);
        Assert.EndsWith("/v1/messages", handler.Requisicao!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Analisar_NumerosIncoerentes_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK,
            RespostaClaude("{\"pessoas\":1,\"semCapacete\":3,\"semColete\":0,\"confianca\":\"media\"}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
    }

    [Fact]
    public async Task Analisar_FalhaDoClaude_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.BadRequest, "erro");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
    }

    [Fact]
    public async Task Analisar_SemChaveConfigurada_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespostaClaude("{}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler, apiKey: null).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
        Assert.Null(handler.Requisicao);
    }
}
```

- [ ] **Step 2: Rodar e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests --filter "FullyQualifiedName~Cameras"`
Expected: erro de compilação (`ClaudeAnalisadorEpiImagem` não existe).

- [ ] **Step 3: Interface**

`src/AAHBRANT.SST.Application/Cameras/IAnalisadorEpiImagem.cs`:

```csharp
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Cameras;

// Isola a IA do resto do módulo: cadastro, registro e alerta não sabem qual motor analisa a foto.
// Lança InvalidOperationException quando a análise não pôde ser feita ou veio incoerente.
public interface IAnalisadorEpiImagem
{
    Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct);
}

public record ResultadoAnaliseEpi(int Pessoas, int SemCapacete, int SemColete, ConfiancaAnaliseEpi Confianca);
```

- [ ] **Step 4: Cliente do Claude — sem alteração**

`ClaudeChatJsonCliente.ObterAsync<T>(instrucoesSistema, mensagemUsuario, nomeSchema, schema, ct, maxTokens, timeoutSegundos, imagens)` já aceita imagens como data URL (`data:image/jpeg;base64,...`) e já tenta de novo em 429/5xx. Não modificar esse arquivo.

- [ ] **Step 5: Implementação do analisador**

`src/AAHBRANT.SST.Infrastructure/Integracao/Cameras/ClaudeAnalisadorEpiImagem.cs`:

```csharp
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

namespace AAHBRANT.SST.Infrastructure.Integracao.Cameras;

public class ClaudeAnalisadorEpiImagem : IAnalisadorEpiImagem
{
    private const string Instrucoes =
        "Você analisa fotos de canteiro de obras para segurança do trabalho. " +
        "Conte apenas pessoas claramente visíveis na imagem. " +
        "Para cada pessoa, verifique: (1) capacete de segurança na cabeça; boné, touca, capuz ou chapéu não contam como capacete; " +
        "(2) colete refletivo ou de alta visibilidade sobre o tronco. " +
        "Se a pessoa estiver longe demais, encoberta ou de um ângulo em que não dá para ter certeza, considere que ela está usando o item e reduza a confiança. " +
        "Não identifique pessoas nem descreva rostos. Responda somente no formato solicitado.";

    private const string Pedido =
        "Informe: pessoas (total de pessoas visíveis), semCapacete (quantas sem capacete), " +
        "semColete (quantas sem colete) e confianca (alta, media ou baixa) da sua avaliação.";

    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            pessoas = new { type = "integer" },
            semCapacete = new { type = "integer" },
            semColete = new { type = "integer" },
            confianca = new { type = "string", @enum = new[] { "alta", "media", "baixa" } },
        },
        required = new[] { "pessoas", "semCapacete", "semColete", "confianca" },
        additionalProperties = false,
    };

    private sealed record Resposta(int Pessoas, int SemCapacete, int SemColete, string? Confianca);

    private readonly ClaudeChatJsonCliente _cliente;
    public ClaudeAnalisadorEpiImagem(ClaudeChatJsonCliente cliente) => _cliente = cliente;

    public async Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct)
    {
        var imagens = new[] { $"data:image/jpeg;base64,{Convert.ToBase64String(imagemJpeg)}" };
        var r = await _cliente.ObterAsync<Resposta>(Instrucoes, Pedido, "analise_epi", Schema, ct,
            maxTokens: 500, timeoutSegundos: 60, imagens: imagens);

        if (r.Pessoas < 0 || r.SemCapacete < 0 || r.SemColete < 0 || r.SemCapacete > r.Pessoas || r.SemColete > r.Pessoas)
            throw new InvalidOperationException("A IA devolveu contagens incoerentes.");

        var confianca = r.Confianca?.ToLowerInvariant() switch
        {
            "alta" => ConfiancaAnaliseEpi.Alta,
            "media" => ConfiancaAnaliseEpi.Media,
            _ => ConfiancaAnaliseEpi.Baixa,
        };
        return new ResultadoAnaliseEpi(r.Pessoas, r.SemCapacete, r.SemColete, confianca);
    }
}
```

Em `DependencyInjection.cs`, logo após `services.AddScoped<ClaudeChatJsonCliente>();`:

```csharp
        services.AddScoped<AAHBRANT.SST.Application.Cameras.IAnalisadorEpiImagem, AAHBRANT.SST.Infrastructure.Integracao.Cameras.ClaudeAnalisadorEpiImagem>();
```

- [ ] **Step 6: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests --filter "FullyQualifiedName~Cameras"`
Expected: PASS (4 testes).

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~SuporteIa|FullyQualifiedName~Relato"`
Expected: PASS — os classificadores de relato continuam funcionando após a extração de `EnviarAoClaudeAsync`.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.Application/Cameras/IAnalisadorEpiImagem.cs src/AAHBRANT.SST.Infrastructure/Integracao src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs tests/AAHBRANT.SST.Infrastructure.Tests/Cameras
git commit -m "feat(cameras): analisador de capacete e colete por imagem com Claude"
```

---

### Task 4: Legenda do alerta e envio de foto JPEG no Telegram

**Files:**
- Create: `src/AAHBRANT.SST.Application/Cameras/LegendaAlertaEpi.cs`
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/ITelegramResumoService.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Integracao/Telegram/TelegramResumoService.cs` (método `EnviarImagemAsync`, linhas 27–52)
- Test: `tests/AAHBRANT.SST.Application.Tests/Cameras/LegendaAlertaEpiTests.cs`

**Interfaces:**
- Produces: `LegendaAlertaEpi.Montar(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete) : string`.
- Produces: `ITelegramResumoService.EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default) : Task<bool>` — `false` quando não enviou.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
using AAHBRANT.SST.Application.Cameras;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class LegendaAlertaEpiTests
{
    // 18:06 UTC = 15:06 em Brasília.
    private static readonly DateTime Momento = new(2026, 11, 9, 18, 6, 0, DateTimeKind.Utc);

    [Fact]
    public void SemCapacete_MontaTresLinhas()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 2, 1, 0);

        Assert.Equal(
            "⚠️ EPI — pessoa sem capacete\n" +
            "Obra Beira Rio · Câmera Betoneira · 09/11 15:06\n" +
            "2 pessoas na cena, 1 sem capacete",
            legenda);
    }

    [Fact]
    public void SemColete_UsaTituloDeColete()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 1, 0, 1);

        Assert.StartsWith("⚠️ EPI — pessoa sem colete\n", legenda);
        Assert.EndsWith("1 pessoa na cena, 1 sem colete", legenda);
    }

    [Fact]
    public void SemOsDois_CitaCapaceteEColete()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 3, 2, 1);

        Assert.StartsWith("⚠️ EPI — pessoa sem capacete e sem colete\n", legenda);
        Assert.EndsWith("3 pessoas na cena, 2 sem capacete, 1 sem colete", legenda);
    }
}
```

- [ ] **Step 2: Rodar e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~LegendaAlertaEpi"`
Expected: erro de compilação (`LegendaAlertaEpi` não existe).

- [ ] **Step 3: Implementar a legenda**

```csharp
namespace AAHBRANT.SST.Application.Cameras;

// Texto do alerta de EPI enviado como legenda da foto no Telegram (spec §6).
public static class LegendaAlertaEpi
{
    public static string Montar(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete)
    {
        var titulo = (semCapacete > 0, semColete > 0) switch
        {
            (true, true) => "pessoa sem capacete e sem colete",
            (true, false) => "pessoa sem capacete",
            _ => "pessoa sem colete",
        };

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(momentoUtc, DateTimeKind.Utc), FusoBrasilia());

        var partes = new List<string> { $"{pessoas} {(pessoas == 1 ? "pessoa" : "pessoas")} na cena" };
        if (semCapacete > 0) partes.Add($"{semCapacete} sem capacete");
        if (semColete > 0) partes.Add($"{semColete} sem colete");

        return $"⚠️ EPI — {titulo}\n" +
               $"Obra {obraNome} · Câmera {cameraNome} · {local:dd/MM HH:mm}\n" +
               string.Join(", ", partes);
    }

    private static TimeZoneInfo FusoBrasilia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
```

- [ ] **Step 4: Envio de foto JPEG no Telegram**

Em `ITelegramResumoService.cs`, acrescentar dentro da interface:

```csharp
    // Envia uma foto JPEG com legenda (alerta de EPI das câmeras). Devolve false quando não enviou.
    Task<bool> EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default);
```

Em `TelegramResumoService.cs`, substituir o método `EnviarImagemAsync` inteiro por:

```csharp
    public Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default)
        => EnviarFotoInternoAsync(imagemPng, "image/png", "resumo-dds.png", legenda, ct);

    public Task<bool> EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default)
        => EnviarFotoInternoAsync(imagemJpeg, "image/jpeg", "alerta-epi.jpg", legenda, ct);

    private async Task<bool> EnviarFotoInternoAsync(byte[] imagem, string tipoConteudo, string nomeArquivo, string legenda, CancellationToken ct)
    {
        var token = _options.Value.BotToken;
        var chatId = ObterChatId();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogInformation("Telegram de resumos não configurado (Telegram:BotToken e Telegram:SuporteChatId ou ResumoChatId): imagem não enviada.");
            return false;
        }

        using var corpo = new MultipartFormDataContent();
        corpo.Add(new StringContent(chatId), "chat_id");
        corpo.Add(new StringContent(legenda.Length > 1000 ? legenda[..1000] : legenda), "caption");
        var foto = new ByteArrayContent(imagem);
        foto.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(tipoConteudo);
        corpo.Add(foto, "photo", nomeArquivo);

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync($"https://api.telegram.org/bot{token}/sendPhoto", corpo, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Falha ao enviar a imagem ao Telegram: {StatusCode}", response.StatusCode);
            return false;
        }
        return true;
    }
```

Depois, localizar as outras implementações da interface (dublês de teste) e acrescentar o método em cada uma:

Run: `grep -rn ": ITelegramResumoService" src tests --include=*.cs`

Em cada classe encontrada além de `TelegramResumoService`, acrescentar:

```csharp
    public Task<bool> EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default) => Task.FromResult(true);
```

- [ ] **Step 5: Rodar os testes e compilar tudo**

Run: `dotnet build SST-APP.sln`
Expected: build sem erros (confirma que nenhum dublê ficou sem o método novo).

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~Cameras"`
Expected: PASS (11 testes).

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat(cameras): legenda do alerta de EPI e envio de foto JPEG no Telegram"
```

---

### Task 5: Análise de quadro, registro da ocorrência e alerta

**Files:**
- Create: `src/AAHBRANT.SST.Application/Cameras/Commands/AnalisarQuadroCameraCommand.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Cameras/AnalisarQuadroCameraTests.cs`

**Interfaces:**
- Consumes: `IAnalisadorEpiImagem`, `ResultadoAnaliseEpi` (Task 3); `LegendaAlertaEpi.Montar`, `ITelegramResumoService.EnviarFotoAsync` (Task 4); `Camera`, `OcorrenciaEpiCamera` (Task 1).
- Produces: `AnalisarQuadroCameraCommand(Guid CameraId, byte[] Imagem, DateTime CapturadaEmUtc) : IRequest<ResultadoQuadroDto>`; `ResultadoQuadroDto(string Situacao, Guid? OcorrenciaId, bool AlertaEnviado, int Pessoas, int SemCapacete, int SemColete)`; valores de `Situacao` em `SituacaoQuadro`: `"ignorado"`, `"limpo"`, `"ocorrencia"`, `"falha"`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class AnalisarQuadroCameraTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0x10, 0x20 };

    private sealed class AnalisadorFalso : IAnalisadorEpiImagem
    {
        private readonly Func<ResultadoAnaliseEpi> _resposta;
        public int Chamadas { get; private set; }
        public AnalisadorFalso(Func<ResultadoAnaliseEpi> resposta) => _resposta = resposta;
        public Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(_resposta());
        }
    }

    private sealed class TelegramFalso : ITelegramResumoService
    {
        public bool Resultado { get; set; } = true;
        public List<string> Legendas { get; } = new();
        public Task EnviarAsync(string mensagem, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarDocumentoAsync(byte[] arquivo, string nomeArquivo, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default)
        {
            Legendas.Add(legenda);
            return Task.FromResult(Resultado);
        }
    }

    private static async Task<(SstDbContext db, Camera camera)> Preparar(string nomeBanco, Action<Camera>? ajustar = null)
    {
        var db = CamerasPersistenciaTests.CriarDb(nomeBanco);
        var obra = new Obra { Codigo = "OB1", Nome = "Beira Rio" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        ajustar?.Invoke(camera);
        db.AddRange(obra, camera);
        await db.SaveChangesAsync();
        return (db, camera);
    }

    private static AnalisarQuadroCameraCommandHandler Criar(SstDbContext db, IAnalisadorEpiImagem analisador, ITelegramResumoService telegram)
        => new(db, analisador, telegram, NullLogger<AnalisarQuadroCameraCommandHandler>.Instance);

    private static ResultadoAnaliseEpi SemCapacete() => new(2, 1, 0, ConfiancaAnaliseEpi.Alta);
    private static ResultadoAnaliseEpi CenaRegular() => new(2, 0, 0, ConfiancaAnaliseEpi.Alta);

    [Fact]
    public async Task ComIrregularidade_GravaOcorrenciaEEnviaAlerta()
    {
        var (db, camera) = await Preparar(nameof(ComIrregularidade_GravaOcorrenciaEEnviaAlerta));
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(r.OcorrenciaId, ocorrencia.Id);
        Assert.Equal(camera.ObraId, ocorrencia.ObraId);
        Assert.Equal(Jpeg, ocorrencia.Foto);
        Assert.Equal(1, ocorrencia.SemCapacete);
        Assert.True(ocorrencia.AlertaEnviado);
        Assert.NotNull(ocorrencia.AlertaEnviadoEmUtc);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.Contains("Obra Beira Rio · Câmera Betoneira", legenda);
        Assert.Contains("sem capacete", legenda);
    }

    [Fact]
    public async Task CenaRegular_NaoGravaFotoNemEnviaAlerta()
    {
        var (db, camera) = await Preparar(nameof(CenaRegular_NaoGravaFotoNemEnviaAlerta));
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(CenaRegular), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Empty(telegram.Legendas);
        Assert.NotNull((await db.Cameras.SingleAsync()).UltimaAnaliseEmUtc);
    }

    [Fact]
    public async Task AnaliseRecente_IgnoraSemChamarAIa()
    {
        var (db, camera) = await Preparar(nameof(AnaliseRecente_IgnoraSemChamarAIa),
            c => c.UltimaAnaliseEmUtc = DateTime.UtcNow.AddSeconds(-5));
        var analisador = new AnalisadorFalso(SemCapacete);

        var r = await Criar(db, analisador, new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ignorado, r.Situacao);
        Assert.Equal(0, analisador.Chamadas);
    }

    [Fact]
    public async Task FalhaDaIa_DevolveFalhaSemOcorrenciaNemAlerta()
    {
        var (db, camera) = await Preparar(nameof(FalhaDaIa_DevolveFalhaSemOcorrenciaNemAlerta));
        var telegram = new TelegramFalso();
        var analisador = new AnalisadorFalso(() => throw new InvalidOperationException("IA fora do ar"));

        var r = await Criar(db, analisador, telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Falha, r.Situacao);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Empty(telegram.Legendas);
    }

    [Fact]
    public async Task TelegramRecusa_OcorrenciaFicaGravadaComAlertaNaoEnviado()
    {
        var (db, camera) = await Preparar(nameof(TelegramRecusa_OcorrenciaFicaGravadaComAlertaNaoEnviado));
        var telegram = new TelegramFalso { Resultado = false };

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.False(r.AlertaEnviado);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.False(ocorrencia.AlertaEnviado);
        Assert.Null((await db.Cameras.SingleAsync()).UltimoAlertaEmUtc);
    }

    [Fact]
    public async Task DentroDoIntervaloEntreAlertas_GravaOcorrenciaSemEnviar()
    {
        var (db, camera) = await Preparar(nameof(DentroDoIntervaloEntreAlertas_GravaOcorrenciaSemEnviar), c =>
        {
            c.IntervaloMinimoAlertasMinutos = 10;
            c.UltimoAlertaEmUtc = DateTime.UtcNow.AddMinutes(-2);
        });
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.False(r.AlertaEnviado);
        Assert.Single(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Empty(telegram.Legendas);
    }

    [Fact]
    public async Task SemLimiteDeIntervalo_EnviaMesmoComAlertaRecente()
    {
        var (db, camera) = await Preparar(nameof(SemLimiteDeIntervalo_EnviaMesmoComAlertaRecente), c =>
        {
            c.IntervaloMinimoAlertasMinutos = 0;
            c.UltimoAlertaEmUtc = DateTime.UtcNow.AddSeconds(-40);
        });
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.True(r.AlertaEnviado);
        Assert.Single(telegram.Legendas);
    }

    [Fact]
    public async Task CameraDesabilitada_LancaInvalidOperationException()
    {
        var (db, camera) = await Preparar(nameof(CameraDesabilitada_LancaInvalidOperationException), c => c.Habilitada = false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(db, new AnalisadorFalso(SemCapacete), new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default));
    }

    [Fact]
    public async Task CameraInexistente_LancaKeyNotFoundException()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(CameraInexistente_LancaKeyNotFoundException));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Criar(db, new AnalisadorFalso(SemCapacete), new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(Guid.NewGuid(), Jpeg, DateTime.UtcNow), default));
    }

    [Fact]
    public void Validador_ExigeJpegDentroDoLimite()
    {
        var validador = new AnalisarQuadroCameraCommandValidator();
        var id = Guid.NewGuid();

        Assert.True(validador.Validate(new AnalisarQuadroCameraCommand(id, Jpeg, DateTime.UtcNow)).IsValid);
        Assert.False(validador.Validate(new AnalisarQuadroCameraCommand(id, Array.Empty<byte>(), DateTime.UtcNow)).IsValid);
        Assert.False(validador.Validate(new AnalisarQuadroCameraCommand(id, new byte[] { 0x89, 0x50, 0x4E, 0x47 }, DateTime.UtcNow)).IsValid);
        var grande = new byte[4_000_001];
        grande[0] = 0xFF;
        grande[1] = 0xD8;
        Assert.False(validador.Validate(new AnalisarQuadroCameraCommand(id, grande, DateTime.UtcNow)).IsValid);
    }
}
```

- [ ] **Step 2: Rodar e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~AnalisarQuadroCamera"`
Expected: erro de compilação (`AnalisarQuadroCameraCommand` não existe).

- [ ] **Step 3: Implementar**

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Cameras.Commands;

public record AnalisarQuadroCameraCommand(Guid CameraId, byte[] Imagem, DateTime CapturadaEmUtc) : IRequest<ResultadoQuadroDto>;

public record ResultadoQuadroDto(string Situacao, Guid? OcorrenciaId, bool AlertaEnviado, int Pessoas, int SemCapacete, int SemColete);

public static class SituacaoQuadro
{
    public const string Ignorado = "ignorado";     // dentro do teto de frequência; nada foi analisado
    public const string Limpo = "limpo";           // analisado, sem irregularidade; foto descartada
    public const string Ocorrencia = "ocorrencia"; // irregularidade gravada
    public const string Falha = "falha";           // a IA não conseguiu analisar; nada gravado
}

public class AnalisarQuadroCameraCommandValidator : AbstractValidator<AnalisarQuadroCameraCommand>
{
    public const int TamanhoMaximoBytes = 4_000_000;

    public AnalisarQuadroCameraCommandValidator()
    {
        RuleFor(x => x.CameraId).NotEmpty();
        RuleFor(x => x.Imagem)
            .NotEmpty()
            .Must(i => i.Length <= TamanhoMaximoBytes).WithMessage("A imagem excede o tamanho máximo de 4 MB.")
            .Must(i => i.Length >= 2 && i[0] == 0xFF && i[1] == 0xD8).WithMessage("A imagem precisa estar em JPEG.");
    }
}

public class AnalisarQuadroCameraCommandHandler : IRequestHandler<AnalisarQuadroCameraCommand, ResultadoQuadroDto>
{
    // O navegador envia no máximo um quadro a cada 30 s; 25 s tolera variação do temporizador.
    public static readonly TimeSpan IntervaloMinimoEntreAnalises = TimeSpan.FromSeconds(25);

    private readonly IAppDbContext _db;
    private readonly IAnalisadorEpiImagem _analisador;
    private readonly ITelegramResumoService _telegram;
    private readonly ILogger<AnalisarQuadroCameraCommandHandler> _logger;

    public AnalisarQuadroCameraCommandHandler(
        IAppDbContext db,
        IAnalisadorEpiImagem analisador,
        ITelegramResumoService telegram,
        ILogger<AnalisarQuadroCameraCommandHandler> logger)
    {
        _db = db;
        _analisador = analisador;
        _telegram = telegram;
        _logger = logger;
    }

    public async Task<ResultadoQuadroDto> Handle(AnalisarQuadroCameraCommand request, CancellationToken ct)
    {
        var camera = await _db.Cameras.Include(c => c.Obra).FirstOrDefaultAsync(c => c.Id == request.CameraId, ct)
            ?? throw new KeyNotFoundException($"Câmera {request.CameraId} não encontrada.");

        if (!camera.Habilitada)
            throw new InvalidOperationException("Esta câmera está com o monitoramento desligado.");

        var agora = DateTime.UtcNow;
        if (camera.UltimaAnaliseEmUtc is { } ultima && agora - ultima < IntervaloMinimoEntreAnalises)
            return new ResultadoQuadroDto(SituacaoQuadro.Ignorado, null, false, 0, 0, 0);

        // Gravado antes de chamar a IA: um segundo envio em paralelo já encontra o teto aplicado.
        camera.UltimaAnaliseEmUtc = agora;
        await _db.SaveChangesAsync(ct);

        ResultadoAnaliseEpi analise;
        try
        {
            analise = await _analisador.AnalisarAsync(request.Imagem, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Análise de EPI da câmera {CameraId} não concluída.", camera.Id);
            return new ResultadoQuadroDto(SituacaoQuadro.Falha, null, false, 0, 0, 0);
        }

        if (analise.SemCapacete == 0 && analise.SemColete == 0)
            return new ResultadoQuadroDto(SituacaoQuadro.Limpo, null, false, analise.Pessoas, 0, 0);

        var ocorrencia = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id,
            ObraId = camera.ObraId,
            CapturadaEmUtc = request.CapturadaEmUtc,
            RecebidaEmUtc = agora,
            Foto = request.Imagem,
            PessoasNaCena = analise.Pessoas,
            SemCapacete = analise.SemCapacete,
            SemColete = analise.SemColete,
            Confianca = analise.Confianca,
        };
        _db.OcorrenciasEpiCamera.Add(ocorrencia);

        var podeAlertar = camera.IntervaloMinimoAlertasMinutos == 0
            || camera.UltimoAlertaEmUtc is null
            || agora - camera.UltimoAlertaEmUtc.Value >= TimeSpan.FromMinutes(camera.IntervaloMinimoAlertasMinutos);

        if (podeAlertar && await EnviarAlertaAsync(camera, ocorrencia, ct))
        {
            ocorrencia.AlertaEnviado = true;
            ocorrencia.AlertaEnviadoEmUtc = agora;
            camera.UltimoAlertaEmUtc = agora;
        }

        await _db.SaveChangesAsync(ct);
        return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, ocorrencia.Id, ocorrencia.AlertaEnviado,
            analise.Pessoas, analise.SemCapacete, analise.SemColete);
    }

    // Falha no Telegram não pode impedir o registro: a ocorrência fica gravada com "alerta não enviado".
    private async Task<bool> EnviarAlertaAsync(Camera camera, OcorrenciaEpiCamera ocorrencia, CancellationToken ct)
    {
        try
        {
            var legenda = LegendaAlertaEpi.Montar(camera.Obra?.Nome ?? "-", camera.Nome, ocorrencia.RecebidaEmUtc,
                ocorrencia.PessoasNaCena, ocorrencia.SemCapacete, ocorrencia.SemColete);
            return await _telegram.EnviarFotoAsync(ocorrencia.Foto, legenda, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Alerta de EPI da câmera {CameraId} não enviado ao Telegram.", camera.Id);
            return false;
        }
    }
}
```

- [ ] **Step 4: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~Cameras"`
Expected: PASS (21 testes).

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Cameras tests/AAHBRANT.SST.Application.Tests/Cameras
git commit -m "feat(cameras): análise de quadro com registro de ocorrência e alerta no Telegram"
```

---

### Task 6: Ocorrências — listar, foto, vincular trabalhador e situação

**Files:**
- Create: `src/AAHBRANT.SST.Application/Cameras/Queries/ListarOcorrenciasEpiQuery.cs`
- Create: `src/AAHBRANT.SST.Application/Cameras/Queries/ObterFotoOcorrenciaEpiQuery.cs`
- Create: `src/AAHBRANT.SST.Application/Cameras/Commands/VincularTrabalhadorOcorrenciaEpiCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Cameras/Commands/DefinirSituacaoOcorrenciaEpiCommand.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Cameras/OcorrenciasEpiTests.cs`

**Interfaces:**
- Consumes: `OcorrenciaEpiCamera`, `Camera`, `IAppDbContext` (Task 1).
- Produces: `ListarOcorrenciasEpiQuery(Guid? ObraId, Guid? CameraId, DateTime? DeUtc, DateTime? AteUtc, SituacaoOcorrenciaEpi? Situacao) : IRequest<List<OcorrenciaEpiDto>>` (até 200 itens, mais recentes primeiro, sem a foto); `OcorrenciaEpiDto(Guid Id, Guid CameraId, string CameraNome, Guid ObraId, string ObraNome, DateTime CapturadaEmUtc, DateTime RecebidaEmUtc, int PessoasNaCena, int SemCapacete, int SemColete, ConfiancaAnaliseEpi Confianca, bool AlertaEnviado, Guid? TrabalhadorId, string? TrabalhadorNome, SituacaoOcorrenciaEpi Situacao)`; `ObterFotoOcorrenciaEpiQuery(Guid Id) : IRequest<byte[]>`; `VincularTrabalhadorOcorrenciaEpiCommand(Guid Id, Guid? TrabalhadorId, string? AzureAdObjectId) : IRequest`; `DefinirSituacaoOcorrenciaEpiCommand(Guid Id, SituacaoOcorrenciaEpi Situacao) : IRequest`.

Antes de escrever, confirmar o nome da propriedade de nome do trabalhador: `grep -n "public string Nome" src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs`. O código abaixo usa `Trabalhador.Nome`; se a propriedade tiver outro nome (por exemplo `NomeCompleto`), usar o nome real nos dois arquivos (consulta e teste).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class OcorrenciasEpiTests
{
    private const string Oid = "oid-tecnico-cameras";

    private static async Task<(SstDbContext db, Obra obra, Camera camera, OcorrenciaEpiCamera recente, OcorrenciaEpiCamera antiga)> Preparar(string nomeBanco)
    {
        var db = CamerasPersistenciaTests.CriarDb(nomeBanco);
        var obra = new Obra { Codigo = "OB1", Nome = "Beira Rio" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        var recente = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x01 },
            RecebidaEmUtc = DateTime.UtcNow, CapturadaEmUtc = DateTime.UtcNow, PessoasNaCena = 2, SemCapacete = 1,
        };
        var antiga = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x02 },
            RecebidaEmUtc = DateTime.UtcNow.AddDays(-5), CapturadaEmUtc = DateTime.UtcNow.AddDays(-5), PessoasNaCena = 1, SemColete = 1,
            Situacao = SituacaoOcorrenciaEpi.FalsoAlerta,
        };
        db.AddRange(obra, camera, recente, antiga,
            new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico", AzureAdObjectId = Oid });
        await db.SaveChangesAsync();
        return (db, obra, camera, recente, antiga);
    }

    [Fact]
    public async Task Listar_OrdenaDaMaisRecenteEAplicaFiltros()
    {
        var (db, obra, camera, recente, antiga) = await Preparar(nameof(Listar_OrdenaDaMaisRecenteEAplicaFiltros));
        var handler = new ListarOcorrenciasEpiQueryHandler(db);

        var todas = await handler.Handle(new ListarOcorrenciasEpiQuery(obra.Id, camera.Id, null, null, null), default);
        Assert.Equal(new[] { recente.Id, antiga.Id }, todas.Select(o => o.Id).ToArray());
        Assert.Equal("Betoneira", todas[0].CameraNome);
        Assert.Equal("Beira Rio", todas[0].ObraNome);

        var doPeriodo = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, DateTime.UtcNow.AddDays(-1), null, null), default);
        Assert.Equal(recente.Id, Assert.Single(doPeriodo).Id);

        var falsos = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, SituacaoOcorrenciaEpi.FalsoAlerta), default);
        Assert.Equal(antiga.Id, Assert.Single(falsos).Id);
    }

    [Fact]
    public async Task ObterFoto_DevolveOsBytesGravados()
    {
        var (db, _, _, recente, _) = await Preparar(nameof(ObterFoto_DevolveOsBytesGravados));

        var foto = await new ObterFotoOcorrenciaEpiQueryHandler(db).Handle(new ObterFotoOcorrenciaEpiQuery(recente.Id), default);

        Assert.Equal(new byte[] { 0xFF, 0xD8, 0x01 }, foto);
    }

    [Fact]
    public async Task ObterFoto_Inexistente_LancaKeyNotFoundException()
    {
        var (db, _, _, _, _) = await Preparar(nameof(ObterFoto_Inexistente_LancaKeyNotFoundException));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ObterFotoOcorrenciaEpiQueryHandler(db).Handle(new ObterFotoOcorrenciaEpiQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Vincular_GravaTrabalhadorQuemVinculouEMarcaConferida()
    {
        var (db, obra, _, recente, _) = await Preparar(nameof(Vincular_GravaTrabalhadorQuemVinculouEMarcaConferida));
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "João da Silva" };
        db.Add(trabalhador);
        await db.SaveChangesAsync();

        await new VincularTrabalhadorOcorrenciaEpiCommandHandler(db).Handle(
            new VincularTrabalhadorOcorrenciaEpiCommand(recente.Id, trabalhador.Id, Oid), default);

        var lida = await db.OcorrenciasEpiCamera.SingleAsync(o => o.Id == recente.Id);
        Assert.Equal(trabalhador.Id, lida.TrabalhadorId);
        Assert.NotNull(lida.VinculadoEmUtc);
        Assert.Equal((await db.Usuarios.SingleAsync()).Id, lida.VinculadoPorUsuarioId);
        Assert.Equal(SituacaoOcorrenciaEpi.Conferida, lida.Situacao);
    }

    [Fact]
    public async Task Vincular_ComTrabalhadorNulo_RemoveOVinculo()
    {
        var (db, obra, _, recente, _) = await Preparar(nameof(Vincular_ComTrabalhadorNulo_RemoveOVinculo));
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "João da Silva" };
        db.Add(trabalhador);
        recente.TrabalhadorId = trabalhador.Id;
        recente.VinculadoEmUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await new VincularTrabalhadorOcorrenciaEpiCommandHandler(db).Handle(
            new VincularTrabalhadorOcorrenciaEpiCommand(recente.Id, null, Oid), default);

        var lida = await db.OcorrenciasEpiCamera.SingleAsync(o => o.Id == recente.Id);
        Assert.Null(lida.TrabalhadorId);
        Assert.Null(lida.VinculadoEmUtc);
        Assert.Null(lida.VinculadoPorUsuarioId);
    }

    [Fact]
    public async Task Vincular_TrabalhadorInexistente_LancaKeyNotFoundException()
    {
        var (db, _, _, recente, _) = await Preparar(nameof(Vincular_TrabalhadorInexistente_LancaKeyNotFoundException));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new VincularTrabalhadorOcorrenciaEpiCommandHandler(db).Handle(
            new VincularTrabalhadorOcorrenciaEpiCommand(recente.Id, Guid.NewGuid(), Oid), default));
    }

    [Fact]
    public async Task DefinirSituacao_MarcaFalsoAlerta()
    {
        var (db, _, _, recente, _) = await Preparar(nameof(DefinirSituacao_MarcaFalsoAlerta));

        await new DefinirSituacaoOcorrenciaEpiCommandHandler(db).Handle(
            new DefinirSituacaoOcorrenciaEpiCommand(recente.Id, SituacaoOcorrenciaEpi.FalsoAlerta), default);

        Assert.Equal(SituacaoOcorrenciaEpi.FalsoAlerta, (await db.OcorrenciasEpiCamera.SingleAsync(o => o.Id == recente.Id)).Situacao);
    }
}
```

- [ ] **Step 2: Rodar e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~OcorrenciasEpi"`
Expected: erro de compilação (`ListarOcorrenciasEpiQuery` não existe).

- [ ] **Step 3: Implementar**

`ListarOcorrenciasEpiQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

public record ListarOcorrenciasEpiQuery(
    Guid? ObraId,
    Guid? CameraId,
    DateTime? DeUtc,
    DateTime? AteUtc,
    SituacaoOcorrenciaEpi? Situacao) : IRequest<List<OcorrenciaEpiDto>>;

// Sem a foto: a lista não pode carregar os bytes de até 200 imagens. A foto vem por
// ObterFotoOcorrenciaEpiQuery, uma por vez.
public record OcorrenciaEpiDto(
    Guid Id,
    Guid CameraId,
    string CameraNome,
    Guid ObraId,
    string ObraNome,
    DateTime CapturadaEmUtc,
    DateTime RecebidaEmUtc,
    int PessoasNaCena,
    int SemCapacete,
    int SemColete,
    ConfiancaAnaliseEpi Confianca,
    bool AlertaEnviado,
    Guid? TrabalhadorId,
    string? TrabalhadorNome,
    SituacaoOcorrenciaEpi Situacao);

public class ListarOcorrenciasEpiQueryHandler : IRequestHandler<ListarOcorrenciasEpiQuery, List<OcorrenciaEpiDto>>
{
    public const int LimiteItens = 200;

    private readonly IAppDbContext _db;
    public ListarOcorrenciasEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<OcorrenciaEpiDto>> Handle(ListarOcorrenciasEpiQuery request, CancellationToken ct)
    {
        var query = _db.OcorrenciasEpiCamera.AsNoTracking();
        if (request.ObraId is { } obraId) query = query.Where(o => o.ObraId == obraId);
        if (request.CameraId is { } cameraId) query = query.Where(o => o.CameraId == cameraId);
        if (request.DeUtc is { } de) query = query.Where(o => o.RecebidaEmUtc >= de);
        if (request.AteUtc is { } ate) query = query.Where(o => o.RecebidaEmUtc <= ate);
        if (request.Situacao is { } situacao) query = query.Where(o => o.Situacao == situacao);

        // LEFT JOIN explícito com câmera e trabalhador (IgnoreQueryFilters): a ocorrência precisa
        // continuar na lista mesmo se a câmera foi excluída ou o trabalhador foi desligado. O escopo
        // por obra já está garantido pelo filtro da própria ocorrência (ObraId).
        return await query
            .OrderByDescending(o => o.RecebidaEmUtc)
            .Take(LimiteItens)
            .Select(o => new OcorrenciaEpiDto(
                o.Id,
                o.CameraId,
                _db.Cameras.IgnoreQueryFilters().Where(c => c.Id == o.CameraId).Select(c => c.Nome).FirstOrDefault() ?? "-",
                o.ObraId,
                _db.Obras.IgnoreQueryFilters().Where(ob => ob.Id == o.ObraId).Select(ob => ob.Nome).FirstOrDefault() ?? "-",
                o.CapturadaEmUtc,
                o.RecebidaEmUtc,
                o.PessoasNaCena,
                o.SemCapacete,
                o.SemColete,
                o.Confianca,
                o.AlertaEnviado,
                o.TrabalhadorId,
                _db.Trabalhadores.IgnoreQueryFilters().Where(t => t.Id == o.TrabalhadorId).Select(t => t.Nome).FirstOrDefault(),
                o.Situacao))
            .ToListAsync(ct);
    }
}
```

`ObterFotoOcorrenciaEpiQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

public record ObterFotoOcorrenciaEpiQuery(Guid Id) : IRequest<byte[]>;

public class ObterFotoOcorrenciaEpiQueryHandler : IRequestHandler<ObterFotoOcorrenciaEpiQuery, byte[]>
{
    private readonly IAppDbContext _db;
    public ObterFotoOcorrenciaEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<byte[]> Handle(ObterFotoOcorrenciaEpiQuery request, CancellationToken ct)
        => await _db.OcorrenciasEpiCamera.AsNoTracking()
               .Where(o => o.Id == request.Id)
               .Select(o => o.Foto)
               .FirstOrDefaultAsync(ct)
           ?? throw new KeyNotFoundException($"Ocorrência {request.Id} não encontrada.");
}
```

`VincularTrabalhadorOcorrenciaEpiCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Vínculo manual do trabalhador pelo técnico. TrabalhadorId nulo remove o vínculo.
public record VincularTrabalhadorOcorrenciaEpiCommand(Guid Id, Guid? TrabalhadorId, string? AzureAdObjectId) : IRequest;

public class VincularTrabalhadorOcorrenciaEpiCommandHandler : IRequestHandler<VincularTrabalhadorOcorrenciaEpiCommand>
{
    private readonly IAppDbContext _db;
    public VincularTrabalhadorOcorrenciaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(VincularTrabalhadorOcorrenciaEpiCommand request, CancellationToken ct)
    {
        var ocorrencia = await _db.OcorrenciasEpiCamera.FirstOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ocorrência {request.Id} não encontrada.");

        if (request.TrabalhadorId is not { } trabalhadorId)
        {
            ocorrencia.TrabalhadorId = null;
            ocorrencia.VinculadoPorUsuarioId = null;
            ocorrencia.VinculadoEmUtc = null;
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (!await _db.Trabalhadores.AnyAsync(t => t.Id == trabalhadorId, ct))
            throw new KeyNotFoundException("Trabalhador não encontrado.");

        ocorrencia.TrabalhadorId = trabalhadorId;
        ocorrencia.VinculadoEmUtc = DateTime.UtcNow;
        ocorrencia.VinculadoPorUsuarioId = string.IsNullOrWhiteSpace(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.Where(u => u.AzureAdObjectId == request.AzureAdObjectId).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (ocorrencia.Situacao == SituacaoOcorrenciaEpi.Nova)
            ocorrencia.Situacao = SituacaoOcorrenciaEpi.Conferida;

        await _db.SaveChangesAsync(ct);
    }
}
```

`DefinirSituacaoOcorrenciaEpiCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Usado para marcar "falso alerta" (a IA errou) ou voltar a ocorrência para nova/conferida.
public record DefinirSituacaoOcorrenciaEpiCommand(Guid Id, SituacaoOcorrenciaEpi Situacao) : IRequest;

public class DefinirSituacaoOcorrenciaEpiCommandValidator : AbstractValidator<DefinirSituacaoOcorrenciaEpiCommand>
{
    public DefinirSituacaoOcorrenciaEpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Situacao).IsInEnum();
    }
}

public class DefinirSituacaoOcorrenciaEpiCommandHandler : IRequestHandler<DefinirSituacaoOcorrenciaEpiCommand>
{
    private readonly IAppDbContext _db;
    public DefinirSituacaoOcorrenciaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirSituacaoOcorrenciaEpiCommand request, CancellationToken ct)
    {
        var ocorrencia = await _db.OcorrenciasEpiCamera.FirstOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ocorrência {request.Id} não encontrada.");

        ocorrencia.Situacao = request.Situacao;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar os testes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~Cameras"`
Expected: PASS (28 testes).

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Cameras tests/AAHBRANT.SST.Application.Tests/Cameras
git commit -m "feat(cameras): lista de ocorrências de EPI, foto, vínculo de trabalhador e falso alerta"
```

---

### Task 7: Controller e verificação final

**Files:**
- Create: `src/AAHBRANT.SST.Api/Controllers/CamerasController.cs`

**Interfaces:**
- Consumes: todos os comandos e consultas das Tasks 2, 5 e 6.
- Produces: os endpoints da seção "Contrato da API" abaixo.

- [ ] **Step 1: Escrever o controller**

```csharp
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

// Câmeras de monitoramento de EPI (capacete e colete). Reaproveita as permissões de inspeção
// (inspecao:ver/criar), mesma decisão do VeiculosController. Exclusão só do Administrador.
[ApiController]
[Route("api/[controller]")]
public class CamerasController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public CamerasController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCamerasQuery(obraId), ct));

    [Authorize(Policy = "inspecao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCameraCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { obraId = command.ObraId }, new { id });
    }

    [Authorize(Policy = "inspecao:criar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCameraCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("O id da rota difere do id do corpo.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCameraCommand(id), ct);
        return NoContent();
    }

    // Recebe um quadro JPEG da tela Monitorar. Sempre responde 200 com a situação
    // (ignorado/limpo/ocorrencia/falha) para o navegador seguir monitorando sem tratar erro.
    [Authorize(Policy = "inspecao:criar")]
    [HttpPost("{id:guid}/quadros")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnalisarQuadro(Guid id, [FromForm] QuadroCameraForm form, CancellationToken ct)
    {
        if (form.Foto is null || form.Foto.Length == 0) return BadRequest("Envie a foto do quadro.");

        using var memoria = new MemoryStream();
        await form.Foto.CopyToAsync(memoria, ct);
        var capturadaEmUtc = (form.CapturadaEm ?? DateTimeOffset.UtcNow).UtcDateTime;

        return Ok(await _mediator.Send(new AnalisarQuadroCameraCommand(id, memoria.ToArray(), capturadaEmUtc), ct));
    }

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet("ocorrencias")]
    public async Task<IActionResult> ListarOcorrencias(
        [FromQuery] Guid? obraId,
        [FromQuery] Guid? cameraId,
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        [FromQuery] SituacaoOcorrenciaEpi? situacao,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarOcorrenciasEpiQuery(obraId, cameraId, de?.UtcDateTime, ate?.UtcDateTime, situacao), ct));

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet("ocorrencias/{id:guid}/foto")]
    public async Task<IActionResult> ObterFoto(Guid id, CancellationToken ct)
        => File(await _mediator.Send(new ObterFotoOcorrenciaEpiQuery(id), ct), "image/jpeg");

    [Authorize(Policy = "inspecao:criar")]
    [HttpPut("ocorrencias/{id:guid}/trabalhador")]
    public async Task<IActionResult> VincularTrabalhador(Guid id, VincularTrabalhadorRequest corpo, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(azureAdObjectId) && _environment.IsDevelopment())
            azureAdObjectId = "dev-local-user";

        await _mediator.Send(new VincularTrabalhadorOcorrenciaEpiCommand(id, corpo.TrabalhadorId, azureAdObjectId), ct);
        return NoContent();
    }

    [Authorize(Policy = "inspecao:criar")]
    [HttpPut("ocorrencias/{id:guid}/situacao")]
    public async Task<IActionResult> DefinirSituacao(Guid id, DefinirSituacaoRequest corpo, CancellationToken ct)
    {
        await _mediator.Send(new DefinirSituacaoOcorrenciaEpiCommand(id, corpo.Situacao), ct);
        return NoContent();
    }
}

public class QuadroCameraForm
{
    public IFormFile? Foto { get; set; }
    public DateTimeOffset? CapturadaEm { get; set; }
}

public record VincularTrabalhadorRequest(Guid? TrabalhadorId);

public record DefinirSituacaoRequest(SituacaoOcorrenciaEpi Situacao);
```

- [ ] **Step 2: Compilar e rodar todos os testes do módulo**

Run: `dotnet build SST-APP.sln`
Expected: build sem erros.

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter "FullyQualifiedName~Cameras"`
Expected: PASS (28 testes).

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests --filter "FullyQualifiedName~Cameras"`
Expected: PASS (4 testes).

- [ ] **Step 3: Rodar a suíte completa de backend**

Run: `dotnet test SST-APP.sln`
Expected: PASS, sem regressão em outros módulos. Se algum teste fora de Câmeras falhar, confirmar com `git stash && dotnet test SST-APP.sln --filter "<nome do teste>"; git stash pop` se já falhava antes; se já falhava, apenas reportar.

- [ ] **Step 4: Verificação manual da API local (sem IA real)**

Pré-requisito: Docker aberto com o SQL Server local (ver memória do projeto); **não resetar o banco**. Aplicar a migration no banco local só com autorização do usuário. Com a API local rodando e uma obra existente (trocar `<obraId>`):

```bash
curl -s -X POST http://localhost:5000/api/cameras -H "Content-Type: application/json" -d '{"obraId":"<obraId>","nome":"Tablet teste","areaSstId":null,"tipo":1,"habilitada":true,"intervaloMinimoAlertasMinutos":0}'
```

Expected: `201` com `{ "id": "..." }`. Depois `curl -s "http://localhost:5000/api/cameras?obraId=<obraId>"` devolve a câmera com `ocorrenciasHoje: 0`. Usar a porta real da API local (conferir em `.claude/launch.json`).

O teste com foto real e Claude real (`POST /api/cameras/{id}/quadros`) fica para o piloto no tablet, depois do plano de frontend: consome crédito da API e precisa de `Claude:ApiKey` e `Telegram:BotToken` configurados.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Api/Controllers/CamerasController.cs
git commit -m "feat(cameras): endpoints de câmeras, análise de quadro e ocorrências de EPI"
```

Não abrir PR nem fazer push sem o usuário pedir.

---

## Contrato da API (entrada para o plano de frontend)

| Método e rota | Permissão | Corpo / parâmetros | Resposta |
| --- | --- | --- | --- |
| `GET /api/cameras?obraId=` | `inspecao:ver` | — | `CameraDto[]` |
| `POST /api/cameras` | `inspecao:criar` | `{ obraId, nome, areaSstId, tipo, habilitada, intervaloMinimoAlertasMinutos }` | `201 { id }` |
| `PUT /api/cameras/{id}` | `inspecao:criar` | `{ id, nome, areaSstId, tipo, habilitada, intervaloMinimoAlertasMinutos }` | `204` |
| `DELETE /api/cameras/{id}` | Administrador | — | `204` |
| `POST /api/cameras/{id}/quadros` | `inspecao:criar` | multipart: `foto` (JPEG ≤ 4 MB), `capturadaEm` (ISO 8601) | `{ situacao, ocorrenciaId, alertaEnviado, pessoas, semCapacete, semColete }` |
| `GET /api/cameras/ocorrencias?obraId=&cameraId=&de=&ate=&situacao=` | `inspecao:ver` | — | `OcorrenciaEpiDto[]` (até 200) |
| `GET /api/cameras/ocorrencias/{id}/foto` | `inspecao:ver` | — | `image/jpeg` |
| `PUT /api/cameras/ocorrencias/{id}/trabalhador` | `inspecao:criar` | `{ trabalhadorId }` (nulo remove) | `204` |
| `PUT /api/cameras/ocorrencias/{id}/situacao` | `inspecao:criar` | `{ situacao }` (1 nova, 2 conferida, 3 falso alerta) | `204` |

`situacao` do quadro: `ignorado` (teto de 25 s), `limpo`, `ocorrencia`, `falha`. `tipo`: 1 canteiro, 2 portaria. `confianca`: 1 baixa, 2 média, 3 alta.

## Cobertura da spec

| Spec | Onde |
| --- | --- |
| §2 fluxo passos 4–6, §7 erros | Task 5 |
| §2 passos 1–3 (tela, movimento) | plano de frontend |
| §3 cadastro de câmeras | Task 2 |
| §3 analisador de EPI | Task 3 |
| §3 notificador, §6 mensagem | Task 4 |
| §3 registro de ocorrências, §4 dados | Tasks 1 e 6 |
| §5 telas | plano de frontend (após mockup) |
| §7 escopo por obra, exclusão só Admin, teto no servidor, validação de imagem | Tasks 1, 5 e 7 |
| §7 "recurso indisponível" na tela | plano de frontend (a API devolve `falha` sem IA configurada) |
| §10 testes | Tasks 1–6; detector de movimento e piloto no plano de frontend |
