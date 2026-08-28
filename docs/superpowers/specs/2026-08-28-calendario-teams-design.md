# Integração do Motor de Alertas com o Calendário do Microsoft Teams

- **Data:** 2026-08-28
- **Autor:** Assistente (Claude Code), a pedido de Wellington Lourenço
- **Status:** Aprovado pelo usuário em chat — pronto para plano de implementação
- **Precede:** plano de implementação (writing-plans)
- **Contexto vivo:** `docs/Motor-Assinatura-Eletronica.md` e a memória
  `project_sst_motor_alertas.md` documentam o Motor Central de Alertas já
  existente — este spec cobre só a peça nova (canal de calendário); não
  repete o desenho das Etapas 0-4 já implementadas.

## 1. Contexto

O SST-APP já tem um Motor Central de Alertas (`AlertaEngineService`,
`AAHBRANT.SST.Application/Alertas/Motor`) que roda periodicamente via
`AlertaEngineWorker`, agrega os módulos com vencimento (ASO, Treinamento,
EPI, Extintor, Equipamento — via `IAlertaOrigemProvider`), cria/atualiza/
resolve `Alerta` automaticamente, e já notifica o destinatário no "sino" do
Teams (Activity Feed, via `GraphActivityNotificacaoTeamsService` e a fila
`IFilaNotificacaoTeams`/`INotificacaoTeamsService`).

O usuário quer estender esse motor para também sincronizar cada vencimento
com o **Calendário** do Teams/Outlook do destinatário — não como um novo
fluxo por módulo, mas plugado no ponto onde o Alerta já é criado/atualizado/
resolvido, para que qualquer módulo que já gera Alerta (hoje e no futuro)
ganhe o calendário de graça.

A notificação Activity Feed em si **já está implementada** — falta apenas a
permissão de aplicativo `TeamsActivity.Send` ser consentida pelo admin do
Entra ID (ação administrativa, fora do escopo deste documento). Este spec
não recria essa parte; usa o mesmo App Registration para pedir a permissão
adicional de calendário.

## 2. Escopo

**Entra:**
- Novo canal de calendário plugado no `AlertaEngineService` e nos Commands
  manuais de `Alerta` (`Criar`, `Atualizar`, `Resolver`, `Ignorar`,
  `Excluir`).
- Nova fila dedicada (`IFilaCalendarioTeams`), espelhando o padrão já usado
  pela fila de Activity Feed (ServiceBus real + fallback InMemory).
- Novo serviço `ICalendarioTeamsService` (Infrastructure) que fala com o
  Microsoft Graph (`/users/{id}/events`) para criar, atualizar e cancelar
  eventos de calendário.
- Nova entidade `CalendarioEventoTeams`, que guarda o `GraphEventId` por
  origem (`EntidadeOrigemTipo` + `EntidadeOrigemId`), permitindo localizar o
  evento depois para atualizar/cancelar.
- Eventos de calendário cobrem, de saída, todos os módulos que hoje já
  passam pelo Motor de Alertas: ASO, Treinamento, EPI, Extintor,
  Equipamento — e qualquer módulo futuro que ganhe um
  `IAlertaOrigemProvider`, sem precisar de código novo aqui.

**Não entra (fora do escopo deste spec):**
- DDS — não gera `Alerta` hoje; ficaria de fora até (se) virar um spec
  separado.
- Criar `IAlertaOrigemProvider` para os módulos que ainda não têm um
  (Documento, PT, Inspeção, Não Conformidade, Autorização) — isso é escopo
  do Motor de Alertas em si, não desta integração. Quando esses providers
  existirem, o canal de calendário já os cobre automaticamente.
- Provisionamento da permissão `Calendars.ReadWrite` no Entra ID — ação
  administrativa do usuário/admin do tenant, fora do que código resolve.
  Registrado como dependência bloqueante em §7.
- Leitura/importação de eventos do calendário do Teams de volta para o
  SST-APP — o fluxo é unidirecional (app → calendário).
- Convidar terceiros/participantes no evento — o evento é criado só no
  calendário do `DestinatarioUsuarioId` (ou `ResponsavelUsuarioId` da
  `RegraAlerta`), sem lista de convidados.

