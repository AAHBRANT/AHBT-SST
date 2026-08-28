# ONBOARDING — App de SST AAHBRANT

> Handoff para iniciar uma sessão nova (outra conta/máquina) sem perder contexto.
> Gerado em 2026-08-21, **reformulado em 2026-08-28** para refletir o estado real do código (a versão anterior estava desatualizada em vários pontos — ver §9 "O que mudou nesta reformulação").

## 1. O que é este projeto

App de **Segurança e Saúde no Trabalho (SST)** da AAHBRANT, Teams-first, para construção civil.
**Escopo Fase 1: apenas o app de SST** — não é o "Hub Gênesis" administrativo completo (RH, financeiro etc. ficam de fora).

Documentos-fonte na raiz do repo — **fonte da verdade, seguir à risca**:
- `Base de Conhecimento — Projeto de Aplicação e Controle de SST.md` — 50 seções, todo o domínio de SST. **Toda entidade/enum novo precisa citar a seção/linha exata, ou ser sinalizado explicitamente como decisão não-literal minha.**
- `PROJECT RULES.md` — arquitetura fixa (ver §2).
- `DESING SYSTEM AAHBRANT.md` — tokens visuais (ver §3; **atenção à divergência de cor no §7**).
- `docs/ERD.md`, `docs/RBAC-Matrix.md` — rascunhos técnicos (RBAC ainda **não validado** pela Diretoria/QSMS; `ERD.md` está desatualizado quanto à criptografia de CPF, ver §7).
- `docs/Motor-Assinatura-Eletronica.md` — spec viva do motor de assinatura genérico (cabeçalho do doc está desatualizado, mas a seção 5 "Ordem de implementação" reflete o estado real).
- `docs/superpowers/specs/*.md` — specs de features pontuais (6 arquivos hoje), cada uma com status de implementação. Ver §5.
- `docs/referencias/` — documentos-modelo institucionais (planilhas/Word) que motivaram specs: ficha de EPI oficial, modelo original de PT em papel, lista mestra de documentos SSO, planilha de riscos×funções×NRs.
- `ANALISE DE OPORTUNIDADES - HUB GENESIS SST - 2026-08-19.md` — lacunas críticas diagnosticadas na origem do projeto (ERD formal, nota LGPD, matriz RBAC).

## 2. Instruções permanentes (não negociáveis)

1. **"Implementação real! Seguir à risca a Base de Conhecimento .md da pasta."** — nunca mockup/protótipo. Toda entidade/status/enum não citado literalmente no `.md` deve ser sinalizado ao usuário como decisão minha, não do documento.
2. **Cada módulo de SST é uma fatia vertical independente**, com sua própria migration EF Core isolada — nunca misturar entidades/migrations entre módulos.
3. **Arquitetura fixa** (`PROJECT RULES.md`): Teams-first (Personal Tab, Group/Channel Tab, Bot), Entra ID SSO (sem autenticação local por senha), Azure App Service/Functions, Microsoft Graph API.
4. **Clean Architecture** — regra de dependência: `Domain` → nada; `Application` → só `Domain`; `Infrastructure`/`Api`/`Worker` → `Application`. Implementações de `IEligibilityRule` ficam em `Application/Elegibilidade/Rules/`, **nunca** em `Infrastructure`.
5. **Barra de verificação obrigatória**: todo módulo só é considerado "pronto" depois de verificação real ponta-a-ponta no navegador (`preview_start` + clicar na UI de verdade + checar console/network) — nunca só `dotnet build` ou Swagger.
6. **Organização (AAHBRANT)**: responder sempre em português (Brasil), direto e prático; identidade visual vinho/marsala — **cor oficial ainda a confirmar com o usuário, ver divergência no §7**; nunca inventar dado — sinalizar incerteza; explicar antes de alterar arquivo e evitar sobrescrever sem confirmação; documentos institucionais em linguagem formal.

## 3. Stack e estrutura

```
AHBT-SST/
├─ src/
│  ├─ AAHBRANT.SST.Domain/          # Entidades, enums, interfaces (IEligibilityRule, IEligibilityService)
│  ├─ AAHBRANT.SST.Application/     # CQRS (MediatR) por módulo, DTOs, validators
│  ├─ AAHBRANT.SST.Infrastructure/  # EF Core (SstDbContext), seeders, configs de entidade, PDFs (QuestPDF)
│  ├─ AAHBRANT.SST.Api/             # ASP.NET Core Web API (controllers, autorização, middlewares)
│  ├─ AAHBRANT.SST.Worker/          # Azure Functions — hoje roda o AlertaEngineWorker (motor de alertas)
│  ├─ AAHBRANT.SST.AgenteBiometria/ # Agente local Windows (bandeja) para o leitor Futronic FS80H — projeto separado
│  └─ AAHBRANT.SST.TeamsApp/        # React + Fluent UI v9, HashRouter
├─ docs/ (ERD.md, RBAC-Matrix.md, Motor-Assinatura-Eletronica.md, referencias/, superpowers/specs/)
```

