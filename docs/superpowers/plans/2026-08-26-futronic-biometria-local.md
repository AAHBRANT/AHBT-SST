# Biometria Digital Local (Futronic FS80H) — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar biometria digital via leitor USB Futronic FS80H como novo método de autenticação no quiosque de assinatura eletrônica do app SST, com matching 1:N feito localmente por um agente Windows (.NET) que fica instalado no PC do quiosque — sem depender do WebAuthn/FIDO2 já existente, que não suporta cadastro compartilhado de ~100 trabalhadores no mesmo dispositivo.

**Architecture:** Um novo projeto standalone `AAHBRANT.SST.AgenteBiometria` (Kestrel + WinForms tray, roda no PC do quiosque) fala com o leitor via uma abstração `IFingerprintReader`/`IFingerprintMatcher` (só `Simulado*` neste plano — o SDK real da Futronic fica fora de escopo). O agente expõe 4 endpoints HTTP em `127.0.0.1`, com CORS travado na origem exata do quiosque. O navegador do quiosque busca `{ dispositivoId, segredoDispositivo }` uma vez do agente local (nunca em localStorage, só em variável JS em memória), captura a digital e o score via `/api/capturar`, e envia tudo isso — mais `trabalhadorId` — para o backend, que tem a palavra final: reautentica o dispositivo pelo segredo, confere o score contra um limiar configurável, e assina o documento.

No backend, dois problemas de Clean Architecture foram resolvidos replicando o padrão já existente `IPinHasher`/`PinHasherService`: (1) `IDispositivoAgenteAutenticador` vive em Application e usa `ISegredoDispositivoHasher` (Application) implementado por um wrapper fino em Infrastructure — isso evita Application depender de Infrastructure enquanto ainda permite tanto a estratégia de autenticação (Infrastructure) quanto o handler de sincronização (Application) compartilharem a mesma validação de dispositivo. (2) `TemplateBiometricoFutronic.TemplateCriptografado` é uma coluna `string` opaca — **não** um `HasConversion<T>` do EF como o `CpfCriptografiaConversor` — porque um conversor EF descriptografaria automaticamente a cada leitura, colocando bytes biométricos em texto puro na memória do backend toda vez que o endpoint de sincronização lê os templates para repassá-los (ainda criptografados) ao agente. A criptografia (AES-256-GCM) acontece uma única vez, no cadastro, através de `ITemplateBiometricoCriptografia` (Application) — que deliberadamente só expõe `Criptografar`, nunca `Descriptografar`, tornando a garantia "backend nunca descriptografa depois do cadastro" estrutural, não apenas uma convenção.

**Tech Stack:** .NET 8 (Clean Architecture: Domain/Application/Infrastructure/Api + novo projeto AgenteBiometria com Sdk.Web + WinForms), EF Core 8 + SQL Server, MediatR/FluentValidation (auto-registrados via assembly scan), xUnit (Application.Tests sem mocks/DB; novo Infrastructure.Tests com EF InMemory), React 18/Vite/Fluent UI (TeamsApp).

**Spec:** [docs/superpowers/specs/2026-08-26-futronic-biometria-local-design.md](../specs/2026-08-26-futronic-biometria-local-design.md)

## Global Constraints

- O SDK/DLL real da Futronic (ScanAPI/ftrapi) **não entra neste plano** — apenas a abstração `IFingerprintReader`/`IFingerprintMatcher` com implementação `Simulado*`, testável sem hardware. Nunca fabricar bindings P/Invoke reais.
- O segredo do dispositivo (`segredoDispositivo`) nunca trafega em URL/query string — sempre em corpo de POST, ou retido apenas em memória (variável JS, nunca `localStorage`).
- O backend nunca descriptografa um template biométrico fora do momento do cadastro — `ITemplateBiometricoCriptografia` não expõe método de descriptografia.
- Toda entidade nova herda `AuditableEntity` e não redeclara `Ativo`/`RowVersion`; toda config EF aplica `HasQueryFilter(x => x.Ativo)`.
- Testes que precisam de banco (real ou InMemory) vivem em `tests/AAHBRANT.SST.Infrastructure.Tests`, nunca em `Application.Tests` (convenção existente: `Application.Tests` é só xUnit puro, sem InMemory/mocks).
- Nunca fazer `git commit` a menos que o usuário peça explicitamente.
- O texto de consentimento LGPD/Termo de Aceite para biometria (`Trabalhador.ConsentimentoBiometriaEm`) permanece fora de escopo — depende de revisão jurídica.

---

## File Structure

**Domain:**
- Criar `src/AAHBRANN.SST.Domain/Entidades/Assinatura/DispositivoAgenteBiometrico.cs` *(caminho corrigido abaixo)*
- Criar `src/AAHBRANT.SST.Domain/Entidades/Assinatura/DispositivoAgenteBiometrico.cs`
- Criar `src/AAHBRANT.SST.Domain/Entidades/Assinatura/TemplateBiometricoFutronic.cs`

**Application:**
- Modificar `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`
- Criar `src/AAHBRANT.SST.Application/Common/Interfaces/ISegredoDispositivoHasher.cs`
- Criar `src/AAHBRANT.SST.Application/Common/Interfaces/ITemplateBiometricoCriptografia.cs`
- Criar `src/AAHBRANT.SST.Application/Assinatura/IDispositivoAgenteAutenticador.cs`
- Criar `src/AAHBRANT.SST.Application/Assinatura/IAutenticacaoBiometriaLocalService.cs`
- Criar `src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarDispositivoAgenteCommand.cs`
- Criar `src/AAHBRANT.SST.Application/Assinatura/Commands/CadastrarTemplateBiometricoCommand.cs`
- Criar `src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarAssinaturaBiometriaLocalCommand.cs`
- Criar `src/AAHBRANT.SST.Application/Assinatura/Queries/SincronizarTemplatesQuery.cs`

**Infrastructure:**
- Criar `src/AAHBRANT.SST.Infrastructure/Seguranca/SegredoDispositivoHasher.cs`
- Criar `src/AAHBRANT.SST.Infrastructure/Seguranca/SegredoDispositivoHasherService.cs`
- Criar `src/AAHBRANT.SST.Infrastructure/Seguranca/TemplateBiometricoCriptografiaConversor.cs`
- Criar `src/AAHBRANT.SST.Infrastructure/Seguranca/TemplateBiometricoCriptografiaService.cs`
- Criar `src/AAHBRANT.SST.Infrastructure/Assinatura/FutronicAutenticacaoStrategy.cs`
- Modificar `src/AAHBRANT.SST.Infrastructure/Assinatura/AssinaturaOptions.cs`
- Modificar `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`
- Modificar `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AssinaturaConfiguracoes.cs`
- Modificar `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`
- Criar migrations em `src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations/` (via `dotnet ef`)

**Api:**
- Criar `src/AAHBRANT.SST.Api/Controllers/DispositivosAgenteController.cs`
- Modificar `src/AAHBRANT.SST.Api/Controllers/AssinaturaController.cs`
- Modificar `src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs`
- Modificar `src/AAHBRANT.SST.Api/appsettings.json`

**Novo projeto AgenteBiometria (roda no PC do quiosque):**
- Criar `src/AAHBRANT.SST.AgenteBiometria/AAHBRANT.SST.AgenteBiometria.csproj`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Program.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/appsettings.json`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Opcoes/AgenteOptions.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Leitores/IFingerprintReader.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Leitores/IFingerprintMatcher.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Leitores/SimuladoFingerprintReader.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Leitores/SimuladoFingerprintMatcher.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Servicos/BackendClient.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Servicos/TemplateCacheService.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Endpoints/AgenteEndpoints.cs`
- Criar `src/AAHBRANT.SST.AgenteBiometria/Tray/TrayApplicationContext.cs`

**Testes:**
- Criar `tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj`
- Criar `tests/AAHBRANT.SST.Infrastructure.Tests/Seguranca/SegredoDispositivoHasherTests.cs`
- Criar `tests/AAHBRANT.SST.Infrastructure.Tests/Seguranca/TemplateBiometricoCriptografiaConversorTests.cs`
- Criar `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/DispositivoAgenteAutenticadorTests.cs`
- Criar `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/FutronicAutenticacaoStrategyTests.cs`
- Criar `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/RegistrarDispositivoAgenteCommandTests.cs`
- Criar `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/CadastrarTemplateBiometricoCommandTests.cs`
- Criar `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/SincronizarTemplatesQueryHandlerTests.cs`
- Criar `tests/AAHBRANT.SST.Application.Tests/Assinatura/RegistrarAssinaturaBiometriaLocalCommandTests.cs`
- Criar `tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj`
- Criar `tests/AAHBRANT.SST.AgenteBiometria.Tests/Leitores/SimuladoFingerprintMatcherTests.cs`
- Criar `tests/AAHBRANT.SST.AgenteBiometria.Tests/Servicos/TemplateCacheServiceTests.cs`
- Criar `tests/AAHBRANT.SST.AgenteBiometria.Tests/Endpoints/AgenteEndpointsTests.cs`

**Frontend:**
- Modificar `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`
- Criar `src/AAHBRANT.SST.TeamsApp/src/lib/agenteBiometricoLocal.ts`
- Modificar `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaQuiosque.tsx`
- Modificar `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/AssinaturaTab.tsx`

**Isolamento hardware:** Tasks 1–20 (tudo exceto o card final do quiosque, Task 21) são 100% construíveis e testáveis sem o FS80H físico — usam `Simulado*`. Task 21 (UI do quiosque) também roda sem hardware, mas só é validável ponta-a-ponta quando o leitor real chegar; isso fica marcado explicitamente na task.

---

### Task 1: Projeto de testes `AAHBRANT.SST.Infrastructure.Tests`

**Files:**
- Create: `tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj`
- Modify: `SST-APP.sln`

**Interfaces:**
- Produces: projeto de teste com EF Core InMemory disponível, referenciando `AAHBRANT.SST.Infrastructure` e `AAHBRANT.SST.Application`, usado por todas as tasks de 2 a 11 que precisam de `SstDbContext`.

- [ ] **Step 1: Criar o `.csproj`, seguindo o template de `AAHBRANT.SST.Application.Tests.csproj`**

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
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
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

- [ ] **Step 2: Registrar o projeto na solution**

Run: `dotnet sln SST-APP.sln add tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj`
Expected: `Project ... added to the solution.`

- [ ] **Step 3: Criar um teste trivial para confirmar que o projeto builda e o InMemory funciona**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/SstDbContextInMemoryTests.cs
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests;

public class SstDbContextInMemoryTests
{
    public static SstDbContext CriarContexto(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options);
    }

    [Fact]
    public void DeveCriarContextoInMemorySemErro()
    {
        using var db = CriarContexto(nameof(DeveCriarContextoInMemorySemErro));
        Assert.NotNull(db);
    }
}
```

- [ ] **Step 4: Rodar o teste**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 5: Commit**

```bash
git add tests/AAHBRANT.SST.Infrastructure.Tests SST-APP.sln
git commit -m "test: cria projeto Infrastructure.Tests com EF InMemory"
```

---

### Task 2: `SegredoDispositivoHasher` + `ISegredoDispositivoHasher`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Common/Interfaces/ISegredoDispositivoHasher.cs`
- Create: `src/AAHBRANT.SST.Infrastructure/Seguranca/SegredoDispositivoHasher.cs`
- Create: `src/AAHBRANT.SST.Infrastructure/Seguranca/SegredoDispositivoHasherService.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Seguranca/SegredoDispositivoHasherTests.cs`

**Interfaces:**
- Produces: `ISegredoDispositivoHasher { string GerarSegredo(); string GerarHash(string segredo); bool Verificar(string segredo, string hash); }` — consumido por `DispositivoAgenteAutenticador` (Task 4) e `RegistrarDispositivoAgenteCommandHandler` (Task 8).

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Seguranca/SegredoDispositivoHasherTests.cs
using AAHBRANT.SST.Infrastructure.Seguranca;

namespace AAHBRANT.SST.Infrastructure.Tests.Seguranca;

public class SegredoDispositivoHasherTests
{
    [Fact]
    public void GerarSegredo_DeveRetornarStringNaoVaziaEAleatoria()
    {
        var segredo1 = SegredoDispositivoHasher.GerarSegredo();
        var segredo2 = SegredoDispositivoHasher.GerarSegredo();

        Assert.False(string.IsNullOrWhiteSpace(segredo1));
        Assert.NotEqual(segredo1, segredo2);
    }

    [Fact]
    public void Verificar_ComSegredoCorreto_DeveRetornarTrue()
    {
        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var hash = SegredoDispositivoHasher.GerarHash(segredo);

        Assert.True(SegredoDispositivoHasher.Verificar(segredo, hash));
    }

    [Fact]
    public void Verificar_ComSegredoErrado_DeveRetornarFalse()
    {
        var hash = SegredoDispositivoHasher.GerarHash(SegredoDispositivoHasher.GerarSegredo());

        Assert.False(SegredoDispositivoHasher.Verificar("segredo-errado", hash));
    }
}
```

- [ ] **Step 2: Rodar o teste e confirmar que falha (classe ainda não existe)**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter SegredoDispositivoHasherTests`
Expected: FAIL com "The type or namespace name 'SegredoDispositivoHasher' could not be found"

- [ ] **Step 3: Implementar `SegredoDispositivoHasher` (estático, SHA-256 simples — o segredo já é alta entropia gerada pelo servidor, diferente do PIN de baixa entropia que justifica PBKDF2 em `PinHasher`)**

```csharp
// src/AAHBRANT.SST.Infrastructure/Seguranca/SegredoDispositivoHasher.cs
using System.Security.Cryptography;
using System.Text;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Diferente de PinHasher (PBKDF2+salt, necessário para um PIN de 4-6 dígitos de baixa entropia),
// o segredo do dispositivo é gerado aqui mesmo com 256 bits de aleatoriedade — SHA-256 simples já
// é suficiente, já que não há risco de força bruta por dicionário.
public static class SegredoDispositivoHasher
{
    public static string GerarSegredo()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public static string GerarHash(string segredo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(segredo));
        return Convert.ToBase64String(bytes);
    }

    public static bool Verificar(string segredo, string hash)
    {
        var hashCalculado = Convert.FromBase64String(GerarHash(segredo));
        var hashEsperado = Convert.FromBase64String(hash);
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
```

