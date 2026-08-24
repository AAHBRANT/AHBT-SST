# REGRAS DO PROJETO - HUB GÊNESIS SST (MICROSOFT TEAMS & AZURE)
`PROJECT_RULES.md` | Guia de Arquitetura e Regras de Negócio para Claude Code

---

## 1. Regras Fundamentais da Aplicação

1. **Nativo do Microsoft Teams (Teams-First):**
   * O aplicativo deve ser empacotado como **Teams App** (Personal Tab, Group/Channel Tab e Bot).
   * Interface otimizada para viewport do Teams e suporte automático aos temas (Light, Dark, High Contrast).
2. **Integração Completa de Notificações:**
   * Uso de **Adaptive Cards v1.5** para envio de alertas interativos em canais e chats 1:1.
   * Aprovações rápidas (ex: liberar Permissão de Trabalho - PT) feitas diretamente pelo card do Teams sem abrir a aba.
3. **Sincronização com Calendário do Teams (Microsoft Graph API):**
   * Agendamento automático de exames (ASOs), treinamentos (NRs) e inspeções de canteiro no calendário dos responsáveis.
4. **Infraestrutura Azure:**
   * Banco de dados relacional em **Azure SQL Database** ou **Azure Cosmos DB** (NoSQL).
   * Backend hospedado no **Azure App Service** ou **Azure Functions** (Serverless).

---

## 2. O Que Mais Você Pode Implementar? (Recursos Recomendados)

| Módulo / Recurso | Serviço / Tecnologia | Caso de Uso Prático |
| :--- | :--- | :--- |
| **Autenticação SSO** | Microsoft Entra ID (Azure AD) | Login único com a conta corporativa da empresa, vinculando perfis de acesso automaticamente. |
| **Visão Computacional (IA)** | Azure AI Vision / OCR | Leitura automática de comprovantes de ASO, Certificados de Treinamento e NRs enviados por foto/PDF. |
| **Bot Conversacional SST** | Azure Bot Service | Bot no Teams para relatar acidentes ("Quase-Acidentes"), consultar validade de EPIs ou abrir chamados por texto/voz. |
| **Armazenamento de Arquivos** | Azure Blob Storage | Guardar fotos de inspeção, laudos técnicos, PDFs do PGR e fichas de EPI assinadas. |
| **Relatórios Avançados** | Power BI Embedded | Incorporar dashboards analíticos avançados da diretoria direto nas abas do aplicativo. |
| **Automação de Fluxos** | Power Automate / Webhooks | Notificar o canal da CIPA sempre que uma Não Conformidade (NC) grave for registrada. |

---

## 3. Arquitetura das Integrações Microsoft Graph

* **Agendamentos de Calendário (`/me/events` ou `/users/{id}/events`):**
  * Toda inspeção ou ASO marcado deve gerar um evento de calendário com lembrete automático 48h antes.
* **Notificações via Bot (`/conversations`):**
  * Envio de mensagem direta ao trabalhador quando a validade do seu treinamento NR-35 ou ASO estiver a 30 dias de expirar.
* **Feed de Atividades do Teams (`/users/{id}/teamwork/sendActivityNotification`):**
  * Exibir notificações nativas no sininho de atividades do Teams para aprovações urgentes de PTs.

---

## 4. Regras de Código para o Claude Code

* **Padrão de API:** Desenvolver rotas REST/GraphQL seguras com validação JWT emitida pelo Entra ID.
* **Tratamento de Exceções:** Falhas no envio de notificações para o Teams devem ser registradas em fila no **Azure Service Bus** e reprocessadas sem travar a aplicação.
* **Estilo de UI:** Seguir rigorosamente o **`DESIGN_SYSTEM.md`** com o tom primário Burgundy (`#7B1E2B`), adaptando os cards para o padrão visual do Teams.