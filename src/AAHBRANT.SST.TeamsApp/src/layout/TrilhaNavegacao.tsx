import { Link, useLocation } from 'react-router-dom';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { ChevronLeft16Regular } from '@fluentui/react-icons';
import { designTokens } from '../ui/tokens/tokens';
import { montarTrilha } from './mapaNavegacao';

const useStyles = makeStyles({
  trilha: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: '6px',
    fontSize: '13px',
    color: designTokens.colorNeutralMedium,
    marginBottom: '10px',
    // No celular só o último elo clicável ("‹ Voltar para APR"), para não ocupar duas linhas.
    '@media (max-width: 680px)': { display: 'none' },
  },
  compacta: {
    display: 'none',
    '@media (max-width: 680px)': { display: 'inline-flex', alignItems: 'center', gap: '4px', fontSize: '13px', marginBottom: '10px' },
  },
  link: {
    color: 'var(--sst-color-primary)',
    textDecoration: 'none',
    ':hover': { textDecoration: 'underline' },
    ':focus-visible': { outline: '2px solid var(--sst-color-primary)', outlineOffset: '2px', borderRadius: '2px' },
  },
});

// Trilha das telas de detalhe (melhoria 2 da proposta de navegação, 03/11/2026): quem chega por alerta
// do Teams, e-mail ou QR code vê o módulo e a seção e volta à lista num clique. Some nas listas.
export function TrilhaNavegacao() {
  const estilos = useStyles();
  const { pathname } = useLocation();
  const trilha = montarTrilha(pathname);
  if (!trilha) return null;
  const anterior = trilha.elos[trilha.elos.length - 1];
  return (
    <>
      <nav className={estilos.trilha} aria-label="Trilha">
        {trilha.elos.map((elo) => (
          <span key={elo.para} style={{ display: 'contents' }}>
            <Link className={estilos.link} to={elo.para}>{elo.rotulo}</Link>
            <span aria-hidden="true">›</span>
          </span>
        ))}
        <span aria-current="page">{trilha.atual}</span>
      </nav>
      <Link className={mergeClasses(estilos.compacta, estilos.link)} to={anterior.para}>
        <ChevronLeft16Regular /> Voltar para {anterior.rotulo}
      </Link>
    </>
  );
}
