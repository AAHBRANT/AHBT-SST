# Matriz de EPI por Função

- **Data:** 2026-08-26
- **Autor:** Assistente (Claude Code), a pedido de Wellington Lourenço
- **Status:** Aprovado pelo usuário em chat — pronto para plano de implementação
- **Precede:** plano de implementação (writing-plans), com migration EF Core
- **Parte de:** reformulação do módulo EPI em 3 fases — (1) Matriz de EPI por
  Função [este documento], (2) Ficha de EPI reformulada (alinhada ao modelo
  oficial `AHBT-FIC-SSO-XXX-00_FichaEntregaEPI`), (3) Controle de Estoque de
  EPI por obra/almoxarifado.

## 1. Contexto

O processo `PR-SST-001 — Gestão de EPI` da AAHBRANT define 3 controles:
Matriz de EPI por Função, Ficha de EPI e Controle de Estoque de EPI. Hoje o
sistema só cobre uma versão parcial da Ficha (via `CatalogoEpi`/`EntregaEpi`)
e do Estoque (saldo único global, sem segmentação por obra). A **Matriz de
EPI por Função não existe** — nem entidade, nem endpoint, nem tela.

Essa lacuna já era conhecida: o spec
`docs/superpowers/specs/2026-08-26-fase1-dados-mock-obra-design.md` cita
literalmente *"Vínculo EPI×Função obrigatório (Fase 2)"* como pendência. O
modelo oficial da Ficha de Entrega de EPI
(`AHBT-FIC-SSO-XXX-00_FichaEntregaEPI_2026-08-26_v01.docx`) reforça isso no
rodapé: *"Antes de cada entrega, confirmar a validade do CA na Matriz de EPI
por Função"* — ou seja, o próprio documento institucional pressupõe que a
matriz existe como fonte de verdade de quais EPIs cabem a cada função.

Esta é a **Fase 1** da reformulação: implementar a Matriz como pré-requisito
das Fases 2 e 3 (a Ficha reformulada e a validação de entrega dependem dela).

## 2. Escopo

**Entra:**
- Entidade `MatrizEpiFuncao` (associação simples Função × CatalogoEpi).
- Endpoints para consultar e definir os EPIs de uma função.
- Tela de gestão da matriz dentro de `FuncoesTab.tsx` (Pessoas → Funções).
- Filtro no formulário de nova `EntregaEpi`: o select de EPI só lista os
  itens vinculados à função do trabalhador selecionado.
- Migration EF Core nova.

**Não entra (fases seguintes):**
- Reformulação da Ficha de EPI (campos de identificação, motivo estruturado,
  segunda assinatura, PDF no padrão oficial) — Fase 2.
- Estoque segmentado por obra/almoxarifado, histórico de movimentações — Fase 3.
- Quantidade ou periodicidade por vínculo EPI×Função (decisão do usuário:
  só obrigatoriedade, sim/não, por ora).

## 3. Modelo atual (o que já existe e não muda)

- `Funcao : AuditableEntity` — `Nome`, `CboCodigo`, `Descricao`, coleção
  `Trabalhadores`. Sem nenhum vínculo com EPI hoje.
- `CatalogoEpi : AuditableEntity` — `Nome`, `Fabricante`, `CA`, validade do
  CA, vida útil, `SaldoEstoque`, coleção `Entregas`.
- `Trabalhador.FuncaoId` já existe (FK para `Funcao`) — é a chave usada para
  descobrir a função do trabalhador ao filtrar o select de EPI.
- `FuncoesController` usa a família de policies `organizacional:*`
  (`ver`/`criar`/`editar`/`excluir`); `CatalogosEpiController` e
  `EntregasEpiController` usam `epi:*`.

## 4. Modelo proposto

### 4.1 Nova entidade

```csharp
public class MatrizEpiFuncao : AuditableEntity
{
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }
    public Guid CatalogoEpiId { get; set; }
    public CatalogoEpi? CatalogoEpi { get; set; }
}
```

Sem campos extras (obrigatoriedade é a própria existência do registro —
decisão do usuário de manter simples nesta fase). Índice único composto
`(FuncaoId, CatalogoEpiId)` para impedir duplicidade. FKs com `Restrict`
(mesmo padrão de `EntregaEpi`), configuradas em
`ConformidadeConfiguracoes.cs` (ou um novo arquivo de configuração dedicado,
a critério do plano de implementação).

### 4.2 Application — Command e Queries

Em vez de comandos individuais de vincular/desvincular um EPI por vez, um
único comando que sincroniza o conjunto completo — mais natural para uma
tela de checklist ("marque os EPIs desta função, salve tudo de uma vez"):

