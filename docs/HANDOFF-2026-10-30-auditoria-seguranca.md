# Handoff SST - 2026-10-30 - Auditoria de seguranca e erros (para correcao)

Relatorio da auditoria feita em 30/10/2026 para guiar as correcoes. Serve de roteiro para a proxima sessao (Claude Code ou Codex).

## Fonte da verdade

- Codigo auditado: `origin/master` no commit `f5cf7053` (merge do PR #263), o mesmo que esta em hml.
- Tipo de auditoria: so leitura do codigo, em 3 frentes (acesso e permissoes; dados sensiveis, LGPD e segredos; entradas, arquivos e bugs). Nada foi corrigido.
- Os achados marcados como **confirmado** foram conferidos lendo o codigo. **Provavel** depende de configuracao de ambiente ou de comportamento de biblioteca. **Hipotese** precisa ser reproduzida antes de corrigir.
- Os caminhos de arquivo sao relativos a `src/`.

## Regras para quem for corrigir

- Uma branch e um PR por grupo, a partir de `origin/master` atualizado.
- Cada correcao com teste automatizado que falhe antes e passe depois.
- Mudanca visivel ao usuario: mockup HTML aprovado antes do codigo, e entrada em `NovidadesSeeder.cs`.
- Deploy so com autorizacao do usuario, depois de mostrar o resultado.
- Itens marcados **[DPO/juridico]** precisam de decisao do encarregado de dados ou do juridico antes de mudar o comportamento.
- Nao apagar dados de producao/hml; nao resetar banco sem pedir.

## O que esta correto (nao mexer)

- Nenhum segredo de producao commitado (codigo atual e historico). `appsettings*.json` de Api, Worker e Agente sem segredos; CI usa valores ficticios; deploy por OIDC.
- CPF cifrado com AES-256-GCM (IV aleatorio) e hash HMAC-SHA256 com chave separada. Fotos cifradas com chave derivada (HKDF).
- Sem SQL injection (o unico `SqlQueryRaw` usa constantes), sem SSRF, sem XSS (nenhum `dangerouslySetInnerHTML`; uploads validados por bytes iniciais e servidos so como imagem/PDF/docx).
- Todas as acoes tem `[Authorize(Policy=...)]`, exceto as rotas anonimas intencionais (agente biometria, `sst/p`, `sst/validar`).
- Bypass de autenticacao so em Development (derruba a API fora dele). CORS de producao restrito.
- Estoque de EPI com RowVersion e indice unico por (catalogo, obra). Saida da IA no PGR validada no servidor. Cliente do Claude com timeout e retentativas limitadas.

---

## GRUPO 1 - Seguranca e LGPD (prioridade maxima)

### 1.1 [ALTA] Conteudo clinico do ASO liberado sem a permissao `aso:ver_clinico` - confirmado
- **Onde:**
  - `AAHBRANT.SST.Infrastructure/Persistencia/Seed/RbacSeeder.cs:54-55` define `aso:ver_status` ("sem detalhe clinico") e `aso:ver_clinico`. O codigo `aso:ver_clinico` nao e usado em nenhuma controller ou handler.
  - `AAHBRANT.SST.Api/Controllers/AsosController.cs` (listar, obter, `GET {id}/arquivo`) usa so `aso:ver_status`.
  - `AAHBRANT.SST.Application/Asos/Queries/ListarAsosQuery.cs:39` e `ObterAsoPorIdQuery.cs:31` devolvem `ObservacoesClinicas`.
  - `AAHBRANT.SST.Application/Trabalhadores/Queries/ObterPerfilCompletoTrabalhadorQuery.cs` (~120, 161-173, 315), exposto por `TrabalhadoresController` `perfil-completo` com `trabalhador:ver`: devolve CPF completo, RG, ASOs com `ObservacoesClinicas`, medico/CRM e fotos de evidencia das assinaturas.
- **Risco:** encarregado ou tecnico que so deveria ver Apto/Inapto le diagnostico e baixa o PDF do ASO (dado de saude, LGPD art. 11).
- **Correcao:**
  - DTOs de status sem `ObservacoesClinicas`; endpoint ou campo clinico so com `aso:ver_clinico`.
  - Download do arquivo do ASO exigindo `aso:ver_clinico`.
  - `perfil-completo`: ASO so com `aso:ver_status`, conteudo clinico so com `aso:ver_clinico`; CPF mascarado.
- **Aceite:** teste de controller/handler com usuario sem `aso:ver_clinico` nao recebe `ObservacoesClinicas` nem o arquivo (403).

### 1.2 [ALTA] Escalada de acesso pela atribuicao de perfil - confirmado
- **Onde:**
  - `AAHBRANT.SST.Api/Controllers/UsuariosController.cs:81-82` (`usuario:editar`).
  - `AAHBRANT.SST.Application/Usuarios/Commands/AtribuirPerfilObraCommand.cs:47-72`: so barra conceder Administrador e conceder a si mesmo. Nao confere se `ObraId` esta no escopo do solicitante e aceita `ObraId = null` (acesso global, ver `AcessoPorObraService`).
  - `AAHBRANT.SST.Application/PerfisAcesso/Commands/DefinirPermissoesPerfilCommand.cs:39-75` (`perfilacesso:gerenciar_permissoes`): altera qualquer perfil, inclusive o do proprio chamador e perfis de sistema.
- **Risco:** tecnico da Obra A da a outra conta perfil Gestor QSMS com acesso a todas as obras; quem gerencia permissoes marca qualquer permissao no proprio perfil.
- **Correcao:**
  - So Administrador (ou escopo global) concede `ObraId = null`.
  - Demais: `ObraId` obrigatorio e dentro do escopo de `usuario:editar` do solicitante.
  - `DefinirPermissoes` so para Administrador; proibir alterar perfil que o proprio solicitante usa.
- **Aceite:** testes cobrindo os tres bloqueios.

### 1.3 [ALTA] Uma leitura de digital assina varios documentos - confirmado
- **Onde:**
  - `AAHBRANT.SST.AgenteBiometria/Endpoints/AgenteEndpoints.cs:23, 82, 111`: lista `Desafios` sem limite; texto assinado `desafio|trabalhadorId|score`, sem documento.
  - `AAHBRANT.SST.Api/Controllers/DispositivosAgenteController.cs` `POST {id}/desafios`: so `[Authorize]`, sem policy nem escopo.
  - `AAHBRANT.SST.Infrastructure/Assinatura/FutronicAutenticacaoStrategy.cs:66-90`: confere leitor, prazo e uso unico do desafio, sem vinculo com `DocumentoAssinaturaId`.
- **Risco:** o operador gera varios desafios, o trabalhador poe o dedo uma vez (ex.: entrega de EPI) e as leituras assinam outros termos que ele nao viu.
- **Correcao:**
  - Desafio emitido ja vinculado ao(s) `DocumentoAssinaturaId` (gravar em `DesafioBiometria`) e conferido no registro.
  - Incluir o id do documento no texto assinado pelo agente.
  - Lote so para documentos de um mesmo fluxo registrado no servidor (ex.: carrinho de entrega).
  - Emissao de desafio exigindo `assinatura:assinar` com escopo de obra.
- **Atencao:** muda o contrato entre navegador, agente e API. Exige nova versao do agente (auto-update) e compatibilidade durante a transicao. Testar com o leitor Futronic real.

### 1.4 [ALTA - conferir hml] Caminho antigo da digital sem leitura assinada - provavel
- **Onde:**
  - `AAHBRANT.SST.Infrastructure/Assinatura/AssinaturaOptions.cs:11`: `ExigirLeituraAssinadaBiometria = false` por padrao; nao aparece em appsettings nem nos workflows.
  - `FutronicAutenticacaoStrategy.cs:28, 97-106`: sem chave publica registrada, basta `SegredoDispositivo` + `TrabalhadorId` + `Score` no corpo (`AssinaturaController.cs:83-90`).
  - `RegistrarChavePublicaDispositivoCommand`: registro da chave e "primeiro que chegar", so com o segredo.
- **Risco:** quem tem o segredo do leitor assina como qualquer trabalhador sem dedo, ou registra a propria chave antes do agente legitimo.
- **Primeiro passo:** conferir no Container App de hml se `Assinatura__ExigirLeituraAssinadaBiometria=true`. Se nao estiver, ligar (com autorizacao do usuario).
- **Correcao:** padrao `true`, remover o caminho sem assinatura, registrar a chave publica no cadastro autenticado do leitor (instalador), nao por chamada anonima.

### 1.5 [MEDIA] Credencial do G-RH com acesso amplo - confirmado
- **Onde:** `AAHBRANT.SST.Api/Autorizacao/AppRolesReconhecidas.cs` mapeia `Grh.LerColaboradores` e `Sst.LerFotos` para `trabalhador:ver`; `Middlewares/EscopoPorObraMiddleware.cs:43` da escopo global a qualquer App Role.
- **Risco:** com o client secret do G-RH le-se `perfil-completo` (CPF, RG, ASO clinico, fotos), relatorio e liberacao de todas as obras.
- **Correcao:** permissoes dedicadas (ex.: `integracao-grh:foto`) usadas so em `IntegracaoGrhController`.

### 1.6 [MEDIA] Foto facial e dados publicos pelo QR/NFC - confirmado [DPO/juridico]
- **Onde:** `AAHBRANT.SST.Api/Controllers/IdentificacaoPublicaController.cs` (`GET sst/p/{uid}/foto`, anonimo); `ObterFotoTrabalhadorPublicoQuery.cs` devolve `Trabalhador.FotoConteudo`, a mesma referencia do Azure Face. `ResolverTrabalhadorPublicoQuery.cs` devolve nome, matricula, obra, funcao, EPIs, treinamentos e ultimos DDS. Tags NFC usam o UID de fabrica (nao e segredo, sequencial por lote).
- **Risco:** quem le o QR/tag ou varre UIDs monta lista de trabalhadores com rosto e obra (dado biometrico, LGPD art. 5, II).
- **Correcao:** foto so com login (ou versao de cracha separada da referencia biometrica); rota anonima so com token aleatorio gravado na tag; tirar foto e historico de DDS da resposta anonima; limite global e alerta de 404 em serie.

### 1.7 [MEDIA] Dados de saude e CPF enviados ao Claude - confirmado [DPO/juridico]
- **Onde:** `AAHBRANT.SST.Infrastructure/LeituraIa/ClaudeClassificadorDocumentoTerceirizado.cs:36-60, 103` (ASO, certificados, fichas inteiros); `Integracao/SuporteIa/AzureOpenAiAnalistaPlanoOcorrencia.cs:126-131` e `AzureOpenAiClassificadorOcorrencia.cs:121` (relato, lesao, nomes). Apesar do nome, usam `ClaudeChatJsonCliente`.
- **Risco:** transferencia internacional de dado sensivel (LGPD art. 33) sem contrato/retencao zero/RIPD documentados.
- **Correcao:** validar contrato, retencao zero e RIPD com DPO/juridico; mascarar nomes e CPF no relato de ocorrencia antes do envio.

### 1.8 [MEDIA] PDF do DDS enviado a grupo do Telegram - confirmado [DPO/juridico]
- **Onde:** `AAHBRANT.SST.Application/Relatorios/GerarListasDePresencaCommand.cs:79-86`, `PublicadorRelatorio.cs:96-103`, `AAHBRANT.SST.Infrastructure/Integracao/Telegram/TelegramResumoService.cs:45,72`. O PDF (`ExportarDdsPdfQuery.cs:56-91`) tem nomes, funcao, forma de assinatura e fotos com latitude/longitude.
- **Correcao:** Telegram so com resumo numerico; PDF so pelo sininho do Teams.

### 1.9 [MEDIA] CPF completo na listagem e na URL - confirmado/provavel
- **Onde:** `AAHBRANT.SST.Application/Trabalhadores/Queries/ListarTrabalhadoresQuery.cs:35` (`GET api/trabalhadores`); o front compara CPF na tela (`TeamsApp/src/pages/terceirizados/ImportarPastaTerceirizado.tsx:392`). `IntegracaoGrhController.cs:28` recebe CPF no caminho (`trabalhadores/{cpf}/foto`), que fica em logs.
- **Correcao:** mascarar CPF na listagem e comparar por CPF num endpoint de servidor (hash); CPF no corpo de POST ou por hash na integracao.

---

## GRUPO 2 - Erros com impacto direto na obra

### 2.1 [ALTA] Devolucao de EPI aceita qualquer quantidade - confirmado
- **Onde:** `AAHBRANT.SST.Application/EntregasEpi/Commands/AtualizarEntregaEpiCommand.cs`: o validador (31-42) nao confere `QuantidadeDevolucao`; ~111-112 faz `estoque.Saldo += QuantidadeDevolucao ?? Quantidade`.
- **Risco:** devolver 500 de uma entrega de 1 cria 500 unidades; numero negativo some com estoque. Apagar ou editar uma devolucao ja feita nao estorna.
- **Correcao:** `QuantidadeDevolucao` entre 1 e `Quantidade`; bloquear edicao/remocao de devolucao ja registrada ou estornar a diferenca.

### 2.2 [ALTA] EPI excluido do catalogo some da Ficha NR-06 - confirmado
- **Onde:** `CatalogosEpi/Commands/ExcluirCatalogoEpiCommand.cs:28` faz exclusao logica (o `Restrict` da FK nunca dispara). `EntregasEpi/Queries/ExportarFichaEpiTrabalhadorQuery.cs:41-45` usa `Include(e => e.CatalogoEpi)` com navegacao obrigatoria e filtro `Ativo` (INNER JOIN), e o mesmo em `Alertas/Motor/EpiAlertaProvider.cs:20-22`. Padrao conhecido: ver memoria "INNER JOIN + soft delete".
- **Risco:** a ficha de EPI, prova na fiscalizacao, perde as entregas daquele EPI; alertas param.
- **Correcao:** bloquear exclusao de catalogo com entregas, ou `IgnoreQueryFilters`/projecao com LEFT JOIN na ficha e nos alertas. Varrer outros `Include` de catalogo com o mesmo problema.

### 2.3 [ALTA] Imagens sem limite de dimensao (pode derrubar a API) - confirmado/provavel
- **Onde:** `AAHBRANT.SST.Infrastructure/LeituraIa/ImagemPaginaPdf.cs:38, 55-57` (`Image.Load` sem limite; usado por `ClaudeClassificadorDocumentoTerceirizado.cs:103` e OCR de PDFs) e `Documentos/MarcaDaguaLogo.cs:34,55` (logo da obra). O protetor `ImagemSegura` ja existe e nao e usado nesses pontos.
- **Risco:** PNG pequeno declarando 50000x50000 px estoura a memoria e reinicia o container.
- **Correcao:** usar `ImagemSegura.Carregar/Validar` nesses pontos e validar a logo no upload.

### 2.4 [MEDIA] Validades comparadas em UTC (vence as 21h) - confirmado
- **Onde:** `EntregasEpi/Commands/CriarEntregaEpiCommand.cs:89` e `InstalacoesEpc/Commands/CriarInstalacaoEpcCommand.cs:45` (`< DateTime.UtcNow`); `Elegibilidade/Rules/AsoValidoRule.cs:37`, `TreinamentoValidoRule.cs:40`, `AprValidaRule.cs:37`, `PermissaoTrabalhoValidaRule.cs:38`, `Trabalhadores/Queries/ObterLiberacaoParaTrabalhoQuery.cs:51`, `Terceirizados/CalculadoraLiberacaoTerceirizado.cs:17` (`DateTime.UtcNow.Date`).
- **Risco:** a partir das 21h de Brasilia, ASO/treinamento/CA que vence hoje aparece vencido e bloqueia o turno da noite.
- **Correcao:** centralizar o `HojeBrasilia()` (ja existe em `CriarEntregaEpiCommand.cs:154`, e `HorarioBrasilia` na Infrastructure) e comparar validade como data.

### 2.5 [MEDIA] Estorno/devolucao de estoque na obra atual do trabalhador - confirmado
- **Onde:** `EntregasEpi/Commands/ExcluirEntregaEpiCommand.cs` (EstornarEstoqueAsync ~76-82), `EntregasUniforme/Commands/ExcluirEntregaUniformeCommand.cs:53-59`, `AtualizarEntregaEpiCommand.cs:69-71, 101-104`.
- **Risco:** trabalhador transferido de obra: o estorno credita a obra nova. Trabalhador desligado (soft delete): o estorno some sem aviso, e a devolucao falha com "Trabalhador nao encontrado".
- **Correcao:** resolver o estoque pela movimentacao `SaidaEntrega` (que guarda `EstoqueEpiId`) ou gravar `ObraId` na entrega; `IgnoreQueryFilters` ao buscar o trabalhador.

### 2.6 [MEDIA] Injecao de instrucoes na importacao da pasta da terceirizada - provavel
- **Onde:** `Application/LeituraIa/ClassificarDocumentoTerceirizado.cs:123-142` (texto do PDF vai direto ao Claude); `TeamsApp/src/pages/terceirizados/ImportarPastaTerceirizado.tsx:503-600` ("Cadastrar tudo" grava ASOs e certificados com os valores da IA).
- **Risco:** PDF com texto escondido faz a IA sugerir "Apto, validade 31/12/2030" e o trabalhador aparece liberado.
- **Correcao:** marcar o texto do documento como dado nao confiavel no prompt; confirmacao individual de ASO Apto e validade, mostrando o trecho de origem; checar validade contra a data do exame.

### 2.7 [MEDIA] Arquivo da EAP pode consumir muita memoria - provavel
- **Onde:** `AAHBRANT.SST.Infrastructure/GeracaoPgr/ExtratorTextoEap.cs:36, 43-62` (xlsx ate 20 MB carregado inteiro; limite de caracteres so depois). Linha 79: `XDocument.Load` do XML do MS Project com DTD habilitado (hipotese de amplificacao).
- **Correcao:** checar tamanho descomprimido das entradas do zip; parar a extracao no limite; `XmlReaderSettings { DtdProcessing = Prohibit, XmlResolver = null }`.

---

## GRUPO 3 - Estrutural

### 3.1 [MEDIA] Chave unica das digitais em todos os PCs de obra - confirmado
- **Onde:** `AAHBRANT.SST.Infrastructure/DependencyInjection.cs:64-71`; `AAHBRANT.SST.AgenteBiometria.Instalador/InstaladorAgente.cs:115` grava chave e `SegredoDispositivo` em texto puro (so ACL, sem DPAPI); `AgenteBiometria/Servicos/TemplateCacheService.cs:29`; `Infrastructure/Seguranca/ImagemBiometricaCriptografiaService.cs:7` usa a mesma chave para as imagens; `Application/Assinatura/Queries/SincronizarTemplatesQuery.cs:46-48` (anonimo com segredo) devolve templates da obra e dos instrutores de todas as obras; `ChaveAssinaturaAgente.cs:48-52` usa DPAPI LocalMachine.
- **Risco:** um PC comprometido decifra digitais e imagens de todas as obras. Digital nao se troca.
- **Correcao:** chave por dispositivo (envelopar templates com a chave publica do posto) ou match no servidor; chave separada para imagens; DPAPI/TPM no posto; rotacionar a chave atual. Exige nova versao do agente.

### 3.2 [MEDIA] Exclusao sem exigir Administrador em ~50 DELETE - confirmado
- **Onde:** so 15 controllers usam `PoliticasAutorizacao.SomenteAdministrador`. Exemplos com `editar/excluir`: `AsosController.cs:68`, `PcmsosController.cs:96`, `AcidentesController.cs:238,273`, `TrabalhadoresController.cs:88`, `PgrsController.cs:68`, `UsuariosController.cs:73,96`, `ObrasController.cs:88`, `ExamesComplementaresController.cs:47`.
- **Regra do usuario:** exclusao so do Administrador (24/09).
- **Correcao:** trocar a policy desses DELETE, ou listar com o usuario quais ficam de fora de proposito.

### 3.3 [MEDIA] Listagens sem paginacao - confirmado
- **Onde:** `Application/Assinatura/Queries/ListarDocumentosAssinaturaQuery.cs:57-66` (carrega tudo de todas as obras e filtra escopo em memoria); `Application/Auditoria/Queries/ListarTrilhaAuditoriaQuery.cs:41-55` (tudo, com JSON antes/depois); ~60 `Listar*Query` sem `Take/Skip`.
- **Correcao:** paginacao, `Take` maximo e filtro de escopo no SQL.

### 3.4 [MEDIA] Retencao das fotos biometricas de evidencia desligada - provavel [DPO/juridico]
- **Onde:** `Infrastructure/Seguranca/RetencaoFotosBiometricasBackgroundService.cs:26-32`; `Lgpd:DiasRetencaoFotosEvidencia` nao esta definido em appsettings nem workflows.
- **Correcao:** DPO/juridico define o prazo; configurar no ambiente.

---

## GRUPO 4 - Baixa gravidade

| Item | Onde | Correcao |
|---|---|---|
| Qualquer logado cria/edita/apaga o pop-up de novidades | `Api/Autorizacao/PermissaoAuthorizationHandler.cs:119`, `NovidadesController.cs:20,36-49` | Escrita so `SomenteAdministrador`; leitura livre |
| Idempotencia sem vinculo a usuario/rota | `Api/Middlewares/IdempotenciaMiddleware.cs:41-48` | Gravar e comparar `oid` + rota + metodo |
| 409 devolve todas as colunas do registro | `Api/Middlewares/TratamentoDeExcecaoMiddleware.cs:60,102` | Devolver o DTO, nao `GetDatabaseValuesAsync` bruto |
| Mensagem interna de `InvalidOperationException` volta ao cliente | `TratamentoDeExcecaoMiddleware.cs:50` | Excecao de negocio propria; generica para as demais |
| Sem CSP/frame-ancestors, X-Content-Type-Options, Referrer-Policy; sem HSTS | `nginx.conf`, `Api/Program.cs` | Adicionar headers (frame-ancestors restrito ao Teams) |
| Sem politica de fallback (acao nova sem atributo nasce anonima) | `PermissaoAuthorizationPolicyProvider.GetFallbackPolicyAsync` | `FallbackPolicy = RequireAuthenticatedUser` + teste de arquitetura |
| Vinculo de conta por `preferred_username` | `Api/Middlewares/VinculoAzureAdMiddleware.cs:56-67` | Exigir `tid` da empresa e conta nao convidada, ou vinculo por convite (hipotese, depende do Entra) |
| Relato por texto do Suporte IA sem limite | `Api/Controllers/SuporteIaController.cs:106-115` | Limitar a ~8 mil caracteres |
| Assinatura dupla em requisicoes simultaneas | `Application/Assinatura/IRegistradorAssinaturaService.cs:~110-133`; indice em `Infrastructure/Persistencia/Configuracoes/AssinaturaConfiguracoes.cs:50` inclui o metodo | Indice unico sem o metodo, ou transacao (hipotese) |
| Tokens do MSAL em localStorage | `TeamsApp/src/lib/browserAuth.ts:54` | sessionStorage, se a experiencia fora do Teams permitir |
| G-JURI aberto se `Seguranca:ApiKey` vazia; CORS `*`; Swagger publico; comparacao nao constante; chave DataJud commitada (publica do CNJ, comeca com "cDZH") | `AAHBRANT.JURI.Api/Program.cs:40, 58-74`, `appsettings.json:19` | Falhar sem chave fora de Development; CORS restrito; `FixedTimeEquals`; Swagger so em dev; chave em variavel de ambiente |

---

## Para confirmar fora do codigo (sem acesso pela auditoria)

- [ ] `Assinatura__ExigirLeituraAssinadaBiometria` nos Container Apps de hml e prod (item 1.4).
- [ ] `Lgpd__DiasRetencaoFotosEvidencia` em hml/prod (item 3.4).
- [ ] `Seguranca__ApiKey` no deploy do G-JURI, se ele estiver publicado.
- [ ] App Registration do SST: "Assignment required" e aceite de convidados (B2B).
- [ ] Contrato com a Anthropic (retencao zero) e RIPD cobrindo dados de saude (item 1.7).

## Ordem sugerida

1. Grupo 1: itens 1.1, 1.2, 1.3 e a conferencia do 1.4 (um PR por item ou 1.1+1.2 juntos).
2. Grupo 2: 2.1 + 2.2 + 2.5 (estoque e ficha de EPI, um PR), 2.4 (datas), 2.3 (imagens).
3. Itens [DPO/juridico] depois da decisao.
4. Grupo 3 e Grupo 4.
