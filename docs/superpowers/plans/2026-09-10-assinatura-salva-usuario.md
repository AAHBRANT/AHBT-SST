# Assinatura Salva do Usuário — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Usuário logado cadastra a própria assinatura (desenho ou foto) uma vez, guardada no perfil (`Usuario`); ela passa a aparecer nos PDFs de APR/PT/Não Conformidade/Inspeção/Treinamento/DDS/EPI no lugar de texto puro, e os pontos de "aprovar/autorizar" que hoje aceitam um Guid de usuário cru do cliente passam a resolver a identidade pela própria sessão logada.

**Architecture:** Duas frentes independentes que convergem no mesmo lugar (imagem salva em `Usuario`): (1) cadastro da assinatura + correção dos pontos de aprovação/autorização que não verificavam identidade (Apr/PT/NC — 6 comandos, mesmo formato em todos); (2) exposição dessa imagem nos PDFs, tanto nos blocos bespoke de Apr/PT/NC quanto no comprovante genérico do Motor de Assinatura (que precisa de um campo novo, `DocumentoSignatario.UsuarioId`, para saber qual login assinou via `SessaoLogada`).

**Tech Stack:** .NET 8 / EF Core 8 / QuestPDF / xUnit (backend), React + TypeScript + Vite + Fluent UI (frontend), `signature_pad` (novo, canvas de assinatura).

**Spec:** `docs/superpowers/specs/2026-09-10-assinatura-salva-usuario-design.md`

## Global Constraints

- Migrations sempre geradas via `dotnet ef migrations add <Nome> --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api` — nunca escritas à mão.
- `dotnet build SST-APP.sln` e (dentro de `src/AAHBRANT.SST.TeamsApp`) `npx tsc -b --force` devem ficar limpos ao final de cada task que toque os respectivos projetos.
- `dotnet test SST-APP.sln` deve continuar 100% verde ao final de cada task de backend.
- Imagem de assinatura: mesmo padrão de validação de `AnexarFotoTrabalhadorCommand` — só `image/jpeg`/`image/png`, máx. 2 MB (assinatura é um traço simples, não precisa do limite de 5 MB usado para fotos), conteúdo verificado por `ValidadorAssinaturaArquivo.AssinaturaConfere`.
- Achado durante o planejamento (não estava na spec original): `EncerrarNaoConformidadeCommand` tem exatamente o mesmo problema de Apr/PT (`ValidadoPorUsuarioId` vindo do cliente, ainda que via dropdown em vez de texto livre) — por bater com a lista original do usuário ("Inspeções e Não Conformidades"), entra neste plano junto com Apr/PT (Task 4). `ValidarAcaoPlanoCommand` (usado por NC *e* Acidente) tem o mesmo formato mas fica **fora de escopo** — não estava na lista do usuário e tem raio de impacto maior (módulo compartilhado); ver seção final "Fora de escopo".

---

### Task 1: `Usuario` ganha os campos de assinatura + migration

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/PerfilAcesso.cs` (classe `Usuario`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AcessoConfiguracoes.cs` (`UsuarioConfiguracao`)
- Create: migration gerada por `dotnet ef migrations add`

**Interfaces:**
- Produces: `Usuario.AssinaturaImagemConteudo` (`byte[]?`), `Usuario.AssinaturaImagemContentType` (`string?`), `Usuario.AssinaturaAtualizadaEm` (`DateTime?`) — consumidos pelas Tasks 2, 6, 7.

- [ ] **Step 1: Adicionar os campos em `Usuario`**

Em `src/AAHBRANT.SST.Domain/Entidades/PerfilAcesso.cs`, dentro da classe `Usuario` (logo após `public Trabalhador? Trabalhador { get; set; }`), adicionar:

```csharp
    // Assinatura salva (desenhada ou foto) — cadastrada pelo próprio usuário no ícone de perfil
    // (ver AppShell.tsx). Mesmo padrão de Trabalhador.FotoConteudo: blob direto na linha, sem Blob
    // Storage (não provisionado neste projeto). Reaproveitada em qualquer PDF onde este Usuario
    // aparece como quem aprovou/autorizou/assinou (ver docs/superpowers/specs/2026-09-10-
    // assinatura-salva-usuario-design.md).
    public byte[]? AssinaturaImagemConteudo { get; set; }
    public string? AssinaturaImagemContentType { get; set; }
    public DateTime? AssinaturaAtualizadaEm { get; set; }
```

- [ ] **Step 2: Configurar o tamanho máximo do content-type**

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AcessoConfiguracoes.cs`, dentro de `UsuarioConfiguracao.Configure`, adicionar (mesmo padrão de `FotoContentType` em `OrganizacaoConfiguracoes.cs`):

```csharp
        builder.Property(u => u.AssinaturaImagemContentType).HasMaxLength(100);
```

- [ ] **Step 3: Gerar a migration**

Run:
```bash
dotnet ef migrations add AdicionarAssinaturaUsuario --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api
```
Expected: novo arquivo em `Migrations/` só com `AddColumn` para as 3 colunas novas em `Usuarios`.

- [ ] **Step 4: Build**

Run: `dotnet build SST-APP.sln --nologo -v quiet`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/PerfilAcesso.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AcessoConfiguracoes.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat(usuarios): adiciona campos de assinatura salva (imagem, content-type, data)"
```

---

### Task 2: Comando/consulta para salvar e obter a própria assinatura + endpoints

**Files:**
- Create: `src/AAHBRANT.SST.Application/Usuarios/Commands/SalvarAssinaturaUsuarioCommand.cs`
- Create: `src/AAHBRANT.SST.Application/Usuarios/Queries/ObterAssinaturaUsuarioQuery.cs`
- Create: `tests/AAHBRANT.SST.Application.Tests/Usuarios/SalvarAssinaturaUsuarioCommandTests.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/UsuariosController.cs`

**Interfaces:**
- Consumes: `IAppDbContext.Usuarios` (`DbSet<Usuario>`), `ValidadorAssinaturaArquivo.AssinaturaConfere(byte[], string)` (já existe em `Application/Common/`).
- Produces: `SalvarAssinaturaUsuarioCommand(string? AzureAdObjectId, byte[] ImagemConteudo, string ImagemContentType) : IRequest`; `ObterAssinaturaUsuarioQuery(string? AzureAdObjectId) : IRequest<AssinaturaUsuarioDto?>` com `record AssinaturaUsuarioDto(string ContentType, DateTime AtualizadaEm)` (a imagem em si vai por um endpoint de bytes separado, mesmo padrão de foto do trabalhador).

- [ ] **Step 1: Escrever o teste do comando (vai falhar — classe ainda não existe)**

Criar `tests/AAHBRANT.SST.Application.Tests/Usuarios/SalvarAssinaturaUsuarioCommandTests.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Usuarios.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Usuarios;

public class SalvarAssinaturaUsuarioCommandTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new AAHBRANT.SST.Infrastructure.Seguranca.CurrentUserService());
    }

    // PNG mínimo válido (assinatura de arquivo real, 1x1 pixel) — necessário porque o validador
    // confere os magic bytes, não só a extensão/content-type declarado.
    private static readonly byte[] PngMinimo =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    };

    [Fact]
    public async Task Handle_DeveSalvarAssinaturaNoUsuarioDaSessao()
    {
        var db = CriarDb(nameof(Handle_DeveSalvarAssinaturaNoUsuarioDaSessao));
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico Teste", AzureAdObjectId = "oid-123" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        var handler = new SalvarAssinaturaUsuarioCommandHandler(db);
        await handler.Handle(new SalvarAssinaturaUsuarioCommand("oid-123", PngMinimo, "image/png"), CancellationToken.None);

        var atualizado = await db.Usuarios.FirstAsync(u => u.Id == usuario.Id);
        Assert.Equal(PngMinimo, atualizado.AssinaturaImagemConteudo);
        Assert.Equal("image/png", atualizado.AssinaturaImagemContentType);
        Assert.NotNull(atualizado.AssinaturaAtualizadaEm);
    }

    [Fact]
    public async Task Handle_SemUsuarioVinculadoAoOid_LancaExcecaoAmigavel()
    {
        var db = CriarDb(nameof(Handle_SemUsuarioVinculadoAoOid_LancaExcecaoAmigavel));
        var handler = new SalvarAssinaturaUsuarioCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SalvarAssinaturaUsuarioCommand("oid-inexistente", PngMinimo, "image/png"), CancellationToken.None));
    }
}
```

