namespace AAHBRANT.SST.Domain.Enums;

// Seção 44 da Base de Conhecimento — 12 perfis de acesso
public enum TipoPerfilAcesso
{
    Administrador = 1,
    Diretor = 2,
    GestorQsms = 3,
    EngenheiroSeguranca = 4,
    TecnicoSeguranca = 5,
    MedicoDoTrabalho = 6,
    Rh = 7,
    GestorDeObra = 8,
    Encarregado = 9,
    Trabalhador = 10,
    Auditor = 11,
    Terceiro = 12
}

// Modelo de escopo G/U/O/P definido na matriz RBAC (docs/RBAC-Matrix.md)
public enum EscopoAcesso
{
    Global = 1,
    Unidade = 2,
    Obra = 3,
    Proprio = 4
}

// Usuario não tem senha local — autenticação é 100% delegada ao Entra ID SSO (PROJECT RULES.md,
// já habilitada condicionalmente em Api/Program.cs). Este status é controle administrativo de
// acesso (bloquear/desativar um usuário), não estado de um fluxo de login local.
public enum StatusUsuario
{
    Ativo = 1,
    Inativo = 2,
    Bloqueado = 3
}

public enum StatusObra
{
    Planejada = 1,
    EmAndamento = 2,
    Paralisada = 3,
    Concluida = 4,
    Encerrada = 5
}

public enum TipoVinculo
{
    Clt = 1,
    Terceirizado = 2,
    Autonomo = 3,
    Estagiario = 4
}

// Seção 9 da Base de Conhecimento — status de aptidão
public enum ResultadoAso
{
    Apto = 1,
    AptoComRestricao = 2,
    Inapto = 3,
    Pendente = 4
}

public enum TipoExameAso
{
    Admissional = 1,
    Periodico = 2,
    RetornoAoTrabalho = 3,
    MudancaDeFuncao = 4,
    Demissional = 5
}

// PR-SST-003 — exames complementares do PCMSO (audiometria, acuidade visual etc.), vinculados
// opcionalmente a um ASO.
public enum TipoExameComplementar
{
    Audiometria = 1,
    AcuidadeVisual = 2,
    Espirometria = 3,
    Laboratoriais = 4,
    AvaliacaoClinica = 5,
    ExameEspecifico = 6
}

// Seção 34 da Base de Conhecimento — tipos de alerta. "PT vencida" é item literal próprio
// (linha 869), distinto de "autorização vencida" (linha 871) — adicionado agora que o módulo
// PT existe; os demais 13 itens já estavam mapeados desde o módulo de Riscos/Alertas.
public enum TipoAlerta
{
    AsoVencendo = 1,
    AsoVencido = 2,
    TreinamentoVencendo = 3,
    TreinamentoVencido = 4,
    AutorizacaoVencendo = 5,
    AutorizacaoVencida = 6,
    EpiValidadeProxima = 7,
    EpiVencido = 8,
    InspecaoPendente = 9,
    NaoConformidadeAberta = 10,
    AcaoAtrasada = 11,
    DocumentoVencendo = 12,
    DocumentoVencido = 13,
    AtividadeBloqueada = 14,
    PtVencida = 15,
    // 16 reservado (módulo Higienização removido) — não reaproveitar, alertas antigos podem ter esse tipo gravado.
    ExtintorVencendo = 17,
    ExtintorVencido = 18,
    EquipamentoVencendo = 19,
    EquipamentoVencido = 20,
    // Pedido do usuário, 02/09: "Vigência do Programa" (Início/Revisão Sugerida/Término) do PGR
    // ganha alerta próprio — Término vira PgrVencendo/Vencido, Revisão Sugerida vira os dois abaixo.
    PgrVencendo = 21,
    PgrVencido = 22,
    PgrRevisaoVencendo = 23,
    PgrRevisaoVencida = 24
}

public enum SeveridadeAlerta
{
    Info = 1,
    Atencao = 2,
    Critico = 3
}

