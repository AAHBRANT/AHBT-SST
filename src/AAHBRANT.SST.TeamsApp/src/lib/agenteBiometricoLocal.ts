import { tocarBipeLeituraDigital } from './bipeAssinatura';
import { api } from './api';

// Porta fixa definida em Program.cs do AAHBRANT.SST.AgenteBiometria (Kestrel em 127.0.0.1:5251).
const AGENTE_LOCAL_URL = 'http://127.0.0.1:5251';

// Agente atualizado (11/10) não devolve mais o segredo: ele assina cada leitura com uma chave própria e o
// servidor confere. segredoDispositivo só vem de agentes antigos, durante a transição.
export interface DispositivoLocal {
  dispositivoId: string;
  segredoDispositivo?: string;
  leituraAssinada?: boolean;
}

// Prova de que a leitura veio do leitor: desafio emitido pelo servidor + assinatura do agente.
export interface LeituraAssinada {
  desafio: string;
  assinatura: string;
}

export interface CapturaLocal {
  trabalhadorId: string;
  score: number;
  // PNG (base64) da impressão lida pelo leitor — enviado junto da assinatura como evidência visual.
  imagemPng?: string | null;
  // Uma por documento pedido em capturarDigitalLocal (mesma ordem); vazio com agente antigo.
  leituras: LeituraAssinada[];
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

// Pede ao servidor um desafio por documento (quantidade) e uma única leitura ao agente, que assina todos.
// Agente antigo ignora os desafios e devolve a leitura sem assinatura (leituras vazio): aí a tela cai no
// caminho antigo, com o segredo, enquanto o servidor ainda aceitar.
export async function capturarDigitalLocal(dispositivoId: string, quantidade = 1): Promise<CapturaLocal> {
  const desafios = await Promise.all(
    Array.from({ length: Math.max(1, quantidade) }, () => api.dispositivosAgente.emitirDesafio(dispositivoId)),
  );
  const r = await requisitarAgenteLocal<{
    trabalhadorId: string;
    score: number;
    imagemPng?: string | null;
    assinaturas?: string[] | null;
  }>('/api/capturar', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ desafios }),
  });
  const assinaturas = r.assinaturas ?? [];
  return {
    trabalhadorId: r.trabalhadorId,
    score: r.score,
    imagemPng: r.imagemPng,
    leituras: assinaturas.length === desafios.length ? desafios.map((desafio, i) => ({ desafio, assinatura: assinaturas[i] })) : [],
  };
}

// Campos que vão no corpo das chamadas de assinatura/presença: leitura assinada quando houver, senão o segredo.
export function credencialLeitura(dispositivo: DispositivoLocal, leitura: LeituraAssinada | undefined) {
  return leitura
    ? { dispositivoId: dispositivo.dispositivoId, desafio: leitura.desafio, assinaturaLeitura: leitura.assinatura }
    : { dispositivoId: dispositivo.dispositivoId, segredoDispositivo: dispositivo.segredoDispositivo };
}
export type CredencialLeitura = ReturnType<typeof credencialLeitura>;

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
