import mapa from './mapaCorporal.json';

// Catálogo do mapa corporal (mapaCorporal.json, gerado por scripts/mapa-corporal/gerar_mapa_corporal.py).
// Código de região: "<vista>:<parte>[-lado]", ex.: "F:mao-esq" (F = frente, C = costas).

export interface RegiaoCorpo {
  codigo: string;
  rotulo: string;
  d: string;
}

export interface VistaCorpo {
  id: string;
  titulo: string;
  detalhes: string[];
  regioes: RegiaoCorpo[];
}

export const mapaCorporal = mapa as { viewBox: string; silhueta: string; vistas: VistaCorpo[] };

const rotulos = new Map(mapaCorporal.vistas.flatMap((v) => v.regioes.map((r) => [r.codigo, r.rotulo] as const)));
const ordem = new Map(mapaCorporal.vistas.flatMap((v) => v.regioes.map((r) => r.codigo)).map((c, i) => [c, i] as const));

export function rotuloRegiaoCorpo(codigo: string): string {
  return rotulos.get(codigo) ?? codigo;
}

/** Ordem do desenho (cabeça → pés, frente antes das costas), para listas e relatório ficarem estáveis. */
export function ordenarRegioesCorpo(codigos: string[]): string[] {
  return [...new Set(codigos)].sort((a, b) => (ordem.get(a) ?? 999) - (ordem.get(b) ?? 999));
}
