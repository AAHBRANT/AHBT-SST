import { tocarBipeLeituraDigital } from './bipeAssinatura';

// Porta fixa definida em Program.cs do AAHBRANT.SST.AgenteBiometria (Kestrel em 127.0.0.1:5251).
const AGENTE_LOCAL_URL = 'http://127.0.0.1:5251';

export interface DispositivoLocal {
  dispositivoId: string;
  segredoDispositivo: string;
}

export interface CapturaLocal {
  trabalhadorId: string;
  score: number;
  // PNG (base64) da impressão lida pelo leitor — enviado junto da assinatura como evidência visual.
  imagemPng?: string | null;
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

// Usado só nas telas de cadastro — captura a digital bruta (não comparada contra cache nenhum) para
// enviar ao backend, que criptografa e persiste como novo template.
// O agente serializa o byte[] via System.Text.Json, que já o codifica como string base64 — não
// como array de números — então basta repassar o valor recebido.
// novoToque: exige tirar o dedo e apoiar de novo (segunda leitura do cadastro).
export interface CapturaBruta {
  templateBruto: string;
  // PNG (base64) da leitura — guardado criptografado como referência do cadastro (log de assinaturas).
  imagemPng: string | null;
}

export async function capturarDigitalBrutaLocal(novoToque = false): Promise<CapturaBruta> {
  const resultado = await requisitarAgenteLocal<{ templateBruto: string; imagemPng?: string | null }>(
    `/api/capturar-bruto${novoToque ? '?novoToque=true' : ''}`,
    { method: 'POST' },
  );
  return { templateBruto: resultado.templateBruto, imagemPng: resultado.imagemPng ?? null };
}

export function compararTemplatesLocal(templateA: string, templateB: string): Promise<{ score: number }> {
  return requisitarAgenteLocal<{ score: number }>('/api/comparar-templates', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ templateA, templateB }),
  });
}

// Duas leituras do mesmo dedo precisam concordar entre si, na mesma escala do limiar de assinatura do
// backend (50). Uma digital borrada ou mal apoiada no cadastro derrubaria todo reconhecimento futuro
// daquele trabalhador — melhor recusar na hora, com ele ainda no leitor.
export const LIMIAR_CONFIRMACAO_CADASTRO = 50;

export async function capturarDigitalParaCadastro(aoMudarEtapa: (mensagem: string) => void): Promise<CapturaBruta> {
  aoMudarEtapa('1ª leitura: apoie o dedo no leitor e mantenha até terminar.');
  const primeira = await capturarDigitalBrutaLocal();
  tocarBipeLeituraDigital();

  aoMudarEtapa('Retire o dedo e apoie o MESMO dedo novamente para confirmar (2ª leitura).');
  const segunda = await capturarDigitalBrutaLocal(true);

  aoMudarEtapa('Comparando as duas leituras…');
  const { score } = await compararTemplatesLocal(primeira.templateBruto, segunda.templateBruto);
  if (score < LIMIAR_CONFIRMACAO_CADASTRO) {
    throw new Error(
      'As duas leituras não coincidiram (dedo diferente, mal apoiado ou digital borrada). ' +
        'Limpe o vidro do leitor e o dedo, apoie o mesmo dedo com firmeza e tente novamente.',
    );
  }
  return primeira;
}
