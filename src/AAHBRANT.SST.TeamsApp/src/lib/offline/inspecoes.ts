import { TipoInspecao, type InspecaoDetalhe, type InspecaoItemResposta, type Inspecao,
  type NovaInspecao, type ChecklistModelo, type Obra, type Atividade, type Usuario } from '../api';
import { API_BASE_URL } from '../apiBase';
import { montarHeadersAuth } from '../authHeaders';
import { anexarDadosFoto, obterDadosFoto, pendenciasFoto } from '../dadosFoto';
import { contaOffline, identificarConta } from './conta';
import { offlineDb, blobParaGravar, type InspecaoLocal, type ItemFilaSaida, type RascunhoItemInspecao, type PreparacaoInspecoes } from './db';
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
  if (local?.criadaLocalmente) return recompor(local);
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

export async function obterPreparacaoInspecoes() {
  return offlineDb.preparacaoInspecoes.get(contaOffline());
}

export async function prepararInspecoesOffline() {
  const { conta, headers } = await identidade();
  async function ler<T>(path: string): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${path}`, { headers });
    if (!response.ok) throw new Error(`Não foi possível preparar as inspeções (${response.status}). Confira a conexão e o login.`);
    return response.json();
  }
  // Referências são baixadas da API autenticada, sem reaproveitar cache de outra conta.
  const [modelos, obras, atividades, usuarios] = await Promise.all([
    ler<ChecklistModelo[]>('/api/checklistmodelos'), ler<Obra[]>('/api/obras'),
    ler<Atividade[]>('/api/atividades'), ler<Usuario[]>('/api/usuarios'),
  ]);
  const anterior = await offlineDb.preparacaoInspecoes.get(conta);
  const genericos = modelos.filter(m => m.tipoInspecao !== TipoInspecao.Alojamento && m.tipoInspecao !== TipoInspecao.Veiculo);
  const detalhes: PreparacaoInspecoes['modelos'] = {};
  // Lotes pequenos: preparar a obra não deve disparar centenas de requisições de uma vez.
  for (let i = 0; i < genericos.length; i += 3) {
    const lote = genericos.slice(i, i + 3);
    const resultados = await Promise.allSettled(lote.map(m => ler<PreparacaoInspecoes['modelos'][string]>(`/api/checklistmodelos/${m.id}`)));
    resultados.forEach((resultado, indice) => {
      const modelo = lote[indice];
      if (resultado.status === 'fulfilled') detalhes[modelo.id] = resultado.value;
      else if (anterior?.modelos[modelo.id]?.checklistModelo.versao === modelo.versao) detalhes[modelo.id] = anterior.modelos[modelo.id];
    });
  }
  if (conta !== contaOffline()) throw new Error('A conta mudou durante a preparação. Reabra a tela com a conta que usará no campo.');
  const preparacao = { conta, modelos: detalhes, totalModelos: genericos.length, obras, atividades, usuarios, atualizadoEm: Date.now() };
  await offlineDb.preparacaoInspecoes.put(preparacao);
  return { preparacao, preparados: Object.keys(detalhes).length, total: genericos.length };
}

export async function criarInspecaoCampo(nova: NovaInspecao): Promise<{ id: string }> {
  const conta = contaOffline();
  const preparacao = await offlineDb.preparacaoInspecoes.get(conta);
  const modelo = preparacao?.modelos[nova.checklistModeloId];
  const obra = preparacao?.obras.find(o => o.id === nova.obraId);
  const usuario = preparacao?.usuarios.find(u => u.id === nova.responsavelUsuarioId);
  const atividade = preparacao?.atividades.find(a => a.id === nova.atividadeId);
  if (!modelo || !obra || !usuario) throw new Error('Prepare o modelo, a obra e os responsáveis com internet antes de criar esta inspeção no campo.');
  if (!modelo.itens.length) throw new Error('O modelo de checklist não tem itens disponíveis. Confira o cadastro com internet.');
  if (modelo.checklistModelo.tipoInspecao === TipoInspecao.Alojamento || modelo.checklistModelo.tipoInspecao === TipoInspecao.Veiculo)
    throw new Error('Alojamentos e veículos usam a criação específica de suas abas, com internet.');
  if (nova.atividadeId && (!atividade || atividade.obraId !== nova.obraId)) throw new Error('A atividade não pertence à obra preparada.');
  const id = crypto.randomUUID();
  const respostas = modelo.itens.map(item => ({ ...item, id: crypto.randomUUID(), inspecaoId: id, checklistModeloItemId: item.id,
    statusItem: null, observacao: null, temFoto: false, temFotoDepois: false }));
  const detalhe: InspecaoDetalhe = { inspecao: { id, ...nova, obraNome: obra.nome, responsavelUsuarioNome: usuario.nome,
    atividadeNome: atividade?.nome, tipoInspecao: modelo.checklistModelo.tipoInspecao, checklistModeloNome: modelo.checklistModelo.nome,
    checklistModeloVersao: modelo.checklistModelo.versao, status: 1, totalItens: respostas.length, itensRespondidos: 0, itensNaoConformes: 0 }, respostas };
  const registro: InspecaoLocal = { chave: chaveLocal(conta, id), conta, id, criadaLocalmente: true, detalhe,
    confirmado: structuredClone(detalhe), revisaoServidor: 0, rascunhos: {}, fotos: {}, atualizadoEm: Date.now() };
  const acao: ItemFilaSaida = { conta, inspecaoId: id, url: '/api/inspecoes', metodo: 'POST', isFormData: false,
    idempotencyKey: crypto.randomUUID(), criadoEm: Date.now(), tentativas: 0,
    corpoJson: JSON.stringify({ ...nova, idCliente: id, versaoChecklistEsperada: modelo.checklistModelo.versao,
      respostasCliente: respostas.map(r => ({ id: r.id, checklistModeloItemId: r.checklistModeloItemId })) }) };
  await offlineDb.transaction('rw', offlineDb.inspecoesLocais, offlineDb.filaSaida, async () => {
    await offlineDb.inspecoesLocais.add(registro);
    await offlineDb.filaSaida.add(acao);
  });
  notificarMudanca();
  void sincronizarFilaSaida().catch(() => undefined);
  return { id };
}

export type InspecaoCampo = Inspecao & { criacaoPendente?: boolean };
export async function mesclarInspecoesLocais(remotas: Inspecao[]): Promise<InspecaoCampo[]> {
  const conta = contaOffline();
  const locais = await offlineDb.inspecoesLocais.where('conta').equals(conta).toArray();
  const fila = await offlineDb.filaSaida.toArray();
  const mapa = new Map<string, InspecaoCampo>(remotas.map(i => [i.id, i]));
  for (const local of locais) {
    const pendente = fila.some(i => i.conta === conta && i.inspecaoId === local.id);
    if (local.criadaLocalmente || !navigator.onLine || !estaOnline() || (mapa.has(local.id) && pendente)) {
      const atual = await recompor(local);
      mapa.set(local.id, { ...atual.detalhe.inspecao, criacaoPendente: Boolean(local.criadaLocalmente) });
    }
  }
  return [...mapa.values()];
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
  const fotoGravavel = await blobParaGravar(foto);
  if (faltas.length) {
    const chave = chaveLocal(contaOffline(), id);
    await offlineDb.transaction('rw', offlineDb.inspecoesLocais, async () => {
      const local = await offlineDb.inspecoesLocais.get(chave);
      if (!local) throw new Error('Abra a inspeção antes de guardar a foto.');
      local.fotos[`/api/inspecoes/respostas/${respostaId}/${depois ? 'foto-depois' : 'foto'}`] = { blob: fotoGravavel, dados, rascunho: true };
      await offlineDb.inspecoesLocais.put(local);
    });
    notificarMudanca();
    return;
  }
  const form = new FormData();
  anexarDadosFoto(form, foto);
  await enfileirar(id, { url: `/api/inspecoes/respostas/${respostaId}/${depois ? 'foto-depois' : 'foto'}`, metodo: 'POST', isFormData: true,
    camposFormData: { metadados: String(form.get('metadados')) }, arquivosFormData: { foto: fotoGravavel } }, respostaId);
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
  const local = await offlineDb.inspecoesLocais.get(chaveLocal(conta, id));
  const itensPendentes = fila.map(i => /^\/api\/inspecoes\/respostas\/([^/]+)/.exec(i.url)?.[1]).filter((id): id is string => Boolean(id));
  return { pendentes: fila.length, criacaoPendente: Boolean(local?.criadaLocalmente), finalizacaoPendente: fila.some(ehEncerramentoInspecao), conflitos: conflitos.length, itensPendentes };
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
