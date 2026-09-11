// Valores padrão de formulário (spec 2026-09-11): centraliza regras que antes eram duplicadas por
// arquivo — ex.: `const hoje = () => new Date().toISOString().slice(0, 10)` em InstalacoesTab.tsx.
// A Onda F do spec audita e migra os usos duplicados existentes para cá.

/** Data de hoje em yyyy-MM-dd, o formato que a API e o CampoData esperam. */
export function hoje(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Id do único item de `itens`, ou '' se a lista estiver vazia ou tiver mais de um item.
 * Regra "Select com uma única opção possível no contexto atual vem pré-selecionado" (spec §2).
 */
export function valorUnico<T>(itens: T[], id: (item: T) => string): string {
  return itens.length === 1 ? id(itens[0]) : '';
}