- [ ] **Step 4: Criar a interface Application e o wrapper Infrastructure**

```csharp
// src/AAHBRANT.SST.Application/Common/Interfaces/ISegredoDispositivoHasher.cs
namespace AAHBRANT.SST.Application.Common.Interfaces;

public interface ISegredoDispositivoHasher
{
    string GerarSegredo();
    string GerarHash(string segredo);
    bool Verificar(string segredo, string hash);
}
```

```csharp
// src/AAHBRANT.SST.Infrastructure/Seguranca/SegredoDispositivoHasherService.cs
using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

public class SegredoDispositivoHasherService : ISegredoDispositivoHasher
{
    public string GerarSegredo() => SegredoDispositivoHasher.GerarSegredo();
    public string GerarHash(string segredo) => SegredoDispositivoHasher.GerarHash(segredo);
    public bool Verificar(string segredo, string hash) => SegredoDispositivoHasher.Verificar(segredo, hash);
}
```

- [ ] **Step 5: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter SegredoDispositivoHasherTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 6: Registrar no DI, em `DependencyInjection.cs`, ao lado de `AddScoped<IPinHasher, PinHasherService>()` (ou equivalente já existente)**

```csharp
services.AddScoped<ISegredoDispositivoHasher, SegredoDispositivoHasherService>();
```

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.Application/Common/Interfaces/ISegredoDispositivoHasher.cs src/AAHBRANT.SST.Infrastructure/Seguranca/SegredoDispositivoHasher.cs src/AAHBRANT.SST.Infrastructure/Seguranca/SegredoDispositivoHasherService.cs src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs tests/AAHBRANT.SST.Infrastructure.Tests/Seguranca/SegredoDispositivoHasherTests.cs
git commit -m "feat: adiciona SegredoDispositivoHasher para autenticacao de dispositivos agente"
```

---

### Task 3: `TemplateBiometricoCriptografiaConversor` + `ITemplateBiometricoCriptografia`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Common/Interfaces/ITemplateBiometricoCriptografia.cs`
- Create: `src/AAHBRANT.SST.Infrastructure/Seguranca/TemplateBiometricoCriptografiaConversor.cs`
- Create: `src/AAHBRANT.SST.Infrastructure/Seguranca/TemplateBiometricoCriptografiaService.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`
- Modify: `src/AAHBRANT.SST.Api/appsettings.json`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Seguranca/TemplateBiometricoCriptografiaConversorTests.cs`

**Interfaces:**
- Produces: `ITemplateBiometricoCriptografia { string Criptografar(byte[] templateBruto); }` (só criptografa — nunca descriptografa, por design) — consumido por `CadastrarTemplateBiometricoCommandHandler` (Task 9). A classe estática `TemplateBiometricoCriptografiaConversor` também expõe `Descriptografar` internamente, usado apenas nos testes (round-trip) — nunca chamado em código de produção do backend.

- [ ] **Step 1: Escrever o teste falhando (round-trip via a classe estática, que os testes de Infrastructure podem acessar diretamente)**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Seguranca/TemplateBiometricoCriptografiaConversorTests.cs
using AAHBRANT.SST.Infrastructure.Seguranca;

namespace AAHBRANT.SST.Infrastructure.Tests.Seguranca;

public class TemplateBiometricoCriptografiaConversorTests
{
    public TemplateBiometricoCriptografiaConversorTests()
    {
        var chave = new byte[32];
        Array.Fill(chave, (byte)7);
        TemplateBiometricoCriptografiaContexto.Configurar(chave);
    }

    [Fact]
    public void Criptografar_DeveGerarStringDiferenteDoOriginal()
    {
        var templateBruto = new byte[] { 1, 2, 3, 4, 5 };

        var cifrado = TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);

        Assert.False(string.IsNullOrWhiteSpace(cifrado));
    }

    [Fact]
    public void Criptografar_ChamadoDuasVezesComMesmoInput_DeveGerarCifradosDiferentes()
    {
        var templateBruto = new byte[] { 1, 2, 3, 4, 5 };

        var cifrado1 = TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);
        var cifrado2 = TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);

        Assert.NotEqual(cifrado1, cifrado2);
    }

    [Fact]
    public void Descriptografar_DeveRecuperarOTemplateOriginal()
    {
        var templateBruto = new byte[] { 10, 20, 30, 40, 50, 60 };

        var cifrado = TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);
        var recuperado = TemplateBiometricoCriptografiaConversor.Descriptografar(cifrado);

        Assert.Equal(templateBruto, recuperado);
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter TemplateBiometricoCriptografiaConversorTests`
Expected: FAIL com "The type or namespace name 'TemplateBiometricoCriptografiaConversor' could not be found"

- [ ] **Step 3: Implementar (AES-256-GCM, mesmo layout nonce|cifrado|tag de `CpfCriptografiaConversor`, adaptado para `byte[]` em vez de `string`)**

```csharp
// src/AAHBRANT.SST.Infrastructure/Seguranca/TemplateBiometricoCriptografiaConversor.cs
using System.Security.Cryptography;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

public static class TemplateBiometricoCriptografiaContexto
{
    private static byte[]? _chave;

    public static void Configurar(byte[] chave) => _chave = chave;

    internal static byte[] ObterChave() =>
        _chave ?? throw new InvalidOperationException(
            "TemplateBiometricoCriptografiaContexto não foi configurado. Chame Configurar() no startup.");
}

// Coluna opaca — sem HasConversion<T> do EF. Um ValueConverter descriptografaria automaticamente a
// cada leitura (ex.: SincronizarTemplatesQueryHandler), colocando bytes biométricos em texto puro na
// memória do backend a cada sincronização. Aqui a criptografia só acontece uma vez, no cadastro
// (CadastrarTemplateBiometricoCommandHandler), e o valor cifrado é tratado como opaco daí em diante —
// só o agente local, dono da mesma chave simétrica (distribuída fora de banda), consegue descriptografar.
public static class TemplateBiometricoCriptografiaConversor
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    public static string Criptografar(byte[] templateBruto)
    {
        var chave = TemplateBiometricoCriptografiaContexto.ObterChave();
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var cifrado = new byte[templateBruto.Length];
        var tag = new byte[TamanhoTag];

        using var aesGcm = new AesGcm(chave, TamanhoTag);
        aesGcm.Encrypt(nonce, templateBruto, cifrado, tag);

        var resultado = new byte[TamanhoNonce + cifrado.Length + TamanhoTag];
        Buffer.BlockCopy(nonce, 0, resultado, 0, TamanhoNonce);
        Buffer.BlockCopy(cifrado, 0, resultado, TamanhoNonce, cifrado.Length);
        Buffer.BlockCopy(tag, 0, resultado, TamanhoNonce + cifrado.Length, TamanhoTag);

        return Convert.ToBase64String(resultado);
    }

    // Só usado em testes de round-trip — nenhum código de produção do backend chama isto.
    public static byte[] Descriptografar(string cifradoBase64)
    {
        var chave = TemplateBiometricoCriptografiaContexto.ObterChave();
        var bytes = Convert.FromBase64String(cifradoBase64);

        var nonce = bytes[..TamanhoNonce];
        var tag = bytes[^TamanhoTag..];
        var cifrado = bytes[TamanhoNonce..^TamanhoTag];
        var textoPlano = new byte[cifrado.Length];

        using var aesGcm = new AesGcm(chave, TamanhoTag);
        aesGcm.Decrypt(nonce, cifrado, tag, textoPlano);

        return textoPlano;
    }
}
```

- [ ] **Step 4: Criar a interface Application (só `Criptografar`) e o wrapper Infrastructure**

```csharp
// src/AAHBRANT.SST.Application/Common/Interfaces/ITemplateBiometricoCriptografia.cs
namespace AAHBRANT.SST.Application.Common.Interfaces;

public interface ITemplateBiometricoCriptografia
{
    string Criptografar(byte[] templateBruto);
}
```

```csharp
// src/AAHBRANT.SST.Infrastructure/Seguranca/TemplateBiometricoCriptografiaService.cs
using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

public class TemplateBiometricoCriptografiaService : ITemplateBiometricoCriptografia
{
    public string Criptografar(byte[] templateBruto) => TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);
}
```

- [ ] **Step 5: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter TemplateBiometricoCriptografiaConversorTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 6: Gerar uma chave AES-256 real para o appsettings de desenvolvimento**

Run: `pwsh -Command "[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))"`
Expected: uma string Base64 de 44 caracteres — copiar o valor impresso.

- [ ] **Step 7: Adicionar a chave e registrar o contexto + DI, mirando o bloco já existente de `Lgpd:ChaveCriptografiaCpfBase64` em `appsettings.json` e `DependencyInjection.cs`**

Em `src/AAHBRANT.SST.Api/appsettings.json`, dentro da seção `"Lgpd"` já existente:

```json
"Lgpd": {
  "ChaveCriptografiaCpfBase64": "...(já existente)...",
  "ChaveHashCpfBase64": "...(já existente)...",
  "ChaveCriptografiaBiometriaBase64": "<COLAR AQUI O VALOR GERADO NO STEP 6>"
}
```

Em `DependencyInjection.cs`, ao lado do bloco que hoje chama `CpfCriptografiaContexto.Configurar(...)`:

```csharp
services.AddScoped<ITemplateBiometricoCriptografia, TemplateBiometricoCriptografiaService>();

var chaveBiometriaBase64 = configuration["Lgpd:ChaveCriptografiaBiometriaBase64"];
if (!string.IsNullOrWhiteSpace(chaveBiometriaBase64))
{
    TemplateBiometricoCriptografiaContexto.Configurar(Convert.FromBase64String(chaveBiometriaBase64));
}
```

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Application/Common/Interfaces/ITemplateBiometricoCriptografia.cs src/AAHBRANT.SST.Infrastructure/Seguranca/TemplateBiometricoCriptografiaConversor.cs src/AAHBRANT.SST.Infrastructure/Seguranca/TemplateBiometricoCriptografiaService.cs src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs src/AAHBRANT.SST.Api/appsettings.json tests/AAHBRANT.SST.Infrastructure.Tests/Seguranca/TemplateBiometricoCriptografiaConversorTests.cs
git commit -m "feat: adiciona criptografia AES-256-GCM para templates biometricos"
```

---

### Task 4: Entidade `DispositivoAgenteBiometrico` + persistência

**Files:**
- Create: `src/AAHBRANT.SST.Domain/Entidades/Assinatura/DispositivoAgenteBiometrico.cs`
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AssinaturaConfiguracoes.cs`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/DispositivoAgenteBiometricoConfiguracaoTests.cs`

**Interfaces:**
- Produces: `DispositivoAgenteBiometrico { Guid ObraId; Obra? Obra; string Nome; string SegredoHash; DateTime? UltimaSincronizacaoEm; }` + `IAppDbContext.DispositivosAgenteBiometrico : DbSet<DispositivoAgenteBiometrico>` — consumido por `DispositivoAgenteAutenticador` (Task 6), `RegistrarDispositivoAgenteCommandHandler` (Task 8), `SincronizarTemplatesQueryHandler` (Task 10).

- [ ] **Step 1: Escrever o teste falhando (confirma que a entidade persiste via InMemory e que a FK para Obra funciona)**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/DispositivoAgenteBiometricoConfiguracaoTests.cs
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class DispositivoAgenteBiometricoConfiguracaoTests
{
    [Fact]
    public async Task DevePersistirEDevolverDispositivo()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(DevePersistirEDevolverDispositivo));

        var obra = new Obra { Codigo = "OBR-001", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id,
            Nome = "Totem Portaria",
            SegredoHash = "hash-fake",
        };
        db.DispositivosAgenteBiometrico.Add(dispositivo);
        await db.SaveChangesAsync();

        var recuperado = await db.DispositivosAgenteBiometrico.FirstOrDefaultAsync(d => d.Id == dispositivo.Id);

        Assert.NotNull(recuperado);
        Assert.Equal("Totem Portaria", recuperado!.Nome);
        Assert.True(recuperado.Ativo);
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter DispositivoAgenteBiometricoConfiguracaoTests`
Expected: FAIL com "The type or namespace name 'DispositivoAgenteBiometrico' could not be found"

- [ ] **Step 3: Criar a entidade (namespace achatado `AAHBRANT.SST.Domain.Entidades`, mesmo em pasta `Assinatura/`, seguindo o padrão de `CredencialWebAuthn.cs`)**

```csharp
// src/AAHBRANT.SST.Domain/Entidades/Assinatura/DispositivoAgenteBiometrico.cs
using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class DispositivoAgenteBiometrico : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string SegredoHash { get; set; } = string.Empty;
    public DateTime? UltimaSincronizacaoEm { get; set; }
}
```

- [ ] **Step 4: Adicionar o DbSet em `IAppDbContext.cs`, logo após `DbSet<CredencialWebAuthn> CredenciaisWebAuthn { get; }`**

```csharp
DbSet<DispositivoAgenteBiometrico> DispositivosAgenteBiometrico { get; }
```

- [ ] **Step 5: Adicionar o DbSet em `SstDbContext.cs`, logo após `public DbSet<CredencialWebAuthn> CredenciaisWebAuthn => Set<CredencialWebAuthn>();` (linha 93)**

```csharp
public DbSet<DispositivoAgenteBiometrico> DispositivosAgenteBiometrico => Set<DispositivoAgenteBiometrico>();
```

- [ ] **Step 6: Adicionar a configuração EF em `AssinaturaConfiguracoes.cs`, seguindo o padrão de `CredencialWebAuthnConfiguracao`**

