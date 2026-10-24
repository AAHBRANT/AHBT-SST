import type { InspecaoDetalhe, InspecaoItemResposta } from '../api';
import { API_BASE_URL } from '../apiBase';
import { montarHeadersAuth } from '../authHeaders';
import { anexarDadosFoto, obterDadosFoto, pendenciasFoto } from '../dadosFoto';
import { contaOffline, identificarConta } from './conta';
import { offlineDb, type InspecaoLocal, type ItemFilaSaida, type RascunhoItemInspecao } from './db';
import { aplicarAcaoInspecao as aplicar, ehEncerramentoInspecao } from './inspecaoEstado';
import { estaOnline, notificarMudanca, sincronizarFilaSaida } from './syncEngine';

async function identidade() {
  const headers = await montarHeadersAuth();
  return { headers, conta: identificarConta(headers) ?? contaOffline() };
}
const chaveLocal = (conta: string, id: string) => `${conta}|${id}`;
async function recompor(local: InspecaoLocal): Promise<InspecaoLocal> {
  local.detalhe = structuredClone(local.confirmado);
  const itens = (await offlineDb.filaSaida.orderBy('criadoEm').toArray()).filter(i => i.inspecaoId === local.id && i.conta === local.conta);
  for (const item of itens) aplicar(item, local.detalhe);
  for (const [url, foto] of Object.entries(local.fotos)) {
    if (foto.rascunho) aplicar({ url, metodo: 'POST', isFormData: true, camposFormData: { metadados: JSON.stringify(foto.dados) },
      criadoEm: 0, tentativas: 0, idempotencyKey: '' }, local.detalhe);
  }
  return local;
}

export async function obterInspecaoLocal(id: string): Promise<InspecaoLocal> {
  const { conta, headers } = await identidade();
  const chave = chaveLocal(conta, id);
  const local = await offlineDb.inspecoesLocais.get(chave);
  if (!navigator.onLine) {
    if (local) return recompor(local);
    throw new Error('Esta inspeção ainda não está disponível neste dispositivo. Abra-a com internet antes de ir a campo.');
  }
  let detalhe: InspecaoDetalhe;
  try {
    const response = await fetch(`${API_BASE_URL}/api/inspecoes/${id}`, { headers });
    if (!response.ok) {
      const body = await response.json().catch(() => ({}));
      throw new Error(body.erro ?? `Não foi possível carregar a inspeção (${response.status}).`);
    }
    detalhe = await response.json();
  } catch (erro) {
    if (erro instanceof TypeError && local) return recompor(local);
    throw erro;
  }
  return offlineDb.transaction('rw', offlineDb.inspecoesLocais, offlineDb.filaSaida, async () => {
    const atual = await offlineDb.inspecoesLocais.get(chave);
    // Uma confirmação ocorrida durante o GET não pode ser sobrescrita pela resposta antiga do GET.
    const confirmado = atual && atual.revisaoServidor !== local?.revisaoServidor ? atual.confirmado : structuredClone(detalhe);
    const registro: InspecaoLocal = { chave, conta, id, detalhe, confirmado, revisaoServidor: atual?.revisaoServidor ?? 0,
      rascunhos: atual?.rascunhos ?? {}, fotos: atual?.fotos ?? {}, atualizadoEm: Date.now() };
    await recompor(registro);
    await offlineDb.inspecoesLocais.put(registro);
    return registro;
  });
}

export async function salvarRascunhoInspecao(id: string, respostaId: string, campos: Partial<RascunhoItemInspecao>) {
  // Não precisa renovar um token só para salvar o preenchimento no próprio aparelho.
  const chave = chaveLocal(contaOffline(), id);
  await offlineDb.transaction('rw', offlineDb.inspecoesLocais, offlineDb.filaSaida, async () => {
    const local = await offlineDb.inspecoesLocais.get(chave);
    const resposta = local?.detalhe.respostas.find(r => r.id === respostaId);
    if (!local || !resposta) throw new Error('Abra a inspeção antes de preencher o checklist.');
    if (local.confirmado.inspecao.status !== 1 || (await offlineDb.filaSaida.toArray()).some(i => i.conta === local.conta && i.inspecaoId === id && ehEncerramentoInspecao(i)))
      throw new Error('A inspeção está concluída ou aguardando finalização.');
    const inicial = edicaoDaResposta(resposta);
    local.rascunhos[respostaId] = { ...inicial, ...local.rascunhos[respostaId], ...campos };
    await offlineDb.inspecoesLocais.put(local);
  });
}

