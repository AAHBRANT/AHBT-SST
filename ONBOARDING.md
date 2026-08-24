# ONBOARDING — App de SST AAHBRANT

> Handoff para iniciar uma sessão nova (outra conta/máquina) sem perder contexto.
> Gerado em 2026-08-21 a partir da memória acumulada da sessão anterior.

## 1. O que é este projeto

App de **Segurança e Saúde no Trabalho (SST)** da AAHBRANT, Teams-first, para construção civil.
**Escopo Fase 1: apenas o app de SST** — não é o "Hub Gênesis" administrativo completo (RH, financeiro etc. ficam de fora).

Documentos-fonte na raiz do repo (`C:\Projetos\SST-APP`) — **fonte da verdade, seguir à risca**:
- `Base de Conhecimento — Projeto de Aplicação e Controle de SST.md` — 50 seções, todo o domínio de SST. **Toda entidade/enum novo precisa citar a seção/linha exata, ou ser sinalizado explicitamente como decisão não-literal minha.**
- `PROJECT RULES.md` — arquitetura fixa (ver §2).
- `DESING SYSTEM AAHBRANT.md` — tokens visuais (ver §3).
- `docs/ERD.md`, `docs/RBAC-Matrix.md` — rascunhos técnicos já existentes (RBAC ainda **não validado** pela Diretoria/QSMS).
- `ANALISE DE OPORTUNIDADES - HUB GENESIS SST - 2026-08-19.md` — lacunas críticas já diagnosticadas (ERD formal, nota LGPD, matriz RBAC).

## 2. Instruções permanentes (não negociáveis)

1. **"Implementação real! Seguir à risca a Base de Conhecimento .md da pasta."** — nunca mockup/protótipo. Toda entidade/status/enum não citado literalmente no `.md` deve ser sinalizado ao usuário como decisão minha, não do documento.
2. **Cada módulo de SST é uma fatia vertical independente**, com sua própria migration EF Core isolada — nunca misturar entidades/migrations entre módulos.
3. **Arquitetura fixa** (`PROJECT RULES.md`): Teams-first (Personal Tab, Group/Channel Tab, Bot), Entra ID SSO (sem autenticação local por senha), Azure App Service/Functions, Microsoft Graph API.
4. **Clean Architecture** — regra de dependência: `Domain` → nada; `Application` → só `Domain`; `Infrastructure`/`Api`/`Worker` → `Application`. Implementações de `IEligibilityRule` ficam em `Application/Elegibilidade/Rules/`, **nunca** em `Infrastructure`.
5. **Barra de verificação obrigatória**: todo módulo só é considerado "pronto" depois de verificação real ponta-a-ponta no navegador (`preview_start` + clicar na UI de verdade + checar console/network) — nunca só `dotnet build` ou Swagger.
6. **Organização (AAHBRANT)**: responder sempre em português (Brasil), direto e prático; identidade visual vinho/marsala `#670000` (nota: `DESING SYSTEM AAHBRANT.md` usa `#7B1E2B` — os dois tons de vinho circulam no projeto, confirmar qual é o oficial se for gerar material de marca fora do app); nunca inventar dado — sinalizar incerteza; explicar antes de alterar arquivo e evitar sobrescrever sem confirmação; documentos institucionais em linguagem formal.

## 3. Stack e estrutura

```
SST-APP/
├─ src/
│  ├─ AAHBRANT.SST.Domain/          # Entidades, enums, interfaces (IEligibilityRule, IEligibilityService)
│  ├─ AAHBRANT.SST.Application/     # CQRS (MediatR) por módulo, DTOs, validators
│  ├─ AAHBRANT.SST.Infrastructure/  # EF Core (SstDbContext), seeders, configs de entidade
│  ├─ AAHBRANT.SST.Api/             # ASP.NET Core Web API (controllers)
│  ├─ AAHBRANT.SST.Worker/          # Azure Functions (ainda não implementado)
│  └─ AAHBRANT.SST.TeamsApp/        # React + Fluent UI v9, HashRouter
├─ docs/ (ERD.md, RBAC-Matrix.md)
```

Padrão de tela do frontend: 1 item de menu por **domínio** (nunca por tabela), com `TabList`/abas internas para sub-cadastros — ver §5 "IA consolidada".

## 4. Estado atual — módulos concluídos e verificados ponta-a-ponta

Todos abaixo têm Domain+migration própria+Application(CQRS)+Api+tela React, e foram **realmente clicados no navegador** (não só testados via HTTP):

