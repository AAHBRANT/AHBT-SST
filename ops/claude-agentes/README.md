# Agentes do Claude Console (Managed Agents)

Configuração versionada dos agentes que rodam na nuvem da Anthropic, sincronizada com o CLI `ant`
(`ant apply`). O `claude-lock.json` guarda os ids criados: **commitar sempre**, senão o próximo `ant apply`
cria duplicatas.

## Engenheiro da assinatura (`agents/engenheiro-assinatura/`)

Vigia a assinatura eletrônica (facial e digital) todo dia às 6h30 (Brasília) e quando a vigia do Worker
chama (`AgenteAssinatura:DeploymentId`). Usa as rotas internas `api/interno/assinatura/*` do G-SST e avisa
no Telegram do responsável. Ação irreversível só com "Aprovar" no Telegram (PRs #405 a #407).

| Arquivo | O que é |
|---|---|
| `agent.md` | Modelo, ferramentas e o prompt (corpo do arquivo) |
| `environment.yaml` | Sandbox com rede limitada (API do G-SST e MCP do GitHub) |
| `vault.yaml` | Cofre. As credenciais são cadastradas no Console, nunca aqui |
| `deployment-diario.md` | Agendamento `30 6 * * *` em `America/Sao_Paulo`, teto de US$ 3 por execução |

### Primeira vez

1. `ant apply --dry-run -v agents/engenheiro-assinatura/agent.md agents/engenheiro-assinatura/environment.yaml agents/engenheiro-assinatura/vault.yaml`
2. Conferir o plano e aplicar sem `--dry-run`.
3. Cadastrar as 2 credenciais do cofre com `agents/engenheiro-assinatura/cadastrar-credenciais.ps1` (o
   responsável roda: a chave vem do Azure, o token do GitHub da área de transferência; nada é impresso).
4. Copiar o id do cofre (`vlt_...`) do `claude-lock.json` para `vault_ids` em `deployment-diario.md` e aplicar o deployment.
5. Rodar uma vez na mão (`ant beta:deployments run --deployment-id ...`) e conferir a mensagem no Telegram.
6. Gravar o id do deployment (`depl_...`) em `AgenteAssinatura__DeploymentId` no `sst-worker-hml`.

Criado em 27/11/2026 no workspace Default: agente `agent_01L9rZxaaJgGgmeG6sN7V9dC`, cofre
`vlt_011ChTeT3EsxDCxnwYs3qVcg`, deployment `depl_01NNmnfjRbDmbAJruYd5kxMP`. Primeira execução: US$ 0,19,
sem alertas.

### Token do GitHub

Fine-grained, dono `AAHBRANT`, só o repositório `AHBT-SST`, Contents `Read-only`, validade de 90 dias.
Para trocar: apagar a credencial "GitHub AHBT-SST" no cofre e rodar `cadastrar-credenciais.ps1 -SoGitHub`.

### Para mudar

Editar o arquivo e rodar `ant apply` de novo, nomeando os arquivos. O agente ganha uma versão nova. O
deployment só passa a usá-la se for atualizado (`ant apply` do `deployment-diario.md`).
