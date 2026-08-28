# ONBOARDING — App de SST AAHBRANT

> Handoff para iniciar uma sessão nova (outra conta/máquina) sem perder contexto.
> Gerado em 2026-08-21, **reescrito por completo em 2026-08-28** a partir da memória
> persistente acumulada (17 arquivos de memória lidos por completo para esta reescrita).
> A versão anterior estava parada em "Inspeções/Checklists" — desde então **mais de 10
> entregas grandes** aconteceram (ver §4). Trate este arquivo como o estado mais recente
> conhecido, mas **sempre confirme contra o código atual** antes de agir — arquivos podem
> ter mudado desde a geração deste handoff.

## 1. O que é este projeto

App de **Segurança e Saúde no Trabalho (SST)** da AAHBRANT, Teams-first, para construção civil.
**Escopo Fase 1: apenas o app de SST** — não é o "Hub Gênesis" administrativo completo (RH, financeiro etc. ficam de fora).

**Já está no ar em homologação** (Microsoft Teams, via Azure Container Apps) — não é mais só ambiente de desenvolvimento local. Ver §7.

Documentos-fonte na raiz do repo (`C:\Projetos\SST-APP`) — **fonte da verdade, seguir à risca**:
- `Base de Conhecimento — Projeto de Aplicação e Controle de SST.md` — 50 seções, todo o domínio de SST. **Toda entidade/enum novo precisa citar a seção/linha exata, ou ser sinalizado explicitamente como decisão não-literal minha.**
- `PROJECT RULES.md` — arquitetura fixa (ver §2).
- `DESING SYSTEM AAHBRANT.md` — tokens visuais (ver §2, item 6 — há conflito de cor não resolvido).
- `docs/ERD.md`, `docs/RBAC-Matrix.md` — rascunhos técnicos (RBAC ainda **não validado** pela Diretoria/QSMS; matriz Perfil×Permissão continua deliberadamente vazia).
- `docs/Motor-Assinatura-Eletronica.md` — spec do Motor de Assinatura Eletrônica (biometria/FIDO2/crachá/PIN).
- `docs/referencias/` — planilhas/modelos reais de SSO fornecidos pelo usuário (Lista Mestra de Documentos, modelo de PT, ficha de entrega de EPI, planilha de riscos por função) — usar como referência de campo/layout ao desenhar telas correspondentes.
- `docs/superpowers/specs/*.md` — specs de brainstorming já aprovadas (ex.: `2026-08-28-calendario-teams-design.md`).
- `ANALISE DE OPORTUNIDADES - HUB GENESIS SST - 2026-08-19.md` — lacunas críticas já diagnosticadas na origem do projeto (a maioria já resolvida, ver §4/§6).
- **`REFORMULAÇÃO.md`** (raiz, **ainda não commitado no git** — apareceu como arquivo não rastreado nesta sessão) — descreve uma estrutura de módulos "PR-SST-001, PR-SST-002..." (EPI, Treinamentos, Saúde Ocupacional etc.) que parece uma reformulação/expansão de escopo. **Não foi processado nem validado ainda** — antes de tratá-lo como fonte da verdade, confirmar com o usuário a intenção e o status desse documento.

## 2. Instruções permanentes (não negociáveis)

