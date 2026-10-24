# Visualizador de Documento PDF em PGR e PCMSO Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ao entrar no detalhe de um PGR ou de um PCMSO, o PDF já cadastrado aparece renderizado inteiro, com rolagem por todas as páginas, numa aba própria ("PGR"/"PCMSO") que é a aba padrão — sem nenhum botão de "abrir em outro lugar".

**Architecture:** Cada documento é gravado como binário direto na própria linha do `Pgr`/`PcmsoDetalhe` (`DocumentoConteudo byte[]` + `DocumentoContentType string`), seguindo o mesmo padrão já usado em todo o projeto para anexos (`Trabalhador.FotoConteudo`, `TreinamentoCipa.CertificadoConteudo` etc.) — **não** a entidade genérica `Evidencia`, que nunca chegou a ser implementada em nenhum módulo real do sistema. Endpoints REST simples (`POST`/`GET .../documento`) fazem upload/download; o frontend busca o PDF autenticado, converte para Blob URL e mostra num `<iframe>` (rolagem/zoom nativos do navegador).

**Tech Stack:** ASP.NET Core + MediatR (CQRS) + EF Core (SQL Server/InMemory) no backend; React + Fluent UI + `fetch` no frontend (TeamsApp).

**Spec:** [docs/superpowers/specs/2026-09-09-visualizador-documento-pgr-pcmso-design.md](../specs/2026-09-09-visualizador-documento-pgr-pcmso-design.md)

**Notas importantes em relação à spec:**
1. A spec original propunha reaproveitar a entidade genérica `Evidencia`. Ao levantar o código real para este plano, descobri que `Evidencia` nunca foi implementada por nenhum módulo (comentário em `Inspecao.cs:79` confirma isso) — todo módulo que precisa guardar um binário usa um par de colunas direto na própria entidade (`FotoConteudo`/`FotoContentType`, `CertificadoConteudo`/`CertificadoContentType` etc.). Este plano segue essa convenção real do projeto em vez da que constava na spec. Nenhuma decisão visível ao usuário muda (nomes de aba, comportamento do viewer, limite de 20 MB, armazenamento no próprio banco) — é só o desenho interno da tabela.
2. A spec mencionava esconder o botão de substituir documento para quem não tem permissão de edição (`podeEditar`). Nenhuma outra tela do sistema hoje esconde botões de ação no cliente por permissão (ex.: o upload de certificado de treinamento do CIPA sempre mostra o botão, e o backend rejeita com 403 se o usuário não tiver a policy) — este plano segue essa mesma convenção: o botão "Anexar/Substituir documento" fica sempre visível, e uma tentativa sem permissão aparece como mensagem de erro (403) na aba, igual a qualquer outra ação do sistema.

## Global Constraints

- Limite de upload do PDF: 20 MB (`20 * 1024 * 1024` bytes), validado no cliente e no servidor.
- Tipo aceito: somente `application/pdf` (validado por Content-Type declarado E pela assinatura de bytes do arquivo, via `ValidadorAssinaturaArquivo.AssinaturaConfere`, já existente em `AAHBRANT.SST.Application.Common`).
- Rótulo da aba: **"PGR"** (não "Documento") na tela de detalhe do PGR; **"PCMSO"** (não "Documento") na tela de detalhe do PCMSO. Ambas são a aba padrão (primeira a abrir).
- Nenhuma migration é aplicada ao banco de hml/produção como parte deste plano — só localmente, para desenvolvimento/teste. Migration em hml/produção segue a regra já registrada: só com autorização explícita do usuário.
- Autorização: reaproveitar as policies RBAC já existentes (`pgr:ver`, `pgr:editar`, `pcmso:ver`, `pcmso:editar`) — nenhuma policy nova.

---

### Task 1: Domínio — campos de documento em Pgr/PcmsoDetalhe + remoção do campo morto `Arquivo`

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Pgr/Pgr.cs`
- Modify: `src/AAHBRANT.SST.Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/SaudeOcupacionalConfiguracoes.cs`
- Modify: `src/AAHBRANT.SST.Application/Pcmsos/PcmsoDto.cs`
- Modify: `src/AAHBRANT.SST.Application/Pcmsos/Commands/CriarPcmsoCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Pcmsos/Commands/AtualizarPcmsoCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Pcmsos/Queries/ObterPcmsoPorIdQuery.cs`
- Modify: `src/AAHBRANT.SST.Application/Pcmsos/Queries/ListarPcmsosQuery.cs`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/saude-ocupacional/PcmsoTab.tsx`
- Create: migration gerada por `dotnet ef migrations add`

**Interfaces:**
- Produces: `Pgr.DocumentoConteudo` (`byte[]?`), `Pgr.DocumentoContentType` (`string?`); `PcmsoDetalhe.DocumentoConteudo` (`byte[]?`), `PcmsoDetalhe.DocumentoContentType` (`string?`) — consumidos pelos handlers das Tasks 2, 3, 5, 6.

- [ ] **Step 1: Adicionar os campos de documento em `Pgr` (`src/AAHBRANT.SST.Domain/Entidades/Pgr/Pgr.cs`), logo após `public ICollection<PgrRevisao> Revisoes { get; set; } = new List<PgrRevisao>();` e antes do `}` de fechamento da classe**

```csharp
    // Documento PDF original do PGR (consulta na aba "PGR" da tela de detalhe) — binário direto na
    // linha, mesmo padrão já usado por Trabalhador.FotoConteudo/FotoContentType.
    public byte[]? DocumentoConteudo { get; set; }
    public string? DocumentoContentType { get; set; }
```

