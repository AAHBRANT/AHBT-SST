# Motor de Assinatura Eletrônica + Biometria — Diagnóstico e Plano

Documento de arquitetura interno (não é documento controlado do SGI). Registra o
levantamento técnico feito antes de implementar o motor centralizado de
assinatura eletrônica — com autenticação híbrida (biometria digital via leitor
físico por obra como método principal, crachá NFC/QR + PIN como reserva
automática, e WebAuthn por celular próprio como método opcional por obra) e
trilha de evidências —, que entra primeiro no módulo DDS e depois é
reaproveitado por Treinamento, EPI, APR, PT, Inspeções etc.

Status: **FASE 1 (diagnóstico) e FASE 2 (modelo de dados) concluídas. Demais
fases da implementação ainda não iniciadas.**

> **Revisão 2026-08-25 (v2)**: decisão final do cliente é **biometria digital
> (impressão digital) como método principal**, via leitor físico compartilhado
> por obra — não celular pessoal do trabalhador. Crachá (NFC/QR) + PIN vira
> **método de reserva**, usado quando o leitor estiver indisponível
> (falha/manutenção/energia/rede) ou em obras que ainda não receberam o
> equipamento. WebAuthn por celular próprio segue **opcional**, para quem
> tiver o recurso. Ver seções 1.6, 2, 3 e 4 (Validade jurídica e LGPD).

## 1. Diagnóstico da arquitetura atual

### 1.1 Tecnologias

- .NET 8, Clean Architecture: `AAHBRANT.SST.Domain`, `AAHBRANT.SST.Application`,
  `AAHBRANT.SST.Infrastructure`, `AAHBRANT.SST.Api`, `AAHBRANT.SST.Worker`.
- EF Core 8 + SQL Server, um `DbContext` (`SstDbContext`), configurações via
  Fluent API (`IEntityTypeConfiguration<T>` por entidade em
  `Infrastructure/Persistencia/Configuracoes/`).
- CQRS com **MediatR** — Command/Validator/Handler no mesmo arquivo, por
  módulo, em `Application/<Modulo>/{Commands,Queries}/`.
- **FluentValidation** (`AbstractValidator<TCommand>`), executado
  automaticamente via `ValidationBehavior<,>` no pipeline do MediatR.
- **QuestPDF** (licença Community) já em uso para geração de PDF (ex.:
  `Infrastructure/Documentos/DdsPdfService.cs`), com a cor institucional
  `#670000` já aplicada — é o padrão visual a seguir no PDF assinado.
- Frontend: React + Vite + Fluent UI, `HashRouter`, rodando como aba do Teams
  (`sst-web-hml...azurecontainerapps.io`).
- Autenticação: JWT via Entra ID / Teams SSO (`Microsoft.Identity.Web`), mas
  **desligada por padrão** se `AzureAd:TenantId` estiver vazio — precisa
  confirmar no Azure Portal se está preenchida em homologação.
- Testes: xUnit, cobertura ainda muito baixa (só há um teste real de negócio,
  `CpfValidadorTests`).

### 1.2 Tabelas/entidades relevantes

- `Dds`, `DdsParticipante` (grava foto como `varbinary(max)` direto no banco —
  decisão consciente já tomada no projeto; **não existe Azure Blob Storage em
  uso em nenhum lugar do sistema hoje**).
- `Trabalhador` (CPF criptografado AES-256-GCM + hash HMAC-SHA256), `Usuario`
  (`AzureAdObjectId?` **opcional** — trabalhador de campo pode não ter conta
  Entra ID), `Obra`.
- **`TrilhaAuditoria`** (`Domain/Entidades/Evidencia.cs`) — já existe, já é
  **append-only com cadeia de hash** (`HashRegistroAnterior`/
  `HashRegistroAtual`). É exatamente o mecanismo de integridade que o motor de
  assinatura precisa — não deve ser duplicado.
- `AprAssinatura` — já existe algo chamado "assinatura", mas o próprio código
  documenta que é só confirmação simples de ciência, sem prova criptográfica.
  Serve de referência de nomenclatura, não de base técnica.

### 1.3 API/rotas do DDS (referência de convenção)

Rota base `/api/dds`, protegida por policy dinâmica `"modulo:acao"` (RBAC via
`PermissaoAuthorizationHandler` — **no-op se Entra ID estiver desligado**).
Upload de foto usa `IFormFile` clássico, grava bytes direto no banco.

### 1.4 QR Code / identificação pública

Já existe infraestrutura de rota pública anônima reaproveitável: `GET
/sst/p/{uid}` (`IdentificacaoPublicaController`, `[AllowAnonymous]`) e uma
página fora do `AppShell` (`AreaPublicaPage.tsx`) para quem não está logado.
**Não existe geração de imagem de QR em lugar nenhum do projeto** — precisa
entrar uma lib nova.

### 1.5 Riscos técnicos identificados

1. Nenhuma lib de WebAuthn no projeto — seria adicionada (`Fido2NetLib`) **se**
   o método WebAuthn for habilitado em alguma obra (ver 1.6 — deixou de ser o
   método principal).
2. Trabalhador de campo pode não ter `Usuario`/conta Entra ID. **Não é um
   problema**: nenhum dos métodos de autenticação propostos depende do Entra
   ID — todos ficam vinculados direto a `TrabalhadorId`.
3. ~~Cada trabalhador assina no próprio celular~~ — **premissa descartada**,
   ver 1.6. Não é realista supor smartphone + biometria + internet +
   disponibilidade de uso pessoal para toda a força de trabalho de obra.
