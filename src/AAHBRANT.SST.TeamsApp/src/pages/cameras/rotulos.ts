import type { Tom } from '@ui';
import { FonteCamera, type AgenteCamera, type CameraResumo } from '../../lib/api';

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

export const ehCameraIp = (camera: Pick<CameraResumo, 'fonte'>): boolean => camera.fonte === FonteCamera.Rtsp;

export type ChaveSituacaoIp = 'recebendo' | 'aguardando' | 'semAgente' | 'desligada';

// Situação mostrada para câmera IP (o servidor calcula situacaoConexao a partir do contato do agente e do último quadro).
// Câmera ligada sem situação calculada ainda conta como "Aguardando sinal".
export function situacaoDaCameraIp(camera: CameraResumo): { chave: ChaveSituacaoIp; rotulo: string; tom: Tom; apoio: string | null } {
  if (!camera.habilitada) return { chave: 'desligada', rotulo: 'Desligada', tom: 'neutro', apoio: null };
  switch (camera.situacaoConexao) {
    case 'Recebendo imagem':
      return { chave: 'recebendo', rotulo: 'Recebendo imagem', tom: 'ok', apoio: null };
    case 'Agente sem contato':
      return {
        chave: 'semAgente',
        rotulo: 'Agente sem contato',
        tom: 'atencao',
        apoio: 'Veja se o PC da obra está ligado e com internet.',
      };
    default:
      return { chave: 'aguardando', rotulo: 'Aguardando sinal', tom: 'info', apoio: 'O agente está tentando falar com a câmera.' };
  }
}

// Câmera IP usa a situação da conexão; câmera de tablet ou navegador segue como sempre foi (Monitorando / Sem análise recente).
export function estadoExibido(camera: CameraResumo): { rotulo: string; tom: Tom } {
  return ehCameraIp(camera) ? situacaoDaCameraIp(camera) : estadoDaCamera(camera);
}

const TRES_MINUTOS_MS = 3 * 60 * 1000;

// Mesma regra do servidor (sem contato há mais de 3 min = agente fora do ar).
export function situacaoDoAgente(agente: AgenteCamera, agora = Date.now()): { rotulo: string; tom: Tom } {
  if (!agente.ativo) return { rotulo: 'Acesso revogado', tom: 'neutro' };
  if (!agente.ultimoContatoEmUtc) return { rotulo: 'Aguardando conexão', tom: 'info' };
  const idade = agora - paraData(agente.ultimoContatoEmUtc).getTime();
  return idade <= TRES_MINUTOS_MS ? { rotulo: 'Conectado', tom: 'ok' } : { rotulo: 'Sem contato', tom: 'atencao' };
}

const FUSO_BRASILIA = 'America/Sao_Paulo';

export function formatarHora(data: Date | string): string {
  return paraData(data).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', timeZone: FUSO_BRASILIA });
}

// "14:32:07" (horário de Brasília).
export function formatarHoraComSegundos(data: Date | string): string {
  return paraData(data).toLocaleTimeString('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    timeZone: FUSO_BRASILIA,
  });
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

const plural = (n: number, singular: string, pluralTexto: string) => `${n} ${n === 1 ? singular : pluralTexto}`;

// "Há 20 segundos", "Há 44 minutos", "Há 3 horas"; acima de um dia cai para a data.
export function descreverHa(data: Date | string, agora = Date.now()): string {
  const segundos = Math.max(0, Math.round((agora - paraData(data).getTime()) / 1000));
  if (segundos < 60) return `Há ${plural(segundos, 'segundo', 'segundos')}`;
  const minutos = Math.round(segundos / 60);
  if (minutos < 60) return `Há ${plural(minutos, 'minuto', 'minutos')}`;
  const horas = Math.round(minutos / 60);
  if (horas < 24) return `Há ${plural(horas, 'hora', 'horas')}`;
  return `Em ${formatarDataHora(data)}`;
}

function diaEmBrasilia(data: Date): string {
  return data.toLocaleDateString('pt-BR', { timeZone: FUSO_BRASILIA });
}

// "Hoje, 14:32" ou "05/11, 17:10".
export function formatarDiaHora(data: Date | string, agora = new Date()): string {
  const d = paraData(data);
  return diaEmBrasilia(d) === diaEmBrasilia(agora) ? `Hoje, ${formatarHora(d)}` : formatarDataHora(d);
}