- [ ] **Step 2: Rodar o teste para confirmar que falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter SalvarAssinaturaUsuarioCommandTests`
Expected: FAIL — `SalvarAssinaturaUsuarioCommand`/`Handler` não existem ainda.

- [ ] **Step 3: Implementar o comando**

Criar `src/AAHBRANT.SST.Application/Usuarios/Commands/SalvarAssinaturaUsuarioCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Commands;

// Assinatura salva do próprio usuário logado (ícone de perfil, canto superior direito — ver
// AppShell.tsx) — resolve o Usuario a partir do AzureAdObjectId da sessão, mesmo padrão de
// RegistrarAssinaturaSessaoLogadaCommand. Guarda o traço/foto como imagem no perfil (mesmo padrão
// de Trabalhador.FotoConteudo), reaproveitada depois em qualquer PDF onde este Usuario assina.
public record SalvarAssinaturaUsuarioCommand(
    string? AzureAdObjectId,
    byte[] ImagemConteudo,
    string ImagemContentType) : IRequest;

public class SalvarAssinaturaUsuarioCommandValidator : AbstractValidator<SalvarAssinaturaUsuarioCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 2 * 1024 * 1024;

    public SalvarAssinaturaUsuarioCommandValidator()
    {
        RuleFor(x => x.ImagemConteudo)
            .NotEmpty().WithMessage("A imagem da assinatura é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A assinatura deve ter no máximo 2 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ImagemContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.ImagemContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A assinatura deve ser um arquivo JPEG ou PNG.");
    }
}

public class SalvarAssinaturaUsuarioCommandHandler : IRequestHandler<SalvarAssinaturaUsuarioCommand>
{
    private readonly IAppDbContext _db;

    public SalvarAssinaturaUsuarioCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(SalvarAssinaturaUsuarioCommand request, CancellationToken ct)
    {
        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);

        if (usuario is null)
            throw new InvalidOperationException(
                "Não foi possível identificar o seu usuário. Faça login novamente e tente salvar a assinatura de novo.");

        usuario.AssinaturaImagemConteudo = request.ImagemConteudo;
        usuario.AssinaturaImagemContentType = request.ImagemContentType;
        usuario.AssinaturaAtualizadaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar o teste para confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter SalvarAssinaturaUsuarioCommandTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Implementar a consulta de leitura**

Criar `src/AAHBRANT.SST.Application/Usuarios/Queries/ObterAssinaturaUsuarioQuery.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Queries;

public record AssinaturaUsuarioDto(string ContentType, DateTime AtualizadaEm);

// Só metadado (pra tela mostrar "você já tem uma assinatura salva, atualizada em X") — a imagem em
// si sai por um endpoint de bytes à parte (mesmo padrão de foto do trabalhador/logo da obra), pra
// não inflar o corpo de uma resposta JSON com base64.
public record ObterAssinaturaUsuarioQuery(string? AzureAdObjectId) : IRequest<AssinaturaUsuarioDto?>;

public class ObterAssinaturaUsuarioQueryHandler : IRequestHandler<ObterAssinaturaUsuarioQuery, AssinaturaUsuarioDto?>
{
    private readonly IAppDbContext _db;

    public ObterAssinaturaUsuarioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AssinaturaUsuarioDto?> Handle(ObterAssinaturaUsuarioQuery request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.AzureAdObjectId)) return null;

        return await _db.Usuarios
            .Where(u => u.AzureAdObjectId == request.AzureAdObjectId && u.AssinaturaImagemConteudo != null)
            .Select(u => new AssinaturaUsuarioDto(u.AssinaturaImagemContentType!, u.AssinaturaAtualizadaEm!.Value))
            .FirstOrDefaultAsync(ct);
    }
}

// Consulta separada só pros bytes — reaproveitada pelo endpoint de imagem (Task 2, Step 6) e,
// depois, pela renderização em PDF (Tasks 6/7/8/9).
public record ObterImagemAssinaturaUsuarioQuery(Guid UsuarioId) : IRequest<(byte[] Conteudo, string ContentType)?>;

public class ObterImagemAssinaturaUsuarioQueryHandler
    : IRequestHandler<ObterImagemAssinaturaUsuarioQuery, (byte[] Conteudo, string ContentType)?>
{
    private readonly IAppDbContext _db;

    public ObterImagemAssinaturaUsuarioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<(byte[] Conteudo, string ContentType)?> Handle(ObterImagemAssinaturaUsuarioQuery request, CancellationToken ct)
    {
        var usuario = await _db.Usuarios
            .Where(u => u.Id == request.UsuarioId && u.AssinaturaImagemConteudo != null)
            .Select(u => new { u.AssinaturaImagemConteudo, u.AssinaturaImagemContentType })
            .FirstOrDefaultAsync(ct);

        return usuario is null ? null : (usuario.AssinaturaImagemConteudo!, usuario.AssinaturaImagemContentType!);
    }
}
```

- [ ] **Step 6: Expor os endpoints em `UsuariosController`**

Em `src/AAHBRANT.SST.Api/Controllers/UsuariosController.cs`, adicionar (usando `ClaimTypes`, já importado em `AssinaturaController.cs` — adicionar `using System.Security.Claims;` no topo deste arquivo também):

```csharp
    // Assinatura salva do PRÓPRIO usuário logado — [Authorize] puro (sem Policy de permissão): não
    // é administração de outro usuário, é a pessoa mexendo no perfil dela mesma. Mesmo raciocínio
    // de AssinarComSessaoLogada em AssinaturaController.
    [Authorize]
    [HttpPut("me/assinatura")]
    [RequestSizeLimit(3_000_000)]
    public async Task<IActionResult> SalvarMinhaAssinatura([FromForm] SalvarAssinaturaRequestBody body, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await using var stream = new MemoryStream();
        await body.Imagem.CopyToAsync(stream, ct);
        await _mediator.Send(new SalvarAssinaturaUsuarioCommand(azureAdObjectId, stream.ToArray(), body.Imagem.ContentType), ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me/assinatura")]
    public async Task<IActionResult> ObterMinhaAssinatura(CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var resultado = await _mediator.Send(new ObterAssinaturaUsuarioQuery(azureAdObjectId), ct);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [Authorize]
    [HttpGet("me/assinatura/imagem")]
    public async Task<IActionResult> ObterMinhaAssinaturaImagem(CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var usuario = string.IsNullOrEmpty(azureAdObjectId)
            ? null
            : await _mediator.Send(new ObterUsuarioIdPorAzureAdObjectIdQuery(azureAdObjectId), ct);
        if (usuario is null) return NotFound();

        var imagem = await _mediator.Send(new ObterImagemAssinaturaUsuarioQuery(usuario.Value), ct);
        return imagem is null ? NotFound() : File(imagem.Value.Conteudo, imagem.Value.ContentType);
    }
```

E o `RequestBody` no final do arquivo:

```csharp
public class SalvarAssinaturaRequestBody
{
    public IFormFile Imagem { get; set; } = null!;
}
```

- [ ] **Step 7: Criar a consulta auxiliar `ObterUsuarioIdPorAzureAdObjectIdQuery`**

No mesmo arquivo `ObterAssinaturaUsuarioQuery.cs` (Step 5), adicionar:

```csharp
public record ObterUsuarioIdPorAzureAdObjectIdQuery(string AzureAdObjectId) : IRequest<Guid?>;

public class ObterUsuarioIdPorAzureAdObjectIdQueryHandler : IRequestHandler<ObterUsuarioIdPorAzureAdObjectIdQuery, Guid?>
{
    private readonly IAppDbContext _db;

    public ObterUsuarioIdPorAzureAdObjectIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Guid?> Handle(ObterUsuarioIdPorAzureAdObjectIdQuery request, CancellationToken ct)
        => await _db.Usuarios.Where(u => u.AzureAdObjectId == request.AzureAdObjectId).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
}
```

Adicionar `using AAHBRANT.SST.Application.Usuarios.Queries;` no topo de `UsuariosController.cs`.

- [ ] **Step 8: Build e testes**

Run: `dotnet build SST-APP.sln --nologo -v quiet && dotnet test SST-APP.sln --nologo -v quiet 2>&1 | tail -15`
Expected: build limpo, todos os testes passando.

- [ ] **Step 9: Commit**

```bash
git add src/AAHBRANT.SST.Application/Usuarios/ src/AAHBRANT.SST.Api/Controllers/UsuariosController.cs tests/AAHBRANT.SST.Application.Tests/Usuarios/
git commit -m "feat(usuarios): endpoints para salvar/obter a assinatura do próprio usuário logado"
```

---

### Task 3: Frontend — cadastro da assinatura (canvas + foto) no ícone de perfil

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/MinhaAssinaturaDialog.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/package.json` (nova dependência)

**Interfaces:**
- Consumes: `api.usuarios.salvarMinhaAssinatura(imagem: Blob)`, `api.usuarios.obterMinhaAssinatura()`, `api.usuarios.urlMinhaAssinaturaImagem()` (novos, ver Step 2); componente `SeletorFotoCamera` (já existe, `aoSelecionarArquivo: (arquivo: File) => void | Promise<void>`).
- Produces: `<MinhaAssinaturaDialog open={boolean} onClose={() => void} />`, consumido pela Task 4.

- [ ] **Step 1: Instalar `signature_pad`**

Run (dentro de `src/AAHBRANT.SST.TeamsApp`): `npm install signature_pad`
Expected: entrada nova em `package.json`/`package-lock.json` (`signature_pad`, sem dependências extras).

- [ ] **Step 2: Adicionar os métodos em `api.ts`**

No objeto `api.usuarios` (ou criar se não existir um bloco dedicado — confirmar com `grep -n "usuarios:" src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`), adicionar:

```ts
    salvarMinhaAssinatura: async (imagem: Blob) => {
      const formData = new FormData();
      formData.append('imagem', imagem, 'assinatura.png');
      const response = await fetch(`${API_BASE_URL}/api/usuarios/me/assinatura`, {
        method: 'PUT',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(extrairMensagemErro(corpo, response.status, response.statusText));
      }
    },
    obterMinhaAssinatura: () =>
      request<{ contentType: string; atualizadaEm: string } | null>('/api/usuarios/me/assinatura').catch(() => null),
    urlMinhaAssinaturaImagem: () => `${API_BASE_URL}/api/usuarios/me/assinatura/imagem`,
```

- [ ] **Step 3: Criar o diálogo de cadastro**

Criar `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/MinhaAssinaturaDialog.tsx`:

```tsx
import { useEffect, useRef, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Tab,
  TabList,
  Text,
} from '@fluentui/react-components';
import SignaturePad from 'signature_pad';
import { api } from '../../lib/api';
import { SeletorFotoCamera } from '../SeletorFotoCamera';

export interface MinhaAssinaturaDialogProps {
  open: boolean;
  onClose: () => void;
}

// Cadastro da assinatura salva do usuário logado (ícone de perfil, AppShell.tsx) — desenho num
// canvas (signature_pad) ou foto de uma assinatura em papel (SeletorFotoCamera, mesmo padrão já
// usado em DDS/Inspeções/Obras). O PNG resultante fica salvo no Usuario e é reaproveitado nos PDFs
// de APR/PT/NC/Inspeção/Treinamento/DDS/EPI onde este usuário aprova/autoriza/assina.
export function MinhaAssinaturaDialog({ open, onClose }: MinhaAssinaturaDialogProps) {
  const [aba, setAba] = useState<'desenhar' | 'foto'>('desenhar');
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const padRef = useRef<SignaturePad | null>(null);
  const [existente, setExistente] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setErro(null);
    api.usuarios
      .obterMinhaAssinatura()
      .then((r) => setExistente(r ? api.usuarios.urlMinhaAssinaturaImagem() : null))
      .catch(() => setExistente(null));
  }, [open]);

