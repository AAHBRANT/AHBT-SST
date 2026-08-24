# BASE DE CONHECIMENTO — PROJETO DE APLICAÇÃO E CONTROLE DE SST

## 1. Objetivo

Este documento estabelece a base conceitual, normativa e funcional para o desenvolvimento de uma **aplicação de controle de Segurança e Saúde no Trabalho (SST)**.

A aplicação deverá transformar os requisitos legais e operacionais de SST em um sistema estruturado de **gestão, controle, rastreabilidade, prevenção, evidências e tomada de decisão**.

O sistema será concebido para aplicação em empresas de engenharia e construção civil, podendo ser adaptado posteriormente para outros setores.

O objetivo não é apenas armazenar documentos, mas permitir que a organização saiba:

- quais requisitos legais são aplicáveis;
- quais riscos existem;
- quais controles são necessários;
- quem está autorizado a executar cada atividade;
- quais treinamentos estão válidos;
- quais exames estão válidos;
- quais EPIs foram entregues;
- quais inspeções foram realizadas;
- quais não conformidades estão abertas;
- quais ações estão atrasadas;
- quais acidentes e incidentes ocorreram;
- quais evidências comprovam o cumprimento das obrigações;
- quais requisitos legais precisam ser revisados.

---

# 2. Conceito do sistema

O sistema deverá trabalhar com a seguinte lógica:

**Legislação → Requisitos → Riscos → Controles → Pessoas → Atividades → Evidências → Inspeções → Não Conformidades → Ações → Indicadores**

A aplicação deverá conectar essas informações.

Um requisito legal não deve existir isoladamente.

Exemplo:

**NR-35**
→ atividade em altura  
→ trabalhador autorizado  
→ capacitação válida  
→ aptidão médica  
→ análise de risco  
→ procedimento  
→ sistema de proteção  
→ inspeção  
→ execução  
→ evidência.

Dessa forma, o sistema deixa de ser um simples cadastro e passa a funcionar como uma plataforma de **gestão de conformidade e prevenção de riscos**.

---

# 3. Princípios do projeto

A aplicação deve seguir os seguintes princípios:

## 3.1 Prevenção

O sistema deve priorizar prevenção de acidentes e doenças ocupacionais.

## 3.2 Conformidade legal

Toda regra de controle deve possuir, sempre que aplicável, vínculo com sua fonte legal ou normativa.

## 3.3 Rastreabilidade

Toda informação importante deverá possuir:

- responsável;
- data;
- origem;
- validade;
- histórico;
- evidência;
- status.

## 3.4 Evidência

Sempre que possível, o sistema deverá registrar documentos, fotos, assinaturas, certificados, checklists, registros e outros comprovantes.

## 3.5 Gestão por risco

A aplicação deverá priorizar os riscos relevantes e suas medidas de prevenção.

## 3.6 Gestão por atividade

O controle deve ocorrer considerando as atividades efetivamente executadas, e não apenas cargos.

## 3.7 Controle por trabalhador

A aplicação deverá permitir verificar rapidamente se determinado trabalhador está apto, treinado, autorizado e documentado para determinada atividade.

## 3.8 Controle por obra

Cada empreendimento deverá possuir contexto próprio de riscos, trabalhadores, atividades, documentos e requisitos aplicáveis.

## 3.9 Histórico

Nenhum registro crítico deve ser simplesmente apagado sem manter histórico e rastreabilidade.

---

# 4. Base normativa

A aplicação deve considerar a legislação brasileira aplicável à SST e manter sua base normativa versionada.

Principais referências:

- Constituição Federal;
- CLT;
- Lei nº 6.514/1977;
- Portaria MTb nº 3.214/1978;
- Normas Regulamentadoras — NRs;
- legislação previdenciária aplicável;
- legislação relacionada a acidentes do trabalho;
- eSocial SST;
- normas técnicas aplicáveis;
- legislação estadual;
- legislação municipal;
- requisitos do Corpo de Bombeiros;
- requisitos contratuais de clientes;
- procedimentos internos da empresa.

