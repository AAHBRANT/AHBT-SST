# Banco de Ideias e Evolução do Produto — integração com Telegram

> Especificação do usuário de 08/10/2026. Esta página registra **o que foi implementado**, **como configurar o
> Telegram** e **as decisões que não vieram literalmente da especificação** (para validação).

## 1. Fluxo

```
Usuário → Telegram → webhook da API → estruturação (heurística) → Banco de Ideias (SQL Server)
        → análise (negócio + tecnologia) → decisão do gestor → requisito → demanda → desenvolvimento → teste → implantação
```

O Telegram é só o canal de captura. A fonte oficial é o banco da plataforma (tabelas `Ideias`, `IdeiaComentarios`,
`IdeiaHistoricos`, `IdeiaAnexos`, `IdeiaRequisitos`, `DemandasDesenvolvimento`). A mensagem original fica em
`Ideias.MensagemOriginal` e nunca é alterada.

## 2. O que existe

| Especificação | Onde |
|---|---|
| §2–4 registro e confirmação pelo Telegram, pergunta objetiva | `TelegramIdeiasController` (webhook), `ProcessarMensagemTelegramIdeiaCommand` |
| §3 estruturação pela IA | `IIdeiaEstruturacaoService` → `HeuristicaIdeiaEstruturacaoService` (**ver §5, limitação**) |
| §5 campos principais | entidade `Ideia` |
| §6 fluxo de status | `FluxoStatusIdeia` (transições permitidas, justificativa obrigatória para Adiada/Descartada) |
| §7 análise / §9 pontuação | tela de detalhe, aba Análise; `PontuacaoIdeia` |
| §8 apoio da IA à análise | `GET /api/ideias/{id}/sugestoes` (semelhantes, perguntas, requisito e critérios sugeridos) |
| §10–11 requisito → demanda | `CriarRequisitoIdeiaCommand`, `AprovarRequisitoIdeiaCommand`, `CriarDemandaDesenvolvimentoCommand` |
| §12 comentários e anexos | `ComentarIdeiaCommand`, `AnexarArquivoIdeiaCommand` (até 5 MB por arquivo) |
| §13 histórico | `IdeiaHistorico` (só INSERT) |
| §14 painel e filtros / §15 pesquisa | `/ideias` (pesquisa cobre ideias, requisitos e demandas) |
| §16 ideias semelhantes | `SimilaridadeIdeias` (coeficiente de Dice sobre palavras relevantes, limiar 0,45) |
| §17 permissões | `ideia:usar`, `ideia:analisar`, `ideia:decidir`, `ideia:desenvolver` |

## 3. Hospedagem separada (decisão de 08/10/2026)

O Banco de Ideias roda em um **Container App próprio**, com a **mesma imagem** da API de SST (`sst-api`) e o **mesmo banco**.
Quem escolhe o que cada app expõe é a variável `Hospedagem__Modo`:

| Modo | Expõe | Migrations/seeders |
|---|---|---|
| `Completo` (padrão, se a variável não existir) | tudo, inclusive o Banco de Ideias — comportamento anterior | sim |
| `Sst` | tudo **menos** o Banco de Ideias e o webhook do Telegram | sim |
| `Ideias` | **só** o Banco de Ideias e o webhook do Telegram | não (a API de SST continua aplicando) |

### Variáveis em cada app

**`sst-api-hml`** (API de SST): `Hospedagem__Modo=Sst`. **Não** recebe `Telegram__IdeiasChatIds` nem `Telegram__IdeiasWebhookSecret`.
Mantém o `Telegram__BotToken` do Suporte IA, se existir.

**Novo app do Banco de Ideias** (sugestão de nome: `sst-ideias-hml`), mesma imagem da API:
- `Hospedagem__Modo=Ideias`
- `Telegram__BotToken` (segredo, token do @IdeiasAHBT_bot), `Telegram__IdeiasChatIds`, `Telegram__IdeiasWebhookSecret` (segredos)
- as **mesmas** de conexão e identidade da API de SST: `ConnectionStrings__DefaultConnection`, `AzureAd__*`, `Lgpd__*` (as 3 chaves,
  obrigatórias na inicialização), `Cors__AllowedOrigin` (origem do `sst-web-hml`) — **os segredos dos Container Apps não sincronizam
  sozinhos**, copie da `sst-api-hml`.

### Frontend e deploy
- Build do web com `VITE_IDEIAS_API_BASE_URL=https://<fqdn do sst-ideias-hml>`; sem ela, o web usa a API principal.
- `deploy.yml`: variável opcional `CONTAINERAPP_IDEIAS` (nome do app novo); quando existe, o deploy atualiza esse app com a mesma imagem.
- Ordem segura: (1) subir o app novo com `Ideias`; (2) buildar o web com `VITE_IDEIAS_API_BASE_URL`; (3) só então colocar `Sst` na `sst-api-hml`.

## 4. Configuração do Telegram (feita pela Tecnologia)

