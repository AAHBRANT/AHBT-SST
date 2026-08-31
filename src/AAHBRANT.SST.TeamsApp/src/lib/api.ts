import { API_BASE_URL } from './apiBase';
import { montarHeadersAuth } from './authHeaders';
import { syncFetchBlob, syncFetchJson, syncMutateJson, syncMutateMultipart } from './offline/syncEngine';

export const StatusObra = {
  Planejada: 1,
  EmAndamento: 2,
  Paralisada: 3,
  Concluida: 4,
  Encerrada: 5,
} as const;

export const statusObraLabel: Record<number, string> = {
  1: 'Planejada',
  2: 'Em andamento',
  3: 'Paralisada',
  4: 'Concluída',
  5: 'Encerrada',
};

export interface Obra {
  id: string;
  codigo: string;
  nome: string;
  cliente?: string | null;
  status: number;
  dataInicio?: string | null;
  dataPrevisaoTermino?: string | null;
  dataTerminoReal?: string | null;
  endereco?: string | null;
  cidade?: string | null;
  uf?: string | null;
  cnpj?: string | null;
  temLogo: boolean;
}

export type NovaObra = Omit<Obra, 'id' | 'dataTerminoReal' | 'temLogo'>;

export const TipoVinculo = {
  Clt: 1,
  Terceirizado: 2,
  Autonomo: 3,
  Estagiario: 4,
} as const;

export const tipoVinculoLabel: Record<number, string> = {
  1: 'CLT',
  2: 'Terceirizado',
  3: 'Autônomo',
  4: 'Estagiário',
};

export interface Trabalhador {
  id: string;
  obraId: string;
  setorId?: string | null;
  equipeId?: string | null;
  funcaoId: string;
  nome: string;
  matricula: string;
  cpf: string;
  vinculo: number;
  dataAdmissao: string;
  dataDemissao?: string | null;
  telegramVinculado: boolean;
  telegramCodigoVinculo?: string | null;
  turno?: string | null;
  temFoto: boolean;
  temBiometria: boolean;
}

export type NovoTrabalhador = Omit<
  Trabalhador,
  'id' | 'dataDemissao' | 'telegramVinculado' | 'telegramCodigoVinculo' | 'temFoto' | 'temBiometria'
>;

export interface GerarVinculoTelegramResultado {
  codigo: string;
  linkTelegram: string;
}

export const TipoExameAso = {
  Admissional: 1,
  Periodico: 2,
  RetornoAoTrabalho: 3,
  MudancaDeFuncao: 4,
  Demissional: 5,
} as const;

export const tipoExameAsoLabel: Record<number, string> = {
  1: 'Admissional',
  2: 'Periódico',
  3: 'Retorno ao trabalho',
  4: 'Mudança de função',
  5: 'Demissional',
};

export const ResultadoAso = {
  Apto: 1,
  AptoComRestricao: 2,
  Inapto: 3,
  Pendente: 4,
} as const;

export const resultadoAsoLabel: Record<number, string> = {
  1: 'Apto',
  2: 'Apto com restrição',
  3: 'Inapto',
  4: 'Pendente',
};

export interface Aso {
  id: string;
  trabalhadorId: string;
  tipo: number;
  dataExame: string;
  dataValidade: string;
  resultadoStatus: number;
  observacoesClinicas?: string | null;
  medicoNome?: string | null;
  medicoCrm?: string | null;
}

export type NovoAso = Omit<Aso, 'id'>;

// PR-SST-003 — exames complementares do PCMSO (audiometria, acuidade visual etc.), vinculados
// opcionalmente a um ASO.
export const TipoExameComplementar = {
  Audiometria: 1,
  AcuidadeVisual: 2,
  Espirometria: 3,
  Laboratoriais: 4,
  AvaliacaoClinica: 5,
  ExameEspecifico: 6,
} as const;

export const tipoExameComplementarLabel: Record<number, string> = {
  1: 'Audiometria',
  2: 'Acuidade visual',
  3: 'Espirometria',
  4: 'Laboratoriais',
  5: 'Avaliação clínica',
  6: 'Exame específico',
};

export interface ExameComplementar {
  id: string;
  trabalhadorId: string;
  asoId?: string | null;
  tipo: number;
  dataRealizacao: string;
  dataValidade: string;
  resultado: string;
  observacoes?: string | null;
  responsavelTecnico?: string | null;
}

export type NovoExameComplementar = Omit<ExameComplementar, 'id'>;
export type AtualizarExameComplementarPayload = ExameComplementar;

// PR-SST-003 — aptidão para atividade crítica (ex.: trabalho em altura, espaço confinado),
// distinta do ASO geral: reaproveita ResultadoAso (Apto/Apto com restrição/Inapto/Pendente).
export interface Aptidao {
  id: string;
  trabalhadorId: string;
  atividadeCritica: string;
  aptidao: number;
  dataAvaliacao: string;
  dataValidade?: string | null;
  medicoResponsavel?: string | null;
  observacoes?: string | null;
}

export type NovaAptidao = Omit<Aptidao, 'id'>;
export type AtualizarAptidaoPayload = Aptidao;

export interface Funcao {
  id: string;
  nome: string;
  cboCodigo?: string | null;
  descricao?: string | null;
}

export type NovaFuncao = Omit<Funcao, 'id'>;

export interface Setor {
  id: string;
  obraId: string;
  obraNome: string;
  nome: string;
}

export type NovoSetor = Omit<Setor, 'id' | 'obraNome'>;

export interface Equipe {
  id: string;
  setorId: string;
  setorNome: string;
  obraId: string;
  obraNome: string;
  nome: string;
  encarregadoId?: string | null;
  encarregadoNome?: string | null;
  quantidadeTrabalhadores: number;
}

export type NovaEquipe = Omit<Equipe, 'id' | 'setorNome' | 'obraId' | 'obraNome' | 'encarregadoNome' | 'quantidadeTrabalhadores'>;

export interface CursoTreinamento {
  id: string;
  nome: string;
  normaReferencia?: string | null;
  cargaHorariaMinima: number;
  validadeEmMeses: number;
}

export type NovoCursoTreinamento = Omit<CursoTreinamento, 'id'>;

export interface Treinamento {
  id: string;
  trabalhadorId: string;
  cursoTreinamentoId: string;
  dataRealizacao: string;
  dataValidade: string;
  cargaHorariaRealizada: number;
  instituicaoInstrutor?: string | null;
  numeroCertificado?: string | null;
}

export type NovoTreinamento = Omit<Treinamento, 'id'>;

// Módulo de Requisitos Legais — Motor de Aplicabilidade Legal (requisito do usuário, 2026-08-29).
// Fase 1 (fundação de dados): cadastro do requisito e seus critérios de aplicabilidade, catálogo do
// questionário e matriz de obrigatoriedade de treinamento por função. O cruzamento de fato (o
// "motor" que avalia cada obra) é uma fase seguinte, ainda não implementada.
export const CategoriaRequisitoLegal = {
  Treinamento: 1,
  Epi: 2,
  Exame: 3,
  Documento: 4,
  Inspecao: 5,
} as const;

export const categoriaRequisitoLegalLabel: Record<number, string> = {
  1: 'Treinamento',
  2: 'EPI',
  3: 'Exame',
  4: 'Documento',
  5: 'Inspeção',
};

export const StatusRequisitoLegal = {
  Ativo: 1,
  Revogado: 2,
} as const;

export const statusRequisitoLegalLabel: Record<number, string> = {
  1: 'Ativo',
  2: 'Revogado',
};

export const TipoCriterioAplicabilidade = {
  Perigo: 1,
  Funcao: 2,
  Equipamento: 3,
  ItemQuestionario: 4,
} as const;

export const tipoCriterioAplicabilidadeLabel: Record<number, string> = {
  1: 'Perigo (PGR)',
  2: 'Função',
  3: 'Equipamento',
  4: 'Item do questionário',
};

export interface RequisitoLegal {
  id: string;
  norma: string;
  artigo?: string | null;
  titulo: string;
  descricao: string;
  categoria: number;
  status: number;
  fonte?: string | null;
}

export type NovoRequisitoLegal = Omit<RequisitoLegal, 'id' | 'status'>;
export type AtualizarRequisitoLegalPayload = Omit<RequisitoLegal, 'id'>;

export interface CriterioAplicabilidadeInput {
  tipo: number;
  perigoId?: string | null;
  funcaoId?: string | null;
  tipoEquipamento?: number | null;
  itemQuestionarioAplicabilidadeId?: string | null;
}

export interface RequisitoLegalCriterio extends CriterioAplicabilidadeInput {
  id: string;
  perigoNome?: string | null;
  funcaoNome?: string | null;
  itemQuestionarioPergunta?: string | null;
}

export interface RequisitoLegalDetalhe {
  requisito: RequisitoLegal;
  criterios: RequisitoLegalCriterio[];
}

export interface ItemQuestionarioAplicabilidade {
  id: string;
  pergunta: string;
  textoApoio?: string | null;
}

export interface RespostaQuestionarioObra {
  itemId: string;
  pergunta: string;
  textoApoio?: string | null;
  resposta: boolean | null;
  observacao?: string | null;
}

export interface CatalogoEpi {
  id: string;
  nome: string;
  fabricante?: string | null;
  certificadoAprovacaoNumero?: string | null;
  certificadoAprovacaoValidade?: string | null;
  vidaUtilEmMeses: number;
  // Soma do estoque do EPI em todas as Obras (Fase 3) — somente leitura; não editável via
  // catálogo. Ver api.estoquesEpi para o estoque segmentado por Obra.
  saldoTotal: number;
}

export type NovoCatalogoEpi = Omit<CatalogoEpi, 'id' | 'saldoTotal'>;
export type AtualizarCatalogoEpi = Omit<CatalogoEpi, 'saldoTotal'>;

// Fase 3 — estoque de EPI segmentado por Obra (substitui o antigo saldo único global).
export const TipoMovimentacaoEstoqueEpi = {
  EntradaManual: 0,
  SaidaEntrega: 1,
  DevolucaoEntrada: 2,
  AjusteManual: 3,
} as const;

export const tipoMovimentacaoEstoqueEpiLabel: Record<number, string> = {
  0: 'Entrada manual',
  1: 'Saída (entrega)',
  2: 'Devolução',
  3: 'Ajuste manual',
};

export interface EstoqueEpiPorObra {
  catalogoEpiId: string;
  catalogoEpiNome: string;
  fabricante?: string | null;
  saldo: number;
}

export interface MovimentacaoEstoqueEpi {
  id: string;
  tipo: number;
  quantidade: number;
  saldoResultante: number;
  createdAtUtc: string;
  observacao?: string | null;
  entregaEpiId?: string | null;
}

export interface RegistrarEntradaEstoqueEpi {
  catalogoEpiId: string;
  obraId: string;
  quantidade: number;
  observacao?: string | null;
}

export interface AjustarEstoqueEpi {
  catalogoEpiId: string;
  obraId: string;
  novoSaldo: number;
  observacao: string;
}

export const MotivoEntregaEpi = {
  Inicial: 0,
  Dano: 1,
  Extravio: 2,
  Vencimento: 3,
  TrocaDeFuncao: 4,
} as const;

export const motivoEntregaEpiLabel: Record<number, string> = {
  0: 'Entrega inicial',
  1: 'Dano',
  2: 'Extravio',
  3: 'Vencimento',
  4: 'Troca de função',
};

export interface EntregaEpi {
  id: string;
  trabalhadorId: string;
  catalogoEpiId: string;
  dataEntrega: string;
  dataDevolucao?: string | null;
  dataValidade?: string | null;
  quantidade: number;
  quantidadeDevolucao?: number | null;
  vistoConsorcioResponsavel?: string | null;
  motivo?: string | null;
  observacoes?: string | null;
  motivoTipo: number | null;
  numeroListaPresencaNr6?: string | null;
  dataTreinamentoNr6?: string | null;
}

export type NovaEntregaEpi = Omit<EntregaEpi, 'id'> & { motivoTipo: number };
export type AtualizarEntregaEpi = EntregaEpi & { motivoTipo: number };

export interface Atividade {
  id: string;
  obraId: string;
  nome: string;
  descricao?: string | null;
}

export type NovaAtividade = Omit<Atividade, 'id'>;

export interface EligibilityCheckItem {
  requisito: string;
  atendido: boolean;
  critico: boolean;
  detalhe?: string | null;
}

export interface EligibilityResult {
  liberado: boolean;
  itens: EligibilityCheckItem[];
  motivoBloqueioResumo?: string | null;
}

export interface AvaliarElegibilidadeQuery {
  trabalhadorId: string;
  obraId: string;
  atividadeId?: string | null;
  tipoAutorizacaoId?: string | null;
  permissaoTrabalhoId?: string | null;
  contextoModulo: string;
}

export interface Perigo {
  id: string;
  nome: string;
  agente?: string | null;
  fonte?: string | null;
  descricao?: string | null;
}

export type NovoPerigo = Omit<Perigo, 'id'>;

export const NivelRisco = {
  Trivial: 1,
  Baixo: 2,
  Moderado: 3,
  Alto: 4,
  Critico: 5,
} as const;

export const nivelRiscoLabel: Record<number, string> = {
  1: 'Trivial',
  2: 'Baixo',
  3: 'Moderado',
  4: 'Alto',
  5: 'Crítico',
};

export const StatusControleRisco = {
  Pendente: 1,
  EmAndamento: 2,
  Concluido: 3,
} as const;

export const statusControleRiscoLabel: Record<number, string> = {
  1: 'Pendente',
  2: 'Em andamento',
  3: 'Concluído',
};

export interface MatrizRiscoCelula {
  probabilidade: number;
  severidade: number;
  nivelRisco: number;
}

export interface MatrizRiscoConfig {
  id: string;
  nome: string;
  numNiveisProbabilidade: number;
  numNiveisSeveridade: number;
  celulas: MatrizRiscoCelula[];
}

export type NovaMatrizRiscoConfig = Omit<MatrizRiscoConfig, 'id'>;

export interface Risco {
  id: string;
  atividadeId: string;
  perigoId: string;
  ambiente?: string | null;
  exposicao?: string | null;
  consequencia?: string | null;
  probabilidade: number;
  severidade: number;
  nivelRisco: number;
  controlesExistentes?: string | null;
  controlesAdicionais?: string | null;
  responsavelUsuarioId?: string | null;
  prazo?: string | null;
  status: number;
  trabalhadoresExpostosIds: string[];
}