// Motor Central de Alertas (requisito adicionado pelo usuário em 2026-08-24): generaliza o padrão
// de vencimento/alerta para qualquer módulo, em vez de cada um reimplementar sua própria checagem
// de dias/severidade. Formaliza o que hoje é o campo livre Alerta.EntidadeOrigemTipo. Só Aso e
// Treinamento têm IAlertaOrigemProvider registrado nesta fase — os demais valores existem para os
// módulos de ativos (Extintor/Equipamento) e os já existentes (Epi/Documento/Inspecao/Dds/
// AcaoPlano) previstos no requisito do usuário, plugados em fases seguintes.
public enum TipoModuloAlerta
{
    Aso = 1,
    Treinamento = 2,
    // 3 reservado (módulo Higienização removido) — não reaproveitar, regras/alertas antigos podem ter esse módulo gravado.
    Epi = 4,
    Documento = 5,
    Inspecao = 6,
    Extintor = 7,
    Equipamento = 8,
    Dds = 9,
    PlanoAcao = 10,
    Outro = 11,
    Pgr = 12
}

public enum StatusAlerta
{
    Aberto = 1,
    EmTratamento = 2,
    Escalonado = 3,
    Resolvido = 4,
    Ignorado = 5
}

// Integração do Motor de Alertas com o Calendário do Teams (docs/superpowers/specs/
// 2026-08-28-calendario-teams-design.md) — canal plugado no AlertaEngineService e nos Commands
// manuais de Alerta, espelhando o padrão de fila já usado pelo Activity Feed.
public enum OperacaoCalendarioTeams
{
    Criar = 1,
    Atualizar = 2,
    Cancelar = 3
}

public enum StatusCalendarioEvento
{
    Pendente = 1,
    Criado = 2,
    Cancelado = 3,
    Falhou = 4
}

// Seção 36 da Base de Conhecimento — 5 categorias literais da matriz de classificação de risco.
public enum NivelRisco
{
    Trivial = 1,
    Baixo = 2,
    Moderado = 3,
    Alto = 4,
    Critico = 5
}

// Não há fluxo literal no documento para o "status" do controle de risco (§14) — proposta
// própria (a diferir do fluxo literal de Não Conformidade, que será citado quando implementado).
public enum StatusControleRisco
{
    Pendente = 1,
    EmAndamento = 2,
    Concluido = 3
}

// Não há fluxo literal no documento para o "status" do PGR (§16) — proposta própria (avisar o
// usuário se ele quiser outro fluxo, ex. um vocabulário alinhado à NR-01).
public enum StatusPgr
{
    EmElaboracao = 1,
    Vigente = 2,
    EmRevisao = 3,
    Encerrado = 4
}

// Não há vocabulário literal para o "status" do PCMSO — mesmo desenho de StatusPgr (proposta
// própria; avisar o usuário se ele quiser outro fluxo).
// PENDENTE: StatusPcmso era do PCMSO v1, descontinuado (ver commit "integra Saúde Ocupacional de
// produção, descontinua PCMSO v1") — não é usado por PcmsoDetalhe. Mantido para não quebrar
// referências antigas; usar StatusPcmsoDocumento abaixo para o PCMSO atual.
public enum StatusPcmso
{
    EmElaboracao = 1,
    Vigente = 2,
    EmRevisao = 3,
    Encerrado = 4
}

// Reintroduzido escopado ao PCMSO em 2026-09-03 (ver nota em PcmsoDetalhe) — era StatusDocumentoGestao,
// compartilhado por todo o módulo de Gestão Documental removido em 2026-08-28; mesmos valores/rótulos,
// porque o frontend (TeamsApp/src/lib/api.ts, StatusPcmsoDocumento) já foi escrito esperando este
// vocabulário e não pode ser adivinhado de outra forma sem quebrar as telas de PCMSO existentes.
public enum StatusPcmsoDocumento
{
    Rascunho = 1,
    EmAprovacao = 2,
    Vigente = 3,
    Obsoleto = 4,
    Cancelado = 5
}

// NTAG.md §2 — identification_tags.type: CHECK (type IN ('NTAG215', 'NTAG213', 'QR_CODE', 'RFID')).
// Valores literais do documento; a numeração 1-4 é atribuição própria (o documento usa VARCHAR/CHECK,
// não enum numérico), mesmo padrão já aplicado a NivelRisco/StatusObra.
public enum TipoTag
{
    Ntag215 = 1,
    Ntag213 = 2,
    QrCode = 3,
    Rfid = 4
}

// NTAG.md §2 — identification_tags.status: CHECK (status IN ('AVAILABLE', 'BOUND', 'DISABLED', 'LOST')).
public enum StatusTag
{
    Disponivel = 1,
    Vinculada = 2,
    Desativada = 3,
    Perdida = 4
}

