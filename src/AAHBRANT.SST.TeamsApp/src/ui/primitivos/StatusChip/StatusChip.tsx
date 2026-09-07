import type { ReactElement, ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { tokensUi, type Tom } from '../../tokens/tokens';

const useStyles = makeStyles({
  root: {
    display: 'inline-flex', alignItems: 'center', gap: '5px', height: '22px', padding: '0 9px',
    borderRadius: tokensUi.raio.full, fontSize: '11px', lineHeight: '14px', fontWeight: 700,
    textTransform: 'uppercase', letterSpacing: '0.05em', whiteSpace: 'nowrap',
  },
  ponto: { width: '6px', height: '6px', borderRadius: '50%', backgroundColor: 'currentColor', flexShrink: 0 },
  ok: { color: tokensUi.status.ok.tinta, backgroundColor: tokensUi.status.ok.fundo },
  atencao: { color: tokensUi.status.atencao.tinta, backgroundColor: tokensUi.status.atencao.fundo },
  alerta: { color: tokensUi.status.alerta.tinta, backgroundColor: tokensUi.status.alerta.fundo },
  info: { color: tokensUi.status.info.tinta, backgroundColor: tokensUi.status.info.fundo },
  neutro: { color: tokensUi.status.neutro.tinta, backgroundColor: tokensUi.status.neutro.fundo },
  pulsaRapido: {
    animationName: {
      '0%': { boxShadow: '0 0 0 0 currentColor' },
      '50%': { boxShadow: '0 0 0 5px transparent' },
      '100%': { boxShadow: '0 0 0 0 currentColor' },
    },
    animationDuration: '0.9s',
    animationTimingFunction: 'cubic-bezier(0.2, 0, 0, 1)',
    animationIterationCount: 'infinite',
  },
  pulsaLeve: {
    animationName: {
      '0%': { opacity: 1 },
      '50%': { opacity: 0.62 },
      '100%': { opacity: 1 },
    },
    animationDuration: '2.4s',
    animationTimingFunction: 'ease-in-out',
    animationIterationCount: 'infinite',
  },
});

export interface StatusChipProps {
  tom: Tom;
  icone?: ReactElement;
  children: ReactNode;
  className?: string;
  pulsar?: 'rapido' | 'leve';
}

// Chip de estado (spec §1.1, §3): sempre fundo lavado + tinta + ponto/ícone, nunca sólido — é o que
// o separa do verde de navegação. Substitui useStatusChipStyles, BadgeVencimento, <Badge color=> do
// Fluent e cores hex passadas por prop. `pulsar` é o único movimento espontâneo além da chegada:
// vencido pulsa rápido, a vencer pulsa leve (pedido do usuário, 07/09); `prefers-reduced-motion` já
// zera em index.css.
export function StatusChip({ tom, icone, children, className, pulsar }: StatusChipProps) {
  const estilos = useStyles();
  return (
    <span
      className={mergeClasses(
        estilos.root,
        estilos[tom],
        pulsar === 'rapido' && estilos.pulsaRapido,
        pulsar === 'leve' && estilos.pulsaLeve,
        className,
      )}
    >
      {icone ?? <span className={estilos.ponto} aria-hidden="true" />}
      {children}
    </span>
  );
}
