# DDS — 3 temas simultâneos por dia (2 automáticos da atividade + 1 livre) e catálogo próprio

- **Data:** 2026-09-01
- **Autor:** Assistente (Claude Code), a pedido de Wellington Lourenço
- **Status:** Aprovado pelo usuário em chat — pronto para plano de implementação
- **Precede:** plano de implementação (writing-plans)
- **Contexto vivo:** memória `project_sst_patrulha_assinatura_tecnico.md` (branch
  ativa é `integracao/deploy-treinamentos`, worktree
  `.worktrees/reformulacao-treinamentos` — hml roda a partir dela, não de
  `master`).

## 1. Contexto

O DDS (Diálogo Diário de Segurança) já foi reformulado em 31/08 para o modelo
em papel do usuário: uma `DdsSemanal` (segunda a sexta) contém 5 registros
`Dds` (um por dia). Ao criar o registro do dia, o gestor marca quantas
Atividades quiser da obra (`DdsAtividade`, N:N) e o sistema já cruza
Atividade → Risco → Perigo para montar o checklist do dia
(`DdsItemChecklist`, uma cópia gravada na hora a partir de
`Risco.ControlesExistentes`/`ControlesAdicionais`).

Hoje o "tema" do DDS é **um único campo** (`Dds.TopicoPrincipal`), preenchido
por **uma de três origens mutuamente exclusivas** (`OrigemTemaDds`):
`AutomaticoAtividade1` (nome do Perigo de maior risco da 1ª atividade
marcada), `AutomaticoAtividade2` (idem, 2ª atividade) ou `Livre` (nome de um
item do catálogo `CatalogoTemaDds`, hoje sem tela própria — só criável
inline, dentro do próprio formulário do dia).

O usuário quer que os **3 deixem de ser uma escolha** e passem a coexistir
sempre: os temas das atividades marcadas (não apenas 1ª/2ª — "todo o escopo",
já detalhado abaixo) **e** opcionalmente um tema livre, todos no mesmo
registro, e que o PDF do dia sirva de **roteiro impresso** para o técnico
conduzir o DDS.

## 2. Escopo

**Entra:**
- Atividade marcada no dia deixa de contribuir só com o nome do Perigo de
  maior risco — passa a gravar, por atividade, uma cópia (Perigo, Descrição,
  Consequência, Controles Existentes, Controles Adicionais) do risco de maior
  nível dela. Isso substitui a lógica de "1ª/2ª atividade automática".
- Tema livre (`CatalogoTemaDdsId`) passa a ser **aditivo**, não excludente —
  qualquer `Dds` pode ter 0 ou 1 tema livre, independente de quantas
  atividades foram marcadas.
- Nova tela de administração do catálogo de temas livres (listar, criar,
  editar, excluir) — hoje só existe criar/excluir, sem tela, sem editar.
- Nova aba dentro do grupo onde DDS já mora (pilar "Procedimentos & Planos",
  rotas `prevencao/*`), ao lado de PGR/Inspeções/DDS.
- PDF do dia (`DdsPdfService`) passa a imprimir, por atividade marcada, o
  bloco completo (Perigo/Descrição/Consequência/Controles) em vez de só o
  nome do tema; e o tema livre, se houver.
- Remoção de `Dds.TopicoPrincipal` e `Dds.OrigemTema` (e do enum
  `OrigemTemaDds`) — não fazem mais sentido como campo único/escolha
  exclusiva. Todo consumidor atual desses campos (listados na seção 5) é
  ajustado.

**Não entra (fora do escopo deste spec):**
- Nenhuma mudança em `Atividade`, `Risco` ou `Perigo` — o conteúdo do "tema
  automático" é 100% derivado do que já está cadastrado na Matriz de Riscos;
  não se cria nenhum campo novo de "roteiro" na Atividade (decisão explícita
  do usuário — ver seção 3).
- Nenhuma mudança no fluxo de condução do dia (`DdsDetalhePage.tsx`:
  participantes, fotos, checklist) — só o que envolve o(s) tema(s).
