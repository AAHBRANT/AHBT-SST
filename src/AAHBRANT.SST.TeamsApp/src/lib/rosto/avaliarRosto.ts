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
  liberaCaptura: boolean;
}

// Espelha ValidarQualidadeParaCadastroAsync no servidor (LadoMinimoRostoPx): abaixo disso a foto de
// cadastro é recusada.
export const LADO_MINIMO_ROSTO_PX = 200;

// Limites de luz (escala 0 a 255). Valores de partida: calibrar com fotos reais de canteiro.
export const LUZ_ROSTO_MINIMA = 70;
export const LUZ_ROSTO_MAXIMA = 225;
export const LUZ_CONTRALUZ_DIFERENCA = 55;
export const LUZ_CONTRALUZ_ROSTO_ATE = 120;

// Quanto o centro do rosto pode se afastar do centro do quadro, em fração da largura/altura.
const TOLERANCIA_CENTRO_X = 0.22;
const TOLERANCIA_CENTRO_Y = 0.25;

const bloqueia = (estado: EstadoRosto, mensagem: string, tom: 'atencao' | 'alerta'): AvaliacaoRosto => ({
  estado,
  mensagem,
  tom,
  liberaCaptura: false,
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

  if (Math.min(c.largura, c.altura) < LADO_MINIMO_ROSTO_PX)
    return bloqueia('longe', 'Aproxime o rosto da câmera.', 'atencao');

  const desvioX = Math.abs(c.x + c.largura / 2 - larguraVideo / 2) / larguraVideo;
  const desvioY = Math.abs(c.y + c.altura / 2 - alturaVideo / 2) / alturaVideo;
  if (desvioX > TOLERANCIA_CENTRO_X || desvioY > TOLERANCIA_CENTRO_Y)
    return bloqueia('fora', 'Centralize o rosto dentro do oval.', 'atencao');

  if (luz) {
    if (luz.rosto < LUZ_ROSTO_MINIMA) return bloqueia('escuro', 'Pouca luz no rosto. Vá para um lugar mais iluminado.', 'atencao');
    if (luz.rosto > LUZ_ROSTO_MAXIMA) return bloqueia('estourado', 'Luz forte demais no rosto. Evite o sol direto na cara.', 'atencao');
    if (luz.fundo - luz.rosto > LUZ_CONTRALUZ_DIFERENCA && luz.rosto < LUZ_CONTRALUZ_ROSTO_ATE)
      return bloqueia('contraluz', 'Contraluz: a luz está atrás da pessoa. Vire de frente para a luz.', 'atencao');
  }

  return { estado: 'ok', mensagem: 'Pronto. Pode capturar.', tom: 'ok', liberaCaptura: true };
}
