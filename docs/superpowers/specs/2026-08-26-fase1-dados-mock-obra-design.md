# Fase 1 — Dados mocados da obra "Edifício Aurora Corporate"

**Data:** 2026-08-26
**Status:** aprovado para plano de implementação
**Contexto:** primeira de 10 fases planejadas para o cenário completo de gestão de
SST em obra de edifício vertical (20 pavimentos, ~200 funcionários diretos). Ver
decomposição completa registrada na conversa que originou esta spec — as fases
seguintes (EPI×função, Zonas de Trabalho, Trabalho em Altura, DDS estruturado,
Patrulha, NC/Auditoria, Instruções de Trabalho, Calendário Teams, Biometria EPI)
dependem desta massa de dados para terem cenários de teste realistas.

## Objetivo

Popular o banco de dados local de desenvolvimento com uma obra fictícia
completa e coerente, usando **somente entidades e módulos já existentes no
sistema** (Obra, AreaSst, Funcao, Setor, Equipe, Trabalhador,
CursoTreinamento/Treinamento, Aso, NaoConformidade, CatalogoEpi/EntregaEpi),
de forma que os módulos de alerta de vencimento, dashboard de NC e estoque de
EPI já tenham dados reais para exercitar sem esperar as fases seguintes.

Não é criada nenhuma entidade nova nesta fase. É puramente um seeder de dados.

## Escopo

**Dentro do escopo:**
- 1 `Obra` fictícia — "Edifício Aurora Corporate".
- 23 `AreaSst` (Subsolo, Térreo, P1–P20, Canteiro/Almoxarifado) — um registro por
  pavimento, usando o cadastro de Área já existente. Os campos `Riscos`/
  `Requisitos` (List<string>) recebem valores placeholder plausíveis (ex.:
  `["Queda de altura", "Queda de material"]`) já que a estrutura de risco por
  atividade formal só existe a partir da Fase 3 — não é objetivo desta fase
  modelar risco corretamente, só ocupar o campo com algo coerente.
- ~14 `Funcao` de obra civil vertical, com proporções realistas para 200
  pessoas.
- `Setor`/`Equipe` o suficiente para agrupar os 200 trabalhadores sob
  encarregados (ver distribuição abaixo).
- ~200 `Trabalhador`, com CPF fictício mas matematicamente válido (respeita o
  dígito verificador de `CpfValidador`), matrícula sequencial, função,
  setor/equipe.
- `CursoTreinamento` para as NRs relevantes a uma obra vertical (NR-35 Altura,
  NR-18 Construção Civil, NR-06 EPI, NR-10 Elétrica, NR-12 Máquinas, NR-33
  Espaço Confinado, NR-11 Movimentação de Carga) + `Treinamento` por
  trabalhador conforme a função, com datas distribuídas em 3 faixas (vencido /
  a vencer em 30 dias / válido).
- `Aso` por trabalhador (Periódico, alguns Admissional para os mais recentes),
  mesma distribuição de 3 faixas de validade.
- ~25 `NaoConformidade` em estados variados (Aberta, EmTratamento,
  AguardandoValidacao, Encerrada), com `Prazo` também distribuído entre
  vencido/a vencer/futuro.
- `CatalogoEpi` (capacete, cinto tipo paraquedista, luva, bota, protetor
  auricular, óculos, máscara PFF2) com `SaldoEstoque` variado, incluindo pelo
  menos 2 itens com saldo zero/crítico. `EntregaEpi` para os trabalhadores.

**Fora do escopo (fica para fases seguintes, não é inventado aqui):**
- Vínculo EPI×Função obrigatório (Fase 2).
- Estrutura de risco/atividade formal por área e vínculo de trabalhador a
  atividade (Fase 3) — as áreas desta fase ficam com dados simples.
- Qualquer coisa de Trabalho em Altura além do curso NR-35 já citado (Fase 4).
- DDS, Patrulha, Instruções de Trabalho, Calendário, biometria em EntregaEpi
  (fases 5–10).
- Cronograma/EAP da obra (não existe campo para isso na entidade `Obra`; não
  será adicionado nesta fase).
- Nenhum dado é escrito em homologação — só ambiente local (`Development`).

## Arquitetura

Novo seeder estático `MockObraSeeder`, no mesmo padrão dos seeders existentes
(`RbacSeeder`, `RegraAlertaSeeder`, `CpfLgpdBackfillSeeder`):

```
src/AAHBRANT.SST.Infrastructure/Persistencia/Seed/MockObraSeeder.cs
```