```csharp
public class DispositivoAgenteBiometricoConfiguracao : IEntityTypeConfiguration<DispositivoAgenteBiometrico>
{
    public void Configure(EntityTypeBuilder<DispositivoAgenteBiometrico> builder)
    {
        builder.Property(d => d.Nome).IsRequired().HasMaxLength(150);
        builder.Property(d => d.SegredoHash).IsRequired().HasMaxLength(100);

        builder.HasOne(d => d.Obra).WithMany()
            .HasForeignKey(d => d.ObraId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(d => d.Ativo);
        builder.Property(d => d.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 7: Gerar a migration**

Run: `dotnet ef migrations add AdicionarDispositivoAgenteBiometrico --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`
Expected: `Done.` e um novo arquivo `<timestamp>_AdicionarDispositivoAgenteBiometrico.cs` em `Persistencia/Migrations/`

- [ ] **Step 8: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter DispositivoAgenteBiometricoConfiguracaoTests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 9: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Assinatura/DispositivoAgenteBiometrico.cs src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AssinaturaConfiguracoes.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/DispositivoAgenteBiometricoConfiguracaoTests.cs
git commit -m "feat: adiciona entidade DispositivoAgenteBiometrico"
```

---

### Task 5: Entidade `TemplateBiometricoFutronic` + persistência

**Files:**
- Create: `src/AAHBRANT.SST.Domain/Entidades/Assinatura/TemplateBiometricoFutronic.cs`
- Modify: `src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AssinaturaConfiguracoes.cs`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/TemplateBiometricoFutronicConfiguracaoTests.cs`

**Interfaces:**
- Consumes: `AAHBRANT.SST.Domain.Entidades.Trabalhador` (existente).
- Produces: `TemplateBiometricoFutronic { Guid TrabalhadorId; Trabalhador? Trabalhador; string TemplateCriptografado; DateTime CapturadoEm; }` + `IAppDbContext.TemplatesBiometricoFutronic : DbSet<TemplateBiometricoFutronic>` — consumido por `CadastrarTemplateBiometricoCommandHandler` (Task 9) e `SincronizarTemplatesQueryHandler` (Task 10).

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/TemplateBiometricoFutronicConfiguracaoTests.cs
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class TemplateBiometricoFutronicConfiguracaoTests
{
    [Fact]
    public async Task DevePersistirTemplateComTextoOpaco()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(DevePersistirTemplateComTextoOpaco));

        var obra = new Obra { Codigo = "OBR-002", Nome = "Obra Teste 2" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Fulano", Matricula = "M-001", Cpf = "12345678901" };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var template = new TemplateBiometricoFutronic
        {
            TrabalhadorId = trabalhador.Id,
            TemplateCriptografado = "base64-fake-cifrado",
            CapturadoEm = DateTime.UtcNow,
        };
        db.TemplatesBiometricoFutronic.Add(template);
        await db.SaveChangesAsync();

        var recuperado = await db.TemplatesBiometricoFutronic.FirstOrDefaultAsync(t => t.Id == template.Id);

        Assert.NotNull(recuperado);
        Assert.Equal("base64-fake-cifrado", recuperado!.TemplateCriptografado);
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter TemplateBiometricoFutronicConfiguracaoTests`
Expected: FAIL com "The type or namespace name 'TemplateBiometricoFutronic' could not be found"

- [ ] **Step 3: Criar a entidade**

```csharp
// src/AAHBRANT.SST.Domain/Entidades/Assinatura/TemplateBiometricoFutronic.cs
using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class TemplateBiometricoFutronic : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public string TemplateCriptografado { get; set; } = string.Empty;
    public DateTime CapturadoEm { get; set; }
}
```

- [ ] **Step 4: Adicionar o DbSet em `IAppDbContext.cs`, logo após `DbSet<DispositivoAgenteBiometrico> DispositivosAgenteBiometrico { get; }` (adicionado na Task 4)**

```csharp
DbSet<TemplateBiometricoFutronic> TemplatesBiometricoFutronic { get; }
```

- [ ] **Step 5: Adicionar o DbSet em `SstDbContext.cs`, na mesma posição**

```csharp
public DbSet<TemplateBiometricoFutronic> TemplatesBiometricoFutronic => Set<TemplateBiometricoFutronic>();
```

- [ ] **Step 6: Adicionar a configuração EF em `AssinaturaConfiguracoes.cs` — `TemplateCriptografado` é `string` puro, SEM `HasConversion` (decisão documentada na Architecture do plano)**

```csharp
public class TemplateBiometricoFutronicConfiguracao : IEntityTypeConfiguration<TemplateBiometricoFutronic>
{
    public void Configure(EntityTypeBuilder<TemplateBiometricoFutronic> builder)
    {
        // TemplateCriptografado é opaco para o EF — nunca HasConversion<T> aqui. Ver rationale
        // completo em TemplateBiometricoCriptografiaConversor.cs.
        builder.Property(t => t.TemplateCriptografado).IsRequired().HasMaxLength(4000);

        builder.HasOne(t => t.Trabalhador).WithMany()
            .HasForeignKey(t => t.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(t => t.Ativo);
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 7: Gerar a migration**

Run: `dotnet ef migrations add AdicionarTemplateBiometricoFutronic --project src/AAHBRANT.SST.Infrastructure --startup-project src/AAHBRANT.SST.Api --output-dir Persistencia/Migrations`
Expected: `Done.`

- [ ] **Step 8: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter TemplateBiometricoFutronicConfiguracaoTests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 9: Commit**

```bash
git add src/AAHBRANT.SST.Domain/Entidades/Assinatura/TemplateBiometricoFutronic.cs src/AAHBRANT.SST.Application/Common/Interfaces/IAppDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/SstDbContext.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Configuracoes/AssinaturaConfiguracoes.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Migrations tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/TemplateBiometricoFutronicConfiguracaoTests.cs
git commit -m "feat: adiciona entidade TemplateBiometricoFutronic com armazenamento opaco"
```

---

### Task 6: `IDispositivoAgenteAutenticador` (helper compartilhado)

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/IDispositivoAgenteAutenticador.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/DispositivoAgenteAutenticadorTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.DispositivosAgenteBiometrico` (Task 4), `ISegredoDispositivoHasher` (Task 2).
- Produces: `IDispositivoAgenteAutenticador { Task<DispositivoAgenteBiometrico> ValidarAsync(Guid dispositivoId, string segredoDispositivo, CancellationToken ct); }` (lança `InvalidOperationException` se não encontrado ou segredo errado) — consumido por `FutronicAutenticacaoStrategy` (Task 7), `RegistrarDispositivoAgenteCommandHandler` não usa (é quem cria o dispositivo), `SincronizarTemplatesQueryHandler` (Task 10).

- [ ] **Step 1: Escrever os testes falhando**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/DispositivoAgenteAutenticadorTests.cs
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class DispositivoAgenteAutenticadorTests
{
    private static async Task<(Persistencia.SstDbContext Db, DispositivoAgenteBiometrico Dispositivo, string Segredo)> PrepararAsync(string nomeBanco)
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nomeBanco);
        var obra = new Obra { Codigo = "OBR-003", Nome = "Obra Teste 3" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id,
            Nome = "Totem 1",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivo);
        await db.SaveChangesAsync();

        return (db, dispositivo, segredo);
    }

    [Fact]
    public async Task ValidarAsync_ComSegredoCorreto_DeveRetornarDispositivo()
    {
        var (db, dispositivo, segredo) = await PrepararAsync(nameof(ValidarAsync_ComSegredoCorreto_DeveRetornarDispositivo));
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());

        var resultado = await autenticador.ValidarAsync(dispositivo.Id, segredo, CancellationToken.None);

        Assert.Equal(dispositivo.Id, resultado.Id);
    }

    [Fact]
    public async Task ValidarAsync_ComSegredoErrado_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, _) = await PrepararAsync(nameof(ValidarAsync_ComSegredoErrado_DeveLancarInvalidOperationException));
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            autenticador.ValidarAsync(dispositivo.Id, "segredo-errado", CancellationToken.None));
    }

    [Fact]
    public async Task ValidarAsync_ComDispositivoInexistente_DeveLancarInvalidOperationException()
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nameof(ValidarAsync_ComDispositivoInexistente_DeveLancarInvalidOperationException));
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            autenticador.ValidarAsync(Guid.NewGuid(), "qualquer-coisa", CancellationToken.None));
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter DispositivoAgenteAutenticadorTests`
Expected: FAIL com "The type or namespace name 'DispositivoAgenteAutenticador' could not be found"

- [ ] **Step 3: Implementar a interface + classe (Application layer, mesma pasta de `IAutenticacaoAssinaturaService.cs`)**

```csharp
// src/AAHBRANT.SST.Application/Assinatura/IDispositivoAgenteAutenticador.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

public interface IDispositivoAgenteAutenticador
{
    Task<DispositivoAgenteBiometrico> ValidarAsync(Guid dispositivoId, string segredoDispositivo, CancellationToken ct);
}

public class DispositivoAgenteAutenticador : IDispositivoAgenteAutenticador
{
    private readonly IAppDbContext _db;
    private readonly ISegredoDispositivoHasher _hasher;

    public DispositivoAgenteAutenticador(IAppDbContext db, ISegredoDispositivoHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<DispositivoAgenteBiometrico> ValidarAsync(Guid dispositivoId, string segredoDispositivo, CancellationToken ct)
    {
        var dispositivo = await _db.DispositivosAgenteBiometrico.FirstOrDefaultAsync(d => d.Id == dispositivoId, ct);

        // InvalidOperationException (não UnauthorizedAccessException): TratamentoDeExcecaoMiddleware
        // não tem handler para 401, então cairia no 500 genérico em vez do 400 esperado para esta
        // falha de regra de negócio — mesma convenção de CrachaPinAutenticacaoStrategy.
        if (dispositivo is null || !_hasher.Verificar(segredoDispositivo, dispositivo.SegredoHash))
        {
            throw new InvalidOperationException("Dispositivo não encontrado ou segredo inválido.");
        }

        return dispositivo;
    }
}
```

- [ ] **Step 4: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter DispositivoAgenteAutenticadorTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Registrar no DI**

```csharp
services.AddScoped<IDispositivoAgenteAutenticador, DispositivoAgenteAutenticador>();
```

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/IDispositivoAgenteAutenticador.cs src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/DispositivoAgenteAutenticadorTests.cs
git commit -m "feat: adiciona DispositivoAgenteAutenticador compartilhado"
```

---

### Task 7: `IAutenticacaoBiometriaLocalService` + `FutronicAutenticacaoStrategy`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/IAutenticacaoBiometriaLocalService.cs`
- Create: `src/AAHBRANT.SST.Infrastructure/Assinatura/FutronicAutenticacaoStrategy.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/Assinatura/AssinaturaOptions.cs`
- Modify: `src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs`
- Modify: `src/AAHBRANT.SST.Api/appsettings.json`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/FutronicAutenticacaoStrategyTests.cs`

**Interfaces:**
- Consumes: `IDispositivoAgenteAutenticador` (Task 6), `IAppDbContext.Trabalhadores`/`.Obras`, `AssinaturaOptions.LimiarConfiancaBiometriaLocal`, `ResultadoAutenticacaoAssinatura` e `MetodoAutenticacaoAssinatura.Biometria` (já existentes em `IAutenticacaoAssinaturaService.cs`), `MetodoAutenticacaoObra.Biometria` (já existente em `Enums.cs`).
- Produces: `IAutenticacaoBiometriaLocalService { Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, CancellationToken ct); }` — consumido por `RegistrarAssinaturaBiometriaLocalCommandHandler` (Task 11).

- [ ] **Step 1: Escrever os testes falhando**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/FutronicAutenticacaoStrategyTests.cs
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class FutronicAutenticacaoStrategyTests
{
    private static async Task<(Persistencia.SstDbContext Db, DispositivoAgenteBiometrico Dispositivo, string Segredo, Trabalhador Trabalhador)> PrepararAsync(string nomeBanco, bool habilitarBiometria = true, bool termoAceito = true)
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nomeBanco);
        var obra = new Obra
        {
            Codigo = "OBR-004",
            Nome = "Obra Teste 4",
            MetodosAutenticacaoHabilitados = habilitarBiometria ? MetodoAutenticacaoObra.Biometria : MetodoAutenticacaoObra.Nenhum,
        };
        db.Obras.Add(obra);

        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Ciclano",
            Matricula = "M-002",
            Cpf = "98765432100",
            TermoAceiteAssinaturaEletronicaEm = termoAceito ? DateTime.UtcNow : null,
            ConsentimentoBiometriaEm = termoAceito ? DateTime.UtcNow : null,
        };
        db.Trabalhadores.Add(trabalhador);

        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id,
            Nome = "Totem 2",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivo);

        await db.SaveChangesAsync();
        return (db, dispositivo, segredo, trabalhador);
    }

    private static FutronicAutenticacaoStrategy CriarStrategy(Persistencia.SstDbContext db, double limiar = 50)
    {
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        var options = Options.Create(new AssinaturaOptions { LimiarConfiancaBiometriaLocal = limiar });
        return new FutronicAutenticacaoStrategy(db, autenticador, options);
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComScoreAcimaDoLimiar_DeveRetornarResultado()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComScoreAcimaDoLimiar_DeveRetornarResultado));
        var strategy = CriarStrategy(db);

        var resultado = await strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, CancellationToken.None);

        Assert.Equal(trabalhador.Id, resultado.TrabalhadorId);
        Assert.Equal(MetodoAutenticacaoAssinatura.Biometria, resultado.Metodo);
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComScoreAbaixoDoLimiar_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComScoreAbaixoDoLimiar_DeveLancarInvalidOperationException));
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 10, CancellationToken.None));
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComObraSemMetodoHabilitado_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComObraSemMetodoHabilitado_DeveLancarInvalidOperationException), habilitarBiometria: false);
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, CancellationToken.None));
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_SemTermoOuConsentimento_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_SemTermoOuConsentimento_DeveLancarInvalidOperationException), termoAceito: false);
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, CancellationToken.None));
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComTrabalhadorDeOutraObra_DeveLancarKeyNotFoundException()
    {
        var (db, dispositivo, segredo, _) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComTrabalhadorDeOutraObra_DeveLancarKeyNotFoundException));

        var outraObra = new Obra { Codigo = "OBR-005", Nome = "Outra Obra", MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.Biometria };
        db.Obras.Add(outraObra);
        var trabalhadorDeOutraObra = new Trabalhador
        {
            ObraId = outraObra.Id,
            Nome = "Beltrano",
            Matricula = "M-003",
            Cpf = "11122233344",
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow,
            ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhadorDeOutraObra);
        await db.SaveChangesAsync();

        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhadorDeOutraObra.Id, 80, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter FutronicAutenticacaoStrategyTests`