// NTAG.md §2 — identification_tags.bound_entity_type: comentário do documento lista 'AREA', 'ASSET', 'WORKER'.
public enum TipoEntidadeVinculada
{
    Area = 1,
    Ativo = 2,
    Trabalhador = 3
}

// NTAG.md §2 — sst_areas.type: comentário do documento lista 'WORK_AREA', 'RISK_ZONE', 'STORAGE'.
public enum TipoArea
{
    AreaDeTrabalho = 1,
    ZonaDeRisco = 2,
    Armazenamento = 3
}

// NTAG.md §2 — sst_areas.status: CHECK (status IN ('ACTIVE', 'INACTIVE', 'BLOCKED')).
public enum StatusArea
{
    Ativa = 1,
    Inativa = 2,
    Bloqueada = 3
}

// Não há fluxo literal no documento para o "status"/"aprovação" da APR (§17) — proposta própria
// (mesmo padrão de disclosure já usado em StatusPgr/StatusControleRisco).
public enum StatusApr
{
    EmElaboracao = 1,
    AguardandoAprovacao = 2,
    Aprovada = 3,
    Reprovada = 4,
    Encerrada = 5
}

// "Assinatura" (§17) não especifica papéis — proposta própria para diferenciar quem assina a APR.
// Renomeado para bater com os 3 papéis literais do formulário APR REV.02 (planilha do usuário,
// 2026-08-29): "envolvido" é qualquer trabalhador da equipe exposta (Ass./Visto), e os dois blocos
// formais do rodapé do documento são "Elaboração / SST / Responsável Técnico" e "Supervisão /
// Encarregado / Engenharia". Substitui os valores antigos (Elaborador/Executante/Aprovador), que
// não correspondiam a nenhum desses três papéis do documento — sem dados existentes a migrar.
public enum PapelAssinaturaApr
{
    Envolvido = 1,
    Elaboracao = 2,
    Supervisao = 3
}

// Matriz de critérios própria da APR (planilha "APR REV.02", aba "Matriz de Risco", 2026-08-29) —
// fórmula fixa e literal do documento (P × S: 1-4 Baixo, 5-9 Moderado, 10-15 Alto, 16-25 Crítico),
// distinta da matriz configurável genérica do módulo Riscos (MatrizRiscoConfig/NivelRisco, que tem
// 5 níveis e é parametrizável por célula). Usar o NivelRisco genérico aqui misturaria duas matrizes
// com propósitos diferentes — a da APR é a fórmula literal do formulário, não configurável.
public enum NivelRiscoApr
{
    Baixo = 1,
    Moderado = 2,
    Alto = 3,
    Critico = 4
}

// Formulário "PT – PERMISSÃO DE TRABALHO REV.01" (planilha do usuário, 2026-08-29), §8: a PT tem
// um estado "Suspensa" próprio (mudança de condição/escopo/emergência etc.), distinto de Encerrada
// — adicionado ao vocabulário que antes só tinha EmElaboracao/Autorizada/Encerrada.
public enum StatusPt
{
    EmElaboracao = 1,
    Autorizada = 2,
    Suspensa = 3,
    Encerrada = 4
}

// §2 do formulário — 6 itens fixos de "Documentos e pré-requisitos", cada PT nasce com os 6 (ver
// CriarPermissaoTrabalhoCommand), cada um marcado Atendido/pendente. Substitui o antigo
// PermissaoTrabalhoRequisito (checklist de texto livre criado pelo usuário) — o documento define uma
// lista fixa e literal, não um catálogo configurável.
public enum ItemPreRequisitoPt
{
    AprEspecificaRevisadaDisponivel = 1,
    PgrInventarioRiscosCompativel = 2,
    InspecoesChecklistsEquipamentosValidos = 3,
    ProcedimentoInstrucaoTrabalhoAplicavelDisponivel = 4,
    TrabalhadoresCapacitadosAutorizadosAptos = 5,
    PlanoEmergenciaMeiosComunicacaoConhecidos = 6
}

