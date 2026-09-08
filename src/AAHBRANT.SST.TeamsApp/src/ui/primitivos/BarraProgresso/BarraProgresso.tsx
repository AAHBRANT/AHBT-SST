import { makeStyles } from '@fluentui/react-components';
import { designTokens, tokensUi, type Tom } from '../../tokens/tokens';

const useStyles = makeStyles({
  trilho: { height: '6px', borderRadius: tokensUi.raio.full, backgroundColor: designTokens.colorNeutralLight, overflow: 'hidden' },
  preenchimento: { height: '100%', borderRadius: tokensUi.raio.full, transitionProperty: 'width', transitionDuration: tokensUi.duracao.normal, transitionTimingFunction: tokensUi.curva },
});

export interface BarraProgressoProps {
  percentual: number;
  tom?: Tom;
  'aria-label'?: string;
}

// Barra de progresso fina para uma fração/percentual dentro de um card (spec §3, achado na Onda 2
// Task 2: PerfilGeralTab montava isto à mão com designTokens direto na página, e usava vinho
// `colorPrimary` — a marca é reservada para ação, nunca para dado, spec §1.1). `tom` reaproveita as
// cinco cores de estado; default `info` por não haver semântica boa/ruim óbvia numa composição.
export function BarraProgresso({ percentual, tom = 'info', 'aria-label': ariaLabel }: BarraProgressoProps) {
  const e = useStyles();
  const clamped = Math.max(0, Math.min(100, percentual));
  return (
    <div className={e.trilho} role="progressbar" aria-valuenow={Math.round(clamped)} aria-valuemin={0} aria-valuemax={100} aria-label={ariaLabel}>
      <div className={e.preenchimento} style={{ width: `${clamped}%`, backgroundColor: tokensUi.status[tom].tinta }} />
    </div>
  );
}
