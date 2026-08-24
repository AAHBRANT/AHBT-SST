# ANÁLISE DE OPORTUNIDADES — HUB GÊNESIS SST
`ANALISE_OPORTUNIDADES.md` | Diagnóstico e recomendações de agregação de valor para Claude Code

Documento gerado a partir da análise cruzada de `PROJECT RULES.md`, `Base de Conhecimento — Projeto de Aplicação e Controle de SST.md` e `DESING SYSTEM AAHBRANT.md`.

Data da análise: 2026-08-19

---

## 1. Diagnóstico

O projeto está em estágio de **especificação conceitual madura**: a Base de Conhecimento cobre 50 seções do domínio de SST com profundidade técnica e normativa consistente, a arquitetura de infraestrutura já está decidida (Teams-first, Azure, Microsoft Graph API) e existe um design system com tokens de cor e tipografia definidos.

Não existe, até o momento, código de aplicação — apenas regras, tokens visuais e um mockup estático (`MODELO VISUAL APENAS PARA NOÇÃO NAO SERVE DE REGRA.html`).

### 1.1 Pontos fortes já definidos

- Modelo de domínio completo: **Legislação → Requisitos → Riscos → Controles → Pessoas → Atividades → Evidências → Inspeções → Não Conformidades → Ações → Indicadores**.
- Arquitetura de infraestrutura decidida (Teams App nativo, Azure SQL/Cosmos DB, App Service/Functions, Adaptive Cards, Microsoft Graph).
- Design system com tokens de cor e tipografia consistentes (`#7B1E2B` como cor institucional primária).

### 1.2 Lacuna central

Falta a ponte entre **"o que o sistema deve saber"** (Base de Conhecimento) e **"como o sistema vai ser construído"**. Hoje existe uma lacuna em três frentes: arquitetura de dados, proteção de dados sensíveis (LGPD) e priorização de execução.

---

## 2. Recomendações — Nível 1: Crítico (resolver antes de codar)

| Item | Justificativa |
| :--- | :--- |
| **Modelo de dados (ERD)** | A Base de Conhecimento descreve entidades e relações em texto (trabalhador, ASO, treinamento, EPI, risco, etc.), mas não existe um schema formal. Sem isso, cada módulo tende a ser codado com estruturas inconsistentes. |
| **Adequação à LGPD** | O sistema armazenará CPF, dados de saúde (ASO, exames) e assinaturas — **dado pessoal sensível de saúde** pela LGPD. Exige base legal específica, criptografia em repouso, controle de acesso granular e política de retenção. Não há menção a isso em nenhum dos documentos atuais. É o maior risco jurídico do projeto. |
| **Matriz de permissões (RBAC)** | A seção 44 da Base de Conhecimento lista 12 perfis de acesso, mas não define o que cada perfil pode ver/editar por módulo. Sem essa matriz, o SSO via Entra ID não tem regra de autorização para aplicar. |

---

## 3. Recomendações — Nível 2: Alto valor, rápido de agregar

- **Motor de bloqueio preventivo como serviço central**, não como regra isolada por módulo (seção 45 da Base de Conhecimento). Se cada módulo (altura, espaço confinado, elétrica) reimplementar sua própria checagem de "apto + treinado + autorizado", a lógica diverge com o tempo. Recomenda-se uma função única de "elegibilidade" reutilizada por todos os módulos.
- **Escalonamento automático de alertas**: hoje o sistema apenas "gera alerta" (seção 34). Sem regra de escalonamento (ex.: gestor não trata em 48h → escala para diretor/CIPA), alertas críticos se perdem na rotina — padrão de falha comum em sistemas de SST.
- **Portal self-service do trabalhador** (Personal Tab do Teams): consulta pelo próprio trabalhador de seus documentos, validade de ASO/treinamento e EPIs recebidos. Reduz carga operacional do time de SST e aumenta adesão.
- **Trilha de auditoria imutável**: para que evidências de acidentes/CATs tenham valor probatório (inclusive judicial), o log de evidências (seção 37) deveria ser *append-only*, com hash de integridade por registro — não um campo opcional "quando necessário".

---

## 4. Recomendações — Nível 3: Diferenciais competitivos (médio prazo)

- **OCR + IA para validação cruzada automática**: já previsto no roadmap (seção 41), mas o ganho real está em cruzar automaticamente o certificado lido com o cadastro do trabalhador e sinalizar divergências (nome, CA de EPI vencido, carga horária insuficiente para a NR) — não apenas extrair texto.
- **Mapa de calor de risco por obra/setor** no dashboard executivo — hoje os indicadores são numéricos (seção 35); uma visualização geográfica/planta baixa muda a qualidade da tomada de decisão da diretoria.
- **Portal de terceiros/subcontratadas** como app ou aba pública, permitindo que a empresa terceira se autoatenda no envio de documentos antes de liberar acesso à obra (seção 30) — reduz gargalo manual do time de SST.
- **Indicador "dias sem acidente" + gamificação leve** por obra/equipe — melhora cultura de segurança com baixo custo técnico.

---

## 5. Recomendações — Nível 4: Longo prazo

Itens já mapeados como evolução futura na Base de Conhecimento (seção 48), reordenados por retorno esperado:

1. **Assinatura digital** com valor jurídico nas evidências — antes do app mobile offline.
2. **Integração eSocial** (S-2210, S-2220, S-2240) — alto retorno de conformidade.
3. **App mobile offline** para inspeções de campo (canteiros com conectividade limitada).
4. **IoT/wearables** (detecção de queda, sensores de gases) — somente após o núcleo estar estável em produção.

---

## 6. Próximos passos sugeridos

A partir deste diagnóstico, os seguintes artefatos podem ser desenvolvidos em sequência:

1. Modelo de dados (ERD) detalhado a partir da Base de Conhecimento.
2. Matriz de permissões por perfil e módulo (RBAC).
3. Roadmap de execução em fases (MVP → V2 → V3) com estimativa de esforço.
4. Nota técnica de adequação à LGPD para dados de saúde e dados pessoais.

**Recomendação de prioridade:** iniciar pelo item de LGPD, por ser o maior risco jurídico identificado — antes de qualquer estrutura de dados que já contemple ASO ou CPF ser implementada.