// §3 do formulário — 12 opções fixas de "Tipo de trabalho / permissões específicas" (multi-select:
// só os tipos marcados viram linha em PermissaoTrabalhoTipoTrabalho). "Outro" carrega texto livre
// complementar (PermissaoTrabalhoTipoTrabalho.DescricaoOutro).
public enum TipoTrabalhoEspecialPt
{
    TrabalhoEmAltura = 1, // NR-35
    TrabalhoAQuenteFonteIgnicao = 2,
    BloqueioEnergiasPerigosas = 3, // LOTO
    DemolicaoCortePerfuracao = 4,
    EspacoConfinado = 5, // NR-33
    EscavacaoValaFundacao = 6,
    TrabalhoProximoTrafegoVias = 7,
    MaquinasEquipamentos = 8,
    EletricidadeIntervencaoEletrica = 9, // NR-10
    MovimentacaoIcamentoCargas = 10,
    ProdutosQuimicosInflamaveis = 11,
    Outro = 12
}

// §4 do formulário — 15 itens fixos de "Verificações obrigatórias antes da liberação", cada PT
// nasce com os 15 (ver CriarPermissaoTrabalhoCommand), cada um respondido C/NC/NA (ver
// RespostaVerificacaoPt) ou ainda não respondido (null). Um 16º item da planilha ("Rota de fuga,
// resgate, primeiros socorros...", linha 34) é só texto informativo, sem caixa C/NC/NA — não vira
// item aqui, fica só como texto fixo na tela/PDF (mesmo tratamento das RECOMENDAÇÕES da APR).
public enum ItemVerificacaoPt
{
    AreaIsoladaSinalizadaAcessoControlado = 1,
    AprDiscutidaComEquipeAntesDoInicio = 2,
    InterferenciasExistentesIdentificadas = 3,
    FontesEnergiaIdentificadasBloqueadasTestadas = 4,
    MaquinasFerramentasAcessoriosInspecionados = 5,
    EpcsInstaladosCondicoesUso = 6,
    EpisDisponiveisAdequadosCaValido = 7,
    CondicoesAcessoCirculacaoIluminacaoOrganizacao = 8,
    CondicoesMeteorologicasPermitemExecucaoSegura = 9,
    RiscoQuedaPessoasObjetosControlado = 10,
    RiscoIncendioExplosaoControladoExtintorDisponivel = 11,
    AtmosferaAvaliadaMonitorada = 12,
    EscavacoesTaludesEscoramentosAcessosInspecionados = 13,
    PlanoIcamentoAcessoriosMovimentacaoVerificados = 14,
    VigiaObservadorSinaleiroApoioDefinido = 15
}

public enum RespostaVerificacaoPt
{
    Conforme = 1,
    NaoConforme = 2,
    NaoAplicavel = 3
}

// §5 do formulário, coluna "EPIs aplicáveis" — algumas opções têm complemento de texto livre
// embutido no próprio formulário (Luvas/Respirador/Cinturão-talabarte: "____"), guardado em
// PermissaoTrabalhoEpi.Complemento quando aplicável.
public enum ItemEpiPt
{
    Capacete = 1,
    Oculos = 2,
    ProtetorFacial = 3,
    ProtetorAuditivo = 4,
    Luvas = 5,
    Calcado = 6,
    Respirador = 7,
    CinturaoTalabarte = 8,
    VestimentaEspecifica = 9
}

// §5 do formulário, coluna "EPCs / recursos aplicáveis".
public enum ItemEpcPt
{
    IsolamentoBarreira = 1,
    GuardaCorpo = 2,
    LinhaDeVida = 3,
    Extintor = 4,
    ExaustaoVentilacao = 5,
    DetectorGases = 6,
    KitResgate = 7,
    Iluminacao = 8,
    Sinalizacao = 9
}

// Seção 23 da Base de Conhecimento (linhas 581-595) — 13 tipos literais de inspeção.
public enum TipoInspecao
{
    Obra = 1,
    Canteiro = 2,
    Epi = 3,
    Epc = 4,
    Maquinas = 5,
    Ferramentas = 6,
    Andaimes = 7,
    Escadas = 8,
    Eletrica = 9,
    Altura = 10,
    EspacoConfinado = 11,
    Comportamental = 12,
    Terceiros = 13
}

// Seção 24 da Base de Conhecimento (linhas 605-614) — status literal de item de checklist.
public enum StatusItemChecklist
{
    Conforme = 1,
    NaoConforme = 2,
    NaoAplicavel = 3
}

