import { makeStyles, tokens } from '@fluentui/react-components';
import { formatarTempoGravacao } from './useGravacaoVoz';

const useStyles = makeStyles({
  raiz: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    width: '100%',
    boxSizing: 'border-box',
    padding: '8px 10px',
    borderRadius: '4px',
    background: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
    fontSize: '13px',
  },
  ponto: {
    width: '10px',
    height: '10px',
    borderRadius: '50%',
    flexShrink: 0,
    background: tokens.colorPaletteRedBackground3,
    animationName: { '50%': { opacity: 0.3 } },
    animationDuration: '1.2s',
    animationIterationCount: 'infinite',
    '@media (prefers-reduced-motion: reduce)': { animationName: 'none' },
  },
  tempo: {
    fontVariantNumeric: 'tabular-nums',
    fontWeight: 700,
  },
  barras: {
    display: 'flex',
    gap: '2px',
    alignItems: 'center',
    height: '16px',
    flex: 1,
    minWidth: 0,
    overflow: 'hidden',
  },
  barra: {
    width: '3px',
    minHeight: '3px',
    borderRadius: '2px',
    background: tokens.colorPaletteRedBackground3,
    opacity: 0.7,
  },
});

// "Gravando 00:23 ▮▮▮" — retorno visual de que o microfone está captando (Suporte IA e Ocorrências).
export function IndicadorGravacao({ segundos, niveis }: { segundos: number; niveis: number[] }) {
  const estilos = useStyles();
  return (
    <div className={estilos.raiz} role="status">
      <span className={estilos.ponto} />
      <span>Gravando</span>
      <span className={estilos.tempo}>{formatarTempoGravacao(segundos)}</span>
      <span className={estilos.barras} aria-hidden="true">
        {niveis.map((nivel, i) => (
          <i key={i} className={estilos.barra} style={{ height: `${Math.round(3 + nivel * 13)}px` }} />
        ))}
      </span>
    </div>
  );
}