1. **"Implementação real! Seguir à risca a Base de Conhecimento .md da pasta."** — nunca mockup/protótipo. Toda entidade/status/enum não citado literalmente no `.md` deve ser sinalizado ao usuário como decisão minha, não do documento.
2. **Cada módulo de SST é uma fatia vertical independente**, com sua própria migration EF Core isolada — nunca misturar entidades/migrations entre módulos.
3. **Arquitetura fixa** (`PROJECT RULES.md`): Teams-first (Personal Tab, Group/Channel Tab, Bot), Entra ID SSO (sem autenticação local por senha), Azure Container Apps (não mais App Service — ver §7), Microsoft Graph API.
4. **Clean Architecture** — regra de dependência: `Domain` → nada; `Application` → só `Domain`; `Infrastructure`/`Api`/`Worker` → `Application`. Implementações de `IEligibilityRule` ficam em `Application/Elegibilidade/Rules/`, **nunca** em `Infrastructure`.
5. **Barra de verificação obrigatória**: todo módulo só é considerado "pronto" depois de verificação real ponta-a-ponta no navegador (`preview_start` + clicar na UI de verdade + checar console/network) — nunca só `dotnet build` ou Swagger. (Exceção documentada: a reformulação de EPI de 27/08 e a foto de DDS de 24/08 foram verificadas só por log/teste automatizado, não manualmente no navegador — ver §6.)
6. **Organização (AAHBRANT)**: responder sempre em português (Brasil), direto e prático; identidade visual vinho/marsala `#670000` — **conflito ainda aberto**: `DESING SYSTEM AAHBRANT.md` documenta `#7B1E2B` e é esse valor que está de fato implementado em `theme.ts`/`manifest.json`/`DdsPdfService.cs` (descoberto em 24/08, nunca resolvido). Não alterar cor sem o usuário decidir qual é a oficial. Nunca inventar dado — sinalizar incerteza; explicar antes de alterar arquivo e evitar sobrescrever sem confirmação; documentos institucionais em linguagem formal.
7. **Não resetar/reseedar o banco de dev local sem pedir permissão explicita** — mesmo para verificação visual rotineira.
8. **Provisionamento de recursos Azure reais** só ocorre com confirmação explícita do usuário, passo a passo (já foi feito uma vez — ver §7 — mas qualquer recurso *novo* segue essa regra).

## 3. Stack e estrutura

```
SST-APP/
├─ src/
│  ├─ AAHBRANT.SST.Domain/          # Entidades, enums, interfaces (IEligibilityRule, IEligibilityService, IAlertaOrigemProvider)
│  ├─ AAHBRANT.SST.Application/     # CQRS (MediatR) por módulo, DTOs, validators, Motor de Alertas
│  ├─ AAHBRANT.SST.Infrastructure/  # EF Core (SstDbContext), seeders, configs de entidade, integrações Graph/Teams
│  ├─ AAHBRANT.SST.Api/             # ASP.NET Core Web API (controllers), Program.cs, autorização
│  ├─ AAHBRANT.SST.Worker/          # Worker .NET (AlertaEngineWorker) — processa fila de Alertas + Calendário Teams em background. Compartilha AddInfrastructure() com a Api (gotcha de secret sync, ver §7).
│  └─ AAHBRANT.SST.TeamsApp/        # React + Fluent UI v9, HashRouter
├─ tests/AAHBRANT.SST.Application.Tests/  # xUnit, fakes escritos à mão (sem mocking library)
├─ docs/ (ERD.md, RBAC-Matrix.md, Motor-Assinatura-Eletronica.md, referencias/, superpowers/specs/)
├─ Dockerfile.api / Dockerfile.web / Dockerfile.worker
```

**Frontend — navegação atual** (`AppShell.tsx` + `App.tsx`, consolidada em 24/08 e revisada em 26/08 — não é mais um sidebar plano de 12+ itens nem os "4 pilares" originais do mockup G-SST):

- Itens de 1º nível na sidebar (cada um sua própria rota/tela): **Dashboard**, **Conformidade**, **Prevenção**, **Riscos**, **Operação**, **Não Conformidades**, **Acidentes & Incidentes**, **EPI**, **Saúde Ocupacional**, **Administração**.
- **Alertas** não está na sidebar — é acessado só pelo sino no header (badge com contagem de abertos).
- **Conformidade** (`PillarLayout`, abas internas): Matriz Legal, Gestão Documental.
- **Prevenção** (`PillarLayout`, abas internas): PGR, Inspeções, DDS.
- **Operação** (`PillarLayout`, abas internas): Obras, Pessoas, APR, PT, Identificação & Acesso, Ativos (Extintores & Equipamentos).
- Riscos, Não Conformidades e Acidentes & Incidentes **saíram** dos pilares Prevenção/Melhoria Contínua e viraram itens de 1º nível (26/08) — cada tela já é autossuficiente (título + abas próprias). O módulo "Melhoria Contínua" foi **removido**; há redirects (`Navigate`) preservando links antigos.
- **Regra para decidir aba-de-pilar vs. item próprio de sidebar** (correção de 27/08, importante não esquecer): a consolidação em abas só se aplica a um cadastro pequeno dentro de um domínio já coeso (ex.: Setores/Equipes dentro de Pessoas). Um módulo PR-SST inteiro e distinto (ex.: EPI) **sempre** ganha item próprio de sidebar, nunca é rebaixado a aba — por isso EPI ficou fora dos módulos-pilar mesmo depois da consolidação.
- Ver [[project_sst_ia_consolidada_por_pessoa]]: dado que pertence a um trabalhador (ASO, Treinamento, EPI) vira **aba dentro do perfil dele** (`/operacao/pessoas/:id`), nunca item de menu solto.

