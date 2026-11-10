import type { Tom } from '@ui';
import type { CameraResumo } from '../../lib/api';

const DOIS_MINUTOS_MS = 2 * 60 * 1000;

// O backend devolve DateTime (Kind Unspecified do EF) sem sufixo Z; sem ele o navegador lê como horário
// local e a hora sai errada. Qualquer texto de data/hora sem fuso é tratado como UTC.
const TEM_FUSO = /(Z|[+-]\d{2}:?\d{2})$/i;

export function paraData(data: Date | string): Date {
  if (data instanceof Date) return data;
  return new Date(TEM_FUSO.test(data) || !data.includes('T') ? data : `${data}Z`);
}

export function descreverIrregularidade(semCapacete: number, semColete: number): string {
  if (semCapacete > 0 && semColete > 0) return 'Sem capacete e sem colete';
  if (semCapacete > 0) return 'Sem capacete';
  if (semColete > 0) return 'Sem colete';
  return 'Cena regular';
}

export function resumoCena(pessoas: number, semCapacete: number, semColete: number): string {
  const partes = [`${pessoas} ${pessoas === 1 ? 'pessoa' : 'pessoas'} na cena`];
  if (semCapacete > 0) partes.push(`${semCapacete} sem capacete`);
  if (semColete > 0) partes.push(`${semColete} sem colete`);
  return partes.join(', ');
}

export function estadoDaCamera(camera: CameraResumo, agora = Date.now()): { rotulo: string; tom: Tom } {
  if (!camera.habilitada) return { rotulo: 'Desligada', tom: 'neutro' };
  const ultima = camera.ultimaAnaliseEmUtc ? paraData(camera.ultimaAnaliseEmUtc).getTime() : 0;
  if (ultima && agora - ultima < DOIS_MINUTOS_MS) return { rotulo: 'Monitorando', tom: 'ok' };
  return { rotulo: 'Sem análise recente', tom: 'atencao' };
}

const FUSO_BRASILIA = 'America/Sao_Paulo';

export function formatarHora(data: Date | string): string {
  return paraData(data).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', timeZone: FUSO_BRASILIA });
}

export function formatarDataHora(data: Date | string): string {
  return paraData(data).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: FUSO_BRASILIA,
  });
}
