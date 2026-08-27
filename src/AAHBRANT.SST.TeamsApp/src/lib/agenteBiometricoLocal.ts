// Porta fixa definida em Program.cs do AAHBRANT.SST.AgenteBiometria (Kestrel em 127.0.0.1:5251).
const AGENTE_LOCAL_URL = 'http://127.0.0.1:5251';

export interface DispositivoLocal {
  dispositivoId: string;
  segredoDispositivo: string;
}

export interface CapturaLocal {
  trabalhadorId: string;
  score: number;
}

async function requisitarAgenteLocal<T>(caminho: string, init?: RequestInit): Promise<T> {
  const resposta = await fetch(`${AGENTE_LOCAL_URL}${caminho}`, init);
  if (!resposta.ok) {
    const corpo = await resposta.text().catch(() => '');
    throw new Error(`${resposta.status} ${resposta.statusText}: ${corpo}`);
  }
  return (await resposta.json()) as T;
}

export async function estaAgenteLocalDisponivel(): Promise<boolean> {
  try {
    await requisitarAgenteLocal('/api/dispositivo');
    return true;
  } catch {
    return false;
  }
}

// Chamado uma vez ao carregar a tela do quiosque — o resultado deve ficar só em memória (variável
// de estado do componente React), nunca em localStorage, e ser enviado só no corpo do POST final
// de assinatura, nunca em query string.
export function obterDispositivoLocal(): Promise<DispositivoLocal> {
  return requisitarAgenteLocal<DispositivoLocal>('/api/dispositivo');
}

export function sincronizarTemplatesLocal(): Promise<{ total: number }> {
  return requisitarAgenteLocal<{ total: number }>('/api/sincronizar', { method: 'POST' });
}

export function capturarDigitalLocal(): Promise<CapturaLocal> {
  return requisitarAgenteLocal<CapturaLocal>('/api/capturar', { method: 'POST' });
}

// Usado só na tela de cadastro (AssinaturaTab) — captura a digital bruta (não comparada contra
// cache nenhum) para enviar ao backend, que criptografa e persiste como novo template.
// O agente serializa o byte[] via System.Text.Json, que já o codifica como string base64 — não
// como array de números — então basta repassar o valor recebido.
export async function capturarDigitalBrutaLocal(): Promise<string> {
  const resultado = await requisitarAgenteLocal<{ templateBruto: string }>('/api/capturar-bruto', { method: 'POST' });
  return resultado.templateBruto;
}
