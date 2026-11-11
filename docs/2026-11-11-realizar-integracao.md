# Plano: Realizar integração (v2, 11/11/2026)

Base: `origin/master` (1a089a15). Mockup de referência: `scripts/mockups/realizar-integracao-2026-11-11-v4.html` (fluxo em etapas).

## Decisões do usuário (11/11)

- Sem agendamento de turma de integração. No pop-up "Funcionário novo" saem "Alterar data da turma" e o chip "Agendado"; entra "Iniciar integração" / "Continuar integração".
- Integração = fluxo sequencial de etapas, ordem fixa: **NR-06 → NR-12 → NR-18 → NR-35 → Ordem de Serviço (NR-01)**.
- **Uma assinatura biométrica por certificado/curso, por pessoa.** A OS tem assinatura própria, à parte.
- Cargas mínimas: NR-18 4 h; NR-12 8 h (faixa 8–16 h); NR-35 8 h; NR-06 a definir pelo QSMS (configurável; 0 = sem trava).
- NR-01 = OS assinada, registro isolado no perfil, sem certificado.
- "Integração de Segurança" não gera mais certificado; ao concluir a OS de todos, o perfil recebe o registro "Integração realizada". Certificados antigos ficam no histórico.
- Assinatura só por biometria (digital ou facial). Sem presença manual; sem biometria → cadastrar na hora. Etapa só avança quando todos daquela etapa assinaram.
- Ninguém sai antes (não modelar).
- OS traz só atividades/riscos/EPIs da função da pessoa.

## 0. Trabalho já começado fora da master

- `feature/os-atividades-por-funcao` (worktree `C:\Projetos\SST-APP-int-os-funcao`): é a correção da OS. Entidade `AtividadeFuncao` (IA sugere, Engenheiro de Segurança confirma), migration aditiva `20261111030502_AdicionarAtividadeFuncao`. Falta commitar `Ghes/PreservacaoAtividadesOs.cs` + teste. Sem confirmação do engenheiro a OS fica com pendência "AtividadesFuncaoNaoConfirmadas".
- `codex/integracao-atendimento` (worktree `C:\Projetos\SST-APP-int-atendimento`): WIP não commitado do atendimento. Encaixe na seção 4.3.
- PR #338 conflita com "sem agendamento": dispensa de ficha agenda turmas de todos os cursos da função; `excluirPacote` agenda cursos fora do pacote; `PacoteIntegracao.cs` fixa NR-06 1 h e NR-35 para todos. Ajustar: tirar agendamento e usar a calculadora de etapas. Fica: aviso agrupado por obra/dia sem calendário, terceirizado com ficha, status `Dispensado`.
- #336 (CreatedBy) e #337 (excluir turma, só Admin): compatíveis.

## 1. Objetivo

O técnico conduz a integração dos admitidos de uma obra em etapas de ordem fixa. Cada etapa de curso exige carga mínima, 3 fotos próprias e a assinatura biométrica de cada participante no certificado daquele curso. Na última etapa cada pessoa lê e assina a própria OS. Concluídas todas as OS, cada perfil recebe "Integração realizada".

## 2. Telas

**Novo**
- Página "Realizar integração" com stepper das 5 etapas (puladas esmaecidas: "Não se aplica a ninguém desta turma").
- Etapa de curso: participantes da etapa, carga lançada x mínimo, 3 fotos, instrutor, assinatura digital/facial por pessoa (cadastro na hora), botão "Avançar" travado com a lista do que falta.
- Etapa OS: uma linha por pessoa (ler, emitir, assinar), pendências iguais agrupadas.
- Pop-up "Funcionário novo": "Iniciar integração" → "Continuar integração — etapa X de 5".
- Perfil › Treinamentos: bloco "Integração realizada" (data, obra, técnico), sem certificado.

**Muda**
- Passo de integração no pop-up: Pendente / Em andamento — etapa X / Feito.
- Aviso ao técnico sem turmas/datas e sem evento de calendário.

**Sai (confirmado pelo usuário)**
- Pop-up: "Alterar data da turma", campo de data, "Salvar data"/"Cancelar" (`FuncionarioNovoModal.tsx` ~297-447); chip "Agendado" e "Turma em dd/MM".
- Criação automática de turmas pelo plano de integração e o evento de calendário da primeira turma.
- Encerrar pela tela comum de turma uma turma que pertence a uma integração.

