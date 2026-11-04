import { calcularMoldura, rostoDentroDaMoldura } from './moldura';

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
  // Só o enquadramento: um único rosto, inteiro dentro da moldura e grande o bastante. É o que libera o
  // botão Capturar — a luz não bloqueia, para não travar quem trabalha em local difícil.
  enquadrado: boolean;
}

// Moldura 3×4 em pé (04/11) no lugar do oval: desenho, verificação e recorte usam a mesma conta
// (lib/rosto/moldura.ts), para nunca divergirem.
export function rostoDentroDoQuadro(c: CaixaRosto, larguraVideo: number, alturaVideo: number): boolean {
  return rostoDentroDaMoldura(c, calcularMoldura(larguraVideo, alturaVideo));
}

// Espelha ValidarQualidadeParaCadastroAsync no servidor (LadoMinimoRostoPx): abaixo disso a foto de
// cadastro é recusada.
export const LADO_MINIMO_ROSTO_PX = 200;

// Mínimo proporcional ao vídeo (03/11): só os 200 px deixavam capturar de longe quando a câmera do
// tablet entrega mais que 720p — 200 px viram uma fração pequena da imagem. O rosto precisa ocupar ao
// menos esta fração da ALTURA do vídeo (a caixa do detector é aproximadamente quadrada). Valor de
// partida: calibrar com o tablet em campo; o teto é a própria moldura.
export const FRACAO_MINIMA_ROSTO_ALTURA_VIDEO = 0.33;

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

  // Uma instrução por vez: se o rosto não cabe nem centralizado, o problema é o tamanho (AFASTE); se
  // cabe centralizado, o problema é a posição (CENTRALIZE). A moldura está sempre dentro do quadro, então
  // isto também cobre o rosto cortado pela borda do vídeo.
  if (!rostoDentroDoQuadro(c, larguraVideo, alturaVideo)) {
    const centralizado = { ...c, x: (larguraVideo - c.largura) / 2, y: (alturaVideo - c.altura) / 2 };
    return rostoDentroDoQuadro(centralizado, larguraVideo, alturaVideo)
      ? bloqueia('fora', 'Coloque o rosto inteiro dentro da moldura.', 'atencao')
      : bloqueia('fora', 'Afaste-se um pouco da câmera.', 'atencao');
  }

  const ladoMinimo = Math.max(LADO_MINIMO_ROSTO_PX, FRACAO_MINIMA_ROSTO_ALTURA_VIDEO * alturaVideo);
  if (Math.min(c.largura, c.altura) < ladoMinimo)
    return bloqueia('longe', 'Aproxime o rosto da câmera.', 'atencao');

  // A partir daqui o rosto está inteiro na moldura: o botão Capturar já pode liberar. A luz só orienta.
  if (luz) {
    if (luz.rosto < LUZ_ROSTO_MINIMA) return bloqueia('escuro', 'Pouca luz no rosto. Vá para um lugar mais iluminado.', 'atencao', true);
    if (luz.rosto > LUZ_ROSTO_MAXIMA) return bloqueia('estourado', 'Luz forte demais no rosto. Evite o sol direto na cara.', 'atencao', true);
    if (luz.fundo - luz.rosto > LUZ_CONTRALUZ_DIFERENCA && luz.rosto < LUZ_CONTRALUZ_ROSTO_ATE)
      return bloqueia('contraluz', 'Contraluz: a luz está atrás da pessoa. Vire de frente para a luz.', 'atencao', true);
  }

  return { estado: 'ok', mensagem: 'Pronto. Pode capturar.', tom: 'ok', liberaCaptura: true, enquadrado: true };
}

// Modo das regras do detector: 'cadastro' (foto de referência — enquadramento completo, acima) ou
// 'assinatura' (pedido do usuário, 04/11): na hora de assinar/confirmar presença a moldura é só guia e
// basta 1 rosto, inteiro na câmera — sem posição, tamanho, ângulo ou luz. A foto sai em 3×4
// centralizada no rosto (recorteNoRosto). Quem decide se a foto serve continua sendo o Azure Face.
export type ModoCapturaRosto = 'cadastro' | 'assinatura';

export function avaliarPresencaRosto(caixas: readonly CaixaRosto[], larguraVideo?: number, alturaVideo?: number): AvaliacaoRosto {
  if (caixas.length === 0) return bloqueia('nenhum', 'Nenhum rosto encontrado. Olhe para a câmera.', 'alerta');
  if (caixas.length > 1) return bloqueia('varios', 'Mais de um rosto. Fique só o trabalhador no quadro.', 'alerta');
  const c = caixas[0];
  if (larguraVideo && alturaVideo && (c.x < 0 || c.y < 0 || c.x + c.largura > larguraVideo || c.y + c.altura > alturaVideo))
    return bloqueia('fora', 'O rosto está cortado. Centralize o rosto na câmera.', 'atencao');
  return { estado: 'ok', mensagem: 'Rosto encontrado. Pode capturar.', tom: 'ok', liberaCaptura: true, enquadrado: true };
}
