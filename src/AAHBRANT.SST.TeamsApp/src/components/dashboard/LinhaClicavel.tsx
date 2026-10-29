import type { KeyboardEvent, ReactElement, ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '@ui';

// Linha de lista clicável do Início (Agenda de renovações e Atividade recente, 28/10/2026): quem
// reage ao mouse e ao clique é SÓ a linha, não o card inteiro — cada item abre a página dele.

export type VarianteIcone = 'bom' | 'info' | 'atencao' | 'alerta' | 'marca';

const useStyles = makeStyles({
  linha: {
    position: 'relative',
    display: 'grid',
    gridTemplateColumns: '34px minmax(0, 1fr) auto 14px',
    alignItems: 'center',
    gap: '12px',
    padding: '9px 10px',
    margin: '0 -10px',
    borderRadius: tokensUi.raio.md,
    cursor: 'pointer',
    transitionProperty: 'background-color, transform',
    transitionDuration: '150ms',
    '::before': {
      content: '""',
      position: 'absolute',
      left: '10px',
      right: '10px',
      top: 0,
      borderTop: `1px solid ${designTokens.colorCardBorder}`,
    },
    ':first-of-type::before': { display: 'none' },
    ':hover': {
      backgroundColor: 'color-mix(in srgb, var(--sst-botao-marca) 9%, var(--sst-color-surface))',
      transform: 'translateX(2px)',
    },
    ':hover::before': { opacity: 0 },
    // A setinha › aparece só na linha debaixo do mouse.
    ':hover > span:last-child': { opacity: 1 },
    ':focus-visible > span:last-child': { opacity: 1 },
    ':focus-visible': { outline: 'none', boxShadow: 'inset 0 0 0 2px var(--sst-botao-marca)' },
  },
  icone: {
    width: '34px',
    height: '34px',
    borderRadius: tokensUi.raio.md,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  },
  bom: { backgroundColor: designTokens.colorSuccessWash, color: designTokens.colorSuccess },
  info: { backgroundColor: designTokens.colorInfoWash, color: designTokens.colorInfo },
  atencao: { backgroundColor: designTokens.colorWarningWash, color: designTokens.colorWarning },
  alerta: { backgroundColor: designTokens.colorAlertWash, color: designTokens.colorAlert },
  marca: { backgroundColor: designTokens.colorNeutralLight, color: designTokens.colorPrimary },
  corpo: { minWidth: 0 },
  titulo: {
    fontSize: '13.5px',
    lineHeight: '19px',
    fontWeight: 700,
    color: designTokens.colorNeutralDark,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  meta: {
    fontSize: '12px',
    lineHeight: '16px',
    fontWeight: 600,
    color: designTokens.colorNeutralMedium,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  seta: {
    color: 'var(--sst-acao-marca-tinta)',
    fontWeight: 800,
    opacity: 0,
    transitionProperty: 'opacity',
    transitionDuration: '150ms',
  },
});

interface LinhaClicavelProps {
  icone: ReactElement;
  variante: VarianteIcone;
  titulo: string;
  meta?: string | null;
  /** Conteúdo à direita (chip de prazo, hora…). */
  direita?: ReactNode;
  aoAbrir: () => void;
}

export function LinhaClicavel({ icone, variante, titulo, meta, direita, aoAbrir }: LinhaClicavelProps) {
  const e = useStyles();
  const aoTeclar = (evento: KeyboardEvent<HTMLDivElement>) => {
    if (evento.key === 'Enter' || evento.key === ' ') {
      evento.preventDefault();
      aoAbrir();
    }
  };
  return (
    <div className={mergeClasses(e.linha, 'linha-clicavel')} role="link" tabIndex={0} onClick={aoAbrir} onKeyDown={aoTeclar} title={titulo}>
      <div className={mergeClasses(e.icone, e[variante])}>{icone}</div>
      <div className={e.corpo}>
        <div className={e.titulo}>{titulo}</div>
        {meta && <div className={e.meta}>{meta}</div>}
      </div>
      <div>{direita}</div>
      <span className={e.seta} aria-hidden="true">
        ›
      </span>
    </div>
  );
}

const useEstilosGrupo = makeStyles({
  grupo: {
    fontSize: '10.5px',
    fontWeight: 700,
    letterSpacing: '0.1em',
    textTransform: 'uppercase',
    color: designTokens.colorNeutralMedium,
    margin: '12px 0 2px',
    ':first-child': { marginTop: 0 },
  },
  rodape: {
    marginTop: '10px',
    paddingTop: '10px',
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    display: 'flex',
    justifyContent: 'flex-end',
  },
});

export function TituloGrupo({ children }: { children: ReactNode }) {
  const e = useEstilosGrupo();
  return <div className={e.grupo}>{children}</div>;
}

export function RodapeLista({ children }: { children: ReactNode }) {
  const e = useEstilosGrupo();
  return <div className={e.rodape}>{children}</div>;
}
