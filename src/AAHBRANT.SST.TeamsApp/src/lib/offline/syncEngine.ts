import { API_BASE_URL } from '../apiBase';
import { montarHeadersAuth } from '../authHeaders';
import { offlineDb, type ItemFilaSaida } from './db';

// Motor de sincronização offline — piloto nos módulos de campo (DDS, Inspeções, Checklists, APRs).
//
// Política acordada com o usuário (24/08):
// - Leituras (GET): cache-then-network. Offline, serve o último dado visto.
// - Mutações (POST/PUT/DELETE): se falhar por falta de conexão, entram numa fila local
//   (IndexedDB) e são reenviadas automaticamente quando a internet volta.
// - Conflito (o mesmo registro mudou no servidor enquanto o app estava offline): o servidor
//   sempre vence (evita corrupção silenciosa), mas o usuário é avisado do que foi descartado —
//   ver ConflitoSincronizacaoError e a fila em offlineDb.conflitos.
//
// Limite conhecido (avaliado e aceito nesta primeira fatia): criar um registro NOVO (ex.: um DDS
// novo) enquanto offline não retorna o id real — o servidor gera o id, e sem conexão não há como
// sabê-lo antes de sincronizar. Por isso "criar" enfileirado lança MutacaoEnfileiradaOfflineError
// em vez de fingir sucesso com um id inventado; ações sobre um registro que JÁ existe (marcar item,
// encerrar, aprovar, registrar presença) funcionam offline normalmente.

export class MutacaoEnfileiradaOfflineError extends Error {
  constructor() {
    super('Sem conexão. A ação foi salva neste dispositivo e será enviada automaticamente quando a internet voltar.');
    this.name = 'MutacaoEnfileiradaOfflineError';
  }
}

export class ConflitoSincronizacaoError extends Error {
  constructor(mensagem: string) {
    super(mensagem);
    this.name = 'ConflitoSincronizacaoError';
  }
}

type Ouvinte = () => void;
const ouvintes = new Set<Ouvinte>();

function notificarMudanca() {
  ouvintes.forEach((fn) => fn());
}

export function assinarMudancasSync(ouvinte: Ouvinte): () => void {
  ouvintes.add(ouvinte);
  return () => ouvintes.delete(ouvinte);
}

let online = navigator.onLine;

export function estaOnline(): boolean {
  return online;
}

function marcarOnline(valor: boolean) {
  if (online !== valor) {
    online = valor;
    notificarMudanca();
  }
}

window.addEventListener('online', () => {
  marcarOnline(true);
  void sincronizarFilaSaida();
});
window.addEventListener('offline', () => marcarOnline(false));

// Rede pode cair sem disparar o evento 'offline' do navegador (ex.: wifi "conectado" mas sem
// internet real de fato) — reforço periódico tentando esvaziar a fila.
setInterval(() => void sincronizarFilaSaida(), 30_000);

export async function contarPendentes(): Promise<number> {
  return offlineDb.filaSaida.count();
}

export async function listarConflitosNaoLidos() {
  const todos = await offlineDb.conflitos.orderBy('criadoEm').reverse().toArray();
  return todos.filter((c) => !c.lido);
}

export async function marcarConflitoComoLido(id: number) {
  await offlineDb.conflitos.update(id, { lido: true });
  notificarMudanca();
}

function ehErroDeRede(erro: unknown): boolean {
  return erro instanceof TypeError;
}

async function lerCorpoErro(response: Response): Promise<string> {
  return response.text().catch(() => '');
}

// Mesma proteção contra corpo não-JSON usada em lib/api.ts: um 200 com HTML (ex.: proxy/ingress
// devolvendo a página estática do front em vez da API) lançaria um SyntaxError críptico ao passar
// direto por JSON.parse — aqui vira um Error com o status HTTP, diagnosticável e não confundido
// com falha de rede por ehErroDeRede.
function parsearJsonSeguro<T>(texto: string, response: Response): T {
  if (!texto) {
    return undefined as T;
  }
  try {
    return JSON.parse(texto) as T;
  } catch {
    throw new Error(
      `Resposta inesperada do servidor (HTTP ${response.status} ${response.statusText}): esperava JSON e recebeu outro tipo de conteúdo.`,
    );
  }
}

