---
name: Engenheiro da assinatura - verificação diária
agent: ./agent.md
environment_id: ./environment.yaml
# Cofre "Engenheiro da assinatura G-SST" (claude-lock.json).
vault_ids: [vlt_011ChTeT3EsxDCxnwYs3qVcg]
schedule:
  type: cron
  expression: "30 6 * * *"
  timezone: America/Sao_Paulo
# Teto por execução. Uma verificação sem alerta custa bem menos; o teto só segura um caso fora do normal.
budget:
  type: limit
  max_list_cost: {amount: "300", currency: USD}
---

Faça a verificação da assinatura agora: chame o relatório de saúde, trate os alertas conforme as suas regras e mande o resumo no Telegram.
