# Auditoria do SST por módulo — 06/10/2026 (v1)

## 1. Método e limites (leia antes)

- **Executado de verdade (frontend):** `npm ci`, `tsc -b` (limpo), `vite build` (ok), `oxlint` (5 erros, 93 avisos), `npm audit --omit=dev` (0 vulnerabilidades), testes de funções puras `cpf.ts`, `datas.ts`, `valoresPadrao.ts` em 3 fusos.
- **NÃO executado:** testes do backend (.NET). O SDK não está instalado e o download é bloqueado pela política de rede da organização. O CI cita 447 testes; isso não foi conferido aqui.
- **Todos os achados de backend vêm de leitura de código**, sem reprodução em execução. Cada item deve ser confirmado com teste antes de corrigir. Itens marcados *(verificar)* foram sinalizados como incertos pelo auditor.
- Não foram feitos: teste de invasão, teste no navegador contra a API real, medição de contraste/acessibilidade (axe), análise de planos de execução do SQL, revisão ortográfica completa.
- Nenhum arquivo do sistema foi alterado.

## 2. Prioridades (o que atacar primeiro)

| # | Item | Módulo | Sev. |
|---|------|--------|------|
| 1 | Assinatura por biometria local forjável (TrabalhadorId/Score vindos do navegador, sem nonce/replay) | Assinatura | Crítica |
| 2 | Escalada de privilégio: `usuario:editar` atribui perfil Administrador | Acesso | Alta |
| 3 | Vazamento entre obras, inclusive dado clínico de ASO; escrita sem checar escopo de obra | Acesso | Alta |
| 4 | Trava de NR-06 só no frontend; backend confia em dados do cliente | EPI/Treinamento | Alta |
| 5 | Taxa de gravidade fora da NBR 14280 (tipos, óbito/IPT, múltiplos envolvidos); falta Taxa de Frequência | Acidentes | Alta |
| 6 | "Podem trabalhar hoje" usa ASO apto antigo em vez do mais recente; elegibilidade genérica para NR-35/33 | Elegibilidade | Alta |
| 7 | Estoque de EPI: estorno em dobro e inflação na edição/exclusão de entrega | EPI | Alta |
| 8 | Fila offline descarta ação em 401/403; cache offline sem isolamento por usuário | Offline | Alta |
| 9 | Service worker referencia `sw-atualizacao.js`, que não existe no repositório | PWA | Alta |
| 10 | Índices únicos sem filtro de excluído: não dá para recadastrar CPF/CNPJ/código | Cadastros | Alta |
| 11 | APR/PT aprovadas podem ser editadas/reabertas sem controle de estado | APR/PT | Alta |
| 12 | Deploy sem gate de testes; migração automática no startup; contêineres como root | Infra | Alta |

## 3. Achados por módulo