  useEffect(() => {
    if (!open || aba !== 'desenhar' || !canvasRef.current) return;
    padRef.current = new SignaturePad(canvasRef.current, { backgroundColor: 'rgba(255,255,255,1)' });
    return () => {
      padRef.current?.off();
      padRef.current = null;
    };
  }, [open, aba]);

  function limpar() {
    padRef.current?.clear();
  }

  async function salvarDesenho() {
    if (!padRef.current || padRef.current.isEmpty()) {
      setErro('Desenhe sua assinatura antes de salvar.');
      return;
    }
    const dataUrl = padRef.current.toDataURL('image/png');
    const blob = await (await fetch(dataUrl)).blob();
    await salvar(blob);
  }

  async function salvarFoto(arquivo: File) {
    await salvar(arquivo);
  }

  async function salvar(imagem: Blob) {
    try {
      setSalvando(true);
      setErro(null);
      await api.usuarios.salvarMinhaAssinatura(imagem);
      onClose();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar a assinatura.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 480 }}>
        <DialogBody>
          <DialogTitle>Minha assinatura</DialogTitle>
          <DialogContent>
            <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
              Essa assinatura fica salva no seu perfil e passa a aparecer nos documentos que você
              aprovar ou assinar (APR, PT, Não Conformidade, Inspeção, DDS, EPI, Treinamento).
            </Text>

            {existente && (
              <div style={{ marginBottom: 12 }}>
                <Text size={200} weight="semibold" style={{ display: 'block', marginBottom: 4 }}>
                  Assinatura atual
                </Text>
                <img src={existente} alt="Assinatura salva" style={{ maxHeight: 60, border: '1px solid #ddd' }} />
              </div>
            )}

            <TabList selectedValue={aba} onTabSelect={(_, d) => setAba(d.value as 'desenhar' | 'foto')}>
              <Tab value="desenhar">Desenhar</Tab>
              <Tab value="foto">Usar uma foto</Tab>
            </TabList>

            {erro && <Text style={{ display: 'block', marginTop: 8, color: 'red' }}>{erro}</Text>}

