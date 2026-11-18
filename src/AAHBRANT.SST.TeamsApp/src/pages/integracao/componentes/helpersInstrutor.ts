// Instrutor com biometria, carga automática, pendências clicáveis e fila única (16/11/2026).
// Regras puras da tela "Realizar integração": sem React e sem valores de lib/api (só tipos), para rodar em
// `node --test tests-unit/`. O relógio entra como parâmetro (`agoraMs`): os testes fixam a hora e a tela
// recalcula a cada tick. A trava real é do servidor (409); aqui a tela só explica o que falta.
import type { EtapaIntegracao, PendenciaEtapaIntegracao } from '../../../lib/api';
import { faltasDaEtapa, participantesAtivos } from './helpers.ts';

/** 'biometria' (nome do servidor) é a DIGITAL; 'reconhecimentoFacial' é a facial. */
export function rotuloMetodo(metodo?: string | null): 'digital' | 'facial' | null {
  return metodo === 'reconhecimentoFacial' ? 'facial' : metodo === 'biometria' ? 'digital' : null;
}

export const CODIGOS_PENDENCIA = {
  semParticipantes: 'sem_participantes',
  instrutorNaoDefinido: 'instrutor_nao_definido',
  carga: 'carga',
  cargaAguardandoFim: 'carga_aguardando_fim',
  fotos: 'fotos',
  assinaturasColaboradores: 'assinaturas_colaboradores',
  instrutorAssinatura: 'instrutor_assinatura',
  instrutorSemBiometria: 'instrutor_sem_biometria',
} as const;

export type AlvoPendencia = 'instrutor' | 'carga' | 'fotos' | 'fila';

/** Para onde a linha "Falta" leva: a seção da tela que resolve a pendência. null = sem ação na tela (ex.: etapa sem participantes). */
export function alvoDaPendencia(codigo: string): AlvoPendencia | null {
  switch (codigo) {
    case CODIGOS_PENDENCIA.instrutorNaoDefinido:
      return 'instrutor';
    case CODIGOS_PENDENCIA.carga:
    case CODIGOS_PENDENCIA.cargaAguardandoFim:
      return 'carga';
    case CODIGOS_PENDENCIA.fotos:
      return 'fotos';
    case CODIGOS_PENDENCIA.assinaturasColaboradores:
    case CODIGOS_PENDENCIA.instrutorAssinatura:
    case CODIGOS_PENDENCIA.instrutorSemBiometria:
      return 'fila';
    default:
      return null;
  }
}

/** Nome da seção para o leitor de tela ("Ir para a fila de assinaturas"). */
export const ROTULO_ALVO: Record<AlvoPendencia, string> = {
  instrutor: 'o instrutor',
  carga: 'a carga horária',
  fotos: 'as fotos',
  fila: 'a fila de assinaturas',
};

// ---- carga horária ----

const FUSO_BRASILIA = 'America/Sao_Paulo';

/** Instante (epoch ms) de uma data (yyyy-MM-dd) e hora (HH:mm) de Brasília. O Brasil não tem horário de verão desde 2019 (UTC-3 fixo). */
export function instanteBrasilia(data: string, hhmm: string): number {
  return Date.parse(`${data.slice(0, 10)}T${hhmm}:00-03:00`);
}

type PeriodoCarga = { data: string; inicio: string; fim: string; minutos: number; automatico?: boolean };

export function inicioDoPeriodoMs(p: Pick<PeriodoCarga, 'data' | 'inicio'>): number {
  return instanteBrasilia(p.data, p.inicio);
}

/** Fim do período. O automático que cruza a meia-noite termina às 23:59 mas conta até 00:00 (o `minutos` é a duração real). */
export function fimDoPeriodoMs(p: Pick<PeriodoCarga, 'data' | 'fim' | 'automatico'>): number {
  const fim = instanteBrasilia(p.data, p.fim);
  return p.automatico && p.fim === '23:59' ? fim + 60_000 : fim;
}

