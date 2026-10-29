import type { ReactElement, ReactNode } from 'react';
import { motion } from 'framer-motion';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi, type Tom } from '../../tokens/tokens';
import { useTons, usePulsos } from '../../tokens/tons';
import { useTipografia } from '../../tokens/tipografia';
import { escalonado } from '../../tokens/movimento';
import { Carregando } from '../../primitivos/Carregando/Carregando';

// Verde dos botões do sistema (o mesmo do "Atalhos"): usado no contorno ao passar o mouse.
const VERDE = 'var(--sst-botao-marca)';

const useStyles = makeStyles({
  // Sem a faixa vinho de 3px no topo (removida a pedido do usuário em 28/10/2026).
  root: {
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.lg,
    boxShadow: designTokens.cardShadow,
    padding: '20px 16px',
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: '12px',
  },
  // Contorno ao passar o mouse: o mesmo desenho de antes (borda fina, sombra interna e leve
  // tingimento), agora no verde translúcido dos botões.
  acionavel: {
    width: '100%',
    minHeight: '100%',
    color: 'inherit',
    font: 'inherit',
    textAlign: 'left',
    cursor: 'pointer',
    transitionDuration: tokensUi.duracao.rapido,
    transitionProperty: 'border, box-shadow, transform, background-color',
    ':hover': {
      border: `1px solid ${VERDE}`,
      backgroundColor: `color-mix(in srgb, var(--sst-color-surface) 95%, ${VERDE})`,
      boxShadow: `inset 0 0 0 1px color-mix(in srgb, ${VERDE} 30%, transparent), 0 4px 16px color-mix(in srgb, ${VERDE} 12%, transparent)`,
    },
    ':focus-visible': {
      outline: `2px solid ${VERDE}`,
      outlineOffset: '2px',
    },
  },
  textos: { display: 'flex', flexDirection: 'column', gap: '6px', minWidth: 0 },
  rotulo: { color: designTokens.colorNeutralMedium, fontWeight: 600 },
  icone: { width: '40px', height: '40px', borderRadius: tokensUi.raio.md, display: 'grid', placeItems: 'center', flexShrink: 0 },
  deltas: { display: 'flex', flexWrap: 'wrap', gap: '6px' },
  delta: { fontSize: '11px', lineHeight: '14px', fontWeight: 700, padding: '3px 8px', borderRadius: tokensUi.raio.full },
  // Indicador principal (Início, 28/10/2026): vidro esmeralda translúcido com um brilho que passa
  // devagar. O usuário pediu GARANTIA de que esse visual não se perde — não simplificar.
  destaque: {
    position: 'relative',
    overflow: 'hidden',
    isolation: 'isolate',
    color: '#ffffff',
    backgroundImage: 'linear-gradient(140deg, rgba(16,185,129,0.92) 0%, rgba(5,150,105,0.86) 48%, rgba(6,95,70,0.92) 100%)',
    border: '1px solid rgba(167,243,208,0.55)',
    boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.45), inset 0 -1px 0 rgba(0,0,0,0.12), 0 10px 30px rgba(5,150,105,0.28)',
    backdropFilter: 'blur(14px) saturate(160%)',
    '::before': {
      content: '""',
      position: 'absolute',
      inset: 0,
      zIndex: -1,
      pointerEvents: 'none',
      backgroundImage:
        'radial-gradient(120% 80% at 0% 0%, rgba(255,255,255,0.35), transparent 55%), radial-gradient(90% 70% at 100% 100%, rgba(6,78,59,0.45), transparent 60%)',
    },
    '::after': {
      content: '""',
      position: 'absolute',
      top: '-40%',
      bottom: '-40%',
      width: '38%',
      left: '-60%',
      zIndex: -1,
      pointerEvents: 'none',
      backgroundImage: 'linear-gradient(100deg, transparent, rgba(167,243,208,0.55), transparent)',
      transform: 'skewX(-18deg)',
      animationName: { '0%': { left: '-60%' }, '60%': { left: '-60%' }, '100%': { left: '130%' } },
      animationDuration: '5.5s',
      animationTimingFunction: 'ease-in-out',
      animationDelay: '1s',
      animationIterationCount: 'infinite',
      '@media (prefers-reduced-motion: reduce)': { animationName: 'none' },
    },
    ':hover': {
      border: '1px solid rgba(209,250,229,0.9)',
      backgroundColor: 'transparent',
      boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.55), 0 14px 38px rgba(5,150,105,0.38)',
    },
  },
  textoDestaque: { color: '#ffffff' },
  rotuloDestaque: { color: 'rgba(255, 255, 255, 0.9)' },
  iconeDestaque: { backgroundColor: 'rgba(255, 255, 255, 0.18)', color: '#ffffff', border: '1px solid rgba(255,255,255,0.3)' },
  deltaDestaque: { backgroundColor: 'rgba(255, 255, 255, 0.16)', color: '#ffffff', border: '1px solid rgba(255,255,255,0.22)' },
});