### 3.1 Acesso, segurança e administração
- **Alta** — Escalada de privilégio por vínculo de perfil (`UsuariosController.cs:84`, `AtribuirPerfilObraCommand.cs:37-59`): sem checar tipo do perfil, escopo de quem concede nem autoatribuição.
- **Alta** — Vazamento entre obras: sem filtro global por obra em Aso, Aptidao, ExameComplementar, Treinamento, Contrato, SessaoTreinamento, InstalacaoEpc, EstoqueEpc, EstoqueUniforme, DispositivoAgente, Gsupri* (`SstDbContext.cs:188-241`, `ListarAsosQuery.cs:17-24`). Pt, Apr, NC e EntregaEpi não foram verificados.
- **Alta** — Escopo de obra não aplicado na escrita; `AcessoNegadoException` nunca é lançada (`AcessoPorObraService.cs:57`). Escopo U/P da matriz é ignorado.
- **Alta** — `NovidadesController`: `novidades:usar` (qualquer autenticado) libera POST/PUT/DELETE.
- Média — Soft delete x índice único em `Usuario.Email` e `UsuarioPerfilObra` → HTTP 500 ao recriar.
- Média — Suporte IA: solicitante pelo e-mail do corpo (`SuporteIaController.cs ~160-180`).
- Média — IP da assinatura via `X-Forwarded-For` sem `ForwardedHeaders` (`AssinaturaController.cs:106-113`) — evidência jurídica manipulável (achado em 2 frentes).
- Média — `IdempotenciaMiddleware`: chave global, sem vínculo a usuário/rota/método; ignora DELETE.
- Média — ~38 endpoints DELETE sob `:editar`, contra a regra "só administrador apaga".
- Média — Dado clínico: `aso:ver_clinico`/`aso:homologar` semeadas e nunca usadas; GET de ASO devolve tudo com `aso:ver_status`; acesso ao clínico não é auditado (LGPD).
- Média — Webhook G-Juri sem fail-closed (diferente do G-Supri).
- Média — Vínculo automático por `preferred_username` (mutável/convidado B2B) → risco de sequestro de pré-cadastro.
- Média — Mutações de RBAC e usuários não vão para `TrilhaAuditoria`; `AuditableEntity` não guarda quem alterou.
- Baixa — `DefinirPermissoesPerfil` sem validação (duplicados → 500); possível desativar o último Administrador; excluir perfil em uso.
- Baixa — Sem rate limit nas rotas anônimas; faltam HSTS/nosniff/CSP; `AllowedHosts "*"`; CORS de produção não aplicado se `Cors:AllowedOrigin` vazio.
- Baixa — Tokens MSAL em `localStorage`; `/eu` retorna `ehAdministrador=true` em dev.
- Padrão — Controllers divergem: `BadRequest(string)` vs `{erro}`; `Listar` sem `CancellationToken`; `IConfiguration` injetado só por flag.
- Doc x código — `RBAC-Matrix.md`: nomes de policy diferentes, Camada 2 descrita como pendente mas existente, faltam permissões suporte-ia/novidades/assinatura/cipa e perfis G-RH/G-Supri; colunas U/P e perfil Terceiro sem implementação.
- Positivo — Varredura de todos os endpoints: nenhum sem proteção, exceto 4 anônimos intencionais; sem segredos hardcoded; chaves LGPD ausentes derrubam a API (fail-closed).

### 3.2 Cadastros e pessoas
- **Alta** — Índices únicos sem `Ativo=1`: `CpfHash`, `(ObraId,Matricula)`, `Obra.Codigo`, `Empresa.Cnpj`, `AreaSst.Codigo` (`OrganizacaoConfiguracoes.cs:14,94,95`, `TerceirizadoConfiguracoes.cs:18`, `IdentificacaoConfiguracoes.cs:26`).
- **Alta** — Criar/Atualizar Trabalhador, Ativo e Área sem validar obra nem FKs; Setor/Equipe de outra obra aceitos.
- **Alta** — Empresa, Contrato, Equipe, Tag e AlojamentoMorador sem filtro por obra.
- **Alta** — Excluir Função/Obra/Setor/Equipe/Trabalhador sem checar dependências (órfãos invisíveis); botão da lista de Funções não protegido (`FuncoesTab.tsx:258,510`).
- **Alta** — `MesclarFuncaoDuplicada`: não migra `ContratoVagaFuncao`; comparação de nome exata.
- Média — CNPJ sem dígito verificador; Obra com texto livre e sem campo para corrigir depois.
- Média — Violações de índice sem tradução (só CPF trata) → "erro inesperado".
- Média — N+1 (5–10 consultas por terceirizado), veículos carregam todo histórico, nenhuma listagem paginada; tela de Trabalhadores baixa tudo.
- Média — Pendências de terceirizados incluem demitidos; data de corte em UTC.
- Média — Vagas de contrato nunca são liberadas; Atualizar Trabalhador pode trocar obra/vínculo de terceirizado.
- Média — Equipes: membros de outra obra, nome duplicado, troca de obra com filhos.
- Média — Tags: sem validar entidade, UID sem normalizar, reativação mantém estado antigo.
- Média — Usuários: e-mail sem normalização, sem trava de autoexclusão/último admin, `usuario:editar` atribui qualquer perfil.
- Baixa — Placa sem índice único e global; Alojamento sem Atualizar/Excluir; `CboCodigo` 20 no validator x 100 no banco (edição falha em funções do G-RH); mensagem de exclusão de trabalhador enganosa (`TrabalhadoresTab.tsx:244`); `toISOString()` UTC e valor congelado em `PessoasDashboardTab.tsx:30`; `hoje()/hojeIso()` duplicados.
- Testes — sem testes de Criar/Atualizar/Excluir Trabalhador, Obras, Setores, Áreas, Ativos, Tags, Usuários, escopo por obra, soft delete com unicidade; `CpfValidador` com 2 testes.
- Frontend testado — `cpf.ts`/`datas.ts` corretos; `mascararCpf` com 12+ dígitos mascara os últimos 2 da entrada inteira; `segundaFeiraAtualIso` no domingo devolve a segunda já passada.

