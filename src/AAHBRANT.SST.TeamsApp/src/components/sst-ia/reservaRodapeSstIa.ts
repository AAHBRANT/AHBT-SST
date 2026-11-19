import { useEffect, type RefObject } from 'react';

// Telas com barra fixa no rodapé (ex.: "Concluir etapa" da Realizar integração) passam a barra para
// este hook: o botão e o painel do SST IA sobem e ficam logo acima dela (regra aprovada em
// 18/11/2026). A altura é medida ao vivo — a barra muda de tamanho no celular e some no computador.
const VARIAVEL = '--sst-ia-rodape';

/** `visivel`: a barra está montada (se ela aparece e some, a medição recomeça). */
export function useReservaRodapeSstIa(barra: RefObject<HTMLElement | null>, visivel = true) {
  useEffect(() => {
    const el = barra.current;
    if (!el || !visivel) return;
    const raiz = document.documentElement;
    const medir = () => {
      // display:none (ex.: tela larga) mede 0 → nada reservado.
      const altura = el.offsetHeight;
      if (altura > 0) raiz.style.setProperty(VARIAVEL, `${altura}px`);
      else raiz.style.removeProperty(VARIAVEL);
    };
    medir();
    const obs = new ResizeObserver(medir);
    obs.observe(el);
    window.addEventListener('resize', medir);
    return () => {
      obs.disconnect();
      window.removeEventListener('resize', medir);
      raiz.style.removeProperty(VARIAVEL);
    };
  }, [barra, visivel]);
}