4. **Divergência de schema em `RowVersion` (pré-existente, não introduzida
   pelo Motor de Assinatura, descoberta durante o teste ponta a ponta da
   etapa 13 em 2026-08-25)**: no banco de desenvolvimento local, a coluna
   `RowVersion` está fisicamente tipada como `timestamp`/`rowversion` em pelo
   menos 60 das 67 tabelas do banco, enquanto todas as migrations do EF Core
   (incluindo `InitialCreate`) e o modelo atual a descrevem como
   `varbinary(max)`. Sem `.IsRowVersion()` na configuração da entidade, o EF
   Core tenta gerar um INSERT explícito nessa coluna, e o SQL Server rejeita
   com `Cannot insert an explicit value into a timestamp column` — quebrando
   qualquer criação de registro para a tabela afetada. Corrigido, um a um
   conforme encontrados (sem migration, sem alterar as demais tabelas, para
   não arriscar dados de desenvolvimento já existentes), para `Permissao`
   (`AcessoConfiguracoes.cs`), `Funcao` e `Trabalhador`
   (`OrganizacaoConfiguracoes.cs`). **Risco em aberto**: qualquer outra
   tabela ainda não exercida por um fluxo de criação pode apresentar o mesmo
   erro; recomenda-se um levantamento dedicado (`sys.columns`/`sys.types`) e
   uma correção única e revisada, em vez de continuar corrigindo tabela por
   tabela conforme aparecem. **Atualização 2026-08-26**: banco de homologação
   (Azure SQL `AAHBRANT.SST.Hml`) foi testado ao vivo — as duas migrations do
   Motor de Assinatura e o RBAC seeder rodaram sem esse erro, ou seja, o
   schema de lá **não** tem o mesmo desvio físico do dev local. O risco segue
   em aberto só para o ambiente de desenvolvimento local.
5. **[RESOLVIDO em 2026-08-26] Cache de build do ACR ignorando `--build-arg`
   do frontend**: ao publicar a imagem `sst-web` no Azure Container Registry
   via `az acr build`, a camada Docker do `RUN npm run build` foi reaproveitada
   do cache mesmo passando um `--build-arg VITE_API_BASE_URL` diferente do
   build anterior — o bundle final ficou com a URL da API vazia, e a tela
   caía em erro `Unexpected token '<'` (a chamada `/api/...` virava relativa
   ao próprio host estático, que devolve o `index.html` da SPA). Confirmado
   comparando o hash de um build local (`vite build` com a env var correta,
   fora do Docker) contra o bundle publicado — divergiam. Corrigido
   adicionando `ARG CACHEBUST` no `Dockerfile.web` logo antes do
   `RUN npm run build`, forçando invalidação de cache em todo redeploy do
   frontend (commit `1ef2538`). **Recomendação para próximos redeploys do
   frontend**: sempre passar um valor novo de `--build-arg CACHEBUST=<algo
   único>` junto do `az acr build`, e depois de atualizar o Container App,
   conferir que o nome do arquivo `assets/index-*.js` servido mudou — se
   ficar igual ao anterior, o cache não invalidou e a imagem antiga ainda
   está no ar (mesmo que `az containerapp update` reporte sucesso).

### 1.6 Reavaliação do método de identificação/autenticação

Levantamento adicional no código confirmou que **a infraestrutura de crachá já
existe, parcialmente pronta**, no módulo de Identificação (NTAG/QR):

- `TagIdentificacao` (`Domain/Entidades/Identificacao/TagIdentificacao.cs`) é
  **polimórfica** (`EntidadeVinculadaTipo` + `EntidadeVinculadaId`) e **já**
  suporta `TipoEntidadeVinculada.Trabalhador` (valor `3` do enum, ao lado de
  `Area` e `Ativo`).
- `TipoTag` já modela `Ntag215`, `Ntag213`, `QrCode` e `Rfid` como o **mesmo
  tipo de entidade** — crachá NFC e crachá QR de um trabalhador são a mesma
  tabela, só muda o campo `Tipo`.
- Já existe uma query de resolução, `ResolverTagPorUidQuery`
  (`Application/TagsIdentificacao/Queries/ResolverTagPorUidQuery.cs`), que
  recebe um `Uid` de tag e já resolve para `Trabalhador` quando aplicável —
  hoje usada só na tela administrativa de cadastro de tags
  (`TagsIdentificacaoTab.tsx`), **não** num fluxo de autenticação (não há
  checagem de PIN nem uso público/quiosque).

O que falta, e será construído nesta feature:
- Campo de PIN pessoal no `Trabalhador` (hash, no mesmo padrão de
  `CpfHash` — nunca PIN em texto puro) — usado no método de **reserva**.
- Um endpoint de **autenticação de quiosque** (Uid da tag + PIN → identifica o
  trabalhador para fins de assinatura), separado do endpoint administrativo
  existente, que não tem esse gate de segurança.
- Integração com **leitor biométrico físico compartilhado por obra** —
  método **principal**. Ver detalhamento técnico e ressalva de hardware na
  seção 3.
- Estratégia opcional de WebAuthn por celular próprio, para quem tiver o
  recurso — plugável atrás da mesma abstração.

> **Nota — pesquisa de hardware pendente**: ainda não temos um modelo
> comercial de leitor biométrico confirmado que (a) seja compatível com
> FIDO2/WebAuthn (para não expor o template da digital ao backend) e (b)
> suporte cadastro de ~100 digitais por equipamento (uma obra típica). Isso
> precisa ser levantado com fornecedores antes da compra do hardware —
> **PROVISÓRIO**, não assumir um modelo específico até confirmar.

## 2. Decisões assumidas para desbloquear a implementação

