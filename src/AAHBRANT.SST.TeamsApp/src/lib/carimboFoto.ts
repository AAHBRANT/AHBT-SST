import { api } from './api';

// Carimbo de data, hora e local desenhado na própria foto, IDÊNTICO ao do G-QUALI (padrão Timestamp Camera):
// texto branco no canto SUPERIOR DIREITO, alinhado à direita, todas as linhas do mesmo tamanho, sem faixa, com sombra
// suave e contorno fino escuro para leitura em parede clara. Linhas: data e hora por extenso, endereço (quando houver),
// latitude/longitude com grau, altitude e uma nota de identificação (obra e local). A evidência viaja junto com a imagem.
// Só vale para foto tirada agora pela câmera; foto da galeria e foto do reconhecimento facial nunca são carimbadas.

const FONTE_MINIMA = 8;
const PROPORCAO_FONTE = 28; // largura / 28 ≈ 3,6 % da largura

export function desenharCarimbo(ctx: CanvasRenderingContext2D, largura: number, altura: number, linhas: string[]): void {
  if (linhas.length === 0) return;
  let fonte = Math.max(12, Math.round(largura / PROPORCAO_FONTE));
  const familia = 'sans-serif';
  const alturaLinha = () => Math.round(fonte * 1.3);
  for (let tentativa = 0; tentativa < 14; tentativa++) {
    ctx.font = `${fonte}px ${familia}`;
    const maior = Math.max(...linhas.map((l) => ctx.measureText(l).width));
    const cabeLargura = maior <= largura * 0.9 - fonte;
    const cabeAltura = linhas.length * alturaLinha() + fonte * 2 <= altura * 0.6;
    if ((cabeLargura && cabeAltura) || fonte <= FONTE_MINIMA) break;
    fonte = Math.max(FONTE_MINIMA, Math.floor(fonte * 0.92));
  }
  ctx.font = `${fonte}px ${familia}`;
  const margem = Math.round(largura * 0.045);
  ctx.textAlign = 'right';
  ctx.textBaseline = 'top';
  ctx.lineJoin = 'round';
  ctx.lineWidth = Math.max(1, Math.round(fonte / 11));
  ctx.strokeStyle = 'rgba(0, 0, 0, 0.45)';
  ctx.shadowColor = 'rgba(0, 0, 0, 0.55)';
  ctx.shadowBlur = Math.max(2, Math.round(fonte / 5));
  ctx.shadowOffsetX = 0;
  ctx.shadowOffsetY = Math.max(1, Math.round(fonte / 16));
  ctx.fillStyle = '#ffffff';
  let y = Math.round(fonte * 1.1);
  for (const linha of linhas) {
    ctx.strokeText(linha, largura - margem, y);
    ctx.fillText(linha, largura - margem, y);
    y += alturaLinha();
  }
  ctx.shadowColor = 'rgba(0, 0, 0, 0)';
  ctx.shadowBlur = 0;
  ctx.shadowOffsetY = 0;
}

// "19 de out. de 2026, 14:40:05", em hora de Brasília, montado peça a peça para não variar entre navegadores.
export function dataHoraExtenso(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const partes = new Intl.DateTimeFormat('pt-BR', {
    timeZone: 'America/Sao_Paulo', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
  }).formatToParts(d);
  const p = (tipo: string) => partes.find((x) => x.type === tipo)?.value ?? '';
  const mes = p('month').replace(/\.$/, '');
  return `${p('day')} de ${mes}. de ${p('year')}, ${p('hour')}:${p('minute')}:${p('second')}`;
}

export interface PosicaoCarimbo {
  latitude?: number | null;
  longitude?: number | null;
  altitudeMetros?: number | null;
  precisaoMetros?: number | null;
  motivo?: string | null;
}

const grau = (v: number) => v.toFixed(6);
const metros = (v: number) => `${Math.round(v)}`;
const numero = (v: number | null | undefined): v is number => typeof v === 'number' && Number.isFinite(v);

