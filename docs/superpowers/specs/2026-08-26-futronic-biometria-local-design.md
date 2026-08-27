# Biometria Digital Local (Futronic FS80H) no Quiosque de Assinatura

- **Data:** 2026-08-26
- **Autor:** Assistente (Claude Code), a pedido de Wellington Lourenço
- **Status:** Aprovado pelo usuário em chat — pronto para plano de implementação
- **Precede:** plano de implementação (writing-plans)
- **Contexto vivo:** `docs/Motor-Assinatura-Eletronica.md` §1.6 (levantamento de
  mercado e decisão de 2026-08-26 registrados lá) — este spec detalha só a
  peça nova; não repete a pesquisa de hardware já documentada.

## 1. Contexto

O Motor de Assinatura Eletrônica hoje resolve "biometria no leitor
compartilhado da obra" via WebAuthn/FIDO2 (`Fido2AutenticacaoStrategy`,
`TipoAutenticadorWebAuthn.LeitorObra`), com a garantia de que o template da
digital nunca sai do autenticador. Nenhum leitor FIDO2 nativo confirmado
comporta os ~100 trabalhadores de uma obra num único aparelho (o melhor
confirmado, FEITIAN BioPass K50 Pro, vai até 50).

O usuário decidiu comprar o **Futronic FS80H** — um scanner óptico USB puro,
sem FIDO2/CTAP2, cujo SDK (`ScanAPI` para captura + `ftrapi` para extração de
minúcias e matching 1:1/1:N) precisa rodar em algum processo que fale
diretamente com o dispositivo. Essa decisão aceita explicitamente o
trade-off de que o template deixa de ficar isolado num secure element de
hardware (ver decisão registrada em `Motor-Assinatura-Eletronica.md` §1.6).

Este documento cobre a arquitetura da peça nova necessária para viabilizar
isso: um **agente local** no PC do quiosque, mais o que muda no backend e no
frontend para recebê-lo.

## 2. Escopo

**Entra:**
- Agente local (.NET, Windows) que fala com o FS80H via SDK Futronic, faz o
  match 1:N localmente contra um cache de templates da obra, e expõe uma API
  HTTP local (`127.0.0.1`) para a página do quiosque.
- Entidade + fluxo de **registro de dispositivo** (o quiosque físico como
  "algo que ele tem", já que aqui não há secure element garantindo isso).
- Entidade + fluxo de **cadastro de digital** (enrollment) por trabalhador —
  peça que não existe hoje em nenhuma forma equivalente.
- Endpoint novo de autenticação no `AssinaturaController`, nova estratégia
  `FutronicAutenticacaoStrategy`, novo enum de tipo de credencial biométrica
  local.
- Endpoint de sincronização de templates (agente → backend, no sentido de
  baixar o cache).
- Tela de cadastro de digital no perfil do trabalhador (ao lado da aba de
  WebAuthn já existente).
- Reescrita do parágrafo de consentimento LGPD que hoje promete "o template
  não sai do leitor" (item já registrado como pendência no doc de
  arquitetura).

**Não entra (fora do escopo deste spec):**
- Compra/instalação física do hardware — depende do usuário.
- Escolha final de motor de liveness/anti-spoofing além do que o próprio SDK
  Futronic já oferecer (LFD) — usar o que vem de fábrica; não avaliar
  hardware adicional agora.
- Instalador/deploy do agente nas máquinas da obra (MSI, atualização
  automática) — o plano de implementação decide o mínimo viável para rodar
  em ambiente de desenvolvimento/teste; distribuição em campo é decisão
  operacional posterior.
- Migrar o WebAuthn existente para Futronic — as duas estratégias convivem;
  `MetodoAutenticacaoObra.Biometria` já é o mesmo flag para as duas, cada
  obra continua escolhendo o que tem instalado.

## 3. Modelo atual (o que já existe e não muda)

- `IAutenticacaoAssinaturaService` (1 chamada, crachá+PIN) e
  `IAutenticacaoWebAuthnService` (2 chamadas, desafio/resposta) — o
  comentário do primeiro já antecipa que cada novo método ganha sua própria
  interface em vez de forçar um contrato genérico.
- `MetodoAutenticacaoAssinatura.Biometria` (valor 1) e
  `MetodoAutenticacaoObra.Biometria` (flag 1) — hoje só preenchidos pelo
  WebAuthn `LeitorObra`. Continuam sendo o rótulo "Biometria (leitor da
  obra)" independente de qual hardware por trás resolveu a identificação.
