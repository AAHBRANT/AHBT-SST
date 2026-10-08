import { criarRastreadorFoto } from './fontesLocalizacao';
import type { LeituraLocalizacao } from './rastreadorLocalizacao';

export interface ContextoFoto { obraId: string; obraNome: string; local?: string | null }
export interface DadosFoto {
  capturadaEm?: string | null;
  fusoMinutos?: number | null;
  origem: 'camera' | 'arquivo';
  local?: string | null;
  obraId?: string | null;
  obraNome?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  precisaoMetros?: number | null;
  localizacaoObtidaEm?: string | null;
  motivoLocalizacao?: string | null;
  recebidaEm?: string | null;
}

const dadosPorArquivo = new WeakMap<Blob, DadosFoto>();
export function vincularDadosFoto(arquivo: Blob, dados: DadosFoto) { dadosPorArquivo.set(arquivo, dados); }
export function obterDadosFoto(arquivo: Blob) { return dadosPorArquivo.get(arquivo); }
export function anexarDadosFoto(form: FormData, arquivo: File) {
  const dados = obterDadosFoto(arquivo);
  if (dados) form.set('metadados', JSON.stringify(dados));
}

export function pendenciasFoto(d?: DadosFoto | null): string[] {
  // Ausência total de metadados identifica uma foto legada. Fotos novas recebem o objeto
  // serializado pelo servidor, inclusive quando ainda estão pendentes.
  if (!d) return [];
  const pendencias: string[] = [];
  // Foto anexada da galeria: sem data/posição comprováveis; só exige a obra.
  // "Local da foto" deixou de ser exigido (pedido do usuário, 08/10) — só aparece em fotos antigas.
  if (d.origem === 'arquivo') {
    if (!d.obraId || !d.obraNome?.trim()) pendencias.push('obra vinculada');
    return pendencias;
  }
  if (!d?.capturadaEm || !Number.isFinite(Date.parse(d.capturadaEm))) pendencias.push('data e hora da captura');
  if (!d?.obraId || !d.obraNome?.trim()) pendencias.push('obra vinculada');
  const lat = d?.latitude, lon = d?.longitude;
  const idade = d?.capturadaEm && d.localizacaoObtidaEm ? Math.abs(Date.parse(d.capturadaEm) - Date.parse(d.localizacaoObtidaEm)) : Infinity;
  if (lat == null || lon == null || !Number.isFinite(lat) || !Number.isFinite(lon)
    || Math.abs(lat) > 90 || Math.abs(lon) > 180 || !(idade <= 60_000)
    || (d?.precisaoMetros != null && !(d.precisaoMetros >= 0 && d.precisaoMetros <= 100))) {
    pendencias.push('geolocalização da captura');
  }
  return pendencias;
}

export function pendenciasGrade(fotos: { ordem: number; dadosFoto?: DadosFoto | null }[], total = 3): string[] {
  return Array.from({ length: total }, (_, i) => {
    const foto = fotos.find(f => f.ordem === i + 1);
    if (!foto) return `Foto ${i + 1}: tire a foto obrigatória.`;
    const faltas = pendenciasFoto(foto.dadosFoto);
    return faltas.length ? `Foto ${i + 1}: falta ${faltas.join(', ')}.` : '';
  }).filter(Boolean);
}

export function formatarDataFoto(d?: DadosFoto | null) {
  if (!d?.capturadaEm || !Number.isFinite(Date.parse(d.capturadaEm))) return 'Data/hora da captura não disponíveis';
  // Preserva a hora local do aparelho na captura, mesmo ao consultar em outro fuso.
  const minutos = d.fusoMinutos ?? 0;
  return new Date(Date.parse(d.capturadaEm) - minutos * 60_000).toLocaleString('pt-BR', { timeZone: 'UTC' })
    + ` (UTC${minutos <= 0 ? '+' : '-'}${String(Math.floor(Math.abs(minutos) / 60)).padStart(2, '0')}:${String(Math.abs(minutos) % 60).padStart(2, '0')})`;
}

export type LocalizacaoFoto = Pick<DadosFoto, 'latitude' | 'longitude' | 'precisaoMetros' | 'localizacaoObtidaEm' | 'motivoLocalizacao'>;

export function leituraParaLocalizacao(l: LeituraLocalizacao): LocalizacaoFoto {
  return { latitude: l.latitude, longitude: l.longitude, precisaoMetros: l.precisaoMetros, localizacaoObtidaEm: new Date(l.obtidaEm).toISOString() };
}

// Pedido avulso (ex.: log de assinaturas). A câmera usa o rastreador contínuo direto (useCapturaFoto).
// Devolve cedo quando há posição boa ou quando todas as fontes falharam; no teto, entrega a melhor
// posição que tiver, mesmo imprecisa — quem consome decide se a precisão serve.
export async function obterLocalizacaoFoto(tetoMs = 20_000): Promise<LocalizacaoFoto> {
  const r = criarRastreadorFoto();
  try {
    await r.aguardarPronta(tetoMs);
    const e = r.estado();
    if (e.leitura) return leituraParaLocalizacao(e.leitura);
    return { motivoLocalizacao: e.motivo ?? 'Tempo de localização esgotado. Tente novamente no local.' };
  } catch {
    return { motivoLocalizacao: 'Não foi possível obter a localização. Confira as permissões e tente novamente.' };
  } finally { r.parar(); }
}