```csharp
public record DefinirMatrizEpiFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoEpiIds) : IRequest;
```

Handler: carrega os vínculos atuais da função, adiciona os que estão em
`CatalogoEpiIds` e ainda não existem, remove os que existem mas não estão
mais na lista (sincronização idempotente, não é só insert).

```csharp
public record ListarEpisPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CatalogoEpiDto>>;
```

Retorna a lista de `CatalogoEpiDto` (mesmo DTO já usado por
`ListarCatalogosEpiQuery`) vinculados àquela função — reaproveitada tanto
pela tela de matriz quanto pelo filtro no formulário de entrega.

### 4.3 Api — endpoints

Em `FuncoesController`:

```
GET  /api/funcoes/{id}/epis   → ListarEpisPorFuncaoQuery
PUT  /api/funcoes/{id}/epis   → DefinirMatrizEpiFuncaoCommand
```

**Policy do GET:** aceita tanto `organizacional:ver` (quem gerencia Funções)
quanto `epi:ver` (quem registra entrega de EPI, ex. Encarregado, que pela
`RBAC-Matrix.md` tem escopo de EPI mas não necessariamente de
organizacional). Implementação sugerida: policy combinada
(`AuthorizationPolicyBuilder` com `RequireAssertion` aceitando qualquer uma
das duas claims), ou duas rotas equivalentes — decisão de como fazer isso no
projeto (padrão já usado em outro lugar?) fica para o plano de
implementação.

**Policy do PUT:** só `organizacional:editar` (editar a matriz é
administração da função, não operação de campo).

### 4.4 TeamsApp — UI

**`FuncoesTab.tsx`:** cada linha da tabela ganha comportamento de
expandir/editar (mesmo padrão de `CatalogoTab.tsx` — clique na linha abre um
painel). O painel expandido mostra um checklist com todos os itens de
`CatalogoEpi` (nome + fabricante), cada um com checkbox marcado se já
vinculado àquela função (via `GET /api/funcoes/{id}/epis`), botão "Salvar
matriz" que chama `PUT /api/funcoes/{id}/epis` com a lista de IDs marcados.

**`EntregasTab.tsx`:** ao selecionar o trabalhador no formulário de nova
entrega, buscar `GET /api/funcoes/{trabalhador.funcaoId}/epis` e restringir
o select de EPI a esse conjunto. Se a função não tiver EPIs vinculados (ou o
trabalhador não tiver função definida), mostrar estado vazio orientando a
cadastrar a matriz da função antes de registrar a entrega, com atalho para
`Pessoas → Funções`.

**`lib/api.ts`:** novos métodos `api.funcoes.listarEpis(funcaoId)` e
`api.funcoes.definirEpis(funcaoId, catalogoEpiIds)`.

## 5. Migration

Nova migration (nome sugerido: `AdicionarMatrizEpiFuncao`), cria a tabela
`MatrizEpiFuncao` com as duas FKs e o índice único composto. Sem impacto em
dados existentes — tabela nova, sem seed obrigatório (cada obra/projeto
define sua própria matriz depois do deploy). Avaliar no plano se o seeder de
obra mocada (`MockObraSeeder`) deve popular uma matriz de exemplo para os
dados de demonstração já existentes.

## 6. Pendências (não decidíveis por mim — registrar e seguir)

1. **Policy combinada do GET** (Seção 4.3) — confirmar no plano qual
   mecanismo de autorização o projeto já usa para "aceitar qualquer uma de
   duas policies" (se já existe precedente) ou se é caso de criar uma nova
   policy única (ex. `matriz-epi:ver`) atribuída a ambos os perfis.
2. **Seed de exemplo no `MockObraSeeder`** — a confirmar se os dados mocados
   de demonstração devem incluir uma matriz já preenchida (facilita
   demonstração da Fase 2, que depende do filtro funcionar).

## 7. Testes (alto nível)

- Teste de unidade no handler de `DefinirMatrizEpiFuncaoCommand`: sincronização
  correta (adiciona novos, remove ausentes, idempotente ao reenviar a mesma
  lista).
- Teste de integração: `GET /api/funcoes/{id}/epis` retorna vazio para
  função sem matriz definida; retorna os itens certos após `PUT`.
- Teste manual no navegador: cadastrar matriz para uma função em
  `FuncoesTab`, depois abrir `EntregasTab`, selecionar um trabalhador dessa
  função e confirmar que o select de EPI mostra só os itens da matriz (e
  mostra o estado vazio para trabalhador de função sem matriz).