- `CredencialWebAuthn` — guarda só o que o protocolo WebAuthn exige
  (`CredentialId`, `PublicKey`, `UserHandle`, `SignCount`), nunca o template.
  **Não será reaproveitada** para o Futronic — misturaria dois modelos de
  confiança diferentes na mesma tabela e quebraria essa garantia documentada
  no comentário da entidade.
- `Trabalhador.TermoAceiteAssinaturaEletronicaEm` /
  `.ConsentimentoBiometriaEm` — gates jurídicos já checados por
  `CrachaPinAutenticacaoStrategy` e `Fido2AutenticacaoStrategy`; a nova
  estratégia reaproveita os dois campos, sem mudança de schema aqui.
- `CpfCriptografiaConversor` / `CpfCriptografiaContexto` — padrão de
  criptografia de aplicação (AES-256-GCM, chave carregada uma vez em
  `DependencyInjection.AddInfrastructure`) reaproveitado para o template.
- `PinHasher` (PBKDF2, 210k iterações, salt por registro) — construído para
  segredo de **baixa entropia** (PIN de 4-6 dígitos). O segredo do
  dispositivo do quiosque é um token aleatório de alta entropia (256 bits) —
  **não** reaproveitar o PinHasher aqui (custo de PBKDF2 é desnecessário
  para uma chave já impossível de forçar por busca); um hash único
  SHA-256 do segredo é suficiente para comparação sem guardá-lo em claro.

## 4. Modelo proposto

### 4.1 Backend — novas entidades

```csharp
// Quiosque físico registrado por obra — a "posse" que substitui a garantia
// de secure element do FIDO2.
public class DispositivoAgenteBiometrico : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public string Nome { get; set; } = string.Empty;       // ex. "Quiosque Portaria"
    public string SegredoHash { get; set; } = string.Empty; // SHA-256 do token, nunca o token em claro
    public bool Ativo { get; set; } = true;
    public DateTime? UltimoHeartbeatEm { get; set; }
}

// Template de digital cadastrado por trabalhador — deliberadamente separada
// de CredencialWebAuthn (ver §3).
public class TemplateBiometricoFutronic : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public string TemplateCriptografado { get; set; } = string.Empty; // AES-256-GCM, mesmo padrão do CPF
    public DateTime CadastradoEm { get; set; }
}
```

Índice único em `TemplateBiometricoFutronic.TrabalhadorId` — um template
consolidado por trabalhador (o SDK já lida com múltiplas capturas na hora do
enrollment para gerar um template robusto; não guardamos 2-3 templates
separados).

Chave de criptografia do template: **reaproveita o mesmo par de chaves do
CPF** (`Lgpd:ChaveCriptografiaCpfBase64`) ou usa uma chave dedicada
(`Lgpd:ChaveCriptografiaBiometriaBase64`)? Recomendo chave **dedicada** —
rotação/revogação de uma não deve afetar a outra, e são categorias de dado
sensível diferentes para efeito de resposta a incidente. Decisão a confirmar
no plano de implementação junto da config.

### 4.2 Backend — Application

Nova interface (1 chamada, mesmo estilo de `IAutenticacaoAssinaturaService`):

```csharp
public interface IAutenticacaoBiometriaLocalService
{
    Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, CancellationToken ct);
}
```

`FutronicAutenticacaoStrategy : IAutenticacaoBiometriaLocalService` valida,
nesta ordem:
1. `DispositivoAgenteBiometrico` existe, está `Ativo`, e o hash do segredo
   recebido bate (`CryptographicOperations.FixedTimeEquals`).
2. `Trabalhador` existe e pertence à mesma `ObraId` do dispositivo (um
   quiosque da Obra A não pode confirmar identidade de trabalhador da Obra B
   — mesmo raciocínio já aplicado em `CrachaPinAutenticacaoStrategy`).
3. `Obra.MetodosAutenticacaoHabilitados.HasFlag(MetodoAutenticacaoObra.Biometria)`.
4. `TermoAceiteAssinaturaEletronicaEm` e `ConsentimentoBiometriaEm` não nulos.
5. `score` acima do limiar mínimo configurado (`AssinaturaOptions` ganha um
   novo campo, ex. `LimiarConfiancaBiometriaLocal`).