- [ ] **Step 2: Em `src/AAHBRANT.SST.Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs`, na classe `PcmsoDetalhe`, substituir a linha `public string? Arquivo { get; set; }` por**

```csharp
    // Documento PDF original do PCMSO (consulta na aba "PCMSO" da tela de detalhe) — binário direto
    // na linha, mesmo padrão já usado por Trabalhador.FotoConteudo/FotoContentType. Substitui o campo
    // Arquivo (string), que nunca chegou a ser ligado a nenhuma tela/upload real.
    public byte[]? DocumentoConteudo { get; set; }
    public string? DocumentoContentType { get; set; }
```

- [ ] **Step 3: Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/SaudeOcupacionalConfiguracoes.cs`, remover a linha**

```csharp
        builder.Property(p => p.Arquivo).HasMaxLength(500);
```

- [ ] **Step 4: Em `src/AAHBRANT.SST.Application/Pcmsos/PcmsoDto.cs`, remover a linha `public string? Arquivo { get; set; }`**

- [ ] **Step 5: Em `src/AAHBRANT.SST.Application/Pcmsos/Commands/CriarPcmsoCommand.cs`, remover `string? Arquivo,` do record, `RuleFor(x => x.Arquivo).MaximumLength(500);` do validator, e `Arquivo = request.Arquivo,` do handler**

- [ ] **Step 6: Em `src/AAHBRANT.SST.Application/Pcmsos/Commands/AtualizarPcmsoCommand.cs`, remover `string? Arquivo,` do record, `RuleFor(x => x.Arquivo).MaximumLength(500);` do validator, e `pcmso.Arquivo = request.Arquivo;` do handler**

- [ ] **Step 7: Em `src/AAHBRANT.SST.Application/Pcmsos/Queries/ObterPcmsoPorIdQuery.cs` e `ListarPcmsosQuery.cs`, remover a linha `Arquivo = p.Arquivo,` de cada um**

- [ ] **Step 8: Em `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`, remover `arquivo?: string | null;` da interface `Pcmso` (linha com `arquivo`) e da interface `NovoPcmso`**

- [ ] **Step 9: Em `src/AAHBRANT.SST.TeamsApp/src/pages/saude-ocupacional/PcmsoTab.tsx`, remover a linha `arquivo: '',` da função `pcmsoVazio()`**

- [ ] **Step 10: Confirmar que o backend compila**

Run: `dotnet build src/AAHBRANT.SST.Api/AAHBRANT.SST.Api.csproj`
Expected: `Build succeeded.`

- [ ] **Step 11: Confirmar que o frontend compila**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: sem erros de TypeScript.

- [ ] **Step 12: Rodar a suíte de testes existente, para confirmar que a remoção do campo `Arquivo` não quebrou nada**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj`
Expected: `Passed!` (nenhum teste referenciava `Arquivo`, então nenhuma falha é esperada).

- [ ] **Step 13: Gerar a migration**

Run: `dotnet ef migrations add AdicionarDocumentoPgrEPcmso --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`
Expected: `Done.` e um novo arquivo `<timestamp>_AdicionarDocumentoPgrEPcmso.cs` em `Persistencia/Migrations/`, adicionando as colunas `DocumentoConteudo`/`DocumentoContentType` em `Pgrs` e `PcmsoDetalhes`, e removendo a coluna `Arquivo` de `PcmsoDetalhes`.

- [ ] **Step 14 (checkpoint — não pular): pedir confirmação do usuário antes de aplicar a migration no banco local**

Aplicar a migration (`dotnet ef database update ...`) é uma alteração de schema (inclui um `DROP COLUMN`). Antes de rodar, mostrar ao usuário a migration gerada e esperar confirmação — mesma regra já registrada do projeto (nunca mexer no banco sem avisar antes), mesmo sendo uma coluna sempre vazia.

- [ ] **Step 15: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Pgr/Pgr.cs src/AAHBRANT.SST.Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/SaudeOcupacionalConfiguracoes.cs src/AAHBRANT.SST.Application/Pcmsos src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations src/AAHBRANT.SST.TeamsApp/src/lib/api.ts src/AAHBRANT.SST.TeamsApp/src/pages/saude-ocupacional/PcmsoTab.tsx
git commit -m "feat(pgr,pcmso): campos de documento PDF; remove campo Arquivo morto"
```

---

### Task 2: PGR — comando `AnexarDocumentoPgrCommand`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Pgrs/Commands/AnexarDocumentoPgrCommand.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Pgrs/AnexarDocumentoPgrCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Pgr.DocumentoConteudo`/`DocumentoContentType` (Task 1); `IAppDbContext.Pgrs : DbSet<Pgr>` (existente).
- Produces: `AnexarDocumentoPgrCommand(Guid PgrId, byte[] Conteudo, string ContentType) : IRequest` — consumido pelo controller na Task 4.

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Pgrs/AnexarDocumentoPgrCommandHandlerTests.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Pgrs;