## 4. Estado atual — módulos concluídos e verificados

Todos abaixo têm Domain+migration própria+Application(CQRS)+Api+tela React. "Verificado no navegador" = clicado de verdade na UI (não só HTTP/Swagger), salvo nota em contrário.

| Módulo | Onde fica | Observação |
|---|---|---|
| Empresas/Obras (núcleo) | Operação → Obras | base organizacional |
| **Pessoas** (Trabalhador+ASO+Treinamento+EPI+Setor+Equipe) | Operação → Pessoas | dados do trabalhador viram abas no perfil; Setores/Equipes viraram abas auxiliares dentro de Pessoas (21/08) |
| **Riscos** + motor de elegibilidade | item próprio `/riscos` | `IEligibilityService` real (`AsoValidoRule`+`TreinamentoValidoRule`), endpoint `POST /api/Elegibilidade/avaliar` |
| **PGR** | Prevenção → PGR | Inventário de riscos + Plano de ação + Revisões |
| **Identificação (NTAG/QR)** | Operação → Identificação & Acesso | Áreas + Tags, resolver por UID, leitura pública via `/p/:codigoOuUid` |
| **APR** | Operação → APR | Etapas + Assinaturas + Aprovar/Reprovar; seletor de Equipe (24/08); `AprValidaRule` integrada à elegibilidade |
| **PT (Permissão de Trabalho)** | Operação → PT | Requisitos + Controles, Autorizar/Encerrar com bloqueio real; seletor de Equipe (24/08); bug de `Hora`/`TimeSpan` corrigido |
| **Ativos (Extintores & Equipamentos)** | Operação → Ativos | **presente no código/rotas, mas sem registro de verificação nesta memória** — confirmar status real com o usuário ou testando antes de assumir "pronto" |
| **Inspeções/Checklists** | Prevenção → Inspeções | Checklists versionados (append-only) + Execuções, bloqueio de encerramento com item pendente |
| **Não Conformidades** + `AcaoPlano` genérico | item próprio `/nao-conformidades` | `AcaoPlano` é entidade polimórfica reaproveitada por NC/Acidentes/Matriz Legal |
| **Acidentes & Incidentes** | item próprio `/acidentes` | investigação (metodologias literais do §27/§28), bloqueio de conclusão com `AcaoPlano` pendente |
| **Matriz Legal** | Conformidade → Matriz Legal | reclassificação binária de status, sem workflow linear |
| **Gestão Documental** | Conformidade → Gestão Documental | ciclo de vida + `DocumentoRevisao` append-only |
| **DDS** (Fase 1) | Prevenção → DDS | roteiro automático + condução + evidência fotográfica obrigatória de presença (commit `b4bf2a7`, 24/08 — **migration ainda não aplicada/verificada em navegador**, ver §6). Fases 2/3 deliberadamente fora (dependem de integrações pagas). |
| **EPI** (reformulação completa, 3 fases) | item próprio `/epi` | Matriz de EPI por Função, Ficha reformulada, Estoque segmentado; mock com 74 funções reais (27/08); **verificado só via log de container em hml, não manualmente via Teams/SSO** |
| **Saúde Ocupacional** (ASO+PCMSO+Exames Complementares+Aptidões) | item próprio `/saude-ocupacional` | PCMSO reaproveita Gestão Documental (`DocumentoGestao`); ações corretivas reaproveitam `AcaoPlano`; verificado no navegador em hml (28/08) — ação Validar sem usuários cadastrados e mismatch de chave CPF ainda bloqueiam parte do fluxo de dados, ver §6 |
| **Administração (Usuários & RBAC)** | item próprio `/administracao` | CRUD de Usuário, Perfis&Permissões (catálogo ~74 códigos), Trilha de Auditoria (só leitura) |
| **Enforcement de autorização** (estrutura) | `[Authorize(Policy="modulo:acao")]` em 38/39 controllers | Camada 1 (checagem por perfil) pronta; bypassa sozinho se `AzureAd:TenantId` vazio — **conferir se está realmente ligado em hml agora que o Entra ID está provisionado** (ver §6) |
| **LGPD/CPF** | transversal | AES-256-GCM em nível de aplicação (não Always Encrypted), mascaramento na UI, hash para índice único, backfill idempotente, validação de dígito verificador (24/08) |
| **Middleware global de exceção** | transversal (Api) | `TratamentoDeExcecaoMiddleware` — `ValidationException`→400, `KeyNotFoundException`→404, `InvalidOperationException`→400, resto→500 genérico |
| **Alertas** (Motor Central) | sino no header | `RegraAlerta` configurável, `AlertaEngineService`, fluxo Aberto→EmTratamento→Escalonado/Resolvido/Ignorado; notificação no Activity Feed do Teams (Graph, não bot de chat) |
| **Calendário do Teams** (integração com Alertas) | transversal (Worker) | Cria/atualiza/cancela evento de dia inteiro no Graph para Alertas com destinatário+prazo; 16 testes; deploy em hml 28/08 |
| **Motor de Assinatura Eletrônica** (14 fases) | usado por APR/PT/DDS/EPI | Biometria digital local (Futronic FS80H) + FIDO2/WebAuthn + fallback crachá/PIN; testado ponta a ponta exceto cerimônia com hardware físico real |
| **Dados mock de obra** | seeder | ~200 trabalhadores, ASOs, treinamentos etc. para "Edifício Aurora Corporate" (mesclado 26/08) |
| **Deploy Azure/Teams** | infra | `sst-api-hml`/`sst-web-hml`/`sst-worker-hml` no ar (ver §7) |