```csharp
public static class MockObraSeeder
{
    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var jaExiste = await db.Obras.IgnoreQueryFilters()
            .AnyAsync(o => o.Codigo == CodigoObraMock, ct);
        if (jaExiste) return; // idempotente — não duplica em restarts sucessivos

        // ... monta Obra, Areas, Funcoes, Setores, Equipes, Trabalhadores,
        // CursosTreinamento, Treinamentos, Asos, NaoConformidades,
        // CatalogoEpi, EntregaEpi — grafo completo antes de um único
        // SaveChangesAsync, igual ao padrão do RegraAlertaSeeder.
    }
}
```

Chamado em `src/AAHBRANT.SST.Api/Program.cs`, imediatamente após os três
seeders existentes, **condicionado a `IsDevelopment()`** — diferente dos
seeders atuais, que rodam em qualquer ambiente:

```csharp
await RbacSeeder.ExecutarAsync(app.Services);
await CpfLgpdBackfillSeeder.ExecutarAsync(app.Services);
await RegraAlertaSeeder.ExecutarAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    await MockObraSeeder.ExecutarAsync(app.Services);
}
```

Nenhuma migration nova é necessária — todas as entidades usadas já existem no
schema atual.

## Geração de dados

**CPF fictício válido:** função local que monta os 9 primeiros dígitos a
partir de um índice sequencial (garante não-colisão) e calcula os 2 dígitos
verificadores pelo mesmo algoritmo usado por `CpfValidador`, para que o CPF
passe validação em qualquer tela que a chame. O `Cpf` é atribuído em texto
plano na entidade — a criptografia e o `CpfHash` são responsabilidade do
`SstDbContext` na gravação (nenhum tratamento especial necessário no seeder,
conforme confirmado no código existente).

**Distribuição de datas de vencimento (Treinamento, Aso, NaoConformidade.Prazo):**
usando `DateTime.UtcNow` como referência (26/08/2026 no momento da escrita,
mas o seeder calcula em runtime, não hardcoded):
- ~20% vencido (data de validade/prazo entre 5 e 60 dias no passado)
- ~25% a vencer em breve (entre 1 e 30 dias no futuro)
- ~55% válido/confortável (entre 31 e 365 dias no futuro)

Essa distribuição é aplicada independentemente a Treinamento e a Aso por
trabalhador (um trabalhador pode ter o treinamento vencido e o ASO válido, e
vice-versa) para gerar combinações realistas para os alertas.

**Distribuição de funções (200 trabalhadores):** proporção típica de obra
vertical —

| Função | Qtde aprox. |
|---|---|
| Servente | 45 |
| Pedreiro | 35 |
| Armador | 20 |
| Carpinteiro | 18 |
| Eletricista | 12 |
| Encanador | 10 |
| Pintor | 10 |
| Soldador | 8 |
| Operador de grua/betoneira | 8 |
| Mestre de obras | 4 |
| Encarregado | 10 |
| Técnico de Segurança do Trabalho | 4 |
| Engenheiro Civil | 6 |
| Almoxarife | 3 |
| Vigia/Porteiro | 7 |

(soma 200 — ajustável no plano de implementação sem alterar a spec)

**Setores/Equipes:** setores por trecho da obra (ex. "Estrutura Térreo–P10",
"Estrutura P11–P20", "Acabamento", "Instalações", "Canteiro/Apoio"), cada um
com 1–3 equipes lideradas por um `Encarregado` já criado como `Trabalhador`.

## Erros e casos de borda

- **Reexecução:** o `AnyAsync` por `Codigo` da Obra garante que rodar o
  seeder de novo (restart da API) não duplica nada. Não há rollback parcial
  a tratar porque tudo é montado em memória e salvo em um único
  `SaveChangesAsync` — ou tudo entra, ou a exceção impede o `SaveChanges` e
  nada é persistido.
- **Ambiente:** se por engano `ASPNETCORE_ENVIRONMENT` estiver diferente de
  `Development` localmente, o seeder simplesmente não roda — sem erro, sem
  necessidade de flag adicional.
- **Reset:** para recomeçar do zero, o desenvolvedor apaga o banco local e
  deixa `MigrateAsync` recriar o schema; não é responsabilidade deste seeder
  prover um "desseed".

## Verificação

Depois do seeder rodar (primeiro `dotnet run` da Api em Development):
1. Abrir o frontend e confirmar ~200 trabalhadores na lista de Pessoas.
2. Confirmar que o dashboard de Não Conformidades mostra os ~25 registros
   distribuídos entre os status.
3. Confirmar que existem alertas de treinamento e ASO vencidos/a vencer
   (Motor de Alertas já existente deve dispará-los na primeira execução do
   worker).
4. Confirmar no módulo de EPI que pelo menos 2 itens do catálogo aparecem
   com saldo crítico/zero.
5. Confirmar as 23 Áreas cadastradas (Subsolo, Térreo, P1–P20, Canteiro).

Não há teste automatizado dedicado — é um seeder de dados de desenvolvimento,
não lógica de negócio nova; a verificação é visual no navegador conforme
acima.