export type NovoRisco = Omit<Risco, 'id' | 'nivelRisco'>;

export const StatusPgr = {
  EmElaboracao: 1,
  Vigente: 2,
  EmRevisao: 3,
  Encerrado: 4,
} as const;

export const statusPgrLabel: Record<number, string> = {
  1: 'Em elaboração',
  2: 'Vigente',
  3: 'Em revisão',
  4: 'Encerrado',
};

export interface Pgr {
  id: string;
  obraId: string;
  nome: string;
  descricao?: string | null;
  dataElaboracao: string;
  dataProximaRevisao?: string | null;
  responsavelUsuarioId?: string | null;
  status: number;
}

export type NovoPgr = Omit<Pgr, 'id'>;

export interface RiscoClassificado {
  riscoId: string;
  perigoNome: string;
  nivelRisco: number;
  controlesExistentes?: string | null;
  controlesAdicionais?: string | null;
  status: number;
}

export interface AtividadeCaracterizada {
  atividadeId: string;
  atividadeNome: string;
  riscos: RiscoClassificado[];
}

export interface PlanoAcaoItem {
  id: string;
  pgrId: string;
  riscoId?: string | null;
  descricao: string;
  responsavelUsuarioId?: string | null;
  prazo?: string | null;
  dataConclusao?: string | null;
  status: number;
}

export type NovoPlanoAcaoItem = Omit<PlanoAcaoItem, 'id' | 'dataConclusao'>;

export interface PgrRevisao {
  id: string;
  pgrId: string;
  numeroRevisao: number;
  dataRevisao: string;
  motivo: string;
  responsavelUsuarioId?: string | null;
}

export type NovaPgrRevisao = Omit<PgrRevisao, 'id' | 'numeroRevisao'>;

export interface PgrDetalhe {
  pgr: Pgr;
  atividades: AtividadeCaracterizada[];
  planoDeAcao: PlanoAcaoItem[];
  revisoes: PgrRevisao[];
}

export const StatusPcmso = {
  EmElaboracao: 1,
  Vigente: 2,
  EmRevisao: 3,
  Encerrado: 4,
} as const;

export const statusPcmsoLabel: Record<number, string> = {
  1: 'Em elaboração',
  2: 'Vigente',
  3: 'Em revisão',
  4: 'Encerrado',
};

export const TipoArea = {
  AreaDeTrabalho: 1,
  ZonaDeRisco: 2,
  Armazenamento: 3,
} as const;

export const tipoAreaLabel: Record<number, string> = {
  1: 'Área de trabalho',
  2: 'Zona de risco',
  3: 'Armazenamento',
};

export const StatusArea = {
  Ativa: 1,
  Inativa: 2,
  Bloqueada: 3,
} as const;

export const statusAreaLabel: Record<number, string> = {
  1: 'Ativa',
  2: 'Inativa',
  3: 'Bloqueada',
};

export interface AreaSst {
  id: string;
  codigo: string;
  nome: string;
  tipo: number;
  obraId: string;
  detalhesLocalizacao?: string | null;
  riscos: string[];
  requisitos: string[];
  status: number;
}

export type NovaAreaSst = Omit<AreaSst, 'id'>;

export const TipoTag = {
  Ntag215: 1,
  Ntag213: 2,
  QrCode: 3,
  Rfid: 4,
} as const;

export const tipoTagLabel: Record<number, string> = {
  1: 'NTAG215',
  2: 'NTAG213',
  3: 'QR Code',
  4: 'RFID',
};

export const StatusTag = {
  Disponivel: 1,
  Vinculada: 2,
  Desativada: 3,
  Perdida: 4,
} as const;

export const statusTagLabel: Record<number, string> = {
  1: 'Disponível',
  2: 'Vinculada',
  3: 'Desativada',
  4: 'Perdida',
};

export const TipoEntidadeVinculada = {
  Area: 1,
  Ativo: 2,
  Trabalhador: 3,
} as const;

export const tipoEntidadeVinculadaLabel: Record<number, string> = {
  1: 'Área',
  2: 'Ativo',
  3: 'Trabalhador',
};

export interface TagIdentificacao {
  id: string;
  uid: string;
  tipo: number;
  status: number;
  entidadeVinculadaTipo?: number | null;
  entidadeVinculadaId?: string | null;
}

export type NovaTagIdentificacao = { uid: string; tipo: number };

export interface ResolverTagDto {
  tagId: string;
  uid: string;
  tipo: number;
  status: number;
  entidadeVinculadaTipo?: number | null;
  entidadeVinculadaId?: string | null;
  entidadeVinculadaNome?: string | null;
}

export interface AreaPublicaDto {
  codigo: string;
  nome: string;
  tipo: number;
  status: number;
  riscos: string[];
  requisitos: string[];
  detalhesLocalizacao?: string | null;
}

export const StatusApr = {
  EmElaboracao: 1,
  AguardandoAprovacao: 2,
  Aprovada: 3,
  Reprovada: 4,
  Encerrada: 5,
} as const;

export const statusAprLabel: Record<number, string> = {
  1: 'Em elaboração',
  2: 'Aguardando aprovação',
  3: 'Aprovada',
  4: 'Reprovada',
  5: 'Encerrada',
};

// Renomeado para bater com os 3 papéis literais do formulário "APR REV.02" (planilha do usuário,
// 2026-08-29): Envolvido é a linha de "Ass./Visto" da equipe exposta; Elaboração e Supervisão são os
// dois blocos formais do rodapé do documento.
export const PapelAssinaturaApr = {
  Envolvido: 1,
  Elaboracao: 2,
  Supervisao: 3,
} as const;

export const papelAssinaturaAprLabel: Record<number, string> = {
  1: 'Envolvido',
  2: 'Elaboração / SST / Responsável Técnico',
  3: 'Supervisão / Encarregado / Engenharia',
};

// Matriz de critérios própria da APR (aba "Matriz de Risco" da planilha) — fórmula fixa
// (1-4 Baixo, 5-9 Moderado, 10-15 Alto, 16-25 Crítico), distinta da matriz configurável genérica
// do módulo Riscos (NivelRisco).
export const NivelRiscoApr = {
  Baixo: 1,
  Moderado: 2,
  Alto: 3,
  Critico: 4,
} as const;

export const nivelRiscoAprLabel: Record<number, string> = {
  1: 'BAIXO',
  2: 'MODERADO',
  3: 'ALTO',
  4: 'CRÍTICO',
};

// Cores idênticas à formatação condicional da planilha original.
export const nivelRiscoAprCor: Record<number, string> = {
  1: '#A9D18E',
  2: '#FFD966',
  3: '#F4B183',
  4: '#C00000',
};

export interface Apr {
  id: string;
  numeroApr?: string | null;
  atividadeId: string;
  atividadeNome: string;
  obraNome?: string | null;
  local: string;
  maquinasEquipamentos?: string | null;
  pgrReferencia?: string | null;
  equipeId?: string | null;
  equipeNome?: string | null;
  data: string;
  validade?: string | null;
  status: number;
  aprovadoPorUsuarioId?: string | null;
  aprovadoPorUsuarioNome?: string | null;
  dataAprovacao?: string | null;
  motivoReprovacao?: string | null;
}

export interface NovaApr {
  numeroApr?: string | null;
  atividadeId: string;
  local: string;
  maquinasEquipamentos?: string | null;
  pgrReferencia?: string | null;
  equipeId?: string | null;
  data: string;
  validade?: string | null;
  responsaveisIds: string[];
}

export type AtualizarAprPayload = NovaApr & { id: string };

export interface AprEtapaRisco {
  id: string;
  aprEtapaId: string;
  perigoEventoPerigoso: string;
  fonteCircunstancia?: string | null;
  possiveisLesoes?: string | null;
  trabalhadoresExpostos?: string | null;
  probabilidadeInicial: number;
  severidadeInicial: number;
  nivelRiscoInicial: number;
  medidasPrevencao?: string | null;
  responsavel?: string | null;
  probabilidadeResidual: number;
  severidadeResidual: number;
  nivelRiscoResidual: number;
}

export type NovoAprEtapaRisco = Omit<AprEtapaRisco, 'id' | 'nivelRiscoInicial' | 'nivelRiscoResidual'>;

export interface AprEtapa {
  id: string;
  aprId: string;
  ordem: number;
  descricao: string;
  riscos: AprEtapaRisco[];
}

export interface NovaAprEtapa {
  aprId: string;
  ordem: number;
  descricao: string;
}

export interface AprResponsavel {
  id: string;
  aprId: string;
  trabalhadorId: string;
  trabalhadorNome: string;
  trabalhadorFuncaoNome?: string | null;
}

export interface AprAssinatura {
  id: string;
  aprId: string;
  trabalhadorId: string;
  trabalhadorNome: string;
  papel: number;
  dataAssinatura: string;
}

export interface NovaAprAssinatura {
  aprId: string;
  trabalhadorId: string;
  papel: number;
}

export interface AprDetalhe {
  apr: Apr;
  etapas: AprEtapa[];
  responsaveis: AprResponsavel[];
  assinaturas: AprAssinatura[];
}

// PT REV.01 — reformulação literal do formulário "PT – PERMISSÃO DE TRABALHO" (planilha do
// usuário, 2026-08-29). Mesmo princípio da APR REV.02: os catálogos fixos do documento (§2 a §5)
// viram enums no backend (não catálogos editáveis), refletidos aqui como const objects + labels.
export const StatusPt = {
  EmElaboracao: 1,
  Autorizada: 2,
  Suspensa: 3,
  Encerrada: 4,
} as const;

export const statusPtLabel: Record<number, string> = {
  1: 'Em elaboração',
  2: 'Autorizada',
  3: 'Suspensa',
  4: 'Encerrada',
};

export const ItemPreRequisitoPt = {
  AprEspecificaRevisadaDisponivel: 1,
  PgrInventarioRiscosCompativel: 2,
  InspecoesChecklistsEquipamentosValidos: 3,
  ProcedimentoInstrucaoTrabalhoAplicavelDisponivel: 4,
  TrabalhadoresCapacitadosAutorizadosAptos: 5,
  PlanoEmergenciaMeiosComunicacaoConhecidos: 6,
} as const;

export const itemPreRequisitoPtLabel: Record<number, string> = {
  1: 'APR específica da atividade revisada e disponível',
  2: 'PGR / Inventário de Riscos compatível com a atividade',
  3: 'Inspeções / checklists dos equipamentos válidos',
  4: 'Procedimento / instrução de trabalho aplicável disponível',
  5: 'Trabalhadores capacitados, autorizados e aptos quando aplicável',
  6: 'Plano de emergência e meios de comunicação conhecidos pela equipe',
};

export const TipoTrabalhoEspecialPt = {
  TrabalhoEmAltura: 1,
  TrabalhoAQuenteFonteIgnicao: 2,
  BloqueioEnergiasPerigosas: 3,
  DemolicaoCortePerfuracao: 4,
  EspacoConfinado: 5,
  EscavacaoValaFundacao: 6,
  TrabalhoProximoTrafegoVias: 7,
  MaquinasEquipamentos: 8,
  EletricidadeIntervencaoEletrica: 9,
  MovimentacaoIcamentoCargas: 10,
  ProdutosQuimicosInflamaveis: 11,
  Outro: 12,
} as const;

export const tipoTrabalhoEspecialPtLabel: Record<number, string> = {
  1: 'Trabalho em altura – NR-35',
  2: 'Trabalho a quente / fonte de ignição',
  3: 'Bloqueio de energias perigosas (LOTO)',
  4: 'Demolição / corte / perfuração',
  5: 'Espaço confinado – NR-33',
  6: 'Escavação / vala / fundação',
  7: 'Trabalho próximo a tráfego / vias',
  8: 'Máquinas e equipamentos',
  9: 'Eletricidade / intervenção elétrica – NR-10',
  10: 'Movimentação e içamento de cargas',
  11: 'Produtos químicos / inflamáveis',
  12: 'Outro',
};

export const ItemVerificacaoPt = {
  AreaIsoladaSinalizadaAcessoControlado: 1,
  AprDiscutidaComEquipeAntesDoInicio: 2,
  InterferenciasExistentesIdentificadas: 3,
  FontesEnergiaIdentificadasBloqueadasTestadas: 4,
  MaquinasFerramentasAcessoriosInspecionados: 5,
  EpcsInstaladosCondicoesUso: 6,
  EpisDisponiveisAdequadosCaValido: 7,
  CondicoesAcessoCirculacaoIluminacaoOrganizacao: 8,
  CondicoesMeteorologicasPermitemExecucaoSegura: 9,
  RiscoQuedaPessoasObjetosControlado: 10,
  RiscoIncendioExplosaoControladoExtintorDisponivel: 11,
  AtmosferaAvaliadaMonitorada: 12,
  EscavacoesTaludesEscoramentosAcessosInspecionados: 13,
  PlanoIcamentoAcessoriosMovimentacaoVerificados: 14,
  VigiaObservadorSinaleiroApoioDefinido: 15,
} as const;

export const itemVerificacaoPtLabel: Record<number, string> = {
  1: 'Área isolada, sinalizada e com acesso controlado?',
  2: 'APR discutida com toda a equipe antes do início?',
  3: 'Interferências existentes identificadas (redes, tubulações, energia, tráfego etc.)?',
  4: 'Fontes de energia identificadas, bloqueadas e testadas quando aplicável?',
  5: 'Máquinas, ferramentas, acessórios e dispositivos inspecionados e adequados?',
  6: 'EPCs instalados e em condições de uso?',
  7: 'EPIs definidos na APR disponíveis, adequados, com CA válido quando aplicável?',
  8: 'Condições de acesso, circulação, iluminação e organização adequadas?',
  9: 'Condições meteorológicas permitem execução segura da atividade?',
  10: 'Risco de queda de pessoas/objetos controlado quando aplicável?',
  11: 'Risco de incêndio/explosão controlado; extintor adequado disponível quando aplicável?',
  12: 'Atmosfera avaliada/monitorada quando aplicável (O₂, inflamáveis e tóxicos)?',
  13: 'Escavações/taludes/escoramentos/acessos inspecionados quando aplicável?',
  14: 'Plano de içamento e acessórios de movimentação verificados quando aplicável?',
  15: 'Vigia, observador, sinaleiro ou trabalhador de apoio definido quando aplicável?',
};

export const RespostaVerificacaoPt = {
  Conforme: 1,
  NaoConforme: 2,
  NaoAplicavel: 3,
} as const;