async function registrarConflito(url: string, metodo: string, corpoRequisicao: unknown, response: Response) {
  const corpo = await lerCorpoErro(response);
  let mensagem = 'Este registro foi alterado por outra pessoa enquanto você estava offline.';
  let dadosServidor: unknown = corpo;
  try {
    const json = JSON.parse(corpo);
    mensagem = json.mensagem ?? mensagem;
    dadosServidor = json.dadosAtuais ?? json;
  } catch {
    // corpo não era JSON — mantém texto bruto em dadosServidor
  }

  await offlineDb.conflitos.add({
    url,
    metodo,
    mensagem,
    dadosDescartados: JSON.stringify(corpoRequisicao),
    dadosServidor: JSON.stringify(dadosServidor),
    criadoEm: Date.now(),
    lido: false,
  });
  notificarMudanca();
  return mensagem;
}

// ---------- Leitura (GET) ----------

export async function syncFetchJson<T>(
  path: string,
  init?: RequestInit,
  authHeaders?: Record<string, string>,
): Promise<T> {
  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...authHeaders, ...init?.headers },
    });
    marcarOnline(true);

    if (!response.ok) {
      const corpo = await lerCorpoErro(response);
      throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    const texto = await response.text();
    const resultado = parsearJsonSeguro<T>(texto, response);
    // Só grava no cache depois de validar que é JSON de verdade — cachear um corpo inválido
    // (ex.: HTML de um proxy com defeito) deixaria essa rota offline quebrada permanentemente,
    // já que toda leitura offline futura reproduziria o mesmo erro a partir do cache envenenado.
    await offlineDb.cacheJson.put({ url: path, corpoJson: texto, atualizadoEm: Date.now() });
    return resultado;
  } catch (erro) {
    if (!ehErroDeRede(erro)) {
      throw erro;
    }
    marcarOnline(false);
    const cache = await offlineDb.cacheJson.get(path);
    if (cache) {
      return JSON.parse(cache.corpoJson) as T;
    }
    throw erro;
  }
}

export async function syncFetchBlob(path: string, authHeaders?: Record<string, string>): Promise<Blob> {
  try {
    const response = await fetch(`${API_BASE_URL}${path}`, { headers: authHeaders });
    marcarOnline(true);
    if (!response.ok) {
      const corpo = await lerCorpoErro(response);
      throw new Error(`${response.status} ${response.statusText}: ${corpo}`);
    }
    const blob = await response.blob();
    await offlineDb.cacheBlob.put({ url: path, blob, atualizadoEm: Date.now() });
    return blob;
  } catch (erro) {
    if (!ehErroDeRede(erro)) {
      throw erro;
    }
    marcarOnline(false);
    const cache = await offlineDb.cacheBlob.get(path);
    if (cache) {
      return cache.blob;
    }
    throw erro;
  }
}

// ---------- Mutação JSON (POST/PUT/DELETE) ----------

export async function syncMutateJson<T>(
  path: string,
  metodo: 'POST' | 'PUT' | 'DELETE',
  corpo?: unknown,
  authHeaders?: Record<string, string>,
): Promise<T> {
  const idempotencyKey = crypto.randomUUID();

  if (online) {
    try {
      const response = await fetch(`${API_BASE_URL}${path}`, {
        method: metodo,
        headers: { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey, ...authHeaders },
        body: corpo !== undefined ? JSON.stringify(corpo) : undefined,
      });
      marcarOnline(true);

      if (response.status === 409) {
        const mensagem = await registrarConflito(path, metodo, corpo, response);
        throw new ConflitoSincronizacaoError(mensagem);
      }

      if (!response.ok) {
        const texto = await lerCorpoErro(response);
        throw new Error(`${response.status} ${response.statusText}: ${texto}`);
      }

      if (response.status === 204) {
        return undefined as T;
      }
      const texto = await response.text();
      return parsearJsonSeguro<T>(texto, response);
    } catch (erro) {
      if (!ehErroDeRede(erro)) {
        throw erro;
      }
      marcarOnline(false);
      // cai para o enfileiramento abaixo
    }
  }

  await offlineDb.filaSaida.add({
    idempotencyKey,
    url: path,
    metodo,
    isFormData: false,
    corpoJson: corpo !== undefined ? JSON.stringify(corpo) : undefined,
    criadoEm: Date.now(),
    tentativas: 0,
  });
  notificarMudanca();
  throw new MutacaoEnfileiradaOfflineError();
}

// ---------- Mutação multipart (upload de foto) ----------