| Módulo | Rota/menu | Observação |
|---|---|---|
| Empresas/Obras (núcleo) | `/obras` | base organizacional |
| **Pessoas** (Trabalhador+ASO+Treinamento+EPI) | `/pessoas` + `/pessoas/:id` | 1 item de menu, dados de trabalhador viram abas no perfil dele — ver regra de IA no §5 |
| **Riscos** + motor de elegibilidade | `/riscos` | `IEligibilityService` real (`AsoValidoRule`+`TreinamentoValidoRule`), endpoint de produção `POST /api/Elegibilidade/avaliar` |
| **PGR** | `/pgr` | Inventário de riscos + Plano de ação + Revisões |
| **Identificação (NTAG/QR)** | `/identificacao` | Áreas + Tags, resolver por UID |
| **APR** | `/apr` | Etapas + Assinaturas + Aprovar/Reprovar; `AprValidaRule` integrada à elegibilidade |
| **Administração (Usuários & RBAC)** | `/administracao` | Usuários, Perfis&Permissões (matriz), Trilha de Auditoria (só leitura, ninguém grava ainda) |
| **PT (Permissão de Trabalho)** | `/pt` | Requisitos + Controles, Autorizar/Encerrar com bloqueio real |
| **Inspeções/Checklists** | `/inspecoes` | Checklists versionados (append-only) + Execuções, bloqueio de encerramento com item pendente |

Detalhes completos, decisões não-literais e evidências de teste de cada módulo: ver o arquivo de memória `project_sst_fase_b_progress.md` (se a sessão nova tiver acesso à mesma memória) ou a seção 7 abaixo (resumo copiado).

## 5. Decisões de IA/UX já validadas com o usuário (não reabrir sem necessidade)

- **Nunca criar item de menu por tabela.** Dado que pertence a uma pessoa (ASO, Treinamento, EPI, futuros Alertas) vira **aba dentro do perfil do trabalhador** (`/pessoas/:id`), não lista solta com dropdown. Confirmado explicitamente pelo usuário após reclamação de sidebar poluída.
- **Mockup "G-SST" aprovado** (4 pilares: Conformidade/Prevenção/Operação/Melhoria Contínua, 22 módulos do MVP + DDS) como referência final de IA/visual — mas a reorganização completa do sidebar em 4 pilares é um **projeto de frontend à parte**, só faz sentido depois que todos os módulos existirem. Não mexer no menu inteiro agora.
- Workaround técnico: se o Browser pane não estiver exibido, cliques por coordenada podem falhar silenciosamente (sem erro, sem POST) — usar `javascript_tool` para disparar a sequência de eventos manualmente (`pointerdown→mousedown→pointerup→mouseup→click`, `bubbles:true`).
- Campo `<input type="date">` não aceita digitação simulada com barras — usar `form_input` com valor ISO (`YYYY-MM-DD`).

## 6. Gaps/pendências conhecidos (rastreados, não bloqueantes para continuar)

- **Sem middleware global de tratamento de exceção na Api** — `InvalidOperationException` de regra de negócio (bloqueios preventivos em PT/PGR/Inspeções/etc.) vira HTTP 500 com stack trace bruto no frontend em vez de 400 limpo. Sistêmico, confirmado em vários módulos. Resolver de uma vez, não módulo a módulo — ideal quando entrar o enforcement real de RBAC (Fase C).
- **Sem CRUD de `Equipe`** — campo existe no schema/DTOs mas não tem tela nem seletor em lugar nenhum.
- **Enforcement real de RBAC pendente** — `[Authorize(Policy=...)]`, handler de escopo por obra, EF Core Global Query Filter por perfil — depende do Entra ID SSO estar provisionado (Fase C).
- **LGPD**: medidas técnicas (Always Encrypted em CPF, mascaramento na UI) ainda não aplicadas — não colocar dado real de CPF/ASO em produção antes disso. Validação jurídica formal ainda pendente.
- **RBAC** (`docs/RBAC-Matrix.md`) é rascunho técnico, não validado pela Diretoria/Gestor QSMS.
- Senha do SA em texto puro em `appsettings.Development.json` (ambiente dev local).
- Provisionamento de recursos Azure reais (App Registration, Azure SQL, App Service/Functions) só ocorre com confirmação explícita do usuário, passo a passo.

## 7. Próximo módulo na fila

Ordem sugerida (cada um como fatia vertical própria, reaproveitando `IEligibilityService`/`Evidencia`):
**NC/Plano de Ação (genérico)** → Acidentes → Matriz Legal → Gestão Documental → DDS.

Antes de desenhar a entidade de cada um: buscar a seção específica na Base de Conhecimento, seguir o padrão de brainstorming (alinhar desenho com o usuário antes de implementar) e o mesmo padrão de vertical slice já usado nos módulos acima.

## 8. Como retomar na sessão nova

1. Ler este arquivo por inteiro antes de qualquer ação.
2. Ler a Base de Conhecimento na seção relevante ao próximo módulo antes de propor schema.
3. Seguir o padrão de camadas já estabelecido (replicar a estrutura de `Asos`/`Riscos`/`PermissoesTrabalho` para o módulo novo).
4. Não considerar nenhum módulo "pronto" sem a verificação real no navegador (não pular esta etapa mesmo sob pressão de tempo).
5. Se o usuário pedir para checar/confirmar algo já registrado aqui, tratar este documento como o estado mais recente conhecido — mas **sempre confirmar contra o código atual** antes de agir (arquivos podem ter mudado desde a geração deste handoff).
