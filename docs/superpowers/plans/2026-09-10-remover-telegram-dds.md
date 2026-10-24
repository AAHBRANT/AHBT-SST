# Remoção do Telegram no DDS — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remover por completo a integração de Telegram do DDS (envio de PDF + confirmação de ciência) e o código morto de crachá+PIN.

**Architecture:** Remoção pura — nenhum substituto entra no lugar. `ITelegramService` é usado hoje só por `EnviarDdsTelegramCommand` (DDS) e `GerarVinculoTelegramCommand` (vínculo do trabalhador, não usado por nenhuma tela hoje); nenhum outro módulo do sistema depende disso (`AlertaEngineWorker` e os processadores de Service Bus/Teams são independentes). Remove de fora para dentro: frontend → controllers → commands → hosted services → schema (migration).

**Tech Stack:** .NET 8 / EF Core 8 / xUnit (backend), React + TypeScript + Vite (frontend).

**Spec:** `docs/superpowers/specs/2026-09-10-assinatura-salva-usuario-design.md` (seções 10 e 11)

## Global Constraints

- Nenhuma migration deve ser escrita à mão — sempre gerar via `dotnet ef migrations add <Nome> --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api`.
- Build da solução (`dotnet build SST-APP.sln`) e `npx tsc -b --force` (dentro de `src/AAHBRANT.SST.TeamsApp`) devem ficar limpos (0 erros) ao final de cada task que toque os respectivos projetos.
- Commits pequenos e frequentes, um por task.

---

### Task 1: Corrigir comentário desatualizado sobre crachá+PIN

**Files:**
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Obra.cs:28-31`

**Interfaces:** nenhuma (só comentário).

- [ ] **Step 1: Atualizar o comentário**

Trocar:
```csharp
    // Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §2/§3) — cada obra decide
    // quais métodos aceita; ex.: obra sem leitor biométrico ainda comprado opera só com CrachaPin
    // até o hardware chegar. Default Nenhum: uma obra só passa a assinar depois de configurada
    // explicitamente, nunca por omissão.
```
por:
```csharp
    // Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §2/§3) — cada obra decide
    // quais métodos aceita (Biometria via Futronic, ReconhecimentoFacial via Azure Face API).
    // CrachaPin/QrCodePin/WebAuthnCelular foram removidos do sistema em 31/08. Default Nenhum: uma
    // obra só passa a assinar depois de configurada explicitamente, nunca por omissão.
```

- [ ] **Step 2: Build**

Run: `dotnet build SST-APP.sln --nologo -v quiet`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Obra.cs
git commit -m "docs: corrige comentário desatualizado sobre crachá+PIN em Obra.cs"
```

---

### Task 2: Remover o Telegram do frontend do DDS

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/dds/DdsDetalhePage.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`

**Interfaces:** nenhuma nova — só remoção.

- [ ] **Step 1: Remover do `DdsDetalhePage.tsx`**

Remover (usando os trechos exatos já localizados nesta sessão):
- Funções `tomTelegram` e `rotuloTelegram` (linhas ~50-60).
- Estados `enviandoTelegram`/`resultadoTelegram` (linha ~93-94).
- Função `enviarTelegram` inteira (linhas ~257-273).
- A coluna `{ chave: 'telegram', rotulo: 'Telegram', render: ... }` da tabela de participantes (linhas ~328-332).
- O botão:
```tsx
            <Button icon={<Send24Regular />} onClick={enviarTelegram} disabled={enviandoTelegram}>
              Enviar via Telegram
            </Button>
```
- A linha `{resultadoTelegram && <FeedbackInline tom="sucesso">{resultadoTelegram}</FeedbackInline>}`.
- O import de `Send24Regular` de `@fluentui/react-icons`, se não for mais usado em nenhum outro lugar do arquivo (confirmar com uma busca antes de remover o import).
- No comentário do topo do arquivo (linhas ~74-77), remover as menções a "telegram" na descrição do cabeçalho/ações utilitárias.

- [ ] **Step 2: Remover de `api.ts`**

Remover o método `enviarTelegram` do objeto `api.dds` (ex.: `enviarTelegram: (id: string) => request<...>(...)`), e os métodos/tipos relacionados a vínculo de Telegram do trabalhador (`gerarVinculoTelegram` ou nome equivalente em `api.trabalhadores`), e os campos `telegramVinculado`/`telegramCodigoVinculo`/`telegramEnviadoEm`/`telegramConfirmadoEm` das interfaces `TrabalhadorDto`/`DdsParticipante` (o que existir).

Localizar exatamente com:
```
grep -n "Telegram" src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
```
e remover cada ocorrência restante depois desta task.

- [ ] **Step 3: Typecheck**

Run (dentro de `src/AAHBRANT.SST.TeamsApp`): `npx tsc -b --force`
Expected: sem erros. Se aparecer erro de import não usado ou propriedade inexistente, é sinal de uma referência a Telegram que ficou para trás — localizar e remover.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/dds/DdsDetalhePage.tsx src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat(dds): remove envio e confirmação de ciência via Telegram (frontend)"
```