- Nenhuma mudança na `DdsSemanal` (abertura/encerramento da semana).
- Envio ao Telegram (`EnviarDdsTelegramCommand`) — será ajustado apenas o
  suficiente para não quebrar (usa `TopicoPrincipal` hoje), sem redesenhar o
  texto da mensagem além do necessário.

## 3. Modelo de dados

### 3.1 `DdsAtividade` ganha os campos de snapshot

```csharp
public class DdsAtividade : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }
    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }
    public int Ordem { get; set; }

    // Novo — cópia do Risco de maior NivelRisco desta atividade, gravada na
    // criação do Dds (mesmo princípio de DdsItemChecklist: cópia, não
    // referência viva, para o documento de um dia não mudar se o cadastro de
    // Risco for editado depois). Nullable: uma atividade pode não ter nenhum
    // Risco cadastrado ainda (mesmo fallback de texto que já existe hoje em
    // ResolverTemaAutomaticoAsync).
    public string? PerigoNome { get; set; }
    public string? PerigoDescricao { get; set; }
    public string? Consequencia { get; set; }
    public string? ControlesExistentes { get; set; }
    public string? ControlesAdicionais { get; set; }
}
```

Por que na `DdsAtividade` e não numa tabela nova: ela já é o registro "esta
atividade participou deste DDS" — o snapshot do que foi apresentado sobre
essa atividade pertence naturalmente a essa linha, sem precisar de mais um
relacionamento.

### 3.2 `Dds` — tema livre vira aditivo, campos antigos saem

```csharp
public class Dds : AuditableEntity
{
    // ...campos existentes inalterados (ObraId, DdsSemanalId, Data,
    // ResponsavelUsuarioId, Status, Atividades, ItensChecklist,
    // Participantes, FotosEvidencia)...

    // Removidos: TopicoPrincipal, OrigemTema (enum OrigemTemaDds é apagado).

    // Tema livre — agora opcional e aditivo (não mutuamente exclusivo com as
    // atividades). Nome/descrição copiados na criação, mesmo raciocínio do
    // snapshot acima (o item do catálogo pode ser editado ou até excluído
    // depois — ExcluirCatalogoTemaDdsCommand já existe).
    public Guid? CatalogoTemaDdsId { get; set; }
    public CatalogoTemaDds? CatalogoTemaDds { get; set; }
    public string? TemaLivreNome { get; set; }
    public string? TemaLivreDescricao { get; set; }
}
```

### 3.3 `CatalogoTemaDds` — sem mudança de schema

Só ganha um comando de atualização (`AtualizarCatalogoTemaDdsCommand`) — hoje
só existe `Criar`/`Excluir`/`Listar`.

### 3.4 Migration

Uma migration: adiciona as 5 colunas em `DdsAtividade`, adiciona
`TemaLivreNome`/`TemaLivreDescricao` em `Dds`, remove `TopicoPrincipal` e
`OrigemTema` de `Dds`. `CatalogoTemaDdsId` já existe e não muda de tipo (só
deixa de ter `NotEmpty().When(Livre)` na validação — vira sempre opcional).

## 4. Fluxo de criação do dia

`CriarDdsCommand` muda de:

```
AtividadesIds, OrigemTema, CatalogoTemaDdsId? → switch exclusivo → 1 TopicoPrincipal
```

para:

```
AtividadesIds (qualquer quantidade), CatalogoTemaDdsId (opcional)
  → para cada atividade: busca o Risco de maior NivelRisco (mesma query que
    já roda pro checklist) e grava o snapshot em DdsAtividade
  → se CatalogoTemaDdsId informado: copia Nome/Descricao pra TemaLivreNome/
    TemaLivreDescricao
```

Sem mínimo/máximo de atividades além da regra que já existe (pelo menos 1).
Sem exigir tema livre.

**Frontend** (`DdsSemanalDetalhePage.tsx`): remove o rádio "origem do tema";
mantém os checkboxes de atividade; troca o mini-formulário inline de
catálogo por um select simples (Tema livre — opcional) que lista
`CatalogoTemaDds` já cadastrados (criar um novo tema passa a ser feito na
aba própria, não mais na hora).

## 5. Pontos de ajuste (consumidores de `TopicoPrincipal`/`OrigemTema`)