export function edicaoDaResposta(r: InspecaoItemResposta): RascunhoItemInspecao {
  return { versaoResposta: r.versaoResposta, descricao: r.descricao, statusItem: r.statusItem == null ? '' : String(r.statusItem), observacao: r.observacao ?? '',
    local: r.local ?? '', planoDeAcao: r.planoDeAcao ?? '', responsavelUsuarioId: r.responsavelUsuarioId ?? '', prazo: r.prazo?.slice(0, 10) ?? '' };
}

async function enfileirar(id: string, item: Omit<ItemFilaSaida, 'conta' | 'inspecaoId' | 'criadoEm' | 'tentativas' | 'idempotencyKey'>, respostaId?: string) {
  const conta = contaOffline();
  const chave = chaveLocal(conta, id);
  await offlineDb.transaction('rw', offlineDb.inspecoesLocais, offlineDb.filaSaida, offlineDb.conflitos, async () => {
    const local = await offlineDb.inspecoesLocais.get(chave);
    if (!local) throw new Error('A inspeção não está salva neste dispositivo.');
    const fila = await offlineDb.filaSaida.toArray();
    const recusados = (await offlineDb.conflitos.toArray()).some(c => !c.lido && c.item?.conta === conta && c.item.inspecaoId === id);
    if (fila.some(i => i.inspecaoId === id && i.conta === conta && ehEncerramentoInspecao(i)) && (!recusados || item.url.endsWith('/encerrar'))) {
      throw new Error('A finalização desta inspeção já está pendente. Aguarde a sincronização.');
    }
    if (local.confirmado.inspecao.status !== 1) throw new Error('Esta inspeção já foi concluída.');
    if (item.url.endsWith('/encerrar') && (Object.keys(local.rascunhos).length || Object.values(local.fotos).some(f => f.rascunho)))
      throw new Error('Confira os itens em rascunho e as fotos com dados pendentes antes de finalizar.');
    await recompor(local);
    const acao: ItemFilaSaida = { ...item, conta, inspecaoId: id, criadoEm: Date.now(), tentativas: 0, idempotencyKey: crypto.randomUUID(),
      versaoResposta: respostaId && !item.isFormData && local.rascunhos[respostaId]
        ? local.rascunhos[respostaId].versaoResposta : local.confirmado.respostas.find(r => r.id === respostaId)?.versaoResposta };
    aplicar(acao, local.detalhe);
    if (respostaId && !item.isFormData) delete local.rascunhos[respostaId];
    if (item.isFormData && item.arquivosFormData?.foto) {
      local.fotos[item.url] = { blob: item.arquivosFormData.foto,
        dados: item.camposFormData?.metadados ? JSON.parse(item.camposFormData.metadados) : undefined };
    }
    local.atualizadoEm = Date.now();
    await offlineDb.inspecoesLocais.put(local);
    await offlineDb.filaSaida.add(acao);
  });
  notificarMudanca();
  void sincronizarFilaSaida().catch(() => undefined);
}

export async function salvarRespostaInspecao(id: string, respostaId: string, edicao: RascunhoItemInspecao) {
  if (!edicao.statusItem) throw new Error('Selecione o status do ponto verificado antes de salvar.');
  await enfileirar(id, { url: `/api/inspecoes/respostas/${respostaId}`, metodo: 'POST', isFormData: false,
    corpoJson: JSON.stringify({ statusItem: Number(edicao.statusItem), observacao: edicao.observacao || null,
      responsavelUsuarioId: edicao.responsavelUsuarioId || null, prazo: edicao.prazo || null,
      descricaoPersonalizada: edicao.descricao || null, local: edicao.local || null, planoDeAcao: edicao.planoDeAcao || null }) }, respostaId);
}

