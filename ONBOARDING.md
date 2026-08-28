# ONBOARDING — App de SST AAHBRANT

> Handoff para iniciar uma sessão nova (outra conta/máquina) sem perder contexto.
> Gerado em 2026-08-21. **Reescrito em 2026-08-28** para refletir o estado real do código, e
> **atualizado no mesmo dia, duas vezes**, após descobrir e reconciliar **duas linhas de trabalho
> divergentes que nunca tinham sido integradas** — ver §0. Trate este arquivo como o estado mais
> recente conhecido, mas **sempre confirme contra o código atual** antes de agir.

## 0. O que aconteceu em 28/08 — leia antes de tudo

Nesta sessão descobrimos que o repositório tinha **três coisas rodando em paralelo sem nunca terem
sido unidas**:

1. **`master`** (GitHub, `AAHBRANT/AHBT-SST`) — seguiu até 28/08 com EPI reformulado (3 fases),
   Motor de Assinatura Eletrônica, biometria Futronic, Motor de Alertas + Calendário Teams.
2. **`origin/master-q7x0c1`** (GitHub, mesma org) — divergiu de `master` em 24/08 e parou em 25/08,
   mas tinha PCMSO (versão 1, entidade própria), **RBAC Camada 2/3** (escopo por obra + Global
   Query Filter) e o **piloto de sincronização offline** (DDS/Inspeções/Checklists/APRs) —
   nenhuma dessas três coisas existia em `master`.
3. **Trabalho local, nunca commitado/enviado a lugar nenhum**, numa pasta separada
   (`C:\Projetos\SST-APP`) apontando para um remote com o **nome antigo da mesma organização**
   (`DesenvolvimentoAHBT/AHBT-SST` — o GitHub confirma via redirect que é o mesmo repo que
   `AAHBRANT/AHBT-SST`, só renomeado). Essa pasta tinha o módulo **Saúde Ocupacional** completo
   (ASO + PCMSO versão 2 + Exames Complementares + Aptidões), **já rodando em produção/homologação
   no Teams**, mas com 3 commits que só existiam no disco daquele computador.

**O que foi feito, nesta ordem:**
- Mesclado `master-q7x0c1` em cima de `master` → trouxe RBAC Camada 2/3 e sincronização offline,
  e uma primeira versão de PCMSO.
- Feito backup do trabalho local não commitado numa branch nova (`backup/saude-ocupacional-28-08`,
  já enviada ao GitHub) — para não perder nada enquanto decidíamos o que fazer.
- **Decisão do usuário**: a versão de Saúde Ocupacional que já está em produção manda. A versão 1
  de PCMSO (do `master-q7x0c1`, entidade própria `Pcmso`/`PcmsoItemMatriz`/`PcmsoRevisao`) foi
  **removida** do código e da migration correspondente ficou só a parte que não é sobre PCMSO (ver
  nota de migrations abaixo). A versão 2 (que reaproveita `DocumentoGestao`, mais completa —
  `PcmsoDetalhe`/`ExameComplementar`/`AptidaoAtividadeEspecifica`) é a que vale.
- Resultado: tudo unificado na branch `claude/onboarding-md-review-dban6k`.

**Pendências deixadas por essa reconciliação (não resolvidas ainda, ver §6 para a lista completa):**
- **Backend nunca foi compilado nesta sessão** (sem SDK .NET no ambiente onde isso foi feito). Só o
  frontend foi validado (`npx tsc --noEmit`, sem erros, duas vezes — antes e depois da 2ª mesclagem).
  **Rodar `dotnet build` + testes é obrigatório antes de qualquer deploy.**
- **Migrations em estado inconsistente**: a migration `20260825110602_AdicionarSincronizacaoRbacPcmso`
  (do `master-q7x0c1`) ainda cria as tabelas `Pcmsos`/`PcmsoItensMatriz`/`PcmsoRevisoes` no `Up()`,
  mas a entidade C# correspondente foi apagada — não editei essa migration à mão (arquivo gerado,
  ~1400 linhas, risco alto de corromper sem poder rodar `dotnet ef` para validar). **Antes de
  aplicar migrations em qualquer banco**, alguém com o SDK .NET precisa rodar
  `dotnet ef migrations add ReconciliarSaudeOcupacional` (ou equivalente) para o EF detectar essa
  divergência e gerar a migration de limpeza corretamente. Enquanto isso não acontecer, **não
  rodar `dotnet ef database update` a partir desta branch** contra o banco de produção.