/** Fim do último período lançado pelo sistema, ou null se não há período automático. */
export function fimCargaAutomaticaMs(periodos: PeriodoCarga[] | undefined | null): number | null {
  const fins = (periodos ?? []).filter((p) => p.automatico).map(fimDoPeriodoMs).filter((v) => Number.isFinite(v));
  return fins.length ? Math.max(...fins) : null;
}

export function formatarHoraBrasil(ms: number): string {
  return new Date(ms).toLocaleTimeString('pt-BR', { timeZone: FUSO_BRASILIA, hour: '2-digit', minute: '2-digit' });
}

function diaBrasil(ms: number): string {
  return new Date(ms).toLocaleDateString('pt-BR', { timeZone: FUSO_BRASILIA, day: '2-digit', month: '2-digit' });
}

/** "2 h 48 min" ou "35 min" (arredonda para cima: nunca promete menos do que falta). */
export function formatarFalta(ms: number): string {
  const min = Math.max(0, Math.ceil(ms / 60_000));
  const h = Math.floor(min / 60);
  const m = min % 60;
  return h > 0 ? `${h} h ${String(m).padStart(2, '0')} min` : `${m} min`;
}

/** "13:00" ou, se o fim cai em outro dia, "00:30 de 17/11". */
export function textoFechaAs(fimMs: number, agoraMs: number): string {
  const hora = formatarHoraBrasil(fimMs);
  return diaBrasil(fimMs) === diaBrasil(agoraMs) ? hora : `${hora} de ${diaBrasil(fimMs)}`;
}

/** Mesma frase da pendência do servidor, recalculada a cada tick: "Carga horária fecha às 13:00 — faltam 2 h 48 min". */
export function textoFechamentoCarga(fimMs: number, agoraMs: number): string {
  return `Carga horária fecha às ${textoFechaAs(fimMs, agoraMs)} — faltam ${formatarFalta(fimMs - agoraMs)}`;
}

/** Minutos de carga já decorridos: o período automático só conta o que o relógio já andou; o manual conta inteiro. */
export function cargaDecorridaMin(periodos: PeriodoCarga[] | undefined | null, agoraMs: number): number {
  let total = 0;
  for (const p of periodos ?? []) {
    if (!p.automatico) {
      total += p.minutos;
      continue;
    }
    const duracaoMs = p.minutos * 60_000;
    total += Math.max(0, Math.min(agoraMs - inicioDoPeriodoMs(p), duracaoMs)) / 60_000;
  }
  return total;
}

export type EstadoCarga = 'sem_periodo' | 'aguardando_fim' | 'abaixo_minimo' | 'cumprida';

/** sem_periodo (nada lançado: técnico lança à mão), aguardando_fim (período automático ainda correndo), abaixo_minimo ou cumprida. */
export function estadoCarga(
  etapa: Pick<EtapaIntegracao, 'periodos' | 'cargaLancadaMinutos' | 'cargaMinimaHoras'>,
  agoraMs: number,
): EstadoCarga {
  const periodos = etapa.periodos ?? [];
  if (periodos.length === 0) return 'sem_periodo';
  const fim = fimCargaAutomaticaMs(periodos);
  if (fim != null && agoraMs < fim) return 'aguardando_fim';
  const minimo = Math.round(etapa.cargaMinimaHoras * 60);
  return minimo > 0 && etapa.cargaLancadaMinutos < minimo ? 'abaixo_minimo' : 'cumprida';
}

/** Percentual da barra: o que já passou do período automático (ou o lançado, se manual) sobre o mínimo (ou sobre o lançado, sem mínimo). */
export function percentualCarga(
  etapa: Pick<EtapaIntegracao, 'periodos' | 'cargaLancadaMinutos' | 'cargaMinimaHoras'>,
  agoraMs: number,
): number {
  const alvo = Math.round(etapa.cargaMinimaHoras * 60) || etapa.cargaLancadaMinutos;
  if (alvo <= 0) return 0;
  return Math.max(0, Math.min(100, Math.round((cargaDecorridaMin(etapa.periodos, agoraMs) / alvo) * 100)));
}

