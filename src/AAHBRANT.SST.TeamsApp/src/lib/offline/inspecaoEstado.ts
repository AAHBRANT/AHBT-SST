import type { InspecaoDetalhe } from '../api';
import type { ItemFilaSaida } from './db';

export function ehEncerramentoInspecao(item: ItemFilaSaida) {
  return /^\/api\/inspecoes\/[^/]+\/encerrar$/.test(item.url);
}

export function ehCriacaoInspecao(item: ItemFilaSaida) {
  return Boolean(item.inspecaoId && item.url === '/api/inspecoes' && item.metodo === 'POST');
}

export function aplicarAcaoInspecao(item: ItemFilaSaida, detalhe: InspecaoDetalhe, confirmado = false) {
  if (ehEncerramentoInspecao(item)) {
    if (confirmado) detalhe.inspecao.status = 2;
    return;
  }
  const respostaId = /^\/api\/inspecoes\/respostas\/([^/]+)/.exec(item.url)?.[1];
  const resposta = detalhe.respostas.find(r => r.id === respostaId);
  if (!resposta) return;
  if (item.isFormData) {
    const depois = item.url.endsWith('/foto-depois');
    const dados = item.camposFormData?.metadados ? JSON.parse(item.camposFormData.metadados) : undefined;
    if (depois) { resposta.temFotoDepois = true; resposta.dadosFotoDepois = dados; }
    else { resposta.temFoto = true; resposta.dadosFoto = dados; }
  } else if (item.corpoJson) {
    const { descricaoPersonalizada, ...campos } = JSON.parse(item.corpoJson);
    Object.assign(resposta, campos);
    if (descricaoPersonalizada != null) resposta.descricao = descricaoPersonalizada;
  }
  detalhe.inspecao.itensRespondidos = detalhe.respostas.filter(r => r.statusItem != null).length;
  detalhe.inspecao.itensNaoConformes = detalhe.respostas.filter(r => r.statusItem === 2).length;
}
