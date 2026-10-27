import type { AtividadeGerada, GheGerado, RiscoGerado } from '../../lib/api';

const normalizar = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/\s+/g, ' ').trim();
export const nivelDe = (r: RiscoGerado) => r.probabilidade * r.severidade;
export const assinaturaMedidas = (r: RiscoGerado) => r.medidas.filter((m) => m.decisao !== 'Descartada').map((m) => normalizar(m.texto)).sort().join('|');

export interface RefRisco { ai: number; ri: number; atividade: AtividadeGerada; r: RiscoGerado }
export interface GrupoPerigo { chave: string; nome: string; tipo: string; itens: RefRisco[]; padrao: RefRisco }

// Agrupa os riscos do GHE pelo perigo (ignora acento, caixa e espaços). O "padrão" é o primeiro risco com o
// P×S mais frequente do grupo; as atividades que diferem dele em nível ou em medidas ficam marcadas.
export function agruparPorPerigo(gh: GheGerado): GrupoPerigo[] {
  const mapa = new Map<string, RefRisco[]>();
  gh.atividades.forEach((atividade, ai) => atividade.riscos.forEach((r, ri) => {
    if (r.excluido) return;
    const k = normalizar(r.perigo);
    const lista = mapa.get(k) ?? [];
    lista.push({ ai, ri, atividade, r });
    mapa.set(k, lista);
  }));
  return [...mapa.entries()].map(([chave, itens]) => {
    const freq = new Map<number, number>();
    itens.forEach((i) => freq.set(nivelDe(i.r), (freq.get(nivelDe(i.r)) ?? 0) + 1));
    const nivelPadrao = [...freq.entries()].sort((a, b) => b[1] - a[1] || b[0] - a[0])[0][0];
    const padrao = itens.find((i) => nivelDe(i.r) === nivelPadrao)!;
    return { chave, nome: padrao.r.perigo, tipo: padrao.r.tipo, itens, padrao };
  }).sort((a, b) => Math.max(...b.itens.map((i) => nivelDe(i.r))) - Math.max(...a.itens.map((i) => nivelDe(i.r))) || b.itens.length - a.itens.length);
}

