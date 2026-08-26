import { authentication } from '@microsoft/teams-js';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7095';

// Fora de um host real do Teams (ex.: dev local no navegador), getAuthToken() tem dois
// comportamentos possíveis: rejeita rápido com "library not initialized" nos primeiros
// instantes após o mount, OU — passada essa janela — nunca resolve nem rejeita, pois fica
// esperando resposta de um frame pai do Teams que não existe. Sem o timeout, esse segundo
// caso travaria para sempre qualquer requisição feita depois do boot inicial do SDK. Em
// ambos os casos seguimos sem o header, como já era o comportamento (API local roda sem auth).
async function obterTokenAutenticacaoTeams(): Promise<string | null> {
  try {
    return await Promise.race([
      authentication.getAuthToken(),
      new Promise<null>((resolve) => setTimeout(() => resolve(null), 3000)),
    ]);
  } catch {
    return null;
  }
}

async function montarHeadersAuth(): Promise<Record<string, string>> {
  const token = await obterTokenAutenticacaoTeams();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

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
}

export type NovaObra = Omit<Obra, 'id' | 'dataTerminoReal'>;

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
}

export type NovoTrabalhador = Omit<
  Trabalhador,
  'id' | 'dataDemissao' | 'telegramVinculado' | 'telegramCodigoVinculo'
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

export interface CatalogoEpi {
  id: string;
  nome: string;
  certificadoAprovacaoNumero?: string | null;
  certificadoAprovacaoValidade?: string | null;
  vidaUtilEmMeses: number;
}

export type NovoCatalogoEpi = Omit<CatalogoEpi, 'id'>;

export interface EntregaEpi {
  id: string;
  trabalhadorId: string;
  catalogoEpiId: string;
  dataEntrega: string;
  dataDevolucao?: string | null;
  dataValidade?: string | null;
  assinaturaColetada: boolean;
}

export type NovaEntregaEpi = Omit<EntregaEpi, 'id'>;

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

export const PapelAssinaturaApr = {
  Elaborador: 1,
  Executante: 2,
  Aprovador: 3,
} as const;

export const papelAssinaturaAprLabel: Record<number, string> = {
  1: 'Elaborador',
  2: 'Executante',
  3: 'Aprovador',
};

export interface Apr {
  id: string;
  atividadeId: string;
  atividadeNome: string;
  local: string;
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
  atividadeId: string;
  local: string;
  equipeId?: string | null;
  data: string;
  validade?: string | null;
  responsaveisIds: string[];
}

export interface AtualizarAprPayload {
  id: string;
  atividadeId: string;
  local: string;
  equipeId?: string | null;
  data: string;
  validade?: string | null;
  responsaveisIds: string[];
}

export interface AprEtapa {
  id: string;
  aprId: string;
  ordem: number;
  descricao: string;
  medidasPreventivas?: string | null;
  riscosIds: string[];
}

export interface NovaAprEtapa {
  aprId: string;
  ordem: number;
  descricao: string;
  medidasPreventivas?: string | null;
  riscosIds: string[];
}