- Motor de alertas: a implementação que existia em `master-q7x0c1`
  (`VerificacaoAutomaticaAlertasService`) tinha **escalonamento automático** de alertas parados —
  foi descartada em favor da versão de `master` (mais completa noutros aspectos), que não tem essa
  capacidade. Avaliar se vale portar.
- **Duas capacidades de RBAC ainda não cobrem Saúde Ocupacional**: `PcmsoDetalhe` (não tem `ObraId`
  direto, herda de `DocumentoGestao`) e `ExameComplementar`/`AptidaoAtividadeEspecifica` (são por
  Trabalhador, não por Obra) não têm filtro de escopo por obra (Camada 3) — ver comentário em
  `SstDbContext.OnModelCreating`.
- **`REFORMULAÇÃO.md`** (na pasta local `C:\Projetos\SST-APP`, ainda não commitado em lugar
  nenhum) — descreve uma estrutura "PR-SST-001, PR-SST-002, PR-SST-003..." (EPI, Treinamentos,
  Saúde Ocupacional). Não foi processado/validado — confirmar intenção com o usuário antes de
  tratar como escopo novo.
- **Deploy em produção está desatualizado em relação a este código**: a tag `saude-ocupacional-v1`
  rodando em hml (ver §7) é de ANTES desta reconciliação — não tem RBAC Camada 2/3 nem
  sincronização offline. Um novo deploy será necessário depois que o build for validado.

## 1. O que é este projeto

App de **Segurança e Saúde no Trabalho (SST)** da AAHBRANT, Teams-first, para construção civil.
**Escopo Fase 1: apenas o app de SST** — não é o "Hub Gênesis" administrativo completo (RH,
financeiro etc. ficam de fora). **Já está no ar em homologação** (Microsoft Teams, via Azure
Container Apps) — não é mais só ambiente de desenvolvimento local. Ver §7.

Documentos-fonte — **fonte da verdade, seguir à risca**:
- `Base de Conhecimento — Projeto de Aplicação e Controle de SST.md` — 50 seções, todo o domínio
  de SST. **Toda entidade/enum novo precisa citar a seção/linha exata**, ou ser sinalizado
  explicitamente como decisão não-literal.
- `PROJECT RULES.md` — arquitetura fixa (ver §2).
- `DESING SYSTEM AAHBRANT.md` — tokens visuais (ver §2, item 6 — conflito de cor não resolvido).
- `docs/ERD.md`, `docs/RBAC-Matrix.md` — rascunhos técnicos (RBAC ainda **não validado** pela
  Diretoria/QSMS; matriz Perfil×Permissão continua deliberadamente vazia; `ERD.md` foi atualizado
  em 28/08 mas **ainda não reflete a reconciliação desta sessão** — revisar de novo).
- `docs/Motor-Assinatura-Eletronica.md` — spec do Motor de Assinatura Eletrônica (biometria
  Futronic/FIDO2/crachá/PIN), atualizada em 28/08.
- `docs/referencias/` — planilhas/modelos reais de SSO fornecidos pelo usuário (Lista Mestra de
  Documentos, modelo de PT, ficha de entrega de EPI, planilha de riscos por função).
- `docs/superpowers/specs/*.md` — specs de brainstorming aprovadas por feature.
- `ANALISE DE OPORTUNIDADES - HUB GENESIS SST - 2026-08-19.md` — lacunas críticas diagnosticadas
  na origem do projeto (a maioria já resolvida, ver §4/§6).
- **`REFORMULAÇÃO.md`** — ver ressalva no §0.

## 2. Instruções permanentes (não negociáveis)

1. **"Implementação real! Seguir à risca a Base de Conhecimento .md da pasta."** — nunca
   mockup/protótipo. Toda entidade/status/enum não citado literalmente no `.md` deve ser
   sinalizado ao usuário como decisão minha, não do documento.
2. **Cada módulo de SST é uma fatia vertical independente**, com sua própria migration EF Core
   isolada — nunca misturar entidades/migrations entre módulos.
3. **Arquitetura fixa** (`PROJECT RULES.md`): Teams-first (Personal Tab, Group/Channel Tab, Bot),
   Entra ID SSO (sem autenticação local por senha), Azure Container Apps (não App Service),
   Microsoft Graph API.