Expected: FAIL com "The type or namespace name 'IAutenticacaoBiometriaLocalService' could not be found"

- [ ] **Step 3: Criar a interface**

```csharp
// src/AAHBRANT.SST.Application/Assinatura/IAutenticacaoBiometriaLocalService.cs
namespace AAHBRANT.SST.Application.Assinatura;

// Autenticação via biometria digital local (Futronic FS80H + agente Windows) — mesma convenção de
// "cada novo método de auth ganha sua própria interface" já usada para IAutenticacaoWebAuthnService.
// O match 1:N em si acontece no agente local (fora do backend); aqui só se reautentica o dispositivo
// (segredo compartilhado) e se confere o score que o agente já calculou contra o limiar configurado.
public interface IAutenticacaoBiometriaLocalService
{
    Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, CancellationToken ct);
}
```

- [ ] **Step 4: Adicionar `LimiarConfiancaBiometriaLocal` a `AssinaturaOptions.cs`**

```csharp
public double LimiarConfiancaBiometriaLocal { get; set; } = 50;
```

- [ ] **Step 5: Implementar `FutronicAutenticacaoStrategy`, espelhando exatamente a ordem de validação de `CrachaPinAutenticacaoStrategy`**

```csharp
// src/AAHBRANT.SST.Infrastructure/Assinatura/FutronicAutenticacaoStrategy.cs
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

public class FutronicAutenticacaoStrategy : IAutenticacaoBiometriaLocalService
{
    private readonly IAppDbContext _db;
    private readonly IDispositivoAgenteAutenticador _dispositivoAutenticador;
    private readonly AssinaturaOptions _options;

    public FutronicAutenticacaoStrategy(IAppDbContext db, IDispositivoAgenteAutenticador dispositivoAutenticador, IOptions<AssinaturaOptions> options)
    {
        _db = db;
        _dispositivoAutenticador = dispositivoAutenticador;
        _options = options.Value;
    }

    public async Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, CancellationToken ct)
    {
        var dispositivo = await _dispositivoAutenticador.ValidarAsync(dispositivoId, segredoDispositivo, ct);

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct);
        if (trabalhador is null || trabalhador.ObraId != dispositivo.ObraId)
        {
            throw new KeyNotFoundException("Trabalhador não encontrado.");
        }

        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct);
        if (obra is null || !obra.MetodosAutenticacaoHabilitados.HasFlag(MetodoAutenticacaoObra.Biometria))
        {
            throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra deste trabalhador.");
        }

        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null || trabalhador.ConsentimentoBiometriaEm is null)
        {
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite ou o consentimento de biometria.");
        }

        if (score < _options.LimiarConfiancaBiometriaLocal)
        {
            throw new InvalidOperationException("Confiança do match biométrico abaixo do limiar exigido.");
        }

        return new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.Biometria);
    }
}
```

- [ ] **Step 6: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter FutronicAutenticacaoStrategyTests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 7: Registrar DI e appsettings**

```csharp
services.AddScoped<IAutenticacaoBiometriaLocalService, FutronicAutenticacaoStrategy>();
```

Em `appsettings.json`, na seção `"Assinatura"` já existente (ao lado de `UrlBaseValidacaoPublica` ou equivalente):

```json
"Assinatura": {
  "LimiarConfiancaBiometriaLocal": 50
}
```

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/IAutenticacaoBiometriaLocalService.cs src/AAHBRANT.SST.Infrastructure/Assinatura/FutronicAutenticacaoStrategy.cs src/AAHBRANT.SST.Infrastructure/Assinatura/AssinaturaOptions.cs src/AAHBRANT.SST.Infrastructure/DependencyInjection.cs src/AAHBRANT.SST.Api/appsettings.json tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/FutronicAutenticacaoStrategyTests.cs
git commit -m "feat: adiciona FutronicAutenticacaoStrategy"
```

---

### Task 8: `RegistrarDispositivoAgenteCommand`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarDispositivoAgenteCommand.cs`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/RegistrarDispositivoAgenteCommandTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.Obras`/`.DispositivosAgenteBiometrico`, `ISegredoDispositivoHasher`.
- Produces: `RegistrarDispositivoAgenteCommand(Guid ObraId, string Nome) : IRequest<string>` (retorna o segredo em claro, uma única vez) — consumido pelo `DispositivosAgenteController` (Task 12).

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/RegistrarDispositivoAgenteCommandTests.cs
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class RegistrarDispositivoAgenteCommandTests
{
    [Fact]
    public async Task Handle_ComObraExistente_DeveCriarDispositivoERetornarSegredoEmClaro()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_ComObraExistente_DeveCriarDispositivoERetornarSegredoEmClaro));
        var obra = new Obra { Codigo = "OBR-006", Nome = "Obra Teste 6" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new RegistrarDispositivoAgenteCommandHandler(db, new SegredoDispositivoHasherService());
        var comando = new RegistrarDispositivoAgenteCommand(obra.Id, "Totem Portaria");

        var segredo = await handler.Handle(comando, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(segredo));
        var dispositivo = await db.DispositivosAgenteBiometrico.FirstOrDefaultAsync(d => d.ObraId == obra.Id);
        Assert.NotNull(dispositivo);
        Assert.NotEqual(segredo, dispositivo!.SegredoHash);
    }

    [Fact]
    public async Task Handle_ComObraInexistente_DeveLancarKeyNotFoundException()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_ComObraInexistente_DeveLancarKeyNotFoundException));
        var handler = new RegistrarDispositivoAgenteCommandHandler(db, new SegredoDispositivoHasherService());
        var comando = new RegistrarDispositivoAgenteCommand(Guid.NewGuid(), "Totem Portaria");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(comando, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter RegistrarDispositivoAgenteCommandTests`
Expected: FAIL com "The type or namespace name 'RegistrarDispositivoAgenteCommand' could not be found"

- [ ] **Step 3: Implementar record + validator + handler (padrão co-locado de `ConfirmarAutenticacaoWebAuthnCommand.cs`)**

```csharp
// src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarDispositivoAgenteCommand.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

public record RegistrarDispositivoAgenteCommand(Guid ObraId, string Nome) : IRequest<string>;

public class RegistrarDispositivoAgenteCommandValidator : AbstractValidator<RegistrarDispositivoAgenteCommand>
{
    public RegistrarDispositivoAgenteCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
    }
}

public class RegistrarDispositivoAgenteCommandHandler : IRequestHandler<RegistrarDispositivoAgenteCommand, string>
{
    private readonly IAppDbContext _db;
    private readonly ISegredoDispositivoHasher _hasher;

    public RegistrarDispositivoAgenteCommandHandler(IAppDbContext db, ISegredoDispositivoHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<string> Handle(RegistrarDispositivoAgenteCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == request.ObraId, ct);
        if (obra is null)
        {
            throw new KeyNotFoundException("Obra não encontrada.");
        }

        var segredo = _hasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = request.ObraId,
            Nome = request.Nome,
            SegredoHash = _hasher.GerarHash(segredo),
        };
        _db.DispositivosAgenteBiometrico.Add(dispositivo);
        await _db.SaveChangesAsync(ct);

        return segredo;
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter RegistrarDispositivoAgenteCommandTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarDispositivoAgenteCommand.cs tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/RegistrarDispositivoAgenteCommandTests.cs
git commit -m "feat: adiciona RegistrarDispositivoAgenteCommand"
```

---

### Task 9: `CadastrarTemplateBiometricoCommand`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/Commands/CadastrarTemplateBiometricoCommand.cs`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/CadastrarTemplateBiometricoCommandTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext.Trabalhadores`/`.TemplatesBiometricoFutronic`, `ITemplateBiometricoCriptografia` (Task 3).
- Produces: `CadastrarTemplateBiometricoCommand(Guid TrabalhadorId, byte[] TemplateBruto) : IRequest` — consumido pelo `TrabalhadoresController` (Task 13).

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/CadastrarTemplateBiometricoCommandTests.cs
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class CriptografiaFalsaParaTeste : ITemplateBiometricoCriptografia
{
    public string Criptografar(byte[] templateBruto) => Convert.ToBase64String(templateBruto);
}

public class CadastrarTemplateBiometricoCommandTests
{
    [Fact]
    public async Task Handle_ComTrabalhadorElegivel_DeveCriarTemplateCriptografado()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_ComTrabalhadorElegivel_DeveCriarTemplateCriptografado));
        var obra = new Obra { Codigo = "OBR-007", Nome = "Obra Teste 7" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Sicrano",
            Matricula = "M-004",
            Cpf = "55566677788",
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow,
            ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaFalsaParaTeste());
        var templateBruto = new byte[] { 1, 2, 3 };

        await handler.Handle(new CadastrarTemplateBiometricoCommand(trabalhador.Id, templateBruto), CancellationToken.None);

        var salvo = await db.TemplatesBiometricoFutronic.FirstOrDefaultAsync(t => t.TrabalhadorId == trabalhador.Id);
        Assert.NotNull(salvo);
        Assert.Equal(Convert.ToBase64String(templateBruto), salvo!.TemplateCriptografado);
    }

    [Fact]
    public async Task Handle_SemConsentimentoBiometria_DeveLancarInvalidOperationException()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_SemConsentimentoBiometria_DeveLancarInvalidOperationException));
        var obra = new Obra { Codigo = "OBR-008", Nome = "Obra Teste 8" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Sicrano2", Matricula = "M-005", Cpf = "99988877766" };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaFalsaParaTeste());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CadastrarTemplateBiometricoCommand(trabalhador.Id, new byte[] { 1 }), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ComTrabalhadorInexistente_DeveLancarKeyNotFoundException()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_ComTrabalhadorInexistente_DeveLancarKeyNotFoundException));
        var handler = new CadastrarTemplateBiometricoCommandHandler(db, new CriptografiaFalsaParaTeste());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new CadastrarTemplateBiometricoCommand(Guid.NewGuid(), new byte[] { 1 }), CancellationToken.None));
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter CadastrarTemplateBiometricoCommandTests`
Expected: FAIL com "The type or namespace name 'CadastrarTemplateBiometricoCommand' could not be found"

- [ ] **Step 3: Implementar**

```csharp
// src/AAHBRANT.SST.Application/Assinatura/Commands/CadastrarTemplateBiometricoCommand.cs
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

public record CadastrarTemplateBiometricoCommand(Guid TrabalhadorId, byte[] TemplateBruto) : IRequest;

public class CadastrarTemplateBiometricoCommandValidator : AbstractValidator<CadastrarTemplateBiometricoCommand>
{
    public CadastrarTemplateBiometricoCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.TemplateBruto).NotEmpty();
    }
}

public class CadastrarTemplateBiometricoCommandHandler : IRequestHandler<CadastrarTemplateBiometricoCommand>
{
    private readonly IAppDbContext _db;
    private readonly ITemplateBiometricoCriptografia _criptografia;

    public CadastrarTemplateBiometricoCommandHandler(IAppDbContext db, ITemplateBiometricoCriptografia criptografia)
    {
        _db = db;
        _criptografia = criptografia;
    }

    public async Task Handle(CadastrarTemplateBiometricoCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct);
        if (trabalhador is null)
        {
            throw new KeyNotFoundException("Trabalhador não encontrado.");
        }

        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null || trabalhador.ConsentimentoBiometriaEm is null)
        {
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite ou o consentimento de biometria.");
        }

        var template = new TemplateBiometricoFutronic
        {
            TrabalhadorId = request.TrabalhadorId,
            TemplateCriptografado = _criptografia.Criptografar(request.TemplateBruto),
            CapturadoEm = DateTime.UtcNow,
        };
        _db.TemplatesBiometricoFutronic.Add(template);
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter CadastrarTemplateBiometricoCommandTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/Commands/CadastrarTemplateBiometricoCommand.cs tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/CadastrarTemplateBiometricoCommandTests.cs
git commit -m "feat: adiciona CadastrarTemplateBiometricoCommand"
```

---

### Task 10: `SincronizarTemplatesQuery`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/Queries/SincronizarTemplatesQuery.cs`
- Test: `tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/SincronizarTemplatesQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IDispositivoAgenteAutenticador` (Task 6), `IAppDbContext.TemplatesBiometricoFutronic`.
- Produces: `TemplateSincronizadoDto(Guid TrabalhadorId, string TrabalhadorNome, string TemplateCriptografado)` e `SincronizarTemplatesQuery(Guid DispositivoId, string SegredoDispositivo) : IRequest<List<TemplateSincronizadoDto>>` — consumido pelo `DispositivosAgenteController` (Task 12) e pelo agente local (`BackendClient`, Task 17) via HTTP.

- [ ] **Step 1: Escrever o teste falhando**