export const respostaVerificacaoPtLabel: Record<number, string> = {
  1: 'Conforme',
  2: 'Não Conforme',
  3: 'Não Aplicável',
};

export const ItemEpiPt = {
  Capacete: 1,
  Oculos: 2,
  ProtetorFacial: 3,
  ProtetorAuditivo: 4,
  Luvas: 5,
  Calcado: 6,
  Respirador: 7,
  CinturaoTalabarte: 8,
  VestimentaEspecifica: 9,
} as const;

export const itemEpiPtLabel: Record<number, string> = {
  1: 'Capacete',
  2: 'Óculos',
  3: 'Protetor facial',
  4: 'Protetor auditivo',
  5: 'Luvas',
  6: 'Calçado',
  7: 'Respirador',
  8: 'Cinturão/talabarte',
  9: 'Vestimenta específica',
};

export const ItemEpcPt = {
  IsolamentoBarreira: 1,
  GuardaCorpo: 2,
  LinhaDeVida: 3,
  Extintor: 4,
  ExaustaoVentilacao: 5,
  DetectorGases: 6,
  KitResgate: 7,
  Iluminacao: 8,
  Sinalizacao: 9,
} as const;

export const itemEpcPtLabel: Record<number, string> = {
  1: 'Isolamento/barreira',
  2: 'Guarda-corpo',
  3: 'Linha de vida',
  4: 'Extintor',
  5: 'Exaustão/ventilação',
  6: 'Detector de gases',
  7: 'Kit de resgate',
  8: 'Iluminação',
  9: 'Sinalização',
};

export interface PermissaoTrabalho {
  id: string;
  numeroPt?: string | null;
  atividadeId: string;
  atividadeNome: string;
  obraNome?: string | null;
  descricaoAtividade: string;
  local: string;
  empresaExecutante?: string | null;
  equipeId?: string | null;
  equipeNome?: string | null;
  data: string;
  horarioInicio?: string | null;
  horarioFim?: string | null;
  validade?: string | null;
  responsavelExecucaoUsuarioId?: string | null;
  responsavelExecucaoUsuarioNome?: string | null;
  responsavelAreaUsuarioId?: string | null;
  responsavelAreaUsuarioNome?: string | null;
  status: number;
  autorizadoPorUsuarioId?: string | null;
  autorizadoPorUsuarioNome?: string | null;
  dataAutorizacao?: string | null;
  dataAssinaturaExecucao?: string | null;
  responsavelSstUsuarioId?: string | null;
  responsavelSstUsuarioNome?: string | null;
  dataAssinaturaSst?: string | null;
  suspensaPorUsuarioId?: string | null;
  suspensaPorUsuarioNome?: string | null;
  dataSuspensao?: string | null;
  motivoSuspensao?: string | null;
  revalidadaPorUsuarioId?: string | null;
  revalidadaPorUsuarioNome?: string | null;
  dataRevalidacao?: string | null;
  encerradaPorUsuarioId?: string | null;
  encerradaPorUsuarioNome?: string | null;
  dataEncerramento?: string | null;
  observacoesEncerramento?: string | null;
  outrosEpis?: string | null;
  outrosEpcs?: string | null;
}

export interface NovaPermissaoTrabalho {
  numeroPt?: string | null;
  atividadeId: string;
  descricaoAtividade: string;
  local: string;
  empresaExecutante?: string | null;
  equipeId?: string | null;
  data: string;
  horarioInicio?: string | null;
  horarioFim?: string | null;
  validade?: string | null;
  responsavelExecucaoUsuarioId?: string | null;
  responsavelAreaUsuarioId?: string | null;
  responsaveisIds: string[];
}

export interface AtualizarPermissaoTrabalhoPayload extends NovaPermissaoTrabalho {
  id: string;
}

export interface PermissaoTrabalhoPreRequisito {
  id: string;
  permissaoTrabalhoId: string;
  item: number;
  atendido: boolean;
}

export interface PermissaoTrabalhoTipoTrabalho {
  id: string;
  permissaoTrabalhoId: string;
  tipo: number;
  descricaoOutro?: string | null;
}

export interface TipoTrabalhoPtInput {
  tipo: number;
  descricaoOutro?: string | null;
}

export interface PermissaoTrabalhoVerificacao {
  id: string;
  permissaoTrabalhoId: string;
  item: number;
  resposta?: number | null;
}

export interface PermissaoTrabalhoEpi {
  id: string;
  permissaoTrabalhoId: string;
  item: number;
  complemento?: string | null;
}

export interface EpiPtInput {
  item: number;
  complemento?: string | null;
}

export interface PermissaoTrabalhoEpc {
  id: string;
  permissaoTrabalhoId: string;
  item: number;
}

export interface PermissaoTrabalhoRiscoCritico {
  id: string;
  permissaoTrabalhoId: string;
  riscoCondicao: string;
  controleComplementar?: string | null;
  responsavelEvidencia?: string | null;
}

export interface NovaPermissaoTrabalhoRiscoCritico {
  permissaoTrabalhoId: string;
  riscoCondicao: string;
  controleComplementar?: string | null;
  responsavelEvidencia?: string | null;
}

export interface PermissaoTrabalhoResponsavel {
  id: string;
  permissaoTrabalhoId: string;
  trabalhadorId: string;
  trabalhadorNome: string;
  trabalhadorFuncaoNome?: string | null;
}

export interface PermissaoTrabalhoDetalhe {
  permissaoTrabalho: PermissaoTrabalho;
  preRequisitos: PermissaoTrabalhoPreRequisito[];
  tiposTrabalho: PermissaoTrabalhoTipoTrabalho[];
  verificacoes: PermissaoTrabalhoVerificacao[];
  epis: PermissaoTrabalhoEpi[];
  epcs: PermissaoTrabalhoEpc[];
  riscosCriticos: PermissaoTrabalhoRiscoCritico[];
  responsaveis: PermissaoTrabalhoResponsavel[];
}

export const StatusUsuario = {
  Ativo: 1,
  Inativo: 2,
  Bloqueado: 3,
} as const;

export const statusUsuarioLabel: Record<number, string> = {
  1: 'Ativo',
  2: 'Inativo',
  3: 'Bloqueado',
};

export const TipoPerfilAcesso = {
  Administrador: 1,
  Diretor: 2,
  GestorQsms: 3,
  EngenheiroSeguranca: 4,
  TecnicoSeguranca: 5,
  MedicoDoTrabalho: 6,
  Rh: 7,
  GestorDeObra: 8,
  Encarregado: 9,
  Trabalhador: 10,
  Auditor: 11,
  Terceiro: 12,
} as const;

export const tipoPerfilAcessoLabel: Record<number, string> = {
  1: 'Administrador',
  2: 'Diretor',
  3: 'Gestor QSMS',
  4: 'Engenheiro de Segurança',
  5: 'Técnico de Segurança',
  6: 'Médico do Trabalho',
  7: 'RH',
  8: 'Gestor de Obra',
  9: 'Encarregado',
  10: 'Trabalhador',
  11: 'Auditor',
  12: 'Terceiro',
};

export const EscopoAcesso = {
  Global: 1,
  Unidade: 2,
  Obra: 3,
  Proprio: 4,
} as const;

export const escopoAcessoLabel: Record<number, string> = {
  1: 'Global',
  2: 'Unidade',
  3: 'Obra',
  4: 'Próprio',
};

export interface UsuarioPerfilObra {
  id: string;
  perfilAcessoId: string;
  perfilAcessoNome: string;
  obraId?: string | null;
  obraNome?: string | null;
}

export interface Usuario {
  id: string;
  // Nulo até o primeiro login via Teams SSO: é vinculado automaticamente pelo backend
  // (claim 'oid' do token), nunca digitado manualmente no cadastro.
  azureAdObjectId: string | null;
  email: string;
  nome: string;
  status: number;
  ultimoLoginUtc?: string | null;
  trabalhadorId?: string | null;
  perfisPorObra: UsuarioPerfilObra[];
}

export interface NovoUsuario {
  email: string;
  nome: string;
  trabalhadorId?: string | null;
}

export interface AtualizarUsuarioPayload {
  id: string;
  nome: string;
  status: number;
  trabalhadorId?: string | null;
}

export interface PerfilAcesso {
  id: string;
  tipo?: number | null;
  nome: string;
  descricao?: string | null;
  ehSistema: boolean;
  quantidadePermissoes: number;
}

export interface NovoPerfilAcesso {
  nome: string;
  descricao?: string | null;
}

export interface PerfilAcessoPermissao {
  id: string;
  permissaoId: string;
  permissaoCodigo: string;
  permissaoModulo: string;
  permissaoAcao: string;
  escopo: number;
  permitido: boolean;
}

export interface ItemPermissaoPerfil {
  permissaoId: string;
  escopo: number;
  permitido: boolean;
}

export interface Permissao {
  id: string;
  codigo: string;
  modulo: string;
  acao: string;
  descricao: string;
}

export const TipoInspecao = {
  Obra: 1,
  Canteiro: 2,
  Epi: 3,
  Epc: 4,
  Maquinas: 5,
  Ferramentas: 6,
  Andaimes: 7,
  Escadas: 8,
  Eletrica: 9,
  Altura: 10,
  EspacoConfinado: 11,
  Comportamental: 12,
  Terceiros: 13,
} as const;

export const tipoInspecaoLabel: Record<number, string> = {
  1: 'Obra',
  2: 'Canteiro',
  3: 'EPI',
  4: 'EPC',
  5: 'Máquinas',
  6: 'Ferramentas',
  7: 'Andaimes',
  8: 'Escadas',
  9: 'Elétrica',
  10: 'Altura',
  11: 'Espaço confinado',
  12: 'Comportamental',
  13: 'Terceiros',
};

export const StatusItemChecklist = {
  Conforme: 1,
  NaoConforme: 2,
  NaoAplicavel: 3,
} as const;

export const statusItemChecklistLabel: Record<number, string> = {
  1: 'Conforme',
  2: 'Não conforme',
  3: 'Não aplicável',
};

export const StatusInspecao = {
  EmAndamento: 1,
  Concluida: 2,
} as const;

export const statusInspecaoLabel: Record<number, string> = {
  1: 'Em andamento',
  2: 'Concluída',
};

export interface ChecklistModelo {
  id: string;
  nome: string;
  tipoInspecao: number;
  versao: number;
  checklistModeloAnteriorId?: string | null;
  quantidadeItens: number;
}

export interface ChecklistModeloItem {
  id: string;
  checklistModeloId: string;
  ordem: number;
  descricao: string;
  exigeFotografia: boolean;
  exigeResponsavel: boolean;
  exigePrazo: boolean;
}

export interface NovoChecklistModeloItem {
  descricao: string;
  exigeFotografia: boolean;
  exigeResponsavel: boolean;
  exigePrazo: boolean;
}

export interface NovoChecklistModelo {
  nome: string;
  tipoInspecao: number;
  itens: NovoChecklistModeloItem[];
}

export interface ChecklistModeloDetalhe {
  checklistModelo: ChecklistModelo;
  itens: ChecklistModeloItem[];
}

export interface Inspecao {
  id: string;
  tipoInspecao: number;
  obraId: string;
  obraNome: string;
  atividadeId?: string | null;
  atividadeNome?: string | null;
  checklistModeloId: string;
  checklistModeloNome: string;
  checklistModeloVersao: number;
  data: string;
  responsavelUsuarioId: string;
  responsavelUsuarioNome: string;
  status: number;
  totalItens: number;
  itensRespondidos: number;
  itensNaoConformes: number;
}

export interface NovaInspecao {
  checklistModeloId: string;
  obraId: string;
  atividadeId?: string | null;
  data: string;
  responsavelUsuarioId: string;
}

export interface InspecaoItemResposta {
  id: string;
  inspecaoId: string;
  checklistModeloItemId: string;
  ordem: number;
  descricao: string;
  exigeFotografia: boolean;
  exigeResponsavel: boolean;
  exigePrazo: boolean;
  statusItem?: number | null;
  observacao?: string | null;
  local?: string | null;
  planoDeAcao?: string | null;
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
  prazo?: string | null;
  temFoto: boolean;
  temFotoDepois: boolean;
  naoConformidadeId?: string | null;
}

export interface InspecaoDetalhe {
  inspecao: Inspecao;
  respostas: InspecaoItemResposta[];
}

export const StatusDds = {
  EmAndamento: 1,
  Concluido: 2,
} as const;

export const statusDdsLabel: Record<number, string> = {
  1: 'Em andamento',
  2: 'Concluído',
};

export interface Dds {
  id: string;
  obraId: string;
  obraNome: string;
  data: string;
  responsavelUsuarioId: string;
  responsavelUsuarioNome: string;
  topicoPrincipal: string;
  status: number;
  atividadesNomes: string[];
  totalItensChecklist: number;
  itensVerificados: number;
  totalParticipantes: number;
}

export interface NovaDds {
  obraId: string;
  atividadesIds: string[];
  data: string;
  responsavelUsuarioId: string;
}

export interface DdsItemChecklist {
  id: string;
  ddsId: string;
  riscoId?: string | null;
  descricao: string;
  verificado: boolean;
}

export const TipoFotoParticipante = {
  Pessoa: 1,
  DocumentoAssinado: 2,
} as const;

export const tipoFotoParticipanteLabel: Record<number, string> = {
  1: 'Foto da pessoa',
  2: 'Documento assinado',
};

export interface DdsParticipante {
  id: string;
  trabalhadorId: string;
  trabalhadorNome: string;
  fotoTipo: number;
  telegramEnviadoEm?: string | null;
  telegramConfirmadoEm?: string | null;
}

export interface DdsDetalhe {
  dds: Dds;
  itensChecklist: DdsItemChecklist[];
  participantes: DdsParticipante[];
}

export interface EnviarDdsTelegramResultado {
  totalParticipantes: number;
  enviados: number;
  semVinculo: number;
}

// Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §3/§5, etapa 6) — genérico,
// identificado por EntidadeTipo/EntidadeId (ex.: "Dds" + ddsId). Primeiro consumidor: AssinarDdsPage.
// PIN/crachá-QR e WebAuthn/FIDO2 foram removidos do sistema em 31/08 (decisão do usuário: único
// método de assinatura é a digital via leitor Futronic FS80H) — como o sistema ainda está em fase de
// testes (sem nenhuma assinatura real registrada por esses métodos), os valores 2/3/4 do enum
// também foram removidos, não só deixados de fora da UI.
export const MetodoAutenticacaoAssinatura = {
  Biometria: 1,
  SessaoLogada: 5,
} as const;

