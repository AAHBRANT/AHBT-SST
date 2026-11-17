// Agenda das fotos automáticas da etapa (16/11/2026), pura: sem React, sem relógio global, sem API.
// Tudo por horário ABSOLUTO (epoch em ms): contar ticks de setInterval não serve, porque aba em segundo
// plano e tablet que dorme atrasam ou param os timers. A tela só pergunta "que horas são e o que é
// devido?" a cada tick e ao voltar a ficar visível. Testado em tests-unit/instrutorBiometrico.test.ts.
import type { FotosAutomaticasIntegracao } from '../../../lib/api';

export const TOTAL_FOTOS_AUTOMATICAS = 3;
/** Padrão do servidor (Integracao:FotosAutomaticas): 1ª aos 60 s, 2ª +5 min, 3ª +5 min. */
export const FOTOS_AUTOMATICAS_PADRAO: FotosAutomaticasIntegracao = { primeiraAposSegundos: 60, segundaAposSegundos: 300, terceiraAposSegundos: 300 };
/** Passou disso do horário devido (tela dormiu, aba em segundo plano): em vez de disparar às cegas, a tela avisa e oferece retomar. */
export const TOLERANCIA_ATRASO_MS = 30_000;

export interface EstadoAgendaFotos {
  /** Quando o técnico tocou em "Fotografar automaticamente" (ou retomou). */
  ativadoEm: number;
  /** Horário real de cada foto já registrada nesta etapa, na ordem (automáticas ou manuais). */
  feitasEm: number[];
  /** Tempo parado em "Pausar", somado ao prazo da foto que está esperando. Zera a cada foto. */
  pausaMs?: number;
}

export interface FotoDevida {
  /** 0 = 1ª foto, 1 = 2ª, 2 = 3ª. */
  indice: number;
  devidaEm: number;
}

function esperaDaFoto(indice: number, cfg: FotosAutomaticasIntegracao): number {
  const s = indice === 0 ? cfg.primeiraAposSegundos : indice === 1 ? cfg.segundaAposSegundos : cfg.terceiraAposSegundos;
  return Math.max(0, s) * 1000;
}

/** A próxima foto e o horário em que ela vence: a 1ª conta da ativação; as seguintes, da foto anterior. null = as 3 já saíram. */
export function proximaFotoDevida(estado: EstadoAgendaFotos, cfg: FotosAutomaticasIntegracao, total = TOTAL_FOTOS_AUTOMATICAS): FotoDevida | null {
  const indice = estado.feitasEm.length;
  if (indice >= total) return null;
  const base = indice === 0 ? estado.ativadoEm : (estado.feitasEm[indice - 1] ?? estado.ativadoEm);
  return { indice, devidaEm: base + esperaDaFoto(indice, cfg) + (estado.pausaMs ?? 0) };
}

/**
 * Agenda ao tocar em "Fotografar automaticamente". Fotos que a etapa já tem contam como feitas NA ATIVAÇÃO, então a
 * próxima do cronograma vence `espera do passo` depois da ativação: 0 fotos = 1ª em 60 s; 1 foto = 2ª em 300 s;
 * 2 fotos = 3ª em 300 s (regra aprovada: o intervalo é o do passo que falta, não o da foto anterior).
 */
export function agendaAoAtivar(fotosJaFeitas: number, ativadoEm: number, total = TOTAL_FOTOS_AUTOMATICAS): EstadoAgendaFotos {
  return { ativadoEm, feitasEm: Array(Math.max(0, Math.min(fotosJaFeitas, total))).fill(ativadoEm), pausaMs: 0 };
}

export type FaseAgenda = 'aguardando' | 'devida' | 'atrasada' | 'concluida';

export interface SituacaoAgenda {
  fase: FaseAgenda;
  indice: number;
  /** Segundos que faltam (arredondados para cima); 0 quando já venceu. */
  restanteSeg: number;
  /** Quanto passou do horário devido, em ms (0 enquanto não venceu). */
  atrasoMs: number;
}

