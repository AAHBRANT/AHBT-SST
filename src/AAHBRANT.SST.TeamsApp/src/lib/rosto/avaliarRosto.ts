// Regras do detector de rosto da câmera facial. É só uma ajuda ao operador: quem decide a qualidade e
// a identidade continua sendo o Azure Face no servidor. Mantido sem dependência de DOM/React para
// poder ser testado isolado.

// Caixa do rosto em pixels do próprio vídeo (a mesma base da foto capturada, que sai do vídeo sem
// reduzir — ver capturarFoto em useCapturaFoto).
export interface CaixaRosto {
  x: number;
  y: number;
  largura: number;
  altura: number;
}

// Brilho médio (0 a 255) do rosto e do fundo ao redor.
export interface MedidaLuz {
  rosto: number;
  fundo: number;
}

export type EstadoRosto = 'nenhum' | 'varios' | 'fora' | 'longe' | 'escuro' | 'contraluz' | 'estourado' | 'ok';

export interface AvaliacaoRosto {
  estado: EstadoRosto;
  mensagem: string;
  tom: 'ok' | 'atencao' | 'alerta';
  // Tudo certo (enquadramento + luz): libera a captura automática.
  liberaCaptura: boolean;
  // Só o enquadramento: um único rosto, inteiro dentro do oval e grande o bastante. É o que libera o
  // botão Capturar — a luz não bloqueia, para não travar quem trabalha em local difícil.
  enquadrado: boolean;
}

// Oval-guia desenhado sobre o vídeo (DialogoCameraFoto): altura = 88% da altura do vídeo, proporção
// largura/altura de 3/4, centralizado. Fica aqui para o desenho e a verificação nunca divergirem.
export const OVAL_ALTURA_FRACAO = 0.88;
export const OVAL_PROPORCAO = 3 / 4;

// true se a caixa do rosto cabe INTEIRA dentro do oval. Como a elipse é convexa, basta testar os quatro
// cantos da caixa. O quadro do vídeo e o oval têm a mesma proporção do vídeo, então a conta é em pixels.
export function rostoDentroDoOval(c: CaixaRosto, larguraVideo: number, alturaVideo: number): boolean {
  const semiY = (OVAL_ALTURA_FRACAO * alturaVideo) / 2;
  const semiX = semiY * OVAL_PROPORCAO;
  const cx = larguraVideo / 2;
  const cy = alturaVideo / 2;
  const cantos: Array<[number, number]> = [
    [c.x, c.y], [c.x + c.largura, c.y], [c.x, c.y + c.altura], [c.x + c.largura, c.y + c.altura],
  ];
  return cantos.every(([px, py]) => ((px - cx) / semiX) ** 2 + ((py - cy) / semiY) ** 2 <= 1);
}

// Espelha ValidarQualidadeParaCadastroAsync no servidor (LadoMinimoRostoPx): abaixo disso a foto de
// cadastro é recusada.
export const LADO_MINIMO_ROSTO_PX = 200;

// Limites de luz (escala 0 a 255). Valores de partida: calibrar com fotos reais de canteiro.
export const LUZ_ROSTO_MINIMA = 70;
export const LUZ_ROSTO_MAXIMA = 225;
export const LUZ_CONTRALUZ_DIFERENCA = 55;
export const LUZ_CONTRALUZ_ROSTO_ATE = 120;

const bloqueia = (estado: EstadoRosto, mensagem: string, tom: 'atencao' | 'alerta', enquadrado = false): AvaliacaoRosto => ({
  estado,
  mensagem,
  tom,
  liberaCaptura: false,
  enquadrado,
});

export function avaliarRostos(
  caixas: readonly CaixaRosto[],
  larguraVideo: number,
  alturaVideo: number,
  luz?: MedidaLuz | null,
): AvaliacaoRosto {
  if (caixas.length === 0) return bloqueia('nenhum', 'Nenhum rosto encontrado. Aponte a câmera para o rosto.', 'alerta');
  if (caixas.length > 1) return bloqueia('varios', 'Mais de um rosto. Fique só o trabalhador no quadro.', 'alerta');

  const c = caixas[0];

  const cortado = c.x < 0 || c.y < 0 || c.x + c.largura > larguraVideo || c.y + c.altura > alturaVideo;
  if (cortado) return bloqueia('fora', 'O rosto está cortado. Afaste-se um pouco e centralize.', 'atencao');

  if (!rostoDentroDoOval(c, larguraVideo, alturaVideo))
    return bloqueia('fora', 'Deixe o rosto inteiro dentro do círculo. Centralize e afaste um pouco.', 'atencao');

  if (Math.min(c.largura, c.altura) < LADO_MINIMO_ROSTO_PX)
    return bloqueia('longe', 'Aproxime o rosto da câmera, sem sair do círculo.', 'atencao');

  // A partir daqui o rosto está inteiro no círculo: o botão Capturar já pode liberar. A luz só orienta.
  if (luz) {
    if (luz.rosto < LUZ_ROSTO_MINIMA) return bloqueia('escuro', 'Pouca luz no rosto. Vá para um lugar mais iluminado.', 'atencao', true);
    if (luz.rosto > LUZ_ROSTO_MAXIMA) return bloqueia('estourado', 'Luz forte demais no rosto. Evite o sol direto na cara.', 'atencao', true);
    if (luz.fundo - luz.rosto > LUZ_CONTRALUZ_DIFERENCA && luz.rosto < LUZ_CONTRALUZ_ROSTO_ATE)
      return bloqueia('contraluz', 'Contraluz: a luz está atrás da pessoa. Vire de frente para a luz.', 'atencao', true);
  }

  return { estado: 'ok', mensagem: 'Pronto. Pode capturar.', tom: 'ok', liberaCaptura: true, enquadrado: true };
}
