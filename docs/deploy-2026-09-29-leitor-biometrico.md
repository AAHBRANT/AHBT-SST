# Resumo de deploy — leitor de digital, assinatura digital/facial e DDS (29/09/2026)

Base atual em hml/`origin/master`: `90546e1` (feat(fotos): exigir metadados de captura…).
Este pacote: `master` local, **20 commits à frente de `90546e1`** (os dois últimos são este documento), ~95 arquivos
(+40 mil linhas; o volume vem principalmente dos snapshots do EF). Confira com `git log --oneline 90546e1..HEAD`.

## ⚠ Leia antes de dar o push

**`git push` na `master` dispara o deploy automático das 3 imagens em hml** (`.github/workflows/deploy.yml`:
API, Worker e Web; tag = hash completo do commit; um deploy por vez). Não existe "subir só uma parte": a
história é linear e cumulativa, então **todos os commits vão juntos**, inclusive o DDS (seleção de funcionários) que
ainda não estava em hml.

Antes de empurrar:

```bash
git fetch origin
git rev-list --left-right --count origin/master...master   # esperado: "0  20" (0 atrás; 20 à frente)
```

Se o primeiro número não for `0`, alguém empurrou algo novo — refaça a conta antes de subir. Se o segundo
número for maior que 20, entrou trabalho novo depois deste resumo: confira o que é antes de empurrar.

## O que muda para o usuário

| Área | Mudança |
|---|---|
| Assinatura | Digital e facial em **todas** as obras por padrão; botão de digital no mesmo padrão do facial; **bipe** ao aceitar assinatura/presença; **APR** ganha assinatura por digital/facial. |
| Cadastro da digital | Duas leituras do mesmo dedo que precisam coincidir; bipe avisa quando retirar o dedo. |
| Cadastro facial | Guia com botão OK antes da câmera; foto aprovada fica guardada no perfil (Cofre de Assinaturas). |
| Administração | Nova aba **Leitores de digital** (registrar, ver situação, revogar); em Obras › Editar, seção "Assinatura eletrônica". |
| DDS | Lista de funcionários da obra com seleção salva; presença por **digital ou facial** por linha. **Muda o fluxo atual do DDS** (o anterior era só digital, por participante). |
| Correções | Horário da assinatura aparecia 3 h adiantado; DDS não listava funcionários antigos (Situação inválida). |
| Novidades | Pop-up 5.34.0 com 8 itens, publicado automaticamente no start da API. |

## Migrations (aplicadas sozinhas no start da API, nesta ordem)

`Program.cs` executa `MigrateAsync()` na subida da API; não há passo manual de banco.

| # | Migration | Tipo | O que faz | Reversível? |
|---|---|---|---|---|
| 1 | `20260929105144_AdicionarSelecaoFuncionariosDds` | Schema | Cria `DdsFuncionariosSelecionados` (FK para `Dds` e `Trabalhadores`, índice único por DDS+trabalhador). | Sim (`Down` remove a tabela). |
| 2 | `20260929130758_HabilitarMetodosAssinaturaTodasObras` | **Dados** | `UPDATE Obras SET MetodosAutenticacaoHabilitados = 3 WHERE MetodosAutenticacaoHabilitados <> 3` — liga digital + facial em **todas** as obras existentes. | **Não** (`Down` vazio). Guarde o estado antes (abaixo). |
| 3 | `20260929131619_AdicionarFotosCadastroFacial` | Schema | Cria `FotosCadastroFacial` (foto `varbinary(max)`, hash SHA-256, FK para `Trabalhadores`). | Sim, mas apagar a tabela **perde as fotos**. |
| 4 | `20260929152542_CorrigirSituacaoTrabalhadorInvalida` | **Dados** | `UPDATE Trabalhadores SET Situacao = 1 WHERE Situacao = 0` — corrige o valor inválido criado pela migration de 09/09. | **Não** (`Down` vazio). Guarde os ids antes (abaixo). |

Só as migrations 2 e 4 alteram dado existente; 1 e 3 são aditivas. Como as duas tabelas são novas e os `UPDATE`
não quebram o código antigo, a versão anterior continua funcionando durante a troca de revisão.

### Por que a migration 4 é obrigatória
A migration de 09/09 criou `Trabalhadores.Situacao` com padrão 0, mas o enum começa em 1 (`Ativo`). Todo
trabalhador anterior a 09/09 ficou com 0, e a lista do DDS filtra `Situacao == Ativo`: **sem a migration 4, o DDS
não listaria os funcionários antigos**. (No banco de dev, os 200 do mock estavam assim.) Só o DDS filtra por
essa situação; desligamento continua valendo por `DataDemissao`/G-RH.

## Checklist pré-deploy

- [ ] **Ponto de restauração** do Azure SQL de hml (ou confirmar o retorno pontual no tempo — PITR — disponível).
- [ ] Guardar o estado das obras (a migration 2 não tem volta):
  ```sql
  SELECT Id, Codigo, MetodosAutenticacaoHabilitados FROM Obras;
  ```
- [ ] Guardar o que a migration 4 vai mudar:
  ```sql
  SELECT COUNT(*) AS Total FROM Trabalhadores WHERE Situacao = 0;
  SELECT Id, DataDemissao FROM Trabalhadores WHERE Situacao = 0;   -- guardar o resultado
  ```
