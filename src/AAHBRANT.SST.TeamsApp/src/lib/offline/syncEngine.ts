import { API_BASE_URL } from '../apiBase';
import { montarHeadersAuth } from '../authHeaders';
import { offlineDb, type ItemFilaSaida } from './db';
import { contaOffline } from './conta';
import { aplicarAcaoInspecao, ehEncerramentoInspecao, ehCriacaoInspecao } from './inspecaoEstado';

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

export function notificarMudanca() {
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

// "Marcar como visto" também descarta a ação guardada (e a foto), liberando o espaço do aparelho.
export async function marcarConflitoComoLido(id: number) {
  const conflito = await offlineDb.conflitos.get(id);
  if (conflito?.item && ehCriacaoInspecao(conflito.item)) {
    // A interface confirma explicitamente o descarte de toda a criação local e dos seus anexos.
    const item = conflito.item;
    await offlineDb.transaction('rw', offlineDb.conflitos, offlineDb.filaSaida, offlineDb.inspecoesLocais, async () => {
      const fila = (await offlineDb.filaSaida.toArray()).filter(i => i.inspecaoId === item.inspecaoId && i.conta === item.conta);
      await offlineDb.filaSaida.bulkDelete(fila.map(i => i.id!));
      const conflitos = (await offlineDb.conflitos.toArray()).filter(c => c.item?.inspecaoId === item.inspecaoId && c.item?.conta === item.conta);
      await offlineDb.conflitos.bulkDelete(conflitos.map(c => c.id!));
      await offlineDb.inspecoesLocais.delete(`${item.conta}|${item.inspecaoId}`);
    });
    notificarMudanca();
    return;
  }
  await offlineDb.conflitos.update(id, { lido: true, item: undefined });
  notificarMudanca();
}

// Devolve à fila uma ação que o servidor recusou (ex.: rosto não reconhecido, pessoa sem cadastro na hora),
// depois de corrigida a causa. Vai com chave de idempotência nova: a tentativa anterior foi recusada.
export async function reenviarConflito(id: number) {
  const conflito = await offlineDb.conflitos.get(id);
  if (!conflito?.item) return;
  if (conflito.item.inspecaoId) {
    const headers = await montarHeadersAuth();
    const conta = contaDoHeader(headers) ?? (import.meta.env.DEV ? 'desenvolvimento' : undefined);
    if (conta !== conflito.item.conta) throw new Error('Entre com a conta que registrou esta inspeção para reenviar.');
    if (!ehCriacaoInspecao(conflito.item)) {
    const response = await fetch(`${API_BASE_URL}/api/inspecoes/${conflito.item.inspecaoId}`, { headers });
    if (!response.ok) throw new Error('Não foi possível conferir a versão atual da inspeção. Tente novamente com internet e login ativo.');
    const detalhe = await response.json();
    const respostaId = /^\/api\/inspecoes\/respostas\/([^/]+)/.exec(conflito.item.url)?.[1];
    conflito.item.versaoResposta = detalhe.respostas.find((r: { id: string }) => r.id === respostaId)?.versaoResposta;
    }
  }
  await offlineDb.filaSaida.add({ ...conflito.item, idempotencyKey: crypto.randomUUID(), criadoEm: Date.now(), tentativas: 0, ultimoErro: undefined });
  await offlineDb.conflitos.delete(id);
  notificarMudanca();
  void sincronizarFilaSaida();
}

// Nome da ação para a pessoa (em vez de "POST /api/documentos/…/autenticacao/facial").
export function descreverAcaoOffline(url: string, metodo: string): string {
  if (url === '/api/inspecoes' && metodo === 'POST') return 'Criação da inspeção';
  if (/\/inspecoes\/[^/]+\/encerrar$/.test(url)) return 'Finalização da inspeção';
  if (url.includes('/inspecoes/respostas/')) return url.endsWith('/foto') || url.endsWith('/foto-depois') ? 'Foto da inspeção' : 'Resposta do checklist';
  if (url.includes('/autenticacao/facial')) return 'Assinatura por reconhecimento facial';
  if (url.includes('/fotos-evidencia')) return 'Foto de evidência';
  if (url.includes('/certificado/arquivo')) return 'Certificado de treinamento';
  if (url.includes('/termoscompromissoepi/')) return 'Termo de compromisso assinado em papel';
  if (url.includes('/acidentes/')) return 'Foto de acidente';
  return `${metodo} ${url}`;
}

function ehErroDeRede(erro: unknown): boolean {
  return erro instanceof TypeError;
}

async function lerCorpoErro(response: Response): Promise<string> {
  return response.text().catch(() => '');
}

// Mesma correção aplicada em lib/api.ts: o backend responde erro como {"erro": "mensagem"} (ou
// {"title": "..."} num 404 de rota que nem chega a controller) — sem isso, o usuário via
// "400 Bad Request: {...}" cru em vez da mensagem limpa.
function extrairMensagemErro(corpo: string, status: number, statusText: string): string {
  if (status === 401) {
    return 'Sessão não autenticada (401). Entre com sua conta Microsoft/Entra para sincronizar os dados.';
  }
  if (corpo) {
    try {
      const json = JSON.parse(corpo) as { erro?: string; title?: string };
      if (typeof json.erro === 'string' && json.erro) return json.erro;
      if (typeof json.title === 'string' && json.title) return json.title;
    } catch {
      // corpo não era JSON — cai no texto bruto abaixo
    }
  }
  return corpo ? `${statusText} (${status}): ${corpo}` : `${statusText} (${status})`;
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
      throw new Error(extrairMensagemErro(corpo, response.status, response.statusText));
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
      throw new Error(extrairMensagemErro(corpo, response.status, response.statusText));
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

// ---------- Conta dona da ação ----------

// Lê tid/oid do Bearer (sem validar: só separa as filas por pessoa; quem valida é o servidor).
function contaDoHeader(authHeaders?: Record<string, string>): string | undefined {
  const token = authHeaders?.Authorization?.replace(/^Bearer\s+/i, '');
  const payload = token?.split('.')[1];
  if (!payload) return undefined;
  try {
    const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    return json.oid ? `${json.tid ?? ''}:${json.oid}` : undefined;
  } catch {
    return undefined;
  }
}

async function contaAtual(authHeaders?: Record<string, string>): Promise<string | undefined> {
  if (authHeaders?.Authorization) return contaDoHeader(authHeaders);
  if (!navigator.onLine) return contaOffline();
  try {
    return contaDoHeader(await montarHeadersAuth());
  } catch {
    return undefined;
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
        throw new Error(extrairMensagemErro(texto, response.status, response.statusText));
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

  // Credencial do leitor de digital nunca vai para o IndexedDB (auditoria 19/10, A04): sem internet a
  // leitura de digital falha e é refeita depois.
  if (corpo && typeof corpo === 'object' && 'segredoDispositivo' in corpo) {
    throw new Error('Sem internet: a assinatura por digital precisa de conexão. Tente de novo quando a rede voltar.');
  }

  await offlineDb.filaSaida.add({
    conta: await contaAtual(authHeaders),
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
        throw new Error(extrairMensagemErro(texto, response.status, response.statusText));
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
    conta: await contaAtual(authHeaders),
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
let novaSincronizacaoSolicitada = false;

function reconstruirFormData(item: ItemFilaSaida): FormData {
  const formData = new FormData();
  Object.entries(item.camposFormData ?? {}).forEach(([chave, valor]) => formData.append(chave, valor));
  Object.entries(item.arquivosFormData ?? {}).forEach(([chave, blob]) => formData.append(chave, blob));
  return formData;
}

export async function sincronizarFilaSaida(): Promise<void> {
  if (sincronizando) {
    novaSincronizacaoSolicitada = true;
    return;
  }
  if (!navigator.onLine) {
    return;
  }
  sincronizando = true;

  try {
    const itens = (await offlineDb.filaSaida.orderBy('criadoEm').toArray())
      .sort((a, b) => (ehCriacaoInspecao(a) ? -1 : Number(ehEncerramentoInspecao(a))) - (ehCriacaoInspecao(b) ? -1 : Number(ehEncerramentoInspecao(b))));
    const bloqueadas = new Set((await offlineDb.conflitos.toArray())
      .filter(c => !c.lido && c.item?.inspecaoId).map(c => `${c.item!.conta}|${c.item!.inspecaoId}`));
    for (const planejado of itens) {
      const item = await offlineDb.filaSaida.get(planejado.id!);
      if (!item) continue;
      if (ehEncerramentoInspecao(item)) {
        const mesmaInspecao = (acao: Omit<ItemFilaSaida, 'id'>) => item.inspecaoId
          ? acao.inspecaoId === item.inspecaoId && acao.conta === item.conta : acao.url.startsWith('/api/inspecoes/');
        const pendencias = (await offlineDb.filaSaida.toArray()).some(i => i.id !== item.id && !ehEncerramentoInspecao(i) && mesmaInspecao(i));
        const recusas = (await offlineDb.conflitos.toArray()).some(c => !c.lido && c.item && mesmaInspecao(c.item));
        if (pendencias || recusas) {
          await offlineDb.filaSaida.update(item.id!, { ultimoErro: 'Aguardando respostas e fotos desta inspeção.' });
          continue;
        }
      }
      const grupo = item.inspecaoId ? `${item.conta}|${item.inspecaoId}` : undefined;
      if (grupo && bloqueadas.has(grupo)) {
        await offlineDb.filaSaida.update(item.id!, { ultimoErro: 'Aguardando resolver os envios anteriores desta inspeção.' });
        continue;
      }
      try {
        // Busca o header de autenticação de novo a cada item (não no momento do enfileiramento):
        // a fila pode ficar dias esperando a internet voltar, e o token capturado offline já
        // estaria expirado.
        const authHeaders = await montarHeadersAuth();
        // Ação de outra conta fica guardada até a dona dela entrar de novo neste navegador.
        const donaAtual = contaDoHeader(authHeaders) ?? (import.meta.env.DEV ? 'desenvolvimento' : undefined);
        if (item.conta && donaAtual !== item.conta) {
          await offlineDb.filaSaida.update(item.id!, { ultimoErro: 'Aguardando a pessoa que registrou entrar para enviar.' });
          continue;
        }
        const form = item.isFormData ? reconstruirFormData(item) : undefined;
        if (form && item.versaoResposta != null) form.set('versaoEsperada', item.versaoResposta);
        const corpo = !item.isFormData && item.versaoResposta != null
          ? JSON.stringify({ ...JSON.parse(item.corpoJson ?? '{}'), versaoEsperada: item.versaoResposta }) : item.corpoJson;
        const response = await fetch(`${API_BASE_URL}${item.url}`, {
          method: item.metodo,
          headers: item.isFormData
            ? { 'Idempotency-Key': item.idempotencyKey, ...authHeaders }
            : { 'Content-Type': 'application/json', 'Idempotency-Key': item.idempotencyKey, ...authHeaders },
          body: form ?? corpo,
        });
        marcarOnline(true);

        if (response.status === 409 && !item.inspecaoId) {
          await registrarConflito(item.url, item.metodo, item.corpoJson ?? '[multipart]', response);
          await offlineDb.filaSaida.delete(item.id!);
          continue;
        }

        if (response.ok) {
          const retorno = item.inspecaoId && !ehEncerramentoInspecao(item) ? await response.json().catch(() => ({})) : {};
          const novaVersao: string | null | undefined = retorno.versaoResposta;
          await offlineDb.transaction('rw', offlineDb.filaSaida, offlineDb.inspecoesLocais, async () => {
            if (item.inspecaoId) {
              const chave = `${item.conta}|${item.inspecaoId}`;
              const local = await offlineDb.inspecoesLocais.get(chave);
              if (local) {
                if (ehCriacaoInspecao(item)) {
                  if (retorno.id !== item.inspecaoId || !retorno.versoesRespostas) throw new Error('A criação ainda não foi confirmada pelo servidor. Mantenha a inspeção neste dispositivo e tente novamente após a atualização.');
                  local.criadaLocalmente = false;
                  local.confirmado.inspecao.numeroDocumento = retorno.numeroDocumento;
                  const seguintes = (await offlineDb.filaSaida.toArray()).filter(i => i.inspecaoId === item.inspecaoId && i.conta === item.conta && i.id !== item.id);
                  for (const r of local.confirmado.respostas) {
                    r.versaoResposta = retorno.versoesRespostas[r.id];
                    if (local.rascunhos[r.id] && local.rascunhos[r.id].versaoResposta == null) local.rascunhos[r.id].versaoResposta = r.versaoResposta;
                    for (const acao of seguintes.filter(i => /^\/api\/inspecoes\/respostas\/([^/]+)/.exec(i.url)?.[1] === r.id))
                      await offlineDb.filaSaida.update(acao.id!, { versaoResposta: r.versaoResposta });
                  }
                }
                aplicarAcaoInspecao(item, local.confirmado, true);
                const respostaId = /^\/api\/inspecoes\/respostas\/([^/]+)/.exec(item.url)?.[1];
                const resposta = local.confirmado.respostas.find(r => r.id === respostaId);
                if (resposta && novaVersao != null) {
                  resposta.versaoResposta = novaVersao;
                  if (respostaId && local.rascunhos[respostaId]?.versaoResposta === item.versaoResposta) {
                    local.rascunhos[respostaId].versaoResposta = novaVersao;
                  }
                  const seguintes = (await offlineDb.filaSaida.toArray()).filter(i => i.id !== item.id && i.inspecaoId === item.inspecaoId && i.conta === item.conta
                    && /^\/api\/inspecoes\/respostas\/([^/]+)/.exec(i.url)?.[1] === respostaId && i.versaoResposta === item.versaoResposta);
                  for (const seguinte of seguintes) await offlineDb.filaSaida.update(seguinte.id!, { versaoResposta: novaVersao });
                }
                local.revisaoServidor += 1;
                await offlineDb.inspecoesLocais.put(local);
              }
            }
            await offlineDb.filaSaida.delete(item.id!);
          });
          continue;
        }

        // Sem login: todos os itens seguintes falhariam igual. Para aqui e espera a pessoa entrar de novo —
        // antes isso contava como erro definitivo e, na 5ª vez, a ação (e a foto) era descartada.
        if (response.status === 401) {
          await offlineDb.filaSaida.update(item.id!, { ultimoErro: 'Aguardando login para enviar.' });
          break;
        }

        // Temporário (servidor fora, cota do reconhecimento facial estourada, tempo esgotado): fica na fila
        // e tenta de novo na próxima sincronização, sem limite de tentativas.
        const novasTentativas = item.tentativas + 1;
        if (response.status === 408 || response.status === 429 || response.status >= 500) {
          if (grupo) bloqueadas.add(grupo);
          await offlineDb.filaSaida.update(item.id!, {
            tentativas: novasTentativas,
            ultimoErro: `${response.status} ${response.statusText}`,
          });
          continue;
        }

        // Recusa definitiva (ex.: rosto não reconhecido, validação): repetir o mesmo envio não adianta, e não
        // pode travar o resto da fila. Vira "conflito" na hora, com a mensagem do servidor e a ação guardada
        // inteira, para a pessoa tentar de novo depois de resolver a causa ou descartar.
        const texto = await lerCorpoErro(response);
        const { id: _id, ...acao } = item;
        if (grupo) bloqueadas.add(grupo);
        await offlineDb.conflitos.add({
          url: item.url,
          metodo: item.metodo,
          mensagem: response.status === 409 ? 'Este item mudou no servidor enquanto você estava offline. A versão do servidor foi mantida; confira os dados antes de tentar novamente.' : extrairMensagemErro(texto, response.status, response.statusText),
          dadosDescartados: item.corpoJson ?? '[multipart]',
          criadoEm: Date.now(),
          lido: false,
          item: acao,
        });
        await offlineDb.filaSaida.delete(item.id!);
      } catch (erro) {
        if (ehErroDeRede(erro)) {
          marcarOnline(false);
          break; // ainda offline de verdade — para e tenta tudo de novo na próxima reconexão
        }
        await offlineDb.filaSaida.update(item.id!, { ultimoErro: erro instanceof Error ? erro.message : 'Aguardando autenticação para enviar.' });
        break;
      }
    }
  } finally {
    sincronizando = false;
    notificarMudanca();
    if (novaSincronizacaoSolicitada) {
      novaSincronizacaoSolicitada = false;
      void sincronizarFilaSaida().catch(() => undefined);
    }
  }
}

// Ao carregar o app, se já houver itens de uma sessão anterior e a rede estiver de fato
// disponível, tenta esvaziar a fila imediatamente.
void sincronizarFilaSaida();
