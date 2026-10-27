import type { AtividadeGerada, GheGerado, RiscoGerado } from '../../lib/api';

const normalizar = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/\s+/g, ' ').trim();
export const nivelDe = (r: RiscoGerado) => r.probabilidade * r.severidade;
type Medida = RiscoGerado['medidas'][number];
const medidasAtivas = (r: RiscoGerado) => r.medidas.filter((m) => m.decisao !== 'Descartada');
const palavras = (t: string) => new Set(normalizar(t).replace(/[^a-z0-9 ]/g, ' ').split(' ').filter((p) => p.length > 3 && !/^\d+$/.test(p)));
const numeros = (t: string) => (t.match(/\d+(?:[.,]\d+)?/g) ?? []).sort().join(',');

// Duas medidas são "a mesma" quando têm a mesma fase e categoria, os mesmos números (7 m e 15 m mudam a regra) e
// ao menos 60% das palavras da frase mais curta em comum. A IA reescreve a medida a cada atividade e às vezes
// acrescenta um trecho, então só a redação não conta.
export function medidasEquivalentes(a: Medida, b: Medida): boolean {
  if (a.fase !== b.fase || (a.categoria ?? '') !== (b.categoria ?? '')) return false;
  if (numeros(a.texto) !== numeros(b.texto)) return false;
  const pa = palavras(a.texto);
  const pb = palavras(b.texto);
  const menor = Math.min(pa.size, pb.size);
  if (menor === 0) return normalizar(a.texto) === normalizar(b.texto);
  return [...pa].filter((p) => pb.has(p)).length / menor >= 0.6;
}

export interface DiferencaDoPadrao {
  nivel: { atividade: number; padrao: number } | null;
  aMais: Medida[];
  aMenos: Medida[];
  igual: boolean;
}

// O que a atividade tem de diferente do padrão: nível, medidas só dela ("a mais") e medidas só do padrão ("a menos").
export function compararComPadrao(padrao: RiscoGerado, r: RiscoGerado): DiferencaDoPadrao {
  const livres = medidasAtivas(padrao);
  const aMais: Medida[] = [];
  for (const m of medidasAtivas(r)) {
    const i = livres.findIndex((p) => medidasEquivalentes(p, m));
    if (i >= 0) livres.splice(i, 1);
    else aMais.push(m);
  }
  const nivel = nivelDe(r) !== nivelDe(padrao) ? { atividade: nivelDe(r), padrao: nivelDe(padrao) } : null;
  return { nivel, aMais, aMenos: livres, igual: !nivel && aMais.length === 0 && livres.length === 0 };
}

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