## 5. Decisões de IA/UX já validadas (não reabrir sem necessidade)

- **Nunca criar item de menu por tabela.** Dado de trabalhador (ASO, Treinamento, EPI) vira aba no perfil dele.
- **Cadastro auxiliar pequeno de um domínio coeso → aba** (Setor/Equipe em Pessoas); **módulo PR-SST inteiro e distinto → sempre item próprio de sidebar** (EPI), nunca aba — mesmo após a consolidação em pilares.
- **Alerta é polimórfico** (pode vir de qualquer entidade via `EntidadeOrigemTipo`/`Id`) — por isso nunca virou aba de um módulo específico, ficou só no sino do header.
- Mockup "G-SST" (4 pilares Conformidade/Prevenção/Operação/Melhoria Contínua) foi o ponto de partida da reorganização de 24/08, mas **já sofreu revisão real em 26/08** (Riscos/NC/Acidentes saíram dos pilares; Melhoria Contínua foi removida) — a estrutura atual do código (§3) é a que vale, não o mockup original.
- Workaround técnico de Browser pane: se o pane não estiver exibido, cliques por coordenada podem falhar silenciosamente — usar `javascript_tool` disparando a sequência `pointerdown→mousedown→pointerup→mouseup→click` (`bubbles:true`) manualmente no DOM.
- `net::ERR_ABORTED` em POST/DELETE com status 2xx real, quando o pane não está exibido, é **falso alarme** da ferramenta — confirmar com um GET fresco antes de reportar como bug.
- `<input type="date">` não aceita digitação simulada com barras — usar `form_input` com valor ISO (`YYYY-MM-DD`). `<input type="time">` devolve `"HH:mm"` mas `TimeSpan?` no backend exige `"HH:mm:ss"` — sempre completar com `:00` ao montar o payload.
- Verificação de PDF gerado: sem Poppler nem acesso do pdf-viewer ao filesystem local, extrair texto via Python + `pypdf`.
- Múltiplos worktrees rodando dev server ao mesmo tempo colidem de porta — `dotnet run` ignora `ASPNETCORE_URLS` do ambiente, e o Vite não recarrega `.env.local` a quente.

