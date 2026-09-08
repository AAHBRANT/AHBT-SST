import { makeStyles } from '@fluentui/react-components';
import { tokensUi } from './tokens';

// Cinco tons de estado como classes lavadas (fundo + tinta) e os dois pulsos de vencimento
// (spec §1.1, §1.5). Único lugar: StatusChip, KpiCard e quem vier depois consomem daqui.
export const useTons = makeStyles({
  ok: { color: tokensUi.status.ok.tinta, backgroundColor: tokensUi.status.ok.fundo },
  atencao: { color: tokensUi.status.atencao.tinta, backgroundColor: tokensUi.status.atencao.fundo },
  alerta: { color: tokensUi.status.alerta.tinta, backgroundColor: tokensUi.status.alerta.fundo },
  info: { color: tokensUi.status.info.tinta, backgroundColor: tokensUi.status.info.fundo },
  neutro: { color: tokensUi.status.neutro.tinta, backgroundColor: tokensUi.status.neutro.fundo },
});

export const usePulsos = makeStyles({
  rapido: {
    animationName: {
      '0%': { boxShadow: '0 0 0 0 currentColor' },
      '50%': { boxShadow: '0 0 0 5px transparent' },
      '100%': { boxShadow: '0 0 0 0 currentColor' },
    },
    animationDuration: '0.9s',
    animationTimingFunction: tokensUi.curva,
    animationIterationCount: 'infinite',
  },
  leve: {
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