            {aba === 'desenhar' ? (
              <div style={{ marginTop: 12 }}>
                <canvas
                  ref={canvasRef}
                  width={420}
                  height={160}
                  style={{ border: '1px solid #ccc', borderRadius: 4, width: '100%', touchAction: 'none' }}
                />
                <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
                  <Button appearance="secondary" onClick={limpar} disabled={salvando}>
                    Limpar
                  </Button>
                  <Button appearance="primary" onClick={salvarDesenho} disabled={salvando}>
                    Salvar assinatura
                  </Button>
                </div>
              </div>
            ) : (
              <div style={{ marginTop: 12 }}>
                <SeletorFotoCamera
                  rotulo="Tirar/enviar foto da assinatura"
                  tamanho="medium"
                  aparencia="primary"
                  modoCamera="environment"
                  desabilitado={salvando}
                  aoSelecionarArquivo={salvarFoto}
                  aoErroValidacao={setErro}
                />
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="subtle" onClick={onClose}>
              Fechar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
```

- [ ] **Step 4: Typecheck**

Run (dentro de `src/AAHBRANT.SST.TeamsApp`): `npx tsc -b --force`
Expected: sem erros. Se `signature_pad` não tiver tipos embutidos, instalar `npm install -D @types/signature_pad` e repetir.

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/components/assinatura/MinhaAssinaturaDialog.tsx src/AAHBRANT.SST.TeamsApp/src/lib/api.ts src/AAHBRANT.SST.TeamsApp/package.json src/AAHBRANT.SST.TeamsApp/package-lock.json
git commit -m "feat(assinatura): tela de cadastro da assinatura salva do usuário (desenho ou foto)"
```

---

### Task 4: Frontend — menu real no ícone de perfil

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx`

**Interfaces:**
- Consumes: `<MinhaAssinaturaDialog open onClose />` (Task 3).

- [ ] **Step 1: Trocar o botão estático por um `Menu`**

Em `src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx`, importar (junto aos demais imports de `@fluentui/react-components`):

```tsx
import { Menu, MenuTrigger, MenuPopover, MenuList, MenuItem } from '@fluentui/react-components';
```

Adicionar o import do novo diálogo:
```tsx
import { MinhaAssinaturaDialog } from '../components/assinatura/MinhaAssinaturaDialog';
```

Adicionar um estado no componente `AppShell` (junto aos demais `useState`):
```tsx
  const [minhaAssinaturaAberta, setMinhaAssinaturaAberta] = useState(false);
```

Trocar o bloco (linhas ~393-398):
```tsx
          <button className={estilos.usuarioChip} title={nomeUsuario}>
            <Text className={estilos.usuarioNome}>{nomeUsuario}</Text>
            <div className={estilos.usuarioAvatar} title="Foto de perfil (em breve)">
              <Person24Regular fontSize={17} />
            </div>
          </button>
```
por:
```tsx
          <Menu>
            <MenuTrigger disableButtonEnhancement>
              <button className={estilos.usuarioChip} title={nomeUsuario}>
                <Text className={estilos.usuarioNome}>{nomeUsuario}</Text>
                <div className={estilos.usuarioAvatar}>
                  <Person24Regular fontSize={17} />
                </div>
              </button>
            </MenuTrigger>
            <MenuPopover>
              <MenuList>
                <MenuItem onClick={() => setMinhaAssinaturaAberta(true)}>Minha assinatura</MenuItem>
              </MenuList>
            </MenuPopover>
          </Menu>
```

Adicionar, próximo ao fechamento do componente (ao lado de outros diálogos globais, se houver, ou logo antes do `</main>`/fechamento do `header`):
```tsx
      <MinhaAssinaturaDialog open={minhaAssinaturaAberta} onClose={() => setMinhaAssinaturaAberta(false)} />
```

- [ ] **Step 2: Typecheck**

Run: `npx tsc -b --force`
Expected: sem erros.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx
git commit -m "feat(assinatura): ícone de perfil vira menu com acesso a 'Minha assinatura'"
```

---

### Task 4b: Helper compartilhado de resolução de usuário por sessão

**Files:**
- Create: `src/AAHBRANT.SST.Application/Common/ResolucaoUsuarioSessao.cs`
- Create: `tests/AAHBRANT.SST.Application.Tests/Common/ResolucaoUsuarioSessaoTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.Usuarios`.
- Produces: `static Task<Guid> ResolucaoUsuarioSessao.ResolverIdOuFalhar(IAppDbContext db, string? azureAdObjectId, CancellationToken ct)` — lança `InvalidOperationException` com mensagem amigável se não resolver. Consumido pela Task 5 (6 comandos).

- [ ] **Step 1: Escrever o teste (falha — classe não existe)**

```csharp
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

public class ResolucaoUsuarioSessaoTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task ResolverIdOuFalhar_ComOidValido_RetornaIdDoUsuario()
    {
        var db = CriarDb(nameof(ResolverIdOuFalhar_ComOidValido_RetornaIdDoUsuario));
        var usuario = new Usuario { Email = "a@aahbrant.com", Nome = "A", AzureAdObjectId = "oid-1" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        var id = await ResolucaoUsuarioSessao.ResolverIdOuFalhar(db, "oid-1", CancellationToken.None);

        Assert.Equal(usuario.Id, id);
    }

    [Fact]
    public async Task ResolverIdOuFalhar_SemOid_LancaExcecao()
    {
        var db = CriarDb(nameof(ResolverIdOuFalhar_SemOid_LancaExcecao));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolucaoUsuarioSessao.ResolverIdOuFalhar(db, null, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ResolucaoUsuarioSessaoTests`
Expected: FAIL (classe não existe).

- [ ] **Step 3: Implementar**

Criar `src/AAHBRANT.SST.Application/Common/ResolucaoUsuarioSessao.cs`:

```csharp
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common;

// Resolve o Usuario.Id da sessão autenticada atual a partir do AzureAdObjectId (claim "oid") — mesmo
// padrão já usado por RegistrarAssinaturaSessaoLogadaCommand, extraído aqui porque os comandos de
// Aprovar/Autorizar/Suspender/Revalidar/Encerrar de Apr/PT/NaoConformidade repetem exatamente esta
// resolução (ver docs/superpowers/specs/2026-09-10-assinatura-salva-usuario-design.md §6/§7).
public static class ResolucaoUsuarioSessao
{
    public static async Task<Guid> ResolverIdOuFalhar(IAppDbContext db, string? azureAdObjectId, CancellationToken ct)
    {
        var usuarioId = string.IsNullOrEmpty(azureAdObjectId)
            ? null
            : await db.Usuarios.Where(u => u.AzureAdObjectId == azureAdObjectId).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);

        return usuarioId ?? throw new InvalidOperationException(
            "Não foi possível identificar o seu usuário para registrar a assinatura. Faça login novamente.");
    }
}
```

- [ ] **Step 4: Rodar e confirmar sucesso**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter ResolucaoUsuarioSessaoTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Common/ResolucaoUsuarioSessao.cs tests/AAHBRANT.SST.Application.Tests/Common/
git commit -m "feat(common): helper para resolver o Usuario da sessão logada a partir do claim oid"
```

---

### Task 5: Backend — Apr/PT/NC resolvem o usuário pela sessão (6 comandos)

**Files:**
- Modify: `src/AAHBRANT.SST.Application/Aprs/Commands/AprovarAprCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/PermissoesTrabalho/Commands/AutorizarPermissaoTrabalhoCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/PermissoesTrabalho/Commands/SuspenderPermissaoTrabalhoCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/PermissoesTrabalho/Commands/RevalidarPermissaoTrabalhoCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/PermissoesTrabalho/Commands/EncerrarPermissaoTrabalhoCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/NaoConformidades/Commands/EncerrarNaoConformidadeCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/AprsController.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/PermissoesTrabalhoController.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/NaoConformidadesController.cs`
- Modify (testes existentes que criam esses commands com um Guid solto — localizar com `grep -rl "new AprovarAprCommand\|new AutorizarPermissaoTrabalhoCommand\|new SuspenderPermissaoTrabalhoCommand\|new RevalidarPermissaoTrabalhoCommand\|new EncerrarPermissaoTrabalhoCommand\|new EncerrarNaoConformidadeCommand" tests/`)

**Interfaces:**
- Consumes: `ResolucaoUsuarioSessao.ResolverIdOuFalhar` (Task 4b).
- Produces: os 6 records passam a receber `string? AzureAdObjectId` no lugar do `Guid` de usuário que existia.

Este é o mesmo diff, repetido 6 vezes — mesmo raciocínio em todos: o record troca o `Guid XUsuarioId` recebido do cliente por `string? AzureAdObjectId`; o handler resolve o `Guid` via `ResolucaoUsuarioSessao` antes de continuar com a regra de negócio já existente (inalterada); o controller para de ler o campo do corpo da requisição e passa a ler o claim `oid` da própria requisição, como `AssinaturaController.AssinarComSessaoLogada` já faz.

- [ ] **Step 1: `AprovarAprCommand`**

Substituir todo o conteúdo de `src/AAHBRANT.SST.Application/Aprs/Commands/AprovarAprCommand.cs`:

```csharp
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Commands;

// "Aprovação" (§17) — ação dedicada em vez de edição genérica de Status, para deixar o gate
// de liberação da atividade (§46) explícito e auditável (quem aprovou e quando). AprovadoPorUsuarioId
// deixou de vir do cliente (era um Guid digitado à mão na tela — nenhuma verificação de identidade,
// achado em 2026-09-10) e passa a ser resolvido a partir da própria sessão logada.
public record AprovarAprCommand(Guid Id, string? AzureAdObjectId) : IRequest;

public class AprovarAprCommandValidator : AbstractValidator<AprovarAprCommand>
{
    public AprovarAprCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class AprovarAprCommandHandler : IRequestHandler<AprovarAprCommand>
{
    private readonly IAppDbContext _db;

    public AprovarAprCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AprovarAprCommand request, CancellationToken ct)
    {
        var apr = await _db.Aprs.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"APR {request.Id} não encontrada.");

        var usuarioId = await ResolucaoUsuarioSessao.ResolverIdOuFalhar(_db, request.AzureAdObjectId, ct);

        apr.Status = StatusApr.Aprovada;
        apr.AprovadoPorUsuarioId = usuarioId;
        apr.DataAprovacao = DateTime.UtcNow;
        apr.MotivoReprovacao = null;

        await _db.SaveChangesAsync(ct);
    }
}
```

Em `src/AAHBRANT.SST.Api/Controllers/AprsController.cs`, adicionar `using System.Security.Claims;` no topo e trocar:
```csharp
    [Authorize(Policy = "apr:aprovar")]
    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id, AprovarAprRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AprovarAprCommand(id, body.AprovadoPorUsuarioId), ct);
        return NoContent();
    }