public class AnexarDocumentoPgrCommandHandlerTests
{
    private static readonly byte[] PdfMinimo = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_PgrExistente_GravaConteudoEContentType()
    {
        var db = CriarDb(nameof(Handle_PgrExistente_GravaConteudoEContentType));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var pgr = new Pgr { ObraId = obra.Id, Obra = obra, Nome = "PGR Teste", DataElaboracao = DateTime.UtcNow };
        db.Obras.Add(obra);
        db.Pgrs.Add(pgr);
        await db.SaveChangesAsync(default);

        var handler = new AnexarDocumentoPgrCommandHandler(db);
        await handler.Handle(new AnexarDocumentoPgrCommand(pgr.Id, PdfMinimo, "application/pdf"), default);

        var atualizado = await db.Pgrs.FirstAsync(p => p.Id == pgr.Id);
        Assert.Equal(PdfMinimo, atualizado.DocumentoConteudo);
        Assert.Equal("application/pdf", atualizado.DocumentoContentType);
    }

    [Fact]
    public async Task Handle_PgrInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_PgrInexistente_LancaKeyNotFoundException));
        var handler = new AnexarDocumentoPgrCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AnexarDocumentoPgrCommand(Guid.NewGuid(), PdfMinimo, "application/pdf"), default));
    }
}
```

- [ ] **Step 2: Rodar o teste e confirmar que falha (a classe ainda não existe)**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter AnexarDocumentoPgrCommandHandlerTests`
Expected: erro de compilação (`AnexarDocumentoPgrCommand`/`AnexarDocumentoPgrCommandHandler` não existem).

- [ ] **Step 3: Implementar o comando**

```csharp
// src/AAHBRANT.SST.Application/Pgrs/Commands/AnexarDocumentoPgrCommand.cs
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pgrs.Commands;

// Documento PDF original do PGR, consultado (não editado) na aba "PGR" da tela de detalhe — mesmo
// padrão de anexo binário direto na linha já usado por AnexarFotoTrabalhadorCommand.
public record AnexarDocumentoPgrCommand(
    Guid PgrId,
    byte[] Conteudo,
    string ContentType) : IRequest;

public class AnexarDocumentoPgrCommandValidator : AbstractValidator<AnexarDocumentoPgrCommand>
{
    private const int TamanhoMaximoBytes = 20 * 1024 * 1024;

    public AnexarDocumentoPgrCommandValidator()
    {
        RuleFor(x => x.PgrId).NotEmpty();
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O documento é obrigatório.")
            .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O documento deve ter no máximo 20 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.ContentType).Equal("application/pdf").WithMessage("O documento deve ser um arquivo PDF.");
    }
}

public class AnexarDocumentoPgrCommandHandler : IRequestHandler<AnexarDocumentoPgrCommand>
{
    private readonly IAppDbContext _db;

    public AnexarDocumentoPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarDocumentoPgrCommand request, CancellationToken ct)
    {
        var pgr = await _db.Pgrs.FirstOrDefaultAsync(p => p.Id == request.PgrId, ct)
            ?? throw new KeyNotFoundException($"PGR {request.PgrId} não encontrado.");

        pgr.DocumentoConteudo = request.Conteudo;
        pgr.DocumentoContentType = request.ContentType;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter AnexarDocumentoPgrCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Pgrs/Commands/AnexarDocumentoPgrCommand.cs tests/AAHBRANT.SST.Application.Tests/Pgrs/AnexarDocumentoPgrCommandHandlerTests.cs
git commit -m "feat(pgr): comando para anexar documento PDF"
```

---

### Task 3: PGR — consulta `ObterDocumentoPgrQuery`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Pgrs/Queries/ObterDocumentoPgrQuery.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Pgrs/ObterDocumentoPgrQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `Pgr.DocumentoConteudo`/`DocumentoContentType` (Task 1).
- Produces: `DocumentoPgrResultado { byte[] Conteudo; string ContentType; string NomeArquivo; }`, `ObterDocumentoPgrQuery(Guid PgrId) : IRequest<DocumentoPgrResultado?>` — consumido pelo controller na Task 4.

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Pgrs/ObterDocumentoPgrQueryHandlerTests.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Pgrs;