// ---- pendências e bloqueio do Avançar ----

/**
 * Pendências que travam o Avançar, na ordem do servidor. Com `pendencias` (servidor novo) vale a lista dele;
 * a espera do período automático é recalculada pelo relógio e some quando o fim chega (até a tela buscar de novo).
 * Servidor antigo (sem `pendencias`): cai nas frases locais de faltasDaEtapa, com código 'outra'.
 */
export function pendenciasDaEtapa(etapa: EtapaIntegracao, instrutorTrabalhadorId: string | null | undefined, agoraMs: number): PendenciaEtapaIntegracao[] {
  if (etapa.status === 'pulada' || participantesAtivos(etapa).length === 0) return [];
  const fim = fimCargaAutomaticaMs(etapa.periodos);
  const base: PendenciaEtapaIntegracao[] = etapa.pendencias ?? faltasDaEtapa(etapa, instrutorTrabalhadorId).map((texto) => ({ codigo: 'outra', texto }));
  const resultado: PendenciaEtapaIntegracao[] = [];
  for (const p of base) {
    if (p.codigo === CODIGOS_PENDENCIA.cargaAguardandoFim && fim != null) {
      if (agoraMs < fim) resultado.push({ codigo: p.codigo, texto: textoFechamentoCarga(fim, agoraMs) });
      continue;
    }
    resultado.push(p);
  }
  return resultado;
}

export interface BloqueioAvancar {
  bloqueado: boolean;
  pendencias: PendenciaEtapaIntegracao[];
  /** Frase para o botão/barra: a espera da carga (com o tempo que falta) tem prioridade, senão a 1ª pendência. null = liberado. */
  motivo: string | null;
}

/** Regra do botão Avançar: desabilitado enquanto a etapa tiver QUALQUER pendência; mostra o motivo e, na carga, o tempo que falta. */
export function bloqueioAvancar(etapa: EtapaIntegracao, instrutorTrabalhadorId: string | null | undefined, agoraMs: number): BloqueioAvancar {
  const pendencias = pendenciasDaEtapa(etapa, instrutorTrabalhadorId, agoraMs);
  if (pendencias.length === 0) return { bloqueado: false, pendencias, motivo: null };
  const espera = pendencias.find((p) => p.codigo === CODIGOS_PENDENCIA.cargaAguardandoFim);
  return { bloqueado: true, pendencias, motivo: (espera ?? pendencias[0]).texto };
}

// ---- fila única de assinaturas ----

export interface LinhaFila {
  tipo: 'instrutor' | 'colaborador';
  trabalhadorId: string;
  nome: string;
  assinou: boolean;
  naoAssinou: boolean;
  motivoNaoAssinou?: string | null;
  metodo: 'digital' | 'facial' | null;
  em: string | null;
  temDigital: boolean;
  temFacial: boolean;
  /** Sem digital nem facial cadastrada e ainda não assinou: precisa cadastrar. */
  semBiometria: boolean;
}