4. **Clean Architecture** — regra de dependência: `Domain` → nada; `Application` → só `Domain`;
   `Infrastructure`/`Api`/`Worker` → `Application`. Implementações de `IEligibilityRule` ficam em
   `Application/Elegibilidade/Rules/`, **nunca** em `Infrastructure`.
5. **Barra de verificação obrigatória**: todo módulo só é considerado "pronto" depois de
   verificação real ponta-a-ponta no navegador — nunca só `dotnet build` ou Swagger. (Exceções já
   documentadas: reformulação de EPI de 27/08 e foto de DDS de 24/08, verificadas só por
   log/teste automatizado; e agora também a reconciliação de 28/08, sem `dotnet build` disponível.)
6. **Organização (AAHBRANT)**: responder sempre em português (Brasil), direto e prático;
   identidade visual vinho/marsala — **conflito ainda aberto**: regra da organização diz
   `#670000`, mas `DESING SYSTEM AAHBRANT.md` documenta `#7B1E2B` e é esse valor que está de fato
   implementado em `theme.ts`/`manifest.json`/`DdsPdfService.cs`. Não alterar cor sem o usuário
   decidir qual é a oficial. Nunca inventar dado — sinalizar incerteza; explicar antes de alterar
   arquivo e evitar sobrescrever sem confirmação; documentos institucionais em linguagem formal.
7. **Não resetar/reseedar o banco de dev local sem pedir permissão explícita** — mesmo para
   verificação visual rotineira.
8. **Provisionamento de recursos Azure reais** só ocorre com confirmação explícita do usuário,
   passo a passo (já feito uma vez, ver §7 — qualquer recurso *novo* segue essa regra).
9. **Antes de criar ou continuar trabalho numa branch, comparar com as outras branches/pastas
   locais existentes** (`git log --all --oneline`, `git log origin/master..origin/<branch>`) — o
   incidente do §0 (trabalho relevante espalhado em 3 lugares por dias sem ninguém perceber) não
   pode se repetir.

## 3. Stack e estrutura

```
SST-APP/
├─ src/
│  ├─ AAHBRANT.SST.Domain/          # Entidades, enums, interfaces (IEligibilityRule, IEligibilityService, IAlertaOrigemProvider)
│  ├─ AAHBRANT.SST.Application/     # CQRS (MediatR) por módulo, DTOs, validators, Motor de Alertas
│  ├─ AAHBRANT.SST.Infrastructure/  # EF Core (SstDbContext), seeders, configs de entidade, integrações Graph/Teams
│  ├─ AAHBRANT.SST.Api/             # ASP.NET Core Web API (controllers), Program.cs, autorização, middlewares
│  ├─ AAHBRANT.SST.Worker/          # Worker .NET (AlertaEngineWorker) — Motor de Alertas + Calendário Teams em background
│  ├─ AAHBRANT.SST.AgenteBiometria/ # Agente local Windows (bandeja) para o leitor Futronic FS80H
│  └─ AAHBRANT.SST.TeamsApp/        # React + Fluent UI v9, HashRouter
├─ tests/AAHBRANT.SST.Application.Tests/  # xUnit, fakes escritos à mão (sem mocking library)
├─ docs/ (ERD.md, RBAC-Matrix.md, Motor-Assinatura-Eletronica.md, referencias/, superpowers/specs/)
├─ Dockerfile.api / Dockerfile.web / Dockerfile.worker
```

**Frontend — navegação atual** (`AppShell.tsx` + `App.tsx`, consolidada em 24/08, revisada em
26/08 e 28/08): sidebar com itens de 1º nível — **Dashboard**, **Conformidade** (abas: Matriz
Legal, Gestão Documental), **Prevenção** (abas: PGR, Inspeções, DDS), **Riscos**, **Operação**
(abas: Obras, Pessoas, APR, PT, Identificação & Acesso, Ativos), **Não Conformidades**,
**Acidentes & Incidentes**, **EPI**, **Saúde Ocupacional**, **Administração**. **Alertas** não
está na sidebar — só no sino do header (badge com contagem de abertos).

**Regra para decidir aba-de-pilar vs. item próprio de sidebar**: a consolidação em abas só se
aplica a um cadastro pequeno dentro de um domínio já coeso (ex.: Setores/Equipes dentro de
Pessoas). Um módulo inteiro e distinto (EPI, Saúde Ocupacional) **sempre** ganha item próprio de
sidebar — dado operacional/cross-worker não caberia como aba de pilar.

## 4. Estado atual — módulos concluídos