## 6. Gaps/pendências conhecidos

- **Enforcement real de RBAC**: estrutura pronta (Camada 1, checagem por perfil) mas **verificar se está de fato ativa em hml** — ela só liga quando `AzureAd:TenantId` está configurado, e o Entra ID já foi provisionado para o deploy (ver §7). Camada 2 (escopo por obra) e Camada 3 (Global Query Filter) não existem. Matriz Perfil×Permissão (`docs/RBAC-Matrix.md`) continua vazia — preenchimento é trabalho humano da Diretoria/Gestor QSMS via `DefinirPermissoesPerfilCommand`.
- **Discrepância de cor de marca**: `#670000` (regra da organização) vs. `#7B1E2B` (implementado de fato em `theme.ts`/`manifest.json`/`DdsPdfService.cs`) — não resolvida.
- **DDS — evidência fotográfica**: código completo (commit `b4bf2a7`, 24/08) mas a migration `AdicionarFotoParticipanteDds` **nunca foi confirmada aplicada a um banco real acessível**, nem o fluxo testado no navegador — verificar antes de considerar essa parte pronta.
- **EPI (reformulação)**: deploy em hml feito (27/08) mas **só verificado via log de container**, nunca manualmente pela UI real (Teams/SSO) em hml.
- **Saúde Ocupacional — 2 gaps abertos em hml (28/08)**: (1) ação "Validar" ASO/PCMSO não testável fim a fim porque não há usuários cadastrados em `/api/usuarios` neste ambiente; (2) `GET /api/trabalhadores` bloqueado por mismatch de chave de criptografia de CPF **em hml** (mesma causa-raiz do bullet de CPF abaixo, mas aqui é o ambiente hml, não dev local) — impede parte do fluxo de cadastro de dados de ASO/Exames/Aptidões ligado a um trabalhador real.
- **Ativos (Extintores & Equipamentos)**: existe no código/rotas mas sem nenhum registro de verificação na memória — status real desconhecido, checar antes de assumir pronto.
- **`Calendars.ReadWrite` de aplicativo** (permissão Graph do App Registration) dá acesso a qualquer caixa de correio do tenant, não só usuários do SST-APP — sugestão de criar uma Application Access Policy no Exchange Online para restringir escopo foi feita ao usuário, ainda não decidida.
- **`AlertaHistoricoEnvio`** (histórico de envio/notificação de Alerta) existe só no schema, sem endpoint/UI. Botão de "Escalonar" alerta não existe na UI (só via API direta).
- **Validação jurídica formal de LGPD** ainda pendente (criptografia/mascaramento técnico já feitos, mas isso não substitui parecer jurídico).
- **Senha do SA e chaves de LGPD em texto puro** em `appsettings.Development.json` (ambiente dev local apenas — segredos de produção/hml vivem só nos secrets do Container App, nunca em arquivo).
- **`RowVersion`** (concorrência otimista do Motor de Assinatura) pode divergir entre o schema de dev local e o de hml — não afeta hml/Azure SQL, só cuidado ao comparar migrations entre ambientes.
- **CPF com chave de criptografia divergente** em ambiente de dev local (`AuthenticationTagMismatchException`) é problema de configuração/dado local, **não bug de código** — não "corrigir" silenciosamente, é dado LGPD-sensível.
- **`REFORMULAÇÃO.md`** (raiz, não commitado) — status/intenção não esclarecidos, revisar com o usuário antes de tratar como novo escopo.
- Cerimônia de assinatura com hardware FIDO2 físico real ainda não testada (todo o resto do Motor de Assinatura foi).

