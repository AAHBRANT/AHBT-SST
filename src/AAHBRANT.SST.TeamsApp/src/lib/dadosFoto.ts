import { geoLocation } from '@microsoft/teams-js';
import { aguardarInicializacaoTeams } from '../teams/teamsInit';

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
  // Foto anexada da galeria: sem data/posição comprováveis; só exige obra e descrição do local.
  if (d.origem === 'arquivo') {
    if (!d.obraId || !d.obraNome?.trim()) pendencias.push('obra vinculada');
    if (!d.local?.trim()) pendencias.push('descrição do local');
    return pendencias;
  }
  if (!d?.capturadaEm || !Number.isFinite(Date.parse(d.capturadaEm))) pendencias.push('data e hora da captura');
  if (!d?.obraId || !d.obraNome?.trim()) pendencias.push('obra vinculada');
  if (!d?.local?.trim()) pendencias.push('descrição do local');
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

// Margem abaixo do limite de 60 s da validação (front e backend): se a posição já tem mais que isso
// na hora de capturar, ela é renovada antes de gravar a foto.
export const IDADE_MAX_LOCALIZACAO_MS = 30_000;
export function localizacaoExpirada(l: { latitude?: number | null; localizacaoObtidaEm?: string | null }, agora = Date.now()) {
  const obtida = l.localizacaoObtidaEm ? Date.parse(l.localizacaoObtidaEm) : NaN;
  return l.latitude == null || !Number.isFinite(obtida) || Math.abs(agora - obtida) > IDADE_MAX_LOCALIZACAO_MS;
}

export type LocalizacaoFoto = Pick<DadosFoto, 'latitude' | 'longitude' | 'precisaoMetros' | 'localizacaoObtidaEm' | 'motivoLocalizacao'>;
export async function obterLocalizacaoFoto(): Promise<LocalizacaoFoto> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  try {
    return await Promise.race([
      (async (): Promise<LocalizacaoFoto> => {
        // Alguns clientes Teams (ex.: tablet) não expõem a capability geoLocation do SDK; nesses casos
        // cai para a geolocalização do WebView em vez de encerrar com erro. Se o SDK declara suporte
        // mas falha ao obter a posição (permissão negada, erro do cliente), também tenta o WebView.
        try {
          if (await aguardarInicializacaoTeams() && geoLocation.isSupported()) {
            const pos = await geoLocation.getCurrentLocation();
            return { latitude: pos.latitude, longitude: pos.longitude, precisaoMetros: pos.accuracy, localizacaoObtidaEm: pos.timestamp ? new Date(pos.timestamp).toISOString() : new Date().toISOString() };
          }
        } catch { /* segue para o fallback do WebView */ }
        if (!navigator.geolocation) return { motivoLocalizacao: 'Localização indisponível neste navegador.' };
        return await new Promise<LocalizacaoFoto>((resolve) => navigator.geolocation.getCurrentPosition(
          p => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude, precisaoMetros: p.coords.accuracy, localizacaoObtidaEm: new Date(p.timestamp).toISOString() }),
          e => resolve({ motivoLocalizacao: e.code === 1 ? 'Permissão de localização bloqueada. Libere nas configurações do aparelho e do aplicativo.' : e.code === 3 ? 'O aparelho demorou para obter a localização. Tente novamente no local.' : 'O aparelho não conseguiu obter a localização. Tente novamente no local.' }),
          { enableHighAccuracy: true, maximumAge: 0, timeout: 15_000 },
        ));
      })(),
      new Promise<LocalizacaoFoto>(resolve => { timer = setTimeout(() => resolve({ motivoLocalizacao: 'Tempo de localização esgotado. Tente novamente no local.' }), 20_000); }),
    ]);
  } catch {
    return { motivoLocalizacao: 'Não foi possível obter a localização. Confira as permissões e tente novamente.' };
  } finally { clearTimeout(timer); }
}