Todos abaixo têm Domain+migration própria+Application(CQRS)+Api+tela React. "Verificado no
navegador" = clicado de verdade na UI (não só HTTP/Swagger), salvo nota em contrário.

| Módulo | Onde fica | Observação |
|---|---|---|
| Empresas/Obras (núcleo) | Operação → Obras | base organizacional |
| **Pessoas** (Trabalhador+ASO+Treinamento+EPI+Setor+Equipe) | Operação → Pessoas | Perfil de Vida do Trabalhador em 6 abas |
| **Riscos** + motor de elegibilidade | item próprio `/riscos` | `IEligibilityService` real, endpoint `POST /api/Elegibilidade/avaliar` |
| **PGR** | Prevenção → PGR | Inventário de riscos + Plano de ação + Revisões |
| **Identificação (NTAG/QR)** | Operação → Identificação & Acesso | Áreas + Tags, leitura pública via `/p/:codigoOuUid` |
| **APR** | Operação → APR | Etapas + Assinaturas + Aprovar/Reprovar |
| **PT (Permissão de Trabalho)** | Operação → PT | Requisitos + Controles, Autorizar/Encerrar com bloqueio real |
| **Ativos (Extintores & Equipamentos)** | Operação → Ativos | presente no código/rotas — sem registro de verificação recente, confirmar status antes de assumir "pronto" |
| **Inspeções/Checklists** | Prevenção → Inspeções | Checklists versionados (append-only) + Execuções — suporte a uso offline, ver §5 |
| **Não Conformidades** + `AcaoPlano` genérico | item próprio `/nao-conformidades` | `AcaoPlano` é polimórfico, reaproveitado por NC/Acidentes/Matriz Legal |
| **Acidentes & Incidentes** | item próprio `/acidentes` | Gravidade/HHT mensal (NBR 14280) |
| **Matriz Legal** | Conformidade → Matriz Legal | |
| **Gestão Documental** | Conformidade → Gestão Documental | ciclo de vida + `DocumentoRevisao` append-only |
| **DDS** | Prevenção → DDS | roteiro + condução + evidência fotográfica de presença — suporte a uso offline, ver §5 |
| **EPI** (reformulação completa, 3 fases) | item próprio `/epi` | Matriz de EPI por Função, Ficha reformulada (consolidada por trabalhador), Estoque segmentado por Obra |
| **Saúde Ocupacional** (PR-SST-003) | item próprio `/saude-ocupacional` | **ASO + PCMSO + Exames Complementares + Aptidões**. PCMSO reaproveita `DocumentoGestao` (`PcmsoDetalhe`, Tipo="PCMSO") em vez de ser entidade solteira — decisão de 28/08, ver §0. Já rodou em produção antes desta reconciliação; **precisa ser verificado de novo no navegador** depois que RBAC Camada 2/3 e sync offline entrarem. |
| **Administração (Usuários & RBAC)** | item próprio `/administracao` | CRUD de Usuário, Perfis&Permissões, Trilha de Auditoria (só leitura) |
| **RBAC — 3 camadas** | transversal | Camada 1 (`[Authorize(Policy="modulo:acao")]`, ~45 pontos) + Camada 2 (`EscopoPorObraMiddleware`, escopo por obra por requisição) + Camada 3 (`HasQueryFilter` em Dds/Inspecao/Acidente/Pgr/Atividade/Setor/Trabalhador/AreaSst). Todas no-op (acesso global) enquanto `AzureAd:TenantId` não estiver configurado. **Saúde Ocupacional ainda não coberta pela Camada 3** (ver §0). |
| **Sincronização offline** (piloto) | transversal a DDS/Inspeções/Checklists/APRs | `lib/offline/syncEngine.ts` + IndexedDB, fila de mutações com `Idempotency-Key`, `IdempotenciaMiddleware`/`IdempotenciaRegistro` no backend |
| **LGPD/CPF** | transversal | AES-256-GCM em nível de aplicação (não Always Encrypted), mascaramento na UI, hash para índice único |
| **Middleware global de exceção** | transversal (Api) | `TratamentoDeExcecaoMiddleware` |
| **Alertas** (Motor Central) | sino no header | `RegraAlerta` configurável por módulo, `AlertaEngineService`, 6 `IAlertaOrigemProvider` (ASO/Treinamento/Extintor/Equipamento/EPI/**Documento de Gestão** — este último cobre PCMSO de graça, já que reaproveita `DocumentoGestao`); fluxo Aberto→EmTratamento→Escalonado/Resolvido/Ignorado; notificação no Activity Feed do Teams |
| **Calendário do Teams** (integração com Alertas) | transversal (Worker) | Cria/atualiza/cancela evento no Graph para Alertas com destinatário+prazo |
| **Motor de Assinatura Eletrônica** | usado por DDS/PT/Entrega+Devolução de EPI | Biometria digital local (Futronic FS80H, ainda simulada) + FIDO2/WebAuthn + fallback crachá/PIN |
| **Deploy Azure/Teams** | infra | `sst-api-hml`/`sst-web-hml`/`sst-worker-hml` no ar, mas **desatualizado** em relação a este código — ver §0/§7 |

## 5. Motores transversais e decisões de IA/UX

- **Motor de Assinatura Eletrônica**: ver `docs/Motor-Assinatura-Eletronica.md` (atualizado
  28/08) para o histórico completo das 14 fases.
- **Sincronização offline**: piloto restrito a módulos de campo (DDS, Inspeções, Checklists,
  APRs). Leituras usam cache-then-network; mutações sem conexão entram numa fila local e
  reenviam sozinhas quando a internet volta; conflito sempre resolve a favor do servidor,
  avisando o usuário (`SyncStatusBadge`).
- **Nunca criar item de menu por tabela.** Dado de trabalhador (ASO, Treinamento, EPI) vira aba
  no perfil dele.
- **Alerta é polimórfico** (`EntidadeOrigemTipo`/`Id`) — por isso nunca virou aba de um módulo
  específico, fica só no sino do header.
- Workaround técnico de Browser pane: se não estiver exibido, cliques por coordenada podem falhar
  silenciosamente — usar `javascript_tool` disparando `pointerdown→mousedown→pointerup→mouseup→click`
  (`bubbles:true`) manualmente.
- `net::ERR_ABORTED` em POST/DELETE com status 2xx real, quando o pane não está exibido, é falso
  alarme — confirmar com um GET fresco antes de reportar como bug.
- `<input type="date">` não aceita digitação simulada com barras — usar valor ISO
  (`YYYY-MM-DD`). `<input type="time">` devolve `"HH:mm"` mas `TimeSpan?` no backend exige
  `"HH:mm:ss"` — completar com `:00`.
- Múltiplos worktrees/dev servers ao mesmo tempo colidem de porta.

## 6. Gaps/pendências conhecidos

- **Ver §0 primeiro** — as pendências da reconciliação de 28/08 (build não validado, migration
  inconsistente, RBAC Camada 3 não cobre Saúde Ocupacional, motor de alertas sem escalonamento
  automático, `REFORMULAÇÃO.md` não avaliado, deploy desatualizado) são as mais urgentes.
- **RBAC**: Matriz Perfil×Permissão (`docs/RBAC-Matrix.md`) continua vazia e não validada pela
  Diretoria/Gestor QSMS.
- **PT — fidelidade ao formulário em papel.** Spec pronta
  (`docs/superpowers/specs/2026-08-26-pt-fidelidade-documento-design.md`), implementação não
  iniciada.
- **Biometria Futronic real** — SDK ainda simulado (`SimuladoFingerprintReader`/`Matcher`).
- **Motor de assinatura não integrado a** Treinamento/Inspeções/APR.
- **Discrepância de cor de marca**: `#670000` (regra da organização) vs. `#7B1E2B` (implementado
  de fato) — não resolvida.
- **Ativos (Extintores & Equipamentos)**: sem registro de verificação recente — checar antes de
  assumir pronto.
- **`Calendars.ReadWrite` de aplicativo** (permissão Graph) dá acesso a qualquer caixa do tenant —
  sugestão de Application Access Policy no Exchange Online ainda não decidida.
- **`AlertaHistoricoEnvio`** existe só no schema, sem endpoint/UI dedicado.
- **Validação jurídica formal de LGPD** ainda pendente.
- **Senha do SA e chaves de LGPD em texto puro** em `appsettings.Development.json` (dev local
  apenas — segredos reais vivem só nos secrets do Container App).
- Cerimônia de assinatura com hardware FIDO2 físico real ainda não testada.

## 7. Deploy Azure/Teams (produção/homologação)

Infra reaproveitada do extinto projeto "Hub Gênesis" (subscription `G-SIPRO-HOMOLOGACAO`,
resource group `rg-gnezis-hub-staging`, ACR `gnezishubstaging2342917073`, Container Apps
Environment `env-gnezis-hub-staging`). Banco: Azure SQL Server próprio (`sql-sst-hml-brs.database.windows.net`,
database `AAHBRANT.SST.Hml`).

Container Apps: `sst-api-hml`, `sst-web-hml`, `sst-worker-hml` (`--min-replicas 1`).

**A tag no ar antes desta sessão (2026-08-28) era `saude-ocupacional-v1`** (commit `16af19f` da
pasta local, não desta branch) — **está desatualizada**: não tem RBAC Camada 2/3 nem sincronização
offline, que só entraram na reconciliação de hoje. Sempre conferir a tag real antes de deployar:
```bash
az containerapp show -g rg-gnezis-hub-staging -n sst-api-hml --query "properties.template.containers[0].image" -o tsv
```

**Padrão de redeploy:**
```bash
az acr build --registry gnezishubstaging2342917073 --image sst-api:v2 --file Dockerfile.api . --no-wait
az acr build --registry gnezishubstaging2342917073 --image sst-web:v2 --file Dockerfile.web --build-arg VITE_API_BASE_URL=https://sst-api-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io --build-arg CACHEBUST=<valor-unico> . --no-wait
# poll: az acr task list-runs --registry gnezishubstaging2342917073 --query "[].{runId:runId,status:status}" -o table
az containerapp update -g rg-gnezis-hub-staging -n sst-api-hml --image gnezishubstaging2342917073.azurecr.io/sst-api:v2
az containerapp update -g rg-gnezis-hub-staging -n sst-web-hml --image gnezishubstaging2342917073.azurecr.io/sst-web:v2
```
Nunca reaproveitar uma tag já publicada. `CACHEBUST` é obrigatório no build do web.

**Gotchas críticos de deploy:**
1. `export` no Bash tool não persiste entre chamadas.
2. `az acr build`/`az acr task logs` trava no Windows com caracteres Unicode — usar `--no-wait` e
   conferir status via `az acr task list-runs`.
3. Rodar `npx tsc -b --force` local antes do build remoto evita gastar build em erro de TypeScript.
4. **`sst-worker-hml` compartilha `AddInfrastructure()` com `sst-api-hml`, mas os secrets dos dois
   Container Apps NÃO sincronizam automaticamente.** Toda config obrigatória nova precisa ser
   copiada manualmente para os secrets do Worker também — comparar `env` via
   `az containerapp show ... --query "properties.template.containers[0].env"`.

**Repositório git**: `https://github.com/AAHBRANT/AHBT-SST` — **atenção**: o remote configurado em
`C:\Projetos\SST-APP` ainda aponta para `https://github.com/DesenvolvimentoAHBT/AHBT-SST.git`, o
nome antigo da mesma organização (o GitHub redireciona automaticamente, mas o ideal é atualizar o
remote local com `git remote set-url origin https://github.com/AAHBRANT/AHBT-SST.git` para evitar
confusão futura — foi exatamente essa duplicidade de nomes que escondeu o trabalho descrito no §0
por dias). `.gitignore` exclui `bin/`/`obj/`/`node_modules/`/`dist/`/`CREDENCIAIS.txt`/
`appsettings.Development.json`/`.env*`.

## 8. Como retomar na sessão nova

1. Ler este arquivo por inteiro antes de qualquer ação — **em especial o §0**.
2. Rodar `git status`/`git log --all --oneline -20` **em toda pasta/clone que você tiver acesso**
   e comparar branches (`git log origin/master..origin/<branch>`) — não presuma que só existe uma
   cópia do trabalho. Isso já causou um incidente sério (§0).
3. Rodar `dotnet build` + testes assim que houver SDK .NET disponível — não foi validado nesta
   sessão de reconciliação.
4. Antes de aplicar migrations num banco real, resolver a inconsistência descrita no §0
   (`dotnet ef migrations add` para reconciliar a tabela órfã de Pcmso antigo).
5. Verificar Saúde Ocupacional de novo no navegador — o código mudou desde a última verificação
   em produção (ganhou RBAC Camada 2/3 e sync offline por baixo).
6. Antes de propor schema para um módulo novo, buscar a seção específica na Base de Conhecimento
   e alinhar o desenho com o usuário antes de implementar.
7. Não considerar nenhum módulo "pronto" sem verificação real no navegador.
8. Antes de deployar, sempre conferir a tag de imagem real em hml (comando no §7) e comparar `env`
   da Api vs. do Worker.