A aplicação deverá permitir que novas alterações legislativas sejam incorporadas sem necessidade de reconstrução do sistema.

---

# 5. Normas Regulamentadoras prioritárias

Para uma empresa de engenharia e construção civil, a aplicação deverá inicialmente considerar:

- NR-1 — Disposições Gerais e Gerenciamento de Riscos Ocupacionais;
- NR-3 — Embargo e Interdição;
- NR-4 — SESMT;
- NR-5 — CIPA;
- NR-6 — EPI;
- NR-7 — PCMSO;
- NR-8 — Edificações;
- NR-9 — Avaliação e Controle das Exposições Ocupacionais;
- NR-10 — Segurança em Instalações e Serviços em Eletricidade;
- NR-11 — Transporte, Movimentação, Armazenagem e Manuseio de Materiais;
- NR-12 — Segurança no Trabalho em Máquinas e Equipamentos;
- NR-13 — Caldeiras, Vasos de Pressão, Tubulações e Tanques;
- NR-15 — Atividades e Operações Insalubres;
- NR-16 — Atividades e Operações Perigosas;
- NR-17 — Ergonomia;
- NR-18 — Segurança e Saúde no Trabalho na Indústria da Construção;
- NR-20 — Inflamáveis e Combustíveis;
- NR-23 — Proteção Contra Incêndios;
- NR-24 — Condições Sanitárias e de Conforto;
- NR-26 — Sinalização de Segurança;
- NR-28 — Fiscalização e Penalidades;
- NR-33 — Segurança e Saúde nos Trabalhos em Espaços Confinados;
- NR-35 — Trabalho em Altura.

Outras NRs deverão ser habilitadas conforme as atividades realizadas pela empresa.

---

# 6. NR-1 como eixo central

A aplicação deverá utilizar o **Gerenciamento de Riscos Ocupacionais — GRO** como uma das estruturas centrais da gestão.

O sistema deverá permitir:

- identificação de perigos;
- identificação de riscos;
- avaliação de riscos;
- classificação de riscos;
- definição de medidas preventivas;
- definição de responsáveis;
- acompanhamento das medidas;
- revisão periódica;
- geração e manutenção do inventário de riscos;
- planos de ação;
- evidências das medidas implantadas.

O PGR deverá ser tratado como resultado de um processo contínuo de gestão, e não apenas como documento estático.

---

# 7. Estrutura organizacional

O sistema deverá permitir múltiplos níveis:

**Empresa**
→ **Unidade**
→ **Obra/Contrato**
→ **Setor**
→ **Atividade**
→ **Equipe**
→ **Trabalhador**

Cada nível poderá possuir requisitos e controles específicos.

---

# 8. Cadastro de trabalhadores

Cada trabalhador deverá possuir um perfil completo.

Dados mínimos:

- nome;
- CPF;
- matrícula;
- empresa;
- função;
- setor;
- obra;
- data de admissão;
- situação contratual;
- vínculo;
- contato;
- responsável imediato.

Relacionamentos:

- exames;
- ASO;
- treinamentos;
- certificados;
- autorizações;
- EPIs;
- atividades autorizadas;
- restrições;
- inspeções;
- ocorrências;
- acidentes;
- documentos.

---

# 9. Controle de aptidão

O sistema deverá controlar a aptidão do trabalhador.

Deverá ser possível verificar:

**Apto / Apto com restrição / Inapto / Pendente**

O sistema deverá considerar:

- ASO;
- exame admissional;
- periódico;
- retorno ao trabalho;
- mudança de risco ocupacional;
- demissional;
- exames complementares;
- validade;
- restrições.

O sistema deverá alertar previamente sobre vencimentos.

---

# 10. Gestão de treinamentos

A aplicação deverá possuir um módulo de capacitação.

Cada treinamento deverá possuir:

- trabalhador;
- curso;
- norma relacionada;
- conteúdo;
- carga horária;
- instrutor;
- instituição;
- data;
- validade;
- certificado;
- evidência;
- status.

