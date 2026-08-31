# 📑 Estrutura de Módulos de SST — Sistema de Gestão Integrado (SGI)

---

## 🟢 PR-SST-001 — Gestão de Equipamentos de Proteção Individual (EPI)

### 1. Controles e Registros Principais
* **Matriz de EPI por Função:** Define os EPIs necessários e obrigatórios para cada função mapeada na empresa.
* **Ficha de EPI (Individual):** Registro do histórico de entregas ao colaborador.
  * *Campos obrigatórios:* EPI, Número do CA, Data de entrega, Substituição e Termo de devolução.
* **Controle de Estoque de EPI:** Gestão do almoxarifado/obra.
  * *Campos obrigatórios:* EPI, Fabricante/Modelo, CA, Validade do CA, Quantidade em estoque, Entradas, Saídas e Saldo atual.

### 2. Campo: Fonte da Necessidade
Coluna obrigatória na matriz corporativa para rastreabilidade legal/operacional:
* PGR
* PCMSO
* NRs (NR-01, NR-05, NR-06, NR-10, NR-11, NR-12, NR-18, NR-33, NR-35)
* Requisito Ambiental / Requisito de Qualidade / Requisito do Cliente
* Procedimento Interno / Necessidade Identificada pela Empresa

---

## 🟡 PR-SST-002 — Gestão de Treinamentos

### 1. Matriz de Treinamentos por Função/Atividade
* **Campos:** Função | Atividade | Treinamento | Área Responsável | Base Legal/Origem | Carga Horária | Periodicidade | Aplicável à Obra? (Qual) | Observações

### 2. Controle de Treinamentos e Validades
* **Campos:** Colaborador | Função | Obra | Treinamento | Data de Realização | Validade | Situação | Certificado Anexado

### 3. Registro do Treinamento (Turmas)
* **Campos:** Lista de Presença | Conteúdo Programático | Instrutor | Data | Carga Horária | Avaliação (quando aplicável)

### 4. Certificados e Evidências
* **Armazenamento:** Certificado digitalizado | Avaliações aplicadas | Registros fotográficos/evidências

---

## 🔴 PR-SST-003 — Gestão de Saúde Ocupacional

### 1. PCMSO (Documento Externo/Legal)
* **Controle de Vigência:** PCMSO Vigente | Médico Responsável | Período de Vigência | Unidades/Obras Abrangidas | Funções Contempladas | Riscos Considerados | Exames Previstos | Periodicidades.
* *Nota:* Tratar como documento externo controlado.

### 2. Controle Unificado de ASO
* **Tipos de Exame:** Admissional, Periódico, Retorno ao Trabalho, Mudança de Risco Ocupacional, Demissional.
* **Tabela Central:** Colaborador | Função | Obra | Tipo de ASO | Data | Resultado | Status
* *Acesso:* Prontuário e ASO completo com acesso restrito (LGPD/Médico).

### 3. Exames Ocupacionais Complementares
* **Exames Monitorados:** Audiometria, Acuidade Visual, Espirometria, Laboratoriais, Avaliação Clínica e Exames Específicos.

### 4. Controle de Validade (Painel Visual)
* 🟢 **Vigente**
* 🟡 **Próximo do Vencimento**
* 🔴 **Vencido**

### 5. Aptidão para Atividades Específicas
* Controle das aptidões para atividades críticas (ex: Trabalho em Altura, Espaço Confinado), sem criar junta médica paralela (critério 100% do médico do trabalho).

### 6. Relatório Analítico do PCMSO & Ações
* **Acompanhamento:** Existência, Período, Emissão, Análise de Dados e Encaminhamento de Ações de Saúde.
* **Plano de Ação:** Necessidade Identificada → Ação Recomendada → Responsável → Prazo → Evidência.

---

## 🔵 PR-SST-004 — Gestão de Riscos Ocupacionais (GRO / PGR)