export async function salvarFotoInspecao(id: string, respostaId: string, foto: File, depois = false) {
  const dados = obterDadosFoto(foto);
  if (!dados) throw new Error('A foto precisa dos dados de captura e da obra. Capture ou selecione novamente pelo aplicativo.');
  const faltas = pendenciasFoto(dados);
  if (faltas.length) {
    const chave = chaveLocal(contaOffline(), id);
    await offlineDb.transaction('rw', offlineDb.inspecoesLocais, async () => {
      const local = await offlineDb.inspecoesLocais.get(chave);
      if (!local) throw new Error('Abra a inspeção antes de guardar a foto.');
      local.fotos[`/api/inspecoes/respostas/${respostaId}/${depois ? 'foto-depois' : 'foto'}`] = { blob: foto, dados, rascunho: true };
      await offlineDb.inspecoesLocais.put(local);
    });
    notificarMudanca();
    return;
  }
  const form = new FormData();
  anexarDadosFoto(form, foto);
  await enfileirar(id, { url: `/api/inspecoes/respostas/${respostaId}/${depois ? 'foto-depois' : 'foto'}`, metodo: 'POST', isFormData: true,
    camposFormData: { metadados: String(form.get('metadados')) }, arquivosFormData: { foto } }, respostaId);
}

export async function finalizarInspecaoLocal(id: string) {
  const local = await offlineDb.inspecoesLocais.get(chaveLocal(contaOffline(), id));
  if (local && Object.keys(local.rascunhos).length) throw new Error('Salve os itens em rascunho antes de finalizar a inspeção.');
  if (local && Object.values(local.fotos).some(f => f.rascunho)) throw new Error('Há fotos com dados de captura pendentes. Confira as evidências antes de finalizar.');
  await enfileirar(id, { url: `/api/inspecoes/${id}/encerrar`, metodo: 'POST', isFormData: false });
}

export async function estadoSyncInspecao(id: string) {
  const conta = contaOffline();
  const fila = (await offlineDb.filaSaida.toArray()).filter(i => i.inspecaoId === id && i.conta === conta);
  const conflitos = (await offlineDb.conflitos.toArray()).filter(c => !c.lido && c.item?.inspecaoId === id && c.item.conta === conta);
  const itensPendentes = fila.map(i => /^\/api\/inspecoes\/respostas\/([^/]+)/.exec(i.url)?.[1]).filter((id): id is string => Boolean(id));
  return { pendentes: fila.length, finalizacaoPendente: fila.some(ehEncerramentoInspecao), conflitos: conflitos.length, itensPendentes };
}

export async function fotoLocalInspecao(id: string, respostaId: string, depois = false) {
  const local = await offlineDb.inspecoesLocais.get(chaveLocal(contaOffline(), id));
  return local?.fotos[`/api/inspecoes/respostas/${respostaId}/${depois ? 'foto-depois' : 'foto'}`]?.blob;
}

export async function baixarFotoInspecaoLocal(id: string, respostaId: string, depois = false) {
  const conta = contaOffline();
  const chave = chaveLocal(conta, id);
  const url = `/api/inspecoes/respostas/${respostaId}/${depois ? 'foto-depois' : 'foto'}`;
  const local = await offlineDb.inspecoesLocais.get(chave);
  const pendente = (await offlineDb.filaSaida.toArray()).some(i => i.inspecaoId === id && i.conta === conta && i.url === url);
  if (local?.fotos[url] && (!navigator.onLine || !estaOnline() || pendente || local.fotos[url].rascunho)) return local.fotos[url].blob;
  try {
    const response = await fetch(`${API_BASE_URL}${url}`, { headers: await montarHeadersAuth() });
    if (!response.ok) throw new Error(`Não foi possível carregar a foto (${response.status}).`);
    const blob = await response.blob();
    await offlineDb.transaction('rw', offlineDb.inspecoesLocais, offlineDb.filaSaida, async () => {
      const atual = await offlineDb.inspecoesLocais.get(chave);
      if (atual) {
        const pendenteAgora = (await offlineDb.filaSaida.toArray()).some(i => i.conta === conta && i.inspecaoId === id && i.url === url);
        if (pendenteAgora || atual.fotos[url]?.rascunho || atual.revisaoServidor !== local?.revisaoServidor) return;
        atual.fotos[url] = { blob };
        await offlineDb.inspecoesLocais.put(atual);
      }
    });
    return blob;
  } catch (erro) {
    if (erro instanceof TypeError && local?.fotos[url]) return local.fotos[url].blob;
    throw erro;
  }
}