Retorna `ResultadoAutenticacaoAssinatura(trabalhador.Id,
MetodoAutenticacaoAssinatura.Biometria)` — mesmo tipo de retorno do WebAuthn,
então `RegistrarAssinaturaCommand`/PDF/trilha de auditoria não mudam nada.

Comandos novos para o ciclo de vida:
```csharp
public record RegistrarDispositivoAgenteCommand(Guid ObraId, string Nome) : IRequest<string>; // retorna o segredo em claro, 1 vez só
public record CadastrarTemplateBiometricoCommand(Guid TrabalhadorId, byte[] TemplateBruto) : IRequest;
public record SincronizarTemplatesQuery(Guid DispositivoId, string SegredoDispositivo) : IRequest<List<TemplateSincronizadoDto>>;
```

`SincronizarTemplatesQuery` retorna os templates (ainda criptografados) de
todos os trabalhadores da obra do dispositivo — o agente descriptografa
localmente (precisa da mesma chave simétrica, distribuída ao agente fora de
banda na instalação, não via API) e monta o cache que o `ftrapi` usa para o
match 1:N.

### 4.3 Backend — API

Em `AssinaturaController`:
```
POST /api/documentos/{id}/autenticacao/biometria-local
  body: { DispositivoId, SegredoDispositivo, TrabalhadorId, Score }
  → FutronicAutenticacaoStrategy → IRegistradorAssinaturaService (mesmo pipeline final do crachá/WebAuthn)
```

Novo controller `DispositivosAgenteController` (ou endpoints dedicados em
`ObrasController`, a decidir no plano):
```
POST /api/obras/{obraId}/dispositivos-agente        → RegistrarDispositivoAgenteCommand
GET  /api/dispositivos-agente/{id}/templates         → SincronizarTemplatesQuery (autenticado pelo segredo do dispositivo, não por cookie/JWT de usuário)
```

Em `TrabalhadoresController`, ao lado do WebAuthn:
```
POST /api/trabalhadores/{id}/assinatura/biometria-local/cadastro   → CadastrarTemplateBiometricoCommand
```

**Policy:** `POST biometria-local` (autenticação de assinatura) usa
`assinatura:assinar`, igual ao WebAuthn. O endpoint de sincronização de
templates **não** deve usar policy de usuário Teams — é chamado pelo agente
local sem sessão de usuário, autenticado só pelo segredo do dispositivo
(mesmo raciocínio de "service-to-service" que o padrão de `[Authorize]`
baseado em claims de usuário não cobre bem; o plano de implementação decide
se isso é um `[AllowAnonymous]` com validação manual do segredo no handler,
ou um esquema de autenticação dedicado).

### 4.4 Agente local (novo componente, fora da solution ASP.NET atual)

Aplicação .NET (Windows Service ou app com tray icon — decisão de UX para o
plano) instalada no PC do quiosque:
- Fala com o FS80H via P/Invoke sobre a DLL Futronic (`ScanAPI.dll` +
  `ftrapi.dll` no Windows, equivalentes aos `.so` do Linux já documentados).
- Ao iniciar (e periodicamente), chama
  `GET /api/dispositivos-agente/{id}/templates`, descriptografa e monta o
  cache local em memória (nunca grava o template descriptografado em disco).
- Expõe servidor HTTP só em `127.0.0.1:<porta>`, com:
  - CORS restrito à origem do SST app (domínio do TeamsApp em produção /
    `localhost:<porta-vite>` em dev).
  - Token de sessão de curta duração, gerado pelo backend quando a página do
    quiosque carrega e repassado à página — evita que outra aba/site no
    mesmo PC dispare uma captura.
  - Endpoint `POST /capturar` (bloqueia até detectar um dedo, faz o match
    local, devolve `{ TrabalhadorId, Score }` ou erro de "não reconhecido").
  - Endpoint `POST /cadastrar` (modo enrollment, usado só pela tela de
    cadastro de digital do perfil do trabalhador).

### 4.5 TeamsApp — UI

- `AssinaturaQuiosque.tsx`: novo método de autenticação "Biometria (leitor
  local)" ao lado dos já existentes. Chama o agente local (`POST
  http://127.0.0.1:<porta>/capturar`), recebe `{TrabalhadorId, Score}`, e
  então chama `POST /api/documentos/{id}/autenticacao/biometria-local` no
  backend com o `DispositivoId`/segredo configurados no quiosque (via env
  var ou config local do agente — não digitados pelo trabalhador).
