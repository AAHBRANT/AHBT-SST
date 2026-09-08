import type { Transition, Variants } from 'framer-motion';

// Movimento (spec §1.5): três durações, uma curva, dois motivos — chegada (montagem) e origem
// (algo abre de onde foi acionado). Páginas nunca escrevem transition={{ duration }} à mão.
export const duracao = { rapido: 0.12, normal: 0.2, entrada: 0.3 } as const;
export const curva: [number, number, number, number] = [0.2, 0, 0, 1];

export const transicaoEntrada: Transition = { duration: duracao.entrada, ease: curva };
export const transicaoNormal: Transition = { duration: duracao.normal, ease: curva };

export const entrada: Variants = {
  inicial: { opacity: 0, y: 8 },
  visivel: { opacity: 1, y: 0, transition: transicaoEntrada },
};

export function escalonado(indice: number): Variants {
  return {
    inicial: { opacity: 0, y: 8 },
    visivel: { opacity: 1, y: 0, transition: { ...transicaoEntrada, delay: indice * 0.04 } },
  };
}

export function deslizarDe(lado: 'direita' | 'baixo'): Variants {
  const fora = lado === 'direita' ? { x: '100%' } : { y: '100%' };
  return {
    fechado: { ...fora, transition: transicaoEntrada },
    aberto: { x: 0, y: 0, transition: transicaoEntrada },
  };
}