---

### Task 3: Remover os commands e o controller de Telegram

**Files:**
- Delete: `src/AAHBRANT.SST.Application/Dds/Commands/EnviarDdsTelegramCommand.cs`
- Delete: `src/AAHBRANT.SST.Application/Trabalhadores/Commands/GerarVinculoTelegramCommand.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/DdsController.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs` (se expuser a rota de vínculo)

**Interfaces:** nenhuma nova — só remoção. Antes de apagar os dois arquivos, localizar os endpoints que os disparam:

```
grep -n "EnviarDdsTelegram\|GerarVinculoTelegram" src/AAHBRANT.SST.Api/Controllers/*.cs
```

- [ ] **Step 1: Remover o endpoint de envio no `DdsController.cs`**

Localizar e remover o `[HttpPost(".../telegram")]` (ou nome equivalente) que envia `EnviarDdsTelegramCommand`, junto com qualquer `RequestBody` record associado só usado por ele.

- [ ] **Step 2: Remover o endpoint de vínculo (se existir) no `TrabalhadoresController.cs`**

Mesmo procedimento para o endpoint que dispara `GerarVinculoTelegramCommand`.

- [ ] **Step 3: Apagar os dois arquivos de comando**

```bash
git rm src/AAHBRANT.SST.Application/Dds/Commands/EnviarDdsTelegramCommand.cs
git rm src/AAHBRANT.SST.Application/Trabalhadores/Commands/GerarVinculoTelegramCommand.cs
```

- [ ] **Step 4: Build (vai falhar até a Task 4 remover `ITelegramService`/`DdsTelegramEnvio` — isso é esperado)**

Run: `dotnet build SST-APP.sln --nologo -v quiet 2>&1 | tail -30`
Expected: erros de compilação apontando para `ITelegramService`, `DdsTelegramEnvio`, `TelegramChatId` — confirma que a próxima task precisa remover essas peças. Não commitar ainda; seguir direto para a Task 4 antes de compilar de novo.

---

### Task 4: Remover a infraestrutura de Telegram (hosted services, DI, entidade)

**Files:**
- Delete: `src/AAHBRANT.SST.Infrastructure/Integracao/TelegramBotService.cs`
- Delete: `src/AAHBRANT.SST.Infrastructure/Integracao/TelegramUpdatesPollingService.cs`
- Delete: `src/AAHBRANT.SST.Infrastructure/Integracao/TelegramOptions.cs`
- Delete: `src/AAHBRANT.SST.Application/Common/Interfaces/ITelegramService.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`
- Modify: `src/AAHBRANT.SST.Api/Program.cs`
- Modify: `src/AAHBRANT.SST.Worker/Program.cs`
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Dds/Dds.cs` (remove a coleção/referência a `DdsTelegramEnvio`, se houver)
- Delete: a classe `DdsTelegramEnvio` (localizar o arquivo com `grep -rn "class DdsTelegramEnvio" src/AAHBRANT.SST.Domain`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/DdsConfiguracoes.cs` (remove a config de `DdsTelegramEnvio`)
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs:95` (remove `DbSet<DdsTelegramEnvio> DdsTelegramEnvios`)
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs` (remove o `DbSet` correspondente)
- Modify: `src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs` (remove `TelegramChatId`/`TelegramCodigoVinculo`)

**Interfaces:** nenhuma — só remoção.

- [ ] **Step 1: Apagar os 4 arquivos de infraestrutura/interface**

```bash
git rm src/AAHBRANT.SST.Infrastructure/Integracao/TelegramBotService.cs
git rm src/AAHBRANT.SST.Infrastructure/Integracao/TelegramUpdatesPollingService.cs
git rm src/AAHBRANT.SST.Infrastructure/Integracao/TelegramOptions.cs
git rm src/AAHBRANT.SST.Application/Common/Interfaces/ITelegramService.cs
```

- [ ] **Step 2: Limpar `DependencyInjection.cs`**

Remover:
- O parâmetro `bool habilitarPollingTelegram = true` da assinatura de `AddInfrastructure` (volta a ser só `AddInfrastructure(IConfiguration configuration)`, sem o segundo parâmetro).
- O bloco de configuração/registro do Telegram (linhas ~70-85: `services.Configure<TelegramOptions>(...)`, `services.AddScoped<ITelegramService, TelegramBotService>()`, o `if (habilitarPollingTelegram) services.AddHostedService<TelegramUpdatesPollingService>();`).
- O método de extensão `AddPollingDeAtualizacoesTelegram()` inteiro (linhas ~177-184).

- [ ] **Step 3: Atualizar os dois `Program.cs`**

Em `src/AAHBRANT.SST.Api/Program.cs`, remover a linha `builder.Services.AddPollingDeAtualizacoesTelegram();`.

