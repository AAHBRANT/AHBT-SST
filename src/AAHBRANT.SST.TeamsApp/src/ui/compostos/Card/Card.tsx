import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  root: {
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.lg,
    boxShadow: designTokens.cardShadow,
    overflow: 'hidden',
  },
  corpo: { padding: tokensUi.espaco.xl },
  corpoComTopo: { paddingTop: tokensUi.espaco.lg },
  compacta: { padding: tokensUi.espaco.lg },
  topo: {
    display: 'flex',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    gap: tokensUi.espaco.md,
    padding: '20px 24px',
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: 'color-mix(in srgb, var(--sst-color-surface) 96%, var(--sst-color-primary))',
  },
  tituloGrupo: {
    position: 'relative',
    paddingLeft: '14px',
    '::before': {
      content: '""',
      position: 'absolute',
      left: 0,
      top: '2px',
      width: '4px',
      height: 'calc(100% - 4px)',
      minHeight: '32px',
      borderRadius: '999px',
      backgroundColor: designTokens.colorPrimary,
    },
  },
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
  const temTopo = Boolean(titulo || subtitulo || acoes);
  return (
    <div className={mergeClasses(estilos.root, className)}>
      {temTopo && (
        <div className={estilos.topo}>
          <div className={estilos.tituloGrupo}>
            {titulo && <div className={tipo.subtitulo}>{titulo}</div>}
            {subtitulo && <div className={mergeClasses(tipo.legenda, estilos.subtitulo)}>{subtitulo}</div>}
          </div>
          {acoes && <div>{acoes}</div>}
        </div>
      )}
      <div className={mergeClasses(estilos.corpo, temTopo && estilos.corpoComTopo, densidade === 'compacta' && estilos.compacta)}>
        {children}
      </div>
    </div>
  );
}