### 3.3 Treinamentos, saúde ocupacional e elegibilidade
- **Alta** — Trava de NR-06 só no frontend (`CriarEntregaEpiCommand.cs:42-100`, `EntregasTab.tsx:454-466`): data/nº de lista forjáveis via API (achado em 2 frentes).
- **Alta** — Liberação em lote usa qualquer ASO apto, não o mais recente (`ObterLiberacaoParaTrabalhoQuery.cs:51-57`), divergindo de `AsoValidoRule`.
- **Alta** — `TreinamentoValidoRule` libera com qualquer treinamento válido; aptidões por atividade crítica e exames complementares não são consultados por regra alguma.
- Média — "Hoje" em UTC (`DateTime.UtcNow.Date`) em ~8 regras e no frontend; após 21h BRT o vencimento do dia vira vencido. Centralizar `HojeBrasilia`.
- Média — Certificado PDF imprime carga horária abaixo do mínimo sem validação.
- Média — Atualizar treinamento troca Externo→Aahbrant e trabalhador; FKs sem validação.
- Média — Validade livre, sem duplicidade (trabalhador+curso+data), Externo sem arquivo aceito pela API.
- Média — Front x back divergem sobre o que é NR-06 (`ehCursoNr6` x `NormaHabilitaEpi`).
- Média — ASO: Demissional não excluído como válido; restrições nunca removidas no sync G-RH; tipo fixo "Periódico".
- Baixa — Encerrar turma: mesmo nº de certificado para todos, sem proteção a duplo clique; presença aceita após conclusão; CA que vence hoje bloqueia desde 00:00 UTC; PDF servido inline sem nosniff/CSP; `ElegibilidadeController` ignora obra e demitidos.
- Confirmado OK — magic bytes + 10 MB no certificado; Externo bloqueia emissão do modelo AAHBRANT; `somarMeses` coerente com `AddMonths`.
- Melhorias — paginar `ListarCertificados`; testes de fronteira de fuso, ASO inapto mais recente, Encerrar turma.

### 3.4 EPI, EPC, uniforme e assinatura eletrônica
- **Crítica** — Assinatura biométrica forjável (`AssinaturaController.cs:67-76`, `FutronicAutenticacaoStrategy.cs:44`, `AgenteEndpoints.cs:57`): segredo do dispositivo entregue ao navegador; sem nonce/HMAC/expiração/vínculo ao documento.
- **Alta** — `AtualizarEntregaEpiCommand`: sem `QuantidadeDevolucao <= Quantidade`; editar quantidade/EPI/trabalhador não move estoque; desfazer devolução não reverte.
- **Alta** — `ExcluirEntregaEpiCommand:44-46`: estorno em dobro com devolução total.
- **Alta** — Entrega sem CA válido: EPI sem CA/validade passa; sem revalidação na edição; EPC idem.
- **Alta** — Entrega assinada continua editável/excluível; hash não cobre quantidade/EPI/data.
- **Alta (LGPD)** — Template AES-GCM com chave em texto no `appsettings.json` do agente; fotos faciais e de evidência sem criptografia; sem liveness; consentimento ainda em rascunho; sem retenção/descarte.
- Média — Carrinho de EPI não atômico (uma chamada por item); IP forjável; documento de assinatura sem validar entidade e sem unicidade; Finalizar aceita só o responsável; cadastro facial trava se o treino Azure falha; assinatura offline recebe a hora do servidor; `DefinirEpisPt/EpcsPt` não checa status da PT.
- Baixa — Agente com `Leitor "Simulado"` por padrão (todos batem com todos), limiar 50, anti-falsificação desligado, `/templates/sincronizar` anônimo sem limite; `VerificarIntegridade` falso "adulterado" com signatário inativo; EPC devolve danificado ao estoque.
- Doc — `Motor-Assinatura-Eletronica.md` descreve PIN, crachá e WebAuthn, removidos do código.
- Confirmado OK — `RowVersion` em todas as entidades converte corrida de saldo em 409.
- Testes ausentes — AtualizarEntregaEpi, EPC/Instalações, Estoque EPI, FinalizarDocumento, hash, VerificarIntegridade, DefinirEpisPt/EpcsPt.

