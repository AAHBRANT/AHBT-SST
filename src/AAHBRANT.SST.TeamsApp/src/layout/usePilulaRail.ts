import { useEffect, useLayoutEffect, useRef, type RefObject } from 'react';

// Indicador verde elástico do rail (skill d3motion-pro, mockup sidebar-icones-movimento-2026-11-27).
// Duas molas — topo e base da pílula. Ao trocar de item, a borda que vai na frente usa a mola
// rápida (LEAD) e a de trás a lenta (TRAIL): a pílula estica no caminho e encolhe ao chegar.
// O ícone do item recém-ativado dá um "pop" (mola POP). Com prefers-reduced-motion, tudo salta direto.
type Mola = { x: number; v: number; alvo: number; k: number; d: number };
const LEAD = { k: 640, d: 38 };
const TRAIL = { k: 190, d: 19 };
const POP = { k: 520, d: 16 };

function passo(m: Mola, dt: number) {
  const sub = Math.max(1, Math.ceil(dt / (1 / 120)));
  const h = dt / sub;
  for (let i = 0; i < sub; i++) {
    const f = -m.k * (m.x - m.alvo) - m.d * m.v;
    m.v += f * h;
    m.x += m.v * h;
  }
}
const parada = (m: Mola) => Math.abs(m.x - m.alvo) < 0.05 && Math.abs(m.v) < 0.05;

export const CLASSE_ITEM_ATIVO = 'gs-rail-item-ativo';
export const CLASSE_ITEM_DESTAQUE = 'gs-rail-item-destaque';