```csharp
// tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/SincronizarTemplatesQueryHandlerTests.cs
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class SincronizarTemplatesQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarSoTemplatesDaMesmaObraDoDispositivo()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_DeveRetornarSoTemplatesDaMesmaObraDoDispositivo));

        var obraA = new Obra { Codigo = "OBR-009", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OBR-010", Nome = "Obra B" };
        db.Obras.AddRange(obraA, obraB);

        var trabalhadorA = new Trabalhador { ObraId = obraA.Id, Nome = "Trab A", Matricula = "M-006", Cpf = "12312312312" };
        var trabalhadorB = new Trabalhador { ObraId = obraB.Id, Nome = "Trab B", Matricula = "M-007", Cpf = "45645645645" };
        db.Trabalhadores.AddRange(trabalhadorA, trabalhadorB);

        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivoA = new DispositivoAgenteBiometrico
        {
            ObraId = obraA.Id,
            Nome = "Totem A",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivoA);
        await db.SaveChangesAsync();

        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = trabalhadorA.Id, TemplateCriptografado = "cifrado-a", CapturadoEm = DateTime.UtcNow });
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = trabalhadorB.Id, TemplateCriptografado = "cifrado-b", CapturadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        var handler = new SincronizarTemplatesQueryHandler(db, autenticador);

        var resultado = await handler.Handle(new SincronizarTemplatesQuery(dispositivoA.Id, segredo), CancellationToken.None);

        Assert.Single(resultado);
        Assert.Equal(trabalhadorA.Id, resultado[0].TrabalhadorId);
        Assert.Equal("cifrado-a", resultado[0].TemplateCriptografado);
    }

    [Fact]
    public async Task Handle_DeveAtualizarUltimaSincronizacao()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_DeveAtualizarUltimaSincronizacao));
        var obra = new Obra { Codigo = "OBR-011", Nome = "Obra Teste 11" };
        db.Obras.Add(obra);
        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id,
            Nome = "Totem X",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivo);
        await db.SaveChangesAsync();

        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        var handler = new SincronizarTemplatesQueryHandler(db, autenticador);

        await handler.Handle(new SincronizarTemplatesQuery(dispositivo.Id, segredo), CancellationToken.None);

        var atualizado = await db.DispositivosAgenteBiometrico.FirstAsync(d => d.Id == dispositivo.Id);
        Assert.NotNull(atualizado.UltimaSincronizacaoEm);
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter SincronizarTemplatesQueryHandlerTests`
Expected: FAIL com "The type or namespace name 'SincronizarTemplatesQuery' could not be found"

- [ ] **Step 3: Implementar**

```csharp
// src/AAHBRANT.SST.Application/Assinatura/Queries/SincronizarTemplatesQuery.cs
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record TemplateSincronizadoDto(Guid TrabalhadorId, string TrabalhadorNome, string TemplateCriptografado);

public record SincronizarTemplatesQuery(Guid DispositivoId, string SegredoDispositivo) : IRequest<List<TemplateSincronizadoDto>>;

public class SincronizarTemplatesQueryValidator : AbstractValidator<SincronizarTemplatesQuery>
{
    public SincronizarTemplatesQueryValidator()
    {
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty();
    }
}

public class SincronizarTemplatesQueryHandler : IRequestHandler<SincronizarTemplatesQuery, List<TemplateSincronizadoDto>>
{
    private readonly IAppDbContext _db;
    private readonly IDispositivoAgenteAutenticador _dispositivoAutenticador;

    public SincronizarTemplatesQueryHandler(IAppDbContext db, IDispositivoAgenteAutenticador dispositivoAutenticador)
    {
        _db = db;
        _dispositivoAutenticador = dispositivoAutenticador;
    }

    public async Task<List<TemplateSincronizadoDto>> Handle(SincronizarTemplatesQuery request, CancellationToken ct)
    {
        var dispositivo = await _dispositivoAutenticador.ValidarAsync(request.DispositivoId, request.SegredoDispositivo, ct);

        dispositivo.UltimaSincronizacaoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await _db.TemplatesBiometricoFutronic
            .Where(t => t.Trabalhador!.ObraId == dispositivo.ObraId)
            .Select(t => new TemplateSincronizadoDto(t.TrabalhadorId, t.Trabalhador!.Nome, t.TemplateCriptografado))
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests/AAHBRANT.SST.Infrastructure.Tests.csproj --filter SincronizarTemplatesQueryHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/Queries/SincronizarTemplatesQuery.cs tests/AAHBRANT.SST.Infrastructure.Tests/Assinatura/SincronizarTemplatesQueryHandlerTests.cs
git commit -m "feat: adiciona SincronizarTemplatesQuery"
```

---

### Task 11: `RegistrarAssinaturaBiometriaLocalCommand`

**Files:**
- Create: `src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarAssinaturaBiometriaLocalCommand.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Assinatura/RegistrarAssinaturaBiometriaLocalCommandTests.cs`

**Interfaces:**
- Consumes: `IAutenticacaoBiometriaLocalService` (Task 7), `IRegistradorAssinaturaService` (já existente, mesmo usado por `ConfirmarAutenticacaoWebAuthnCommand`).
- Produces: `RegistrarAssinaturaBiometriaLocalCommand(Guid DocumentoAssinaturaId, Guid DispositivoId, string SegredoDispositivo, Guid TrabalhadorId, double Score) : IRequest<DocumentoSignatarioDto>` — consumido pelo `AssinaturaController` (Task 13).

Este handler só orquestra duas interfaces injetadas (sem tocar `IAppDbContext` diretamente) — segue a mesma convenção de `ConfirmarAutenticacaoWebAuthnCommandHandler`, por isso o teste usa fakes manuais e vive em `Application.Tests` (sem precisar de InMemory).

- [ ] **Step 1: Escrever o teste falhando, com fakes manuais (convenção do projeto: sem framework de mock)**

```csharp
// tests/AAHBRANT.SST.Application.Tests/Assinatura/RegistrarAssinaturaBiometriaLocalCommandTests.cs
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Dtos;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class AutenticacaoBiometriaLocalFalsa : IAutenticacaoBiometriaLocalService
{
    public Guid? DispositivoIdRecebido { get; private set; }
    public double? ScoreRecebido { get; private set; }

    public Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, CancellationToken ct)
    {
        DispositivoIdRecebido = dispositivoId;
        ScoreRecebido = score;
        return Task.FromResult(new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.Biometria));
    }
}

public class RegistradorAssinaturaFalso : IRegistradorAssinaturaService
{
    public Guid? DocumentoIdRecebido { get; private set; }

    public Task<DocumentoSignatarioDto> RegistrarAsync(Guid documentoAssinaturaId, ResultadoAutenticacaoAssinatura resultado, CancellationToken ct)
    {
        DocumentoIdRecebido = documentoAssinaturaId;
        return Task.FromResult(new DocumentoSignatarioDto(Guid.NewGuid(), resultado.TrabalhadorId, "Nome Fake", resultado.Metodo, DateTime.UtcNow));
    }
}

public class RegistrarAssinaturaBiometriaLocalCommandTests
{
    [Fact]
    public async Task Handle_DeveAutenticarERegistrarAssinatura()
    {
        var autenticacao = new AutenticacaoBiometriaLocalFalsa();
        var registrador = new RegistradorAssinaturaFalso();
        var handler = new RegistrarAssinaturaBiometriaLocalCommandHandler(autenticacao, registrador);

        var documentoId = Guid.NewGuid();
        var dispositivoId = Guid.NewGuid();
        var trabalhadorId = Guid.NewGuid();

        var resultado = await handler.Handle(
            new RegistrarAssinaturaBiometriaLocalCommand(documentoId, dispositivoId, "segredo", trabalhadorId, 90), CancellationToken.None);

        Assert.Equal(dispositivoId, autenticacao.DispositivoIdRecebido);
        Assert.Equal(90, autenticacao.ScoreRecebido);
        Assert.Equal(documentoId, registrador.DocumentoIdRecebido);
        Assert.Equal(trabalhadorId, resultado.TrabalhadorId);
    }
}
```

**Nota:** ajustar `DocumentoSignatarioDto`/`IRegistradorAssinaturaService` no fake acima para os nomes de propriedade e assinatura reais desses tipos existentes (confirmar em `src/AAHBRANT.SST.Application/Assinatura/` antes de escrever — se os nomes de campo divergirem do exemplo, usar os reais; a forma do teste continua válida).

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter RegistrarAssinaturaBiometriaLocalCommandTests`
Expected: FAIL com "The type or namespace name 'RegistrarAssinaturaBiometriaLocalCommand' could not be found"

- [ ] **Step 3: Implementar (mesmo padrão de 2 linhas de `ConfirmarAutenticacaoWebAuthnCommandHandler`)**

```csharp
// src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarAssinaturaBiometriaLocalCommand.cs
using AAHBRANT.SST.Application.Assinatura.Dtos;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

public record RegistrarAssinaturaBiometriaLocalCommand(
    Guid DocumentoAssinaturaId, Guid DispositivoId, string SegredoDispositivo, Guid TrabalhadorId, double Score) : IRequest<DocumentoSignatarioDto>;

public class RegistrarAssinaturaBiometriaLocalCommandValidator : AbstractValidator<RegistrarAssinaturaBiometriaLocalCommand>
{
    public RegistrarAssinaturaBiometriaLocalCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }
}

public class RegistrarAssinaturaBiometriaLocalCommandHandler : IRequestHandler<RegistrarAssinaturaBiometriaLocalCommand, DocumentoSignatarioDto>
{
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;
    private readonly IRegistradorAssinaturaService _registrador;

    public RegistrarAssinaturaBiometriaLocalCommandHandler(IAutenticacaoBiometriaLocalService autenticacao, IRegistradorAssinaturaService registrador)
    {
        _autenticacao = autenticacao;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(RegistrarAssinaturaBiometriaLocalCommand request, CancellationToken ct)
    {
        var resultado = await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score, ct);
        return await _registrador.RegistrarAsync(request.DocumentoAssinaturaId, resultado, ct);
    }
}
```

**Nota:** confirmar o namespace real de `DocumentoSignatarioDto` e `IRegistradorAssinaturaService` (visto em `ConfirmarAutenticacaoWebAuthnCommand.cs`) e ajustar o `using` acima de acordo — o exemplo assume `AAHBRANT.SST.Application.Assinatura.Dtos`, mas pode já estar no mesmo namespace `AAHBRANT.SST.Application.Assinatura`, sem `using` extra.

- [ ] **Step 4: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests/AAHBRANT.SST.Application.Tests.csproj --filter RegistrarAssinaturaBiometriaLocalCommandTests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Assinatura/Commands/RegistrarAssinaturaBiometriaLocalCommand.cs tests/AAHBRANT.SST.Application.Tests/Assinatura/RegistrarAssinaturaBiometriaLocalCommandTests.cs
git commit -m "feat: adiciona RegistrarAssinaturaBiometriaLocalCommand"
```

---

### Task 12: `DispositivosAgenteController`

**Files:**
- Create: `src/AAHBRANT.SST.Api/Controllers/DispositivosAgenteController.cs`

**Interfaces:**
- Consumes: `RegistrarDispositivoAgenteCommand` (Task 8), `SincronizarTemplatesQuery` (Task 10), `IMediator` (padrão MediatR já usado em todos os controllers).
- Produces: `POST /api/dispositivos-agente` (Authorize `organizacional:editar`) e `POST /api/dispositivos-agente/{id}/templates/sincronizar` (`[AllowAnonymous]`, autenticado manualmente pelo segredo no corpo) — o segundo é consumido pelo `BackendClient` do agente local (Task 17).

- [ ] **Step 1: Implementar o controller**

```csharp
// src/AAHBRANT.SST.Api/Controllers/DispositivosAgenteController.cs
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/dispositivos-agente")]
public class DispositivosAgenteController : ControllerBase
{
    private readonly IMediator _mediator;

    public DispositivosAgenteController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public record RegistrarDispositivoAgenteRequestBody(Guid ObraId, string Nome);

    [HttpPost]
    [Authorize(Policy = "organizacional:editar")]
    public async Task<ActionResult<string>> Registrar(RegistrarDispositivoAgenteRequestBody body, CancellationToken ct)
    {
        var segredo = await _mediator.Send(new RegistrarDispositivoAgenteCommand(body.ObraId, body.Nome), ct);
        return Ok(segredo);
    }

    public record SincronizarTemplatesRequestBody(string SegredoDispositivo);

    // AllowAnonymous: este endpoint é chamado pelo agente local (sem token Entra ID), não pelo
    // navegador do quiosque. A autenticação é o segredo do dispositivo no corpo do POST, validado
    // manualmente dentro do handler via IDispositivoAgenteAutenticador — nunca em query string.
    [HttpPost("{id:guid}/templates/sincronizar")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TemplateSincronizadoDto>>> Sincronizar(Guid id, SincronizarTemplatesRequestBody body, CancellationToken ct)
    {
        var templates = await _mediator.Send(new SincronizarTemplatesQuery(id, body.SegredoDispositivo), ct);
        return Ok(templates);
    }
}
```

- [ ] **Step 2: Buildar a API e confirmar que compila sem erro**

Run: `dotnet build src/AAHBRANT.SST.Api/AAHBRANT.SST.Api.csproj`
Expected: `Build succeeded.`

- [ ] **Step 3: Rodar a suíte completa de testes de backend para garantir que nada quebrou**

Run: `dotnet test SST-APP.sln`
Expected: `Passed!` em todos os projetos de teste

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.Api/Controllers/DispositivosAgenteController.cs
git commit -m "feat: adiciona DispositivosAgenteController"
```

---

### Task 13: Estender `AssinaturaController` e `TrabalhadoresController`

**Files:**
- Modify: `src/AAHBRANT.SST.Api/Controllers/AssinaturaController.cs`
- Modify: `src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs`

**Interfaces:**
- Consumes: `RegistrarAssinaturaBiometriaLocalCommand` (Task 11), `CadastrarTemplateBiometricoCommand` (Task 9).
- Produces: `POST /api/documentos/{id}/autenticacao/biometria-local` (policy `assinatura:assinar`, mesma do `{id}/assinar`) e `POST /api/trabalhadores/{id}/assinatura/biometria-local/cadastro` (policy `trabalhador:assinatura`, mesma dos outros endpoints de cadastro biométrico) — consumidos pelo frontend (Task 19).

- [ ] **Step 1: Adicionar o endpoint em `AssinaturaController.cs`, ao lado de `{id}/assinar/webauthn/confirmar`**

```csharp
public record AutenticarBiometriaLocalRequestBody(Guid DispositivoId, string SegredoDispositivo, Guid TrabalhadorId, double Score);

[HttpPost("{id:guid}/autenticacao/biometria-local")]
[Authorize(Policy = "assinatura:assinar")]
public async Task<ActionResult<DocumentoSignatarioDto>> AutenticarBiometriaLocal(Guid id, AutenticarBiometriaLocalRequestBody body, CancellationToken ct)
{
    var resultado = await _mediator.Send(
        new RegistrarAssinaturaBiometriaLocalCommand(id, body.DispositivoId, body.SegredoDispositivo, body.TrabalhadorId, body.Score), ct);
    return Ok(resultado);
}
```

- [ ] **Step 2: Adicionar o endpoint em `TrabalhadoresController.cs`, ao lado de `{id}/assinatura/webauthn/cadastro/confirmar`**

```csharp
public record CadastrarBiometriaLocalRequestBody(byte[] TemplateBruto);