- Aba de Assinatura do perfil do trabalhador (mesmo lugar do
  `AssinaturaTab.tsx` que já lista credenciais WebAuthn): novo bloco
  "Digital cadastrada" com botão "Cadastrar digital", que chama o agente
  local em modo `/cadastrar` e depois
  `POST /trabalhadores/{id}/assinatura/biometria-local/cadastro`.
- `lib/api.ts`: novos métodos espelhando os dois endpoints acima.

## 5. LGPD — consentimento

O texto atual de `ConsentimentoBiometriaEm` promete "o template não sai do
leitor" — não é mais verdade nesta arquitetura (o template cacheado é
descriptografado em memória no agente, que não é mais um secure element
isolado). Reescrever antes de qualquer coleta real via Futronic. Sugestão de
direção (não é o texto final — jurídico deve revisar por ser cláusula de
consentimento formal): trocar a promessa de "nunca sai do leitor" por
"template criptografado, armazenado sob controle da AAHBRANT, usado
exclusivamente para confirmar sua identidade nas assinaturas eletrônicas
desta obra". Este spec só sinaliza a necessidade; o texto final fica fora do
escopo de implementação de código.

## 6. Risco de segurança aceito (documentar, não é decisão a reabrir)

Diferente do FIDO2 (prova criptográfica ligada a hardware), aqui o backend
confia na identificação feita localmente pelo agente. A "prova" vira posse
do dispositivo registrado (segredo do quiosque) + resultado do match local.
Isso depende de:
- O segredo do dispositivo não vazar (só existe em memória/config do
  agente, nunca em código-fonte ou log).
- O SDK Futronic aplicar Live Finger Detection contra digital falsa/foto —
  usar o que o hardware já oferece, sem componente extra.

Comparável em força a crachá+PIN (posse + conhecimento vs. aqui posse +
característica física), não comparável à garantia do FIDO2. Já aceito pelo
usuário na decisão de 2026-08-26.

## 7. Migrations

Duas migrations novas (nomes sugeridos):
- `AdicionarDispositivoAgenteBiometrico`
- `AdicionarTemplateBiometricoFutronic`

Sem impacto em dados existentes — tabelas novas. Sem seed obrigatório
(cadastro de dispositivo e de digital são operacionais, pós-deploy).

## 8. Pendências (não decidíveis por mim — registrar e seguir)

1. **Chave de criptografia do template** (§4.1) — dedicada vs. reaproveitar
   a do CPF. Recomendo dedicada; confirmar no plano.
2. **Esquema de autenticação do endpoint de sincronização de templates**
   (§4.3) — `[AllowAnonymous]` + validação manual vs. esquema dedicado.
3. **Onde os comandos de dispositivo vivem** — controller novo
   (`DispositivosAgenteController`) vs. sub-rotas de `ObrasController`.
4. **UX do agente** — Windows Service silencioso vs. app com tray icon
   (afeta como o time de obra diagnostica problema de conexão do leitor).
5. **Distribuição/instalação do agente em campo** — fora do escopo deste
   spec (§2), mas o plano de implementação deve pelo menos deixar claro o
   que falta para isso (instalador, driver USB, etc.) mesmo sem construir.
6. **Texto final do consentimento LGPD** (§5) — fora do escopo de código,
   mas é bloqueio operacional antes de qualquer coleta real.

## 9. Testes (alto nível)

- Unidade: `FutronicAutenticacaoStrategy` — segredo de dispositivo inválido
  rejeita; dispositivo de outra obra rejeita; obra sem `Biometria`
  habilitada rejeita; score abaixo do limiar rejeita; termo/consentimento
  ausente rejeita; caminho feliz retorna `Biometria` com o `TrabalhadorId`
  certo.
- Unidade: hash do segredo do dispositivo — comparação em tempo constante,
  nunca compara string em claro.
- Integração: fluxo completo de `RegistrarDispositivoAgenteCommand` →
  `SincronizarTemplatesQuery` retorna só templates da obra certa.
- Manual (sem hardware disponível ainda): não é possível validar o agente
  local ponta a ponta até o FS80H chegar — o plano deve isolar o que dá para
  testar sem o dispositivo físico (mock do resultado de captura) do que
  fica pendente até a chegada do hardware.