### 3.5 APR, permissão de trabalho, inspeções, NC, plano de ação e DDS
- **Alta** — NC Encerrar: `ValidadoPorUsuarioId` vem do corpo (`NaoConformidadesController.cs ~91`), PT/APR usam o token.
- **Alta** — APR: aprovar/reprovar sem guarda de estado; editar/excluir APR aprovada sem reabertura; `AprValidaRule` pode ser alterada após aprovação.
- **Alta** — PT: editar PT autorizada; Suspensa→Autorizada sem reavaliar pré-requisitos; `NovaValidade` aceita data passada.
- Média — NC Devolver→Responder cria ação nova e deixa a antiga em andamento, travando o encerramento.
- Média — Excluir NC deixa ações e alertas órfãos; editar NC encerrada.
- Média — `StatusApr.Encerrada` nunca atribuído; APR/PT sem validade valem para sempre.
- Média — Inspeção de veículo/alojamento: obra fora do escopo do usuário gera 500 (duplicate key) no "obter ou criar"; `ExcluirInspecao` apaga inspeção concluída/assinada.
- Média — `EncerrarDdsCommand` sem guarda de status e sem exigir participante.
- Baixa — Prazos no passado; status Concluído sem data; FKs sem validação; assinatura de APR sem índice único (duplo clique); DDS do dia sem índice único; quem autoriza PT pode ser o executor.
- Confirmado OK — fix de inspeção excluída (`49cb64a`/`3d025da`/`314c53e`): índices filtrados `Status=1 AND Ativo=1`, migration e snapshot consistentes. Falta teste de comportamento (criar, excluir, criar de novo). Validação pública/QR coerente.
- Testes — APR com 5 testes; faltam transições de APR e revalidação de PT.
- Não verificado — frontend de APR/PT/inspeções, `tests-ui` e planilha da PT.

### 3.6 PGR, riscos, CIPA, acidentes e HHT
- **Alta** — Taxa de gravidade soma incidente/quase-acidente/condição insegura/doença (`TaxaGravidadeCard.tsx:126-131,144-149`, `TaxaGravidadeKpiCard.tsx`); lógica duplicada.
- **Alta** — Óbito/IPT somam dias perdidos + 6.000 debitados (deveria ser só debitados).
- **Alta** — Múltiplos envolvidos: dias guardados 1× por ocorrência; TG subestimada (óbito com 2 vítimas conta 6.000, deveria ser 12.000).
- **Alta** — Não existe Taxa de Frequência, embora o HHT já exista.
- Média — `CriarPgrRevisao`: sem validar PGR; não atualiza `DataProximaRevisao` (alerta continua aberto); número da revisão colide em concorrência.
- Média — Acidente: backend não valida coerência afastamento/dias/gravidade; sem controle de CAT; dias debitados IPP sem teto.
- Média — Envolvidos não validados contra a obra; excluir acidente não limpa vínculos.
- Média — CIPA: dimensionamento manual sem Quadro I da NR-5 (aceita 0 titulares); inscrição sem checar status/janela; apuração sem quórum; membro duplicado; "encerrar mandato" faz soft delete e perde o histórico.
- Média — `NivelRiscoLookup` ignora `atividadeId`, escolhe matriz arbitrária; células da matriz não validadas; escala do APR (fixa) diverge da do PGR (configurável).
- Baixa — FKs de Risco/PGR sem validação (500); HHT aceita 0/meses futuros; meta de TG em `localStorage`; fuso local vs UTC no gráfico; `Number(salvo)` pode virar NaN.
- Sem testes — taxa de gravidade, `TabelaDiasDebitados`, revisão de PGR, CIPA, matriz de risco.
- Aviso — pontos de NBR 14280, NR-5, NR-1 e Lei 8.213 citados pelo auditor devem ser validados com o profissional de SST responsável antes de alterar regras.