## 3. Modelo atual (o que já existe e não muda)

- `Alerta` (`Domain/Entidades/Alerta.cs`): `DestinatarioUsuarioId`,
  `DataLimiteTratamento`, `EntidadeOrigemTipo`/`EntidadeOrigemId`
  (referência polimórfica à origem), `AlertaHistoricoEnvio` (log de envio
  por canal — hoje só usa `Canal = "ActivityFeed"`).
- `AlertaOrigemItem` (`Domain/Interfaces/IAlertaEngineService.cs`): contrato
  que cada `IAlertaOrigemProvider` devolve, já com `DataVencimento`,
  `EntidadeOrigemTipo/Id`, `Titulo`, `Descricao`. É a fonte da data usada
  pelo evento de calendário.
- `AlertaEngineService.ProcessarAsync`: cria/atualiza/resolve `Alerta` a
  partir dos providers, e já enfileira `NotificacaoTeamsMensagem` via
  `IFilaNotificacaoTeams` quando há destinatário.
- `IFilaNotificacaoTeams` / `ServiceBusFilaNotificacaoTeams` /
  `InMemoryFilaNotificacaoTeams`: par de implementações (Service Bus real,
  ativado por config; fallback em `Channel<T>` para dev/CI).
- `ServiceBusNotificacaoTeamsProcessor` / `InMemoryNotificacaoTeamsProcessor`
  (`BackgroundService`): consomem a fila, chamam
  `INotificacaoTeamsService.EnviarAsync`, e gravam o resultado em
  `AlertaHistoricoEnvio` (3 tentativas, backoff 5s×tentativa).
- `GraphActivityNotificacaoTeamsService` / `GraphOptions`: credenciais
  (`ClientSecretCredential`) e chamada HTTP ao Graph
  (`POST /users/{aad}/teamwork/sendActivityNotification`).
- `CriarAlertaCommand` / `AtualizarAlertaCommand` / `ResolverAlertaCommand`
  / `IgnorarAlertaCommand` / `ExcluirAlertaCommand`
  (`Application/Alertas/Commands`): pontos manuais de mutação do `Alerta`.

Nenhuma dessas peças muda de comportamento — o canal de calendário se
adiciona ao lado do que já existe, sem alterar a lógica do Activity Feed.

## 4. Design proposto

### 4.1 Modelo de dados

Novo enum (`Domain/Enums/Enums.cs`):
```csharp
public enum OperacaoCalendarioTeams { Criar = 1, Atualizar = 2, Cancelar = 3 }

public enum StatusCalendarioEvento { Pendente = 1, Criado = 2, Cancelado = 3, Falhou = 4 }
```

Nova entidade (`Domain/Entidades/CalendarioEventoTeams.cs`):
```csharp
public class CalendarioEventoTeams : AuditableEntity
{
    public string EntidadeOrigemTipo { get; set; } = string.Empty; // "Alerta" (única origem por ora)
    public Guid EntidadeOrigemId { get; set; }
    public Guid OrganizadorUsuarioId { get; set; }
    public Usuario? OrganizadorUsuario { get; set; }
    public string? GraphEventId { get; set; }
    public StatusCalendarioEvento Status { get; set; } = StatusCalendarioEvento.Pendente;
    public string? MensagemErro { get; set; }
}
```
Índice único em `(EntidadeOrigemTipo, EntidadeOrigemId)` — um evento de
calendário por origem. `EntidadeOrigemTipo` é string (não enum) pelo mesmo
motivo do campo homônimo em `Alerta`: permite novas origens sem migração de
schema.

### 4.2 Contratos (Application)

```csharp
// Common/Interfaces/IFilaCalendarioTeams.cs
public record CalendarioTeamsMensagem(
    string EntidadeOrigemTipo,
    Guid EntidadeOrigemId,
    OperacaoCalendarioTeams Operacao,
    Guid OrganizadorUsuarioId,
    string? Titulo,
    string? Descricao,
    DateTime? Data); // data do vencimento; irrelevante para Cancelar

public interface IFilaCalendarioTeams
{
    Task EnfileirarAsync(CalendarioTeamsMensagem mensagem, CancellationToken ct = default);
}

// Common/Interfaces/ICalendarioTeamsService.cs
public interface ICalendarioTeamsService
{
    Task<string> CriarEventoAsync(
        Guid organizadorUsuarioId, string titulo, string? descricao, DateTime data, CancellationToken ct = default);

    Task AtualizarEventoAsync(
        Guid organizadorUsuarioId, string graphEventId, string titulo, string? descricao, DateTime data,
        CancellationToken ct = default);

    Task CancelarEventoAsync(
        Guid organizadorUsuarioId, string graphEventId, CancellationToken ct = default);
}
```