Exemplos:

- NR-10;
- NR-11;
- NR-12;
- NR-18;
- NR-33;
- NR-35;
- integração;
- trabalho em altura;
- espaço confinado;
- operação de equipamentos;
- procedimentos internos.

O sistema deverá identificar automaticamente trabalhadores com treinamento vencido ou próximo do vencimento.

---

# 11. Controle de autorizações

Treinamento não significa automaticamente autorização.

O sistema deverá permitir registrar:

**Treinamento válido + aptidão válida + requisitos adicionais + autorização**

Exemplos:

- trabalhador autorizado para trabalho em altura;
- trabalhador autorizado para eletricidade;
- operador autorizado;
- trabalhador autorizado para espaço confinado;
- vigia autorizado;
- profissional designado para atividades específicas.

---

# 12. Controle de EPI

O módulo de EPI deverá registrar:

- trabalhador;
- EPI;
- descrição;
- fabricante;
- modelo;
- CA;
- tamanho;
- quantidade;
- data de entrega;
- responsável;
- validade quando aplicável;
- substituição;
- devolução;
- motivo da substituição;
- evidência de entrega.

Deverá existir histórico completo de entrega e substituição.

---

# 13. Gestão de EPC

O sistema também deverá controlar equipamentos de proteção coletiva.

Exemplos:

- guarda-corpo;
- proteção de periferia;
- sinalização;
- isolamento de áreas;
- proteção de aberturas;
- sistemas de proteção contra quedas;
- barreiras;
- proteções de máquinas;
- dispositivos de segurança.

Cada EPC deverá possuir inspeções e evidências quando aplicável.

---

# 14. Gestão de riscos

A aplicação deverá possuir um módulo específico para gestão de riscos.

Estrutura:

**Atividade → Perigo → Evento perigoso → Consequência → Risco → Avaliação → Controle**

O cadastro deverá permitir:

- atividade;
- ambiente;
- perigo;
- agente;
- fonte;
- exposição;
- trabalhadores expostos;
- consequência;
- probabilidade;
- severidade;
- nível de risco;
- controles existentes;
- controles adicionais;
- responsável;
- prazo;
- status.

---

# 15. Inventário de riscos

O sistema deverá gerar e manter o inventário de riscos ocupacionais.

O inventário deverá permitir relacionamento com:

- obra;
- setor;
- atividade;
- função;
- trabalhador;
- perigo;
- risco;
- agente;
- controles;
- documentos;
- evidências.

A informação deve permanecer vinculada ao histórico de revisões.

---

# 16. PGR

O sistema deverá permitir estruturar os componentes relacionados ao PGR.

Exemplo:

**PGR**
- identificação da organização;
- caracterização das atividades;
- inventário de riscos;
- classificação dos riscos;
- medidas de prevenção;
- plano de ação;
- acompanhamento;
- revisão;
- evidências.

O sistema poderá futuramente gerar documentos automaticamente a partir dos dados cadastrados.

---

# 17. Análise de Risco / APR / AR

A aplicação deverá possuir módulo para criação e gestão de análises de risco.

Estrutura:

**Atividade**
→ etapas  
→ perigos  
→ riscos  
→ controles  
→ responsáveis  
→ aprovação.

Deverá permitir:

- identificação da atividade;
- local;
- equipe;
- data;
- validade;
- etapas;
- riscos;
- medidas preventivas;
- responsáveis;
- aprovação;
- assinatura;
- evidências.

---

# 18. Permissão de Trabalho

O sistema deverá permitir emissão e controle de PT quando exigida pelo procedimento ou requisito aplicável.

A PT deverá possuir:

- atividade;
- local;
- equipe;
- data;
- horário;
- validade;
- perigos;
- controles;
- requisitos;
- responsáveis;
- autorização;
- encerramento.

Deverá existir rastreabilidade de todas as PTs emitidas.

---

# 19. Trabalho em altura

Módulo específico para NR-35.

