import type { ReactElement, ReactNode } from 'react';
import { motion } from 'framer-motion';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi, type Tom } from '../../tokens/tokens';
import { useTons, usePulsos } from '../../tokens/tons';
import { useTipografia } from '../../tokens/tipografia';
import { escalonado } from '../../tokens/movimento';
import { Carregando } from '../../primitivos/Carregando/Carregando';

const useStyles = makeStyles({
  root: { backgroundColor: designTokens.colorSurface, border: `1px solid ${designTokens.colorCardBorder}`, borderRadius: tokensUi.raio.lg, boxShadow: designTokens.cardShadow, padding: tokensUi.espaco.xl, display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '10px' },
  acionavel: {
    width: '100%',
    minHeight: '100%',
    color: 'inherit',
    font: 'inherit',
    textAlign: 'left',
    cursor: 'pointer',
    transitionDuration: tokensUi.duracao.rapido,
    transitionProperty: 'border, box-shadow, transform',
    ':hover': {
      border: `1px solid ${designTokens.colorPrimary}`,
      boxShadow: designTokens.cardShadow,
    },
    ':focus-visible': {
      outline: `2px solid ${designTokens.colorPrimary}`,
      outlineOffset: '2px',
    },
  },
  textos: { display: 'flex', flexDirection: 'column', gap: '6px', minWidth: 0 },
  rotulo: { color: designTokens.colorNeutralMedium },
  icone: { width: '42px', height: '42px', borderRadius: '50%', display: 'grid', placeItems: 'center', flexShrink: 0 },
  deltas: { display: 'flex', flexWrap: 'wrap', gap: '6px' },
  delta: { fontSize: '11px', lineHeight: '14px', fontWeight: 700, padding: '3px 8px', borderRadius: tokensUi.raio.full },
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
}

// Cartão de indicador (spec §3): valor grande + rótulo à esquerda, ícone em círculo lavado à direita,
// pílulas de variação. Promove components/dashboard/KpiCard.tsx trocando `cor: string` por `tom` —
// é o que elimina os 41 hex soltos dos painéis. Entrada escalonada pelo `indice`. Deltas de
// vencimento podem pulsar (`pulsar`): vencido rápido, a vencer leve — pedido do usuário, 07/09.
export function KpiCard({ rotulo, valor, tom, icone, deltas, indice = 0, carregando, onClick, ariaLabel }: KpiCardProps) {
  const e = useStyles(); const tipo = useTipografia(); const tons = useTons(); const pulsos = usePulsos();
  if (carregando) return <Carregando variante="kpi" linhas={1} />;
  const Elemento = onClick ? motion.button : motion.div;
  return (
    <Elemento
      type={onClick ? 'button' : undefined}
      className={mergeClasses(e.root, onClick && e.acionavel)}
      variants={escalonado(indice)}
      initial="inicial"
      animate="visivel"
      onClick={onClick}
      aria-label={ariaLabel}
    >
      <div className={e.textos}>
        <span className={tipo.display}>{valor}</span>
        <span className={mergeClasses(tipo.legenda, e.rotulo)}>{rotulo}</span>
        {deltas && deltas.length > 0 && (
          <div className={e.deltas}>{deltas.map((d, i) => <span key={`${i}-${d.texto}`} className={mergeClasses(e.delta, tons[d.tom], d.pulsar === 'rapido' && pulsos.rapido, d.pulsar === 'leve' && pulsos.leve)}>{d.texto}</span>)}</div>
        )}
      </div>
      {icone && <div className={mergeClasses(e.icone, tons[tom])}>{icone}</div>}
    </Elemento>
  );
}
