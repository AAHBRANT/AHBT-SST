// Valores padrão de formulário (spec 2026-09-11): centraliza regras que antes eram duplicadas por
// arquivo — ex.: `const hoje = () => new Date().toISOString().slice(0, 10)` em InstalacoesTab.tsx.
// A Onda F do spec audita e migra os usos duplicados existentes para cá.

/** Data de hoje em yyyy-MM-dd, o formato que a API e o CampoData esperam. */
export function hoje(): string {
  // toLocaleDateString('sv-SE') usa o formato yyyy-MM-dd (padrão sueco) já no fuso local do
  // navegador, evitando o bug de toISOString() (que converte para UTC e vira o dia errado à
  // noite no fuso do Brasil, UTC-3).
  return new Date().toLocaleDateString('sv-SE');
}

/**
 * Id do único item de `itens`, ou '' se a lista estiver vazia ou tiver mais de um item.
 * Regra "Select com uma única opção possível no contexto atual vem pré-selecionado" (spec §2).
 */
export function valorUnico<T>(itens: T[], id: (item: T) => string): string {
  return itens.length === 1 ? id(itens[0]) : '';
}