export async function syncMutateMultipart<T>(
  path: string,
  formData: FormData,
  authHeaders?: Record<string, string>,
): Promise<T> {
  const idempotencyKey = crypto.randomUUID();

  if (online) {
    try {
      const response = await fetch(`${API_BASE_URL}${path}`, {
        method: 'POST',
        headers: { 'Idempotency-Key': idempotencyKey, ...authHeaders },
        body: formData,
      });
      marcarOnline(true);

      if (response.status === 409) {
        const mensagem = await registrarConflito(path, 'POST', '[multipart]', response);
        throw new ConflitoSincronizacaoError(mensagem);
      }

      if (!response.ok) {
        const texto = await lerCorpoErro(response);
        throw new Error(`${response.status} ${response.statusText}: ${texto}`);
      }
      const texto = await response.text();
      return parsearJsonSeguro<T>(texto, response);
    } catch (erro) {
      if (!ehErroDeRede(erro)) {
        throw erro;
      }
      marcarOnline(false);
    }
  }

  const camposFormData: Record<string, string> = {};
  const arquivosFormData: Record<string, Blob> = {};
  formData.forEach((valor, chave) => {
    if (valor instanceof Blob) {
      arquivosFormData[chave] = valor;
    } else {
      camposFormData[chave] = valor;
    }
  });

  await offlineDb.filaSaida.add({
    idempotencyKey,
    url: path,
    metodo: 'POST',
    isFormData: true,
    camposFormData,
    arquivosFormData,
    criadoEm: Date.now(),
    tentativas: 0,
  });
  notificarMudanca();
  throw new MutacaoEnfileiradaOfflineError();
}

// ---------- Esvaziamento da fila ----------

let sincronizando = false;

function reconstruirFormData(item: ItemFilaSaida): FormData {
  const formData = new FormData();
  Object.entries(item.camposFormData ?? {}).forEach(([chave, valor]) => formData.append(chave, valor));
  Object.entries(item.arquivosFormData ?? {}).forEach(([chave, blob]) => formData.append(chave, blob));
  return formData;
}

export async function sincronizarFilaSaida(): Promise<void> {
  if (sincronizando || !online) {
    return;
  }
  sincronizando = true;

  try {
    const itens = await offlineDb.filaSaida.orderBy('criadoEm').toArray();
    for (const item of itens) {
      try {
        // Busca o header de autenticação de novo a cada item (não no momento do enfileiramento):
        // a fila pode ficar dias esperando a internet voltar, e o token capturado offline já
        // estaria expirado.
        const authHeaders = await montarHeadersAuth();
        const response = await fetch(`${API_BASE_URL}${item.url}`, {
          method: item.metodo,
          headers: item.isFormData
            ? { 'Idempotency-Key': item.idempotencyKey, ...authHeaders }
            : { 'Content-Type': 'application/json', 'Idempotency-Key': item.idempotencyKey, ...authHeaders },
          body: item.isFormData ? reconstruirFormData(item) : item.corpoJson,
        });

        if (response.status === 409) {
          await registrarConflito(item.url, item.metodo, item.corpoJson ?? '[multipart]', response);
          await offlineDb.filaSaida.delete(item.id!);
          continue;
        }

        if (response.ok) {
          await offlineDb.filaSaida.delete(item.id!);
          continue;
        }

        // Erro definitivo do servidor (ex.: 400 de validação) não se resolve tentando de novo com
        // o mesmo corpo — mas também não pode travar o resto da fila. Registra e segue adiante;
        // depois de 5 tentativas, vira "conflito" para o usuário decidir manualmente (descartar
        // ou refazer a ação online).
        const novasTentativas = item.tentativas + 1;
        if (response.status < 500 && novasTentativas >= 5) {
          const texto = await lerCorpoErro(response);
          await offlineDb.conflitos.add({
            url: item.url,
            metodo: item.metodo,
            mensagem: `Falha ao sincronizar após ${novasTentativas} tentativas: ${texto || response.statusText}`,
            dadosDescartados: item.corpoJson ?? '[multipart]',
            criadoEm: Date.now(),
            lido: false,
          });
          await offlineDb.filaSaida.delete(item.id!);
        } else {
          await offlineDb.filaSaida.update(item.id!, {
            tentativas: novasTentativas,
            ultimoErro: `${response.status} ${response.statusText}`,
          });
        }
      } catch (erro) {
        if (ehErroDeRede(erro)) {
          marcarOnline(false);
          break; // ainda offline de verdade — para e tenta tudo de novo na próxima reconexão
        }
        throw erro;
      }
    }
  } finally {
    sincronizando = false;
    notificarMudanca();
  }
}

// Ao carregar o app, se já houver itens de uma sessão anterior e a rede estiver de fato
// disponível, tenta esvaziar a fila imediatamente.
void sincronizarFilaSaida();