## 3. Mapa do que existe (master)

- Plano: `Domain/Entidades/PlanoIntegracao.cs`; `Application/PlanosIntegracao/PlanoIntegracaoService.cs`, `AgendaTreinamentosIntegracao.cs`, `Queries/ListarPlanosIntegracaoPendentesQuery.cs`; `Api/Controllers/PlanosIntegracaoController.cs`.
- Pop-up: `TeamsApp/src/components/planos-integracao/FuncionarioNovoModal.tsx`.
- Turma (base de cada etapa): `Domain/Entidades/SessaoTreinamento.cs`; `SessoesTreinamento/EvidenciaPresencaTreinamento.cs`, `PadraoTurmaTreinamento.cs`; `SessoesTreinamento/Commands/` (RegistrarPresenca digital e facial, AnexarFotoEvidencia, DefinirInstrutor, `EncerrarSessaoTreinamentoCommand` — já gera um certificado por pessoa com `DocumentoAssinatura` próprio, assinatura biométrica do trabalhador e do instrutor); `SessoesTreinamentoController.cs`; `pages/treinamentos/SessaoTreinamentoDetalhePage.tsx`, `BlocoInstrutorTurma.tsx`.
- Cursos: `Domain/Entidades/Treinamento.cs` (`CargaHorariaMinima`, `EhIntegracaoSeguranca`, `AtendeNr6`); `CatalogoNormasRegulamentadoras.cs`; `CursosObrigatorios/ExigenciasTreinamento.cs`.
- Certificados: `Infrastructure/Documentos/CertificadoTreinamentoPdfService.cs`, `CertificadoAssinaturasPdf*.cs`; `Treinamentos/Queries/ExportarCertificadoTreinamentoQuery.cs`.
- OS: `Domain/Entidades/OrdemServico.cs`; `OrdensServico/ObterMinutaOrdemServicoQuery.cs`, `AvaliadorPendenciasOs.cs`, `EmitirOrdemServicoCommand.cs`; `OrdensServicoController.cs`; `pages/pessoas/OrdemServicoTab.tsx`; `components/assinatura/AssinaturaOrdemServicoDialog.tsx`.
- Biometria (front): `BotaoBiometriaDigital.tsx`, `FilaFacialDds.tsx`, `lib/agenteBiometricoLocal.ts`, `BiometriaDigitalCartao.tsx`, `GradeFotosEvidencia.tsx`, `camera/SlotFoto.tsx`.
- Dependem do curso Integração: `EntregasEpi/Commands/CriarEntregaEpiCommand.cs`, `Terceirizados/CalculadoraLiberacaoTerceirizado.cs`, `Trabalhadores/Queries/ObterLiberacaoParaTrabalhoQuery.cs`.
- RBAC: `SstDbContext.cs` ~222-340 (`HasQueryFilter` por ObraId substitui o de `Ativo`).
- Novidades: `NovidadesSeeder.cs` (master 5.155.0; #338 usa 5.158.0 → próxima livre no merge).

## 4. Modelo

### 4.1 Fotos: por etapa, ao vivo, sem reaproveitamento (regra de rastreabilidade — usuário, 11/11)
Cada etapa é uma `SessaoTreinamento`; o encerramento exige 3 fotos da turma e o certificado de cada NR mostra as fotos da sua turma. Reaproveita endpoints de fotos, SlotFoto e carimbo. Custo: até 12 fotos por atendimento.

Regras obrigatórias (cada NR exige evidência individual; a mesma foto nunca vale para dois cursos):
- **Só câmera ao vivo** (sem galeria/upload de arquivo), no padrão SlotFoto/carimbo já existente: data/hora, geolocalização, obra, curso e técnico gravados como metadados.
- **Hash SHA-256 de cada foto** gravado no registro. O servidor **recusa** foto cujo hash já exista em qualquer outra turma/etapa (mesmo atendimento ou não) e foto repetida dentro da mesma etapa.
- **Carimbo de tempo dentro da etapa:** a foto só é aceita com a etapa `EmAndamento` e com captura posterior ao início da etapa; fotos de etapas diferentes ficam em blocos de tempo distintos (ex.: NR-06 de manhã, NR-12 à tarde). O servidor recusa foto com horário de captura anterior ao início da etapa ou de outra etapa.
- As 3 fotos de uma etapa devem ser distribuídas ao longo dela (ex.: início, meio, fim); o certificado mostra os horários.
- A assinatura biométrica de cada certificado também é uma captura própria por etapa (não reaproveita a leitura de outra etapa) — já é assim no modelo de presença por turma.
- Testes B4: foto com hash repetido entre etapas → recusada; foto fora da janela da etapa → recusada; upload sem origem câmera → recusado.

Risco: a exigência do MTE foi trazida pelo usuário; a redação exata da norma não foi conferida aqui. Validar com o QSMS/jurídico.

### 4.2 Entidades (migration aditiva `AdicionarAtendimentoIntegracao`)
Todas com `ObraId` próprio, `HasQueryFilter(Ativo && escopo da obra)`, sem navegação obrigatória para pai (Id + snapshot de nome).

- `IntegracaoAtendimento`: ObraId, Numero (INT-AAAA-NNNN), ResponsavelUsuarioId, InstrutorTrabalhadorId, Status (enum EmAndamento/Concluido/Cancelado), EtapaAtualOrdem?, IniciadaEmUtc, ConcluidaEmUtc?, Versao (concorrência).
- `IntegracaoEtapa`: ObraId, IntegracaoAtendimentoId, Ordem (1-5), Tipo (Nr06/Nr12/Nr18/Nr35/OrdemServico), CursoTreinamentoId?, SessaoTreinamentoId?, CargaMinimaHoras (snapshot; 0 = sem trava), Status (Pendente/EmAndamento/Concluida/Pulada), IniciadaEmUtc?, ConcluidaEmUtc?.
- `ParticipanteIntegracao`: ObraId, IntegracaoAtendimentoId, TrabalhadorId, PlanoIntegracaoId?, FuncaoId, FuncaoNome, Nome, OrdemServicoId?.
- `PeriodoEtapaIntegracao`: ObraId, IntegracaoEtapaId, Data, Inicio, Fim, Minutos.
- `SessaoTreinamento.IntegracaoAtendimentoId?`.
- `RegistroIntegracao`: ObraId, TrabalhadorId, IntegracaoAtendimentoId, RealizadaEm, TecnicoUsuarioId, TecnicoNome, FuncaoNome.

Carga mínima: `PadraoTurmaTreinamento.De(curso).CargaHorariaMinima` (QSMS ajusta no catálogo de cursos; NR-12 = 8). Quem entra: NR-06 e NR-18 todos; NR-12 e NR-35 pelos cursos obrigatórios do PGR (`ExigenciasTreinamento`); fora quem já tem a NR válida; OS todos.

### 4.3 Encaixe com o WIP do Codex
- Mantém: `IntegracaoAtendimento` (Status vira enum, + EtapaAtualOrdem), `ParticipanteIntegracao`, `SessaoTreinamento.IntegracaoAtendimentoId`, autorização (Técnico/Admin com vínculo na obra; instrutor com perfil), `Versao`, chave `Integracao:AtendimentoHabilitado`.
- Troca: pacote fixo e `IncluiNr12` → `CalculadoraEtapasIntegracao`; `CodigoModuloIntegracao` → `IntegracaoEtapa.Tipo`; `FotoEvidenciaIntegracao` → fotos da turma; `AssinadaEmUtc` → presença por turma e OS por pessoa.
- Lógica sai de `Infrastructure/IntegracoesAtendimento/IntegracaoAtendimentoService.cs` para handlers MediatR em `Application/IntegracoesAtendimento/`.

### 4.4 Estados e transições
1. Iniciar: calcula as 5 etapas; etapa de curso sem participante = Pulada; OS nunca pulada; um atendimento EmAndamento por obra; primeira não pulada vira EmAndamento.
2. Entrar em etapa de curso: cria a `SessaoTreinamento` (padrão da NR, instrutor, IntegracaoAtendimentoId) e inscreve só os participantes da etapa.
3. Durante: períodos, 3 fotos e presença biométrica (= assinatura do certificado) só na etapa atual.
4. Avançar exige: todos com presença confirmada, carga ≥ mínimo (0 não trava), 3 fotos, instrutor. Grava carga e data, chama `EncerrarSessaoTreinamentoCommand` (ViaIntegracao), marca Concluida, abre a próxima. Transação idempotente.
5. Etapa OS: por pessoa minuta → emitir → assinatura biométrica do funcionário → assinatura do técnico. Concluir só com todas assinadas: atendimento Concluido, `RegistroIntegracao` por pessoa, planos recalculados.
6. Sem volta (etapa concluída é imutável); sem "saiu antes"; "Continuar" abre em EtapaAtualOrdem.

## 5. API (`api/integracoes-atendimento`, nenhum endpoint anônimo)

Policy `treinamento:ver` / `treinamento:criar` + checagem Técnico/Admin com vínculo na obra; sem acesso → 404.

| Método | Rota | Handler |
|---|---|---|
| GET | `entrada?obraId=` | ObterEntradaIntegracaoQuery |
| POST | `/` | IniciarIntegracaoCommand |
| GET | `{id}` | ObterIntegracaoQuery |
| POST | `{id}/participantes` | IncluirParticipantesIntegracaoCommand |
| POST/DELETE | `{id}/etapas/{etapaId}/periodos[/{pid}]` | Registrar/RemoverPeriodoEtapaCommand |
| POST | `{id}/etapa-atual/avancar` | AvancarEtapaIntegracaoCommand (409 com o que falta) |
| GET | `{id}/ordens-servico` | ListarOsIntegracaoQuery (pendências agrupadas) |
| POST | `{id}/concluir` | ConcluirIntegracaoCommand |
| GET | `relatorio/turmas-integracao-agendadas` (Admin) | só leitura |

Reaproveitados com proteção: presença/fotos/instrutor em `api/sessoes-treinamento/{id}/...` só na etapa EmAndamento; encerrar direto e exclusão (#337) recusam turma de integração; OS reaproveita `api/trabalhadores/{id}/ordem-servico`.

## 6. Execução

Pré-requisito: #336, #337 e `os-atividades-por-funcao` prontos; ajustar #338; WIP do Codex assumido por esta sessão (Codex sem limite até 13:58 de 11/11 — não retomar nos mesmos worktrees).

| # | Agente | Escopo | Ordem |
|---|---|---|---|
| B1 | backend-dotnet | Entidades, enums, DbContext, filtros, configurações, migration | primeiro |
| B2 | backend-dotnet | Tirar agendamento (Agenda, PlanoIntegracaoService, ListarPlanos…) atrás da chave | após B1 |
| B3 | backend-dotnet | CalculadoraEtapas, Iniciar, Incluir, Períodos, Entrada, Obter, controller | após B2 |
| B4 | backend-dotnet | Avançar, Concluir, proteções, relatório | após B3 |
| B5 | backend-dotnet | OS: tirar curso Integração dos exigidos; ListarOsIntegracao agrupada | paralelo B3/B4 |
| B6 | backend-dotnet | EPI/terceirizado/liberação aceitam RegistroIntegracao ou certificado antigo | paralelo |
| B7 | backend-dotnet | NovidadesSeeder | fim |
| F0 | frontend-react | `lib/api.ts` | paralelo B3 |
| F1 | frontend-react | Extrair PresencaBiometricaTurma e FotosTurma da página de turma (sem mudança visual) | após F0 |
| F2 | frontend-react | `pages/integracao/RealizarIntegracaoPage.tsx` + componentes; rotas | após F1 e mockup v4 aprovado |
| F3 | frontend-react | FuncionarioNovoModal: tira data/chip; Iniciar/Continuar | paralelo |
| F4 | frontend-react | TreinamentosTab (Integração realizada); OrdemServicoTab (título) | paralelo |
| R | revisor | tudo | fim |

Testes por etapa: ver relatório do arquiteto (calculadora, trava do Avançar, idempotência, escopo 404, OS agrupada, EPI com registro novo e antigo, `tsc -b`).

## 7. Riscos e dúvidas

- LGPD: uma leitura biométrica por certificado multiplica capturas (padrão atual apaga imagem e guarda hash); cadastro na hora exige consentimento; pendências mostram só "Sem ASO apto".
- NR (validar com QSMS): carga NR-06; critério 8 x 16 h na NR-12; OS assinada cumpre NR-01; frequência por dia em etapa de vários dias; fonte da exigência MTE de assinatura por certificado.
- Dependência: sem atividades confirmadas pelo engenheiro, a etapa OS não emite e o atendimento não conclui.

**Respostas do usuário (11/11, prevalecem sobre os padrões abaixo):**
- Q1: o Técnico de Segurança é quem conduz a integração (instrutor/responsável). Continua isento de fazer o combo.
- Q2: sem ASO não emite OS (pendência do RH). Os demais seguem (padrão mantido).
- Q3: sem limite por dia; uma assinatura por curso/certificado.
- Q4: SIM — com a chave ligada, nenhum curso da função é agendado automaticamente (inclui NR-10, NR-33 etc.); passam a turma avulsa manual.
- Q5: incluir/retirar só antes da primeira assinatura.
- Q6: a data de cada certificado/OS é a data em que a pessoa assinou aquele curso/certificado/OS (não o último dia da etapa).
- Q7: entrega de EPI aceita NR-06 concluída.
- Q9: se a assinatura não der certo, a pessoa NÃO recebe o certificado daquele curso e NÃO entra na lista de presença. Implementação: o técnico marca "Não assinou" (com motivo) na etapa; a pessoa sai da etapa sem certificado e o Avançar deixa de esperar por ela. Ela não recebe "Integração realizada" enquanto faltar curso.
- Q10: abre pelo pop-up "Funcionário novo" e por uma aba "Integração" em Treinamentos.

**Mockup v4 aprovado pelo usuário em 11/11.** Padrões adotados antes das respostas acima (histórico):
- Q1: Técnico de Segurança continua isento, como hoje.
- Q2: pendência externa na OS (ex.: Sem ASO) NÃO segura os demais. "Concluir" libera quando todas as OS emitíveis estão assinadas; quem ficou com pendência fica "OS pendente" e recebe o registro "Integração realizada" quando assinar a OS depois (pelo perfil).
- Q3: uma assinatura biométrica por etapa (presença da turma), não por dia.
- Q4: só o combo deixa de ser agendado; demais cursos da função ficam como hoje (fora desta entrega).
- Q5: pode incluir pessoas (com ou sem plano aberto) enquanto a 1ª etapa não tiver nenhuma assinatura; retirar idem.
- Q6: certificado usa a data do último dia (comportamento atual).
- Q7: entrega de EPI aceita NR-06 concluída e assinada, ou `RegistroIntegracao`, ou certificado antigo de Integração.
- Q8: não bloquear turma avulsa (fora desta entrega).
- Q9: sim, quem recusa biometria trava a etapa.
- Q10: abre pelo pop-up "Funcionário novo"; sem item novo de menu.

Perguntas abertas originais: Q1 Técnico isento? Q2 OS travada por pendência do RH segura todos? Q3 presença por dia? Q4 demais cursos (NR-10, NR-33) deixam de ser agendados? Q5 incluir/retirar pessoas após iniciar? Q6 certificado: último dia ou período? Q7 EPI exige "Integração realizada" ou basta NR-06? Q8 bloquear turma avulsa de "Integração de Segurança"? Q9 recusa de biometria trava a etapa? Q10 abrir também por Gestão SST › Treinamentos?

Fora desta entrega: cancelar atendimento (Admin), ata consolidada, decisão sobre turmas antigas.

## 8. Como verificar

1. Pop-up sem controles de data/"Agendado"; Iniciar → Continuar na etapa em andamento.
2. Admissão com a chave ligada não cria turma nem evento.
3. Stepper NR-06 → NR-12 → NR-18 → NR-35 → OS com participantes certos e etapa Pulada.
4. Avançar travado sem presença/carga/fotos; libera após completar (2 dias, digital, facial, cadastro na hora).
5. Fechar e "Continuar" volta na mesma etapa.
6. Perfil: um certificado por NR com fotos e assinatura próprias.
7. OS só da função, pendências agrupadas, assinatura individual; ao concluir, "Integração realizada".
8. Encerrar/excluir turma da etapa pela tela comum: recusado.
9. Técnico de outra obra: 404.
10. Novidades e relatório Admin.

## Risco residual das fotos (M5, 11/11/2026)

A foto de turma de integracao so e aceita com token de captura emitido pelo servidor (uso unico, 5 min), e a hora gravada e a do servidor. O hash SHA-256 barra a mesma imagem em duas etapas, mas imagem regravada ou reencodada muda o hash e nao e detectada. Validar com QSMS/juridico se isso basta para a exigencia do MTE.