Em `src/AAHBRANT.SST.Worker/Program.cs`, trocar:
```csharp
// habilitarPollingTelegram: false — a Api já roda o long polling do Telegram; ver
// AAHBRANT.SST.Infrastructure.DependencyInjection para o motivo.
builder.Services.AddInfrastructure(builder.Configuration, habilitarPollingTelegram: false);
```
por:
```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

- [ ] **Step 4: Remover `DdsTelegramEnvio` (entidade, config, DbSet)**

Localizar o arquivo da entidade:
```
grep -rln "class DdsTelegramEnvio" src/AAHBRANT.SST.Domain
```
Apagar a classe (se o arquivo só contém essa classe, apagar o arquivo inteiro com `git rm`; se dividir arquivo com outras entidades do DDS, remover só a classe).

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/DdsConfiguracoes.cs`, remover a classe `IEntityTypeConfiguration<DdsTelegramEnvio>` correspondente.

Em `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`, remover a linha `DbSet<DdsTelegramEnvio> DdsTelegramEnvios { get; }`.

Em `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`, remover `public DbSet<DdsTelegramEnvio> DdsTelegramEnvios => Set<DdsTelegramEnvio>();` (ou nome equivalente).

Em `src/AAHBRANT.SST.Domain/Entidades/Dds/Dds.cs`, remover qualquer coleção `ICollection<DdsTelegramEnvio>` se existir.

- [ ] **Step 5: Remover os campos de Telegram do `Trabalhador`**

Em `src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs`, remover as propriedades `TelegramChatId` (`long?`) e `TelegramCodigoVinculo` (`string?`), com seus comentários.

Em qualquer `IEntityTypeConfiguration<Trabalhador>` que configure essas colunas (buscar com `grep -rn "TelegramChatId\|TelegramCodigoVinculo" src/AAHBRANT.SST.Infrastructure`), remover as linhas de configuração.

Em `src/AAHBRANT.SST.Application/Trabalhadores/Queries/ListarTrabalhadoresQuery.cs` e `ObterTrabalhadorPorIdQuery.cs`, remover `TelegramVinculado = t.TelegramChatId != null` (ou equivalente) da projeção, e o campo correspondente do DTO.

- [ ] **Step 6: Build**

Run: `dotnet build SST-APP.sln --nologo -v quiet 2>&1 | tail -40`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. Resolver qualquer referência residual que aparecer.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(dds): remove infraestrutura de Telegram (hosted services, DI, entidade DdsTelegramEnvio, campos de vínculo do trabalhador)"
```

---

### Task 5: Migration removendo tabela e colunas de Telegram

**Files:**
- Create: migration gerada por `dotnet ef migrations add`.

**Interfaces:** nenhuma.

- [ ] **Step 1: Gerar a migration**

Run:
```bash
dotnet ef migrations add RemoverTelegramDds --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api
```
Expected: um novo arquivo `<timestamp>_RemoverTelegramDds.cs` em `src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/`, com `migrationBuilder.DropTable("DdsTelegramEnvios", ...)` e `migrationBuilder.DropColumn("TelegramChatId"/"TelegramCodigoVinculo", "Trabalhadores", ...)`.

- [ ] **Step 2: Conferir a migration gerada**

Abrir o arquivo e confirmar que só remove o que é esperado (tabela `DdsTelegramEnvios` + as duas colunas de `Trabalhadores`) — nenhuma outra tabela/coluna deve aparecer no diff.

- [ ] **Step 3: Build da solução completa**

Run: `dotnet build SST-APP.sln --nologo -v quiet`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Rodar a suíte de testes**

Run: `dotnet test SST-APP.sln --nologo -v quiet 2>&1 | tail -20`
Expected: todos os testes passando (nenhum teste deveria referenciar Telegram — se algum falhar por isso, é sinal de teste morto a remover, não de regressão).

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/
git commit -m "feat(dds): migration removendo tabela DdsTelegramEnvios e colunas de vínculo do Trabalhador"
```

---

### Task 6: Aplicar a migration em homologação e validar

**Files:** nenhum (execução, não código).

- [ ] **Step 1: Push da branch e abertura de PR**

```bash
git push -u origin <nome-da-branch>
gh pr create --base master --title "feat(dds): remove integração de Telegram" --body "Remove por completo o envio/confirmação de DDS via Telegram (não usado desde a decisão do usuário em 2026-09-10) e corrige comentário desatualizado sobre crachá+PIN. Ver docs/superpowers/specs/2026-09-10-assinatura-salva-usuario-design.md §10/§11."
```

- [ ] **Step 2: Aguardar aprovação e merge do usuário**

O merge do PR dispara o deploy automático (`.github/workflows/deploy.yml`) — a migration roda automaticamente no startup da API (`Database.Migrate()`), conforme já documentado em `ONBOARDING.md`.

- [ ] **Step 3: Validar em homologação**

Depois do deploy, abrir um DDS existente e confirmar que a tela não mostra mais nenhum vestígio de Telegram (botão, coluna, mensagem).