/** Aguardando (contagem regressiva), devida (pode disparar agora), atrasada (passou da tolerância: perguntar) ou concluída. */
export function situacaoAgenda(estado: EstadoAgendaFotos, cfg: FotosAutomaticasIntegracao, agora: number, total = TOTAL_FOTOS_AUTOMATICAS): SituacaoAgenda {
  const devida = proximaFotoDevida(estado, cfg, total);
  if (!devida) return { fase: 'concluida', indice: total, restanteSeg: 0, atrasoMs: 0 };
  const falta = devida.devidaEm - agora;
  if (falta > 0) return { fase: 'aguardando', indice: devida.indice, restanteSeg: Math.ceil(falta / 1000), atrasoMs: 0 };
  const atrasoMs = -falta;
  return { fase: atrasoMs > TOLERANCIA_ATRASO_MS ? 'atrasada' : 'devida', indice: devida.indice, restanteSeg: 0, atrasoMs };
}

/** "00:42", "04:12", "61:05": contagem regressiva em mm:ss. */
export function formatarContagem(segundos: number): string {
  const s = Math.max(0, Math.round(segundos));
  return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
}

/** "1ª", "2ª" ou "3ª" (índice base 0). */
export function ordinalFoto(indice: number): string {
  return `${indice + 1}ª`;
}

/** "3 min" ou "45 s" para dizer o quanto a foto atrasou. */
export function textoAtraso(ms: number): string {
  const seg = Math.max(0, Math.round(ms / 1000));
  return seg >= 90 ? `${Math.round(seg / 60)} min` : `${seg} s`;
}

/** Um valor ruim vindo da configuração (0, negativo, texto) volta ao padrão: nunca dispara foto em rajada. */
export function configuracaoFotosSegura(cfg?: Partial<FotosAutomaticasIntegracao> | null): FotosAutomaticasIntegracao {
  const ok = (v: unknown, padrao: number) => (typeof v === 'number' && Number.isFinite(v) && v >= 5 && v <= 3600 ? v : padrao);
  return {
    primeiraAposSegundos: ok(cfg?.primeiraAposSegundos, FOTOS_AUTOMATICAS_PADRAO.primeiraAposSegundos),
    segundaAposSegundos: ok(cfg?.segundaAposSegundos, FOTOS_AUTOMATICAS_PADRAO.segundaAposSegundos),
    terceiraAposSegundos: ok(cfg?.terceiraAposSegundos, FOTOS_AUTOMATICAS_PADRAO.terceiraAposSegundos),
  };
}

/** "1ª após 60 s · 2ª +5 min · 3ª +5 min" (texto de apoio antes de ativar). */
export function resumoIntervalos(cfg: FotosAutomaticasIntegracao): string {
  const dur = (s: number) => (s % 60 === 0 && s >= 120 ? `${s / 60} min` : `${s} s`);
  return `1ª após ${dur(cfg.primeiraAposSegundos)} · 2ª +${dur(cfg.segundaAposSegundos)} · 3ª +${dur(cfg.terceiraAposSegundos)}`;
}

/**
 * Novo `pausaMs` para a foto pendente vencer em `alvoMs` (retomar depois de a tela dormir: "daqui a 5 s";
 * tentar de novo depois de uma falha: "daqui a 15 s"). Mantém o resto da agenda como está.
 */
export function pausaParaVencerEm(estado: EstadoAgendaFotos, cfg: FotosAutomaticasIntegracao, alvoMs: number): number {
  const semPausa = proximaFotoDevida({ ...estado, pausaMs: 0 }, cfg);
  return semPausa ? alvoMs - semPausa.devidaEm : 0;
}

export type FalhaCamera = 'negada' | 'indisponivel';

/** NotAllowedError/SecurityError = o navegador ou o Teams bloqueou; o resto (ocupada, sem câmera, restrição) = indisponível. */
export function classificarFalhaCamera(nomeDoErro?: string | null): FalhaCamera {
  return nomeDoErro === 'NotAllowedError' || nomeDoErro === 'SecurityError' || nomeDoErro === 'PermissionDeniedError' ? 'negada' : 'indisponivel';
}

/** Próximo número de quadro livre (1 a 3) dado o que já está gravado. null = todos ocupados. */
export function proximaOrdemLivre(ordensOcupadas: readonly number[], total = TOTAL_FOTOS_AUTOMATICAS): number | null {
  for (let o = 1; o <= total; o++) if (!ordensOcupadas.includes(o)) return o;
  return null;
}
