// Compara dois quadros reduzidos em tons de cinza. Usado para só enviar foto à IA quando a cena
// muda: cena parada não gera custo. Valores definidos para o piloto; ajustar com dados reais.
export const LARGURA_AMOSTRA = 64;
export const ALTURA_AMOSTRA = 36;
export const LIMIAR_PIXEL = 25; // diferença mínima de brilho (0–255) para um pixel contar como alterado
export const LIMIAR_FRACAO = 0.02; // 2% dos pixels alterados = movimento

/** Converte RGBA (ImageData.data) em luminância, um byte por pixel. */
export function paraCinza(rgba: Uint8ClampedArray): Uint8Array {
  const cinza = new Uint8Array(rgba.length / 4);
  for (let i = 0, p = 0; i < rgba.length; i += 4, p += 1) {
    cinza[p] = (rgba[i] * 299 + rgba[i + 1] * 587 + rgba[i + 2] * 114) / 1000;
  }
  return cinza;
}

/** Fração (0–1) de pixels cuja luminância mudou mais que o limiar. Tamanhos diferentes = 1. */
export function fracaoAlterada(anterior: Uint8Array, atual: Uint8Array, limiarPixel = LIMIAR_PIXEL): number {
  if (anterior.length !== atual.length || atual.length === 0) return 1;
  let alterados = 0;
  for (let i = 0; i < atual.length; i += 1) {
    if (Math.abs(atual[i] - anterior[i]) > limiarPixel) alterados += 1;
  }
  return alterados / atual.length;
}

export function houveMovimento(anterior: Uint8Array | null, atual: Uint8Array): boolean {
  if (!anterior) return true; // primeiro quadro: analisa uma vez para ter referência
  return fracaoAlterada(anterior, atual) >= LIMIAR_FRACAO;
}