```
por:
```csharp
    [Authorize(Policy = "apr:aprovar")]
    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await _mediator.Send(new AprovarAprCommand(id, azureAdObjectId), ct);
        return NoContent();
    }
```
E remover a linha `public record AprovarAprRequestBody(Guid AprovadoPorUsuarioId);` no final do arquivo (não é mais usada).

- [ ] **Step 2: `AutorizarPermissaoTrabalhoCommand`**

No mesmo arquivo, trocar a assinatura do record:
```csharp
public record AutorizarPermissaoTrabalhoCommand(Guid Id, Guid AutorizadoPorUsuarioId, Guid? ResponsavelSstUsuarioId) : IRequest;
```
por:
```csharp
public record AutorizarPermissaoTrabalhoCommand(Guid Id, string? AzureAdObjectId, Guid? ResponsavelSstUsuarioId) : IRequest;
```
(`ResponsavelSstUsuarioId` continua sendo escolhido manualmente — é um terceiro papel opcional, não quem está clicando o botão; fica fora desta correção).

No validator, remover `RuleFor(x => x.AutorizadoPorUsuarioId).NotEmpty();`.

No handler, trocar:
```csharp
        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.AutorizadoPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.AutorizadoPorUsuarioId} não encontrado.");
```
por:
```csharp
        var autorizadoPorUsuarioId = await ResolucaoUsuarioSessao.ResolverIdOuFalhar(_db, request.AzureAdObjectId, ct);
```
E trocar, mais abaixo:
```csharp
        pt.AutorizadoPorUsuarioId = request.AutorizadoPorUsuarioId;
```
por:
```csharp
        pt.AutorizadoPorUsuarioId = autorizadoPorUsuarioId;
```
Adicionar `using AAHBRANT.SST.Application.Common;` no topo do arquivo.

- [ ] **Step 3: `SuspenderPermissaoTrabalhoCommand`**

Trocar:
```csharp
public record SuspenderPermissaoTrabalhoCommand(Guid Id, string Motivo, Guid SuspensaPorUsuarioId) : IRequest;
```
por:
```csharp
public record SuspenderPermissaoTrabalhoCommand(Guid Id, string Motivo, string? AzureAdObjectId) : IRequest;
```
Remover a validação `NotEmpty()` de `SuspensaPorUsuarioId` (troca por nada — `AzureAdObjectId` é resolvido, não validado como "não vazio" no FluentValidation, já que a falha de resolução já lança exceção própria). No handler, mesmo padrão do Step 2: trocar o `_db.Usuarios.AnyAsync(...)` por `ResolucaoUsuarioSessao.ResolverIdOuFalhar`, e usar o `Guid` resolvido ao atribuir `pt.SuspensaPorUsuarioId`. Adicionar o `using AAHBRANT.SST.Application.Common;`.

- [ ] **Step 4: `RevalidarPermissaoTrabalhoCommand`**

Mesmo padrão: `Guid RevalidadaPorUsuarioId` → `string? AzureAdObjectId`; handler resolve via `ResolucaoUsuarioSessao` e atribui `pt.RevalidadaPorUsuarioId`.

- [ ] **Step 5: `EncerrarPermissaoTrabalhoCommand`**

Mesmo padrão: `Guid EncerradaPorUsuarioId` → `string? AzureAdObjectId`; handler resolve via `ResolucaoUsuarioSessao` e atribui `pt.EncerradaPorUsuarioId`.

- [ ] **Step 6: `PermissoesTrabalhoController` — os 4 endpoints**

Adicionar `using System.Security.Claims;` no topo. Para cada um dos 4 métodos (`Autorizar`, `Suspender`, `Revalidar`, `Encerrar`), trocar a leitura do `RequestBody` (que hoje inclui o Guid de usuário) para resolver o `oid` da requisição e passar `string? azureAdObjectId` no lugar. Exemplo para `Autorizar`:
```csharp
    [Authorize(Policy = "pt:autorizar")]
    [HttpPost("{id:guid}/autorizar")]
    public async Task<IActionResult> Autorizar(Guid id, AutorizarPermissaoTrabalhoRequestBody body, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await _mediator.Send(new AutorizarPermissaoTrabalhoCommand(id, azureAdObjectId, body.ResponsavelSstUsuarioId), ct);
        return NoContent();
    }
```
Repetir o mesmo raciocínio para `Suspender`/`Revalidar`/`Encerrar`, removendo do `RequestBody` de cada um o campo de usuário que não é mais necessário (mantendo os demais campos — `Motivo`, `NovaValidade`, `NovoHorarioFim`, `Observacoes`).

- [ ] **Step 7: `EncerrarNaoConformidadeCommand`**

Trocar:
```csharp
public record EncerrarNaoConformidadeCommand(
    Guid Id,
    Guid ValidadoPorUsuarioId,
    string? ObservacoesEncerramento) : IRequest;
```
por:
```csharp
public record EncerrarNaoConformidadeCommand(
    Guid Id,
    string? AzureAdObjectId,
    string? ObservacoesEncerramento) : IRequest;
```
Remover `RuleFor(x => x.ValidadoPorUsuarioId).NotEmpty();` do validator. No handler, trocar:
```csharp
        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.ValidadoPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.ValidadoPorUsuarioId} não encontrado.");
```
por:
```csharp
        var validadoPorUsuarioId = await ResolucaoUsuarioSessao.ResolverIdOuFalhar(_db, request.AzureAdObjectId, ct);