### 1. Inventário de Riscos Ocupacionais
* **Campos:** Atividade | Processo | Função | Perigo | Fonte/Circunstância | Possível Consequência | Grupo Expôsto | Avaliação de Risco (PxS) | Medidas Existentes | Medidas Adicionais.

### 2. Plano de Ação
* **Estrutura:** Risco/Problema | Medida | Responsável | Prazo | Situação

### 3. Metodologia e Identificação de Perigos
* Padronização corporativa da matriz de risco (Probabilidade x Severidade, Nível de Risco e Criterio de Aceitabilidade).
* Abrangência: Máquinas, Instalações, Agentes (Físicos, Químicos, Biológicos), Ergonomia, Acidentes e Emergências.

### 4. Hierarquia das Medidas de Prevenção
1. Eliminação do Perigo
2. Proteção Coletiva (EPC)
3. Medidas Administrativas / Organização do Trabalho
4. Equipamento de Proteção Individual (EPI)

### 5. Gestão de Mudanças e Controle por Obra
* Gatilhos para atualização do PGR: Novas obras, novos equipamentos, mudanças de processo, acidentes graves, etc.
* Matriz de Gestão: Obra | PGR | Responsável | Data | Revisão | Situação.

### 6. Integração Sistêmica
* **PGR → Treinamentos:** Riscos específicos geram matriz de treinamento.
* **PGR → EPI:** Risco residual define especificação de EPI.
* **PGR → PCMSO:** Inventário de riscos alimenta o programa médico.
* **PGR → Emergência / Inspeções / Indicadores:** Alimenta respostas a emergências e rotinas de fiscalização.

---

## 🟠 PR-SST-005 — Gestão de Inspeções de Segurança

### Tipos de Inspeção
1. **Inspeção de Rotina:** Organização, limpeza, sinalização, EPI/EPC, instalações elétricas, escavações, trabalho em altura.
2. **Inspeções Específicas:** Andaimes, máquinas/equipamentos, içamento de carga, espaço confinado, alojamentos.
3. **Inspeção de Pré-Uso:** Verificação diária/prévia de ferramentas, máquinas e sistemas.
4. **Inspeção Comportamental:** Observação e orientação de comportamento seguro (Opcional).
5. **Inspeções Extraordinárias:** Acionadas pós-acidentes, novos riscos ou alterações operacionais.

---

## 🟣 PR-SST-007 — Gestão da CIPA

### Estrutura do Módulo
1. **Dimensionamento:** Enquadramento, número de membros, representantes do empregador e empregados.
2. **Processo Eleitoral:** Convocação, inscrição, eleição, apuração e ata de divulgação.
3. **Designação:** Indicação de Presidente/Vice-Presidente e membros nomeados/eleitos.
4. **Treinamento & Reuniões:** Capacitação da comissão e Controle de Reuniões/Ações:
   * *Campos:* Data | Obra | Reunião | Tema/Problema | Ação | Responsável.
5. **SIPAT & Gestão Documental:** Organização do evento anual e repositório de atas, editais e listas de presença.

---

## 🔴 PR-SST-008 — Gestão de Acidentes, Incidentes e Ocorrências

### 1. Comunicação e Registro
* Canal padrão para acidentes (com/sem lesão), quase-acidentes e condições inseguras.
* **Registro de Ocorrência:** Data/Hora | Obra | Local | Trabalhador | Descrição | Testemunhas | Lesão/Atendimento | Classificação.

### 2. Atendimento Legal e Investigação de Causa Raiz
* **CAT:** Controle rigoroso de prazos e emissão legal.
* **Análise de Causa:** Foco no sistema (ambiente, método, equipamento, barreiras falhas, causas básicas e imediatas).

### 3. Plano de Ação e Indicadores
* Integração: Ocorrência grave → Revisão imediata do PGR.
* **Métricas Principais:** Taxa de Frequência, Taxa de Gravidade, Dias Perdidos, % de Ações Concluídas no Prazo.

---

## 🛠️ PR-SST-009 — Análise Preliminar de Riscos (APR) e Permissão de Trabalho (PT)

### Fluxo Operacional