public class ObterDocumentoPgrQueryHandlerTests
{
    private static readonly byte[] PdfMinimo = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_PgrSemDocumento_RetornaNull()
    {
        var db = CriarDb(nameof(Handle_PgrSemDocumento_RetornaNull));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var pgr = new Pgr { ObraId = obra.Id, Obra = obra, Nome = "PGR Teste", DataElaboracao = DateTime.UtcNow };
        db.Obras.Add(obra);
        db.Pgrs.Add(pgr);
        await db.SaveChangesAsync(default);

        var handler = new ObterDocumentoPgrQueryHandler(db);
        var resultado = await handler.Handle(new ObterDocumentoPgrQuery(pgr.Id), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Handle_PgrComDocumento_RetornaConteudoEContentType()
    {
        var db = CriarDb(nameof(Handle_PgrComDocumento_RetornaConteudoEContentType));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var pgr = new Pgr
        {
            ObraId = obra.Id,
            Obra = obra,
            Nome = "PGR Teste",
            DataElaboracao = DateTime.UtcNow,
            DocumentoConteudo = PdfMinimo,
            DocumentoContentType = "application/pdf",
        };
        db.Obras.Add(obra);
        db.Pgrs.Add(pgr);
        await db.SaveChangesAsync(default);

        var handler = new ObterDocumentoPgrQueryHandler(db);
        var resultado = await handler.Handle(new ObterDocumentoPgrQuery(pgr.Id), default);

        Assert.NotNull(resultado);
        Assert.Equal(PdfMinimo, resultado!.Conteudo);
        Assert.Equal("application/pdf", resultado.ContentType);
        Assert.Equal($"pgr-{pgr.Id}.pdf", resultado.NomeArquivo);
    }
}
```

- [ ] **Step 2: Rodar o teste e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter ObterDocumentoPgrQueryHandlerTests`
Expected: erro de compilação (classe ainda não existe).

- [ ] **Step 3: Implementar a consulta**

```csharp
// src/AAHBRANT.SST.Application/Pgrs/Queries/ObterDocumentoPgrQuery.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pgrs.Queries;

public class DocumentoPgrResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public record ObterDocumentoPgrQuery(Guid PgrId) : IRequest<DocumentoPgrResultado?>;

public class ObterDocumentoPgrQueryHandler : IRequestHandler<ObterDocumentoPgrQuery, DocumentoPgrResultado?>
{
    private readonly IAppDbContext _db;

    public ObterDocumentoPgrQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoPgrResultado?> Handle(ObterDocumentoPgrQuery request, CancellationToken ct)
    {
        var pgr = await _db.Pgrs.FirstOrDefaultAsync(p => p.Id == request.PgrId, ct);
        if (pgr?.DocumentoConteudo is null || pgr.DocumentoContentType is null) return null;

        return new DocumentoPgrResultado
        {
            Conteudo = pgr.DocumentoConteudo,
            ContentType = pgr.DocumentoContentType,
            NomeArquivo = $"pgr-{pgr.Id}.pdf",
        };
    }
}
```

- [ ] **Step 4: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter ObterDocumentoPgrQueryHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Pgrs/Queries/ObterDocumentoPgrQuery.cs tests/AAHBRANT.SST.Application.Tests/Pgrs/ObterDocumentoPgrQueryHandlerTests.cs
git commit -m "feat(pgr): consulta para obter documento PDF"
```

---

### Task 4: PGR — endpoints no `PgrsController`

**Files:**
- Modify: `src/AAHBRANT.SST.Api/Controllers/PgrsController.cs`

**Interfaces:**
- Consumes: `AnexarDocumentoPgrCommand` (Task 2), `ObterDocumentoPgrQuery`/`DocumentoPgrResultado` (Task 3).

- [ ] **Step 1: Adicionar os `using` no topo do arquivo**

```csharp
using AAHBRANT.SST.Application.Pgrs.Commands;
using AAHBRANT.SST.Application.Pgrs.Queries;
using Microsoft.AspNetCore.Http;
```

(o `using AAHBRANT.SST.Application.Pgrs.Commands;` já existe — não duplicar.)

- [ ] **Step 2: Adicionar os dois endpoints, logo antes do `[Authorize(Policy = "pgr:editar")]` do método `Excluir`**

```csharp
    [Authorize(Policy = "pgr:editar")]
    [HttpPost("{id:guid}/documento")]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> AnexarDocumento(Guid id, [FromForm] AnexarDocumentoPgrRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);

        await _mediator.Send(new AnexarDocumentoPgrCommand(id, stream.ToArray(), body.Arquivo.ContentType), ct);
        return NoContent();
    }

    [Authorize(Policy = "pgr:ver")]
    [HttpGet("{id:guid}/documento")]
    public async Task<IActionResult> ObterDocumento(Guid id, CancellationToken ct)
    {
        var documento = await _mediator.Send(new ObterDocumentoPgrQuery(id), ct);
        return documento is null ? NotFound() : File(documento.Conteudo, documento.ContentType);
    }

```

- [ ] **Step 3: Adicionar a classe do corpo do request, depois do fechamento da classe `PgrsController`**

```csharp
public class AnexarDocumentoPgrRequestBody
{
    public IFormFile Arquivo { get; set; } = null!;
}
```

- [ ] **Step 4: Confirmar que compila**

Run: `dotnet build src/AAHBRANT.SST.Api/AAHBRANT.SST.Api.csproj`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Api/Controllers/PgrsController.cs
git commit -m "feat(pgr): endpoints de upload/download do documento PDF"
```

---

### Task 5: PCMSO — comando `AnexarDocumentoPcmsoCommand`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Pcmsos/Commands/AnexarDocumentoPcmsoCommand.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Pcmsos/AnexarDocumentoPcmsoCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `PcmsoDetalhe.DocumentoConteudo`/`DocumentoContentType` (Task 1); `IAppDbContext.PcmsoDetalhes : DbSet<PcmsoDetalhe>` (existente).
- Produces: `AnexarDocumentoPcmsoCommand(Guid PcmsoId, byte[] Conteudo, string ContentType) : IRequest` — consumido pelo controller na Task 7.

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Pcmsos/AnexarDocumentoPcmsoCommandHandlerTests.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pcmsos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Pcmsos;

public class AnexarDocumentoPcmsoCommandHandlerTests
{
    private static readonly byte[] PdfMinimo = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_PcmsoExistente_GravaConteudoEContentType()
    {
        var db = CriarDb(nameof(Handle_PcmsoExistente_GravaConteudoEContentType));
        var pcmso = new PcmsoDetalhe { Nome = "PCMSO Teste", DataEmissao = DateTime.UtcNow };
        db.PcmsoDetalhes.Add(pcmso);
        await db.SaveChangesAsync(default);

        var handler = new AnexarDocumentoPcmsoCommandHandler(db);
        await handler.Handle(new AnexarDocumentoPcmsoCommand(pcmso.Id, PdfMinimo, "application/pdf"), default);

        var atualizado = await db.PcmsoDetalhes.FirstAsync(p => p.Id == pcmso.Id);
        Assert.Equal(PdfMinimo, atualizado.DocumentoConteudo);
        Assert.Equal("application/pdf", atualizado.DocumentoContentType);
    }