// Documento não lista vocabulário literal para o "status" da execução da inspeção (mesma lacuna
// já registrada em StatusApr/StatusPgr/StatusPt/StatusControleRisco) — proposta própria.
public enum StatusInspecao
{
    EmAndamento = 1,
    Concluida = 2
}

// Seção 25 da Base de Conhecimento (linhas 620-643) não lista vocabulário fechado para a "origem"
// da não conformidade — proposta própria (avisar o usuário se quiser outro vocabulário), inspirada
// nas próprias fontes já citadas no documento: inspeção formal (§23/§24), auditoria, denúncia/
// reclamação e observação direta em campo.
public enum OrigemNaoConformidade
{
    Inspecao = 1,
    Auditoria = 2,
    Denuncia = 3,
    ObservacaoDireta = 4,
    Outro = 5
}

// Seção 25 da Base de Conhecimento (linha 641) — vocabulário literal do fluxo de status da NC:
// "Aberta → Em tratamento → Aguardando validação → Encerrada". Ampliado em 2026-08-29 conforme o
// Procedimento de Inspeção Técnica de Campo (§9): Enviada/EmAnalise/Devolvida entram como valores
// NOVOS (5/6/7, não reaproveitam números existentes); EmTratamento foi apenas RENOMEADO para
// EmAndamento (mesmo valor 2, vocabulário do documento) — renomear um nome de enum não afeta dados
// já gravados, só reatribuir o número afetaria. "Atrasada" (§9) não vira valor de status: é
// calculada pelo motor de alertas (ver NaoConformidadeAlertaProvider), mesmo padrão já usado por
// Aso/Treinamento/etc., em vez de duplicar o conceito de vencimento como estado gravado.
public enum StatusNaoConformidade
{
    Aberta = 1,
    EmAndamento = 2,
    AguardandoValidacao = 3,
    Encerrada = 4,
    Enviada = 5,
    EmAnalise = 6,
    Devolvida = 7,
}

// Seção 26 da Base de Conhecimento (linha 660) — vocabulário literal de prioridade do plano de ação:
// "Crítica / Alta / Média / Baixa".
public enum PrioridadeAcao
{
    Critica = 1,
    Alta = 2,
    Media = 3,
    Baixa = 4
}

// §25 lista "ação corretiva" e "ação preventiva" como dois campos da não conformidade (linhas
// 633-634). Decisão de modelagem: em vez de dois campos de texto livre na NC, cada ação vira uma
// linha na entidade genérica AcaoPlano (ver Domain/Entidades/AcaoPlano.cs), marcada com este tipo —
// proposta própria, não literal do documento. "Melhoria" cobre o uso do mesmo AcaoPlano por outros
// módulos futuros (Acidentes, Auditorias) que não se limitam a corretiva/preventiva.
public enum TipoAcaoPlano
{
    Corretiva = 1,
    Preventiva = 2,
    Melhoria = 3
}

// Seção 27 da Base de Conhecimento (linhas 666-670) — vocabulário literal de tipo de ocorrência:
// acidente; incidente; quase acidente; condição insegura; ato inseguro; doença ocupacional.
public enum TipoOcorrencia
{
    Acidente = 1,
    Incidente = 2,
    QuaseAcidente = 3,
    CondicaoInsegura = 4,
    AtoInseguro = 5,
    DoencaOcupacional = 6
}

// Seção 28 da Base de Conhecimento (linhas 702-708) — vocabulário literal de metodologias de
// investigação: análise de causa raiz; 5 Porquês; árvore de causas; fatores contribuintes;
// falhas de barreira.
public enum MetodologiaInvestigacao
{
    AnaliseCausaRaiz = 1,
    CincoPorques = 2,
    ArvoreDeCausas = 3,
    FatoresContribuintes = 4,
    FalhasDeBarreira = 5
}

// Documento não lista vocabulário literal para o "status" da investigação do acidente (mesma
// lacuna já registrada em StatusApr/StatusPgr/StatusPt/StatusInspecao/StatusControleRisco) —
// proposta própria (avisar o usuário se quiser outro fluxo).
public enum StatusAcidente
{
    Registrado = 1,
    EmInvestigacao = 2,
    Concluido = 3
}