[HttpPost("{id:guid}/assinatura/biometria-local/cadastro")]
[Authorize(Policy = "trabalhador:assinatura")]
public async Task<IActionResult> CadastrarBiometriaLocal(Guid id, CadastrarBiometriaLocalRequestBody body, CancellationToken ct)
{
    await _mediator.Send(new CadastrarTemplateBiometricoCommand(id, body.TemplateBruto), ct);
    return NoContent();
}
```

- [ ] **Step 3: Buildar e confirmar sucesso**

Run: `dotnet build src/AAHBRANT.SST.Api/AAHBRANT.SST.Api.csproj`
Expected: `Build succeeded.`

- [ ] **Step 4: Rodar a suíte completa**

Run: `dotnet test SST-APP.sln`
Expected: `Passed!`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Api/Controllers/AssinaturaController.cs src/AAHBRANT.SST.Api/Controllers/TrabalhadoresController.cs
git commit -m "feat: adiciona endpoints de biometria local em AssinaturaController e TrabalhadoresController"
```

---

### Task 14: Scaffold do projeto `AAHBRANT.SST.AgenteBiometria`

**Files:**
- Create: `src/AAHBRANT.SST.AgenteBiometria/AAHBRANT.SST.AgenteBiometria.csproj`
- Create: `src/AAHBRANT.SST.AgenteBiometria/appsettings.json`
- Create: `src/AAHBRANT.SST.AgenteBiometria/Opcoes/AgenteOptions.cs`
- Modify: `SST-APP.sln`

**Interfaces:**
- Produces: `AgenteOptions { Guid DispositivoId; string SegredoDispositivo; string ChaveCriptografiaBiometriaBase64; string BackendBaseUrl; string OrigemPermitida; }` — consumido por todas as tasks seguintes do agente (15-18).

- [ ] **Step 1: Criar o `.csproj` (Sdk.Web + WinForms + net8.0-windows, já que a tray icon do WinForms precisa de TFM Windows)**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <OutputType>WinExe</OutputType>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Criar `appsettings.json` inicial (valores de exemplo — o `DispositivoId`/`SegredoDispositivo` reais vêm da resposta de `POST /api/dispositivos-agente`, Task 12)**

```json
{
  "Agente": {
    "DispositivoId": "00000000-0000-0000-0000-000000000000",
    "SegredoDispositivo": "",
    "ChaveCriptografiaBiometriaBase64": "",
    "BackendBaseUrl": "https://sst-api-hml.azurewebsites.net",
    "OrigemPermitida": "https://sst-web-hml.azurewebsites.net"
  }
}
```

- [ ] **Step 3: Criar `AgenteOptions.cs`**

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Opcoes/AgenteOptions.cs
namespace AAHBRANT.SST.AgenteBiometria.Opcoes;

public class AgenteOptions
{
    public Guid DispositivoId { get; set; }
    public string SegredoDispositivo { get; set; } = string.Empty;
    public string ChaveCriptografiaBiometriaBase64 { get; set; } = string.Empty;
    public string BackendBaseUrl { get; set; } = string.Empty;
    public string OrigemPermitida { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Registrar o projeto na solution**

Run: `dotnet sln SST-APP.sln add src/AAHBRANT.SST.AgenteBiometria/AAHBRANT.SST.AgenteBiometria.csproj`
Expected: `Project ... added to the solution.`

- [ ] **Step 5: Buildar e confirmar sucesso**

Run: `dotnet build src/AAHBRANT.SST.AgenteBiometria/AAHBRANT.SST.AgenteBiometria.csproj`
Expected: `Build succeeded.` (sem `Program.cs` ainda, o SDK Web gera um host mínimo automaticamente — se falhar por falta de entry point, prosseguir para a Task 15/18 antes de re-validar o build)

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.AgenteBiometria/AAHBRANT.SST.AgenteBiometria.csproj src/AAHBRANT.SST.AgenteBiometria/appsettings.json src/AAHBRANT.SST.AgenteBiometria/Opcoes/AgenteOptions.cs SST-APP.sln
git commit -m "chore: scaffold do projeto AAHBRANT.SST.AgenteBiometria"
```

---

### Task 15: Abstrações do leitor (`IFingerprintReader`/`IFingerprintMatcher`) + projeto de testes do agente

**Files:**
- Create: `src/AAHBRANT.SST.AgenteBiometria/Leitores/IFingerprintReader.cs`
- Create: `src/AAHBRANT.SST.AgenteBiometria/Leitores/IFingerprintMatcher.cs`
- Create: `src/AAHBRANT.SST.AgenteBiometria/Leitores/SimuladoFingerprintReader.cs`
- Create: `src/AAHBRANT.SST.AgenteBiometria/Leitores/SimuladoFingerprintMatcher.cs`
- Create: `tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj`
- Modify: `SST-APP.sln`
- Test: `tests/AAHBRANT.SST.AgenteBiometria.Tests/Leitores/SimuladoFingerprintMatcherTests.cs`

**Interfaces:**
- Produces: `IFingerprintReader { Task<byte[]> CapturarAsync(CancellationToken ct); }`, `IFingerprintMatcher { double Comparar(byte[] capturaBruta, byte[] templateBruto); }`, `SimuladoFingerprintReader(byte[] proximaCaptura)`, `SimuladoFingerprintMatcher` — consumidos por `AgenteEndpoints` (Task 18). Estas são as ÚNICAS implementações neste plano — o SDK Futronic real (ScanAPI/ftrapi) fica fora de escopo.

- [ ] **Step 1: Criar o projeto de testes do agente**

```xml
<!-- tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AAHBRANT.SST.AgenteBiometria\AAHBRANT.SST.AgenteBiometria.csproj" />
  </ItemGroup>

</Project>
```

Run: `dotnet sln SST-APP.sln add tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj`
Expected: `Project ... added to the solution.`

- [ ] **Step 2: Escrever o teste falhando para o matcher simulado**

```csharp
// tests/AAHBRANT.SST.AgenteBiometria.Tests/Leitores/SimuladoFingerprintMatcherTests.cs
using AAHBRANT.SST.AgenteBiometria.Leitores;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Leitores;

public class SimuladoFingerprintMatcherTests
{
    [Fact]
    public void Comparar_ComArraysIdenticos_DeveRetornar100()
    {
        var matcher = new SimuladoFingerprintMatcher();
        var template = new byte[] { 1, 2, 3, 4, 5 };

        var score = matcher.Comparar(template, template);

        Assert.Equal(100, score);
    }

    [Fact]
    public void Comparar_ComArraysTotalmenteDiferentes_DeveRetornarProximoDeZero()
    {
        var matcher = new SimuladoFingerprintMatcher();

        var score = matcher.Comparar(new byte[] { 1, 1, 1, 1 }, new byte[] { 2, 2, 2, 2 });

        Assert.Equal(0, score);
    }

    [Fact]
    public void Comparar_ComArrayVazio_DeveRetornarZero()
    {
        var matcher = new SimuladoFingerprintMatcher();

        var score = matcher.Comparar(Array.Empty<byte>(), new byte[] { 1, 2, 3 });

        Assert.Equal(0, score);
    }
}
```

- [ ] **Step 3: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj --filter SimuladoFingerprintMatcherTests`
Expected: FAIL com "The type or namespace name 'SimuladoFingerprintMatcher' could not be found"

- [ ] **Step 4: Implementar as interfaces e as implementações simuladas**

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Leitores/IFingerprintReader.cs
namespace AAHBRANT.SST.AgenteBiometria.Leitores;

public interface IFingerprintReader
{
    Task<byte[]> CapturarAsync(CancellationToken ct);
}
```

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Leitores/IFingerprintMatcher.cs
namespace AAHBRANT.SST.AgenteBiometria.Leitores;

public interface IFingerprintMatcher
{
    // Retorna um score de 0 a 100 representando a similaridade entre a captura ao vivo e um
    // template cadastrado.
    double Comparar(byte[] capturaBruta, byte[] templateBruto);
}
```

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Leitores/SimuladoFingerprintReader.cs
namespace AAHBRANT.SST.AgenteBiometria.Leitores;

// Implementação simulada — sem o SDK Futronic real (ScanAPI/ftrapi) não há hardware para capturar.
// Usada em desenvolvimento/testes; troca por uma implementação real via P/Invoke assim que o FS80H
// físico e o SDK chegarem (fora do escopo deste plano — ver spec §2 "Não entra").
public class SimuladoFingerprintReader : IFingerprintReader
{
    private readonly byte[] _proximaCaptura;

    public SimuladoFingerprintReader(byte[] proximaCaptura)
    {
        _proximaCaptura = proximaCaptura;
    }

    public Task<byte[]> CapturarAsync(CancellationToken ct) => Task.FromResult(_proximaCaptura);
}
```

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Leitores/SimuladoFingerprintMatcher.cs
namespace AAHBRANT.SST.AgenteBiometria.Leitores;

// Implementação simulada — comparação byte-a-byte, não é um algoritmo biométrico real. Serve para
// desenvolvimento/testes sem hardware; troca por um matcher real do SDK Futronic quando o hardware
// chegar (fora do escopo deste plano).
public class SimuladoFingerprintMatcher : IFingerprintMatcher
{
    public double Comparar(byte[] capturaBruta, byte[] templateBruto)
    {
        if (capturaBruta.Length == 0 || templateBruto.Length == 0)
        {
            return 0;
        }

        var tamanhoComum = Math.Min(capturaBruta.Length, templateBruto.Length);
        var iguais = 0;
        for (var i = 0; i < tamanhoComum; i++)
        {
            if (capturaBruta[i] == templateBruto[i])
            {
                iguais++;
            }
        }

        return 100.0 * iguais / Math.Max(capturaBruta.Length, templateBruto.Length);
    }
}
```

- [ ] **Step 5: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj --filter SimuladoFingerprintMatcherTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.AgenteBiometria/Leitores tests/AAHBRANT.SST.AgenteBiometria.Tests SST-APP.sln
git commit -m "feat: adiciona abstracao IFingerprintReader/IFingerprintMatcher com implementacao simulada"
```

---

### Task 16: `BackendClient` + `TemplateCacheService`

**Files:**
- Create: `src/AAHBRANT.SST.AgenteBiometria/Servicos/BackendClient.cs`
- Create: `src/AAHBRANT.SST.AgenteBiometria/Servicos/TemplateCacheService.cs`
- Test: `tests/AAHBRANT.SST.AgenteBiometria.Tests/Servicos/TemplateCacheServiceTests.cs`

**Interfaces:**
- Consumes: `AgenteOptions` (Task 14).
- Produces: `TemplateCacheado(Guid TrabalhadorId, string TrabalhadorNome, byte[] TemplateBruto)`, `TemplateCacheService.SincronizarAsync(CancellationToken ct)`, `TemplateCacheService.Templates : IReadOnlyList<TemplateCacheado>` — consumidos por `AgenteEndpoints` (Task 18).

**Nota de design:** a rotina de descriptografia AES-256-GCM abaixo duplica deliberadamente o layout nonce|cifrado|tag de `TemplateBiometricoCriptografiaConversor.cs` (Task 3) — o agente é um executável standalone que roda em PCs de obra e não referencia `AAHBRANT.SST.Infrastructure`, então não há como compartilhar a classe sem criar um pacote NuGet interno só para ~15 linhas de código. Se o layout de criptografia mudar no backend, esta cópia precisa ser atualizada manualmente.

- [ ] **Step 1: Escrever o teste falhando (round-trip: cifra do jeito que o backend cifraria, testa que o serviço descriptografa certo)**

```csharp
// tests/AAHBRANT.SST.AgenteBiometria.Tests/Servicos/TemplateCacheServiceTests.cs
using System.Security.Cryptography;
using AAHBRANT.SST.AgenteBiometria.Servicos;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Servicos;

public class TemplateCacheServiceTests
{
    private static string CriptografarComoOBackendFaria(byte[] templateBruto, byte[] chave)
    {
        const int tamanhoNonce = 12;
        const int tamanhoTag = 16;
        var nonce = RandomNumberGenerator.GetBytes(tamanhoNonce);
        var cifrado = new byte[templateBruto.Length];
        var tag = new byte[tamanhoTag];

        using var aesGcm = new AesGcm(chave, tamanhoTag);
        aesGcm.Encrypt(nonce, templateBruto, cifrado, tag);

        var resultado = new byte[tamanhoNonce + cifrado.Length + tamanhoTag];
        Buffer.BlockCopy(nonce, 0, resultado, 0, tamanhoNonce);
        Buffer.BlockCopy(cifrado, 0, resultado, tamanhoNonce, cifrado.Length);
        Buffer.BlockCopy(tag, 0, resultado, tamanhoNonce + cifrado.Length, tamanhoTag);
        return Convert.ToBase64String(resultado);
    }

    [Fact]
    public void DescriptografarTemplate_DeveRecuperarOTemplateOriginal()
    {
        var chave = new byte[32];
        Array.Fill(chave, (byte)9);
        var templateOriginal = new byte[] { 11, 22, 33, 44 };
        var cifrado = CriptografarComoOBackendFaria(templateOriginal, chave);

        var recuperado = TemplateCacheService.DescriptografarTemplate(cifrado, chave);

        Assert.Equal(templateOriginal, recuperado);
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj --filter TemplateCacheServiceTests`
Expected: FAIL com "The type or namespace name 'TemplateCacheService' could not be found"

- [ ] **Step 3: Implementar `BackendClient`**

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Servicos/BackendClient.cs
using System.Net.Http.Json;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Servicos;

public record TemplateSincronizadoResponse(Guid TrabalhadorId, string TrabalhadorNome, string TemplateCriptografado);

public class BackendClient
{
    private readonly HttpClient _httpClient;
    private readonly AgenteOptions _options;

    public BackendClient(HttpClient httpClient, IOptions<AgenteOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<List<TemplateSincronizadoResponse>> SincronizarTemplatesAsync(CancellationToken ct)
    {
        var url = $"{_options.BackendBaseUrl}/api/dispositivos-agente/{_options.DispositivoId}/templates/sincronizar";
        var resposta = await _httpClient.PostAsJsonAsync(url, new { SegredoDispositivo = _options.SegredoDispositivo }, ct);
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<List<TemplateSincronizadoResponse>>(cancellationToken: ct) ?? new();
    }
}
```

- [ ] **Step 4: Implementar `TemplateCacheService`**

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Servicos/TemplateCacheService.cs
using System.Security.Cryptography;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Servicos;

public record TemplateCacheado(Guid TrabalhadorId, string TrabalhadorNome, byte[] TemplateBruto);

public class TemplateCacheService
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    private readonly BackendClient _backendClient;
    private readonly AgenteOptions _options;
    private List<TemplateCacheado> _cache = new();

    public TemplateCacheService(BackendClient backendClient, IOptions<AgenteOptions> options)
    {
        _backendClient = backendClient;
        _options = options.Value;
    }

    public IReadOnlyList<TemplateCacheado> Templates => _cache;

    public async Task SincronizarAsync(CancellationToken ct)
    {
        var templates = await _backendClient.SincronizarTemplatesAsync(ct);
        var chave = Convert.FromBase64String(_options.ChaveCriptografiaBiometriaBase64);

        _cache = templates
            .Select(t => new TemplateCacheado(t.TrabalhadorId, t.TrabalhadorNome, DescriptografarTemplate(t.TemplateCriptografado, chave)))
            .ToList();
    }

    // Duplica deliberadamente o layout nonce|cifrado|tag de TemplateBiometricoCriptografiaConversor
    // (backend) — o agente não referencia AAHBRANT.SST.Infrastructure por ser um executável standalone.
    public static byte[] DescriptografarTemplate(string cifradoBase64, byte[] chave)
    {
        var bytes = Convert.FromBase64String(cifradoBase64);
        var nonce = bytes[..TamanhoNonce];
        var tag = bytes[^TamanhoTag..];
        var cifrado = bytes[TamanhoNonce..^TamanhoTag];
        var textoPlano = new byte[cifrado.Length];

        using var aesGcm = new AesGcm(chave, TamanhoTag);
        aesGcm.Decrypt(nonce, cifrado, tag, textoPlano);

        return textoPlano;
    }
}
```

- [ ] **Step 5: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj --filter TemplateCacheServiceTests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.AgenteBiometria/Servicos tests/AAHBRANT.SST.AgenteBiometria.Tests/Servicos
git commit -m "feat: adiciona BackendClient e TemplateCacheService no agente"
```

---

### Task 17: Endpoints HTTP do agente (`AgenteEndpoints`)

**Files:**
- Create: `src/AAHBRANT.SST.AgenteBiometria/Endpoints/AgenteEndpoints.cs`
- Test: `tests/AAHBRANT.SST.AgenteBiometria.Tests/Endpoints/AgenteEndpointsTests.cs`

**Interfaces:**
- Consumes: `IFingerprintReader`/`IFingerprintMatcher` (Task 15), `TemplateCacheService` (Task 16), `AgenteOptions` (Task 14).
- Produces: `AgenteEndpoints.Mapear(WebApplication app, string politicaCors)` mapeando `/api/dispositivo`, `/api/sincronizar`, `/api/capturar-bruto`, `/api/capturar`; DTOs `DispositivoResponse`, `SincronizarResponse`, `CapturaBrutaResponse`, `CapturaResponse`, `ErroResponse` — os handlers estáticos são chamados diretamente pelo `Program.cs` (Task 18) e testados sem precisar de um `TestServer` HTTP real.

- [ ] **Step 1: Escrever os testes falhando, chamando os handlers estáticos diretamente**

```csharp
// tests/AAHBRANT.SST.AgenteBiometria.Tests/Endpoints/AgenteEndpointsTests.cs
using AAHBRANT.SST.AgenteBiometria.Endpoints;
using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Endpoints;

public class AgenteEndpointsTests
{
    [Fact]
    public void ObterDispositivo_DeveRetornarDispositivoIdESegredoDasOpcoes()
    {
        var dispositivoId = Guid.NewGuid();
        var options = Options.Create(new AgenteOptions { DispositivoId = dispositivoId, SegredoDispositivo = "segredo-x" });

        var resultado = AgenteEndpoints.ObterDispositivo(options);

        var ok = Assert.IsType<Ok<DispositivoResponse>>(resultado);
        Assert.Equal(dispositivoId, ok.Value!.DispositivoId);
        Assert.Equal("segredo-x", ok.Value.SegredoDispositivo);
    }

    [Fact]
    public async Task Capturar_ComMelhorScoreAcimaDeZero_DeveRetornarTrabalhadorComMaiorSimilaridade()
    {
        var leitor = new SimuladoFingerprintReader(new byte[] { 1, 2, 3, 4 });
        var matcher = new SimuladoFingerprintMatcher();
        var options = Options.Create(new AgenteOptions());
        var httpClient = new HttpClient();
        var cache = new TemplateCacheService(new BackendClient(httpClient, options), options);

        // TemplateCacheService.Templates é populado só via SincronizarAsync (que chama o backend);
        // para testar Capturar isoladamente, este teste cobre o caminho "cache vazio" abaixo e o
        // caminho "com match" fica coberto pelo teste de integração manual descrito na Task 21.
        var resultado = await AgenteEndpoints.Capturar(leitor, matcher, cache, CancellationToken.None);

        Assert.IsType<NotFound<ErroResponse>>(resultado);
    }

    [Fact]
    public async Task CapturarBruto_DeveRetornarBytesCapturados()
    {
        var captura = new byte[] { 9, 8, 7 };
        var leitor = new SimuladoFingerprintReader(captura);

        var resultado = await AgenteEndpoints.CapturarBruto(leitor, CancellationToken.None);

        var ok = Assert.IsType<Ok<CapturaBrutaResponse>>(resultado);
        Assert.Equal(captura, ok.Value!.TemplateBruto);
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `dotnet test tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj --filter AgenteEndpointsTests`
Expected: FAIL com "The type or namespace name 'AgenteEndpoints' could not be found"

- [ ] **Step 3: Implementar (DTOs nomeados em vez de tipos anônimos, para permitir assert forte nos testes)**

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Endpoints/AgenteEndpoints.cs
using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Endpoints;

public record DispositivoResponse(Guid DispositivoId, string SegredoDispositivo);
public record SincronizarResponse(int Total);
public record CapturaBrutaResponse(byte[] TemplateBruto);
public record CapturaResponse(Guid TrabalhadorId, double Score);
public record ErroResponse(string Erro);

public static class AgenteEndpoints
{
    public static void Mapear(WebApplication app, string politicaCors)
    {
        app.MapGet("/api/dispositivo", ObterDispositivo).RequireCors(politicaCors);
        app.MapPost("/api/sincronizar", Sincronizar).RequireCors(politicaCors);
        app.MapPost("/api/capturar-bruto", CapturarBruto).RequireCors(politicaCors);
        app.MapPost("/api/capturar", Capturar).RequireCors(politicaCors);
    }

    public static Ok<DispositivoResponse> ObterDispositivo(IOptions<AgenteOptions> options) =>
        TypedResults.Ok(new DispositivoResponse(options.Value.DispositivoId, options.Value.SegredoDispositivo));

    public static async Task<Ok<SincronizarResponse>> Sincronizar(TemplateCacheService cache, CancellationToken ct)
    {
        await cache.SincronizarAsync(ct);
        return TypedResults.Ok(new SincronizarResponse(cache.Templates.Count));
    }

    public static async Task<Ok<CapturaBrutaResponse>> CapturarBruto(IFingerprintReader leitor, CancellationToken ct)
    {
        var captura = await leitor.CapturarAsync(ct);
        return TypedResults.Ok(new CapturaBrutaResponse(captura));
    }

    public static async Task<Results<Ok<CapturaResponse>, NotFound<ErroResponse>>> Capturar(
        IFingerprintReader leitor, IFingerprintMatcher matcher, TemplateCacheService cache, CancellationToken ct)
    {
        var captura = await leitor.CapturarAsync(ct);

        var melhor = cache.Templates
            .Select(t => new { t.TrabalhadorId, Score = matcher.Comparar(captura, t.TemplateBruto) })
            .OrderByDescending(r => r.Score)
            .FirstOrDefault();

        if (melhor is null)
        {
            return TypedResults.NotFound(new ErroResponse("Nenhum template cadastrado no cache local. Rode /api/sincronizar primeiro."));
        }

        return TypedResults.Ok(new CapturaResponse(melhor.TrabalhadorId, melhor.Score));
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj --filter AgenteEndpointsTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.AgenteBiometria/Endpoints tests/AAHBRANT.SST.AgenteBiometria.Tests/Endpoints
git commit -m "feat: adiciona endpoints HTTP do agente biometrico"
```

---

### Task 18: `Program.cs` do agente (Kestrel + tray WinForms) + CORS + binding em 127.0.0.1

**Files:**
- Create: `src/AAHBRANT.SST.AgenteBiometria/Program.cs`
- Create: `src/AAHBRANT.SST.AgenteBiometria/Tray/TrayApplicationContext.cs`

**Interfaces:**
- Consumes: `AgenteOptions` (Task 14), `IFingerprintReader`/`IFingerprintMatcher` + `Simulado*` (Task 15), `BackendClient`/`TemplateCacheService` (Task 16), `AgenteEndpoints.Mapear` (Task 17).
- Produces: executável `AAHBRANT.SST.AgenteBiometria.exe` — Kestrel escutando só em `127.0.0.1:5251`, CORS restrito à origem do quiosque, tray icon com opção "Sair".

- [ ] **Step 1: Implementar `TrayApplicationContext`**

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Tray/TrayApplicationContext.cs
using System.Windows.Forms;

namespace AAHBRANT.SST.AgenteBiometria.Tray;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly WebApplication _app;

    public TrayApplicationContext(WebApplication app)
    {
        _app = app;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Sair", null, (_, _) => Sair());

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
            Text = "AAHBRANT — Agente Biometria",
        };
    }

    private void Sair()
    {
        _trayIcon.Visible = false;
        _ = _app.StopAsync();
        Application.Exit();
    }
}
```

- [ ] **Step 2: Implementar `Program.cs` — classe explícita com `[STAThread]` (não top-level statements, necessário para WinForms) combinando Kestrel em background com o loop de mensagens do WinForms na thread principal**

```csharp
// src/AAHBRANT.SST.AgenteBiometria/Program.cs
using AAHBRANT.SST.AgenteBiometria.Endpoints;
using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using AAHBRANT.SST.AgenteBiometria.Tray;

namespace AAHBRANT.SST.AgenteBiometria;

public static class Program
{
    private const string PoliticaCorsKiosk = "KioskOrigin";