```
E trocar as duas atribuições `acao.ValidadoPorUsuarioId = request.ValidadoPorUsuarioId;` (dentro do `foreach`) e qualquer outra atribuição de `ValidadoPorUsuarioId`/`nc.ValidadoPorUsuarioId` mais abaixo no handler para usar a variável `validadoPorUsuarioId` resolvida. Adicionar `using AAHBRANT.SST.Application.Common;`.

Em `src/AAHBRANT.SST.Api/Controllers/NaoConformidadesController.cs`, mesmo tratamento do Step 6: ler `oid` da requisição, passar como `AzureAdObjectId`.

- [ ] **Step 8: Atualizar testes existentes que instanciam esses 6 commands**

Rodar:
```bash
grep -rl "new AprovarAprCommand\|new AutorizarPermissaoTrabalhoCommand\|new SuspenderPermissaoTrabalhoCommand\|new RevalidarPermissaoTrabalhoCommand\|new EncerrarPermissaoTrabalhoCommand\|new EncerrarNaoConformidadeCommand" tests/
```
Para cada teste encontrado, trocar o `Guid` de usuário passado por um `string` de `AzureAdObjectId` de teste (ex.: `"oid-teste"`), e garantir que o `Usuario` semeado no banco em memória daquele teste tenha `AzureAdObjectId = "oid-teste"` (em vez de só existir com um `Id` qualquer usado antes como `AprovadoPorUsuarioId`).

- [ ] **Step 9: Build e testes**

Run: `dotnet build SST-APP.sln --nologo -v quiet && dotnet test SST-APP.sln --nologo -v quiet 2>&1 | tail -20`
Expected: build limpo, 100% dos testes passando (nenhum a menos que os removidos por referenciar Telegram — este plano não mexe em Telegram).

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "fix(apr,pt,naoconformidade): aprovação/autorização/encerramento resolvem o usuário pela sessão logada, não por Guid enviado pelo cliente"
```

---

### Task 6: Frontend — Apr/PT/NC: troca inputs de usuário por ação direta

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/apr/AprDetalhePage.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/pt/PermissaoTrabalhoDetalhePage.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/naoconformidades/NaoConformidadeDetalhePage.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`

**Interfaces:**
- Consumes: os endpoints ajustados na Task 5 (não recebem mais o campo de usuário no corpo).

- [ ] **Step 1: Ajustar `api.ts`**

Trocar as 5 assinaturas (linhas já localizadas nesta sessão):
```ts
    aprovar: (id: string, aprovadoPorUsuarioId: string) =>
      request<void>(`/api/aprs/${id}/aprovar`, { method: 'POST', body: JSON.stringify({ aprovadoPorUsuarioId }) }),
```
por:
```ts
    aprovar: (id: string) => request<void>(`/api/aprs/${id}/aprovar`, { method: 'POST' }),
```

```ts
    autorizar: (id: string, autorizadoPorUsuarioId: string, responsavelSstUsuarioId?: string | null) =>
      request<void>(`/api/permissoestrabalho/${id}/autorizar`, {
        method: 'POST',
        body: JSON.stringify({ autorizadoPorUsuarioId, responsavelSstUsuarioId }),
      }),
```
por:
```ts
    autorizar: (id: string, responsavelSstUsuarioId?: string | null) =>
      request<void>(`/api/permissoestrabalho/${id}/autorizar`, {
        method: 'POST',
        body: JSON.stringify({ responsavelSstUsuarioId }),
      }),
```

```ts
    suspender: (id: string, motivo: string, suspensaPorUsuarioId: string) =>
      request<void>(`/api/permissoestrabalho/${id}/suspender`, {
        method: 'POST',
        body: JSON.stringify({ motivo, suspensaPorUsuarioId }),
      }),
```
por:
```ts
    suspender: (id: string, motivo: string) =>
      request<void>(`/api/permissoestrabalho/${id}/suspender`, { method: 'POST', body: JSON.stringify({ motivo }) }),
```

```ts
    revalidar: (id: string, novaValidade: string, novoHorarioFim: string | null, revalidadaPorUsuarioId: string) =>
      request<void>(`/api/permissoestrabalho/${id}/revalidar`, {
        method: 'POST',
        body: JSON.stringify({ novaValidade, novoHorarioFim, revalidadaPorUsuarioId }),
      }),
```
por:
```ts
    revalidar: (id: string, novaValidade: string, novoHorarioFim: string | null) =>
      request<void>(`/api/permissoestrabalho/${id}/revalidar`, {
        method: 'POST',
        body: JSON.stringify({ novaValidade, novoHorarioFim }),
      }),
```

```ts
    encerrar: (id: string, encerradaPorUsuarioId: string, observacoes?: string | null) =>
      request<void>(`/api/permissoestrabalho/${id}/encerrar`, {
        method: 'POST',
        body: JSON.stringify({ encerradaPorUsuarioId, observacoes }),
      }),
```
por:
```ts
    encerrar: (id: string, observacoes?: string | null) =>
      request<void>(`/api/permissoestrabalho/${id}/encerrar`, { method: 'POST', body: JSON.stringify({ observacoes }) }),
```

E, no bloco de `naoConformidades`:
```ts
    encerrar: (id: string, validadoPorUsuarioId: string, observacoesEncerramento?: string | null) =>
      request<void>(`/api/naoconformidades/${id}/encerrar`, {
        method: 'POST',
        body: JSON.stringify({ validadoPorUsuarioId, observacoesEncerramento }),
      }),
```
por:
```ts
    encerrar: (id: string, observacoesEncerramento?: string | null) =>
      request<void>(`/api/naoconformidades/${id}/encerrar`, {
        method: 'POST',
        body: JSON.stringify({ observacoesEncerramento }),
      }),
```

- [ ] **Step 2: `AprDetalhePage.tsx`**

Remover o estado `aprovadoPorUsuarioId` e o campo:
```tsx
        <Field label="ID do usuário aprovador (GUID)" required>
          <Input value={aprovadoPorUsuarioId} onChange={(_, d) => setAprovadoPorUsuarioId(d.value)} />
        </Field>
```
A função `aprovar` (buscar com `grep -n "function aprovar" src/AAHBRANT.SST.TeamsApp/src/pages/apr/AprDetalhePage.tsx`) deixa de checar/enviar `aprovadoPorUsuarioId` e passa a chamar só `api.aprs.aprovar(id)`. Remover também `formulario: (...)` do item correspondente na lista de ações (já que não há mais nada pra preencher antes de aprovar — o botão de "Aprovar" passa a executar direto, sem abrir formulário).

- [ ] **Step 3: `PermissaoTrabalhoDetalhePage.tsx`**

Mesmo tratamento para as 4 ações:
- Remover estados/inputs de `autorizadoPorUsuarioId`, `suspensaPorUsuarioId`, `revalidadaPorUsuarioId`, `encerradaPorUsuarioId` (`responsavelSstUsuarioId` continua existindo — é escolha manual de um terceiro papel opcional, não quem clica).
- Atualizar as chamadas (linhas já localizadas): `api.permissoesTrabalho.autorizar(id, responsavelSstUsuarioId || null)`, `.suspender(id, motivoSuspensao)`, `.revalidar(id, novaValidade, null)`, `.encerrar(id, observacoesEncerramento || null)`.
- Remover o `<Input value={autorizadoPorUsuarioId} .../>` (linha ~193) e os equivalentes de suspensão/revalidação/encerramento.

- [ ] **Step 4: `NaoConformidadeDetalhePage.tsx`**

Remover o estado `usuarioValidador` e os dois `<Dropdown ... valor={usuarioValidador} aoMudar={setUsuarioValidador} />` (linhas ~330 e ~438) usados para "encerrar". **Atenção:** `usuarioValidador` também é usado em `validarAcao`/`api.acoesPlano.validar(acaoId, usuarioValidador)` (linha ~230) — esse uso é de um comando diferente (`ValidarAcaoPlanoCommand`, compartilhado com Acidente) que está **fora de escopo deste plano** (ver seção final). Manter esse dropdown funcionando exatamente como está para `validarAcao`; remover só a exigência de `usuarioValidador` na função `encerrar` e trocar a chamada para `api.naoConformidades.encerrar(id, observacoesEncerramento || null)`.

- [ ] **Step 5: Typecheck**

Run: `npx tsc -b --force`
Expected: sem erros.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/apr/AprDetalhePage.tsx src/AAHBRANT.SST.TeamsApp/src/pages/pt/PermissaoTrabalhoDetalhePage.tsx src/AAHBRANT.SST.TeamsApp/src/pages/naoconformidades/NaoConformidadeDetalhePage.tsx src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat(apr,pt,naoconformidade): remove seleção manual de usuário aprovador — a sessão logada já basta"
```

