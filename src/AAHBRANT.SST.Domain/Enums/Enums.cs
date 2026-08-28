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
    EquipamentoVencido = 20
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
    Outro = 11
}

public enum StatusAlerta
{
    Aberto = 1,
    EmTratamento = 2,
    Escalonado = 3,
    Resolvido = 4,
    Ignorado = 5
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
public enum PapelAssinaturaApr
{
    Elaborador = 1,
    Executante = 2,
    Aprovador = 3
}

// "Autorização" e "encerramento" (§18) — documento não lista vocabulário literal de status para
// a PT (mesma lacuna já registrada em StatusApr/StatusPgr/StatusControleRisco) — proposta própria.
public enum StatusPt
{
    EmElaboracao = 1,
    Autorizada = 2,
    Encerrada = 3
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
// "Aberta → Em tratamento → Aguardando validação → Encerrada".
public enum StatusNaoConformidade
{
    Aberta = 1,
    EmTratamento = 2,
    AguardandoValidacao = 3,
    Encerrada = 4
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

// Seção 32 da Base de Conhecimento (linha 811) — vocabulário literal de status: "Conforme/Não conforme".
public enum StatusRequisitoLegal
{
    Conforme = 1,
    NaoConforme = 2
}

// Seção 31 da Base de Conhecimento (linhas 767-769) — vocabulário literal de status:
// "Rascunho → Em aprovação → Vigente → Obsoleto → Cancelado".
public enum StatusDocumentoGestao
{
    Rascunho = 1,
    EmAprovacao = 2,
    Vigente = 3,
    Obsoleto = 4,
    Cancelado = 5
}

// DDS não tem seção própria na Base de Conhecimento (módulo pedido pelo usuário em 2026-08-20,
// fora do MVP da §47) — mesma lacuna já registrada em StatusApr/StatusPgr/StatusPt/
// StatusInspecao. Proposta própria.
public enum StatusDds
{
    EmAndamento = 1,
    Concluido = 2
}

// Evidência de presença no DDS — a Fase 1 (2026-08-24) previa "assinatura/foto fora do escopo"
// (ver comentário original em DdsParticipante); trazido para o escopo a pedido do usuário no mesmo
// dia. Vocabulário próprio, sem seção literal da Base de Conhecimento: quem conduz o DDS escolhe
// bater foto da pessoa presente OU do documento (lista de presença) assinado por ela.
public enum TipoFotoParticipante
{
    Pessoa = 1,
    DocumentoAssinado = 2
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
public enum MetodoAutenticacaoAssinatura
{
    Biometria = 1,
    CrachaPin = 2,
    QrCodePin = 3,
    WebAuthnCelular = 4,
    // Assinatura em um clique do usuário logado (ex.: entregador de EPI assinando com a própria
    // sessão) — não é um método do "cardápio" por obra (MetodoAutenticacaoObra), pois não depende
    // de hardware/kiosque: está sempre disponível para quem já está autenticado no app.
    SessaoLogada = 5
}

// [Flags] em Obra.MetodosAutenticacaoHabilitados: cada obra decide quais métodos aceita (ex.: obra
// sem leitor biométrico ainda comprado opera só com CrachaPin até o hardware chegar — ver §3 do doc,
// "usar como principal temporário até o hardware ser confirmado").
[Flags]
public enum MetodoAutenticacaoObra
{
    Nenhum = 0,
    Biometria = 1,
    CrachaPin = 2,
    QrCodePin = 4,
    WebAuthnCelular = 8
}

// Etapa 13 do Motor de Assinatura Eletrônica — CredencialWebAuthn.Tipo. LeitorObra e CelularProprio
// usam a mesma cerimônia FIDO2/WebAuthn (Fido2AutenticacaoStrategy); só o autenticador físico muda:
// leitor biométrico compartilhado da obra (credencial "discoverable", vários trabalhadores por
// dispositivo) vs. celular do próprio trabalhador (credencial de um único dono).
public enum TipoAutenticadorWebAuthn
{
    LeitorObra = 1,
    CelularProprio = 2
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