| Decisão | O que foi assumido | Por quê |
|---|---|---|
| Onde guardar PDF/evidências | `varbinary(max)` no SQL Server, igual à foto do DDS hoje | Reaproveita o padrão já existente em vez de introduzir Blob Storage |
| Trilha de eventos de assinatura | Escreve em cima da `TrilhaAuditoria` existente (já tem cadeia de hash) | Evita duplicar auditoria |
| Identidade do signatário | `TrabalhadorId`, independente de ter `Usuario`/Entra ID | Nenhum método exige Azure AD; nem todo trabalhador de campo tem conta corporativa |
| Método de autenticação principal | **Biometria digital (impressão)**, via leitor físico compartilhado por obra, compatível com FIDO2/WebAuthn — 1 equipamento por canteiro, não por trabalhador | Decisão do cliente (2026-08-25 v2); template da digital fica no hardware, nunca é enviado ao backend |
| Método de reserva | Crachá (NFC ou QR) + PIN pessoal, no mesmo tablet/quiosque | Usado quando o leitor biométrico estiver indisponível (falha, manutenção, energia, rede) ou em obra sem equipamento ainda; reaproveita `TagIdentificacao` já existente |
| PIN (reserva) | Numérico curto (4-6 dígitos), armazenado como hash (nunca texto puro), definido no cadastro do trabalhador ou autoatendimento supervisionado | Mesmo padrão de segurança já usado para CPF (`CpfHash`) |
| WebAuthn/biometria de celular | Método **opcional**, habilitável por obra, para quem tiver smartphone próprio | Reaproveita quem já tem o recurso, sem torná-lo obrigatório; tecnicamente é a mesma estratégia WebAuthn do leitor físico, só muda o dispositivo |
| Consentimento LGPD para biometria | Termo específico de consentimento para tratamento de dado biométrico (art. 5º, II e art. 11 LGPD), separado do termo de validade jurídica da assinatura | Biometria é dado pessoal sensível — exige base legal própria, não coberta pelo consentimento de assinatura eletrônica |

Qualquer uma dessas pode ser revista — foram assumidas para não travar o
projeto em uma segunda rodada de perguntas, e estão sinalizadas aqui para
validação.

## 3. Proposta de arquitetura

O `SignatureService` não muda — o que muda é que a autenticação passa a ser
plugável, com biometria física como principal e crachá+PIN como reserva
automática:

```
                 SignatureService
                        │
             IAutenticacaoAssinaturaService
                        │
     ┌──────────────────┴──────────────────┬──────────────────┐
     ↓                                      ↓                   ↓
 WebAuthn (FIDO2)                      CrachaPin/QrCodePin   WebAuthn (celular)
 leitor biométrico da obra ⭐ principal  crachá+PIN — reserva  opcional, por obra
     │                                      │                   │
     └──────────────────┬───────────────────┴──────────────────┘
                         ↓
               Assinatura registrada
                         ↓
              Auditoria + Hash + PDF + QR
```

**Insight técnico importante**: leitor biométrico da obra e celular próprio do
trabalhador usam o **mesmo protocolo** (WebAuthn/FIDO2) — a diferença é só o
tipo de authenticator: o leitor da obra é um *roaming authenticator* (USB/BLE,
compartilhado, com várias digitais cadastradas — *resident keys*), o celular é
um *platform authenticator* (Face/Touch ID, individual). Isso significa que
**não precisamos de uma estratégia `BiometriaFisica` separada** — é a mesma
`Fido2AutenticacaoStrategy`, só variando o dispositivo. Simplifica o desenho
anterior (que ainda tratava como 2 estratégias distintas).

`CrachaPin` e `QrCodePin` continuam existindo como **fallback automático**:
se o leitor biométrico não responder (offline, sem energia, sem digital
cadastrada para aquele Uid), a tela do quiosque cai para identificação por
crachá + PIN sem exigir intervenção manual. Ambas reaproveitam a mesma tabela
`TagIdentificacao` já existente (só muda o `TipoTag`).

```
Domain/Entidades/Assinatura/
  DocumentoAssinatura.cs       (Document)
  DocumentoSignatario.cs       (DocumentSigner)
  CredencialWebAuthn.cs        (credencial FIDO2 — vinculada ao leitor da obra OU ao celular do trabalhador)
  Trabalhador.PinHash          (novo campo — hash do PIN pessoal, usado no método de reserva)
  Trabalhador.TermoAceiteAssinaturaEletronicaEm (novo campo — data do aceite, ver seção 4)
  Trabalhador.ConsentimentoBiometriaEm          (novo campo — data do consentimento LGPD específico, ver seção 4)
  Obra.MetodosAutenticacaoHabilitados (novo campo — flags: BiometriaWebAuthn, CrachaPin, QrCodePin)

Application/Assinatura/
  Commands/CriarDocumentoAssinaturaCommand.cs
  Commands/IniciarAssinaturaWebAuthnCommand.cs  (gera challenge — leitor da obra ou celular)
  Commands/ConfirmarAutenticacaoWebAuthnCommand.cs
  Commands/AutenticarPorCrachaOuQrCommand.cs  (Uid da tag + PIN → identifica TrabalhadorId — fallback)
  Commands/RegistrarAssinaturaCommand.cs      (grava assinatura + evento + hash)
  Commands/FinalizarDocumentoCommand.cs       (gera PDF + QR + fecha hash final)
  Queries/ObterDocumentoQuery.cs
  Queries/VerificarIntegridadeQuery.cs
  IAutenticacaoAssinaturaService.cs  (abstração — WebAuthn como principal, Crachá/QR+PIN como fallback)
  IDocumentoPdfService.cs

Infrastructure/
  Assinatura/Fido2AutenticacaoStrategy.cs       (Fido2NetLib — leitor biométrico da obra e celular, mesma classe)
  Assinatura/CrachaPinAutenticacaoStrategy.cs   (fallback — reaproveita TagIdentificacao)
  Assinatura/QrCodePinAutenticacaoStrategy.cs   (fallback — idem, TipoTag.QrCode)
  Assinatura/QrCodeService.cs                   (QRCoder — geração do QR de validação do documento)
  Persistencia/Configuracoes/AssinaturaConfiguracoes.cs
  Migrations/AdicionarMotorAssinatura.cs

Api/Controllers/
  AssinaturaController.cs        (/api/documentos, /api/documentos/{id}/assinar, /api/documentos/{id}/autenticacao/*)
  ValidacaoPublicaController.cs  (/sst/validar/{token}, [AllowAnonymous], mesmo padrão do módulo NTAG)

TeamsApp/src/pages/dds/
  AssinarDdsPage.tsx  (tela de quiosque: solicita digital no leitor → se indisponível, cai para crachá/QR + PIN)
TeamsApp/src/pages/validacao/
  ValidarDocumentoPage.tsx (fora do AppShell, igual AreaPublicaPage)
```