/** Instrutor primeiro (se a integração tem instrutor), depois os participantes na ordem da etapa. */
export function montarFilaAssinaturas(
  etapa: Pick<EtapaIntegracao, 'participantes' | 'instrutorAssinou' | 'instrutorAssinadoEmUtc' | 'instrutorMetodo' | 'instrutorTemDigital' | 'instrutorTemFacial' | 'pendencias'>,
  instrutor: { trabalhadorId: string; nome: string } | null,
): LinhaFila[] {
  const linhas: LinhaFila[] = [];
  if (instrutor) {
    const assinou = !!etapa.instrutorAssinou;
    const semBioServidor = (etapa.pendencias ?? []).some((p) => p.codigo === CODIGOS_PENDENCIA.instrutorSemBiometria);
    const temDigital = etapa.instrutorTemDigital ?? true;
    const temFacial = etapa.instrutorTemFacial ?? true;
    linhas.push({
      tipo: 'instrutor',
      trabalhadorId: instrutor.trabalhadorId,
      nome: instrutor.nome,
      assinou,
      naoAssinou: false,
      metodo: assinou ? rotuloMetodo(etapa.instrutorMetodo) : null,
      em: assinou ? etapa.instrutorAssinadoEmUtc ?? null : null,
      temDigital,
      temFacial,
      semBiometria: !assinou && (semBioServidor || (!temDigital && !temFacial)),
    });
  }
  for (const p of etapa.participantes) {
    linhas.push({
      tipo: 'colaborador',
      trabalhadorId: p.trabalhadorId,
      nome: p.nome,
      assinou: p.presencaConfirmada,
      naoAssinou: !!p.naoAssinou,
      motivoNaoAssinou: p.motivoNaoAssinou,
      metodo: p.presencaConfirmada ? rotuloMetodo(p.metodo) : null,
      em: p.presencaConfirmada ? p.presencaEmUtc ?? null : null,
      temDigital: p.temDigital,
      temFacial: p.temFacial,
      semBiometria: !p.presencaConfirmada && !p.naoAssinou && !p.temDigital && !p.temFacial,
    });
  }
  return linhas;
}

/** "Fila de assinaturas (n/total)": quem foi marcado "Não assinou" sai da conta. */
export function resumoFila(linhas: LinhaFila[]): { feitos: number; total: number } {
  const contam = linhas.filter((l) => !l.naoAssinou);
  return { feitos: contam.filter((l) => l.assinou).length, total: contam.length };
}

export type VezDaFila = { tipo: 'instrutor'; linha: LinhaFila } | { tipo: 'colaborador'; linha: LinhaFila } | { tipo: 'todos' };

/**
 * De quem é a vez: do instrutor primeiro; se ele já assinou OU não tem biometria (a fila segue sem ele),
 * do primeiro colaborador que falta. Todos assinaram = 'todos'. Instrutor sem biometria e ninguém mais na
 * fila: a vez continua sendo dele, para a tela mostrar o aviso e o atalho de cadastro.
 */
export function vezDaFila(linhas: LinhaFila[]): VezDaFila {
  const instrutor = linhas.find((l) => l.tipo === 'instrutor');
  const pendentes = linhas.filter((l) => l.tipo === 'colaborador' && !l.assinou && !l.naoAssinou);
  if (instrutor && !instrutor.assinou && !instrutor.semBiometria) return { tipo: 'instrutor', linha: instrutor };
  if (pendentes.length > 0) return { tipo: 'colaborador', linha: pendentes[0] };
  if (instrutor && !instrutor.assinou) return { tipo: 'instrutor', linha: instrutor };
  return { tipo: 'todos' };
}

// ---- pré-checagem ao iniciar ----

export type SituacaoBiometriaInstrutor = 'com_biometria' | 'sem_biometria' | 'desconhecida';

/**
 * Pré-checagem na hora de escolher o instrutor (decisão 2, 16/11): só a biometria dele; ASO não entra aqui (só
 * barra na OS). 'desconhecida' = servidor antigo (sem os campos) ou nenhum instrutor: a tela não bloqueia, o
 * servidor continua recusando o "Iniciar" (400 com o nome).
 */
export function situacaoBiometriaInstrutor(instrutor: { temDigital?: boolean; temFacial?: boolean } | null | undefined): SituacaoBiometriaInstrutor {
  if (!instrutor) return 'desconhecida';
  if (instrutor.temDigital === true || instrutor.temFacial === true) return 'com_biometria';
  if (instrutor.temDigital === false && instrutor.temFacial === false) return 'sem_biometria';
  return 'desconhecida';
}