---

### Task 7: PDF — desenhar a assinatura salva nos blocos de Apr/PT

**Files:**
- Modify: `src/AAHBRANT.SST.Application/PermissoesTrabalho/IPtPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/PermissoesTrabalho/Queries/ExportarPermissaoTrabalhoPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/PtPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Aprs/IAprPdfService.cs`
- Modify: `src/AAHBRANT.SST.Application/Aprs/Queries/ExportarAprPdfQuery.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Documentos/AprPdfService.cs`
**Achado do self-review deste plano:** Não Conformidade **não tem exportação de PDF nenhuma hoje** — confirmado com `find src -iname "*NaoConformidade*Pdf*"` (nenhum resultado) e `grep "pdf" NaoConformidadesController.cs` (nenhum endpoint). O requisito da spec ("aparece em PDF") não se aplica a NC até existir um PDF pra ela — criar esse export do zero é uma feature maior e separada, fora deste plano. A correção de identidade de `EncerrarNaoConformidadeCommand` (Task 5) segue de pé — vale por si só (era um Guid sem verificação), só a parte de "desenhar no PDF" de NC fica de fora aqui.

**Interfaces:**
- Consumes: `Usuario.AssinaturaImagemConteudo` (Task 1).

- [ ] **Step 1: PT — `PtPdfAssinatura` ganha a imagem**

Em `src/AAHBRANT.SST.Application/PermissoesTrabalho/IPtPdfService.cs`, trocar:
```csharp
public record PtPdfAssinatura(string? Nome, DateTime? Data);
```
por:
```csharp
public record PtPdfAssinatura(string? Nome, DateTime? Data, byte[]? ImagemAssinatura);
```

Em `ExportarPermissaoTrabalhoPdfQuery.cs`, antes de montar `PtPdfModelo`, buscar as imagens dos usuários envolvidos:
```csharp
        var usuarioIdsEnvolvidos = new[] { pt.AutorizadoPorUsuarioId, pt.ResponsavelExecucaoUsuarioId, pt.ResponsavelSstUsuarioId }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var assinaturasPorUsuario = await _db.Usuarios
            .Where(u => usuarioIdsEnvolvidos.Contains(u.Id) && u.AssinaturaImagemConteudo != null)
            .ToDictionaryAsync(u => u.Id, u => u.AssinaturaImagemConteudo!, ct);
```
E trocar as 3 construções de `PtPdfAssinatura` (linhas já localizadas):
```csharp
            new PtPdfAssinatura(pt.AutorizadoPorUsuarioNome, pt.DataAutorizacao),
            new PtPdfAssinatura(pt.ResponsavelExecucaoUsuarioNome, pt.DataAssinaturaExecucao),
            pt.ResponsavelSstUsuarioId.HasValue ? new PtPdfAssinatura(pt.ResponsavelSstUsuarioNome, pt.DataAssinaturaSst) : null,
```
por:
```csharp
            new PtPdfAssinatura(pt.AutorizadoPorUsuarioNome, pt.DataAutorizacao, pt.AutorizadoPorUsuarioId.HasValue ? assinaturasPorUsuario.GetValueOrDefault(pt.AutorizadoPorUsuarioId.Value) : null),
            new PtPdfAssinatura(pt.ResponsavelExecucaoUsuarioNome, pt.DataAssinaturaExecucao, pt.ResponsavelExecucaoUsuarioId.HasValue ? assinaturasPorUsuario.GetValueOrDefault(pt.ResponsavelExecucaoUsuarioId.Value) : null),
            pt.ResponsavelSstUsuarioId.HasValue ? new PtPdfAssinatura(pt.ResponsavelSstUsuarioNome, pt.DataAssinaturaSst, assinaturasPorUsuario.GetValueOrDefault(pt.ResponsavelSstUsuarioId.Value)) : null,
```

Em `src/AAHBRANT.SST.Infrastructure/Documentos/PtPdfService.cs`, trocar `BlocoAssinatura`:
```csharp
    private static void BlocoAssinatura(IContainer container, string titulo, PtPdfAssinatura assinatura)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(coluna =>
        {
            coluna.Item().Text(titulo).Bold().FontSize(7.5f);
            coluna.Item().PaddingTop(4).Text($"Nome: {assinatura.Nome ?? "____________________________"}");
            coluna.Item().Text("Assinatura: ______________________");
            coluna.Item().Text($"Data: {(assinatura.Data.HasValue ? assinatura.Data.Value.ToString("dd/MM/yyyy HH:mm") : "____/____/______")}");
        });
    }
```
por:
```csharp
    private static void BlocoAssinatura(IContainer container, string titulo, PtPdfAssinatura assinatura)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(coluna =>
        {
            coluna.Item().Text(titulo).Bold().FontSize(7.5f);
            coluna.Item().PaddingTop(4).Text($"Nome: {assinatura.Nome ?? "____________________________"}");
            if (assinatura.ImagemAssinatura is not null)
                coluna.Item().PaddingTop(2).Height(40).Image(assinatura.ImagemAssinatura).FitArea();
            else
                coluna.Item().Text("Assinatura: ______________________");
            coluna.Item().Text($"Data: {(assinatura.Data.HasValue ? assinatura.Data.Value.ToString("dd/MM/yyyy HH:mm") : "____/____/______")}");
        });
    }
```

- [ ] **Step 2: APR — novo bloco "Aprovação" no PDF**

Em `src/AAHBRANT.SST.Application/Aprs/IAprPdfService.cs`, adicionar ao `AprPdfModelo` (localizar a assinatura completa do record com `grep -n "record AprPdfModelo" -A 20`) um novo campo `AprPdfAssinatura? Aprovacao` (o record `AprPdfAssinatura(string? Nome, string? Funcao, DateTime? Data)` já existe — reaproveitar, adicionando o mesmo `byte[]? ImagemAssinatura` do Step 1 em vez de duplicar o tipo).

Em `ExportarAprPdfQuery.cs`, buscar a imagem do `apr.AprovadoPorUsuarioId` (mesmo padrão do Step 1) e montar `new AprPdfAssinatura(detalhe.Apr.AprovadoPorUsuarioNome, null, detalhe.Apr.DataAprovacao, imagem)` como o novo campo `Aprovacao` do modelo, só quando `apr.Status == StatusApr.Aprovada`.

Em `src/AAHBRANT.SST.Infrastructure/Documentos/AprPdfService.cs`, adicionar uma seção nova (mesmo padrão visual de `SecaoAssinaturasLiberacao` da PT, adaptado): título "APROVAÇÃO", renderizada só quando `modelo.Aprovacao is not null`, com `BlocoAssinatura` reaproveitado (extrair para uma classe utilitária compartilhada se `PtPdfService`/`AprPdfService` já tiverem uma base comum de blocos — confirmar com `grep -n "class.*PdfServiceBase\|static class.*PdfHelpers"` antes de duplicar o método).

- [ ] **Step 3: Build e testes**