Deverá controlar:

- trabalhadores autorizados;
- capacitação;
- aptidão;
- análise de risco;
- planejamento;
- sistemas de proteção;
- equipamentos;
- inspeções;
- PT quando aplicável;
- emergência;
- resgate;
- evidências.

O sistema deverá impedir ou alertar sobre tentativa de liberação de atividade quando requisitos obrigatórios não estiverem atendidos.

---

# 20. Espaço confinado

Módulo específico para NR-33.

Deverá controlar:

- cadastro de espaços;
- trabalhadores autorizados;
- vigias;
- supervisores;
- treinamentos;
- aptidão;
- avaliação atmosférica;
- equipamentos;
- PET;
- monitoramento;
- comunicação;
- emergência;
- resgate;
- encerramento.

---

# 21. Segurança elétrica

Módulo relacionado à NR-10.

Deverá controlar:

- instalações;
- trabalhadores autorizados;
- treinamentos;
- documentos;
- procedimentos;
- inspeções;
- equipamentos;
- proteções;
- bloqueios;
- sinalização;
- análises de risco.

---

# 22. Máquinas e equipamentos

O sistema deverá possuir cadastro de máquinas e equipamentos.

Informações:

- equipamento;
- fabricante;
- modelo;
- patrimônio;
- localização;
- responsável;
- riscos;
- proteções;
- manutenção;
- inspeções;
- documentação;
- treinamento de operadores;
- autorização de operação;
- status.

---

# 23. Inspeções

O sistema deverá possuir um mecanismo de inspeções configuráveis.

Tipos:

- inspeção de obra;
- inspeção de canteiro;
- inspeção de EPI;
- inspeção de EPC;
- inspeção de máquinas;
- inspeção de ferramentas;
- inspeção de andaimes;
- inspeção de escadas;
- inspeção elétrica;
- inspeção de altura;
- inspeção de espaço confinado;
- inspeção comportamental;
- inspeção de terceiros.

Cada inspeção deverá gerar evidência e, quando necessário, uma não conformidade.

---

# 24. Checklists

Os checklists deverão ser parametrizáveis.

Cada item poderá possuir:

- conforme;
- não conforme;
- não aplicável;
- observação;
- fotografia;
- responsável;
- prazo;
- evidência.

O sistema deverá permitir diferentes versões do checklist.

---

# 25. Não conformidades

Toda não conformidade deverá possuir:

- origem;
- requisito relacionado;
- descrição;
- local;
- atividade;
- risco;
- evidência;
- responsável;
- prazo;
- ação corretiva;
- ação preventiva;
- status;
- data de conclusão;
- evidência de encerramento.

Status:

**Aberta → Em tratamento → Aguardando validação → Encerrada**

---

# 26. Plano de ação

Cada ação deverá possuir:

- descrição;
- origem;
- responsável;
- prioridade;
- prazo;
- status;
- evidência;
- validação.

Prioridades sugeridas:

**Crítica / Alta / Média / Baixa**

O sistema deverá apresentar ações vencidas e próximas do vencimento.

---

# 27. Acidentes e incidentes

O módulo deverá registrar:

- acidente;
- incidente;
- quase acidente;
- condição insegura;
- ato inseguro;
- doença ocupacional quando aplicável.

Dados:

- trabalhador;
- obra;
- local;
- data;
- hora;
- atividade;
- descrição;
- lesão;
- consequência;
- atendimento;
- afastamento;
- CAT;
- investigação;
- causas;
- ações;
- evidências.

---

# 28. Investigação de acidentes

O sistema deverá permitir metodologias de investigação.

Exemplos:

- análise de causa raiz;
- 5 Porquês;
- árvore de causas;
- fatores contribuintes;
- falhas de barreira.

A investigação deve buscar causas sistêmicas, evitando limitar o resultado a “erro do trabalhador”.

---

# 29. Emergência

Deverá existir módulo de emergência.

Possibilidades:

- incêndio;
- queda;
- choque elétrico;
- soterramento;
- espaço confinado;
- vazamento;
- acidente com máquinas;
- exposição química;
- emergência médica.

Cada cenário poderá possuir:

- procedimento;
- responsáveis;
- contatos;
- recursos;
- rota;
- equipamento;
- treinamento;
- simulado;
- evidência.

---

# 30. Terceiros e subcontratadas

O sistema deverá possuir gestão específica de terceiros.

Antes do início da atividade, deverá verificar:

- documentos da empresa;
- trabalhadores;
- ASO;
- treinamentos;
- certificados;
- EPIs;
- autorizações;
- integração;
- documentos específicos da atividade.

O sistema deverá permitir bloquear ou sinalizar trabalhadores/empresas que não estejam conformes.

---

# 31. Gestão documental

Todos os documentos relacionados a SST deverão possuir:

- nome;
- tipo;
- categoria;
- origem;
- responsável;
- versão;
- validade;
- data de emissão;
- data de revisão;
- requisito relacionado;
- obra;
- setor;
- status;
- arquivo;
- histórico.

Status:

**Rascunho → Em aprovação → Vigente → Obsoleto → Cancelado**

Documentos obsoletos não devem ser confundidos com documentos vigentes.

---

# 32. Matriz de requisitos legais

A matriz legal deverá ser uma estrutura central.

Campos sugeridos:

| Campo | Descrição |
|---|---|
| Código | Identificador interno |
| Norma | NR, lei, decreto, ABNT etc. |
| Item | Artigo/item/subitem |
| Tema | Assunto |
| Requisito | Exigência |
| Aplicabilidade | Sim/Não |
| Justificativa | Motivo |
| Evidência | Documento/registro |
| Responsável | Responsável interno |
| Periodicidade | Frequência |
| Prazo | Quando aplicável |
| Status | Conforme/Não conforme |
| Última revisão | Controle |
| Próxima revisão | Controle |

---

# 33. Motor de aplicabilidade

Uma função importante da aplicação será determinar quais requisitos se aplicam a cada obra ou atividade.

Exemplo:

**Obra possui trabalho em altura?**

Sim:

→ NR-35 aplicável.

**Possui espaço confinado?**

Sim:

→ NR-33 aplicável.

**Possui instalações elétricas?**

Sim:

→ NR-10 aplicável.

**Possui máquinas?**

Sim:

→ NR-12 aplicável.

**Possui inflamáveis?**

Sim:

→ NR-20 aplicável.

A aplicação deverá permitir regras automáticas de aplicabilidade.

---

# 34. Sistema de alertas

O sistema deverá gerar alertas para:

- treinamento vencendo;
- treinamento vencido;
- ASO vencendo;
- ASO vencido;
- EPI pendente;
- documento vencido;
- inspeção atrasada;
- ação atrasada;
- PT vencida;
- certificado vencido;
- autorização vencida;
- requisito legal em revisão;
- não conformidade crítica;
- trabalhador sem requisito obrigatório.

---

# 35. Dashboard

O dashboard executivo deverá apresentar, no mínimo:

### Indicadores de conformidade
- % de requisitos atendidos;
- % de requisitos pendentes;
- requisitos críticos;
- documentos vencidos.

### Pessoas
- trabalhadores ativos;
- aptos;
- inaptos;
- treinamentos vencidos;
- treinamentos próximos do vencimento.

### Segurança
- acidentes;
- incidentes;
- quase acidentes;
- inspeções;
- não conformidades.

### Ações
- abertas;
- em andamento;
- vencidas;
- concluídas.

### Obras
- obras ativas;
- obras conformes;
- obras críticas;
- riscos críticos.

---

# 36. Sistema de classificação de risco

O sistema poderá utilizar uma matriz parametrizável.

Exemplo:

**Probabilidade × Severidade = Nível de risco**

Classificações:

- Trivial;
- Baixo;
- Moderado;
- Alto;
- Crítico.

A matriz deverá ser configurável pela organização e não estar rigidamente programada.

---