Pacotes novos: `Fido2NetLib` (backend, autenticação biométrica — agora é
dependência do caminho principal, não mais opcional) e `QRCoder` (backend,
geração do QR do documento assinado).

**Ordem de entrega recomendada dentro do motor de autenticação**: como a
biometria depende de pesquisa/compra de hardware (nota da seção 1.6), vale
implementar `CrachaPin`/`QrCodePin` **primeiro**, tecnicamente — reaproveita
infraestrutura já existente e não tem dependência externa — e ativar como
método principal temporário até o leitor FIDO2 chegar; quando o hardware
estiver definido, a mesma tela já estará pronta para alternar a prioridade
via `Obra.MetodosAutenticacaoHabilitados`, sem retrabalho.

O DDS atual continua intacto: o novo fluxo entra como uma tela adicional
(`/prevencao/dds/:id/assinar`); o botão "Assinar DDS" convive com o registro
de participante por foto que já existe, não o substitui.

## 4. Validade jurídica e LGPD

Biometria e trilha de auditoria dão prova técnica forte de autoria e
integridade, mas **não bastam sozinhas** para validade jurídica — falta o
consentimento formal do trabalhador. Dois documentos distintos precisam
existir antes de um trabalhador assinar pela primeira vez:

1. **Termo de Aceite de Assinatura Eletrônica** — o trabalhador declara que
   aceita a identificação biométrica (ou crachá+PIN, no fallback) como sua
   assinatura eletrônica válida para documentos de SST, com base no
   art. 10, §2º da MP 2.200-2/2001 (o qual permite meios de comprovação de
   autoria fora do ICP-Brasil, desde que aceitos pelas partes).
2. **Consentimento específico para tratamento de dado biométrico** — exigido
   pela LGPD por se tratar de dado pessoal sensível (art. 5º, II e art. 11).
   Precisa deixar claro: que dado é coletado (só o template, nunca a imagem
   bruta, e o template não sai do leitor), finalidade, prazo de retenção, e
   que o trabalhador pode usar o método de reserva (crachá+PIN) se não quiser
   consentir com a biometria.

No modelo de dados, isso vira dois campos de data em `Trabalhador`
(`TermoAceiteAssinaturaEletronicaEm`, `ConsentimentoBiometriaEm`) —
o cadastro/edição de trabalhador passa a exigir essas confirmações antes de
habilitar a assinatura para aquela pessoa.

**Risco a sinalizar**: eu não sou advogado. O texto exato dos dois termos, e
a confirmação de que esse conjunto é defensável numa eventual ação
trabalhista ou fiscalização, precisam de revisão de um advogado
trabalhista/direito digital antes de qualquer coleta real de biometria em
produção. O que este documento resolve é o desenho técnico (quais campos,
quando exigir o aceite, como isso se conecta ao fluxo de assinatura) — não a
redação jurídica em si.

## 5. Ordem de implementação

1. ~~Diagnóstico da arquitetura atual~~ (concluído — este documento)
2. ~~Modelo de dados~~ (concluído 2026-08-25): entidades `DocumentoAssinatura` +
   `DocumentoSignatario` (`Domain/Entidades/Assinatura/DocumentoAssinatura.cs`),
   campos `PinHash`, `TermoAceiteAssinaturaEletronicaEm`,
   `ConsentimentoBiometriaEm` em `Trabalhador`, campo
   `MetodosAutenticacaoHabilitados` em `Obra`, campo `TrabalhadorId` (nullable)
   em `TrilhaAuditoria` (gap identificado durante a implementação: a trilha só
   suportava autor via `Usuario`, mas trabalhador que assina por crachá/
   biometria normalmente não tem conta `Usuario`), helper `PinHasher`
   (Infrastructure — PBKDF2-SHA256 com salt por trabalhador, não o mesmo
   padrão de `CpfCriptografiaConversor.CalcularHash`: PIN de 4-6 dígitos tem
   entropia baixa demais para HMAC simples). Migration
   `20260825185208_AdicionarMotorAssinatura`, build da solution verificado.
3. ~~`SignatureService` central (Application) e abstração
   `IAutenticacaoAssinaturaService`~~ (concluído 2026-08-25):
   `Application/Assinatura/IAutenticacaoAssinaturaService.cs` — interface com
   um único método (`AutenticarPorCrachaOuQrAsync`) e o record
   `ResultadoAutenticacaoAssinatura`. Deliberadamente não generalizada para
   cobrir o futuro fluxo WebAuthn/FIDO2 (etapa 13), que precisa de um
   desafio/resposta em duas chamadas — cada estratégia ganha seu contrato
   quando chegar a vez dela.
4. ~~Estratégia `CrachaPin`/`QrCodePin`~~ (concluído 2026-08-25):
   `Infrastructure/Assinatura/CrachaPinAutenticacaoStrategy.cs` — reaproveita
   `TagIdentificacao` (mesma resolução por `Uid` de `ResolverTagPorUidQuery`,
   agora com gate de PIN via `PinHasher.Verificar`), valida
   `TagIdentificacao.Status == Vinculada` +
   `EntidadeVinculadaTipo == Trabalhador`, e checa
   `Obra.MetodosAutenticacaoHabilitados` (cada obra decide quais métodos
   aceita, §2/§3) antes de autenticar. Falhas de regra usam
   `InvalidOperationException` (não `UnauthorizedAccessException` — o
   `TratamentoDeExcecaoMiddleware` não tem handler para 401, cairia no 500
   genérico). Comando `AutenticarPorCrachaOuQrCommand` (CQRS,
   `Application/Assinatura/Commands/`) expõe a estratégia via MediatR.
   Registrado em `Infrastructure/DependencyInjection.cs`. Rodou como
   principal temporário até o leitor biométrico ser definido/comprado.
   Build da solution verificado (0 erros). Ainda não expostos: controller de
   API e integração com a tela do DDS — ficam para a etapa 6.