    [Fact]
    public async Task Handle_PcmsoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_PcmsoInexistente_LancaKeyNotFoundException));
        var handler = new AnexarDocumentoPcmsoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AnexarDocumentoPcmsoCommand(Guid.NewGuid(), PdfMinimo, "application/pdf"), default));
    }
}
```

- [ ] **Step 2: Rodar o teste e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter AnexarDocumentoPcmsoCommandHandlerTests`
Expected: erro de compilação (classe ainda não existe).

- [ ] **Step 3: Implementar o comando**

```csharp
// src/AAHBRANT.SST.Application/Pcmsos/Commands/AnexarDocumentoPcmsoCommand.cs
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record AnexarDocumentoPcmsoCommand(
    Guid PcmsoId,
    byte[] Conteudo,
    string ContentType) : IRequest;

public class AnexarDocumentoPcmsoCommandValidator : AbstractValidator<AnexarDocumentoPcmsoCommand>
{
    private const int TamanhoMaximoBytes = 20 * 1024 * 1024;

    public AnexarDocumentoPcmsoCommandValidator()
    {
        RuleFor(x => x.PcmsoId).NotEmpty();
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O documento é obrigatório.")
            .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O documento deve ter no máximo 20 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.ContentType).Equal("application/pdf").WithMessage("O documento deve ser um arquivo PDF.");
    }
}

public class AnexarDocumentoPcmsoCommandHandler : IRequestHandler<AnexarDocumentoPcmsoCommand>
{
    private readonly IAppDbContext _db;

    public AnexarDocumentoPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarDocumentoPcmsoCommand request, CancellationToken ct)
    {
        var pcmso = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.Id == request.PcmsoId, ct)
            ?? throw new KeyNotFoundException($"PCMSO {request.PcmsoId} não encontrado.");

        pcmso.DocumentoConteudo = request.Conteudo;
        pcmso.DocumentoContentType = request.ContentType;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter AnexarDocumentoPcmsoCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Pcmsos/Commands/AnexarDocumentoPcmsoCommand.cs tests/AAHBRANT.SST.Application.Tests/Pcmsos/AnexarDocumentoPcmsoCommandHandlerTests.cs
git commit -m "feat(pcmso): comando para anexar documento PDF"
```

---

### Task 6: PCMSO — consulta `ObterDocumentoPcmsoQuery`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Pcmsos/Queries/ObterDocumentoPcmsoQuery.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Pcmsos/ObterDocumentoPcmsoQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `PcmsoDetalhe.DocumentoConteudo`/`DocumentoContentType` (Task 1).
- Produces: `DocumentoPcmsoResultado { byte[] Conteudo; string ContentType; string NomeArquivo; }`, `ObterDocumentoPcmsoQuery(Guid PcmsoId) : IRequest<DocumentoPcmsoResultado?>` — consumido pelo controller na Task 7.

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Pcmsos/ObterDocumentoPcmsoQueryHandlerTests.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pcmsos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Pcmsos;