export const metodoAutenticacaoAssinaturaLabel: Record<number, string> = {
  1: 'Digital (Futronic FS80H)',
  5: 'Sessão logada',
};

export const StatusDocumentoAssinatura = {
  EmAndamento: 1,
  Finalizado: 2,
  Cancelado: 3,
} as const;

export const statusDocumentoAssinaturaLabel: Record<number, string> = {
  1: 'Em andamento',
  2: 'Finalizado',
  3: 'Cancelado',
};

export interface DocumentoSignatario {
  trabalhadorId: string;
  trabalhadorNome: string;
  metodoAutenticacao: number;
  assinadoEm: string;
}

export interface DocumentoAssinatura {
  id: string;
  entidadeTipo: string;
  entidadeId: string;
  status: number;
  signatarios: DocumentoSignatario[];
}

// Página pública de validação (/#/validar/{token}, etapa 11) — deliberadamente sem id/entidadeId
// (ver DocumentoPublicoDto no backend: "nunca expor Id/EntidadeId/dado pessoal na página pública").
export interface DocumentoPublicoSignatario {
  trabalhadorNome: string;
  metodoAutenticacao: number;
  assinadoEm: string;
}

export interface DocumentoPublico {
  entidadeTipo: string;
  finalizadoEm: string;
  conteudoHash: string;
  signatarios: DocumentoPublicoSignatario[];
}

// Painel administrativo (etapa 12) — ao contrário de DocumentoPublico, aqui o consumidor já está
// autenticado/autorizado (assinatura:ver), então id/entidadeId aparecem: os botões de ação precisam
// deles (baixar PDF, copiar link público).
export interface DocumentoAssinaturaResumo {
  id: string;
  entidadeTipo: string;
  entidadeId: string;
  status: number;
  criadoEm: string;
  finalizadoEm?: string | null;
  quantidadeSignatarios: number;
  temPdf: boolean;
  tokenValidacaoPublica?: string | null;
}

// Motor Central de Alertas + Cadastro de Ativos (requisito do usuário, 2026-08-25): entidade única
// AtivoSst com campo discriminador TipoAtivo — a validade aqui é um campo fixo (DataValidade), não
// calculada a partir de um histórico de registros.
export const TipoAtivo = {
  Extintor: 1,
  Equipamento: 2,
} as const;

export const tipoAtivoLabel: Record<number, string> = {
  1: 'Extintor',
  2: 'Equipamento',
};

export interface AtivoSst {
  id: string;
  obraId: string;
  obraNome: string;
  tipoAtivo: number;
  identificacao: string;
  descricao: string;
  localizacao?: string | null;
  dataValidade: string;
  observacoes?: string | null;
}

export type NovoAtivoSst = Omit<AtivoSst, 'id' | 'obraNome'>;

export interface TrilhaAuditoria {
  id: string;
  timestamp: string;
  usuarioId?: string | null;
  usuarioNome?: string | null;
  acao: string;
  entidadeTipo: string;
  entidadeId: string;
  dadosAntesJson?: string | null;
  dadosDepoisJson?: string | null;
}

export const OrigemNaoConformidade = {
  Inspecao: 1,
  Auditoria: 2,
  Denuncia: 3,
  ObservacaoDireta: 4,
  Outro: 5,
} as const;

export const origemNaoConformidadeLabel: Record<number, string> = {
  1: 'Inspeção',
  2: 'Auditoria',
  3: 'Denúncia',
  4: 'Observação direta',
  5: 'Outro',
};

export const StatusNaoConformidade = {
  Aberta: 1,
  EmAndamento: 2,
  AguardandoValidacao: 3,
  Encerrada: 4,
  Enviada: 5,
  EmAnalise: 6,
  Devolvida: 7,
} as const;

export const statusNaoConformidadeLabel: Record<number, string> = {
  1: 'Aberta',
  2: 'Em andamento',
  3: 'Aguardando validação',
  4: 'Encerrada',
  5: 'Enviada',
  6: 'Em análise',
  7: 'Devolvida',
};

export const PrioridadeAcao = {
  Critica: 1,
  Alta: 2,
  Media: 3,
  Baixa: 4,
} as const;

export const prioridadeAcaoLabel: Record<number, string> = {
  1: 'Crítica',
  2: 'Alta',
  3: 'Média',
  4: 'Baixa',
};

export const TipoAcaoPlano = {
  Corretiva: 1,
  Preventiva: 2,
  Melhoria: 3,
} as const;

export const tipoAcaoPlanoLabel: Record<number, string> = {
  1: 'Corretiva',
  2: 'Preventiva',
  3: 'Melhoria',
};

// Reaproveita o mesmo enum de StatusControleRisco (Pendente/EmAndamento/Concluido/Vencido) do
// módulo de Riscos — decisão própria, ver disclosure em AcaoPlano.cs.
export const StatusAcaoPlano = {
  Pendente: 1,
  EmAndamento: 2,
  Concluido: 3,
  Vencido: 4,
} as const;

export const statusAcaoPlanoLabel: Record<number, string> = {
  1: 'Pendente',
  2: 'Em andamento',
  3: 'Concluído',
  4: 'Vencido',
};

export interface NaoConformidade {
  id: string;
  origemDeteccao: number;
  requisitoRelacionado?: string | null;
  descricao: string;
  local?: string | null;
  atividadeId?: string | null;
  atividadeNome?: string | null;
  riscoId?: string | null;
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
  prazo?: string | null;
  status: number;
  dataConclusao?: string | null;
  observacoesEncerramento?: string | null;
  motivoDevolucao?: string | null;
  inspecaoItemRespostaId?: string | null;
}

export interface NovaNaoConformidade {
  origemDeteccao: number;
  requisitoRelacionado?: string | null;
  descricao: string;
  local?: string | null;
  atividadeId?: string | null;
  riscoId?: string | null;
  responsavelUsuarioId?: string | null;
  prazo?: string | null;
}

export type AtualizarNaoConformidadePayload = NovaNaoConformidade;

export interface AcaoPlano {
  id: string;
  origemTipo: string;
  origemId: string;
  tipo: number;
  descricao: string;
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
  prioridade: number;
  prazo?: string | null;
  status: number;
  dataConclusao?: string | null;
  dataValidacao?: string | null;
  validadoPorUsuarioId?: string | null;
  validadoPorUsuarioNome?: string | null;
}

export interface NaoConformidadeDetalhe {
  naoConformidade: NaoConformidade;
  acoesPlano: AcaoPlano[];
}

export interface NovaAcaoPlano {
  origemTipo: string;
  origemId: string;
  tipo: number;
  descricao: string;
  responsavelUsuarioId?: string | null;
  prioridade: number;
  prazo?: string | null;
}

export interface AtualizarAcaoPlanoPayload {
  tipo: number;
  descricao: string;
  responsavelUsuarioId?: string | null;
  prioridade: number;
  prazo?: string | null;
  status: number;
  dataConclusao?: string | null;
}

// Seção 27 da Base de Conhecimento — vocabulário literal de tipo de ocorrência.
export const TipoOcorrencia = {
  Acidente: 1,
  Incidente: 2,
  QuaseAcidente: 3,
  CondicaoInsegura: 4,
  AtoInseguro: 5,
  DoencaOcupacional: 6,
} as const;

export const tipoOcorrenciaLabel: Record<number, string> = {
  1: 'Acidente',
  2: 'Incidente',
  3: 'Quase acidente',
  4: 'Condição insegura',
  5: 'Ato inseguro',
  6: 'Doença ocupacional',
};

// Seção 28 da Base de Conhecimento — vocabulário literal de metodologias de investigação.
export const MetodologiaInvestigacao = {
  AnaliseCausaRaiz: 1,
  CincoPorques: 2,
  ArvoreDeCausas: 3,
  FatoresContribuintes: 4,
  FalhasDeBarreira: 5,
} as const;

export const metodologiaInvestigacaoLabel: Record<number, string> = {
  1: 'Análise de causa raiz',
  2: '5 Porquês',
  3: 'Árvore de causas',
  4: 'Fatores contribuintes',
  5: 'Falhas de barreira',
};

// Documento não lista vocabulário literal para o status da investigação — proposta própria
// (mesma decisão do backend, ver Domain/Enums/Enums.cs StatusAcidente).
export const StatusAcidente = {
  Registrado: 1,
  EmInvestigacao: 2,
  Concluido: 3,
} as const;

export const statusAcidenteLabel: Record<number, string> = {
  1: 'Registrado',
  2: 'Em investigação',
  3: 'Concluído',
};

// Classificação de gravidade do acidente, usada para calcular Dias Debitados na Taxa de
// Gravidade (NBR 14280). Vocabulário não citado literalmente na Base de Conhecimento —
// proposta própria, mesma natureza de StatusAcidente acima.
export const GravidadeAcidente = {
  SemAfastamento: 1,
  ComAfastamento: 2,
  IncapacidadePermanenteParcial: 3,
  IncapacidadePermanenteTotal: 4,
  Obito: 5,
} as const;

export const gravidadeAcidenteLabel: Record<number, string> = {
  1: 'Sem afastamento',
  2: 'Com afastamento',
  3: 'Incapacidade permanente parcial',
  4: 'Incapacidade permanente total',
  5: 'Óbito',
};

export interface Acidente {
  id: string;
  tipo: number;
  obraId: string;
  obraNome?: string | null;
  trabalhadorId?: string | null;
  trabalhadorNome?: string | null;
  atividadeId?: string | null;
  atividadeNome?: string | null;
  local: string;
  data: string;
  hora?: string | null;
  descricao: string;
  lesao?: string | null;
  consequencia?: string | null;
  atendimento?: string | null;
  houveAfastamento: boolean;
  diasAfastamento?: number | null;
  numeroCat?: string | null;
  gravidade: number;
  diasDebitados: number;
  metodologiaInvestigacao?: number | null;
  causas?: string | null;
  status: number;
  dataConclusaoInvestigacao?: string | null;
}

export interface NovoAcidente {
  tipo: number;
  obraId: string;
  trabalhadorId?: string | null;
  atividadeId?: string | null;
  local: string;
  data: string;
  hora?: string | null;
  descricao: string;
  lesao?: string | null;
  consequencia?: string | null;
  atendimento?: string | null;
  houveAfastamento: boolean;
  diasAfastamento?: number | null;
  numeroCat?: string | null;
  gravidade: number;
  diasDebitadosInformados?: number | null;
  metodologiaInvestigacao?: number | null;
  causas?: string | null;
}

export type AtualizarAcidentePayload = NovoAcidente;

export interface AcidenteDetalhe {
  acidente: Acidente;
  acoesPlano: AcaoPlano[];
}

// Lançamento mensal de HHT (Horas-Homem Trabalhadas) por obra, usado no cálculo da Taxa de
// Gravidade (NBR 14280) — TG = (Dias Perdidos + Dias Debitados) × 1.000.000 / HHT.
export interface RegistroHhtMensal {
  id: string;
  obraId: string;
  obraNome?: string | null;
  ano: number;
  mes: number;
  horasHomemTrabalhadas: number;
}

export type NovoRegistroHhtMensal = Omit<RegistroHhtMensal, 'id' | 'obraNome'>;

export type AtualizarRegistroHhtMensalPayload = NovoRegistroHhtMensal;

// PR-SST-003 — PCMSO reaproveitava DocumentoGestao (Tipo="PCMSO") como documento controlado +
// PcmsoDetalhe com os campos clínicos específicos. Id abaixo é o Id do próprio PcmsoDetalhe;
// documentoGestaoId aponta para o DocumentoGestao vinculado (edição/exclusão usam este último).
export interface Pcmso {
  id: string;
  documentoGestaoId: string;
  nome: string;
  versao?: string | null;
  validade?: string | null;
  dataEmissao: string;
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
  obraId?: string | null;
  setorId?: string | null;
  arquivo?: string | null;
  status: number;
  medicoResponsavelNome?: string | null;
  medicoResponsavelCrm?: string | null;
  funcoesContempladas?: string | null;
  riscosConsiderados?: string | null;
  examesPrevistos?: string | null;
  periodicidades?: string | null;
  unidadesObrasAbrangidas?: string | null;
}

export interface NovoPcmso {
  nome: string;
  versao?: string | null;
  validade?: string | null;
  dataEmissao: string;
  responsavelUsuarioId?: string | null;
  obraId?: string | null;
  setorId?: string | null;
  arquivo?: string | null;
  medicoResponsavelNome?: string | null;
  medicoResponsavelCrm?: string | null;
  funcoesContempladas?: string | null;
  riscosConsiderados?: string | null;
  examesPrevistos?: string | null;
  periodicidades?: string | null;
  unidadesObrasAbrangidas?: string | null;
}

export type AtualizarPcmsoPayload = NovoPcmso;

// Vocabulário de status que este PCMSO (PR-SST-003, reaproveitando DocumentoGestao) usava emprestado
// de StatusDocumentoGestao (removido junto com Gestão Documental/Conformidade em 2026-08-28) —
// mantido aqui, escopado só a este PCMSO, para as telas não perderem o rótulo/cor de status enquanto
// o backend não é reformulado (ver PENDENTE acima e em Pcmsos/* no backend). Nome diferente de
// StatusPcmso (acima) de propósito: aquele é do PCMSO v1 antigo, vocabulário numérico incompatível.
export const StatusPcmsoDocumento = {
  Rascunho: 1,
  EmAprovacao: 2,
  Vigente: 3,
  Obsoleto: 4,
  Cancelado: 5,
} as const;

export const statusPcmsoDocumentoLabel: Record<number, string> = {
  1: 'Rascunho',
  2: 'Em aprovação',
  3: 'Vigente',
  4: 'Obsoleto',
  5: 'Cancelado',
};

// PENDENTE: DocumentoGestao (e o backend de src/AAHBRANT.SST.Application/Pcmsos) foi removido
// junto com o módulo de Conformidade (Matriz Legal + Gestão Documental) em 2026-08-28. O PCMSO
// fica sem armazenamento de documento até ser reformulado para não depender mais dele — ver
// AAHBRANT.SST.Application/Pcmsos/* e DocumentoAlertaProvider.cs no backend.

