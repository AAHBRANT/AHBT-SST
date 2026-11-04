// Moldura 3×4 em pé da câmera facial (pedido do usuário, 04/11), no lugar do oval: padrão de foto de
// documento (ISO/IEC 19794-5 / ICAO). O cadastro só libera com o rosto inteiro dentro dela; na
// assinatura ela é guia. Nos dois casos a foto é salva em 3×4 em pé, centralizada no rosto
// (recorteNoRosto), em qualquer orientação do aparelho. No tablet deitado (Positivo Vision 10 no
// teclado) a moldura fica no centro, alinhada com a câmera do meio da borda.

export const PROPORCAO_MOLDURA = 3 / 4; // largura / altura
// A moldura usa 88% do lado que limita (mesma altura do antigo oval), com folga nas bordas.
export const OCUPACAO_MOLDURA = 0.88;

export interface RetanguloMoldura {
  x: number;
  y: number;
  largura: number;
  altura: number;
}

// Retângulo da moldura em pixels do vídeo (mesma base da foto capturada), centralizado. Funciona com
// vídeo deitado (tablet, notebook) ou em pé (celular).
export function calcularMoldura(larguraVideo: number, alturaVideo: number): RetanguloMoldura {
  let altura = alturaVideo * OCUPACAO_MOLDURA;
  let largura = altura * PROPORCAO_MOLDURA;
  if (largura > larguraVideo * OCUPACAO_MOLDURA) {
    largura = larguraVideo * OCUPACAO_MOLDURA;
    altura = largura / PROPORCAO_MOLDURA;
  }
  return {
    x: Math.round((larguraVideo - largura) / 2),
    y: Math.round((alturaVideo - altura) / 2),
    largura: Math.round(largura),
    altura: Math.round(altura),
  };
}

// Rosto inteiro dentro da moldura (2% de tolerância para o detector não piscar na borda).
export function rostoDentroDaMoldura(
  rosto: { x: number; y: number; largura: number; altura: number },
  moldura: RetanguloMoldura,
): boolean {
  const folga = moldura.largura * 0.02;
  return (
    rosto.x >= moldura.x - folga &&
    rosto.y >= moldura.y - folga &&
    rosto.x + rosto.largura <= moldura.x + moldura.largura + folga &&
    rosto.y + rosto.altura <= moldura.y + moldura.altura + folga
  );
}

// Moldura em porcentagem do vídeo, para desenhar sobre ele.
export function molduraEmPorcentagem(larguraVideo: number, alturaVideo: number) {
  const m = calcularMoldura(larguraVideo, alturaVideo);
  return {
    left: `${(m.x / larguraVideo) * 100}%`,
    top: `${(m.y / alturaVideo) * 100}%`,
    width: `${(m.largura / larguraVideo) * 100}%`,
    height: `${(m.altura / alturaVideo) * 100}%`,
  };
}

// Onde fica o centro do rosto na foto 3×4: um pouco acima do meio, como em foto de documento
// (sobra espaço para pescoço e ombros embaixo).
const CENTRO_ROSTO_NA_ALTURA = 0.42;

// Recorte 3×4 em pé do tamanho da moldura, centralizado no rosto detectado (caixa em fração 0–1 do
// vídeo). Encosta na borda da imagem quando o rosto está perto dela — o rosto continua inteiro dentro.
// Sem rosto detectado (detector não carregou), usa a moldura do centro.
export function recorteNoRosto(
  larguraVideo: number,
  alturaVideo: number,
  rosto: { x: number; y: number; largura: number; altura: number } | null,
): RetanguloMoldura {
  const m = calcularMoldura(larguraVideo, alturaVideo);
  if (!rosto) return m;
  const cx = (rosto.x + rosto.largura / 2) * larguraVideo;
  const cy = (rosto.y + rosto.altura / 2) * alturaVideo;
  const limitar = (v: number, max: number) => Math.round(Math.min(Math.max(v, 0), max));
  return {
    x: limitar(cx - m.largura / 2, larguraVideo - m.largura),
    y: limitar(cy - m.altura * CENTRO_ROSTO_NA_ALTURA, alturaVideo - m.altura),
    largura: m.largura,
    altura: m.altura,
  };
}