export interface KpiCardProps {
  rotulo: ReactNode;
  valor: ReactNode;
  tom: Tom;
  icone?: ReactElement;
  deltas?: { texto: string; tom: Tom; pulsar?: 'rapido' | 'leve' }[];
  indice?: number;
  carregando?: boolean;
  onClick?: () => void;
  ariaLabel?: string;
  /** Indicador principal da tela: vidro esmeralda com brilho (ver estilo `destaque`). */
  destaque?: boolean;
  /** Conteúdo extra abaixo do valor (ex.: barra de proporção do destaque). */
  extra?: ReactNode;
}

// Cartão de indicador (spec §3): valor grande + rótulo à esquerda, ícone em círculo lavado à direita,
// pílulas de variação. Promove components/dashboard/KpiCard.tsx trocando `cor: string` por `tom` —
// é o que elimina os 41 hex soltos dos painéis. Entrada escalonada pelo `indice`. Deltas de
// vencimento podem pulsar (`pulsar`): vencido rápido, a vencer leve — pedido do usuário, 07/09.
export function KpiCard({ rotulo, valor, tom, icone, deltas, indice = 0, carregando, onClick, ariaLabel, destaque, extra }: KpiCardProps) {
  const e = useStyles(); const tipo = useTipografia(); const tons = useTons(); const pulsos = usePulsos();
  if (carregando) return <Carregando variante="kpi" linhas={1} />;
  const Elemento = onClick ? motion.button : motion.div;
  return (
    <Elemento
      type={onClick ? 'button' : undefined}
      className={mergeClasses(e.root, onClick && e.acionavel, destaque && e.destaque)}
      variants={escalonado(indice)}
      initial="inicial"
      animate="visivel"
      onClick={onClick}
      aria-label={ariaLabel}
    >
      <div className={e.textos}>
        <span className={mergeClasses(tipo.display, destaque && e.textoDestaque)}>{valor}</span>
        <span className={mergeClasses(tipo.legenda, e.rotulo, destaque && e.rotuloDestaque)}>{rotulo}</span>
        {extra}
        {deltas && deltas.length > 0 && (
          <div className={e.deltas}>{deltas.map((d, i) => {
            // Critério de cor: tom "alerta" (vermelho) sempre pulsa rápido, mesmo sem `pulsar` explícito — pedido do usuário, 15/09.
            // No destaque os chips ficam em vidro branco (sem pulsar), para não brigar com o fundo esmeralda.
            const pulsar = destaque ? undefined : d.pulsar ?? (d.tom === 'alerta' ? 'rapido' : undefined);
            return <span key={`${i}-${d.texto}`} className={mergeClasses(e.delta, tons[d.tom], destaque && e.deltaDestaque, pulsar === 'rapido' && pulsos.rapido, pulsar === 'leve' && pulsos.leve)}>{d.texto}</span>;
          })}</div>
        )}
      </div>
      {icone && <div className={mergeClasses(e.icone, tons[tom], destaque && e.iconeDestaque)}>{icone}</div>}
    </Elemento>
  );
}