# 37. Evidências

Toda evidência deverá possuir:

- data;
- autor;
- origem;
- localização;
- vínculo;
- arquivo;
- fotografia;
- assinatura, quando aplicável;
- hash ou mecanismo de integridade quando necessário.

O sistema deverá evitar registros sem rastreabilidade.

---

# 38. Auditoria

Deverá existir módulo de auditoria.

Possibilidades:

- auditoria interna;
- auditoria de obra;
- auditoria de terceiros;
- auditoria legal;
- auditoria de cliente.

Cada auditoria deverá possuir:

**escopo → requisitos → evidências → achados → ações → conclusão.**

---

# 39. Indicadores de desempenho

Indicadores sugeridos:

### Reativos
- acidentes;
- afastamentos;
- horas perdidas;
- gravidade;
- frequência.

### Proativos
- inspeções realizadas;
- inspeções planejadas;
- treinamentos realizados;
- ações preventivas;
- quase acidentes registrados;
- comportamentos observados;
- requisitos atendidos;
- não conformidades eliminadas.

O sistema deve privilegiar indicadores preventivos, não apenas número de acidentes.

---

# 40. Integração com eSocial

A aplicação deverá prever integração futura ou direta com os eventos relacionados à SST do eSocial, especialmente:

- S-2210 — Comunicação de Acidente de Trabalho;
- S-2220 — Monitoramento da Saúde do Trabalhador;
- S-2240 — Condições Ambientais do Trabalho — Agentes Nocivos.

Os dados do sistema devem ser estruturados para reduzir duplicidade de cadastro e facilitar a geração das informações necessárias.

---

# 41. Inteligência Artificial

A IA poderá ser utilizada como camada de apoio à gestão.

Possibilidades:

- interpretação de requisitos legais;
- classificação de aplicabilidade;
- identificação de riscos;
- sugestão de controles;
- análise de documentos;
- comparação entre versões de normas;
- análise automática de inspeções;
- identificação de inconsistências;
- geração de relatórios;
- resumo executivo;
- identificação de documentos vencidos;
- sugestão de ações corretivas;
- análise de tendências;
- consulta à base normativa.

A IA **não deverá substituir a responsabilidade técnica ou legal dos profissionais de SST**.

---

# 42. Base de conhecimento

A base de conhecimento deverá ser organizada em camadas:

## Camada 1 — Legislação
Leis, decretos, portarias e regulamentos.

## Camada 2 — Normas
Normas Regulamentadoras e normas técnicas aplicáveis.

## Camada 3 — Interpretação
Guias oficiais, manuais, notas técnicas e orientações.

## Camada 4 — Procedimentos internos
Políticas, procedimentos, instruções e padrões da empresa.

## Camada 5 — Operação
Checklists, APR, PT, inspeções e registros.

## Camada 6 — Evidências
Fotos, certificados, documentos, relatórios e assinaturas.

A IA deverá sempre priorizar fontes oficiais e documentos vigentes.

---

# 43. Controle de versão normativa

A aplicação deverá manter histórico das normas.

Cada requisito deverá possuir:

- versão;
- data de publicação;
- início de vigência;
- situação;
- fonte oficial;
- alteração realizada;
- impacto;
- requisitos afetados.

Quando uma norma mudar, o sistema deverá identificar quais documentos, procedimentos, treinamentos e controles podem ter sido impactados.

---

# 44. Permissões e perfis

O sistema deverá possuir controle de acesso.

Perfis possíveis:

- Administrador;
- Diretor;
- Gestor QSMS;
- Engenheiro de Segurança;
- Técnico de Segurança;
- Médico do Trabalho;
- RH;
- Gestor de Obra;
- Encarregado;
- Trabalhador;
- Auditor;
- Terceiro.

Cada perfil deverá possuir permissões específicas.

---

# 45. Regra de bloqueio preventivo

Uma das funções mais importantes do sistema será impedir ou sinalizar situações inseguras.

Exemplo:

**Trabalho em altura**

O sistema verifica:

- ASO válido?
- Treinamento válido?
- autorização válida?
- análise de risco disponível?
- sistema de proteção definido?
- inspeção realizada?
- documentação obrigatória válida?

Se requisitos críticos estiverem ausentes:

**ATIVIDADE NÃO LIBERADA**

O sistema deverá registrar o motivo.

---

# 46. Fluxo operacional esperado

Fluxo geral:

**Cadastro da obra**

↓

**Identificação das atividades**

↓

**Identificação dos perigos**

↓

**Avaliação dos riscos**

↓

**Definição dos controles**

↓

**Cadastro dos trabalhadores**

↓

**Verificação de aptidão**

↓

**Verificação de treinamento**

↓

**Autorização**

↓

**Análise de risco**

↓

**Liberação da atividade**

↓

**Inspeção**

↓

**Execução**

↓

**Registro de evidências**

↓

**Identificação de desvios**

↓

**Não conformidade**

↓

**Ação corretiva**

↓

**Validação**

↓

**Encerramento**

↓

**Indicadores**

---

# 47. Requisitos mínimos do MVP

A primeira versão da aplicação deverá priorizar:

1. Cadastro de empresas;
2. Cadastro de obras;
3. Cadastro de trabalhadores;
4. Cadastro de funções;
5. Controle de ASO;
6. Controle de treinamentos;
7. Controle de autorizações;
8. Controle de EPI;
9. Cadastro de riscos;
10. Inventário de riscos;
11. PGR;
12. APR/AR;
13. PT;
14. Inspeções;
15. Checklists;
16. Não conformidades;
17. Plano de ação;
18. Acidentes e incidentes;
19. Gestão documental;
20. Matriz legal;
21. Alertas;
22. Dashboard.

---

# 48. Evolução futura

Após o MVP, poderão ser adicionados:

- aplicativo mobile;
- QR Code para equipamentos;
- QR Code para trabalhadores;
- assinatura digital;
- reconhecimento de documentos por IA;
- análise automática de fotos;
- integração com eSocial;
- integração com RH;
- integração com ERP;
- integração com gestão de obras;
- geolocalização;
- notificações;
- portal de terceiros;
- aplicativo offline para inspeções;
- geração automática de documentos;
- inteligência preditiva.

---

# 49. Resultado esperado

Ao final, a aplicação deverá responder rapidamente:

### Sobre uma pessoa:
**“Este trabalhador pode executar esta atividade?”**

### Sobre uma atividade:
**“Esta atividade está liberada e controlada?”**

### Sobre uma obra:
**“Qual é o nível de conformidade da obra?”**

### Sobre um risco:
**“Quais controles existem e estão funcionando?”**

### Sobre uma lei:
**“Onde esta obrigação está sendo cumprida e qual é a evidência?”**

### Sobre uma não conformidade:
**“Quem deve corrigir, até quando e qual é a situação?”**

### Sobre a empresa:
**“Qual é a situação geral da SST agora?”**

---

# 50. Diretriz final de arquitetura

A aplicação deve ser construída como um **Sistema de Gestão de SST**, e não como um simples sistema documental.

A arquitetura deverá considerar quatro grandes pilares:

**1. CONFORMIDADE**
- legislação;
- NRs;
- matriz legal;
- requisitos.

**2. PREVENÇÃO**
- riscos;
- PGR;
- controles;
- treinamentos;
- inspeções.

**3. OPERAÇÃO**
- trabalhadores;
- atividades;
- APR;
- PT;
- EPI/EPC;
- autorizações.

**4. MELHORIA CONTÍNUA**
- acidentes;
- incidentes;
- auditorias;
- não conformidades;
- ações;
- indicadores.

A aplicação deverá garantir integração entre esses quatro pilares.

O sistema deve permitir que cada registro tenha contexto, relacionamento, responsável, validade, evidência e histórico.

**Objetivo final: transformar a legislação e os requisitos de SST em controles operacionais verificáveis, rastreáveis e mensuráveis.**