# Fase 1 — Dados Mocados da Obra "Edifício Aurora Corporate" — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Popular o banco local de desenvolvimento com uma obra fictícia completa (20 pavimentos, ~200 trabalhadores) usando somente entidades já existentes no sistema, para servir de massa de teste realista às fases seguintes do roadmap de SST.

**Architecture:** Um seeder estático `MockObraSeeder` (mesmo padrão de `RbacSeeder`/`RegraAlertaSeeder`), dividido em arquivos parciais por responsabilidade (dados estáticos, estrutura organizacional, treinamentos/ASO, NC/EPI), chamado em `Program.cs` só quando `IsDevelopment()`. Duas peças de lógica pura e testável (geração de CPF fictício válido, distribuição de datas de vencimento) ficam na camada `Application` e são cobertas por TDD; o restante é montagem de grafo EF Core sem lógica de negócio nova, verificado manualmente (mesmo padrão dos seeders existentes, nenhum dos quais tem teste automatizado).

**Tech Stack:** .NET 8, EF Core (SQL Server), xUnit.

**Spec:** [docs/superpowers/specs/2026-08-26-fase1-dados-mock-obra-design.md](../specs/2026-08-26-fase1-dados-mock-obra-design.md)

## Global Constraints

- O seeder só roda quando `app.Environment.IsDevelopment()` é verdadeiro — nunca em homologação/produção.
- Nenhuma entidade nova é criada — só instâncias das entidades já existentes: `Obra`, `AreaSst`, `Funcao`, `Setor`, `Equipe`, `Trabalhador`, `CursoTreinamento`, `Treinamento`, `Aso`, `NaoConformidade`, `CatalogoEpi`, `EntregaEpi`.
- Nenhuma migration nova é necessária.
- CPF gerado deve ser fictício mas passar em `AAHBRANT.SST.Application.Common.CpfValidador.EhValido` (dígito verificador correto).
- O seeder deve ser idempotente: reexecuções (restart da API) não duplicam a obra.
- Distribuição de datas de vencimento: ~20% vencido (5–60 dias no passado), ~25% a vencer (1–30 dias no futuro), ~55% válido (31–365 dias no futuro).
- 200 trabalhadores distribuídos exatamente conforme a tabela de funções da spec (soma = 200).
- 25 não conformidades distribuídas conforme a spec (Aberta=8, EmTratamento=7, AguardandoValidacao=4, Encerrada=6).

---

## Task 1: Gerador de CPF fictício válido

**Files:**
- Create: `src/AAHBRANT.SST.Application/Common/GeradorCpfFicticio.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Common/GeradorCpfFicticioTests.cs`

**Interfaces:**
- Consumes: nada (função pura).
- Produces: `GeradorCpfFicticio.Gerar(int indiceSequencial) : string` — usado pela Task 4 para atribuir `Trabalhador.Cpf`.

- [ ] **Step 1: Write the failing test**

```csharp
using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.Tests.Common;

public class GeradorCpfFicticioTests
{
    [Fact]
    public void Gerar_DeveRetornarOnzeDigitosNumericos()
    {
        var cpf = GeradorCpfFicticio.Gerar(0);

        Assert.Equal(11, cpf.Length);
        Assert.All(cpf, char.IsDigit);
    }

    [Fact]
    public void Gerar_DeveRetornarCpfComDigitoVerificadorValido()
    {
        for (var indice = 0; indice < 250; indice++)
        {
            var cpf = GeradorCpfFicticio.Gerar(indice);
            Assert.True(CpfValidador.EhValido(cpf), $"CPF inválido para índice {indice}: {cpf}");
        }
    }

    [Fact]
    public void Gerar_NaoDeveColidirParaIndicesDiferentes()
    {
        var cpfs = Enumerable.Range(0, 250).Select(GeradorCpfFicticio.Gerar).ToList();

        Assert.Equal(cpfs.Count, cpfs.Distinct().Count());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter GeradorCpfFicticioTests`
Expected: FAIL with build error "The type or namespace name 'GeradorCpfFicticio' could not be found"

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace AAHBRANT.SST.Application.Common;

// Gera CPFs fictícios (nunca de pessoa real) com dígito verificador matematicamente válido,
// para popular dados de teste/desenvolvimento sem quebrar CpfValidador.EhValido em nenhuma tela.
// Faixa reservada 900000000-999999999 nos 9 primeiros dígitos — fora da faixa historicamente
// emitida pela Receita Federal, para deixar claro que não é um CPF real ainda que válido.
public static class GeradorCpfFicticio
{
    public static string Gerar(int indiceSequencial)
    {
        var baseNumerica = 900_000_000 + (indiceSequencial % 99_999_999);
        var noveDigitos = baseNumerica.ToString("D9");
        var digitos = noveDigitos.Select(c => c - '0').ToArray();

        var primeiroDigito = CalcularDigitoVerificador(digitos, 9);
        var digitosComPrimeiro = digitos.Append(primeiroDigito).ToArray();
        var segundoDigito = CalcularDigitoVerificador(digitosComPrimeiro, 10);

        return noveDigitos + primeiroDigito + segundoDigito;
    }