5. ~~Fluxo de aceite/consentimento no cadastro do trabalhador~~ (concluído
   2026-08-25): três comandos CQRS novos em `Application/Trabalhadores/Commands/`
   (mesmo módulo de `GerarVinculoTelegramCommand`, não `Application/Assinatura/` —
   são ações sobre o cadastro do trabalhador, não sobre uma transação de
   assinatura): `DefinirPinAssinaturaCommand` (define/troca o PIN do método de
   reserva — sem isso ninguém consegue assinar por crachá+PIN, gap que não
   tinha sido coberto nas etapas 2-4), `RegistrarTermoAceiteAssinaturaCommand`
   e `RegistrarConsentimentoBiometriaCommand` — **dois comandos separados de
   propósito**, não um único "aceitar tudo": LGPD exige consentimento
   específico para dado biométrico (art. 5º II/art. 11), então o trabalhador
   pode aceitar a assinatura eletrônica e mesmo assim recusar biometria,
   ficando só no crachá+PIN. Nova abstração `IPinHasher`
   (`Application/Common/Interfaces/`) + `PinHasherService`
   (`Infrastructure/Seguranca/`, wrapper do `PinHasher` estático) para a
   Application poder hashear o PIN sem violar a regra de dependência do
   Clean Architecture. `CrachaPinAutenticacaoStrategy` ganhou o gate: agora
   exige `TermoAceiteAssinaturaEletronicaEm != null` antes de autenticar
   (não checa `ConsentimentoBiometriaEm` — esse é específico da futura
   estratégia biométrica). Três endpoints novos em
   `Api/Controllers/TrabalhadoresController.cs`
   (`POST /api/trabalhadores/{id}/assinatura/pin|termo-aceite|consentimento-biometria`),
   atrás de uma única permissão nova `trabalhador:assinatura` (seed em
   `RbacSeeder.cs`) — resolvida em runtime pelo
   `PermissaoAuthorizationPolicyProvider` já existente, sem precisar registrar
   a policy em nenhum outro lugar. Build da solution verificado (0 erros).
   Nenhuma UI (React) foi criada — os três termos ainda não têm texto jurídico
   definitivo (ver risco sinalizado na seção 4); a tela de cadastro do
   trabalhador só deve chamar estes endpoints depois que o texto for revisado
   por advogado.
6. ~~Integração com o fluxo do DDS (tela de quiosque `AssinarDdsPage.tsx`)~~
   (concluído 2026-08-25): três peças novas em `Application/Assinatura/` —
   `Commands/CriarDocumentoAssinaturaCommand.cs` (idempotente: se já existe um
   `DocumentoAssinatura` `EmAndamento` para a `EntidadeTipo`/`EntidadeId`
   informada, devolve o mesmo Id em vez de duplicar),
   `Queries/ObterDocumentoQuery.cs` (busca pela entidade de origem, não pelo Id
   do documento — é assim que a tela descobre se já existe um documento aberto
   para o DDS sem guardar `DocumentoAssinaturaId` em nenhum lugar do módulo
   Dds) e `Commands/RegistrarAssinaturaCommand.cs` (autentica via
   `IAutenticacaoAssinaturaService` + grava `DocumentoSignatario`;
   deliberadamente **sem** hash/evento de auditoria — ficam para as etapas 7/8,
   que acontecem na finalização do documento, um momento diferente do ciclo de
   vida). Novo `Api/Controllers/AssinaturaController.cs`, genérico
   (`/api/documentos`, `/api/documentos/{id}/assinar`), atrás de duas
   permissões novas `assinatura:ver`/`assinatura:assinar` (seed em
   `RbacSeeder.cs`, módulo próprio em vez de reaproveitar `dds:conduzir`,
   porque o controller não é específico do DDS). No frontend,
   `TeamsApp/src/pages/dds/AssinarDdsPage.tsx` (rota
   `/prevencao/dds/:id/assinar`) — tela de quiosque com dois campos (crachá/QR
   lido por leitor USB-wedge, depois PIN) e lista de quem já assinou; o botão
   "Assinar DDS" foi adicionado em `DdsDetalhePage.tsx` **ao lado** do fluxo
   existente de registro de participante por foto, sem alterá-lo. Build da
   solution (0 erros) e `tsc --noEmit` do TeamsApp (0 erros) verificados; não
   houve verificação end-to-end no navegador (exigiria banco de dados
   provisionado e autenticação Teams SSO, fora do escopo desta etapa).