public class ObterDocumentoPcmsoQueryHandlerTests
{
    private static readonly byte[] PdfMinimo = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_PcmsoSemDocumento_RetornaNull()
    {
        var db = CriarDb(nameof(Handle_PcmsoSemDocumento_RetornaNull));
        var pcmso = new PcmsoDetalhe { Nome = "PCMSO Teste", DataEmissao = DateTime.UtcNow };
        db.PcmsoDetalhes.Add(pcmso);
        await db.SaveChangesAsync(default);

        var handler = new ObterDocumentoPcmsoQueryHandler(db);
        var resultado = await handler.Handle(new ObterDocumentoPcmsoQuery(pcmso.Id), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Handle_PcmsoComDocumento_RetornaConteudoEContentType()
    {
        var db = CriarDb(nameof(Handle_PcmsoComDocumento_RetornaConteudoEContentType));
        var pcmso = new PcmsoDetalhe
        {
            Nome = "PCMSO Teste",
            DataEmissao = DateTime.UtcNow,
            DocumentoConteudo = PdfMinimo,
            DocumentoContentType = "application/pdf",
        };
        db.PcmsoDetalhes.Add(pcmso);
        await db.SaveChangesAsync(default);

        var handler = new ObterDocumentoPcmsoQueryHandler(db);
        var resultado = await handler.Handle(new ObterDocumentoPcmsoQuery(pcmso.Id), default);

        Assert.NotNull(resultado);
        Assert.Equal(PdfMinimo, resultado!.Conteudo);
        Assert.Equal("application/pdf", resultado.ContentType);
        Assert.Equal($"pcmso-{pcmso.Id}.pdf", resultado.NomeArquivo);
    }
}
```

- [ ] **Step 2: Rodar o teste e confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter ObterDocumentoPcmsoQueryHandlerTests`
Expected: erro de compilação (classe ainda não existe).

- [ ] **Step 3: Implementar a consulta**

```csharp
// src/AAHBRANT.SST.Application/Pcmsos/Queries/ObterDocumentoPcmsoQuery.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Queries;

public class DocumentoPcmsoResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public record ObterDocumentoPcmsoQuery(Guid PcmsoId) : IRequest<DocumentoPcmsoResultado?>;

public class ObterDocumentoPcmsoQueryHandler : IRequestHandler<ObterDocumentoPcmsoQuery, DocumentoPcmsoResultado?>
{
    private readonly IAppDbContext _db;

    public ObterDocumentoPcmsoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoPcmsoResultado?> Handle(ObterDocumentoPcmsoQuery request, CancellationToken ct)
    {
        var pcmso = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.Id == request.PcmsoId, ct);
        if (pcmso?.DocumentoConteudo is null || pcmso.DocumentoContentType is null) return null;

        return new DocumentoPcmsoResultado
        {
            Conteudo = pcmso.DocumentoConteudo,
            ContentType = pcmso.DocumentoContentType,
            NomeArquivo = $"pcmso-{pcmso.Id}.pdf",
        };
    }
}
```

- [ ] **Step 4: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter ObterDocumentoPcmsoQueryHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Pcmsos/Queries/ObterDocumentoPcmsoQuery.cs tests/AAHBRANT.SST.Application.Tests/Pcmsos/ObterDocumentoPcmsoQueryHandlerTests.cs
git commit -m "feat(pcmso): consulta para obter documento PDF"
```

---

### Task 7: PCMSO — endpoints no `PcmsosController`

**Files:**
- Modify: `src/AAHBRANT.SST.Api/Controllers/PcmsosController.cs`

**Interfaces:**
- Consumes: `AnexarDocumentoPcmsoCommand` (Task 5), `ObterDocumentoPcmsoQuery`/`DocumentoPcmsoResultado` (Task 6).

- [ ] **Step 1: Adicionar os `using` no topo do arquivo**

```csharp
using AAHBRANT.SST.Application.Pcmsos.Commands;
using AAHBRANT.SST.Application.Pcmsos.Queries;
using Microsoft.AspNetCore.Http;
```

(os `using` de `Pcmsos.Commands`/`Pcmsos.Queries` já existem — não duplicar.)

- [ ] **Step 2: Adicionar os dois endpoints, logo antes do `[Authorize(Policy = "pcmso:editar")]` do método `Excluir`**

```csharp
    [Authorize(Policy = "pcmso:editar")]
    [HttpPost("{id:guid}/documento")]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> AnexarDocumento(Guid id, [FromForm] AnexarDocumentoPcmsoRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);

        await _mediator.Send(new AnexarDocumentoPcmsoCommand(id, stream.ToArray(), body.Arquivo.ContentType), ct);
        return NoContent();
    }

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet("{id:guid}/documento")]
    public async Task<IActionResult> ObterDocumento(Guid id, CancellationToken ct)
    {
        var documento = await _mediator.Send(new ObterDocumentoPcmsoQuery(id), ct);
        return documento is null ? NotFound() : File(documento.Conteudo, documento.ContentType);
    }

```

- [ ] **Step 3: Adicionar a classe do corpo do request, depois do fechamento da classe `PcmsosController`**

```csharp
public class AnexarDocumentoPcmsoRequestBody
{
    public IFormFile Arquivo { get; set; } = null!;
}
```

- [ ] **Step 4: Confirmar que compila**

Run: `dotnet build src/AAHBRANT.SST.Api/AAHBRANT.SST.Api.csproj`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Api/Controllers/PcmsosController.cs
git commit -m "feat(pcmso): endpoints de upload/download do documento PDF"
```

---

