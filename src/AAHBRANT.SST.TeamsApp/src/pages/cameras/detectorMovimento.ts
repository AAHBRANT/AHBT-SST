// Compara dois quadros reduzidos em tons de cinza. Usado para só enviar foto à IA quando a cena
// muda: cena parada não gera custo. Cada quadro tem a luminância média subtraída antes da comparação,
// então mudança global de brilho (nuvem, auto-exposição, luz acendendo) não conta como movimento. Valores definidos para o piloto; ajustar com dados reais.
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

function media(v: Uint8Array): number {
  let soma = 0;
  for (let i = 0; i < v.length; i += 1) soma += v[i];
  return soma / v.length;
}

/**
 * Fração (0–1) de pixels cuja luminância (sem o brilho médio de cada quadro) mudou mais que o limiar.
 * Tamanhos diferentes ou vazios = 0: sem base de comparação não se gera custo de análise.
 */
export function fracaoAlterada(anterior: Uint8Array, atual: Uint8Array, limiarPixel = LIMIAR_PIXEL): number {
  if (anterior.length !== atual.length || atual.length === 0) return 0;
  const mediaAnterior = media(anterior);
  const mediaAtual = media(atual);
  let alterados = 0;
  for (let i = 0; i < atual.length; i += 1) {
    if (Math.abs(atual[i] - mediaAtual - (anterior[i] - mediaAnterior)) > limiarPixel) alterados += 1;
  }
  return alterados / atual.length;
}

export function houveMovimento(anterior: Uint8Array | null, atual: Uint8Array): boolean {
  if (!anterior) return true; // primeiro quadro: analisa uma vez para ter referência
  return fracaoAlterada(anterior, atual) >= LIMIAR_FRACAO;
}
