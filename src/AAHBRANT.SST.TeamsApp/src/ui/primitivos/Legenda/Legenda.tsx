import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  raiz: { color: designTokens.colorNeutralMedium, display: 'block' },
});

export interface LegendaProps {
  children: ReactNode;
  className?: string;
}

// Texto secundário/meta — datas, contagens auxiliares, rótulos de campo (spec §1.2, passo
// "legenda": 12px/600/16). Achado na Onda 2 Task 2: páginas montavam isto com
// <Text style={{ color: designTokens... }}> — cor de token cru é privilégio de src/ui/, nunca de
// página (spec §1.6). Substitui esse padrão em toda a varredura.
export function Legenda({ children, className }: LegendaProps) {
  const estilos = useStyles();
  const tipo = useTipografia();
  return <span className={mergeClasses(tipo.legenda, estilos.raiz, className)}>{children}</span>;
}