### Task 8: Frontend — `api.ts` (métodos `obterDocumento`/`enviarDocumento`)

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`

**Interfaces:**
- Produces: `api.pgrs.obterDocumento(id: string): Promise<Blob | null>`, `api.pgrs.enviarDocumento(id: string, arquivo: File): Promise<void>`, `api.pcmsos.obterDocumento(id: string): Promise<Blob | null>`, `api.pcmsos.enviarDocumento(id: string, arquivo: File): Promise<void>` — consumidos pelo componente da Task 9.

- [ ] **Step 1: No objeto `pgrs` (dentro de `export const api = {`), adicionar os dois métodos, logo após `excluir: (id: string) => request<void>(\`/api/pgrs/${id}\`, { method: 'DELETE' }),`**

```typescript
    obterDocumento: async (id: string): Promise<Blob | null> => {
      const response = await fetch(`${API_BASE_URL}/api/pgrs/${id}/documento`, {
        headers: await montarHeadersAuth(),
      });
      if (response.status === 404) return null;
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(extrairMensagemErro(corpo, response.status, response.statusText));
      }
      return response.blob();
    },
    enviarDocumento: async (id: string, arquivo: File) => {
      const formData = new FormData();
      formData.append('Arquivo', arquivo);
      const response = await fetch(`${API_BASE_URL}/api/pgrs/${id}/documento`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(extrairMensagemErro(corpo, response.status, response.statusText));
      }
    },
```

- [ ] **Step 2: No objeto `pcmsos`, adicionar os dois métodos equivalentes, logo após `excluir: (id: string) => request<void>(\`/api/pcmsos/${id}\`, { method: 'DELETE' }),`**

```typescript
    obterDocumento: async (id: string): Promise<Blob | null> => {
      const response = await fetch(`${API_BASE_URL}/api/pcmsos/${id}/documento`, {
        headers: await montarHeadersAuth(),
      });
      if (response.status === 404) return null;
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(extrairMensagemErro(corpo, response.status, response.statusText));
      }
      return response.blob();
    },
    enviarDocumento: async (id: string, arquivo: File) => {
      const formData = new FormData();
      formData.append('Arquivo', arquivo);
      const response = await fetch(`${API_BASE_URL}/api/pcmsos/${id}/documento`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(extrairMensagemErro(corpo, response.status, response.statusText));
      }
    },
```

- [ ] **Step 3: Confirmar que compila**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: sem erros de TypeScript.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat(api): metodos de documento PDF para pgrs e pcmsos"
```

---

### Task 9: Frontend — componente compartilhado `VisualizadorDocumentoPdf`

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/components/VisualizadorDocumentoPdf.tsx`

**Interfaces:**
- Consumes: `SeletorFotoCamera` (existente, `src/components/SeletorFotoCamera.tsx`), `EstadoVazio` (existente), `usePageStyles().erro` (existente).
- Produces: `VisualizadorDocumentoPdf({ id, obterDocumento, enviarDocumento })` — consumido por `PgrDetalhePage.tsx` (Task 10) e `PcmsoDetalhePage.tsx` (Task 11).

- [ ] **Step 1: Criar o componente**

```tsx
// src/AAHBRANT.SST.TeamsApp/src/components/VisualizadorDocumentoPdf.tsx
import { useEffect, useRef, useState } from 'react';
import { Spinner, Text } from '@fluentui/react-components';
import { SeletorFotoCamera } from './SeletorFotoCamera';
import { EstadoVazio } from './EstadoVazio';
import { usePageStyles } from '../pages/pageStyles';

interface VisualizadorDocumentoPdfProps {
  id: string;
  obterDocumento: () => Promise<Blob | null>;
  enviarDocumento: (arquivo: File) => Promise<void>;
}

// Mostra o PDF já cadastrado direto na tela (iframe + Blob URL), com rolagem nativa do navegador
// por todas as páginas — pedido do usuário (09/09): nada de botão "abrir em outro lugar", o
// documento tem que aparecer inteiro assim que a aba é aberta.
export function VisualizadorDocumentoPdf({ id, obterDocumento, enviarDocumento }: VisualizadorDocumentoPdfProps) {
  const estilos = usePageStyles();
  const [carregando, setCarregando] = useState(true);
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const blobUrlRef = useRef<string | null>(null);

  async function carregar() {
    try {
      setCarregando(true);
      setErro(null);
      const blob = await obterDocumento();
      if (blobUrlRef.current) {
        URL.revokeObjectURL(blobUrlRef.current);
        blobUrlRef.current = null;
      }
      if (!blob) {
        setBlobUrl(null);
        return;
      }
      const url = URL.createObjectURL(blob);
      blobUrlRef.current = url;
      setBlobUrl(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar o documento.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    return () => {
      if (blobUrlRef.current) URL.revokeObjectURL(blobUrlRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function enviar(arquivo: File) {
    try {
      setErro(null);
      await enviarDocumento(arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar o documento.');
    }
  }

  if (carregando) {
    return (
      <div style={{ padding: 32, textAlign: 'center' }}>
        <Spinner label="Carregando documento..." />
      </div>
    );
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}
      {!blobUrl ? (
        <>
          <EstadoVazio mensagem="Nenhum documento anexado ainda." />
          <div style={{ textAlign: 'center', marginTop: 8 }}>
            <SeletorFotoCamera
              rotulo="Anexar documento"
              tiposAceitos="application/pdf"
              tamanhoMaximoMb={20}
              aoSelecionarArquivo={enviar}
              aoErroValidacao={setErro}
            />
          </div>
        </>
      ) : (
        <>
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 8 }}>
            <SeletorFotoCamera
              rotulo="Substituir documento"
              tiposAceitos="application/pdf"
              tamanhoMaximoMb={20}
              aoSelecionarArquivo={enviar}
              aoErroValidacao={setErro}
            />
          </div>
          <iframe src={blobUrl} title="Documento" style={{ width: '100%', height: '80vh', border: 'none' }} />
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Confirmar que compila**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: sem erros de TypeScript.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/components/VisualizadorDocumentoPdf.tsx
git commit -m "feat(ui): componente VisualizadorDocumentoPdf"
```

---

### Task 10: Frontend — aba "PGR" em `PgrDetalhePage.tsx`

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/pgr/PgrDetalhePage.tsx`

**Interfaces:**
- Consumes: `VisualizadorDocumentoPdf` (Task 9), `api.pgrs.obterDocumento`/`enviarDocumento` (Task 8).

- [ ] **Step 1: Adicionar o import do componente, junto aos outros imports de aba**

Substituir:
```typescript
import { InventarioTab } from './InventarioTab';
import { PlanoAcaoTab } from './PlanoAcaoTab';
import { PgrRevisoesTab } from './PgrRevisoesTab';
```
por:
```typescript
import { InventarioTab } from './InventarioTab';
import { PlanoAcaoTab } from './PlanoAcaoTab';
import { PgrRevisoesTab } from './PgrRevisoesTab';
import { VisualizadorDocumentoPdf } from '../../components/VisualizadorDocumentoPdf';
```

- [ ] **Step 2: Adicionar `'documento'` ao tipo de aba e trocar o valor inicial**

Substituir:
```typescript
type AbaPgr = 'inventario' | 'planoAcao' | 'revisoes';
```
por:
```typescript
type AbaPgr = 'documento' | 'inventario' | 'planoAcao' | 'revisoes';
```

Substituir:
```typescript
  const [aba, setAba] = useState<AbaPgr>('inventario');
```
por:
```typescript
  const [aba, setAba] = useState<AbaPgr>('documento');
```

- [ ] **Step 3: Adicionar a aba "PGR" antes de "Inventário de riscos"**

Substituir:
```tsx
      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPgr)}
        className={estilosAba.lista}
      >
        <Tab value="inventario">Inventário de riscos</Tab>
