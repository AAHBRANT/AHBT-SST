import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  root: {
    backgroundColor: designTokens.colorSurface, border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.lg, boxShadow: designTokens.cardShadow, padding: tokensUi.espaco.xl,
  },
  compacta: { padding: tokensUi.espaco.lg },
  topo: { display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: tokensUi.espaco.md, marginBottom: tokensUi.espaco.md },
  subtitulo: { color: designTokens.colorNeutralMedium, marginTop: '2px' },
});

export interface CardProps {
  densidade?: 'confortavel' | 'compacta';
  titulo?: ReactNode;
  subtitulo?: ReactNode;
  acoes?: ReactNode;
  className?: string;
  children: ReactNode;
}

// O card único do sistema (spec §1.4): raio lg, padding xl (lg na densa), uma sombra por tema.
// Substitui usePageStyles.card, useDashboardStyles.chartCard/motorPainel e cards ad hoc.
export function Card({ densidade = 'confortavel', titulo, subtitulo, acoes, className, children }: CardProps) {
  const estilos = useStyles();
  const tipo = useTipografia();
  const temTopo = titulo || subtitulo || acoes;
  return (
    <div className={mergeClasses(estilos.root, densidade === 'compacta' && estilos.compacta, className)}>
      {temTopo && (
        <div className={estilos.topo}>
          <div>
            {titulo && <div className={tipo.subtitulo}>{titulo}</div>}
            {subtitulo && <div className={mergeClasses(tipo.legenda, estilos.subtitulo)}>{subtitulo}</div>}
          </div>
          {acoes && <div>{acoes}</div>}
        </div>
      )}
      {children}
    </div>
  );
}
