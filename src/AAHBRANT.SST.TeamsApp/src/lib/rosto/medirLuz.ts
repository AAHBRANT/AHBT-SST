import type { CaixaRosto, MedidaLuz } from './avaliarRosto';

// Amostra pequena do quadro: basta para estimar o brilho e mantém a medição barata (roda ~8x/s).
const LARGURA_AMOSTRA = 64;

let canvas: HTMLCanvasElement | null = null;

// Brilho médio (luma 0 a 255) do rosto e do fundo. O fundo é tudo que está fora da caixa do rosto, o
// que permite reconhecer contraluz (fundo bem mais claro que o rosto).
export function medirLuz(video: HTMLVideoElement, caixa: CaixaRosto): MedidaLuz | null {
  const larguraVideo = video.videoWidth;
  const alturaVideo = video.videoHeight;
  if (!larguraVideo || !alturaVideo) return null;

  const escala = LARGURA_AMOSTRA / larguraVideo;
  const largura = LARGURA_AMOSTRA;
  const altura = Math.max(1, Math.round(alturaVideo * escala));

  canvas ??= document.createElement('canvas');
  canvas.width = largura;
  canvas.height = altura;
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) return null;
  ctx.drawImage(video, 0, 0, largura, altura);
  const { data } = ctx.getImageData(0, 0, largura, altura);

  const x0 = caixa.x * escala;
  const y0 = caixa.y * escala;
  const x1 = (caixa.x + caixa.largura) * escala;
  const y1 = (caixa.y + caixa.altura) * escala;

  let somaRosto = 0;
  let nRosto = 0;
  let somaFundo = 0;
  let nFundo = 0;
  for (let y = 0; y < altura; y++) {
    for (let x = 0; x < largura; x++) {
      const i = (y * largura + x) * 4;
      const luma = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
      if (x >= x0 && x < x1 && y >= y0 && y < y1) {
        somaRosto += luma;
        nRosto++;
      } else {
        somaFundo += luma;
        nFundo++;
      }
    }
  }
  if (nRosto === 0) return null;
  // Se o rosto ocupa o quadro todo não há fundo para comparar: usa o do próprio rosto (sem contraluz).
  const rosto = somaRosto / nRosto;
  return { rosto, fundo: nFundo > 0 ? somaFundo / nFundo : rosto };
}