Padrão de tela do frontend: 1 item de menu por **domínio** (nunca por tabela), com `TabList`/abas internas para sub-cadastros — ver §6 "Decisões de IA/UX".

## 4. Estado atual — 16 módulos, todos com Domain+migration+Application(CQRS)+Api+tela React

O menu já está organizado em **4 pilares** (ver §6 — decisão que já foi executada, não é mais "projeto à parte"):

| Pilar / nível | Módulo | Rota | Observação |
|---|---|---|---|
| **Conformidade** | Matriz Legal | `/conformidade/matriz-legal` | Requisitos legais aplicáveis |
| **Conformidade** | Gestão Documental | `/conformidade/gestao-documental` | Documentos controlados do SGI |
| **Prevenção** | PGR | `/prevencao/pgr` | Inventário de riscos + Plano de ação + Revisões |
| **Prevenção** | Inspeções/Checklists | `/prevencao/inspecoes` | Checklists versionados (append-only) + Execuções |
| **Prevenção** | DDS | `/prevencao/dds` | Diálogo Diário de Segurança, com assinatura eletrônica (1º módulo integrado ao motor de assinatura) |
| **Operação** | Empresas/Obras | `/operacao/obras` | Base organizacional |
| **Operação** | Pessoas | `/operacao/pessoas` + `/pessoas/:id` | Perfil de Vida do Trabalhador em **6 abas** — ver §6 |
| **Operação** | APR | `/operacao/apr` | Etapas + Assinaturas (ainda `AprAssinatura` simples, não integrada ao motor genérico) + Aprovar/Reprovar |
| **Operação** | PT (Permissão de Trabalho) | `/operacao/pt` | Requisitos + Controles, Autorizar/Encerrar; **fidelidade ao formulário em papel ainda pendente, ver §7** |
| **Operação** | Identificação (NTAG/QR) | `/operacao/identificacao` | Áreas + Tags, resolver por UID |
| **Operação** | Ativos | `/operacao/ativos` | Ativos de SST (extintores, equipamentos etc.) |
| *(1º nível)* | Alertas | `/alertas` | Motor central de alertas (ASO, EPI, Equipamento, Extintor, Treinamento vencendo) |
| *(1º nível)* | Riscos + elegibilidade | `/riscos` | `IEligibilityService` real (`AsoValidoRule`+`TreinamentoValidoRule`+`AprValidaRule`), endpoint `POST /api/Elegibilidade/avaliar` |
| *(1º nível)* | Não Conformidades | `/nao-conformidades` | NC + Ações de plano |
| *(1º nível)* | Acidentes | `/acidentes` | Acidente/incidente/quase acidente/condição insegura/ato inseguro/**doença ocupacional** (enum, não módulo próprio) |
| *(1º nível)* | EPI | `/epi` | Catálogo + Matriz EPI×Função + Entregas + Devoluções + Estoque segmentado por Obra — módulo reformulado em 3 fases, ver §5 |
| *(1º nível)* | Administração (Usuários & RBAC) | `/administracao` | Usuários, Perfis&Permissões (matriz), Trilha de Auditoria |

**Pilar "Melhoria Contínua" foi removido** na reorganização (24–26/08): Riscos, Não Conformidades, Acidentes e Alertas viraram itens de 1º nível. Rotas antigas (`/pgr`, `/dds`, `/melhoria/...` etc.) continuam funcionando via `<Navigate replace>` — não remover esses redirects sem checar se algo externo ainda os referencia.

## 5. Reformulação do módulo EPI — concluída (3 fases)

- **Fase 1** — Matriz de EPI por Função (`MatrizEpiFuncao`), filtro no formulário de entrega.
- **Fase 2** — Ficha de EPI reformulada: ficha única cumulativa por trabalhador (não mais 1 PDF por entrega), termo de compromisso nas 5 cláusulas, campos de Obra (CNPJ + logo), fluxo de devolução com assinatura própria (`EntidadeTipo="DevolucaoEpi"`).
- **Fase 3** — Estoque segmentado por Obra (sem conceito de Almoxarifado separado).

Todas implementadas e mescladas (ver `docs/superpowers/specs/2026-08-26-matriz-epi-funcao-design.md` e `2026-08-27-ficha-epi-reformulada-design.md`).

## 6. Motores transversais (usados por vários módulos)

- **Motor de Alertas** (`Application/Alertas/Motor/AlertaEngineService`, rodando via `AlertaEngineWorker` no projeto Worker): agrega 5 provedores de origem (ASO, EPI, Equipamento, Extintor, Treinamento) e cria/atualiza/resolve `Alerta` automaticamente, notificando por Activity Feed do Teams. **Extensão recente (28/08)**: qualquer alerta agora também gera evento no **Calendário do Teams/Outlook** do destinatário (`CalendarioEventoTeams` + `GraphCalendarioTeamsService`), plugado no mesmo ponto de criação/atualização/resolução — novos módulos ganham isso de graça.
- **Motor de Assinatura Eletrônica genérico** (`Entidades/Assinatura/DocumentoAssinatura`+`DocumentoSignatario`, chave `(EntidadeTipo, EntidadeId)`): estratégias plugáveis — crachá NFC/QR+PIN (`CrachaPinAutenticacaoStrategy`, principal), WebAuthn (`Fido2AutenticacaoStrategy`, opcional), biometria física (`FutronicAutenticacaoStrategy`). UI reutilizável: `components/assinatura/AssinaturaQuiosque.tsx`. **Já integrado a**: DDS, PT, Entrega/Devolução de EPI. **Ainda não integrado a**: Treinamento (modelo de dados incompatível), Inspeções, APR (decisão de produto pendente sobre substituir `AprAssinatura`). Doc vivo: `docs/Motor-Assinatura-Eletronica.md`.
- **Biometria Futronic FS80H** (projeto `AAHBRANT.SST.AgenteBiometria`): arquitetura completa (agente local Windows com API HTTP, cache de templates, matching 1:N) — **mas hoje roda com `SimuladoFingerprintReader`/`SimuladoFingerprintMatcher`**, porque o SDK real (ScanAPI/ftrapi) depende do hardware físico ainda não disponível. Não tratar como pronto para produção até essa troca.

## 7. Perfil de Vida do Trabalhador — as 6 abas (`/operacao/pessoas/:id`)

1. **Geral & ASO**
2. **EPI & Matriz**
3. **Treinamentos & DDS**
4. **Riscos & OS**
5. **Ocorrências**
6. **Cofre de Assinaturas**

Confirma a regra do §6-antigo: dado que pertence a uma pessoa nunca vira item de menu solto, sempre aba no perfil.

## 8. Decisões de IA/UX já validadas com o usuário (não reabrir sem necessidade)

- **Nunca criar item de menu por tabela.** Confirmado explicitamente pelo usuário após reclamação de sidebar poluída.
- **Reorganização do sidebar em 4 pilares (mockup "G-SST") — já executada** (ver §4). Não é mais um "projeto à parte" pendente — qualquer ajuste futuro de IA deve partir da estrutura atual, não da lista antiga de 9 módulos "soltos".
- Workaround técnico: se o Browser pane não estiver exibido, cliques por coordenada podem falhar silenciosamente (sem erro, sem POST) — usar `javascript_tool` para disparar a sequência de eventos manualmente (`pointerdown→mousedown→pointerup→mouseup→click`, `bubbles:true`).
- Campo `<input type="date">` não aceita digitação simulada com barras — usar `form_input` com valor ISO (`YYYY-MM-DD`).

## 9. Gaps/pendências (atualizado em 2026-08-28)

**Resolvidos desde a versão anterior do onboarding:**
- ~~Sem middleware global de exceção~~ → `TratamentoDeExcecaoMiddleware` (`Api/Middlewares/`) trata `ValidationException`→400, `KeyNotFoundException`→404, `InvalidOperationException`→400, genérico→500 sem stack trace exposto.
- ~~Sem CRUD de Equipe~~ → `EquipesController` completo, com `[Authorize(Policy=...)]`.
- ~~LGPD/CPF sem medida técnica~~ → implementado, mas **não** via "Always Encrypted" (que o onboarding antigo previa) — em vez disso, criptografia em nível de aplicação: `CpfCriptografiaConversor` (AES-256-GCM via `ValueConverter`) + `Trabalhador.CpfHash` (HMAC-SHA256 determinístico, só para unicidade) + `CpfMascarador` na exibição. Migration `20260823161315_AdicionarCriptografiaCpf` já rodou com backfill. **`docs/ERD.md` ainda cita "Always Encrypted" — desatualizado, atualizar quando alguém mexer nesse doc.**

**Ainda pendentes:**
- **RBAC — só a Camada 1 está implementada.** Existem `[Authorize(Policy=...)]` reais (~45 pontos) e `PermissaoAuthorizationHandler` checando `PerfilAcesso`/`PerfilAcessoPermissao` no banco. Mas **Camada 2 (escopo por obra) e Camada 3 (Global Query Filter por perfil)** — descritas em `docs/RBAC-Matrix.md §4` — seguem deliberadamente pendentes (comentário no próprio handler: aguardando "threading do contexto de obra por requisição"). **Além disso, se `AzureAd:TenantId` estiver vazio (Entra ID não configurado), o handler autoriza tudo incondicionalmente** — RBAC é no-op nesse cenário, cuidado ao testar localmente achando que está protegido.
- **Matriz RBAC não validada pela Diretoria/Gestor QSMS** — `docs/RBAC-Matrix.md` segue como rascunho técnico.
- **PT — fidelidade ao formulário em papel.** Spec pronta (`docs/superpowers/specs/2026-08-26-pt-fidelidade-documento-design.md`), status "em revisão pelo usuário" — **implementação não iniciada**. É o item mais concreto e pronto para puxar (Tipo de Serviço estruturado, Cuidados Comuns, Precauções por categoria, 3 papéis de aprovação/encerramento em vez de 1, fotos de evidência, exportação PDF).
- **Biometria Futronic real** — SDK ainda simulado, ver §6.
- **Motor de assinatura não integrado a** Treinamento/Inspeções/APR (ver §6).
- **"Saúde Ocupacional" como módulo próprio não existe.** Hoje só há: (a) aba ASO dentro do perfil do trabalhador (atestados pontuais); (b) enum `DoençaOcupacional` dentro de Acidentes (registro de ocorrência isolada). Não há PCMSO, periodicidade de exames por risco, indicadores epidemiológicos, nem tela dedicada. **Sinalizado pelo usuário em 28/08 — escopo ainda a definir com ele antes de desenhar qualquer entidade nova** (buscar seção específica na Base de Conhecimento primeiro).
- **Divergência de cor vinho oficial**: o tema web (`TeamsApp/src/theme.ts`) usa `#7B1E2B` (igual ao `DESING SYSTEM AAHBRANT.md`), mas os 4 serviços de PDF (`DdsPdfService`, `EntregaEpiPdfService`, `DocumentoAssinaturaPdfService`, `RelatorioFiscalizacaoPdfService`) usam `#670000` (const `CorMarca`). **Confirmar com o usuário/marca qual é a cor-mestra real** antes de alterar qualquer um dos dois lados.
- **Senha do SA em texto puro** — não foi possível confirmar nem descartar nesta reformulação (nenhum `appsettings.Development.json` versionado foi encontrado; pode existir localmente fora do controle de versão). Confirmar manualmente no ambiente de desenvolvimento.
- Provisionamento de recursos Azure reais (App Registration, Azure SQL, App Service/Functions) só ocorre com confirmação explícita do usuário, passo a passo.
- `docs/Motor-Assinatura-Eletronica.md` tem o **cabeçalho desatualizado** ("Fase 1 e 2 concluídas, demais não iniciadas") — a seção 5 do próprio doc (mais abaixo) é que reflete o estado real.

## 10. Próximo passo sugerido

Ordem sugerida, mas **sempre alinhar com o usuário antes de implementar**:
1. **PT — fidelidade ao formulário em papel** (spec já pronta, só falta aprovação final + implementação).
2. **RBAC Camada 2/3** (escopo por obra + query filter), quando o contexto de obra por requisição estiver desenhado.
3. **Saúde Ocupacional** — definir escopo com o usuário (PCMSO? periodicidade de exames? indicadores?) antes de qualquer entidade nova, citando a seção exata da Base de Conhecimento.
4. **Biometria Futronic real** (troca dos simuladores pelo SDK, quando o hardware estiver disponível para teste).
5. Integrar motor de assinatura a Treinamento/Inspeções/APR (depende de decisão de produto sobre `AprAssinatura`).

## 11. Como retomar na sessão nova

1. Ler este arquivo por inteiro antes de qualquer ação.
2. Ler a Base de Conhecimento na seção relevante ao próximo módulo antes de propor schema.
3. Seguir o padrão de camadas já estabelecido (replicar a estrutura de módulos já prontos, ex. `Asos`/`Riscos`/`PermissoesTrabalho`, para o módulo novo).
4. Não considerar nenhum módulo "pronto" sem a verificação real no navegador (não pular esta etapa mesmo sob pressão de tempo).
5. Tratar este documento como o estado mais recente conhecido, mas **sempre confirmar contra o código atual** antes de agir (arquivos podem ter mudado desde a geração deste handoff) — em especial os pontos marcados como "a confirmar" no §9 (cor oficial, senha SA, escopo de Saúde Ocupacional).