Run: `dotnet build SST-APP.sln --nologo -v quiet && dotnet test SST-APP.sln --nologo -v quiet 2>&1 | tail -15`
Expected: build limpo, testes passando. Se algum teste de PDF existente quebrar por causa do novo parâmetro em `PtPdfAssinatura`/`AprPdfAssinatura` (construtor com mais um campo posicional), ajustar as chamadas do teste para passar `null` explícito no novo campo.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(pdf): desenha a assinatura salva do usuário nos blocos de aprovação/autorização de APR e PT"
```

---

### Task 8: `DocumentoSignatario` ganha `UsuarioId` + imagem no comprovante genérico

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Assinatura/DocumentoAssinatura.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AssinaturaConfiguracoes.cs`
- Modify: `src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarAssinaturaSessaoLogadaCommand.cs`
- Modify: `src/AAHBRANT.SST.Application/Assinatura/IRegistradorAssinaturaService.cs`
- Modify: `src/AAHBRANT.SST.Application/Assinatura/Queries/ObterDocumentoQuery.cs` (ou onde `DocumentoAssinaturaPdfModelo` é montado)
- Modify: `src/AAHBRANT.SST.Infrastructure/Assinatura/DocumentoAssinaturaPdfService.cs`

**Interfaces:**
- Produces: `DocumentoSignatario.UsuarioId` (`Guid?`, novo — populado só quando `MetodoAutenticacao == SessaoLogada`).

- [ ] **Step 1: Adicionar `UsuarioId` em `DocumentoSignatario`**

Em `src/AAHBRANT.SST.Domain/Entidades/Assinatura/DocumentoAssinatura.cs`, dentro de `DocumentoSignatario`, adicionar (logo após `public string? IpAddress { get; set; }`):
```csharp
    // Preenchido só quando MetodoAutenticacao == SessaoLogada — DocumentoSignatario historicamente
    // só grava TrabalhadorId (biometria/facial não têm conceito de "login"). Sem este campo,
    // reconstruir "qual conta assinou" a partir só do TrabalhadorId seria ambíguo se um dia mais de
    // um Usuario apontar para o mesmo Trabalhador. Usado só para buscar a imagem da assinatura
    // salva (Usuario.AssinaturaImagemConteudo) na hora de gerar o PDF — não muda nada da trilha
    // jurídica/de auditoria, que continua idêntica.
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
```

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AssinaturaConfiguracoes.cs`, na config de `DocumentoSignatarioConfiguracao`, adicionar:
```csharp
        builder.HasOne(s => s.Usuario).WithMany()
            .HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 2: Popular na assinatura por sessão logada**

Em `IRegistradorAssinaturaService.cs`, ajustar `ResultadoAutenticacaoAssinatura` para carregar o `UsuarioId` opcional:
```csharp
public record ResultadoAutenticacaoAssinatura(Guid TrabalhadorId, MetodoAutenticacaoAssinatura Metodo, Guid? UsuarioId = null);
```
E, dentro de `RegistradorAssinaturaService.RegistrarAsync`, ao montar `signatario`, adicionar `UsuarioId = resultado.UsuarioId`.

Em `RegistrarAssinaturaSessaoLogadaCommand.cs`, trocar:
```csharp
        var resultado = new ResultadoAutenticacaoAssinatura(usuario.TrabalhadorId.Value, MetodoAutenticacaoAssinatura.SessaoLogada);
```
por:
```csharp
        var resultado = new ResultadoAutenticacaoAssinatura(usuario.TrabalhadorId.Value, MetodoAutenticacaoAssinatura.SessaoLogada, usuario.Id);
```

- [ ] **Step 3: Gerar a migration**

Run:
```bash
dotnet ef migrations add AdicionarUsuarioIdEmDocumentoSignatario --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api
```
Expected: `AddColumn` para `UsuarioId` em `DocumentoSignatarios` + a FK correspondente. Conferir que não altera/remove nenhuma coluna existente (tabela sensível — auditoria jurídica).

- [ ] **Step 4: Desenhar a imagem no comprovante genérico**

Em `DocumentoAssinaturaPdfModelo`/consulta que monta os dados do comprovante (localizar com `grep -rn "class DocumentoAssinaturaPdfModelo\|record.*DocumentoAssinaturaPdfModelo"`), incluir a imagem do usuário (buscando `Usuario.AssinaturaImagemConteudo` pelos `UsuarioId` dos signatários que tiverem).

Em `src/AAHBRANT.SST.Infrastructure/Assinatura/DocumentoAssinaturaPdfService.cs`, trocar:
```csharp
                    foreach (var signatario in modelo.Signatarios)
                    {
                        coluna.Item().Text(t =>
                        {
                            t.Span($"• {signatario.TrabalhadorNome} — ").SemiBold();
                            t.Span($"{DescreverMetodo(signatario.Metodo)}, em {signatario.AssinadoEm:dd/MM/yyyy HH:mm}");
                        });
                    }
```
por:
```csharp
                    foreach (var signatario in modelo.Signatarios)
                    {
                        coluna.Item().Row(linha =>
                        {
                            linha.RelativeItem().Text(t =>
                            {
                                t.Span($"• {signatario.TrabalhadorNome} — ").SemiBold();
                                t.Span($"{DescreverMetodo(signatario.Metodo)}, em {signatario.AssinadoEm:dd/MM/yyyy HH:mm}");
                            });
                            if (signatario.ImagemAssinatura is not null)
                                linha.ConstantItem(80).Height(24).Image(signatario.ImagemAssinatura).FitArea();
                        });
                    }
```
(adicionar `ImagemAssinatura` — `byte[]?` — ao DTO de signatário usado por este modelo, seguindo o mesmo padrão do Step 1 da Task 7).

- [ ] **Step 5: Build e testes**

Run: `dotnet build SST-APP.sln --nologo -v quiet && dotnet test SST-APP.sln --nologo -v quiet 2>&1 | tail -15`
Expected: build limpo, testes passando.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(assinatura): DocumentoSignatario grava qual Usuario assinou por sessão logada e desenha a assinatura salva no comprovante"
```

---

### Task 9: Deploy

**Files:** nenhum.

- [ ] **Step 1: Push e PR**

```bash
git push -u origin <nome-da-branch>
gh pr create --base master --title "feat(assinatura): assinatura salva do usuário + correção de identidade em APR/PT/NC" --body "Implementa docs/superpowers/specs/2026-09-10-assinatura-salva-usuario-design.md: usuário cadastra assinatura (desenho/foto) no ícone de perfil; APR/PT/Não Conformidade param de aceitar Guid de usuário aprovador vindo do cliente (achado de segurança: eram campos sem verificação de identidade) e passam a resolver pela sessão logada; PDFs de APR/PT e o comprovante genérico do Motor de Assinatura passam a desenhar a assinatura salva (Não Conformidade não tem PDF hoje — fica para quando esse export existir)."
```

- [ ] **Step 2: Aguardar aprovação e merge**

O merge dispara o deploy automático — as duas migrations (Task 1 e Task 8) rodam no startup da API.

- [ ] **Step 3: Validar em homologação**

Cadastrar uma assinatura no ícone de perfil, aprovar uma APR de teste, e conferir que o PDF exportado mostra a imagem no bloco de aprovação.

---

## Fora de escopo (achados durante o planejamento, não entram neste plano)

- **`ValidarAcaoPlanoCommand`** (usado por Não Conformidade *e* Acidente para validar itens do plano de ação) tem o mesmo formato de Guid vindo do cliente que os 6 comandos corrigidos aqui — mas não estava na lista original do usuário (APR, PT, Inspeções, NC) e tem raio de impacto maior por ser compartilhado com Acidente. Candidato a um plano futuro, seguindo exatamente o mesmo padrão desta Task 5/4b.
- **PDF de Não Conformidade não existe hoje** (achado do self-review deste plano — não há endpoint `/pdf` nem serviço de PDF pra NC). A correção de identidade do `EncerrarNaoConformidadeCommand` (Task 5) entra normalmente; "aparecer no PDF" fica pendente até esse export ser criado, feature separada.
- Assinatura para trabalhadores de campo sem login (Futronic/reconhecimento facial) — fora de escopo desde a spec original.