// Classificação de gravidade do acidente, usada para calcular Dias Debitados na Taxa de
// Gravidade (NBR 14280, ver TabelaDiasDebitados). Vocabulário não citado literalmente na Base
// de Conhecimento — proposta própria, mesma natureza de StatusAcidente acima.
public enum GravidadeAcidente
{
    SemAfastamento = 1,
    ComAfastamento = 2,
    IncapacidadePermanenteParcial = 3,
    IncapacidadePermanenteTotal = 4,
    Obito = 5
}

// DDS não tem seção própria na Base de Conhecimento (módulo pedido pelo usuário em 2026-08-20,
// fora do MVP da §47) — mesma lacuna já registrada em StatusApr/StatusPgr/StatusPt/
// StatusInspecao. Proposta própria.
public enum StatusDds
{
    EmAndamento = 1,
    Concluido = 2
}

// Sessão/Turma de Treinamento (04/09) — mesmo vocabulário de StatusDds.
public enum StatusSessaoTreinamento
{
    EmAndamento = 1,
    Concluida = 2
}

// Evidência de presença no DDS — a Fase 1 (2026-08-24) previa "assinatura/foto fora do escopo"
// (ver comentário original em DdsParticipante); trazido para o escopo a pedido do usuário no mesmo
// dia. Pessoa/DocumentoAssinado preservados só para exibir o histórico de registros anteriores a
// 2026-08-31 — a partir dessa data a evidência de presença passou a ser exclusivamente Biometria
// (pedido do usuário), reaproveitando o leitor Futronic FS80H já usado no Motor de Assinatura
// Eletrônica (ver IAutenticacaoBiometriaLocalService).
public enum TipoFotoParticipante
{
    Pessoa = 1,
    DocumentoAssinado = 2,
    Biometria = 3
}

// DDS Semanal (31/08) — reformulação para seguir o modelo "Registro Semanal de DDS" do usuário
// (documento em papel, 2 layouts): cada semana de uma obra tem um registro para empregados próprios
// e, quando aplicável, outro para terceirizados — nunca misturados no mesmo documento (o papel já
// separa por página/formulário inteiro, com bloco de assinatura da empresa terceirizada só na
// segunda versão).
public enum TipoDdsSemanal
{
    Proprios = 1,
    Terceirizados = 2
}

public enum StatusDdsSemanal
{
    EmAndamento = 1,
    Concluida = 2
}

// Motor Central de Alertas + Cadastro de Ativos (requisito do usuário, 2026-08-25): entidade única
// AtivoSst (ver Domain/Entidades/AtivoSst.cs) com este campo discriminador — em vez de duas tabelas
// separadas (Extintor/Equipamento) — para permitir adicionar novos tipos de ativo no futuro sem
// migration de schema. Distinto de TipoModuloAlerta.Extintor/Equipamento (que classifica o módulo
// dentro do motor de alertas): este enum classifica o registro dentro da tabela AtivoSst.
public enum TipoAtivo
{
    Extintor = 1,
    Equipamento = 2
}

// Motor Central de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md, requisito do usuário
// em 2026-08-25): arquitetura final após 2 revisões — biometria digital (leitor FIDO2 por obra) é o
// método principal, crachá/QR+PIN é a reserva automática, WebAuthn por celular próprio é opcional.
// Vocabulário próprio, sem seção literal da Base de Conhecimento.
public enum StatusDocumentoAssinatura
{
    EmAndamento = 1,
    Finalizado = 2,
    Cancelado = 3
}

// Método efetivamente usado por UM signatário em UMA assinatura — distinto de
// MetodoAutenticacaoObra (que é o cardápio de métodos habilitados na obra como um todo).
// CrachaPin/QrCodePin/WebAuthnCelular removidos do sistema em 31/08 (decisão do usuário: único
// método de assinatura é a digital via leitor Futronic FS80H). Ainda em fase de testes, sem nenhuma
// assinatura real registrada por esses métodos — removidos por completo (não reservados), ao
// contrário do que se faria se já houvesse dado histórico a preservar.
public enum MetodoAutenticacaoAssinatura
{
    Biometria = 1,
    // Assinatura em um clique do usuário logado (ex.: entregador de EPI assinando com a própria
    // sessão) — não é um método do "cardápio" por obra (MetodoAutenticacaoObra), pois não depende
    // de hardware/kiosque: está sempre disponível para quem já está autenticado no app.
    SessaoLogada = 5
}