### 3.7 Alertas, calendário, integrações, offline e dashboard
- **Alta** — Alerta ignorado/resolvido é recriado a cada rodada de 6h (spam; sem índice único) (`AlertaEngineService.cs:46-49,119`).
- **Alta** — Evento de calendário sem lembrete de 48h (`GraphCalendarioTeamsService.cs:156-168`), contra o `PROJECT RULES.md`.
- **Alta** — `sw-atualizacao.js` inexistente (`vite.config.ts:57`, `main.tsx:27`): instalação do service worker pode falhar em build limpo *(confirmar no build publicado)*.
- **Alta** — Fila offline descarta ação após 5 tentativas em qualquer status <500, inclusive 401/403/408/429 (`syncEngine.ts`).
- **Alta** — Cache/fila offline sem isolamento por usuário; sem limpeza no logout (`db.ts`).
- **Regras do PROJECT RULES.md não atendidas** — Adaptive Cards v1.5 e Bot do Teams não existem; só Activity Feed via Graph.
- Média — alerta do motor não aparece no Calendário (`DataLimiteTratamento` nunca preenchido); sem outbox; retry sem backoff e sem respeitar `Retry-After`, envio não idempotente, DLQ sem monitoramento; calendário perde atualização com `Status=Falhou` ou duplica evento; sem timeout/`AbortController`; `catch {}` silenciosos (Suporte IA, Telegram) e Dashboard mostra zeros em caso de erro; `ex.Message` do Graph exposto à UI.
- Baixa — Fuso UTC; Worker sem lock (duplicidade com réplicas); só 3 telas tratam mutação enfileirada offline; `CalendarioPage` sem cancelar requisições.
- Melhorias — endpoint agregado para o Dashboard (hoje 11 listagens no cliente); escalonamento automático inexistente; `$top=250` sem paginação.
- Positivo — fila Service Bus com reprocessamento e dead-letter atendida; JWT Entra presente.