`ICalendarioTeamsService` segue o mesmo motivo de existir em Application
como interface e em Infrastructure como implementação que
`INotificacaoTeamsService` já documenta: depende do SDK/Graph que
Application não referencia.

### 4.3 Infrastructure

- `GraphCalendarioTeamsService : ICalendarioTeamsService`
  (`Integracao/Teams/`) — reaproveita `GraphOptions` e
  `ClientSecretCredential` já existentes (mesmo App Registration). Chama:
  - `POST /users/{aad}/events` (criar)
  - `PATCH /users/{aad}/events/{graphEventId}` (atualizar)
  - `DELETE /users/{aad}/events/{graphEventId}` (cancelar)
  Lança exceção em falha (nunca engole), mesmo padrão do
  `GraphActivityNotificacaoTeamsService` — quem decide o que fazer é o
  processador da fila.
- `ServiceBusFilaCalendarioTeams` / `InMemoryFilaCalendarioTeams`
  (`Integracao/Bot/`) — mesma estrutura de
  `ServiceBusSender`/`Channel<CalendarioTeamsMensagem>` que já existe para
  notificação, com fila própria (`ServiceBus:FilaCalendarioTeams` em
  config).
- `ServiceBusCalendarioTeamsProcessor` / `InMemoryCalendarioTeamsProcessor`
  (`BackgroundService`) — consomem a mensagem:
  1. Busca a linha `CalendarioEventoTeams` por
     `(EntidadeOrigemTipo, EntidadeOrigemId)`; cria uma nova
     (`Status = Pendente`) se `Operacao == Criar` e não existir ainda.
  2. Chama o método correspondente de `ICalendarioTeamsService`.
  3. Em sucesso: grava `GraphEventId` (só no caso Criar) e
     `Status = Criado` ou `Cancelado`.
  4. Em falha: grava `Status = Falhou` e `MensagemErro`; retry (3
     tentativas, backoff 5s×tentativa), mesmo padrão do processador de
     Activity Feed.

### 4.4 Pontos de integração

| Gatilho | Ação enfileirada | Data usada |
|---|---|---|
| `AlertaEngineService` cria `Alerta` novo com destinatário | `Criar` | `item.DataVencimento` |
| `AlertaEngineService` atualiza `Alerta` existente que já tinha destinatário | `Atualizar` | `item.DataVencimento` |
| `AlertaEngineService` resolve automaticamente (item saiu do vencimento) | `Cancelar` | — |
| `CriarAlertaCommand` manual, com destinatário e `DataLimiteTratamento` | `Criar` | `DataLimiteTratamento` |
| `AtualizarAlertaCommand`, com destinatário | `Atualizar` (ou `Criar` se ainda não havia evento) | `DataLimiteTratamento` |
| `ResolverAlertaCommand` / `IgnorarAlertaCommand` / `ExcluirAlertaCommand` | `Cancelar` (só se existir `CalendarioEventoTeams` com `Status = Criado`) | — |

Sem `DestinatarioUsuarioId` ou sem data, nenhuma mensagem é enfileirada —
mesma guarda que já existe hoje para o Activity Feed.

### 4.5 Payload do Graph — evento de dia inteiro

Todo evento é criado como dia inteiro (`isAllDay: true`), sem horário
específico, e `showAs: "free"` para não aparecer como "ocupado" na agenda de
quem recebe (é um lembrete de vencimento, não uma reunião). Eventos de dia
inteiro no Graph exigem `end` = `start` + 1 dia:

```json
POST /users/{AzureAdObjectId}/events
{
  "subject": "AsoVencendo: ASO de João (faltam 5 dias)",
  "body": { "contentType": "text", "content": "..." },
  "start": { "dateTime": "2026-09-15T00:00:00", "timeZone": "America/Sao_Paulo" },
  "end":   { "dateTime": "2026-09-16T00:00:00", "timeZone": "America/Sao_Paulo" },
  "isAllDay": true,
  "showAs": "free"
}
```

## 5. Tratamento de erro e resiliência

Mesmo padrão do Activity Feed: falha em qualquer chamada ao Graph nunca
derruba o `AlertaEngineWorker` nem os Commands de Alerta — a exceção é
lançada só dentro do `GraphCalendarioTeamsService`, capturada pelo
processador da fila, que decide retry/registro. A diferença em relação ao
Activity Feed é que aqui há **estado a rastrear** (o `GraphEventId`
precisa sobreviver para uma futura atualização/cancelamento), por isso o
resultado vai na própria linha `CalendarioEventoTeams` (`Status`,
`MensagemErro`), em vez de um log append-only como `AlertaHistoricoEnvio`.

Cenário de borda: se `Atualizar`/`Cancelar` chegam para uma origem cujo
`CalendarioEventoTeams.Status` ainda é `Pendente` ou `Falhou` (a criação
nunca teve sucesso, não existe `GraphEventId`), o processador não chama o
Graph — não há o que atualizar/cancelar. Loga e descarta a mensagem.

## 6. Configuração e permissões

Reaproveita a seção `Graph` existente em `appsettings`
(`TenantId`/`ClientId`/`ClientSecret`) — nenhuma configuração nova de
credencial. Duas mudanças de configuração:

1. Nova entrada `ServiceBus:FilaCalendarioTeams` (nome da fila), mesmo
   padrão de `ServiceBus:FilaNotificacoesTeams`.
2. O App Registration no Entra ID precisa da permissão de aplicativo
   **`Calendars.ReadWrite`** (Microsoft Graph), com consentimento de admin
   do tenant — a mesma pendência administrativa que já existe hoje para
   `TeamsActivity.Send` (podem ser solicitadas juntas).

## 7. Riscos e dependências

- **Bloqueante para produção:** sem a permissão `Calendars.ReadWrite`
  consentida, `GraphCalendarioTeamsService` lança exceção em toda chamada
  (mesmo comportamento do Activity Feed hoje sem `TeamsActivity.Send`) — o
  código funciona e é testável, mas não cria eventos reais até a permissão
  existir.
- **Volume:** hoje 5 módulos já geram Alerta automaticamente
  (ASO/Treinamento/EPI/Extintor/Equipamento); cada vencimento em aberto
  gera um evento de calendário por execução do `AlertaEngineWorker` que o
  reavalia — como o motor já é idempotente por
  `(EntidadeOrigemTipo, EntidadeOrigemId)`, isso não deve gerar eventos
  duplicados, mas a Fase de implementação precisa de um teste de
  integração cobrindo especificamente essa idempotência (rodar o motor 2x
  seguidas sobre o mesmo item vencido e confirmar 1 evento só).
- **`Usuario.AzureAdObjectId` pode estar vazio:** trabalhador/usuário
  pré-cadastrado que nunca logou não tem esse campo — mesma limitação já
  documentada no Activity Feed. O processador trata isso como falha comum
  (registrada em `CalendarioEventoTeams.MensagemErro`), não como caso
  especial.

## 8. Testes

- Unitário: `GraphCalendarioTeamsService` (mock de `HttpClient`) — payload
  correto para criar/atualizar/cancelar, inclusive o cálculo de `end` =
  `start + 1 dia` para dia inteiro.
- Unitário: processadores (`InMemoryCalendarioTeamsProcessor` primeiro,
  espelhando `InMemoryFilaNotificacaoTeamsTests` já existente) — cenários
  de sucesso, falha com retry, e o cenário de borda de §5 (Atualizar/
  Cancelar sem `GraphEventId`).
- Integração: `AlertaEngineService.ProcessarAsync` gerando `Alerta` com
  destinatário → mensagem de calendário enfileirada com os dados certos;
  idempotência ao rodar 2x (ver §7).
- Integração: ciclo completo `CriarAlertaCommand` → `ResolverAlertaCommand`
  enfileira `Criar` e depois `Cancelar` na ordem certa.