Backend:
- `CriarDdsCommand.cs` — reescrito (seção 4).
- `EnviarDdsTelegramCommand.cs` — troca `TopicoPrincipal` pela concatenação
  dos nomes de Perigo das atividades + `TemaLivreNome`, se houver.
- `DdsDto.cs` / `DdsSemanalDto.cs` — trocam `TopicoPrincipal`/`OrigemTema`
  por uma lista de temas (nome + descrição por atividade) e o tema livre.
- `IDdsPdfService.cs` / `DdsPdfService.cs` — layout novo (seção 6).
- `ExportarDdsPdfQuery.cs` / `ExportarDdsSemanalPdfQuery.cs` — passam o
  snapshot completo pro modelo do PDF em vez do texto único.
- `ListarDdsQuery.cs` / `ObterDdsSemanalDetalheQuery.cs` — projeção ajustada
  pro novo shape do DTO.
- `Dds.cs` / `DdsConfiguracoes.cs` — entidade e mapeamento EF.
- `Enums.cs` — remove o enum `OrigemTemaDds`.

Frontend:
- `lib/api.ts` — tipos `Dds`/`DdsDetalhe` e o novo endpoint de atualizar
  catálogo.
- `DashboardPage.tsx` — onde exibe o tema do DDS mais recente (card de KPI).
- `AssinarDdsPage.tsx` / `DdsDetalhePage.tsx` — cabeçalho que hoje mostra
  `topicoPrincipal`.
- `DdsSemanalDetalhePage.tsx` — formulário de criação (seção 4).

## 6. PDF do dia

Por atividade marcada, um bloco:

```
Atividade: <nome da atividade>
Perigo: <PerigoNome>          Consequência: <Consequencia>
Descrição: <PerigoDescricao>
Controles existentes: <ControlesExistentes>
Controles adicionais: <ControlesAdicionais>
```

Se `TemaLivreNome` estiver preenchido, um bloco final:

```
Tema livre: <TemaLivreNome>
<TemaLivreDescricao>
```

Mesmo padrão visual já usado no restante dos documentos (cor da marca
`#670000`, cabeçalho já existente do `DdsPdfService` — sem mudança de
cabeçalho, só do corpo).

## 7. Nova aba "Temas de DDS"

Página nova `CatalogoTemasDdsPage.tsx`: tabela (Nome, Descrição, ações
editar/excluir) + formulário de criação, mesmo padrão de outras telas de
catálogo simples do sistema (ex.: `ChecklistModelosTab`). Nova entrada no
array `abas` de `PillarLayout` em `App.tsx` (prefixo `prevencao`), rota
`prevencao/temas-dds`.

## 8. Erros e validações

- Criar `Dds` sem nenhuma atividade: já bloqueado hoje (`NotEmpty`), mantém.
- Atividade sem nenhum Risco cadastrado: grava snapshot com
  `PerigoNome = null` e os demais campos nulos; o PDF mostra um aviso
  ("Nenhum risco cadastrado para esta atividade — revisar Matriz de
  Riscos"), mesmo texto de fallback que `ResolverTemaAutomaticoAsync` já usa
  hoje.
- Excluir um `CatalogoTemaDds` que já foi usado em algum `Dds`: permitido
  (o snapshot em `Dds.TemaLivreNome/Descricao` preserva o histórico) — a FK
  `CatalogoTemaDdsId` usa `ON DELETE SET NULL` (mesmo padrão de FK opcional
  já usado no restante do schema), então o registro antigo não quebra nem
  fica travando a exclusão do tema.

## 9. Testes

- `CriarDdsCommandHandlerTests`: snapshot de Perigo/Risco gravado
  corretamente por atividade; tema livre opcional (com e sem
  `CatalogoTemaDdsId`); atividade sem risco cadastrado não quebra.
- `AtualizarCatalogoTemaDdsCommandHandlerTests`: novo comando.
- Ajustar os testes existentes que hoje montam `Dds`/`CriarDdsCommand` com
  `OrigemTema`/`TopicoPrincipal` (buscar por esses símbolos nos testes antes
  de implementar, pra não deixar teste quebrado órfão).