Só funciona depois destes passos — **sem eles o webhook rejeita tudo** (falha fechada).

1. **Bot**: pode ser o mesmo já usado pelo Suporte IA (`Telegram:BotToken`). No BotFather, em *Bot Settings → Group Privacy*,
   **desative a privacidade** (`/setprivacy` → *Disable*); senão, num grupo, o bot só enxerga comandos e respostas.
2. **Grupo**: crie o grupo de ideias e adicione o bot. Descubra o `chat_id` do grupo (número negativo).
3. **Configuração da API do Banco de Ideias** (segredos do Container App `sst-ideias-hml`, ver §3):
   - `Telegram__IdeiasChatIds` = `chat_id` do grupo (vários separados por vírgula);
   - `Telegram__IdeiasWebhookSecret` = texto aleatório longo (letras, números, `_` e `-`).
4. **Registrar o webhook** (uma vez; o Telegram só aceita um webhook por bot, e a URL precisa ser HTTPS pública):
   ```bash
   curl "https://api.telegram.org/bot<TOKEN>/setWebhook" \
     -d url=https://<URL-DA-API>/api/telegram/ideias \
     -d secret_token=<Telegram__IdeiasWebhookSecret> \
     -d 'allowed_updates=["message"]'
   ```
5. Teste: escreva no grupo uma ideia com mais de 15 caracteres. O bot deve responder `Ideia registrada!` com o ID.
   Comandos: `/ajuda` e `/status IDEIA-0001`.

Perguntas do bot (módulo ou ideia semelhante) são respondidas **respondendo à mensagem do bot** (use "Responder" no Telegram).
Foto ou arquivo enviado **com legenda** vira uma ideia nova com anexo; **sem legenda**, respondendo à mensagem da ideia, é só anexado.

## 5. Permissões (§17)

| Papel da especificação | Permissão | Pode |
|---|---|---|
| Usuário | `ideia:usar` (todo usuário autenticado) | registrar, consultar, comentar, anexar |
| Analista | `ideia:analisar` | analisar, complementar, criar requisito, vincular duplicadas, mover para Em análise / Aguardando decisão |
| Gestor | `ideia:decidir` | aprovar, adiar, descartar, priorizar, aprovar requisito, criar demanda |
| Tecnologia | `ideia:desenvolver` | atualizar demandas e levar a ideia para Em desenvolvimento, Em teste e Implantada |
| Administrador | todas | acesso completo (concedido pelo servidor ao perfil Administrador) |

Marque as três últimas na tela **Administração → Controle de Acesso** para os perfis desejados — a matriz continua
sendo preenchida à mão, como no resto do sistema.

## 6. Decisões minhas e limitações (validar)

- **"IA" hoje é heurística local**, não um modelo de linguagem: palavras-chave para módulo/categoria, título = primeira frase,
  `Problema`, `Objetivo` e `Benefício` ficam em branco para a equipe preencher. Não há chamada a nenhum provedor de LLM.
  A interface `IIdeiaEstruturacaoService` está pronta para uma implementação com Claude (trocar o registro em
  `Application/DependencyInjection.cs`); isso exige definir chave de API, custo e política de dados — **decisão pendente**.
- **Visibilidade**: toda pessoa autenticada vê todas as ideias (a especificação diz "às quais possui acesso"; a regra de
  acesso por ideia não foi definida). Isso também permite o aviso de duplicidade.
- **Pontuação** (0–100) = `75% × ganho + 25% × (1 − custo)`; ganho = (3·Impacto + 2·Urgência + 3·Valor) ÷ 8; custo = média de
  Esforço e Complexidade. P1 ≥ 67, P2 ≥ 40, P3 abaixo disso. Fórmula e cortes são **propostas**, ajustáveis em `PontuacaoIdeia`.
- **Duplicidade**: vincular não descarta nem apaga; a ideia continua no banco apontando para a principal e some só da contagem
  do painel.
- **Usuário do Telegram não é mapeado a usuário do sistema**: o registro guarda o nome exibido no Telegram.
- Anexos ficam no próprio banco (varbinary), limite de 5 MB; não há armazenamento de blobs para isso no projeto.
- Não há notificação ao gestor (Teams/Alertas) quando uma ideia chega ao status *Aguardando decisão* — não estava na especificação.
- Telegram (`Telegram:IdeiasChatIds`) é a única barreira de quem pode registrar por lá; qualquer membro do grupo registra.

## 7. Verificação feita

- `dotnet build` da API sem erros; 677 testes passando (18 novos em `tests/.../Ideias`).
- Migration `CriarBancoDeIdeias` gerada pelo EF (só cria as 6 tabelas novas).
- Telas navegadas no navegador com API simulada (lista, filtros, detalhe, sugestões, discussão).
- **Não verificado**: execução contra SQL Server real e chamada real ao Telegram (sem banco nem bot neste ambiente).
  Antes de usar: aplicar a migration em homologação e rodar o passo a passo das §3 e §4.