// [Flags] em Obra.MetodosAutenticacaoHabilitados: cada obra decide se aceita assinatura (Biometria,
// via Futronic) ou não (Nenhum). CrachaPin/QrCodePin/WebAuthnCelular removidos em 31/08 junto com os
// métodos correspondentes (ver MetodoAutenticacaoAssinatura acima).
[Flags]
public enum MetodoAutenticacaoObra
{
    Nenhum = 0,
    Biometria = 1
}

// Ficha de EPI reformulada (docs/superpowers/specs/2026-08-27-ficha-epi-reformulada-design.md) —
// espelha exatamente as opções da coluna "Motivo" do modelo oficial AHBT-FIC-SSO-XXX-00.
public enum MotivoEntregaEpi
{
    Inicial,
    Dano,
    Extravio,
    Vencimento,
    TrocaDeFuncao,
}

// Fase 3 da reformulação do módulo EPI (estoque segmentado por Obra) — classifica cada linha do
// ledger MovimentacaoEstoqueEpi. SaidaEntrega/DevolucaoEntrada são geradas automaticamente pelos
// commands de EntregaEpi; EntradaManual e AjusteManual vêm da tela de estoque.
public enum TipoMovimentacaoEstoqueEpi
{
    EntradaManual = 0,
    SaidaEntrega = 1,
    DevolucaoEntrada = 2,
    AjusteManual = 3,
}

// EPC (pedido do usuário, 04/09) — mesmo vocabulário de TipoMovimentacaoEstoqueEpi, só que
// "SaidaInstalacao"/"RetornoRemocao" em vez de "SaidaEntrega"/"DevolucaoEntrada", já que o EPC não é
// entregue a um funcionário — é instalado/removido de uma Obra.
public enum TipoMovimentacaoEstoqueEpc
{
    EntradaManual = 0,
    SaidaInstalacao = 1,
    RetornoRemocao = 2,
    AjusteManual = 3,
}

public enum StatusInspecaoEpc
{
    Conforme = 1,
    NaoConforme = 2,
}

// Motor de Aplicabilidade Legal (requisito do usuário, 2026-08-29) — classifica o requisito legal
// cadastrado por qual tipo de obrigação ele gera quando aplicável. Vocabulário próprio: a Base de
// Conhecimento não define essa taxonomia, e o conteúdo jurídico real (quais normas, quais critérios)
// não é gerado por este sistema — só o cadastro estruturado que QSMS/jurídico preenche e valida.
public enum CategoriaRequisitoLegal
{
    Treinamento = 1,
    Epi = 2,
    Exame = 3,
    Documento = 4,
    Inspecao = 5
}

public enum StatusRequisitoLegal
{
    Ativo = 1,
    Revogado = 2
}

// Um requisito legal pode ter vários critérios (qualquer um satisfeito já torna aplicável — lógica
// OU, decisão própria de escopo): cada critério aponta para UM fator (Perigo do PGR, Função,
// Equipamento por TipoAtivo, ou resposta de um item do questionário por obra).
public enum TipoCriterioAplicabilidade
{
    Perigo = 1,
    Funcao = 2,
    Equipamento = 3,
    ItemQuestionario = 4
}

// Módulo CIPA (NR-5, requisito do usuário, 2026-08-31) — ver disclosure em Domain/Entidades/Cipa/Cipa.cs.
public enum StatusProcessoEleitoralCipa
{
    Convocado = 1,
    InscricoesAbertas = 2,
    InscricoesEncerradas = 3,
    VotacaoRealizada = 4,
    Apurado = 5,
    Encerrado = 6
}

public enum StatusCandidatoCipa
{
    Inscrito = 1,
    Deferido = 2,
    Indeferido = 3,
    Eleito = 4,
    Suplente = 5,
    NaoEleito = 6
}

public enum OrigemMembroCipa
{
    Empregador = 1,
    Empregado = 2
}

public enum CargoMembroCipa
{
    Titular = 1,
    Suplente = 2,
    Presidente = 3,
    VicePresidente = 4,
    Secretario = 5
}

public enum TipoReuniaoCipa
{
    Ordinaria = 1,
    Extraordinaria = 2
}

public enum StatusReuniaoCipa
{
    Agendada = 1,
    Realizada = 2,
    AtaRegistrada = 3
}