```
por:
```tsx
      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPgr)}
        className={estilosAba.lista}
      >
        <Tab value="documento">PGR</Tab>
        <Tab value="inventario">Inventário de riscos</Tab>
```

- [ ] **Step 4: Adicionar a renderização condicional da aba, logo antes de `{aba === 'inventario' && ...}`**

Substituir:
```tsx
      {aba === 'inventario' && <InventarioTab atividades={detalhe?.atividades ?? []} />}
```
por:
```tsx
      {aba === 'documento' && (
        <VisualizadorDocumentoPdf
          id={id}
          obterDocumento={() => api.pgrs.obterDocumento(id)}
          enviarDocumento={(arquivo) => api.pgrs.enviarDocumento(id, arquivo)}
        />
      )}
      {aba === 'inventario' && <InventarioTab atividades={detalhe?.atividades ?? []} />}
```

- [ ] **Step 5: Confirmar que compila**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: sem erros de TypeScript.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/pgr/PgrDetalhePage.tsx
git commit -m "feat(pgr): aba PGR com visualizador de documento PDF, como aba padrao"
```

---

### Task 11: Frontend — abas "PCMSO"/"Dados" em `PcmsoDetalhePage.tsx`

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/saude-ocupacional/PcmsoDetalhePage.tsx`

**Interfaces:**
- Consumes: `VisualizadorDocumentoPdf` (Task 9), `api.pcmsos.obterDocumento`/`enviarDocumento` (Task 8), `usePillTabStyles` (existente, `src/pages/pageStyles.ts`).

- [ ] **Step 1: Adicionar `Tab`, `TabList` e os tipos de evento ao import do Fluent UI**

Substituir:
```typescript
import {
  Badge,
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
```
por:
```typescript
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
```

- [ ] **Step 2: Adicionar os imports do componente novo e de `usePillTabStyles`**

Substituir:
```typescript
import { BadgeVencimento } from '../../components/badges/BadgeVencimento';
import { usePageStyles } from '../pageStyles';
```
por:
```typescript
import { BadgeVencimento } from '../../components/badges/BadgeVencimento';
import { VisualizadorDocumentoPdf } from '../../components/VisualizadorDocumentoPdf';
import { usePageStyles, usePillTabStyles } from '../pageStyles';
```

- [ ] **Step 3: Adicionar o estado de aba, logo após `const estilos = usePageStyles();`**

Substituir:
```typescript
  const estilos = usePageStyles();
  const [pcmso, setPcmso] = useState<Pcmso | null>(null);
```
por:
```typescript
  const estilos = usePageStyles();
  const estilosAba = usePillTabStyles();
  const [aba, setAba] = useState<'documento' | 'dados'>('documento');
  const [pcmso, setPcmso] = useState<Pcmso | null>(null);
```

- [ ] **Step 4: Adicionar a `TabList` e envolver o conteúdo existente na aba "Dados"**

Substituir:
```tsx
      {!pcmso || !edicao ? (
        <Text>Carregando...</Text>
      ) : (
        <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
```
por:
```tsx
      {!pcmso || !edicao ? (
        <Text>Carregando...</Text>
      ) : (
        <>
          <TabList
            selectedValue={aba}
            onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as 'documento' | 'dados')}
            className={estilosAba.lista}
            style={{ marginBottom: 16 }}
          >
            <Tab value="documento">PCMSO</Tab>
            <Tab value="dados">Dados</Tab>
          </TabList>

          {aba === 'documento' && (
            <VisualizadorDocumentoPdf
              id={id}
              obterDocumento={() => api.pcmsos.obterDocumento(id)}
              enviarDocumento={(arquivo) => api.pcmsos.enviarDocumento(id, arquivo)}
            />
          )}

          {aba === 'dados' && (
            <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
```

- [ ] **Step 5: Fechar o novo bloco condicional no final do JSX**

Substituir (final do arquivo):
```tsx
              </TableBody>
            </Table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
```
por:
```tsx
              </TableBody>
            </Table>
            )}
          </div>
            </>
          )}
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 6: Confirmar que compila**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: sem erros de TypeScript.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/saude-ocupacional/PcmsoDetalhePage.tsx
git commit -m "feat(pcmso): abas PCMSO (documento, padrao) e Dados"
```

---

### Task 12: Verificação visual no navegador

**Files:** nenhum (só verificação manual).

- [ ] **Step 1: Subir o backend e o frontend localmente (dev server) e abrir o Browser pane**

- [ ] **Step 2: Abrir um PGR já cadastrado (lista de PGRs → clicar numa linha) e confirmar:**
  - A aba **"PGR"** já vem selecionada por padrão, antes de "Inventário de riscos".
  - Sem documento ainda: aparece "Nenhum documento anexado ainda." e o botão "Anexar documento".
  - Enviar um PDF de teste pelo botão: o documento aparece renderizado por inteiro na tela, com rolagem funcionando por todas as páginas.
  - Trocar para outra aba e voltar para "PGR": o documento continua aparecendo (sem duplo carregamento quebrado).

- [ ] **Step 3: Repetir o mesmo roteiro para um PCMSO já cadastrado (abas "PCMSO"/"Dados"), confirmando que a aba "Dados" mostra exatamente o formulário que já existia antes, sem nenhuma mudança de comportamento nela.**

- [ ] **Step 4: Reportar ao usuário com prints/descrição do que foi verificado, antes de considerar a feature concluída.**