export function linhasCarimbo(capturadaEmIso: string, posicao: PosicaoCarimbo, nota?: string, endereco?: string[]): string[] {
  const linhas: string[] = [dataHoraExtenso(capturadaEmIso)];
  for (const e of endereco ?? []) if (e.trim() !== '') linhas.push(e.trim());
  const { latitude, longitude } = posicao;
  if (numero(latitude) && numero(longitude)) {
    linhas.push(`Lat: ${grau(latitude)}°  Long: ${grau(longitude)}°`);
    const altitude = numero(posicao.altitudeMetros) ? `${metros(posicao.altitudeMetros)} m` : 'n/d';
    linhas.push(`Alt: ${altitude}${numero(posicao.precisaoMetros) ? `  (±${metros(posicao.precisaoMetros)} m)` : ''}`);
  } else {
    linhas.push(`Sem localização: ${posicao.motivo?.trim() || 'GPS indisponível'}`);
  }
  if (nota && nota.trim() !== '') linhas.push(nota.trim());
  return linhas.filter(Boolean);
}

// ---- endereço por extenso (Azure Maps pela API) ----
// Nunca trava a foto: tempo limite curto e qualquer falha = sem endereço. Só consulta de novo se andou mais de 50 m.

export const DISTANCIA_NOVA_CONSULTA_M = 50;
export const TEMPO_LIMITE_ENDERECO_MS = 4000;

export function distanciaMetros(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const rad = (g: number) => (g * Math.PI) / 180;
  const dLat = rad(lat2 - lat1);
  const dLon = rad(lon2 - lon1);
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin(dLon / 2) ** 2;
  return 6371000 * 2 * Math.asin(Math.min(1, Math.sqrt(a)));
}

const linhasValidas = (x: unknown): string[] =>
  Array.isArray(x) ? x.filter((l): l is string => typeof l === 'string' && l.trim() !== '').map((l) => l.trim()).slice(0, 8) : [];

export async function enderecoDaPosicao(latitude: number, longitude: number, limiteMs = TEMPO_LIMITE_ENDERECO_MS): Promise<string[]> {
  let guarda: ReturnType<typeof setTimeout> | undefined;
  try {
    const tempo = new Promise<never>((_, rejeitar) => { guarda = setTimeout(() => rejeitar(new Error('tempo')), limiteMs); });
    return linhasValidas((await Promise.race([api.fotos.endereco(latitude, longitude), tempo])).linhas);
  } catch {
    return [];
  } finally {
    clearTimeout(guarda);
  }
}

export interface ObservadorEndereco {
  atualizar(latitude: number, longitude: number): void;
  linhas(latitude: number, longitude: number): string[];
  parar(): void;
}

// Câmera aberta: acompanha a posição e mantém o endereço do ponto atual pronto para o clique.
export function observarEndereco(): ObservadorEndereco {
  let atual: string[] = [];
  let consultadoEm: { lat: number; lon: number } | null = null;
  let emAndamento = false;
  let parado = false;
  let proximaTentativa = 0;
  const ESPERA_APOS_FALHA_MS = 15000;
  return {
    atualizar(latitude, longitude) {
      if (parado || emAndamento) return;
      const moveu = consultadoEm === null || distanciaMetros(consultadoEm.lat, consultadoEm.lon, latitude, longitude) > DISTANCIA_NOVA_CONSULTA_M;
      if (!moveu || Date.now() < proximaTentativa) return;
      emAndamento = true;
      void enderecoDaPosicao(latitude, longitude).then((linhas) => {
        emAndamento = false;
        if (parado) return;
        if (linhas.length > 0) { atual = linhas; consultadoEm = { lat: latitude, lon: longitude }; }
        else proximaTentativa = Date.now() + ESPERA_APOS_FALHA_MS;
      });
    },
    // Só devolve o endereço se ele for do ponto atual (até 50 m).
    linhas(latitude, longitude) {
      return consultadoEm && distanciaMetros(consultadoEm.lat, consultadoEm.lon, latitude, longitude) <= DISTANCIA_NOVA_CONSULTA_M ? atual : [];
    },
    parar() { parado = true; },
  };
}