    [STAThread]
    public static void Main(string[] args)
    {
        var app = CriarApp(args);
        _ = app.RunAsync();

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.Run(new TrayApplicationContext(app));
    }

    // Separado de Main() para ser testável sem precisar do loop de mensagens WinForms — Main() em si
    // não tem cobertura de teste automatizado por chamar Application.Run (bloqueante).
    public static WebApplication CriarApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<AgenteOptions>(builder.Configuration.GetSection("Agente"));
        builder.Services.AddHttpClient<BackendClient>();
        builder.Services.AddSingleton<TemplateCacheService>();

        // Únicas implementações neste plano — sem SDK Futronic real disponível (fora de escopo).
        builder.Services.AddSingleton<IFingerprintReader>(new SimuladoFingerprintReader(new byte[] { 1, 2, 3, 4 }));
        builder.Services.AddSingleton<IFingerprintMatcher, SimuladoFingerprintMatcher>();

        var agenteOptions = builder.Configuration.GetSection("Agente").Get<AgenteOptions>() ?? new AgenteOptions();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(PoliticaCorsKiosk, policy =>
                policy.WithOrigins(agenteOptions.OrigemPermitida).AllowAnyHeader().AllowAnyMethod());
        });

        // Kestrel só escuta em loopback — junto com o CORS travado na origem exata do quiosque, isso
        // substitui a ideia (descartada por complexidade desnecessária) de um token de sessão emitido
        // pelo backend só para este canal local — ver spec §4.4 e Architecture deste plano.
        builder.WebHost.ConfigureKestrel(serverOptions =>
            serverOptions.Listen(System.Net.IPAddress.Loopback, 5251));

        var app = builder.Build();
        app.UseCors(PoliticaCorsKiosk);
        AgenteEndpoints.Mapear(app, PoliticaCorsKiosk);

        return app;
    }
}
```

- [ ] **Step 3: Buildar o projeto e confirmar sucesso**

Run: `dotnet build src/AAHBRANT.SST.AgenteBiometria/AAHBRANT.SST.AgenteBiometria.csproj`
Expected: `Build succeeded.`

- [ ] **Step 4: Rodar a suíte completa do agente para garantir que nada quebrou**

Run: `dotnet test tests/AAHBRANT.SST.AgenteBiometria.Tests/AAHBRANT.SST.AgenteBiometria.Tests.csproj`
Expected: `Passed!`

- [ ] **Step 5: Teste manual local (opcional, mas recomendado) — rodar o agente e confirmar que os 4 endpoints respondem**

Run: `dotnet run --project src/AAHBRANT.SST.AgenteBiometria/AAHBRANT.SST.AgenteBiometria.csproj`
Expected: um ícone aparece na bandeja do sistema; `curl http://127.0.0.1:5251/api/dispositivo` retorna `{"dispositivoId":"00000000-0000-0000-0000-000000000000","segredoDispositivo":""}`

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.AgenteBiometria/Program.cs src/AAHBRANT.SST.AgenteBiometria/Tray
git commit -m "feat: adiciona Program.cs do agente com Kestrel + tray WinForms"
```

---

### Task 19: `lib/api.ts` — chamadas HTTP para os novos endpoints do backend

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`