### 3.8 Infraestrutura, banco, testes e padrões de UI
- **Alta** — `MigrateAsync` + seeders a cada startup (`Program.cs:123`); sem health check.
- **Alta** — `deploy.yml` não depende do CI, sem smoke test nem rollback; usa `:latest`.
- **Alta** — Dockerfiles api/web/worker sem `USER` (root); nginx como root.
- **Alta** — Raiz do repo com arquivos pesados soltos (PNG 1,56 MB, HTML modelo, WEBP, logos, vários .md); `.claude/launch.json` com caminho `C:/Projetos/...`; `scripts/desbloquear-rbac-admin.sql` com e-mail pessoal fixo.
- **Alta** — Cobertura: módulos de Application sem referência em teste (busca por nome, pode ter falso positivo): AprAssinaturas, Aptidoes, AreasSst, Auditoria, CatalogosEpc/Epi/Uniforme, EstoquesEpc, ExamesComplementares, InstalacoesEpc, IntegracaoGsupri, MatrizRisco, Novidades, PermissaoTrabalho{Epcs,Epis,RiscosCriticos,TiposTrabalho}, PgrRevisoes, PlanoAcao, RegistrosHhtMensais, SessoesTreinamento. Domain.Tests com 1 arquivo; frontend sem testes unitários; Playwright fora do CI.
- Média — nginx sem cabeçalhos de segurança nem gzip (bundle 2,7 MB); Node 20 na imagem x Node 24 no CI; `AllowedHosts "*"`, seção `Telegram` morta.
- Média — Cascade em 38 relações (Inspeção, Acidente, PT, PGR, Usuário) para registros de valor legal; 25+ query filters que substituem o filtro `Ativo` (armadilhas já comentadas no código).
- Média — CI: backend só em Windows; lint só em arquivos alterados (5 erros antigos permanecem); sem `npm audit`/`dotnet list package --vulnerable`.
- Média — UI: 5 erros `no-restricted-imports` (NovidadesTab, LeitoresDigitalTab, SuporteIaTab, EsteiraSuporteIa, CursosTreinamentoTab); 72 `set-state-in-effect`; 17 `exhaustive-deps`; bundle 2,72 MB sem `React.lazy`.
- Baixa — Cores: código usa `#670000` (correto e coerente com `design-system.md`); **`PROJECT RULES.md:50` ainda cita `#7B1E2B`** (corrigir o doc). 91 hex fixos fora dos tokens (ex.: `GuiaCadastroFacialDialog:71`, `BotaoBiometriaDigital:15`, CSS de impressão de `EntregasTab:587-588`); 735 `style={{}}`; 5 elementos clicáveis sem role/teclado.
- Baixa — Precisão/tamanho de colunas e índices compostos a conferir com plano de execução.
- Positivo — `.gitignore` protege credenciais; sem segredos reais versionados; job de migration pendente no CI; deploy por OIDC; `npm audit` com 0 vulnerabilidades.

## 4. Padrões transversais (a mesma causa em vários módulos)

1. **"Hoje" em UTC** em ~12 pontos (regras, alertas, frontend). Criar um relógio único `HojeBrasilia` no backend e utilitário de data local no frontend.
2. **Soft delete x índice único** em Usuário, Perfil, Trabalhador, Obra, Empresa, Área, Inspeção. Padronizar índices filtrados por `Ativo=1` ou reativação.
3. **Escopo por obra** só em parte das entidades e ausente na escrita.
4. **Regra de negócio só no frontend** (NR-06, validações de acidente, Externo sem arquivo, CPF).
5. **Ausência de guarda de estado** (APR, PT, NC, DDS, treinamento, entrega assinada).
6. **FKs não validadas** → HTTP 500 em vez de erro de negócio; violação de índice sem tradução.
7. **IP por `X-Forwarded-For`** sem configuração (assinatura e auditoria).
8. **Listagens sem paginação**, N+1.
9. **Cobertura de testes** baixa nas regras críticas.

## 5. Plano de execução sugerido

| Fase | Prazo sugerido | Itens |
|------|----------------|-------|
| 0 — Contenção | até 1 semana | Prioridades 1–3; trocar `Leitor "Simulado"` e chaves do agente; restringir escrita de Novidades; `ForwardedHeaders` |
| 1 — Integridade legal | 2–3 semanas | NR-06 no backend; ASO mais recente; estoque EPI; TG/TF conforme NBR 14280 (validar com o responsável de SST); hash do conteúdo da entrega; bloqueio de edição de documento assinado |
| 2 — Robustez | 3–4 semanas | Máquinas de estado APR/PT/NC/DDS; índices filtrados; `HojeBrasilia`; offline (401/403, isolamento por usuário, `sw-atualizacao.js`); alertas sem duplicidade e lembrete de 48h |
| 3 — Plataforma | contínuo | Gate de CI no deploy, health check, migração fora do startup, USER não-root, cabeçalhos nginx, `React.lazy`, lint total no CI, limpeza da raiz do repo, atualizar docs (RBAC-Matrix, Motor de Assinatura, PROJECT RULES) |
| 4 — Testes | contínuo | Cobrir os módulos sem teste e as regras acima antes de cada correção |

Riscos que exigem validação profissional: cálculos de taxas (NBR 14280), CIPA (NR-5), CAT (Lei 8.213), biometria/LGPD (jurídico e DPO), assinatura eletrônica como prova (jurídico).
