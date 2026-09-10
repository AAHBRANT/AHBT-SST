# Assinatura Salva do Usuário + Remoção de Crachá+PIN e Telegram/DDS — Design

**Data:** 2026-09-10
**Status:** aprovado em brainstorming, pronto para virar plano de implementação (`writing-plans`)

## 1. Objetivo

O usuário logado (técnico, engenheiro, qualquer conta com login no Teams) cadastra a própria
assinatura uma única vez — desenhando na tela ou enviando uma foto — e ela fica salva no perfil da
conta (`Usuario`), acessível pelo ícone de perfil no canto superior direito. Daí em diante, sempre
que essa pessoa aprovar/autorizar/assinar um documento (APR, PT, Inspeção/NC, relatórios em PDF), o
sistema desenha essa assinatura no lugar de imprimir só o nome.

Junto (pedido do usuário, mesma conversa): remover código morto de crachá+PIN e remover por
completo a integração de Telegram do DDS.

Fora de escopo: trabalhadores de campo sem conta Entra ID — esses continuam assinando por
biometria (Futronic) ou reconhecimento facial, que não usam imagem de assinatura (a autenticação
em si já é a prova).

## 2. Achado que redesenha o escopo original: APR/PT não passam pelo Motor de Assinatura

O projeto já tem um Motor de Assinatura Eletrônica genérico (`DocumentoAssinatura`/
`DocumentoSignatario`, `EntidadeTipo`/`EntidadeId` polimórfico) com métodos `Biometria`,
`SessaoLogada` e `ReconhecimentoFacial`. **Inspeções/NC e Treinamento/DDS Semanal já usam esse
motor de verdade** — `AssinaturaQuiosque`/`assinarComSessao` resolvem a identidade a partir do
token da sessão (claim `oid`), nunca de algo que o cliente informa.

**APR e PT não usam esse motor.** `AprovarAprCommand`/`AutorizarPermissaoTrabalhoCommand` recebem
um `Guid` (`AprovadoPorUsuarioId`/`AutorizadoPorUsuarioId`) **direto do corpo da requisição**, e o
frontend de APR (`AprDetalhePage.tsx`) tem, literalmente, um campo de texto livre rotulado "ID do
usuário aprovador (GUID)" — não é dropdown, é colar um GUID à mão. Não existe verificação de que
quem clicou é quem está logado.

Colar uma imagem de assinatura em cima disso decoraria um campo que ninguém verifica. **Decisão do
usuário: corrigir isso junto** — substituir a entrada de GUID por "o próprio usuário logado clica
para assinar", replicando o padrão que `AssinaturaCertificadoTreinamentoDialog.tsx` já usa
(`api.assinatura.assinarComSessao(documentoId)`, sem nenhum dado extra no corpo — o servidor resolve
tudo a partir do JWT).

## 3. Onde mora a assinatura: `Usuario`, não `Trabalhador`

Decisão do usuário: a assinatura salva é do **login** (`Usuario`), não do cadastro de campo
(`Trabalhador`) — mesmo que a pessoa também tenha um `Trabalhador` vinculado. Motivo prático: nem
todo `Usuario` que aprova PT/APR tem (ou precisa ter) um `Trabalhador` — `Usuario.TrabalhadorId` é
opcional hoje. E o público desta feature é só quem tem login (confirmado pelo usuário logo no
início do brainstorming: "os funcionários da obra não têm acesso ao Teams nem conta").

**Consequência no schema de auditoria:** `DocumentoSignatario` hoje só grava `TrabalhadorId` (nunca
`UsuarioId`) — reconstruir "qual login assinou" a partir só do `TrabalhadorId` seria ambíguo se um
dia mais de uma conta apontar para o mesmo trabalhador. Por isso este design adiciona
`DocumentoSignatario.UsuarioId` (`Guid?`, preenchido só quando `MetodoAutenticacao == SessaoLogada`)
— campo aditivo, não altera nem remove nada do que já existe na tabela.

## 4. Modelo de dados novo

- `Usuario.AssinaturaImagemConteudo` (`byte[]?`), `AssinaturaImagemContentType` (`string?`),
  `AssinaturaAtualizadaEm` (`DateTime?`) — mesmo padrão já usado em
  `Trabalhador.FotoConteudo`/`FotoContentType` (blob no próprio banco, sem Blob Storage, que não
  existe neste projeto).