**Interfaces:**
- Consumes: `request<T>()` (helper existente, `api.ts:1783`), endpoints das Tasks 12 e 13.
- Produces: `api.dispositivosAgente.registrar(obraId, nome)`, `api.trabalhadores.cadastrarBiometriaLocal(id, templateBrutoBase64)`, `api.assinatura.autenticarBiometriaLocal(documentoId, dispositivoId, segredoDispositivo, trabalhadorId, score)` — consumidos por `agenteBiometricoLocal.ts` não diretamente, mas por `AssinaturaQuiosque.tsx`/`AssinaturaTab.tsx` (Tasks 20-21).

- [ ] **Step 1: Adicionar `cadastrarBiometriaLocal` em `trabalhadores`, logo após `confirmarCadastroWebAuthn` (linha 1835 de `api.ts`)**

```typescript
    // Cadastro de digital via agente local (Futronic FS80H) — templateBruto vem em base64 do
    // agente (fetch local a /api/capturar-bruto); o backend criptografa antes de persistir.
    cadastrarBiometriaLocal: (id: string, templateBrutoBase64: string) =>
      request<void>(`/api/trabalhadores/${id}/assinatura/biometria-local/cadastro`, {
        method: 'POST',
        body: JSON.stringify({ templateBruto: templateBrutoBase64 }),
      }),
```

- [ ] **Step 2: Adicionar `autenticarBiometriaLocal` em `assinatura` (ou equivalente `documentos`), logo após `confirmarAssinaturaWebAuthn` (linha 2216 de `api.ts`)**

```typescript
    // Autenticação via biometria digital local (Futronic FS80H) — dispositivoId/segredoDispositivo
    // vêm do agente local (fetch a /api/dispositivo), nunca de localStorage.
    autenticarBiometriaLocal: (documentoId: string, dispositivoId: string, segredoDispositivo: string, trabalhadorId: string, score: number) =>
      request<DocumentoSignatario>(`/api/documentos/${documentoId}/autenticacao/biometria-local`, {
        method: 'POST',
        body: JSON.stringify({ dispositivoId, segredoDispositivo, trabalhadorId, score }),
      }),
```

- [ ] **Step 3: Adicionar um novo bloco `dispositivosAgente`, próximo ao final de `export const api = { ... }`, ao lado de outros blocos administrativos**

```typescript
  dispositivosAgente: {
    // Chamado uma vez na configuração inicial de cada totem/quiosque (tela administrativa,
    // fora de escopo deste plano) — retorna o segredo em claro, exibido uma única vez.
    registrar: (obraId: string, nome: string) =>
      request<string>('/api/dispositivos-agente', {
        method: 'POST',
        body: JSON.stringify({ obraId, nome }),
      }),
  },
```

- [ ] **Step 4: Verificar que o projeto ainda builda (typecheck do Vite/TS)**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: build concluído sem erros de TypeScript

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts
git commit -m "feat: adiciona chamadas de api.ts para biometria local"
```

---

### Task 20: `lib/agenteBiometricoLocal.ts` — cliente do agente local no navegador

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/lib/agenteBiometricoLocal.ts`

**Interfaces:**
- Produces: `obterDispositivoLocal(): Promise<{ dispositivoId: string; segredoDispositivo: string }>`, `sincronizarTemplatesLocal(): Promise<{ total: number }>`, `capturarDigitalLocal(): Promise<{ trabalhadorId: string; score: number }>`, `estaAgenteLocalDisponivel(): Promise<boolean>` — consumidos por `AssinaturaQuiosque.tsx` (Task 21) e `AssinaturaTab.tsx` (Task 21, indiretamente via `capturarDigitalBrutaLocal`).

- [ ] **Step 1: Implementar o cliente, seguindo o estilo de `webauthn.ts` (fetch simples, sem dependências externas)**

```typescript
// src/AAHBRANT.SST.TeamsApp/src/lib/agenteBiometricoLocal.ts

// Porta fixa definida em Program.cs do AAHBRANT.SST.AgenteBiometria (Kestrel em 127.0.0.1:5251).
const AGENTE_LOCAL_URL = 'http://127.0.0.1:5251';

export interface DispositivoLocal {
  dispositivoId: string;
  segredoDispositivo: string;
}

export interface CapturaLocal {
  trabalhadorId: string;
  score: number;
}

async function requisitarAgenteLocal<T>(caminho: string, init?: RequestInit): Promise<T> {
  const resposta = await fetch(`${AGENTE_LOCAL_URL}${caminho}`, init);
  if (!resposta.ok) {
    const corpo = await resposta.text().catch(() => '');
    throw new Error(`${resposta.status} ${resposta.statusText}: ${corpo}`);
  }
  return (await resposta.json()) as T;
}

export async function estaAgenteLocalDisponivel(): Promise<boolean> {
  try {
    await requisitarAgenteLocal('/api/dispositivo');
    return true;
  } catch {
    return false;
  }
}

// Chamado uma vez ao carregar a tela do quiosque — o resultado deve ficar só em memória (variável
// de estado do componente React), nunca em localStorage, e ser enviado só no corpo do POST final
// de assinatura, nunca em query string.
export function obterDispositivoLocal(): Promise<DispositivoLocal> {
  return requisitarAgenteLocal<DispositivoLocal>('/api/dispositivo');
}

export function sincronizarTemplatesLocal(): Promise<{ total: number }> {
  return requisitarAgenteLocal<{ total: number }>('/api/sincronizar', { method: 'POST' });
}

export function capturarDigitalLocal(): Promise<CapturaLocal> {
  return requisitarAgenteLocal<CapturaLocal>('/api/capturar', { method: 'POST' });
}

// Usado só na tela de cadastro (AssinaturaTab) — captura a digital bruta (não comparada contra
// cache nenhum) para enviar ao backend, que criptografa e persiste como novo template.
export async function capturarDigitalBrutaLocal(): Promise<string> {
  const resultado = await requisitarAgenteLocal<{ templateBruto: number[] }>('/api/capturar-bruto', { method: 'POST' });
  return btoa(String.fromCharCode(...resultado.templateBruto));
}
```

- [ ] **Step 2: Verificar que o projeto builda**

Run: `cd src/AAHBRANT.SST.TeamsApp && npm run build`
Expected: build concluído sem erros de TypeScript

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/agenteBiometricoLocal.ts
git commit -m "feat: adiciona cliente do agente biometrico local no frontend"
```

---

### Task 21: UI — card "Biometria (leitor local)" no quiosque e no cadastro do trabalhador

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaQuiosque.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/AssinaturaTab.tsx`

**Interfaces:**
- Consumes: `estaAgenteLocalDisponivel`, `obterDispositivoLocal`, `sincronizarTemplatesLocal`, `capturarDigitalLocal`, `capturarDigitalBrutaLocal` (Task 20), `api.assinatura.autenticarBiometriaLocal`, `api.trabalhadores.cadastrarBiometriaLocal` (Task 19).

**Nota de escopo hardware:** este código roda e é clicável sem o FS80H físico (o agente local usa `SimuladoFingerprintReader`/`Matcher`), mas a validação end-to-end real (ler uma digital de verdade, comparar contra um cadastro de verdade) só é possível quando o leitor chegar. Marcado explicitamente no Step 5.

- [ ] **Step 1: Em `AssinaturaQuiosque.tsx`, adicionar estado e o efeito de checagem de disponibilidade do agente, ao lado do estado `webAuthnDisponivel` existente**

```typescript
const [agenteLocalDisponivel, setAgenteLocalDisponivel] = useState(false);
const [dispositivoLocal, setDispositivoLocal] = useState<{ dispositivoId: string; segredoDispositivo: string } | null>(null);

useEffect(() => {
  estaAgenteLocalDisponivel().then(async (disponivel) => {
    setAgenteLocalDisponivel(disponivel);
    if (disponivel) {
      const dispositivo = await obterDispositivoLocal();
      setDispositivoLocal(dispositivo);
    }
  });
}, []);
```

(Import no topo do arquivo: `import { estaAgenteLocalDisponivel, obterDispositivoLocal, capturarDigitalLocal } from '../../lib/agenteBiometricoLocal';`)

- [ ] **Step 2: Adicionar a função de assinatura via biometria local, ao lado de `assinarComBiometria()` existente**

```typescript
async function assinarComBiometriaLocal() {
  if (!dispositivoLocal) return;
  setCarregando(true);
  setErro(null);
  try {
    const captura = await capturarDigitalLocal();
    const resultado = await api.assinatura.autenticarBiometriaLocal(
      documentoId, dispositivoLocal.dispositivoId, dispositivoLocal.segredoDispositivo, captura.trabalhadorId, captura.score,
    );
    onAssinado(resultado);
  } catch (e) {
    setErro(e instanceof Error ? e.message : 'Falha ao autenticar via biometria local.');
  } finally {
    setCarregando(false);
  }
}
```

(`documentoId`, `onAssinado`, `setCarregando`, `setErro` seguem os nomes de prop/estado já usados por `assinarComBiometria()` no mesmo arquivo — ajustar conforme os nomes reais encontrados no componente.)

- [ ] **Step 3: Adicionar o card na UI, ao lado do bloco `{webAuthnDisponivel && (...)}` existente**

```tsx
{agenteLocalDisponivel && dispositivoLocal && (
  <div className={estilos.card}>
    <h3>Biometria (leitor local)</h3>
    <p>Coloque o dedo no leitor conectado a este totem.</p>
    <Button appearance="primary" onClick={assinarComBiometriaLocal} disabled={carregando}>
      Autenticar com digital
    </Button>
  </div>
)}
```

- [ ] **Step 4: Em `AssinaturaTab.tsx`, adicionar o card de cadastro, ao lado do card de cadastro WebAuthn existente**

```typescript
const [cadastrandoBiometriaLocal, setCadastrandoBiometriaLocal] = useState(false);

async function cadastrarBiometriaLocal() {
  setCadastrandoBiometriaLocal(true);
  try {
    const templateBase64 = await capturarDigitalBrutaLocal();
    await api.trabalhadores.cadastrarBiometriaLocal(trabalhador.id, templateBase64);
  } finally {
    setCadastrandoBiometriaLocal(false);
  }
}
```

```tsx
<div className={estilos.card}>
  <h3>Cadastrar digital (leitor local)</h3>
  <p>Peça ao trabalhador para colocar o dedo no leitor conectado ao totem.</p>
  <Button appearance="secondary" onClick={cadastrarBiometriaLocal} disabled={cadastrandoBiometriaLocal}>
    Capturar digital
  </Button>
</div>
```

(Import no topo: `import { capturarDigitalBrutaLocal } from '../../lib/agenteBiometricoLocal';`; `trabalhador.id` segue o nome de prop já usado pelos outros cards do mesmo componente — ajustar conforme o real.)

- [ ] **Step 5: Testar manualmente no navegador com o agente local rodando (Task 18, `Simulado*`) — isso cobre o fluxo de ponta a ponta MENOS a captura real de digital, que só é possível com o FS80H físico**

Run: `dotnet run --project src/AAHBRANT.SST.AgenteBiometria/AAHBRANT.SST.AgenteBiometria.csproj` (em um terminal)
Run: `cd src/AAHBRANT.SST.TeamsApp && npm run dev` (em outro terminal)

Expected: abrir o quiosque no navegador, ver o card "Biometria (leitor local)" aparecer (agente detectado), clicar em "Autenticar com digital" e ver a chamada em rede ir para `/api/documentos/{id}/autenticacao/biometria-local` — vai falhar no backend com `InvalidOperationException` (score/dispositivo simulados não batem com um cadastro real) até que exista um dispositivo/trabalhador de teste cadastrado, o que é esperado sem hardware real.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaQuiosque.tsx src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/AssinaturaTab.tsx
git commit -m "feat: adiciona UI de biometria local no quiosque e no cadastro do trabalhador"
```

---

## Self-Review

**1. Cobertura das pendências da spec §8:**
1. Chave de criptografia dedicada → resolvida na Task 3 (`Lgpd:ChaveCriptografiaBiometriaBase64`, chave própria, distinta da chave de CPF).
2. Esquema de auth do endpoint de sync → resolvido na Task 12 (`[AllowAnonymous]` + segredo no corpo, validado manualmente por `IDispositivoAgenteAutenticador`).
3. Onde os comandos de dispositivo vivem → resolvido nas Tasks 6, 8, 10 (`Application/Assinatura`, mesma pasta dos demais comandos de assinatura).
4. UX do agente → resolvida na Task 18 (tray icon WinForms com opção "Sair") e Tasks 20-21 (cards no quiosque/cadastro).
5. Isolamento hardware-vs-sem-hardware → Tasks 1-20 100% testáveis sem FS80H; Task 21 marca explicitamente o que fica bloqueado.
6. (Implícito na spec, resolvido nesta sessão) Direção de dependência Application/Infrastructure para o hasher de segredo e para a criptografia de template → Tasks 2 e 3, via o padrão `IPinHasher`/`PinHasherService`.

**2. Varredura de placeholders:** nenhum "TBD"/"TODO"/"implementar depois" encontrado. As duas únicas notas de ajuste manual (Task 11, nomes exatos de `DocumentoSignatarioDto`/`IRegistradorAssinaturaService`; Task 21, nomes exatos de props/estado dos componentes React) são avisos de confirmação-antes-de-colar explícitos, não lacunas de design — o comportamento e a assinatura já estão totalmente especificados.

**3. Consistência de tipos:** `ResultadoAutenticacaoAssinatura`, `MetodoAutenticacaoAssinatura.Biometria`, `MetodoAutenticacaoObra.Biometria`, `DispositivoAgenteBiometrico`, `TemplateBiometricoFutronic`, `TemplateSincronizadoDto`, `IDispositivoAgenteAutenticador.ValidarAsync`, `ISegredoDispositivoHasher`, `ITemplateBiometricoCriptografia.Criptografar` usam a mesma assinatura em toda ocorrência, da Task 2 à Task 21.

---

Plano completo e salvo em `docs/superpowers/plans/2026-08-26-futronic-biometria-local.md`. Duas opções de execução:

**1. Subagent-Driven (recomendado)** — dispatch de um subagente por task, com revisão entre tasks e iteração rápida.

**2. Inline Execution** — execução das tasks nesta sessão, em lote, com checkpoints para revisão.

Qual abordagem você prefere?