export interface AprResponsavel {
  id: string;
  aprId: string;
  trabalhadorId: string;
  trabalhadorNome: string;
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

export const StatusPt = {
  EmElaboracao: 1,
  Autorizada: 2,
  Encerrada: 3,
} as const;

export const statusPtLabel: Record<number, string> = {
  1: 'Em elaboração',
  2: 'Autorizada',
  3: 'Encerrada',
};

export interface PermissaoTrabalho {
  id: string;
  atividadeId: string;
  atividadeNome: string;
  local: string;
  equipeId?: string | null;
  equipeNome?: string | null;
  data: string;
  horarioInicio?: string | null;
  horarioFim?: string | null;
  validade?: string | null;
  status: number;
  autorizadoPorUsuarioId?: string | null;
  autorizadoPorUsuarioNome?: string | null;
  dataAutorizacao?: string | null;
  encerradaPorUsuarioId?: string | null;
  encerradaPorUsuarioNome?: string | null;
  dataEncerramento?: string | null;
  observacoesEncerramento?: string | null;
}

export interface NovaPermissaoTrabalho {
  atividadeId: string;
  local: string;
  equipeId?: string | null;
  data: string;
  horarioInicio?: string | null;
  horarioFim?: string | null;
  validade?: string | null;
  perigosIds: string[];
  responsaveisIds: string[];
}

export interface AtualizarPermissaoTrabalhoPayload {
  id: string;
  atividadeId: string;
  local: string;
  equipeId?: string | null;
  data: string;
  horarioInicio?: string | null;
  horarioFim?: string | null;
  validade?: string | null;
  perigosIds: string[];
  responsaveisIds: string[];
}

export interface PermissaoTrabalhoPerigo {
  id: string;
  permissaoTrabalhoId: string;
  perigoId: string;
  perigoNome: string;
}

export interface PermissaoTrabalhoControle {
  id: string;
  permissaoTrabalhoId: string;
  descricao: string;
}

export interface NovaPermissaoTrabalhoControle {
  permissaoTrabalhoId: string;
  descricao: string;
}

export interface PermissaoTrabalhoRequisito {
  id: string;
  permissaoTrabalhoId: string;
  descricao: string;
  atendido: boolean;
}

export interface NovaPermissaoTrabalhoRequisito {
  permissaoTrabalhoId: string;
  descricao: string;
}

export interface PermissaoTrabalhoResponsavel {
  id: string;
  permissaoTrabalhoId: string;
  trabalhadorId: string;
  trabalhadorNome: string;
}

export interface PermissaoTrabalhoDetalhe {
  permissaoTrabalho: PermissaoTrabalho;
  perigos: PermissaoTrabalhoPerigo[];
  controles: PermissaoTrabalhoControle[];
  requisitos: PermissaoTrabalhoRequisito[];
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
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
  prazo?: string | null;
  temFoto: boolean;
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
export const MetodoAutenticacaoAssinatura = {
  Biometria: 1,
  CrachaPin: 2,
  QrCodePin: 3,
  WebAuthnCelular: 4,
} as const;

export const metodoAutenticacaoAssinaturaLabel: Record<number, string> = {
  1: 'Biometria',
  2: 'Crachá + PIN',
  3: 'QR Code + PIN',
  4: 'Celular (WebAuthn)',
};

export const StatusDocumentoAssinatura = {
  EmAndamento: 1,
  Finalizado: 2,
  Cancelado: 3,
} as const;

// Etapa 13 — leitor biométrico fixo da obra (credencial "discoverable", vários trabalhadores) vs.
// celular próprio do trabalhador (credencial vinculada a um único TrabalhadorId).
export const TipoAutenticadorWebAuthn = {
  LeitorObra: 1,
  CelularProprio: 2,
} as const;

export const tipoAutenticadorWebAuthnLabel: Record<number, string> = {
  1: 'Leitor biométrico da obra',
  2: 'Celular próprio',
};

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

export interface ItemHigienizacao {
  id: string;
  obraId: string;
  obraNome: string;
  nome: string;
  local?: string | null;
  periodicidadeDias: number;
  ultimaHigienizacaoEm?: string | null;
  proximoVencimentoEm: string;
  totalRegistros: number;
}

export interface NovoItemHigienizacao {
  obraId: string;
  nome: string;
  local?: string | null;
  periodicidadeDias: number;
}

export interface RegistroHigienizacao {
  id: string;
  trabalhadorId: string;
  trabalhadorNome: string;
  dataHora: string;
  observacoes?: string | null;
}

export interface ItemHigienizacaoDetalhe {
  item: ItemHigienizacao;
  registros: RegistroHigienizacao[];
}

// Motor Central de Alertas + Cadastro de Ativos (requisito do usuário, 2026-08-25): entidade única
// AtivoSst com campo discriminador TipoAtivo — diferente de Higienização, a validade aqui é um
// campo fixo (DataValidade), não calculada a partir de um histórico de registros.
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
  EmTratamento: 2,
  AguardandoValidacao: 3,
  Encerrada: 4,
} as const;

export const statusNaoConformidadeLabel: Record<number, string> = {
  1: 'Aberta',
  2: 'Em tratamento',
  3: 'Aguardando validação',
  4: 'Encerrada',
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

// Seção 32 da Base de Conhecimento (linha 811) — vocabulário literal: "Conforme/Não conforme".
export const StatusRequisitoLegal = {
  Conforme: 1,
  NaoConforme: 2,
} as const;

export const statusRequisitoLegalLabel: Record<number, string> = {
  1: 'Conforme',
  2: 'Não conforme',
};

export interface RequisitoLegal {
  id: string;
  codigo: string;
  norma: string;
  item?: string | null;
  tema: string;
  requisito: string;
  aplicabilidade: boolean;
  justificativa?: string | null;
  evidencia?: string | null;
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
  periodicidade?: string | null;
  prazo?: string | null;
  status: number;
  ultimaRevisao?: string | null;
  proximaRevisao?: string | null;
  obraId?: string | null;
  obraNome?: string | null;
}

export interface NovoRequisitoLegal {
  codigo: string;
  norma: string;
  item?: string | null;
  tema: string;
  requisito: string;
  aplicabilidade: boolean;
  justificativa?: string | null;
  evidencia?: string | null;
  responsavelUsuarioId?: string | null;
  periodicidade?: string | null;
  prazo?: string | null;
  ultimaRevisao?: string | null;
  proximaRevisao?: string | null;
  obraId?: string | null;
}

export type AtualizarRequisitoLegalPayload = NovoRequisitoLegal;

export interface RequisitoLegalDetalhe {
  requisitoLegal: RequisitoLegal;
  acoesPlano: AcaoPlano[];
}

// Seção 31 da Base de Conhecimento (linhas 767-769) — vocabulário literal de status:
// "Rascunho → Em aprovação → Vigente → Obsoleto → Cancelado".
export const StatusDocumentoGestao = {
  Rascunho: 1,
  EmAprovacao: 2,
  Vigente: 3,
  Obsoleto: 4,
  Cancelado: 5,
} as const;

export const statusDocumentoGestaoLabel: Record<number, string> = {
  1: 'Rascunho',
  2: 'Em aprovação',
  3: 'Vigente',
  4: 'Obsoleto',
  5: 'Cancelado',
};

export interface DocumentoGestao {
  id: string;
  nome: string;
  tipo?: string | null;
  categoria?: string | null;
  origemDocumento?: string | null;
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
  versao?: string | null;
  validade?: string | null;
  dataEmissao: string;
  dataRevisao?: string | null;
  requisitoLegalId?: string | null;
  requisitoLegalCodigo?: string | null;
  obraId?: string | null;
  obraNome?: string | null;
  setorId?: string | null;
  setorNome?: string | null;
  status: number;
  arquivo?: string | null;
}

export interface NovoDocumentoGestao {
  nome: string;
  tipo?: string | null;
  categoria?: string | null;
  origemDocumento?: string | null;
  responsavelUsuarioId?: string | null;
  versao?: string | null;
  validade?: string | null;
  dataEmissao: string;
  dataRevisao?: string | null;
  requisitoLegalId?: string | null;
  obraId?: string | null;
  setorId?: string | null;
  arquivo?: string | null;
}

export type AtualizarDocumentoGestaoPayload = NovoDocumentoGestao;

export interface DocumentoRevisao {
  id: string;
  numeroRevisao: number;
  dataRevisao: string;
  motivo: string;
  responsavelUsuarioId?: string | null;
  responsavelUsuarioNome?: string | null;
}

export interface DocumentoGestaoDetalhe {
  documento: DocumentoGestao;
  historico: DocumentoRevisao[];
}

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
  HigienizacaoVencendo: 16,
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
// pelos IAlertaOrigemProvider do Motor Central de Alertas (Aso, Treinamento, ItemHigienizacao,
// AtivoSst) quanto por alertas criados manualmente, cujo EntidadeOrigemTipo é texto livre (ver
// CriarAlertaCommand); por isso o fallback retorna o próprio valor quando não reconhecido.
export const categoriaAlertaLabel: Record<string, string> = {
  Aso: 'ASO',
  Treinamento: 'Treinamentos',
  ItemHigienizacao: 'Higienização',
  AtivoSst: 'Ativos (Extintores/Equipamentos)',
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

// Motor Central de Alertas (requisito do usuário, 2026-08-25): tela de administração que permite
// ajustar RegraAlerta.DiasAntecedencia/Severidade por módulo, hoje só editável direto no banco. O
// AlertaEngineService escolhe a regra mais urgente cujo DiasAntecedencia cobre os dias restantes —
// ver AAHBRANT.SST.Application/Alertas/Motor/AlertaEngineService.cs.
export const TipoModuloAlerta = {
  Aso: 1,
  Treinamento: 2,
  Higienizacao: 3,
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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const authHeaders = await montarHeadersAuth();
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
    criar: (obra: NovaObra) => request<{ id: string }>('/api/obras', { method: 'POST', body: JSON.stringify(obra) }),
    excluir: (id: string) => request<void>(`/api/obras/${id}`, { method: 'DELETE' }),
  },
  trabalhadores: {
    listar: (obraId?: string) =>
      request<Trabalhador[]>(`/api/trabalhadores${obraId ? `?obraId=${obraId}` : ''}`),
    criar: (trabalhador: NovoTrabalhador) =>
      request<{ id: string }>('/api/trabalhadores', { method: 'POST', body: JSON.stringify(trabalhador) }),
    excluir: (id: string) => request<void>(`/api/trabalhadores/${id}`, { method: 'DELETE' }),
    gerarVinculoTelegram: (id: string) =>
      request<GerarVinculoTelegramResultado>(`/api/trabalhadores/${id}/telegram/vinculo`, { method: 'POST' }),
    definirPinAssinatura: (id: string, pin: string, confirmarPin: string) =>
      request<void>(`/api/trabalhadores/${id}/assinatura/pin`, {
        method: 'POST',
        body: JSON.stringify({ trabalhadorId: id, pin, confirmarPin }),
      }),
    // Cadastro de credencial WebAuthn/FIDO2 (etapa 13) — cerimônia em duas chamadas; a conversão
    // JSON<->ArrayBuffer com o navegador fica em lib/webauthn.ts (criarCredencialWebAuthn).
    iniciarCadastroWebAuthn: (id: string, tipo: number) =>
      request<string>(`/api/trabalhadores/${id}/assinatura/webauthn/cadastro/iniciar?tipo=${tipo}`, {
        method: 'POST',
      }),
    confirmarCadastroWebAuthn: (id: string, tipo: number, opcoesJson: string, respostaJson: string) =>
      request<void>(`/api/trabalhadores/${id}/assinatura/webauthn/cadastro/confirmar`, {
        method: 'POST',
        body: JSON.stringify({ tipo, opcoesJson, respostaJson }),
      }),
  },
  funcoes: {
    listar: () => request<Funcao[]>('/api/funcoes'),
    criar: (funcao: NovaFuncao) =>
      request<{ id: string }>('/api/funcoes', { method: 'POST', body: JSON.stringify(funcao) }),
    excluir: (id: string) => request<void>(`/api/funcoes/${id}`, { method: 'DELETE' }),
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
    criar: (aso: NovoAso) => request<{ id: string }>('/api/asos', { method: 'POST', body: JSON.stringify(aso) }),
    excluir: (id: string) => request<void>(`/api/asos/${id}`, { method: 'DELETE' }),
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
  catalogosEpi: {
    listar: () => request<CatalogoEpi[]>('/api/catalogosepi'),
    criar: (epi: NovoCatalogoEpi) =>
      request<{ id: string }>('/api/catalogosepi', { method: 'POST', body: JSON.stringify(epi) }),
    excluir: (id: string) => request<void>(`/api/catalogosepi/${id}`, { method: 'DELETE' }),
  },
  entregasEpi: {
    listar: (trabalhadorId?: string) =>
      request<EntregaEpi[]>(`/api/entregasepi${trabalhadorId ? `?trabalhadorId=${trabalhadorId}` : ''}`),
    criar: (entrega: NovaEntregaEpi) =>
      request<{ id: string }>('/api/entregasepi', { method: 'POST', body: JSON.stringify(entrega) }),
    excluir: (id: string) => request<void>(`/api/entregasepi/${id}`, { method: 'DELETE' }),
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
  },
  aprEtapas: {
    listar: (aprId: string) => request<AprEtapa[]>(`/api/aprEtapas?aprId=${aprId}`),
    criar: (etapa: NovaAprEtapa) =>
      request<{ id: string }>('/api/aprEtapas', { method: 'POST', body: JSON.stringify(etapa) }),
    excluir: (id: string) => request<void>(`/api/aprEtapas/${id}`, { method: 'DELETE' }),
  },
  aprAssinaturas: {
    listar: (aprId: string) => request<AprAssinatura[]>(`/api/aprAssinaturas?aprId=${aprId}`),
    criar: (assinatura: NovaAprAssinatura) =>
      request<{ id: string }>('/api/aprAssinaturas', { method: 'POST', body: JSON.stringify(assinatura) }),
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
    autorizar: (id: string, autorizadoPorUsuarioId: string) =>
      request<void>(`/api/permissoesTrabalho/${id}/autorizar`, {
        method: 'POST',
        body: JSON.stringify({ autorizadoPorUsuarioId }),
      }),
    encerrar: (id: string, encerradaPorUsuarioId: string, observacoes?: string | null) =>
      request<void>(`/api/permissoesTrabalho/${id}/encerrar`, {
        method: 'POST',
        body: JSON.stringify({ encerradaPorUsuarioId, observacoes }),
      }),
  },
  permissaoTrabalhoControles: {
    listar: (permissaoTrabalhoId: string) =>
      request<PermissaoTrabalhoControle[]>(`/api/permissaoTrabalhoControles?permissaoTrabalhoId=${permissaoTrabalhoId}`),
    criar: (controle: NovaPermissaoTrabalhoControle) =>
      request<{ id: string }>('/api/permissaoTrabalhoControles', { method: 'POST', body: JSON.stringify(controle) }),
    excluir: (id: string) => request<void>(`/api/permissaoTrabalhoControles/${id}`, { method: 'DELETE' }),
  },
  permissaoTrabalhoRequisitos: {
    listar: (permissaoTrabalhoId: string) =>
      request<PermissaoTrabalhoRequisito[]>(
        `/api/permissaoTrabalhoRequisitos?permissaoTrabalhoId=${permissaoTrabalhoId}`,
      ),
    criar: (requisito: NovaPermissaoTrabalhoRequisito) =>
      request<{ id: string }>('/api/permissaoTrabalhoRequisitos', { method: 'POST', body: JSON.stringify(requisito) }),
    marcar: (id: string, atendido: boolean) =>
      request<void>(`/api/permissaoTrabalhoRequisitos/${id}/marcar`, {
        method: 'POST',
        body: JSON.stringify({ atendido }),
      }),
    excluir: (id: string) => request<void>(`/api/permissaoTrabalhoRequisitos/${id}`, { method: 'DELETE' }),
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
    ) =>
      request<void>(`/api/inspecoes/respostas/${respostaId}`, {
        method: 'POST',
        body: JSON.stringify({ statusItem, observacao, responsavelUsuarioId, prazo }),
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
    encerrar: (id: string) => request<void>(`/api/inspecoes/${id}/encerrar`, { method: 'POST' }),
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
      const response = await fetch(`${API_BASE_URL}/api/dds/${ddsId}/participantes`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return (await response.json()) as { id: string };
    },
    encerrar: (id: string) => request<void>(`/api/dds/${id}/encerrar`, { method: 'POST' }),
    baixarPdf: async (id: string) => {
      const response = await fetch(`${API_BASE_URL}/api/dds/${id}/pdf`, { headers: await montarHeadersAuth() });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
    },
    baixarFotoParticipante: async (participanteId: string) => {
      const response = await fetch(`${API_BASE_URL}/api/dds/participantes/${participanteId}/foto`, {
        headers: await montarHeadersAuth(),
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return response.blob();
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
    assinar: (documentoId: string, uid: string, pin: string) =>
      request<DocumentoSignatario>(`/api/documentos/${documentoId}/assinar`, {
        method: 'POST',
        body: JSON.stringify({ uid, pin }),
      }),
    // Assinatura biométrica WebAuthn/FIDO2 (etapa 13) — cerimônia em duas chamadas; ver
    // lib/webauthn.ts (obterAssercaoWebAuthn) para a conversão JSON<->ArrayBuffer com o navegador.
    iniciarAssinaturaWebAuthn: (trabalhadorId?: string) =>
      request<string>(`/api/documentos/assinar/webauthn/iniciar${trabalhadorId ? `?trabalhadorId=${trabalhadorId}` : ''}`, {
        method: 'POST',
      }),
    confirmarAssinaturaWebAuthn: (documentoId: string, opcoesJson: string, respostaJson: string) =>
      request<DocumentoSignatario>(`/api/documentos/${documentoId}/assinar/webauthn/confirmar`, {
        method: 'POST',
        body: JSON.stringify({ opcoesJson, respostaJson }),
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
  higienizacao: {
    listar: (obraId?: string) =>
      request<ItemHigienizacao[]>(`/api/higienizacao${obraId ? `?obraId=${obraId}` : ''}`),
    obterDetalhe: (id: string) => request<ItemHigienizacaoDetalhe>(`/api/higienizacao/${id}`),
    criar: (item: NovoItemHigienizacao) =>
      request<{ id: string }>('/api/higienizacao', { method: 'POST', body: JSON.stringify(item) }),
    registrarHigienizacao: async (
      itemId: string,
      trabalhadorId: string,
      observacoes: string | null,
      foto: File,
    ) => {
      const formData = new FormData();
      formData.append('trabalhadorId', trabalhadorId);
      if (observacoes) formData.append('observacoes', observacoes);
      formData.append('foto', foto);
      const response = await fetch(`${API_BASE_URL}/api/higienizacao/${itemId}/registros`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
      }
      return (await response.json()) as { id: string };
    },
    baixarFotoRegistro: async (registroId: string) => {
      const response = await fetch(`${API_BASE_URL}/api/higienizacao/registros/${registroId}/foto`, {
        headers: await montarHeadersAuth(),
      });
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
    avancarStatus: (id: string) =>
      request<void>(`/api/naoconformidades/${id}/avancar-status`, { method: 'POST' }),
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
  matrizLegal: {
    listar: (filtros?: { norma?: string; tema?: string; aplicabilidade?: boolean; status?: number; obraId?: string }) => {
      const params = new URLSearchParams();
      if (filtros?.norma) params.set('norma', filtros.norma);
      if (filtros?.tema) params.set('tema', filtros.tema);
      if (filtros?.aplicabilidade !== undefined) params.set('aplicabilidade', String(filtros.aplicabilidade));
      if (filtros?.status) params.set('status', String(filtros.status));
      if (filtros?.obraId) params.set('obraId', filtros.obraId);
      const query = params.toString();
      return request<RequisitoLegal[]>(`/api/matrizlegal${query ? `?${query}` : ''}`);
    },
    obterDetalhe: (id: string) => request<RequisitoLegalDetalhe>(`/api/matrizlegal/${id}`),
    criar: (requisito: NovoRequisitoLegal) =>
      request<{ id: string }>('/api/matrizlegal', { method: 'POST', body: JSON.stringify(requisito) }),
    atualizar: (id: string, requisito: AtualizarRequisitoLegalPayload) =>
      request<void>(`/api/matrizlegal/${id}`, { method: 'PUT', body: JSON.stringify(requisito) }),
    excluir: (id: string) => request<void>(`/api/matrizlegal/${id}`, { method: 'DELETE' }),
    atualizarStatus: (id: string, novoStatus: number) =>
      request<void>(`/api/matrizlegal/${id}/status`, { method: 'POST', body: JSON.stringify({ novoStatus }) }),
  },
  gestaoDocumental: {
    listar: (filtros?: {
      nome?: string;
      tipo?: string;
      categoria?: string;
      status?: number;
      obraId?: string;
      setorId?: string;
    }) => {
      const params = new URLSearchParams();
      if (filtros?.nome) params.set('nome', filtros.nome);
      if (filtros?.tipo) params.set('tipo', filtros.tipo);
      if (filtros?.categoria) params.set('categoria', filtros.categoria);
      if (filtros?.status) params.set('status', String(filtros.status));
      if (filtros?.obraId) params.set('obraId', filtros.obraId);
      if (filtros?.setorId) params.set('setorId', filtros.setorId);
      const query = params.toString();
      return request<DocumentoGestao[]>(`/api/gestaodocumental${query ? `?${query}` : ''}`);
    },
    obterDetalhe: (id: string) => request<DocumentoGestaoDetalhe>(`/api/gestaodocumental/${id}`),
    criar: (documento: NovoDocumentoGestao) =>
      request<{ id: string }>('/api/gestaodocumental', { method: 'POST', body: JSON.stringify(documento) }),
    atualizar: (id: string, documento: AtualizarDocumentoGestaoPayload) =>
      request<void>(`/api/gestaodocumental/${id}`, { method: 'PUT', body: JSON.stringify(documento) }),
    excluir: (id: string) => request<void>(`/api/gestaodocumental/${id}`, { method: 'DELETE' }),
    atualizarStatus: (id: string, novoStatus: number) =>
      request<void>(`/api/gestaodocumental/${id}/status`, { method: 'POST', body: JSON.stringify({ novoStatus }) }),
    criarRevisao: (id: string, motivo: string, responsavelUsuarioId?: string | null, novaVersao?: string | null) =>
      request<{ id: string }>(`/api/gestaodocumental/${id}/revisoes`, {
        method: 'POST',
        body: JSON.stringify({ motivo, responsavelUsuarioId, novaVersao }),
      }),
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
};