export function usePilulaRail(
  navRef: RefObject<HTMLElement | null>,
  pilulaRef: RefObject<HTMLDivElement | null>,
  dependencias: unknown[],
) {
  const estado = useRef({
    topo: { x: 0, v: 0, alvo: 0, ...LEAD } as Mola,
    base: { x: 0, v: 0, alvo: 0, ...LEAD } as Mola,
    pop: null as { mola: Mola; svg: SVGElement } | null,
    ativo: null as HTMLElement | null,
    ativoRota: null as string | null,
    raf: 0,
    ultimo: 0,
    seguranca: 0 as ReturnType<typeof setTimeout> | 0,
    iniciado: false,
  });

  const reduzMovimento = () =>
    typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

  function desenhar() {
    const s = estado.current;
    const pil = pilulaRef.current;
    if (!pil) return;
    pil.style.transform = `translateY(${s.topo.x}px)`;
    pil.style.height = `${Math.max(20, s.base.x - s.topo.x)}px`;
    if (s.pop) s.pop.svg.style.transform = `scale(${s.pop.mola.x})`;
  }

  function animar(agora: number) {
    const s = estado.current;
    const dt = Math.min(0.05, (agora - (s.ultimo || agora)) / 1000);
    s.ultimo = agora;
    passo(s.topo, dt);
    passo(s.base, dt);
    if (s.pop) passo(s.pop.mola, dt);
    desenhar();
    const popParado = !s.pop || parada(s.pop.mola);
    if (s.pop && popParado) {
      s.pop.svg.style.transform = '';
      s.pop = null;
    }
    if (parada(s.topo) && parada(s.base) && popParado) {
      s.topo.x = s.topo.alvo;
      s.base.x = s.base.alvo;
      desenhar();
      s.raf = 0;
      return;
    }
    s.raf = requestAnimationFrame(animar);
  }

  function posicionar(animado: boolean) {
    const s = estado.current;
    const nav = navRef.current;
    const pil = pilulaRef.current;
    if (!nav || !pil) return;
    const el = nav.querySelector<HTMLElement>(`.${CLASSE_ITEM_ATIVO}:not(.${CLASSE_ITEM_DESTAQUE})`);
    if (!el) {
      pil.style.opacity = '0';
      s.ativo = null;
      s.ativoRota = null;
      return;
    }
    // Posição relativa ao nav (que é o offsetParent) — inclui o rodapé (SST IA).
    const navCaixa = nav.getBoundingClientRect();
    const caixa = el.getBoundingClientRect();
    const y = caixa.top - navCaixa.top + nav.scrollTop;
    const h = caixa.height;
    pil.style.left = `${caixa.left - navCaixa.left}px`;
    pil.style.width = `${caixa.width}px`;
    const estavaOculta = pil.style.opacity === '0' || !s.iniciado;
    pil.style.opacity = '1';
    // Compara pela rota: abrir/fechar o rail remonta o link, mas não é troca de item (sem "pop").
    const rota = el.getAttribute('href');
    const trocou = s.ativoRota !== rota;
    s.ativo = el;
    s.ativoRota = rota;

    if (!animado || estavaOculta || reduzMovimento()) {
      s.topo.x = s.topo.alvo = y;
      s.base.x = s.base.alvo = y + h;
      s.topo.v = s.base.v = 0;
      s.iniciado = true;
      desenhar();
      return;
    }
    if (!trocou && Math.abs(s.topo.alvo - y) < 0.5 && Math.abs(s.base.alvo - y - h) < 0.5) return;
    const descendo = y > s.topo.alvo;
    Object.assign(s.topo, descendo ? TRAIL : LEAD, { alvo: y });
    Object.assign(s.base, descendo ? LEAD : TRAIL, { alvo: y + h });
    if (trocou) {
      const svg = el.querySelector<SVGElement>('svg');
      if (svg) {
        if (s.pop) s.pop.svg.style.transform = '';
        s.pop = { mola: { x: 0.7, v: 0, alvo: 1, ...POP }, svg };
      }
    }
    if (!s.raf) {
      s.ultimo = 0;
      s.raf = requestAnimationFrame(animar);
    }
    // Aba em segundo plano não roda requestAnimationFrame: se em 600 ms a animação não andou,
    // salta direto para o destino para a pílula nunca ficar presa no item anterior.
    if (s.seguranca) clearTimeout(s.seguranca);
    s.seguranca = setTimeout(() => {
      s.seguranca = 0;
      if (s.raf && performance.now() - s.ultimo > 200) concluir();
    }, 600);
  }

  function concluir() {
    const s = estado.current;
    if (s.raf) cancelAnimationFrame(s.raf);
    s.raf = 0;
    s.topo.x = s.topo.alvo;
    s.base.x = s.base.alvo;
    s.topo.v = s.base.v = 0;
    if (s.pop) {
      s.pop.svg.style.transform = '';
      s.pop = null;
    }
    desenhar();
  }

  // Mudou a rota ou abriu/fechou o rail: mede de novo depois que o React pintou as classes.
  useLayoutEffect(() => {
    const s = estado.current;
    const anterior = s.ativo;
    // Se o elemento ativo continuar o mesmo (só mudou a seção ?secao=), só reajusta sem pop.
    posicionar(s.iniciado && anterior !== null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, dependencias);

  // Tamanho do rail muda (abrir/fechar, subitens da seção, janela): reajusta sem animação.
  useEffect(() => {
    const nav = navRef.current;
    if (!nav || typeof ResizeObserver === 'undefined') return;
    const obs = new ResizeObserver(() => {
      const s = estado.current;
      if (!s.raf) {
        posicionar(false);
        return;
      }
      // Animando: só acompanha largura/posição horizontal (abrir/fechar o rail no meio do trajeto).
      const pil = pilulaRef.current;
      if (pil && s.ativo) {
        const navCaixa = nav.getBoundingClientRect();
        const caixa = s.ativo.getBoundingClientRect();
        pil.style.left = `${caixa.left - navCaixa.left}px`;
        pil.style.width = `${caixa.width}px`;
      }
    });
    obs.observe(nav);
    Array.from(nav.children).forEach((filho) => obs.observe(filho));
    return () => obs.disconnect();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(
    () => () => {
      if (estado.current.raf) cancelAnimationFrame(estado.current.raf);
      if (estado.current.seguranca) clearTimeout(estado.current.seguranca);
    },
    [],
  );
}