## 7. Deploy Azure/Teams (produção/homologação)

Infra reaproveitada do extinto projeto "Hub Gênesis" (subscription `G-SIPRO-HOMOLOGACAO`, resource group `rg-gnezis-hub-staging`, ACR `gnezishubstaging2342917073`, Container Apps Environment `env-gnezis-hub-staging`). Banco: Azure SQL Server próprio e novo (`sql-sst-hml-brs.database.windows.net`, database `AAHBRANT.SST.Hml`), não reaproveitou o Postgres do Hub Gênesis.

Container Apps: `sst-api-hml`, `sst-web-hml`, `sst-worker-hml` (este último processa o Motor de Alertas + Calendário Teams em background). Todos `--min-replicas 1`.

**Tag atualmente no ar (2026-08-28):** `sst-api:saude-ocupacional-v1` / `sst-web:saude-ocupacional-v1` / `sst-worker:saude-ocupacional-v1` (commit `16af19f`, inclui o módulo Saúde Ocupacional — ASO/PCMSO/Exames Complementares/Aptidões — e o fix de migration RowVersion Drop+Add). **A tag fica desatualizada rápido**; sempre conferir antes de deployar:
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
1. `export` no Bash tool não persiste entre chamadas — gerar+usar segredo num único comando, ou salvar em arquivo fora do repo.
2. `az acr build`/`az acr task logs` trava no Windows com caracteres Unicode (ex.: "✓" do Vite) — usar `--no-wait` e conferir status via `az acr task list-runs`, nunca ler o log completo se tiver saída do Vite.
3. Rodar `npx tsc -b --force` local antes do build remoto evita gastar ~1min de build em erro de TypeScript.
4. **`sst-worker-hml` compartilha `AddInfrastructure()` com `sst-api-hml`, mas os secrets dos dois Container Apps NÃO sincronizam automaticamente.** Toda config obrigatória nova (chave LGPD, Graph, etc.) precisa ser copiada manualmente para os secrets do Worker também, senão ele crash-loopa silenciosamente sem afetar a Api. Já aconteceu uma vez (`lgpdbiometria`). Antes de considerar um deploy do Worker concluído: comparar `env` da Api vs. do Worker via `az containerapp show ... --query "properties.template.containers[0].env"`.

Repositório git: `https://github.com/DesenvolvimentoAHBT/AHBT-SST.git`, branch `master`. `.gitignore` exclui `bin/`/`obj/`/`node_modules/`/`dist/`/`CREDENCIAIS.txt`/`appsettings.Development.json`/`.env*` — segredos reais nunca vão para o histórico do git.

## 8. Como retomar na sessão nova

1. Ler este arquivo por inteiro antes de qualquer ação.
2. Rodar `git status`/`git log --oneline -10` e comparar com §4/§7 — este documento pode já estar defasado.
3. Se a sessão nova tiver acesso à mesma memória persistente (`C:\Users\...\memory\`), ler `MEMORY.md` e os arquivos linkados — eles têm mais detalhe (decisões não-literais, evidências exatas de teste) do que este resumo.
4. Antes de propor schema para um módulo novo, buscar a seção específica na Base de Conhecimento e seguir o padrão de brainstorming (alinhar desenho com o usuário antes de implementar).
5. Seguir o padrão de camadas já estabelecido (replicar a estrutura de módulos existentes — Domain+migration própria+Application CQRS+Api+tela React).
6. Não considerar nenhum módulo "pronto" sem verificação real no navegador — e, para os itens marcados como "verificado só via log"/"não verificado" no §6, tratar como pendência, não como concluído.
7. Antes de deployar, sempre conferir a tag de imagem real em hml (comando no §7) e comparar `env` da Api vs. do Worker.
8. Se o usuário pedir para checar/confirmar algo já registrado aqui, tratar este documento como o estado mais recente conhecido — mas **sempre confirmar contra o código atual** antes de agir.