- `DocumentoSignatario.UsuarioId` (`Guid?`, nullable, FK para `Usuario`) — ver seção 3.
- Nenhuma tabela nova.

## 5. Cadastro da assinatura — ícone de perfil

`AppShell.tsx` já tem um botão no canto superior direito (`usuarioChip`, com nome + ícone de
pessoa) com o tooltip **"Foto de perfil (em breve)"** — hoje sem nenhuma ação. Vira um menu real
(Fluent UI `Menu`/`MenuPopover`) com o item **"Minha assinatura"**, que abre um diálogo com:

- Um canvas de desenho — biblioteca `signature_pad` (MIT, ~7 KB, sem dependências; o projeto não
  tem nenhum componente de assinatura por traço hoje, então não há padrão interno a reaproveitar).
- Uma alternativa "Usar uma foto" — mesmo padrão `SeletorFotoCamera.tsx` já usado em
  DDS/Inspeções/Obras (`<input type="file" capture>`), evitando um segundo componente de captura.
- Botões Salvar / Limpar / Refazer.

Endpoint novo: `PUT /api/usuarios/me/assinatura` (multipart, aceita o PNG do canvas ou a foto
enviada) — resolve o `Usuario` a partir do `AzureAdObjectId` da sessão (mesma regra de
`RegistrarAssinaturaSessaoLogadaCommand`), grava a imagem. `GET /api/usuarios/me/assinatura` para
mostrar a prévia salva ao reabrir o diálogo.

## 6. APR — troca da caixa de GUID pelo clique autenticado

- `AprDetalhePage.tsx`: remove o `<Input>` de "ID do usuário aprovador (GUID)". No lugar, um botão
  **"Assinar e aprovar"**, visível para qualquer usuário logado, que:
  1. Cria/obtém `DocumentoAssinatura(EntidadeTipo="Apr", EntidadeId)` (idempotente, mesmo padrão do
     Treinamento).
  2. Chama `POST /api/documentos/{id}/assinar/sessao` (`assinarComSessao`) — grava o
     `DocumentoSignatario` com `MetodoAutenticacao=SessaoLogada` e o novo `UsuarioId`.
  3. Ato de assinar já dispara `AprovarAprCommand` internamente (não precisa mais do parâmetro
     `AprovadoPorUsuarioId` vindo do cliente — o handler passa a resolver o usuário pela sessão,
     mesmo caminho do passo 2).
- Mesmo tratamento para reprovação, se hoje também usar um campo de usuário livre (a confirmar no
  plano de implementação, olhando `ReprovarAprCommand`).

## 7. PT — mesmo tratamento nos 4 pontos de assinatura

`PermissaoTrabalho` tem quatro atos que hoje recebem um `Guid` cru:
`AutorizarPermissaoTrabalhoCommand` (emitente/responsável de área + responsável SST, quando
informado), `SuspenderPermissaoTrabalhoCommand`, `RevalidarPermissaoTrabalhoCommand`,
`EncerrarPermissaoTrabalhoCommand`. Todos passam a resolver o usuário pela sessão logada, mesmo
padrão da seção 6 — cada botão da tela de PT correspondente assina via `assinarComSessao` antes de
disparar o comando de domínio.

`ResponsavelExecucaoUsuarioId` **não muda**: é escolhido na criação/edição da PT (quem vai
executar), e sua `DataAssinaturaExecucao` já é carimbada automaticamente no momento da autorização
— não é um ato de assinatura próprio, não precisa de botão dedicado.

## 8. Inspeções/NC

Já usam o motor de verdade (`AssinaturaQuiosque`) — nenhuma mudança de fluxo. Só passam a desenhar
a imagem salva (seção 9) em vez de mostrar apenas o nome, quando o signatário tiver uma assinatura
cadastrada.

## 9. Renderização no PDF

Nos serviços de PDF que hoje imprimem só texto num bloco de assinatura (ex.:
`ExportarPermissaoTrabalhoPdfQuery`/`PtPdfAssinatura(Nome, Data)`, e equivalentes de APR e
Inspeção), busca-se a assinatura do `Usuario` vinculado ao `DocumentoSignatario.UsuarioId` (quando
presente) e desenha a imagem acima do nome com `.Image(bytes).FitArea()` (mesmo padrão já usado
para logo/fotos em `CabecalhoDocumentoPadrao.cs`/`InspecaoPdfService.cs`), num bloco de tamanho fixo
(ex.: 160×60 px) para manter o layout do documento estável independente do formato/proporção da
imagem enviada pelo usuário.