export const TipoAlerta = {
  AsoVencendo: 1,
  AsoVencido: 2,
  TreinamentoVencendo: 3,
  TreinamentoVencido: 4,
  AutorizacaoVencendo: 5,
  AutorizacaoVencida: 6,
  EpiValidadeProxima: 7,
  EpiVencido: 8,
  InspecaoPendente: 9,
  NaoConformidadeAberta: 10,
  AcaoAtrasada: 11,
  DocumentoVencendo: 12,
  DocumentoVencido: 13,
  AtividadeBloqueada: 14,
  PtVencida: 15,
  // 16 reservado (módulo Higienização removido) — não reaproveitar, alertas antigos podem ter esse tipo gravado.
  ExtintorVencendo: 17,
  ExtintorVencido: 18,
  EquipamentoVencendo: 19,
  EquipamentoVencido: 20,
} as const;

export const tipoAlertaLabel: Record<number, string> = {
  1: 'ASO vencendo',
  2: 'ASO vencido',
  3: 'Treinamento vencendo',
  4: 'Treinamento vencido',
  5: 'Autorização vencendo',
  6: 'Autorização vencida',
  7: 'EPI com validade próxima',
  8: 'EPI vencido',
  9: 'Inspeção pendente',
  10: 'Não conformidade aberta',
  11: 'Ação atrasada',
  12: 'Documento vencendo',
  13: 'Documento vencido',
  14: 'Atividade bloqueada',
  15: 'PT vencida',
  16: 'Higienização vencendo',
  17: 'Extintor vencendo',
  18: 'Extintor vencido',
  19: 'Equipamento vencendo',
  20: 'Equipamento vencido',
};

export const SeveridadeAlerta = {
  Info: 1,
  Atencao: 2,
  Critico: 3,
} as const;

export const severidadeAlertaLabel: Record<number, string> = {
  1: 'Informativo',
  2: 'Atenção',
  3: 'Crítico',
};

export const StatusAlerta = {
  Aberto: 1,
  EmTratamento: 2,
  Escalonado: 3,
  Resolvido: 4,
  Ignorado: 5,
} as const;

export const statusAlertaLabel: Record<number, string> = {
  1: 'Aberto',
  2: 'Em tratamento',
  3: 'Escalonado',
  4: 'Resolvido',
  5: 'Ignorado',
};

// Rótulo amigável do módulo de origem do alerta (Alerta.EntidadeOrigemTipo) — alimentado tanto
// pelos IAlertaOrigemProvider do Motor Central de Alertas (Aso, Treinamento, AtivoSst) quanto por
// alertas criados manualmente, cujo EntidadeOrigemTipo é texto livre (ver CriarAlertaCommand); por
// isso o fallback retorna o próprio valor quando não reconhecido. ItemHigienizacao é mantido aqui
// (módulo Higienização removido) só para exibir corretamente alertas antigos já gravados no banco.
export const categoriaAlertaLabel: Record<string, string> = {
  Aso: 'ASO',
  Treinamento: 'Treinamentos',
  ItemHigienizacao: 'Higienização',
  AtivoSst: 'Ativos (Extintores/Equipamentos)',
  NaoConformidade: 'Ocorrências de inspeção',
  AcaoPlano: 'Ações do plano',
};

export function categoriaAlertaRotulo(entidadeOrigemTipo: string): string {
  return categoriaAlertaLabel[entidadeOrigemTipo] ?? entidadeOrigemTipo;
}

export interface Alerta {
  id: string;
  tipo: number;
  severidade: number;
  status: number;
  titulo: string;
  descricao?: string | null;
  entidadeOrigemTipo: string;
  entidadeOrigemId: string;
  trabalhadorId?: string | null;
  trabalhadorNome?: string | null;
  obraId?: string | null;
  obraNome?: string | null;
  destinatarioUsuarioId?: string | null;
  destinatarioUsuarioNome?: string | null;
  dataLimiteTratamento?: string | null;
  escalonadoParaUsuarioId?: string | null;
  escalonadoParaUsuarioNome?: string | null;
  dataEscalonamento?: string | null;
  createdAtUtc: string;
}

export interface NovoAlerta {
  tipo: number;
  severidade: number;
  titulo: string;
  descricao?: string | null;
  entidadeOrigemTipo: string;
  entidadeOrigemId: string;
  trabalhadorId?: string | null;
  obraId?: string | null;
  destinatarioUsuarioId?: string | null;
  dataLimiteTratamento?: string | null;
}

export interface AtualizarAlertaPayload {
  tipo: number;
  severidade: number;
  titulo: string;
  descricao?: string | null;
  trabalhadorId?: string | null;
  obraId?: string | null;
  destinatarioUsuarioId?: string | null;
  dataLimiteTratamento?: string | null;
}

// "Quero o calendário dentro do aplicativo, tem que ser o Teams" (requisito do usuário,
// 2026-08-29) — combina os eventos reais do Outlook/Teams do usuário logado (lidos via Microsoft
// Graph) com os vencimentos que o Motor de Alertas já gera para ele. Só a própria agenda do
// usuário logado (endpoint não recebe usuarioId — ver CalendarioController).
export interface EventoGraphCalendario {
  graphEventId: string;
  assunto: string;
  inicio: string;
  fim: string;
  diaInteiro: boolean;
  local?: string | null;
  organizadorNome?: string | null;
  reuniaoOnline: boolean;
  linkReuniaoOnline?: string | null;
}

export interface EventoSstCalendario {
  alertaId: string;
  titulo: string;
  descricao?: string | null;
  data: string;
  tipo: number;
  severidade: number;
  status: number;
  entidadeOrigemTipo: string;
  entidadeOrigemId: string;
}

export interface Calendario {
  usuarioIdentificado: boolean;
  graphDisponivel: boolean;
  mensagemErroGraph?: string | null;
  eventosGraph: EventoGraphCalendario[];
  eventosSst: EventoSstCalendario[];
}

// Motor Central de Alertas (requisito do usuário, 2026-08-25): tela de administração que permite
// ajustar RegraAlerta.DiasAntecedencia/Severidade por módulo, hoje só editável direto no banco. O
// AlertaEngineService escolhe a regra mais urgente cujo DiasAntecedencia cobre os dias restantes —
// ver AAHBRANT.SST.Application/Alertas/Motor/AlertaEngineService.cs.
export const TipoModuloAlerta = {
  Aso: 1,
  Treinamento: 2,
  // 3 reservado (módulo Higienização removido) — não reaproveitar, regras/alertas antigos podem ter esse módulo gravado.
  Epi: 4,
  Documento: 5,
  Inspecao: 6,
  Extintor: 7,
  Equipamento: 8,
  Dds: 9,
  PlanoAcao: 10,
  Outro: 11,
} as const;

export const moduloAlertaLabel: Record<number, string> = {
  1: 'ASO',
  2: 'Treinamento',
  3: 'Higienização',
  4: 'EPI',
  5: 'Documento',
  6: 'Inspeção',
  7: 'Extintor',
  8: 'Equipamento',
  9: 'DDS',
  10: 'Plano de ação',
  11: 'Outro',
};

export interface RegraAlerta {
  id: string;
  modulo: number;
  diasAntecedencia: number;
  severidade: number;
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
}

export type NovaRegraAlerta = Omit<RegraAlerta, 'id'>;

// Perfil de Vida do Trabalhador — espelha PerfilCompletoTrabalhadorDto (backend), reaproveitando os
// tipos Aso/Treinamento/EntregaEpi já existentes acima para as sub-listas correspondentes.
export interface FrequenciaTrocaEpi {
  catalogoEpiId: string;
  catalogoEpiNome: string;
  quantidadeTrocas: number;
}

export interface AssiduidadeDds {
  totalRealizados: number;
  totalParticipados: number;
}

export interface RiscoExpostoPerfil {
  riscoId: string;
  perigoNome: string;
  atividadeNome: string;
  ambiente?: string | null;
  exposicao?: string | null;
  consequencia?: string | null;
  probabilidade: number;
  severidade: number;
  nivelRisco: number;
  controlesExistentes?: string | null;
  controlesAdicionais?: string | null;
  status: number;
}

export interface OcorrenciaPerfil {
  id: string;
  tipo: number;
  data: string;
  local: string;
  descricao: string;
  gravidade: number;
  houveAfastamento: boolean;
  diasAfastamento?: number | null;
  status: number;
}

export interface AssinaturaPerfil {
  documentoAssinaturaId: string;
  entidadeTipo: string;
  entidadeId: string;
  metodo: number;
  assinadoEm: string;
  ipAddress?: string | null;
  temPdf: boolean;
}

export interface PerfilCompletoTrabalhador {
  id: string;
  nome: string;
  matricula: string;
  cpf: string;
  rg?: string | null;
  obraId: string;
  obraNome: string;
  funcaoId: string;
  funcaoNome: string;
  vinculo: number;
  dataAdmissao: string;
  temFoto: boolean;
  temBiometria: boolean;
  statusAptidao: string;
  asos: Aso[];
  episAtivos: EntregaEpi[];
  frequenciaTrocas: FrequenciaTrocaEpi[];
  treinamentos: Treinamento[];
  assiduidadeDds: AssiduidadeDds;
  riscos: RiscoExpostoPerfil[];
  ocorrencias: OcorrenciaPerfil[];
  assinaturas: AssinaturaPerfil[];
}

// Módulos com suporte a uso offline (piloto acordado com o usuário em 24/08: módulos de campo,
// onde falta de sinal é mais comum — obra/canteiro). Os demais ~25 módulos seguem com fetch
// direto, sem fila local nem cache — extensão do piloto é trabalho futuro, módulo a módulo.
const PREFIXOS_OFFLINE = ['/api/dds', '/api/inspecoes', '/api/checklistmodelos', '/api/aprs', '/api/aprEtapas', '/api/aprAssinaturas'];