7. ~~Eventos de auditoria (reaproveitando `TrilhaAuditoria`)~~ (concluído
   2026-08-25): `TrilhaAuditoria` era só esqueleto até aqui — só existia a
   entidade e `ListarTrilhaAuditoriaQuery` (leitura), nenhum handler gravava
   nela. Nova abstração `IAuditoriaService`
   (`Application/Common/Interfaces/IAuditoriaService.cs`, mesmo padrão de
   `IPinHasher` — Application não pode calcular hash/acessar o "último
   registro" diretamente) implementada por
   `Infrastructure/Auditoria/AuditoriaService.cs`: cada chamada calcula
   `HashRegistroAtual` = SHA-256 de
   `HashRegistroAnterior|Acao|EntidadeTipo|EntidadeId|UsuarioId|TrabalhadorId|Timestamp|DadosDepoisJson`
   (primeiro registro usa a constante `"GENESIS"` como hash anterior) — cadeia
   estilo blockchain, sem chave (o objetivo é evidenciar adulteração, não
   confidencialidade). `RegistrarAsync` só dá `Add`, não salva sozinho — o
   handler chamador grava tudo (evento de auditoria + mudança de negócio) em
   um único `SaveChangesAsync`, mesma transação. `RegistrarAssinaturaCommand`
   agora chama `_auditoria.RegistrarAsync("Assinatura.Registrada", ...)` após
   gravar o `DocumentoSignatario`, usando `EntidadeTipo`/`EntidadeId` do
   documento de origem (ex.: `"Dds"`/Id do DDS, não o Id interno do
   `DocumentoAssinatura`) para que a trilha seja consultável pela entidade de
   negócio real. Registrado em `Infrastructure/DependencyInjection.cs`
   (`AddScoped<IAuditoriaService, AuditoriaService>`). **Limitação conhecida e
   aceita**: a leitura do último registro + gravação do novo não é atômica
   (sem lock/transação `SERIALIZABLE`), então duas assinaturas concorrentes em
   documentos diferentes podem ler o mesmo `HashRegistroAnterior` e bifurcar a
   cadeia — aceitável para o volume de uso atual (documentado no código); se
   isso virar requisito forte, a correção é usar transação serializável ou um
   contador dedicado. Build da solution verificado (0 erros); sem mudança de
   frontend nesta etapa, então não houve verificação de UI.
8. ~~Hash do documento (`generateDocumentHash` / `verifyDocumentIntegrity`)~~
   (concluído 2026-08-25): campos `ConteudoHash`/`FinalizadoEm`/`PdfConteudo`/
   `TokenValidacaoPublica` já existiam em `DocumentoAssinatura` desde a etapa 2
   (não precisou de migration nova). Novo helper estático
   `Application/Assinatura/HashConteudoDocumentoCalculador.cs` — SHA-256 puro
   (sem chave) sobre `EntidadeTipo|EntidadeId|` + lista ordenada de
   signatários (`TrabalhadorId|MetodoAutenticacao|AssinadoEm` de cada um);
   fica em Application, não Infrastructure (ao contrário de
   `IAuditoriaService`/`IPinHasher`), porque é primitivo puro do BCL sem
   dependência de configuração/segredo. **Não confundir com o hash da etapa
   7**: `TrilhaAuditoria.HashRegistroAtual` é por evento e encadeado
   (blockchain-style); `DocumentoAssinatura.ConteudoHash` é um hash único
   sobre o conteúdo final do documento inteiro, calculado uma vez na
   finalização. Novo `Application/Assinatura/Commands/FinalizarDocumentoCommand.cs`:
   carrega o documento, exige `Status == EmAndamento` (senão
   `InvalidOperationException` — "já foi finalizado ou cancelado") e pelo
   menos 1 signatário (senão `InvalidOperationException` — "sem nenhuma
   assinatura não pode ser finalizado"), calcula o hash, marca
   `Status = Finalizado` + `FinalizadoEm` + `ConteudoHash`, registra evento
   `"Documento.Finalizado"` via `IAuditoriaService` (com `usuarioId`/
   `trabalhadorId` nulos — não há `ICurrentUserService` no projeto hoje para
   capturar quem disparou a finalização, avaliar quando o botão existir no
   frontend) e salva tudo em um único `SaveChangesAsync`. Novo
   `Application/Assinatura/Queries/VerificarIntegridadeQuery.cs`: recalcula o
   hash com o mesmo helper e compara com `ConteudoHash` armazenado
   (`VerificacaoIntegridadeDto.Integro`); exige documento já `Finalizado`
   (senão `InvalidOperationException`). Nova permissão `assinatura:finalizar`
   (seed em `RbacSeeder.cs`, distinta de `assinatura:assinar` porque
   finalizar é uma ação mais consequente — fecha o documento). Dois
   endpoints novos em `AssinaturaController.cs`:
   `POST /api/documentos/{id}/finalizar` (policy `assinatura:finalizar`) e
   `GET /api/documentos/{id}/integridade` (policy `assinatura:ver`, é
   leitura). `ObterDocumentoQuery`/`DocumentoAssinaturaDto` passaram a expor
   `ConteudoHash`/`FinalizadoEm`. Deliberadamente **não** preenchidos nesta
   etapa: `PdfConteudo`/`TokenValidacaoPublica` (ficam para as etapas 9-11).
   **Sem UI de frontend nesta etapa** — finalizar um documento sem PDF para
   baixar (que só chega na etapa 9) tem valor prático limitado, então o
   gatilho de UI faz mais sentido junto da geração do PDF. Build da solution
   verificado (0 erros).
9. ~~Geração de PDF final (QuestPDF, reaproveitando `DdsPdfService` como
   referência)~~ (concluído 2026-08-25): novo
   `Application/Assinatura/IDocumentoAssinaturaPdfService.cs` +
   `Infrastructure/Assinatura/DocumentoAssinaturaPdfService.cs` — mesmo
   branding AAHBRANT (`#670000`) e biblioteca (QuestPDF) de
   `DdsPdfService`, mas layout próprio: **não é** reprodução do conteúdo do
   documento de origem (o DDS já tem seu PDF via `IDdsPdfService`/
   `ExportarDdsPdfQuery`) — é um comprovante genérico de assinatura (quem
   assinou, por qual método, quando, e o hash de integridade), coerente com
   o motor ser desacoplado de cada módulo (só conhece `EntidadeTipo`/
   `EntidadeId`, não o conteúdo de negócio). `FinalizarDocumentoCommand`
   passou a gerar o PDF e gravar em `documento.PdfConteudo` no mesmo
   `SaveChangesAsync` da finalização (não virou comando novo, conforme
   planejado na etapa 8). Novo
   `Application/Assinatura/Queries/ObterPdfDocumentoQuery.cs` — bytes crus
   à parte de `DocumentoAssinaturaDto` (que ganhou só um `bool TemPdf`) para
   não serializar o PDF inteiro no polling de status do quiosque. Novo
   endpoint `GET /api/documentos/{id}/pdf` (policy `assinatura:ver`),
   retorna `application/pdf` via `File()`. Registrado em DI
   (`AddScoped<IDocumentoAssinaturaPdfService, DocumentoAssinaturaPdfService>`).
   **Sem UI de frontend nesta etapa** (mesma decisão da etapa 8 — ainda não
   há botão de finalizar no `AssinarDdsPage.tsx`; ficará para quando o fluxo
   de finalização ganhar UI, fora do escopo desta etapa). Build da solution
   verificado (0 erros).
10. ~~QR Code (`QRCoder`)~~ (concluído 2026-08-25): pacote `QRCoder` 1.6.0
    adicionado à Infrastructure. Novo `Application/Assinatura/
    IQrCodeDocumentoService.cs` (contrato — recebe só o token, devolve
    `QrCodeDocumentoResultado(byte[] Png, string UrlValidacao)`, sem vazar a
    URL base do frontend para o Application) +
    `Infrastructure/Assinatura/QrCodeDocumentoService.cs` (implementação com
    `QRCoder.QRCodeGenerator`/`PngByteQRCode`, monta a URL a partir de
    `AssinaturaOptions.UrlBaseValidacaoPublica`, nova config em
    `appsettings.json`, reaproveitando o mesmo valor de `Cors:AllowedOrigin`
    hoje, mas como chave própria — pode divergir no futuro, ex. domínio
    customizado). Config vazia não derruba a finalização (mesmo espírito de
    tolerância usado em Telegram/Graph/ServiceBus): o QR cai para um caminho
    relativo. Novo `Application/Assinatura/TokenValidacaoPublicaGerador.cs`
    (32 chars hex via `RandomNumberGenerator`, cabe em
    `TokenValidacaoPublica` nvarchar(64) único). `FinalizarDocumentoCommand`
    estendido de novo (mesmo padrão das etapas 8/9): gera token, chama
    `IQrCodeDocumentoService`, grava `documento.TokenValidacaoPublica` e
    embute o QR (imagem + URL como texto de apoio) no rodapé do comprovante
    PDF via `DocumentoAssinaturaPdfModelo.QrCodePng`/`UrlValidacaoPublica`
    (parâmetros opcionais, trailing). `DocumentoAssinaturaDto`/
    `ObterDocumentoQuery` passaram a expor `TokenValidacaoPublica` (dado já
    público por natureza — vai impresso no PDF/QR — útil para o frontend
    montar o link mesmo antes da página pública existir). O QR aponta para
    `/#/validar/{token}` (prefixo `#/` obrigatório — ver correção registrada
    na etapa 11 abaixo), rota que só é implementada na etapa 11 — o link não
    resolvia nada ainda nesta etapa, mas nenhum PDF precisará ser regenerado
    quando a página existir. **Sem UI de frontend nesta etapa** (mesma decisão
    das etapas 8/9). Build da solution verificado (0 erros).
11. ~~Página pública de validação~~ (concluída 2026-08-25): **correção sobre a
    etapa 10** — ao ler `App.tsx` para desenhar a rota, percebi que o
    TeamsApp usa `HashRouter` (evita depender de rota configurada no servidor
    durante o sideload no Teams), então a rota navegável real é
    `/#/validar/{token}`, não `/sst/validar/{token}` como a etapa 10 tinha
    gerado. `QrCodeDocumentoService.cs` foi corrigido para montar a URL com o
    prefixo `#/` antes que qualquer PDF real fosse validado com o link
    quebrado (nenhuma migração de dados necessária — o token em si não muda,
    só a URL montada a partir dele). Novo
    `Application/Assinatura/Queries/ResolverDocumentoPublicoQuery.cs` —
    resolve por `TokenValidacaoPublica` (só documentos com
    `Status == Finalizado`), devolve `DocumentoPublicoDto` deliberadamente
    sem `DocumentoAssinaturaId`/`EntidadeId` (conforme o comentário em
    `DocumentoAssinatura.cs`: "nunca expor Id/EntidadeId/dado pessoal na
    página pública"), só `EntidadeTipo`, `FinalizadoEm`, `ConteudoHash` e a
    lista de signatários (nome, método, data — sem `TrabalhadorId`). Novo
    `Api/Controllers/ValidacaoPublicaController.cs`
    (`[AllowAnonymous] [Route("sst/validar")]`), mesmo padrão de
    `IdentificacaoPublicaController.cs` (módulo NTAG). Novo
    `TeamsApp/src/pages/validacao/ValidarDocumentoPage.tsx`, mesmo padrão de
    `AreaPublicaPage.tsx` (card centralizado, fora do `AppShell`, estados de
    carregando/não encontrado/válido). Rota `/validar/:token` registrada em
    `App.tsx` como irmã de `/p/:codigoOuUid`, fora do `LayoutComTeams`. Novo
    método `api.validacaoPublica.resolver(token)` em `api.ts`. Build da
    solution e `tsc --noEmit` do TeamsApp verificados (0 erros).
12. ~~Painel administrativo de assinaturas~~ — **concluído em 2026-08-25.** Nova aba
    "Assinaturas" dentro de `AdministracaoPage.tsx` (padrão "IA consolidada":
    funcionalidade nova vira aba, não item novo de sidebar), ao lado de
    "Controle de Acesso" e "Trilha de Auditoria". Novo
    `Application/Assinatura/Queries/ListarDocumentosAssinaturaQuery.cs` —
    filtros opcionais `EntidadeTipo`/`Status`/`DataInicio`/`DataFim` (filtro
    de data usa `CreatedAtUtc`, não `FinalizadoEm`, pois documentos
    `EmAndamento`/`Cancelado` não têm `FinalizadoEm`), devolve
    `DocumentoAssinaturaResumoDto` — ao contrário do DTO da página pública
    (item 11), este **inclui** `Id`/`EntidadeId` porque o consumidor já é
    autenticado/autorizado via `assinatura:ver` e precisa dos IDs para agir
    (baixar PDF, copiar link). Novo endpoint
    `GET /api/documentos/listar` em `AssinaturaController.cs`, sub-rota
    distinta do `[HttpGet]` raiz (que já é usado por `Obter` com
    `entidadeTipo`/`entidadeId` obrigatórios), mesma policy `assinatura:ver`
    (sem nova permissão RBAC). Novo
    `TeamsApp/src/pages/administracao/PainelAssinaturasTab.tsx`, mesmo
    template de `TrilhaAuditoriaTab.tsx` (filtro por tipo/data + tabela);
    ações por linha: baixar PDF (mesmo padrão blob-download de
    `DdsDetalhePage.tsx`, quando `TemPdf`) e copiar link público
    `/#/validar/{token}` via `navigator.clipboard` (quando
    `TokenValidacaoPublica` presente). Novos `api.assinatura.listar(filtros)`
    e `api.assinatura.baixarPdf(id)`, e `statusDocumentoAssinaturaLabel` em
    `api.ts` (mapa que faltava, análogo ao já existente
    `metodoAutenticacaoAssinaturaLabel`). Build da solution e
    `tsc --noEmit` do TeamsApp verificados (0 erros). **Sem verificação
    end-to-end no navegador** (exigiria banco provisionado com documentos já
    criados/finalizados).
13. Estratégia `Fido2AutenticacaoStrategy` (`Fido2NetLib`) — leitor biométrico
    da obra (principal, após hardware definido — ver nota da seção 1.6) e
    celular próprio (opcional, por obra). Backend e frontend **concluídos e
    testados de ponta a ponta em 2026-08-25**: cadeia completa Termo de
    Aceite → Consentimento de Biometria → PIN → início/confirmação do
    cadastro WebAuthn (`AssinaturaController.cs`), exercida via chamadas
    diretas à API (Swagger) e depois pela UI real (`AssinaturaTab.tsx`, aba
    "Assinatura" do perfil do trabalhador), confirmando que o botão
    "Cadastrar leitor da obra" dispara corretamente `navigator.credentials
    .create()` com o desafio retornado pelo backend. **Ainda sem hardware
    FIDO2 físico confirmado** (nota da seção 1.6 continua "PROVISÓRIO"), então
    a ceremônia em si (resposta de um autenticador real) não pôde ser
    validada — apenas o encadeamento até a chamada ao navegador. Não retomar
    o teste com hardware real sem confirmação explícita de modelo/fornecedor
    do leitor. Para viabilizar esse teste local, foi adicionado um bloco
    `Fido2` **somente em `appsettings.Development.json`**
    (`ServerDomain: "localhost"`, `Origins` apontando para as portas locais
    da API/TeamsApp) — `Fido2Options.ServerDomain`/`Origins` seguem
    propositalmente vazios em `appsettings.json` (produção), pelo mesmo
    padrão já usado em `GraphOptions`/`TelegramOptions`, até o domínio de
    produção e o hardware do leitor serem confirmados. **Não copiar esse
    bloco para produção.**
14. ~~Preparação para reuso em Treinamento/EPI/APR/PT/Inspeções~~ —
    **concluído em 2026-08-25** (parcialmente, ver escopo abaixo). O backend
    já era genérico desde a etapa 6 (`EntidadeTipo`/`EntidadeId` em
    `DocumentoAssinatura`), então não houve trabalho de backend nesta etapa.
    No frontend, a lógica de quiosque (leitura de crachá/QR + PIN, tabela de
    assinaturas registradas) foi extraída de `AssinarDdsPage.tsx` para um
    novo componente reutilizável
    `TeamsApp/src/components/assinatura/AssinaturaQuiosque.tsx` (primeiro
    arquivo em `components/` fora de `components/dashboard/`), parametrizado
    por `entidadeTipo`/`entidadeId`. `AssinarDdsPage.tsx` foi refatorado para
    consumi-lo, mantendo só o que é específico do DDS (cabeçalho com
    tópico/obra/data, rota de volta `/prevencao/dds/:id`). Para plugar em um
    novo módulo, basta renderizar
    `<AssinaturaQuiosque entidadeTipo="..." entidadeId={id} />` dentro da
    página de detalhe do módulo. **Escopo desta etapa foi só a extração do
    bloco reutilizável** ("preparação para reuso", não integração completa)
    — não foi adicionada UI de "Assinar" em Treinamento, EPI, APR, PT ou
    Inspeções, porque: (a) `Treinamento` tem modelo de dados por
    trabalhador/curso (um registro = uma conclusão), diferente do modelo de
    documento compartilhado com múltiplos signatários do DDS, exigindo
    decisão de UX própria; (b) o módulo EPI ainda não existe como entidade no
    domínio; (c) `AprAssinatura` (em `Apr.cs`) é só referência de
    nomenclatura, não base técnica (confirmação simples de ciência, sem
    prova criptográfica) — integrar exigiria desenho específico também.
    Build da solution não foi necessário (mudança só de frontend);
    `tsc --noEmit` do TeamsApp verificado (0 erros); app testado no
    navegador (build/runtime sem erros de console/servidor). **Sem
    verificação end-to-end do fluxo de assinatura no navegador** (exige
    login via Teams SSO + DDS já provisionado no banco — mesma limitação já
    registrada no item 12).

## 6. Referências

- [`docs/ERD.md`](ERD.md)
- [`docs/RBAC-Matrix.md`](RBAC-Matrix.md)