**Fallback:** signatário sem assinatura cadastrada (não passou pela seção 5 ainda) ou assinado por
`Biometria`/`ReconhecimentoFacial` (métodos sem imagem, a autenticação em si é a prova) continua
mostrando só texto — comportamento de hoje, sem quebra.

## 10. Remoção de crachá+PIN (código morto)

Já foi removido por completo em 31/08 (migration `RemoverPinEWebAuthn`, enum sem os valores
`CrachaPin`/`QrCodePin`/`WebAuthnCelular`, sem tabela/coluna residual). Verificado nesta sessão:
não sobrou nenhuma referência de código a esses tipos, só comentários históricos explicando a
remoção. Único ajuste: o comentário em `Obra.cs` ainda cita "opera só com CrachaPin até o hardware
chegar" — corrigido para refletir a realidade atual (só Biometria/ReconhecimentoFacial).

## 11. Remoção completa do Telegram no DDS

Confirmado nesta sessão: `ITelegramService` é usado **só** por DDS
(`EnviarDdsTelegramCommand`) e pelo vínculo de trabalhador
(`GerarVinculoTelegramCommand`/`TelegramUpdatesPollingService`) — nenhum outro módulo depende disso.
Remoção completa, sem substituto:

- **Frontend** (`DdsDetalhePage.tsx`): botão "Enviar via Telegram", função `enviarTelegram`,
  estados `enviandoTelegram`/`resultadoTelegram`, funções `tomTelegram`/`rotuloTelegram` e a coluna
  "Telegram" na tabela de participantes. Tela de cadastro/vínculo de Telegram do trabalhador
  (onde estiver, ex.: `TrabalhadorDetalhePage`/aba de perfil).
- **Backend**: `EnviarDdsTelegramCommand`, `GerarVinculoTelegramCommand`, `ITelegramService` e as
  duas implementações (`TelegramBotService`, `TelegramUpdatesPollingService`), os dois registros de
  `AddHostedService<TelegramUpdatesPollingService>()` em `DependencyInjection.cs`, o método
  `AddPollingDeAtualizacoesTelegram()` referenciado em `Program.cs` da API.
- **Dados**: entidade e tabela `DdsTelegramEnvio`, colunas `Trabalhador.TelegramChatId`/
  `TelegramCodigoVinculo` — migration de remoção.
- **Configuração**: `Telegram:BotToken`/`BotUsername` deixam de ser lidos (podem sumir dos
  `appsettings`/secrets do Container App depois do deploy, sem urgência).
- `AlertaEngineWorker` e os processadores de Service Bus/Teams (calendário, notificações) não usam
  `ITelegramService` — seguem intocados.

## 12. Testes

- Unitário: comandos de APR/PT (`AprovarApr`, `AutorizarPermissaoTrabalho`, `Suspender`,
  `Revalidar`, `Encerrar`) passam a resolver o usuário pela sessão — cobrir o caso de usuário
  autenticado sem `Usuario.TrabalhadorId` vinculado (deve poder assinar mesmo assim, já que a
  assinatura agora é do `Usuario`, não do `Trabalhador`).
- Unitário: upload de assinatura (`PUT /api/usuarios/me/assinatura`) grava no `Usuario` certo;
  rejeita content-type fora de imagem.
- Unitário: renderização do PDF usa a imagem quando `DocumentoSignatario.UsuarioId` tem assinatura
  cadastrada, cai para texto quando não tem ou quando o método é Biometria/ReconhecimentoFacial.
- Regressão: build limpo depois de remover toda a árvore de Telegram (nenhuma referência órfã).

## 13. Fora de escopo (não mexer nesta rodada)

- Assinatura para trabalhadores de campo sem login (continuam por biometria/facial).
- Mudar `ResponsavelExecucaoUsuarioId` da PT para um ato de assinatura próprio.
- Reaproveitar a assinatura desenhada como imagem de treino para o reconhecimento facial (Azure
  Face API) — são capacidades independentes.