- [ ] Confirmar no Container App `sst-api-hml` que já existe o segredo de `Lgpd:ChaveCriptografiaBiometriaBase64`
      (as digitais são cifradas com ele). **Não há segredo novo neste pacote.**
- [ ] Confirmar se o **Azure Face API** está liberado em hml (o pedido de acesso limitado da Microsoft estava em
      análise desde 04/09). Sem isso, o **cadastro e a assinatura facial falham** — a digital não depende dele.
- [ ] Decidir se o **cadastro de biometria de trabalhadores reais** só começa depois da revisão jurídica do termo
      (`docs/juridico/consentimento-biometria-rascunho.md`). Como toda obra passa a aceitar os dois métodos, os
      botões aparecem em todas as telas assim que o deploy sobe.

## Deploy (executado por você)

1. `git fetch` e a conferência do "0 20" acima.
2. `git push origin master` — dispara o workflow. Acompanhe: `gh run watch`.
3. Ao terminar, confirme os 3 Container Apps `Healthy/Running` (`sst-api-hml`, `sst-worker-hml`, `sst-web-hml`) com
   a imagem `:<hash do commit empurrado>`. Nomes conforme as notas de deploy do projeto (`rg-gnezis-hub-staging`,
   ACR `gnezishubstaging2342917073`) — confirme antes de usar.
4. A API aplica as 4 migrations no start e semeia a novidade 5.34.0.

O agente do leitor **não** faz parte do pipeline: é instalado à parte no PC da obra
(`docs/piloto-leitor-biometrico.md`).

## Verificação pós-deploy

```sql
-- 4 migrations novas no topo do histórico
SELECT TOP 5 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
-- esperado 0 nas duas:
SELECT COUNT(*) FROM Obras WHERE MetodosAutenticacaoHabilitados <> 3;
SELECT COUNT(*) FROM Trabalhadores WHERE Situacao = 0;
-- tabelas criadas:
SELECT name FROM sys.tables WHERE name IN ('FotosCadastroFacial','DdsFuncionariosSelecionados');
```

Testes rápidos na tela (hml):

| # | Onde | Esperado |
|---|---|---|
| 1 | Pop-up ao entrar | Aparece a novidade 5.34.0 (8 itens). |
| 2 | Administração › Leitores de digital | Aba abre, lista vazia, dá para registrar. |
| 3 | Administração › Obras › Editar | Seção "Assinatura eletrônica" com os dois métodos marcados. |
| 4 | DDS do dia | Lista os funcionários da obra (inclusive os antigos) com seleção e os dois botões por linha. |
| 5 | APR › aba Assinaturas | Botão "Assinar com digital ou facial" abre a tela de assinatura. |
| 6 | Perfil do trabalhador › Cofre de Assinaturas | "Facial Azure" abre o guia; sem leitor, "Capturar digital" fica habilitado mas o agente não responde (esperado até o piloto). |
| 7 | Qualquer assinatura já feita | Horário mostrado igual ao horário local. |

Os testes com o **leitor real** e o **site HTTPS falando com o agente (Chrome e Teams)** só acontecem no piloto.

## Rollback

- **Código:** recolocar a imagem anterior nos 3 apps (`sst-api`, `sst-worker`, `sst-web`) com a tag
  `90546e16d9072f73cbebc0e0584cba034bd6b623`:
  `az containerapp update -g <rg> -n <app> --image <acr>.azurecr.io/<imagem>:90546e16d9072f73cbebc0e0584cba034bd6b623`.
  As tabelas novas ficam no banco sem uso; o código antigo as ignora.
- **Dados (migrations 2 e 4):** não têm volta automática. Reverter = reaplicar o estado guardado no pré-deploy
  (ids de `Obras` e de `Trabalhadores` com `Situacao = 0`) ou restaurar o ponto de restauração do banco.
- **Não apagar** `FotosCadastroFacial` depois que houver cadastros reais (dado biométrico sensível; a perda é
  irreversível e a eliminação de dados biométricos precisa seguir o termo aprovado pelo jurídico).

## Riscos e pontos de atenção

1. **Migration 2 muda todas as obras**, inclusive as que alguém tinha deixado sem método de propósito. Dá para
   restringir depois em Obras › Editar; o estado anterior está no pré-deploy.
2. **Facial depende do Azure Face API** (acesso limitado); digital funciona sem ele.
3. **DDS muda de fluxo** para todos os usuários (seleção da lista antes da presença).
4. **LGPD:** dados biométricos sensíveis agora ficam no banco (template cifrado e fotos faciais). O texto do termo
   ainda precisa de revisão jurídica; ver o rascunho.
5. **Detecção de dedo falso** está **desligada** no agente (ligada recusou dedos verdadeiros no FS80H testado).
6. **Site HTTPS → agente local** (Chrome/Teams) nunca foi testado fora do ambiente de desenvolvimento.

## Testes executados antes do pacote

| Suíte | Resultado |
|---|---|
| Application | 483 aprovados |
| Infrastructure | 65 aprovados |
| Agente de biometria | 28 aprovados |
| Testes de tela do DDS (Playwright, câmera simulada) | 7 aprovados |
| Tipos do front (`tsc -b`) | sem erros |
| Verificação manual pela tela com o leitor real (cadastro em 2 leituras, quiosque da APR, presença do DDS) | aprovada, incluindo recusas |

**Não testado:** facial com câmera real; quiosques de treinamento, PT e EPI com o leitor real; Teams; deploy em hml.