function ehRotaOffline(path: string): boolean {
  return PREFIXOS_OFFLINE.some((prefixo) => path.startsWith(prefixo));
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const authHeaders = await montarHeadersAuth();
  const metodo = (init?.method ?? 'GET').toUpperCase();

  if (ehRotaOffline(path)) {
    if (metodo === 'GET') {
      return syncFetchJson<T>(path, init, authHeaders);
    }
    const corpo = init?.body ? JSON.parse(init.body as string) : undefined;
    return syncMutateJson<T>(path, metodo as 'POST' | 'PUT' | 'DELETE', corpo, authHeaders);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...authHeaders,
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const corpo = await response.text().catch(() => '');
    throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const api = {
  obras: {
    listar: () => request<Obra[]>('/api/obras'),
    // Logomarca obrigatória no cadastro (decisão do usuário, 31/08) — a criação passa a ser
    // multipart/form-data (como o anexarLogo abaixo) em vez de JSON, para enviar obra + logo numa
    // única chamada, já que o backend agora exige o arquivo para finalizar o cadastro da obra.
    criar: async (obra: NovaObra, logo: File) => {
      const formData = new FormData();
      formData.append('Codigo', obra.codigo);
      formData.append('Nome', obra.nome);
      if (obra.cliente) formData.append('Cliente', obra.cliente);
      formData.append('Status', String(obra.status));
      if (obra.dataInicio) formData.append('DataInicio', obra.dataInicio);
      if (obra.dataPrevisaoTermino) formData.append('DataPrevisaoTermino', obra.dataPrevisaoTermino);
      if (obra.endereco) formData.append('Endereco', obra.endereco);
      if (obra.cidade) formData.append('Cidade', obra.cidade);
      if (obra.uf) formData.append('Uf', obra.uf);
      if (obra.cnpj) formData.append('Cnpj', obra.cnpj);
      formData.append('Logo', logo);
      const response = await fetch(`${API_BASE_URL}/api/obras`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.json() as Promise<{ id: string }>;
    },
    excluir: (id: string) => request<void>(`/api/obras/${id}`, { method: 'DELETE' }),
    anexarLogo: async (id: string, arquivo: File) => {
      const formData = new FormData();
      formData.append('Logo', arquivo);
      const response = await fetch(`${API_BASE_URL}/api/obras/${id}/logo`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
    },
    baixarLogo: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/obras/${id}/logo`, { headers: await montarHeadersAuth() });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
  },
  trabalhadores: {
    listar: (obraId?: string) =>
      request<Trabalhador[]>(`/api/trabalhadores${obraId ? `?obraId=${obraId}` : ''}`),
    criar: (trabalhador: NovoTrabalhador) =>
      request<{ id: string }>('/api/trabalhadores', { method: 'POST', body: JSON.stringify(trabalhador) }),
    excluir: (id: string) => request<void>(`/api/trabalhadores/${id}`, { method: 'DELETE' }),
    enviarFoto: async (id: string, arquivo: File) => {
      const formData = new FormData();
      formData.append('Foto', arquivo);
      const response = await fetch(`${API_BASE_URL}/api/trabalhadores/${id}/foto`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
    },
    baixarFoto: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/trabalhadores/${id}/foto`, {
        headers: await montarHeadersAuth(),
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
    gerarVinculoTelegram: (id: string) =>
      request<GerarVinculoTelegramResultado>(`/api/trabalhadores/${id}/telegram/vinculo`, { method: 'POST' }),
    registrarTermoAceiteAssinatura: (id: string) =>
      request<void>(`/api/trabalhadores/${id}/assinatura/termo-aceite`, { method: 'POST' }),
    registrarConsentimentoBiometria: (id: string) =>
      request<void>(`/api/trabalhadores/${id}/assinatura/consentimento-biometria`, { method: 'POST' }),
    obterPerfilCompleto: (id: string) =>
      request<PerfilCompletoTrabalhador>(`/api/trabalhadores/${id}/perfil-completo`),
    baixarRelatorioFiscalizacao: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/trabalhadores/${id}/relatorio-pdf`, { headers: await montarHeadersAuth() });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
    // Cadastro de digital via agente local (Futronic FS80H) — templateBruto vem em base64 do
    // agente (fetch local a /api/capturar-bruto); o backend criptografa antes de persistir.
    cadastrarBiometriaLocal: (id: string, templateBrutoBase64: string) =>
      request<void>(`/api/trabalhadores/${id}/assinatura/biometria-local/cadastro`, {
        method: 'POST',
        body: JSON.stringify({ templateBruto: templateBrutoBase64 }),
      }),
  },
  funcoes: {
    listar: () => request<Funcao[]>('/api/funcoes'),
    criar: (funcao: NovaFuncao) =>
      request<{ id: string }>('/api/funcoes', { method: 'POST', body: JSON.stringify(funcao) }),
    excluir: (id: string) => request<void>(`/api/funcoes/${id}`, { method: 'DELETE' }),
    listarEpis: (funcaoId: string) => request<CatalogoEpi[]>(`/api/funcoes/${funcaoId}/epis`),
    definirEpis: (funcaoId: string, catalogoEpiIds: string[]) =>
      request<void>(`/api/funcoes/${funcaoId}/epis`, {
        method: 'PUT',
        body: JSON.stringify({ catalogoEpiIds }),
      }),
    listarTreinamentosObrigatorios: (funcaoId: string) =>
      request<CursoTreinamento[]>(`/api/funcoes/${funcaoId}/treinamentos-obrigatorios`),
    definirTreinamentosObrigatorios: (funcaoId: string, cursoTreinamentoIds: string[]) =>
      request<void>(`/api/funcoes/${funcaoId}/treinamentos-obrigatorios`, {
        method: 'PUT',
        body: JSON.stringify({ cursoTreinamentoIds }),
      }),
  },
  setores: {
    listar: (obraId?: string) => request<Setor[]>(`/api/setores${obraId ? `?obraId=${obraId}` : ''}`),
    criar: (setor: NovoSetor) =>
      request<{ id: string }>('/api/setores', { method: 'POST', body: JSON.stringify(setor) }),
    excluir: (id: string) => request<void>(`/api/setores/${id}`, { method: 'DELETE' }),
  },
  equipes: {
    listar: (params?: { obraId?: string; setorId?: string }) => {
      const query = new URLSearchParams();
      if (params?.obraId) query.set('obraId', params.obraId);
      if (params?.setorId) query.set('setorId', params.setorId);
      const qs = query.toString();
      return request<Equipe[]>(`/api/equipes${qs ? `?${qs}` : ''}`);
    },
    criar: (equipe: NovaEquipe) =>
      request<{ id: string }>('/api/equipes', { method: 'POST', body: JSON.stringify(equipe) }),
    excluir: (id: string) => request<void>(`/api/equipes/${id}`, { method: 'DELETE' }),
  },
  asos: {
    listar: (trabalhadorId?: string) =>
      request<Aso[]>(`/api/asos${trabalhadorId ? `?trabalhadorId=${trabalhadorId}` : ''}`),
    obterPorId: (id: string) => request<Aso>(`/api/asos/${id}`),
    criar: (aso: NovoAso) => request<{ id: string }>('/api/asos', { method: 'POST', body: JSON.stringify(aso) }),
    atualizar: (aso: Aso) => request<void>(`/api/asos/${aso.id}`, { method: 'PUT', body: JSON.stringify(aso) }),
    excluir: (id: string) => request<void>(`/api/asos/${id}`, { method: 'DELETE' }),
  },
  examesComplementares: {
    listar: (trabalhadorId?: string) =>
      request<ExameComplementar[]>(`/api/examescomplementares${trabalhadorId ? `?trabalhadorId=${trabalhadorId}` : ''}`),
    obterPorId: (id: string) => request<ExameComplementar>(`/api/examescomplementares/${id}`),
    criar: (exame: NovoExameComplementar) =>
      request<{ id: string }>('/api/examescomplementares', { method: 'POST', body: JSON.stringify(exame) }),
    atualizar: (exame: AtualizarExameComplementarPayload) =>
      request<void>(`/api/examescomplementares/${exame.id}`, { method: 'PUT', body: JSON.stringify(exame) }),
    excluir: (id: string) => request<void>(`/api/examescomplementares/${id}`, { method: 'DELETE' }),
  },
  aptidoes: {
    listar: (trabalhadorId?: string) =>
      request<Aptidao[]>(`/api/aptidoes${trabalhadorId ? `?trabalhadorId=${trabalhadorId}` : ''}`),
    obterPorId: (id: string) => request<Aptidao>(`/api/aptidoes/${id}`),
    criar: (aptidao: NovaAptidao) =>
      request<{ id: string }>('/api/aptidoes', { method: 'POST', body: JSON.stringify(aptidao) }),
    atualizar: (aptidao: AtualizarAptidaoPayload) =>
      request<void>(`/api/aptidoes/${aptidao.id}`, { method: 'PUT', body: JSON.stringify(aptidao) }),
    excluir: (id: string) => request<void>(`/api/aptidoes/${id}`, { method: 'DELETE' }),
  },
  pcmsos: {
    listar: (obraId?: string) => request<Pcmso[]>(`/api/pcmsos${obraId ? `?obraId=${obraId}` : ''}`),
    obterPorId: (id: string) => request<Pcmso>(`/api/pcmsos/${id}`),
    criar: (pcmso: NovoPcmso) => request<{ id: string }>('/api/pcmsos', { method: 'POST', body: JSON.stringify(pcmso) }),
    atualizar: (id: string, pcmso: AtualizarPcmsoPayload) =>
      // ...pcmso primeiro: pcmso é o Pcmso DTO (que tem seu próprio "id" = PcmsoDetalhe.Id) espalhado
      // em cima do NovoPcmso; "id" precisa vir por último para sempre prevalecer como o
      // documentoGestaoId esperado pela rota (PcmsoDetalhePage navega/edita por documentoGestaoId).
      request<void>(`/api/pcmsos/${id}`, { method: 'PUT', body: JSON.stringify({ ...pcmso, id }) }),
    excluir: (id: string) => request<void>(`/api/pcmsos/${id}`, { method: 'DELETE' }),
  },
  cursosTreinamento: {
    listar: () => request<CursoTreinamento[]>('/api/cursostreinamento'),
    criar: (curso: NovoCursoTreinamento) =>
      request<{ id: string }>('/api/cursostreinamento', { method: 'POST', body: JSON.stringify(curso) }),
    excluir: (id: string) => request<void>(`/api/cursostreinamento/${id}`, { method: 'DELETE' }),
  },
  treinamentos: {
    listar: (trabalhadorId?: string) =>
      request<Treinamento[]>(`/api/treinamentos${trabalhadorId ? `?trabalhadorId=${trabalhadorId}` : ''}`),
    criar: (treinamento: NovoTreinamento) =>
      request<{ id: string }>('/api/treinamentos', { method: 'POST', body: JSON.stringify(treinamento) }),
    excluir: (id: string) => request<void>(`/api/treinamentos/${id}`, { method: 'DELETE' }),
  },
  requisitosLegais: {
    listar: (categoria?: number, status?: number) => {
      const params = new URLSearchParams();
      if (categoria) params.set('categoria', String(categoria));
      if (status) params.set('status', String(status));
      const qs = params.toString();
      return request<RequisitoLegal[]>(`/api/requisitoslegais${qs ? `?${qs}` : ''}`);
    },
    obterDetalhe: (id: string) => request<RequisitoLegalDetalhe>(`/api/requisitoslegais/${id}`),
    criar: (requisito: NovoRequisitoLegal) =>
      request<{ id: string }>('/api/requisitoslegais', { method: 'POST', body: JSON.stringify(requisito) }),
    atualizar: (id: string, requisito: AtualizarRequisitoLegalPayload) =>
      request<void>(`/api/requisitoslegais/${id}`, { method: 'PUT', body: JSON.stringify({ ...requisito, id }) }),
    excluir: (id: string) => request<void>(`/api/requisitoslegais/${id}`, { method: 'DELETE' }),
    definirCriterios: (id: string, criterios: CriterioAplicabilidadeInput[]) =>
      request<void>(`/api/requisitoslegais/${id}/criterios`, { method: 'PUT', body: JSON.stringify({ criterios }) }),
  },
  questionarioAplicabilidade: {
    listarItens: () => request<ItemQuestionarioAplicabilidade[]>('/api/questionario-aplicabilidade/itens'),
    criarItem: (pergunta: string, textoApoio?: string | null) =>
      request<{ id: string }>('/api/questionario-aplicabilidade/itens', {
        method: 'POST',
        body: JSON.stringify({ pergunta, textoApoio }),
      }),
    atualizarItem: (id: string, pergunta: string, textoApoio?: string | null) =>
      request<void>(`/api/questionario-aplicabilidade/itens/${id}`, {
        method: 'PUT',
        body: JSON.stringify({ pergunta, textoApoio }),
      }),
    excluirItem: (id: string) => request<void>(`/api/questionario-aplicabilidade/itens/${id}`, { method: 'DELETE' }),
    obterQuestionarioObra: (obraId: string) =>
      request<RespostaQuestionarioObra[]>(`/api/questionario-aplicabilidade/obras/${obraId}`),
    responder: (obraId: string, itemId: string, resposta: boolean, observacao?: string | null) =>
      request<void>(`/api/questionario-aplicabilidade/obras/${obraId}/itens/${itemId}`, {
        method: 'PUT',
        body: JSON.stringify({ resposta, observacao }),
      }),
  },
  catalogosEpi: {
    listar: () => request<CatalogoEpi[]>('/api/catalogosepi'),
    criar: (epi: NovoCatalogoEpi) =>
      request<{ id: string }>('/api/catalogosepi', { method: 'POST', body: JSON.stringify(epi) }),
    atualizar: (epi: AtualizarCatalogoEpi) =>
      request<void>(`/api/catalogosepi/${epi.id}`, { method: 'PUT', body: JSON.stringify(epi) }),
    excluir: (id: string) => request<void>(`/api/catalogosepi/${id}`, { method: 'DELETE' }),
  },
  estoquesEpi: {
    listarPorObra: (obraId: string) => request<EstoqueEpiPorObra[]>(`/api/estoquesepi/obra/${obraId}`),
    listarMovimentacoes: (obraId: string, catalogoEpiId: string) =>
      request<MovimentacaoEstoqueEpi[]>(`/api/estoquesepi/obra/${obraId}/epi/${catalogoEpiId}/movimentacoes`),
    registrarEntrada: (dados: RegistrarEntradaEstoqueEpi) =>
      request<void>('/api/estoquesepi/entrada', { method: 'POST', body: JSON.stringify(dados) }),
    ajustar: (dados: AjustarEstoqueEpi) =>
      request<void>('/api/estoquesepi/ajuste', { method: 'POST', body: JSON.stringify(dados) }),
  },
  entregasEpi: {
    listar: (trabalhadorId?: string) =>
      request<EntregaEpi[]>(`/api/entregasepi${trabalhadorId ? `?trabalhadorId=${trabalhadorId}` : ''}`),
    obterPorId: (id: string) => request<EntregaEpi>(`/api/entregasepi/${id}`),
    criar: (entrega: NovaEntregaEpi) =>
      request<{ id: string }>('/api/entregasepi', { method: 'POST', body: JSON.stringify(entrega) }),
    atualizar: (entrega: AtualizarEntregaEpi) =>
      request<void>(`/api/entregasepi/${entrega.id}`, { method: 'PUT', body: JSON.stringify(entrega) }),
    excluir: (id: string) => request<void>(`/api/entregasepi/${id}`, { method: 'DELETE' }),
    baixarFichaTrabalhador: async (trabalhadorId: string) => {
      const response = await fetch(`${API_BASE_URL}/api/entregasepi/ficha-trabalhador/${trabalhadorId}/pdf`, {
        headers: await montarHeadersAuth(),
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
  },
  atividades: {
    listar: (obraId?: string) => request<Atividade[]>(`/api/atividades${obraId ? `?obraId=${obraId}` : ''}`),
    criar: (atividade: NovaAtividade) =>
      request<{ id: string }>('/api/atividades', { method: 'POST', body: JSON.stringify(atividade) }),
    excluir: (id: string) => request<void>(`/api/atividades/${id}`, { method: 'DELETE' }),
  },
  perigos: {
    listar: () => request<Perigo[]>('/api/perigos'),
    criar: (perigo: NovoPerigo) =>
      request<{ id: string }>('/api/perigos', { method: 'POST', body: JSON.stringify(perigo) }),
    excluir: (id: string) => request<void>(`/api/perigos/${id}`, { method: 'DELETE' }),
  },
  matrizRisco: {
    listar: () => request<MatrizRiscoConfig[]>('/api/matrizrisco'),
    criar: (matriz: NovaMatrizRiscoConfig) =>
      request<{ id: string }>('/api/matrizrisco', { method: 'POST', body: JSON.stringify(matriz) }),
    excluir: (id: string) => request<void>(`/api/matrizrisco/${id}`, { method: 'DELETE' }),
  },
  riscos: {
    listar: (atividadeId?: string) => request<Risco[]>(`/api/riscos${atividadeId ? `?atividadeId=${atividadeId}` : ''}`),
    criar: (risco: NovoRisco) => request<{ id: string }>('/api/riscos', { method: 'POST', body: JSON.stringify(risco) }),
    excluir: (id: string) => request<void>(`/api/riscos/${id}`, { method: 'DELETE' }),
  },
  pgrs: {
    listar: (obraId?: string) => request<Pgr[]>(`/api/pgrs${obraId ? `?obraId=${obraId}` : ''}`),
    obterDetalhe: (id: string) => request<PgrDetalhe>(`/api/pgrs/${id}`),
    criar: (pgr: NovoPgr) => request<{ id: string }>('/api/pgrs', { method: 'POST', body: JSON.stringify(pgr) }),
    atualizar: (id: string, pgr: Pgr) =>
      request<void>(`/api/pgrs/${id}`, { method: 'PUT', body: JSON.stringify(pgr) }),
    excluir: (id: string) => request<void>(`/api/pgrs/${id}`, { method: 'DELETE' }),
  },
  planoAcao: {
    listar: (pgrId: string) => request<PlanoAcaoItem[]>(`/api/planoacao?pgrId=${pgrId}`),
    criar: (item: NovoPlanoAcaoItem) =>
      request<{ id: string }>('/api/planoacao', { method: 'POST', body: JSON.stringify(item) }),
    atualizar: (id: string, item: PlanoAcaoItem) =>
      request<void>(`/api/planoacao/${id}`, { method: 'PUT', body: JSON.stringify(item) }),
    excluir: (id: string) => request<void>(`/api/planoacao/${id}`, { method: 'DELETE' }),
  },
  pgrRevisoes: {
    listar: (pgrId: string) => request<PgrRevisao[]>(`/api/pgrrevisoes?pgrId=${pgrId}`),
    criar: (revisao: NovaPgrRevisao) =>
      request<{ id: string }>('/api/pgrrevisoes', { method: 'POST', body: JSON.stringify(revisao) }),
  },
  areasSst: {
    listar: (obraId?: string) => request<AreaSst[]>(`/api/areassst${obraId ? `?obraId=${obraId}` : ''}`),
    criar: (area: NovaAreaSst) => request<{ id: string }>('/api/areassst', { method: 'POST', body: JSON.stringify(area) }),
    atualizar: (id: string, area: AreaSst) =>
      request<void>(`/api/areassst/${id}`, { method: 'PUT', body: JSON.stringify(area) }),
    excluir: (id: string) => request<void>(`/api/areassst/${id}`, { method: 'DELETE' }),
  },
  tagsIdentificacao: {
    listar: (status?: number, tipo?: number) => {
      const params = new URLSearchParams();
      if (status) params.set('status', String(status));
      if (tipo) params.set('tipo', String(tipo));
      const query = params.toString();
      return request<TagIdentificacao[]>(`/api/tagsidentificacao${query ? `?${query}` : ''}`);
    },
    criar: (tag: NovaTagIdentificacao) =>
      request<{ id: string }>('/api/tagsidentificacao', { method: 'POST', body: JSON.stringify(tag) }),
    vincular: (id: string, entidadeVinculadaTipo: number, entidadeVinculadaId: string) =>
      request<void>(`/api/tagsidentificacao/${id}/vincular`, {
        method: 'POST',
        body: JSON.stringify({ entidadeVinculadaTipo, entidadeVinculadaId }),
      }),
    desvincular: (id: string) => request<void>(`/api/tagsidentificacao/${id}/desvincular`, { method: 'POST' }),
    atualizarStatus: (id: string, status: number) =>
      request<void>(`/api/tagsidentificacao/${id}/status`, { method: 'PUT', body: JSON.stringify({ status }) }),
    resolverPorUid: (uid: string) => request<ResolverTagDto>(`/api/tagsidentificacao/resolver/${encodeURIComponent(uid)}`),
    vincularPorUid: (uid: string, entidadeVinculadaTipo: number, entidadeVinculadaId: string) =>
      request<void>('/api/tagsidentificacao/vincular-por-uid', {
        method: 'POST',
        body: JSON.stringify({ uid, entidadeVinculadaTipo, entidadeVinculadaId }),
      }),
    excluir: (id: string) => request<void>(`/api/tagsidentificacao/${id}`, { method: 'DELETE' }),
  },
  identificacaoPublica: {
    resolver: (codigoOuUid: string) => request<AreaPublicaDto>(`/sst/p/${encodeURIComponent(codigoOuUid)}`),
  },
  validacaoPublica: {
    resolver: (token: string) => request<DocumentoPublico>(`/sst/validar/${encodeURIComponent(token)}`),
  },
  aprs: {
    listar: (atividadeId?: string) => request<Apr[]>(`/api/aprs${atividadeId ? `?atividadeId=${atividadeId}` : ''}`),
    obterDetalhe: (id: string) => request<AprDetalhe>(`/api/aprs/${id}`),
    criar: (apr: NovaApr) => request<{ id: string }>('/api/aprs', { method: 'POST', body: JSON.stringify(apr) }),
    atualizar: (id: string, apr: AtualizarAprPayload) =>
      request<void>(`/api/aprs/${id}`, { method: 'PUT', body: JSON.stringify(apr) }),
    excluir: (id: string) => request<void>(`/api/aprs/${id}`, { method: 'DELETE' }),
    aprovar: (id: string, aprovadoPorUsuarioId: string) =>
      request<void>(`/api/aprs/${id}/aprovar`, { method: 'POST', body: JSON.stringify({ aprovadoPorUsuarioId }) }),
    reprovar: (id: string, motivo: string) =>
      request<void>(`/api/aprs/${id}/reprovar`, { method: 'POST', body: JSON.stringify({ motivo }) }),
    exportarPdf: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/aprs/${id}/pdf`, {
        headers: await montarHeadersAuth(),
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
  },
  aprEtapas: {
    listar: (aprId: string) => request<AprEtapa[]>(`/api/aprEtapas?aprId=${aprId}`),
    criar: (etapa: NovaAprEtapa) =>
      request<{ id: string }>('/api/aprEtapas', { method: 'POST', body: JSON.stringify(etapa) }),
    excluir: (id: string) => request<void>(`/api/aprEtapas/${id}`, { method: 'DELETE' }),
    criarRisco: (risco: NovoAprEtapaRisco) =>
      request<{ id: string }>('/api/aprEtapas/riscos', { method: 'POST', body: JSON.stringify(risco) }),
    atualizarRisco: (id: string, risco: Omit<NovoAprEtapaRisco, 'aprEtapaId'>) =>
      request<void>(`/api/aprEtapas/riscos/${id}`, { method: 'PUT', body: JSON.stringify(risco) }),
    excluirRisco: (id: string) => request<void>(`/api/aprEtapas/riscos/${id}`, { method: 'DELETE' }),
  },
  aprAssinaturas: {
    listar: (aprId: string) => request<AprAssinatura[]>(`/api/aprAssinaturas?aprId=${aprId}`),
    criar: (assinatura: NovaAprAssinatura) =>
      request<{ id: string }>('/api/aprAssinaturas', { method: 'POST', body: JSON.stringify(assinatura) }),
  },
  calendario: {
    obter: (inicio: Date, fim: Date) =>
      request<Calendario>(
        `/api/calendario?inicio=${encodeURIComponent(inicio.toISOString())}&fim=${encodeURIComponent(fim.toISOString())}`,
      ),
  },
  permissoesTrabalho: {
    listar: (atividadeId?: string) =>
      request<PermissaoTrabalho[]>(`/api/permissoesTrabalho${atividadeId ? `?atividadeId=${atividadeId}` : ''}`),
    obterDetalhe: (id: string) => request<PermissaoTrabalhoDetalhe>(`/api/permissoesTrabalho/${id}`),
    criar: (pt: NovaPermissaoTrabalho) =>
      request<{ id: string }>('/api/permissoesTrabalho', { method: 'POST', body: JSON.stringify(pt) }),
    atualizar: (id: string, pt: AtualizarPermissaoTrabalhoPayload) =>
      request<void>(`/api/permissoesTrabalho/${id}`, { method: 'PUT', body: JSON.stringify(pt) }),
    excluir: (id: string) => request<void>(`/api/permissoesTrabalho/${id}`, { method: 'DELETE' }),
    autorizar: (id: string, autorizadoPorUsuarioId: string, responsavelSstUsuarioId?: string | null) =>
      request<void>(`/api/permissoesTrabalho/${id}/autorizar`, {
        method: 'POST',
        body: JSON.stringify({ autorizadoPorUsuarioId, responsavelSstUsuarioId: responsavelSstUsuarioId || null }),
      }),
    suspender: (id: string, motivo: string, suspensaPorUsuarioId: string) =>
      request<void>(`/api/permissoesTrabalho/${id}/suspender`, {
        method: 'POST',
        body: JSON.stringify({ motivo, suspensaPorUsuarioId }),
      }),
    revalidar: (id: string, novaValidade: string, novoHorarioFim: string | null, revalidadaPorUsuarioId: string) =>
      request<void>(`/api/permissoesTrabalho/${id}/revalidar`, {
        method: 'POST',
        body: JSON.stringify({ novaValidade, novoHorarioFim, revalidadaPorUsuarioId }),
      }),
    encerrar: (id: string, encerradaPorUsuarioId: string, observacoes?: string | null) =>
      request<void>(`/api/permissoesTrabalho/${id}/encerrar`, {
        method: 'POST',
        body: JSON.stringify({ encerradaPorUsuarioId, observacoes }),
      }),
    marcarPreRequisito: (id: string, itemId: string, atendido: boolean) =>
      request<void>(`/api/permissoesTrabalho/${id}/pre-requisitos/${itemId}/marcar`, {
        method: 'POST',
        body: JSON.stringify({ atendido }),
      }),
    responderVerificacao: (id: string, itemId: string, resposta: number) =>
      request<void>(`/api/permissoesTrabalho/${id}/verificacoes/${itemId}/responder`, {
        method: 'POST',
        body: JSON.stringify({ resposta }),
      }),
    definirTiposTrabalho: (id: string, tipos: TipoTrabalhoPtInput[]) =>
      request<void>(`/api/permissoesTrabalho/${id}/tipos-trabalho`, {
        method: 'PUT',
        body: JSON.stringify({ tipos }),
      }),
    definirEpis: (id: string, itens: EpiPtInput[], outrosEpis?: string | null) =>
      request<void>(`/api/permissoesTrabalho/${id}/epis`, {
        method: 'PUT',
        body: JSON.stringify({ itens, outrosEpis }),
      }),
    definirEpcs: (id: string, itens: number[], outrosEpcs?: string | null) =>
      request<void>(`/api/permissoesTrabalho/${id}/epcs`, {
        method: 'PUT',
        body: JSON.stringify({ itens, outrosEpcs }),
      }),
    criarRiscoCritico: (risco: NovaPermissaoTrabalhoRiscoCritico) =>
      request<{ id: string }>('/api/permissoesTrabalho/riscos-criticos', {
        method: 'POST',
        body: JSON.stringify(risco),
      }),
    atualizarRiscoCritico: (
      riscoId: string,
      payload: { riscoCondicao: string; controleComplementar?: string | null; responsavelEvidencia?: string | null },
    ) =>
      request<void>(`/api/permissoesTrabalho/riscos-criticos/${riscoId}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      }),
    excluirRiscoCritico: (riscoId: string) =>
      request<void>(`/api/permissoesTrabalho/riscos-criticos/${riscoId}`, { method: 'DELETE' }),
    exportarPdf: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/permissoesTrabalho/${id}/pdf`, {
        headers: await montarHeadersAuth(),
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
  },
  usuarios: {
    listar: (status?: number) => request<Usuario[]>(`/api/usuarios${status ? `?status=${status}` : ''}`),
    obterPorId: (id: string) => request<Usuario>(`/api/usuarios/${id}`),
    criar: (usuario: NovoUsuario) =>
      request<{ id: string }>('/api/usuarios', { method: 'POST', body: JSON.stringify(usuario) }),
    atualizar: (id: string, usuario: AtualizarUsuarioPayload) =>
      request<void>(`/api/usuarios/${id}`, { method: 'PUT', body: JSON.stringify(usuario) }),
    excluir: (id: string) => request<void>(`/api/usuarios/${id}`, { method: 'DELETE' }),
    atribuirPerfilObra: (usuarioId: string, perfilAcessoId: string, obraId?: string | null) =>
      request<{ id: string }>('/api/usuarios/perfis-obra', {
        method: 'POST',
        body: JSON.stringify({ usuarioId, perfilAcessoId, obraId }),
      }),
    removerPerfilObra: (id: string) => request<void>(`/api/usuarios/perfis-obra/${id}`, { method: 'DELETE' }),
  },
  perfisAcesso: {
    listar: () => request<PerfilAcesso[]>('/api/perfisacesso'),
    obterPorId: (id: string) => request<PerfilAcesso>(`/api/perfisacesso/${id}`),
    criar: (perfil: NovoPerfilAcesso) =>
      request<{ id: string }>('/api/perfisacesso', { method: 'POST', body: JSON.stringify(perfil) }),
    atualizar: (id: string, perfil: NovoPerfilAcesso) =>
      request<void>(`/api/perfisacesso/${id}`, { method: 'PUT', body: JSON.stringify({ id, ...perfil }) }),
    excluir: (id: string) => request<void>(`/api/perfisacesso/${id}`, { method: 'DELETE' }),
    listarPermissoes: (id: string) =>
      request<PerfilAcessoPermissao[]>(`/api/perfisacesso/${id}/permissoes`),
    definirPermissoes: (id: string, itens: ItemPermissaoPerfil[]) =>
      request<void>(`/api/perfisacesso/${id}/permissoes`, { method: 'PUT', body: JSON.stringify(itens) }),
  },
  permissoes: {
    listar: (modulo?: string) =>
      request<Permissao[]>(`/api/permissoes${modulo ? `?modulo=${encodeURIComponent(modulo)}` : ''}`),
  },
  checklistModelos: {
    listar: (tipoInspecao?: number) =>
      request<ChecklistModelo[]>(`/api/checklistmodelos${tipoInspecao ? `?tipoInspecao=${tipoInspecao}` : ''}`),
    obterDetalhe: (id: string) => request<ChecklistModeloDetalhe>(`/api/checklistmodelos/${id}`),
    criar: (checklist: NovoChecklistModelo) =>
      request<{ id: string }>('/api/checklistmodelos', { method: 'POST', body: JSON.stringify(checklist) }),
    excluir: (id: string) => request<void>(`/api/checklistmodelos/${id}`, { method: 'DELETE' }),
    novaVersao: (id: string, itens: NovoChecklistModeloItem[]) =>
      request<{ id: string }>(`/api/checklistmodelos/${id}/novaVersao`, {
        method: 'POST',
        body: JSON.stringify({ itens }),
      }),
  },
  inspecoes: {
    listar: (obraId?: string) => request<Inspecao[]>(`/api/inspecoes${obraId ? `?obraId=${obraId}` : ''}`),
    obterDetalhe: (id: string) => request<InspecaoDetalhe>(`/api/inspecoes/${id}`),
    criar: (inspecao: NovaInspecao) =>
      request<{ id: string }>('/api/inspecoes', { method: 'POST', body: JSON.stringify(inspecao) }),
    responderItem: (
      respostaId: string,
      statusItem: number,
      observacao?: string | null,
      responsavelUsuarioId?: string | null,
      prazo?: string | null,
      descricaoPersonalizada?: string | null,
      local?: string | null,
      planoDeAcao?: string | null,
    ) =>
      request<void>(`/api/inspecoes/respostas/${respostaId}`, {
        method: 'POST',
        body: JSON.stringify({
          statusItem,
          observacao,
          responsavelUsuarioId,
          prazo,
          descricaoPersonalizada,
          local,
          planoDeAcao,
        }),
      }),
    anexarFoto: async (respostaId: string, foto: File) => {
      const formData = new FormData();
      formData.append('foto', foto);
      const response = await fetch(`${API_BASE_URL}/api/inspecoes/respostas/${respostaId}/foto`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
    },
    baixarFoto: async (respostaId: string) => {
      const response = await fetch(`${API_BASE_URL}/api/inspecoes/respostas/${respostaId}/foto`, {
        headers: await montarHeadersAuth(),
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
    // Evidência posterior (depois de resolvido o achado) — par dos dois métodos acima, pedido
    // "Patrulha de Segurança do Trabalho" (planilha do usuário, 31/08).
    anexarFotoDepois: async (respostaId: string, foto: File) => {
      const formData = new FormData();
      formData.append('foto', foto);
      const response = await fetch(`${API_BASE_URL}/api/inspecoes/respostas/${respostaId}/foto-depois`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
    },
    baixarFotoDepois: async (respostaId: string) => {
      const response = await fetch(`${API_BASE_URL}/api/inspecoes/respostas/${respostaId}/foto-depois`, {
        headers: await montarHeadersAuth(),
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
    encerrar: (id: string) => request<void>(`/api/inspecoes/${id}/encerrar`, { method: 'POST' }),
    baixarPdf: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/inspecoes/${id}/pdf`, { headers: await montarHeadersAuth() });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
    gerarOcorrencia: (respostaId: string, body: {
      requisitoRelacionado?: string | null;
      local?: string | null;
      riscoId?: string | null;
      responsavelUsuarioId?: string | null;
      prazo?: string | null;
    }) =>
      request<{ id: string }>(`/api/inspecoes/respostas/${respostaId}/gerar-ocorrencia`, {
        method: 'POST',
        body: JSON.stringify(body),
      }),
  },
  dds: {
    listar: (obraId?: string) => request<Dds[]>(`/api/dds${obraId ? `?obraId=${obraId}` : ''}`),
    obterDetalhe: (id: string) => request<DdsDetalhe>(`/api/dds/${id}`),
    criar: (dds: NovaDds) => request<{ id: string }>('/api/dds', { method: 'POST', body: JSON.stringify(dds) }),
    marcarItem: (itemId: string, verificado: boolean) =>
      request<void>(`/api/dds/itens/${itemId}/marcar`, { method: 'POST', body: JSON.stringify({ verificado }) }),
    registrarParticipante: async (ddsId: string, trabalhadorId: string, fotoTipo: number, foto: File) => {
      const formData = new FormData();
      formData.append('trabalhadorId', trabalhadorId);
      formData.append('fotoTipo', String(fotoTipo));
      formData.append('foto', foto);
      const authHeaders = await montarHeadersAuth();
      return syncMutateMultipart<{ id: string }>(`/api/dds/${ddsId}/participantes`, formData, authHeaders);
    },
    encerrar: (id: string) => request<void>(`/api/dds/${id}/encerrar`, { method: 'POST' }),
    // PDF gerado sob demanda no servidor a partir do estado atual — não faz sentido cachear para
    // uso offline (ficaria sempre desatualizado assim que o DDS mudasse). Segue fetch direto.
    baixarPdf: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/dds/${id}/pdf`, { headers: await montarHeadersAuth() });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
    baixarFotoParticipante: async (participanteId: string) => {
      const authHeaders = await montarHeadersAuth();
      return syncFetchBlob(`/api/dds/participantes/${participanteId}/foto`, authHeaders);
    },
    enviarTelegram: (id: string) =>
      request<EnviarDdsTelegramResultado>(`/api/dds/${id}/telegram/enviar`, { method: 'POST' }),
  },
  assinatura: {
    obter: async (entidadeTipo: string, entidadeId: string) => {
      const query = new URLSearchParams({ entidadeTipo, entidadeId });
      const response = await fetch(`${API_BASE_URL}/api/documentos?${query.toString()}`, {
        headers: await montarHeadersAuth(),
      });
      if (response.status === 404) return null;
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return (await response.json()) as DocumentoAssinatura;
    },
    criar: (entidadeTipo: string, entidadeId: string) =>
      request<{ id: string }>('/api/documentos', {
        method: 'POST',
        body: JSON.stringify({ entidadeTipo, entidadeId }),
      }),
    // Assinatura em um clique do usuário logado (entregador) — sem uid/pin, o backend resolve o
    // trabalhador a partir da sessão autenticada (claim "oid" do Entra ID).
    assinarComSessao: (documentoId: string) =>
      request<DocumentoSignatario>(`/api/documentos/${documentoId}/assinar/sessao`, { method: 'POST' }),
    // Autenticação via biometria digital local (Futronic FS80H) — dispositivoId/segredoDispositivo
    // vêm do agente local (fetch a /api/dispositivo), nunca de localStorage.
    autenticarBiometriaLocal: (documentoId: string, dispositivoId: string, segredoDispositivo: string, trabalhadorId: string, score: number) =>
      request<DocumentoSignatario>(`/api/documentos/${documentoId}/autenticacao/biometria-local`, {
        method: 'POST',
        body: JSON.stringify({ dispositivoId, segredoDispositivo, trabalhadorId, score }),
      }),
    listar: (filtros?: { entidadeTipo?: string; status?: number; dataInicio?: string; dataFim?: string }) => {
      const query = new URLSearchParams();
      if (filtros?.entidadeTipo) query.set('entidadeTipo', filtros.entidadeTipo);
      if (filtros?.status) query.set('status', String(filtros.status));
      if (filtros?.dataInicio) query.set('dataInicio', filtros.dataInicio);
      if (filtros?.dataFim) query.set('dataFim', filtros.dataFim);
      const suffix = query.toString();
      return request<DocumentoAssinaturaResumo[]>(`/api/documentos/listar${suffix ? `?${suffix}` : ''}`);
    },
    baixarPdf: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/documentos/${id}/pdf`, { headers: await montarHeadersAuth() });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
  },
  ativos: {
    listar: (obraId?: string, tipoAtivo?: number) => {
      const params = new URLSearchParams();
      if (obraId) params.set('obraId', obraId);
      if (tipoAtivo) params.set('tipoAtivo', String(tipoAtivo));
      const query = params.toString();
      return request<AtivoSst[]>(`/api/ativos${query ? `?${query}` : ''}`);
    },
    obterDetalhe: (id: string) => request<AtivoSst>(`/api/ativos/${id}`),
    criar: (ativo: NovoAtivoSst) =>
      request<{ id: string }>('/api/ativos', { method: 'POST', body: JSON.stringify(ativo) }),
    atualizar: (id: string, ativo: AtivoSst) =>
      request<void>(`/api/ativos/${id}`, { method: 'PUT', body: JSON.stringify(ativo) }),
    excluir: (id: string) => request<void>(`/api/ativos/${id}`, { method: 'DELETE' }),
  },
  naoConformidades: {
    listar: (status?: number) =>
      request<NaoConformidade[]>(`/api/naoconformidades${status ? `?status=${status}` : ''}`),
    obterDetalhe: (id: string) => request<NaoConformidadeDetalhe>(`/api/naoconformidades/${id}`),
    criar: (nc: NovaNaoConformidade) =>
      request<{ id: string }>('/api/naoconformidades', { method: 'POST', body: JSON.stringify(nc) }),
    atualizar: (id: string, nc: AtualizarNaoConformidadePayload) =>
      request<void>(`/api/naoconformidades/${id}`, { method: 'PUT', body: JSON.stringify(nc) }),
    excluir: (id: string) => request<void>(`/api/naoconformidades/${id}`, { method: 'DELETE' }),
    enviar: (id: string) =>
      request<void>(`/api/naoconformidades/${id}/enviar`, { method: 'POST' }),
    responder: (id: string, body: {
      descricaoAcao: string;
      responsavelExecucaoId?: string | null;
      prioridade: number;
      prazo?: string | null;
      justificativaPrazo?: string | null;
    }) =>
      request<{ acaoId: string }>(`/api/naoconformidades/${id}/responder`, {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    registrarConclusao: (id: string, descricaoConclusao?: string | null) =>
      request<void>(`/api/naoconformidades/${id}/registrar-conclusao`, {
        method: 'POST',
        body: JSON.stringify({ descricaoConclusao }),
      }),
    devolver: (id: string, motivo: string) =>
      request<void>(`/api/naoconformidades/${id}/devolver`, {
        method: 'POST',
        body: JSON.stringify({ motivo }),
      }),
    encerrar: (id: string, validadoPorUsuarioId: string, observacoesEncerramento?: string | null) =>
      request<void>(`/api/naoconformidades/${id}/encerrar`, {
        method: 'POST',
        body: JSON.stringify({ validadoPorUsuarioId, observacoesEncerramento }),
      }),
  },
  acoesPlano: {
    listar: (origemTipo: string, origemId: string) =>
      request<AcaoPlano[]>(`/api/acoesplano?origemTipo=${origemTipo}&origemId=${origemId}`),
    criar: (acao: NovaAcaoPlano) =>
      request<{ id: string }>('/api/acoesplano', { method: 'POST', body: JSON.stringify(acao) }),
    atualizar: (id: string, acao: AtualizarAcaoPlanoPayload) =>
      request<void>(`/api/acoesplano/${id}`, { method: 'PUT', body: JSON.stringify(acao) }),
    excluir: (id: string) => request<void>(`/api/acoesplano/${id}`, { method: 'DELETE' }),
    validar: (id: string, validadoPorUsuarioId: string) =>
      request<void>(`/api/acoesplano/${id}/validar`, {
        method: 'POST',
        body: JSON.stringify({ validadoPorUsuarioId }),
      }),
  },
  acidentes: {
    listar: (filtros?: { tipo?: number; status?: number; obraId?: string }) => {
      const params = new URLSearchParams();
      if (filtros?.tipo) params.set('tipo', String(filtros.tipo));
      if (filtros?.status) params.set('status', String(filtros.status));
      if (filtros?.obraId) params.set('obraId', filtros.obraId);
      const query = params.toString();
      return request<Acidente[]>(`/api/acidentes${query ? `?${query}` : ''}`);
    },
    obterDetalhe: (id: string) => request<AcidenteDetalhe>(`/api/acidentes/${id}`),
    criar: (acidente: NovoAcidente) =>
      request<{ id: string }>('/api/acidentes', { method: 'POST', body: JSON.stringify(acidente) }),
    atualizar: (id: string, acidente: AtualizarAcidentePayload) =>
      request<void>(`/api/acidentes/${id}`, { method: 'PUT', body: JSON.stringify(acidente) }),
    excluir: (id: string) => request<void>(`/api/acidentes/${id}`, { method: 'DELETE' }),
    avancarStatus: (id: string) => request<void>(`/api/acidentes/${id}/avancar-status`, { method: 'POST' }),
  },
  registrosHht: {
    listar: (filtros?: { obraId?: string; ano?: number }) => {
      const params = new URLSearchParams();
      if (filtros?.obraId) params.set('obraId', filtros.obraId);
      if (filtros?.ano) params.set('ano', String(filtros.ano));
      const query = params.toString();
      return request<RegistroHhtMensal[]>(`/api/registroshhtmensais${query ? `?${query}` : ''}`);
    },
    criar: (registro: NovoRegistroHhtMensal) =>
      request<{ id: string }>('/api/registroshhtmensais', { method: 'POST', body: JSON.stringify(registro) }),
    atualizar: (id: string, registro: AtualizarRegistroHhtMensalPayload) =>
      request<void>(`/api/registroshhtmensais/${id}`, { method: 'PUT', body: JSON.stringify(registro) }),
    excluir: (id: string) => request<void>(`/api/registroshhtmensais/${id}`, { method: 'DELETE' }),
  },
  alertas: {
    listar: (filtros?: { status?: number; severidade?: number; obraId?: string; trabalhadorId?: string }) => {
      const params = new URLSearchParams();
      if (filtros?.status) params.set('status', String(filtros.status));
      if (filtros?.severidade) params.set('severidade', String(filtros.severidade));
      if (filtros?.obraId) params.set('obraId', filtros.obraId);
      if (filtros?.trabalhadorId) params.set('trabalhadorId', filtros.trabalhadorId);
      const query = params.toString();
      return request<Alerta[]>(`/api/alertas${query ? `?${query}` : ''}`);
    },
    obterPorId: (id: string) => request<Alerta>(`/api/alertas/${id}`),
    criar: (alerta: NovoAlerta) =>
      request<{ id: string }>('/api/alertas', { method: 'POST', body: JSON.stringify(alerta) }),
    atualizar: (id: string, alerta: AtualizarAlertaPayload) =>
      request<void>(`/api/alertas/${id}`, { method: 'PUT', body: JSON.stringify(alerta) }),
    excluir: (id: string) => request<void>(`/api/alertas/${id}`, { method: 'DELETE' }),
    iniciarTratamento: (id: string) => request<void>(`/api/alertas/${id}/iniciar-tratamento`, { method: 'POST' }),
    escalonar: (id: string, escalonadoParaUsuarioId: string) =>
      request<void>(`/api/alertas/${id}/escalonar`, {
        method: 'POST',
        body: JSON.stringify({ escalonadoParaUsuarioId }),
      }),
    resolver: (id: string) => request<void>(`/api/alertas/${id}/resolver`, { method: 'POST' }),
    ignorar: (id: string) => request<void>(`/api/alertas/${id}/ignorar`, { method: 'POST' }),
  },
  regrasAlerta: {
    listar: () => request<RegraAlerta[]>('/api/regrasalerta'),
    criar: (regra: NovaRegraAlerta) =>
      request<{ id: string }>('/api/regrasalerta', { method: 'POST', body: JSON.stringify(regra) }),
    atualizar: (id: string, regra: RegraAlerta) =>
      request<void>(`/api/regrasalerta/${id}`, { method: 'PUT', body: JSON.stringify(regra) }),
    excluir: (id: string) => request<void>(`/api/regrasalerta/${id}`, { method: 'DELETE' }),
  },
  elegibilidade: {
    avaliar: (query: AvaliarElegibilidadeQuery) =>
      request<EligibilityResult>('/api/elegibilidade/avaliar', { method: 'POST', body: JSON.stringify(query) }),
  },
  trilhaAuditoria: {
    listar: (filtros?: {
      entidadeTipo?: string;
      entidadeId?: string;
      usuarioId?: string;
      dataInicio?: string;
      dataFim?: string;
    }) => {
      const params = new URLSearchParams();
      if (filtros?.entidadeTipo) params.set('entidadeTipo', filtros.entidadeTipo);
      if (filtros?.entidadeId) params.set('entidadeId', filtros.entidadeId);
      if (filtros?.usuarioId) params.set('usuarioId', filtros.usuarioId);
      if (filtros?.dataInicio) params.set('dataInicio', filtros.dataInicio);
      if (filtros?.dataFim) params.set('dataFim', filtros.dataFim);
      const query = params.toString();
      return request<TrilhaAuditoria[]>(`/api/trilhaauditoria${query ? `?${query}` : ''}`);
    },
  },
  dispositivosAgente: {
    // Chamado uma vez na configuração inicial de cada totem/quiosque (tela administrativa,
    // fora de escopo deste plano) — retorna o segredo em claro, exibido uma única vez.
    registrar: (obraId: string, nome: string) =>
      request<string>('/api/dispositivos-agente', {
        method: 'POST',
        body: JSON.stringify({ obraId, nome }),
      }),
  },
};