    private static int CalcularDigitoVerificador(int[] digitos, int quantidade)
    {
        var soma = 0;
        var multiplicador = quantidade + 1;
        for (var i = 0; i < quantidade; i++)
            soma += digitos[i] * multiplicador--;

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter GeradorCpfFicticioTests`
Expected: PASS (3 testes)

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Common/GeradorCpfFicticio.cs tests/AAHBRANT.SST.Application.Tests/Common/GeradorCpfFicticioTests.cs
git commit -m "feat: gerador de CPF ficticio valido para dados mocados"
```

---

## Task 2: Distribuidor de faixas de vencimento

**Files:**
- Create: `src/AAHBRANT.SST.Application/Common/DistribuidorFaixaVencimento.cs`
- Test: `tests/AAHBRANT.SST.Application.Tests/Common/DistribuidorFaixaVencimentoTests.cs`

**Interfaces:**
- Consumes: nada (função pura).
- Produces: `DistribuidorFaixaVencimento.Faixa` (enum: `Vencido`, `AVencerEmBreve`, `Valido`); `DistribuidorFaixaVencimento.ObterFaixa(int indice) : Faixa`; `DistribuidorFaixaVencimento.CalcularData(int indice, DateTime referenciaUtc) : DateTime` — usado pelas Tasks 5 e 6 para gerar `DataValidade`/`Prazo`.

- [ ] **Step 1: Write the failing test**

```csharp
using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.Tests.Common;

public class DistribuidorFaixaVencimentoTests
{
    [Fact]
    public void ObterFaixa_DistribuicaoEmCadaBlocoDeVinte_DeveSeguirProporcaoDaSpec()
    {
        var faixas = Enumerable.Range(0, 20).Select(DistribuidorFaixaVencimento.ObterFaixa).ToList();

        Assert.Equal(4, faixas.Count(f => f == DistribuidorFaixaVencimento.Faixa.Vencido));
        Assert.Equal(5, faixas.Count(f => f == DistribuidorFaixaVencimento.Faixa.AVencerEmBreve));
        Assert.Equal(11, faixas.Count(f => f == DistribuidorFaixaVencimento.Faixa.Valido));
    }

    [Fact]
    public void CalcularData_ParaFaixaVencida_DeveRetornarDataNoPassadoDentroDoIntervaloDaSpec()
    {
        var referencia = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        var indicesVencidos = Enumerable.Range(0, 20)
            .Where(i => DistribuidorFaixaVencimento.ObterFaixa(i) == DistribuidorFaixaVencimento.Faixa.Vencido);

        foreach (var indice in indicesVencidos)
        {
            var data = DistribuidorFaixaVencimento.CalcularData(indice, referencia);
            var diasNoPassado = (referencia - data).TotalDays;
            Assert.InRange(diasNoPassado, 5, 60);
        }
    }

    [Fact]
    public void CalcularData_ParaFaixaAVencerEmBreve_DeveRetornarDataFuturaDentroDeTrintaDias()
    {
        var referencia = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        var indices = Enumerable.Range(0, 20)
            .Where(i => DistribuidorFaixaVencimento.ObterFaixa(i) == DistribuidorFaixaVencimento.Faixa.AVencerEmBreve);

        foreach (var indice in indices)
        {
            var data = DistribuidorFaixaVencimento.CalcularData(indice, referencia);
            var diasNoFuturo = (data - referencia).TotalDays;
            Assert.InRange(diasNoFuturo, 1, 30);
        }
    }

    [Fact]
    public void CalcularData_ParaFaixaValida_DeveRetornarDataFuturaAlemDeTrintaDias()
    {
        var referencia = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        var indices = Enumerable.Range(0, 20)
            .Where(i => DistribuidorFaixaVencimento.ObterFaixa(i) == DistribuidorFaixaVencimento.Faixa.Valido);

        foreach (var indice in indices)
        {
            var data = DistribuidorFaixaVencimento.CalcularData(indice, referencia);
            var diasNoFuturo = (data - referencia).TotalDays;
            Assert.InRange(diasNoFuturo, 31, 365);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter DistribuidorFaixaVencimentoTests`
Expected: FAIL with build error "The type or namespace name 'DistribuidorFaixaVencimento' could not be found"

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace AAHBRANT.SST.Application.Common;

// Distribui datas de vencimento (treinamento/ASO/prazo de NC) em 3 faixas determinísticas por
// índice — nunca aleatório, para o seeder de dados mocados ser 100% reprodutível e testável.
// Proporção pedida na spec da Fase 1: ~20% vencido, ~25% a vencer em breve, ~55% válido.
public static class DistribuidorFaixaVencimento
{
    public enum Faixa
    {
        Vencido,
        AVencerEmBreve,
        Valido,
    }

    public static Faixa ObterFaixa(int indice)
    {
        var posicao = ((indice % 20) + 20) % 20;
        if (posicao < 4) return Faixa.Vencido;         // 4/20 = 20%
        if (posicao < 9) return Faixa.AVencerEmBreve;  // 5/20 = 25%
        return Faixa.Valido;                             // 11/20 = 55%
    }

    public static DateTime CalcularData(int indice, DateTime referenciaUtc)
    {
        var variacao = ((indice % 10) + 10) % 10; // 0-9, varia a data dentro da faixa

        return ObterFaixa(indice) switch
        {
            Faixa.Vencido => referenciaUtc.AddDays(-(5 + variacao * 6)),          // 5 a 59 dias no passado
            Faixa.AVencerEmBreve => referenciaUtc.AddDays(1 + variacao * 3),      // 1 a 28 dias no futuro
            Faixa.Valido => referenciaUtc.AddDays(31 + variacao * 33),            // 31 a 328 dias no futuro
            _ => referenciaUtc,
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AAHBRANT.SST.Application.Tests --filter DistribuidorFaixaVencimentoTests`
Expected: PASS (4 testes)

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Application/Common/DistribuidorFaixaVencimento.cs tests/AAHBRANT.SST.Application.Tests/Common/DistribuidorFaixaVencimentoTests.cs
git commit -m "feat: distribuidor deterministico de faixas de vencimento para dados mocados"
```

---

## Task 3: Dados estáticos do seeder (funções, cursos, EPIs, NCs, nomes fictícios)

**Files:**
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.DadosEstaticos.cs`
- Test: `tests/AAHBRANT.SST.Api.IntegrationTests/Persistencia/Seed/MockObraSeederDadosEstaticosTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces (todos `public static readonly`/`public static` na `partial class MockObraSeeder`, consumidos pelas Tasks 4–6):
  - `DistribuicaoFuncoes : (string Funcao, int Quantidade, string[] CodigosCursos)[]`
  - `CatalogoCursosNr : (string Codigo, string Nome, string NormaReferencia, int CargaHorariaMinima, int ValidadeEmMeses)[]`
  - `CatalogoEpisPadrao : (string Nome, string Fabricante, string CertificadoAprovacaoNumero, int VidaUtilEmMeses, int SaldoEstoque)[]`
  - `DistribuicaoNaoConformidades : (AAHBRANT.SST.Domain.Enums.StatusNaoConformidade Status, int Quantidade)[]`
  - `GerarNome(int indice) : string`

- [ ] **Step 1: Write the failing test**

```csharp
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;

namespace AAHBRANT.SST.Api.IntegrationTests.Persistencia.Seed;

public class MockObraSeederDadosEstaticosTests
{
    [Fact]
    public void DistribuicaoFuncoes_DeveSomarDuzentosTrabalhadores()
    {
        var total = MockObraSeeder.DistribuicaoFuncoes.Sum(f => f.Quantidade);

        Assert.Equal(200, total);
    }

    [Fact]
    public void DistribuicaoNaoConformidades_DeveSomarVinteECincoRegistros()
    {
        var total = MockObraSeeder.DistribuicaoNaoConformidades.Sum(n => n.Quantidade);

        Assert.Equal(25, total);
    }

    [Fact]
    public void CatalogoEpisPadrao_DeveTerAoMenosDoisItensComSaldoCritico()
    {
        var itensCriticos = MockObraSeeder.CatalogoEpisPadrao.Count(e => e.SaldoEstoque <= 3);

        Assert.True(itensCriticos >= 2, $"Esperado >= 2 itens com saldo <= 3, encontrado {itensCriticos}");
    }

    [Fact]
    public void CatalogoCursosNr_TodosOsCodigosUsadosEmDistribuicaoFuncoesDevemExistirNoCatalogo()
    {
        var codigosDoCatalogo = MockObraSeeder.CatalogoCursosNr.Select(c => c.Codigo).ToHashSet();
        var codigosUsados = MockObraSeeder.DistribuicaoFuncoes.SelectMany(f => f.CodigosCursos).Distinct();

        foreach (var codigo in codigosUsados)
            Assert.Contains(codigo, codigosDoCatalogo);
    }

    [Fact]
    public void GerarNome_ParaDuzentosIndices_DeveTerBaixaTaxaDeColisao()
    {
        var nomes = Enumerable.Range(0, 200).Select(MockObraSeeder.GerarNome).ToList();

        Assert.True(nomes.Distinct().Count() >= 150, "Esperado ao menos 150 nomes distintos em 200 gerados");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AAHBRANT.SST.Api.IntegrationTests --filter MockObraSeederDadosEstaticosTests`
Expected: FAIL with build error "The type or namespace name 'MockObraSeeder' could not be found"

- [ ] **Step 3: Write minimal implementation**

```csharp
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    public static readonly (string Funcao, int Quantidade, string[] CodigosCursos)[] DistribuicaoFuncoes =
    {
        ("Servente", 45, new[] { "NR-06", "NR-18" }),
        ("Pedreiro", 35, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Armador", 20, new[] { "NR-06", "NR-18", "NR-12" }),
        ("Carpinteiro", 18, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Eletricista", 12, new[] { "NR-06", "NR-10", "NR-35" }),
        ("Encanador", 10, new[] { "NR-06", "NR-18" }),
        ("Pintor", 10, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Soldador", 8, new[] { "NR-06", "NR-18", "NR-12" }),
        ("Operador de Grua/Betoneira", 8, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Mestre de Obras", 4, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Encarregado", 10, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Técnico de Segurança do Trabalho", 4, new[] { "NR-06", "NR-18", "NR-33" }),
        ("Engenheiro Civil", 6, new[] { "NR-06", "NR-18" }),
        ("Almoxarife", 3, new[] { "NR-06", "NR-11" }),
        ("Vigia/Porteiro", 7, new[] { "NR-06" }),
    };

    public static readonly (string Codigo, string Nome, string NormaReferencia, int CargaHorariaMinima, int ValidadeEmMeses)[] CatalogoCursosNr =
    {
        ("NR-06", "NR-06 Equipamento de Proteção Individual", "NR-06", 4, 24),
        ("NR-10", "NR-10 Segurança em Instalações e Serviços em Eletricidade", "NR-10", 40, 24),
        ("NR-11", "NR-11 Transporte, Movimentação, Armazenagem e Manuseio de Materiais", "NR-11", 16, 24),
        ("NR-12", "NR-12 Segurança no Trabalho em Máquinas e Equipamentos", "NR-12", 8, 12),
        ("NR-18", "NR-18 Condições e Meio Ambiente de Trabalho na Construção", "NR-18", 8, 12),
        ("NR-33", "NR-33 Segurança e Saúde nos Trabalhos em Espaços Confinados", "NR-33", 16, 12),
        ("NR-35", "NR-35 Trabalho em Altura", "NR-35", 8, 24),
    };

    public static readonly (string Nome, string Fabricante, string CertificadoAprovacaoNumero, int VidaUtilEmMeses, int SaldoEstoque)[] CatalogoEpisPadrao =
    {
        ("Capacete de Segurança Classe B", "3M", "CA-31469", 60, 40),
        ("Cinto de Segurança Tipo Paraquedista", "Talabart", "CA-38200", 36, 0),
        ("Luva de Vaqueta", "Danny", "CA-11845", 6, 120),
        ("Bota de Segurança com Bico de Aço", "Vulcabras", "CA-40129", 12, 3),
        ("Protetor Auricular Tipo Plug", "3M", "CA-5745", 4, 200),
        ("Óculos de Proteção Ampla Visão", "Steel Pro", "CA-25763", 12, 0),
        ("Máscara Respiratória PFF2", "3M", "CA-34972", 2, 500),
    };

    public static readonly (StatusNaoConformidade Status, int Quantidade)[] DistribuicaoNaoConformidades =
    {
        (StatusNaoConformidade.Aberta, 8),
        (StatusNaoConformidade.EmTratamento, 7),
        (StatusNaoConformidade.AguardandoValidacao, 4),
        (StatusNaoConformidade.Encerrada, 6),
    };

    private static readonly string[] PrimeirosNomes =
    {
        "João", "Maria", "Carlos", "Ana", "Pedro", "Paulo", "Marcos", "Lucas", "Rafael", "Fernanda",
        "Juliana", "Bruno", "Diego", "Felipe", "Gabriel", "Renata", "Patrícia", "Rodrigo", "Sandra", "Vitor",
    };

    private static readonly string[] Sobrenomes =
    {
        "Silva", "Souza", "Oliveira", "Santos", "Pereira", "Costa", "Rodrigues", "Almeida", "Nascimento", "Lima",
        "Araújo", "Fernandes", "Carvalho", "Gomes", "Martins", "Rocha", "Ribeiro", "Alves", "Monteiro", "Cardoso",
    };

    public static string GerarNome(int indice)
    {
        var primeiro = PrimeirosNomes[indice % PrimeirosNomes.Length];
        var sobrenome1 = Sobrenomes[(indice / PrimeirosNomes.Length) % Sobrenomes.Length];
        var sobrenome2 = Sobrenomes[(indice * 7 + 3) % Sobrenomes.Length];
        return $"{primeiro} {sobrenome1} {sobrenome2}";
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AAHBRANT.SST.Api.IntegrationTests --filter MockObraSeederDadosEstaticosTests`
Expected: PASS (5 testes)

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.DadosEstaticos.cs tests/AAHBRANT.SST.Api.IntegrationTests/Persistencia/Seed/MockObraSeederDadosEstaticosTests.cs
git commit -m "feat: dados estaticos do seeder de obra mocada (funcoes, cursos, epis, nomes)"
```

---

## Task 4: Estrutura organizacional (Obra, Áreas, Funções, Setores, Equipes, Trabalhadores)

**Files:**
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.cs`

**Interfaces:**
- Consumes: `MockObraSeeder.DistribuicaoFuncoes`, `MockObraSeeder.GerarNome` (Task 3); `GeradorCpfFicticio.Gerar` (Task 1).
- Produces: `MockObraSeeder.CodigoObraMock : string` (constante); método privado `ConstruirEstruturaOrganizacional(DateTime referenciaUtc) : (Obra Obra, List<AreaSst> Areas, List<Funcao> Funcoes, List<Setor> Setores, List<Equipe> Equipes, List<Trabalhador> Trabalhadores)` — consumido pelas Tasks 5 e 6.

Sem teste automatizado dedicado nesta task (grafo EF Core sem lógica de negócio nova — mesmo padrão de `RbacSeeder`/`RegraAlertaSeeder`, nenhum dos quais tem teste; a lógica testável de fato — geração de CPF e distribuição de datas — já foi coberta nas Tasks 1 e 2). A verificação deste código acontece de ponta a ponta na Task 8.

- [ ] **Step 1: Implementar a estrutura organizacional**

```csharp
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Entidades.Identificacao;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder de dados mocados da Fase 1 do roadmap de SST (obra fictícia "Edifício Aurora
// Corporate", 20 pavimentos, ~200 trabalhadores) — só roda em ambiente Development (ver
// Program.cs), nunca em homologação/produção. Idempotente: se a Obra com CodigoObraMock já
// existe, não faz nada. Usa somente entidades já existentes no sistema; nenhuma migration nova.
// Ver docs/superpowers/specs/2026-08-26-fase1-dados-mock-obra-design.md.
public static partial class MockObraSeeder
{
    public const string CodigoObraMock = "OBRA-MOCK-AURORA";

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var jaExiste = await db.Obras.IgnoreQueryFilters().AnyAsync(o => o.Codigo == CodigoObraMock, ct);
        if (jaExiste) return;

        var referenciaUtc = DateTime.UtcNow;

        var (obra, areas, funcoes, setores, equipes, trabalhadores) = ConstruirEstruturaOrganizacional(referenciaUtc);

        db.Obras.Add(obra);
        db.AreasSst.AddRange(areas);
        db.Funcoes.AddRange(funcoes);
        db.Setores.AddRange(setores);
        db.Equipes.AddRange(equipes);
        db.Trabalhadores.AddRange(trabalhadores);

        await db.SaveChangesAsync(ct);
    }

    private static (Obra Obra, List<AreaSst> Areas, List<Funcao> Funcoes, List<Setor> Setores, List<Equipe> Equipes, List<Trabalhador> Trabalhadores)
        ConstruirEstruturaOrganizacional(DateTime referenciaUtc)
    {
        var obra = new Obra
        {
            Codigo = CodigoObraMock,
            Nome = "Edifício Aurora Corporate",
            Cliente = "Aurora Empreendimentos Imobiliários S.A.",
            Status = StatusObra.EmAndamento,
            DataInicio = referenciaUtc.AddMonths(-6),
            DataPrevisaoTermino = referenciaUtc.AddMonths(18),
            Endereco = "Av. das Torres, 1200",
            Cidade = "Belo Horizonte",
            Uf = "MG",
        };

        var areas = new List<AreaSst>
        {
            NovaArea(obra, "SUB", "Subsolo", TipoArea.AreaDeTrabalho),
            NovaArea(obra, "TER", "Térreo", TipoArea.AreaDeTrabalho),
        };
        for (var pavimento = 1; pavimento <= 20; pavimento++)
            areas.Add(NovaArea(obra, $"P{pavimento:D2}", $"Pavimento {pavimento}", TipoArea.AreaDeTrabalho));
        areas.Add(NovaArea(obra, "CANT", "Canteiro/Almoxarifado", TipoArea.Armazenamento));

        var funcoes = DistribuicaoFuncoes
            .Select(f => new Funcao { Nome = f.Funcao })
            .ToList();

        var setores = new List<Setor>
        {
            NovoSetor(obra, "Estrutura Térreo–P10"),
            NovoSetor(obra, "Estrutura P11–P20"),
            NovoSetor(obra, "Acabamento"),
            NovoSetor(obra, "Instalações"),
            NovoSetor(obra, "Canteiro/Apoio"),
        };

        var equipes = new List<Equipe>();
        foreach (var setor in setores)
        {
            equipes.Add(new Equipe { Setor = setor, Nome = $"{setor.Nome} — Equipe A" });
            equipes.Add(new Equipe { Setor = setor, Nome = $"{setor.Nome} — Equipe B" });
        }

        var trabalhadores = new List<Trabalhador>();
        var indiceGlobal = 0;
        var indiceEquipe = 0;

        foreach (var (nomeFuncao, quantidade, _) in DistribuicaoFuncoes)
        {
            var funcao = funcoes.Single(f => f.Nome == nomeFuncao);
            for (var i = 0; i < quantidade; i++)
            {
                var equipe = equipes[indiceEquipe % equipes.Count];
                var trabalhador = new Trabalhador
                {
                    Obra = obra,
                    Setor = equipe.Setor,
                    Equipe = equipe,
                    Funcao = funcao,
                    Nome = GerarNome(indiceGlobal),
                    Matricula = $"AUR-{indiceGlobal + 1:D4}",
                    Cpf = GeradorCpfFicticio.Gerar(indiceGlobal),
                    Vinculo = TipoVinculo.Clt,
                    DataAdmissao = referenciaUtc.AddMonths(-6).AddDays(indiceGlobal % 150),
                };
                trabalhadores.Add(trabalhador);

                if (nomeFuncao == "Encarregado" && equipe.Encarregado is null)
                    equipe.Encarregado = trabalhador;

                indiceGlobal++;
                indiceEquipe++;
            }
        }

        return (obra, areas, funcoes, setores, equipes, trabalhadores);
    }

    private static AreaSst NovaArea(Obra obra, string codigo, string nome, TipoArea tipo) => new()
    {
        Obra = obra,
        Codigo = codigo,
        Nome = nome,
        Tipo = tipo,
        Riscos = new List<string> { "Queda de altura", "Queda de material", "Atropelamento por equipamento" },
        Requisitos = new List<string> { "Uso obrigatório de capacete", "Delimitação de área com fita zebrada" },
        Status = StatusArea.Ativa,
    };

    private static Setor NovoSetor(Obra obra, string nome) => new() { Obra = obra, Nome = nome };
}
```

- [ ] **Step 2: Build da solução**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure`
Expected: Build succeeded, 0 erros.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.cs
git commit -m "feat: estrutura organizacional do seeder de obra mocada (obra/areas/funcoes/equipes/trabalhadores)"
```

---

## Task 5: Treinamentos e ASOs dos trabalhadores mocados

**Files:**
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.TreinamentosEAsos.cs`

**Interfaces:**
- Consumes: `MockObraSeeder.CatalogoCursosNr`, `MockObraSeeder.DistribuicaoFuncoes` (Task 3); `DistribuidorFaixaVencimento.CalcularData` (Task 2); `List<Trabalhador>` produzido pela Task 4 (cada trabalhador com `.Funcao` e `.Matricula` já setados).
- Produces: método privado `ConstruirTreinamentosEAsos(List<Trabalhador> trabalhadores, DateTime referenciaUtc) : (List<CursoTreinamento> Cursos, List<Treinamento> Treinamentos, List<Aso> Asos)` — consumido pela Task 6 (orquestrador final).

Sem teste automatizado dedicado (mesma justificativa da Task 4 — grafo EF Core reaproveitando lógica já testada nas Tasks 1/2).

- [ ] **Step 1: Implementar**

```csharp
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    private static (List<CursoTreinamento> Cursos, List<Treinamento> Treinamentos, List<Aso> Asos)
        ConstruirTreinamentosEAsos(List<Trabalhador> trabalhadores, DateTime referenciaUtc)
    {
        var cursos = CatalogoCursosNr
            .Select(c => new CursoTreinamento
            {
                Nome = c.Nome,
                NormaReferencia = c.NormaReferencia,
                CargaHorariaMinima = c.CargaHorariaMinima,
                ValidadeEmMeses = c.ValidadeEmMeses,
            })
            .ToList();

        var treinamentos = new List<Treinamento>();
        var asos = new List<Aso>();
        var indiceGlobal = 0;

        foreach (var trabalhador in trabalhadores)
        {
            var codigosCursos = DistribuicaoFuncoes.Single(f => f.Funcao == trabalhador.Funcao!.Nome).CodigosCursos;
            foreach (var codigoCurso in codigosCursos)
            {
                var curso = cursos.Single(c => c.NormaReferencia == codigoCurso);
                var dataValidade = DistribuidorFaixaVencimento.CalcularData(indiceGlobal, referenciaUtc);
                treinamentos.Add(new Treinamento
                {
                    Trabalhador = trabalhador,
                    CursoTreinamento = curso,
                    DataRealizacao = dataValidade.AddMonths(-curso.ValidadeEmMeses),
                    DataValidade = dataValidade,
                    CargaHorariaRealizada = curso.CargaHorariaMinima,
                    InstituicaoInstrutor = "SENAI - Unidade Construção Civil",
                    NumeroCertificado = $"CERT-{codigoCurso}-{trabalhador.Matricula}",
                });
                indiceGlobal++;
            }

            var dataValidadeAso = DistribuidorFaixaVencimento.CalcularData(indiceGlobal, referenciaUtc);
            // ~5% dos trabalhadores (1 em cada 20) representam admissões recentes na obra — os
            // demais já passaram pelo admissional em algum momento anterior e estão no periódico.
            var tipoExame = indiceGlobal % 20 == 0
                ? TipoExameAso.Admissional
                : TipoExameAso.Periodico;
            asos.Add(new Aso
            {
                Trabalhador = trabalhador,
                Tipo = tipoExame,
                DataExame = tipoExame == TipoExameAso.Admissional ? trabalhador.DataAdmissao : dataValidadeAso.AddYears(-1),
                DataValidade = dataValidadeAso,
                ResultadoStatus = ResultadoAso.Apto,
                MedicoNome = "Dr. Marcelo Andrade",
                MedicoCrm = "CRM-MG 45231",
            });
            indiceGlobal++;
        }

        return (cursos, treinamentos, asos);
    }
}
```

- [ ] **Step 2: Build da solução**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure`
Expected: Build succeeded, 0 erros.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.TreinamentosEAsos.cs
git commit -m "feat: treinamentos e asos mocados por trabalhador da obra Aurora"
```

---

## Task 6: Não Conformidades, EPI e orquestrador final

**Files:**
- Modify: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.cs` (`ExecutarAsync` passa a chamar as Tasks 5 e 6 e persistir tudo)
- Create: `src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.NaoConformidadesEEpi.cs`

**Interfaces:**
- Consumes: `MockObraSeeder.DistribuicaoNaoConformidades`, `MockObraSeeder.CatalogoEpisPadrao` (Task 3); `DistribuidorFaixaVencimento.CalcularData` (Task 2); `List<AreaSst>`/`List<Trabalhador>` (Task 4); `ConstruirTreinamentosEAsos` (Task 5).
- Produces: método privado `ConstruirNaoConformidadesEEpi(List<Trabalhador> trabalhadores, List<AreaSst> areas, DateTime referenciaUtc) : (List<NaoConformidade>, List<CatalogoEpi>, List<EntregaEpi>)`; `ExecutarAsync` completo (ponto de entrada usado pela Task 7).

Sem teste automatizado dedicado (mesma justificativa das Tasks 4/5).

- [ ] **Step 1: Implementar `ConstruirNaoConformidadesEEpi`**

```csharp
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Entidades.Identificacao;
using AAHBRANT.SST.Domain.Entidades.NaoConformidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    private static (List<NaoConformidade> NaoConformidades, List<CatalogoEpi> CatalogosEpi, List<EntregaEpi> EntregasEpi)
        ConstruirNaoConformidadesEEpi(List<Trabalhador> trabalhadores, List<AreaSst> areas, DateTime referenciaUtc)
    {
        var naoConformidades = new List<NaoConformidade>();
        var indiceNc = 0;
        foreach (var (status, quantidade) in DistribuicaoNaoConformidades)
        {
            for (var i = 0; i < quantidade; i++)
            {
                var area = areas[indiceNc % areas.Count];
                var prazo = DistribuidorFaixaVencimento.CalcularData(indiceNc, referenciaUtc);
                naoConformidades.Add(new NaoConformidade
                {
                    OrigemDeteccao = (OrigemNaoConformidade)((indiceNc % 5) + 1),
                    Descricao = $"Não conformidade identificada em {area.Nome}: uso incorreto de EPI / condição insegura de acesso.",
                    Local = area.Nome,
                    Prazo = prazo,
                    Status = status,
                    DataConclusao = status == StatusNaoConformidade.Encerrada ? prazo.AddDays(-2) : null,
                });
                indiceNc++;
            }
        }

        var catalogosEpi = CatalogoEpisPadrao
            .Select(e => new CatalogoEpi
            {
                Nome = e.Nome,
                Fabricante = e.Fabricante,
                CertificadoAprovacaoNumero = e.CertificadoAprovacaoNumero,
                CertificadoAprovacaoValidade = referenciaUtc.AddYears(2),
                VidaUtilEmMeses = e.VidaUtilEmMeses,
                SaldoEstoque = e.SaldoEstoque,
            })
            .ToList();

        var epiCapacete = catalogosEpi.Single(e => e.Nome.Contains("Capacete"));
        var epiBota = catalogosEpi.Single(e => e.Nome.Contains("Bota"));

        var entregasEpi = new List<EntregaEpi>();
        var indiceEntrega = 0;
        foreach (var trabalhador in trabalhadores)
        {
            entregasEpi.Add(NovaEntrega(trabalhador, epiCapacete, referenciaUtc, indiceEntrega++));
            entregasEpi.Add(NovaEntrega(trabalhador, epiBota, referenciaUtc, indiceEntrega++));
        }

        return (naoConformidades, catalogosEpi, entregasEpi);
    }

    private static EntregaEpi NovaEntrega(Trabalhador trabalhador, CatalogoEpi epi, DateTime referenciaUtc, int indice) => new()
    {
        Trabalhador = trabalhador,
        CatalogoEpi = epi,
        DataEntrega = referenciaUtc.AddDays(-(30 + indice % 60)),
        DataValidade = referenciaUtc.AddMonths(epi.VidaUtilEmMeses).AddDays(-(indice % 30)),
        Quantidade = 1,
        Motivo = "Entrega inicial",
    };
}
```

- [ ] **Step 2: Atualizar `ExecutarAsync` em `MockObraSeeder.cs` para persistir tudo**

Editar o método `ExecutarAsync` (Task 4) para o conteúdo final:

```csharp
    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var jaExiste = await db.Obras.IgnoreQueryFilters().AnyAsync(o => o.Codigo == CodigoObraMock, ct);
        if (jaExiste) return;

        var referenciaUtc = DateTime.UtcNow;

        var (obra, areas, funcoes, setores, equipes, trabalhadores) = ConstruirEstruturaOrganizacional(referenciaUtc);
        var (cursos, treinamentos, asos) = ConstruirTreinamentosEAsos(trabalhadores, referenciaUtc);
        var (naoConformidades, catalogosEpi, entregasEpi) = ConstruirNaoConformidadesEEpi(trabalhadores, areas, referenciaUtc);

        db.Obras.Add(obra);
        db.AreasSst.AddRange(areas);
        db.Funcoes.AddRange(funcoes);
        db.Setores.AddRange(setores);
        db.Equipes.AddRange(equipes);
        db.Trabalhadores.AddRange(trabalhadores);
        db.CursosTreinamento.AddRange(cursos);
        db.Treinamentos.AddRange(treinamentos);
        db.Asos.AddRange(asos);
        db.NaoConformidades.AddRange(naoConformidades);
        db.CatalogoEpis.AddRange(catalogosEpi);
        db.EntregasEpi.AddRange(entregasEpi);

        await db.SaveChangesAsync(ct);
    }
```

- [ ] **Step 3: Build da solução**

Run: `dotnet build src/AAHBRANT.SST.Infrastructure`
Expected: Build succeeded, 0 erros.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.cs src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.NaoConformidadesEEpi.cs
git commit -m "feat: nao conformidades e epi mocados, finaliza orquestrador do seeder da obra"
```

---

## Task 7: Registrar o seeder no Program.cs (só em Development)

**Files:**
- Modify: `src/AAHBRANT.SST.Api/Program.cs:112-114`

**Interfaces:**
- Consumes: `MockObraSeeder.ExecutarAsync(IServiceProvider, CancellationToken)` (Task 6).
- Produces: nada (ponto de entrada do host).

- [ ] **Step 1: Editar `Program.cs`**

Estado atual (linhas 112-114):
```csharp
await RbacSeeder.ExecutarAsync(app.Services);
await CpfLgpdBackfillSeeder.ExecutarAsync(app.Services);
await RegraAlertaSeeder.ExecutarAsync(app.Services);
```

Novo estado:
```csharp
await RbacSeeder.ExecutarAsync(app.Services);
await CpfLgpdBackfillSeeder.ExecutarAsync(app.Services);
await RegraAlertaSeeder.ExecutarAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    await MockObraSeeder.ExecutarAsync(app.Services);
}
```

- [ ] **Step 2: Build da solução completa**

Run: `dotnet build`
Expected: Build succeeded, 0 erros.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.Api/Program.cs
git commit -m "feat: aciona o seeder de obra mocada no start da api em Development"
```

---

## Task 8: Execução e verificação manual end-to-end

Sem arquivo novo — esta task só executa e observa o sistema já implementado nas Tasks 1–7.

- [ ] **Step 1: Rodar todos os testes automatizados**

Run: `dotnet test`
Expected: PASS em todos os testes (incluindo os 9 novos das Tasks 1–3, mais os pré-existentes).

- [ ] **Step 2: Subir a API localmente em Development**

Run: `dotnet run --project src/AAHBRANT.SST.Api`
Expected: log de startup sem exceções; `MockObraSeeder.ExecutarAsync` roda sem lançar erro (nenhuma exceção de FK/constraint no console).

- [ ] **Step 3: Confirmar idempotência**

Parar a API (Ctrl+C) e rodar `dotnet run --project src/AAHBRANT.SST.Api` novamente.
Expected: nenhuma duplicata — a segunda execução do seeder retorna imediatamente (checagem `jaExiste`).

- [ ] **Step 4: Verificar visualmente no frontend (conforme seção "Verificação" da spec)**

Usar o preview/browser do frontend (`src/AAHBRANT.SST.TeamsApp`) e confirmar:
1. Lista de Trabalhadores mostra ~200 registros da obra "Edifício Aurora Corporate".
2. Dashboard de Não Conformidades mostra 25 registros distribuídos entre Aberta/EmTratamento/AguardandoValidacao/Encerrada.
3. Existem alertas de Treinamento e ASO vencidos/a vencer (podem depender de uma execução do worker do Motor de Alertas — se não aparecerem imediatamente, checar `AAHBRANT.SST.Worker` está rodando).
4. Módulo de EPI mostra "Cinto de Segurança Tipo Paraquedista" e "Óculos de Proteção Ampla Visão" com saldo 0, e "Bota de Segurança com Bico de Aço" com saldo 3.
5. Cadastro de Áreas mostra 23 áreas (Subsolo, Térreo, Pavimento 1–20, Canteiro/Almoxarifado).

- [ ] **Step 5: Reportar resultado**

Se todos os pontos do Step 4 se confirmarem, a Fase 1 está completa. Documentar qualquer divergência encontrada (ex.: worker de alertas não rodando localmente) para o usuário decidir se é bloqueante.
