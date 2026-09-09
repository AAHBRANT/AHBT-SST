import type { ReactElement, ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { tokensUi, type Tom } from '../../tokens/tokens';
import { useTons, usePulsos } from '../../tokens/tons';

const useStyles = makeStyles({
  root: {
    display: 'inline-flex', alignItems: 'center', gap: '5px', height: '22px', padding: '0 9px',
    borderRadius: tokensUi.raio.full, fontSize: '11px', lineHeight: '14px', fontWeight: 700,
    textTransform: 'uppercase', letterSpacing: '0.05em', whiteSpace: 'nowrap',
  },
  ponto: { width: '6px', height: '6px', borderRadius: '50%', backgroundColor: 'currentColor', flexShrink: 0 },
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
  const estilos = useStyles(); const tons = useTons(); const pulsos = usePulsos();
  return (
    <span
      className={mergeClasses(
        estilos.root,
        tons[tom],
        pulsar === 'rapido' && pulsos.rapido,
        pulsar === 'leve' && pulsos.leve,
        className,
      )}
    >
      {icone ?? <span className={estilos.ponto} aria-hidden="true" />}
      {children}
    </span>
  );
}
