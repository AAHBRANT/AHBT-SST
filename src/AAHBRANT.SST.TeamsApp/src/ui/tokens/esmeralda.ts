import { makeStyles } from '@fluentui/react-components';

// Vidro esmeralda do indicador principal do Início (KpiCard `destaque`, 28/10/2026), extraído para um
// lugar só em 16/11/2026 para o cabeçalho, a etapa concluída e a barra de carga da "Realizar integração"
// usarem EXATAMENTE o mesmo degradê, borda, sombra, reflexo e brilho (sem copiar valores soltos).
// O usuário pediu GARANTIA de que esse visual não se perde: não simplificar. O brilho que passa fica
// desligado com prefers-reduced-motion. Texto sobre o vidro é sempre branco; o canto mais claro do degradê
// tem contraste baixo com branco, então textos pequenos sobre ele usam peso 600 ou mais.
export const useEstilosEsmeralda = makeStyles({
  vidro: {
    position: 'relative',
    overflow: 'hidden',
    isolation: 'isolate',
    color: '#ffffff',
    backgroundImage: 'linear-gradient(140deg, rgba(16,185,129,0.92) 0%, rgba(5,150,105,0.86) 48%, rgba(6,95,70,0.92) 100%)',
    border: '1px solid rgba(167,243,208,0.55)',
    boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.45), inset 0 -1px 0 rgba(0,0,0,0.12), 0 10px 30px rgba(5,150,105,0.28)',
    backdropFilter: 'blur(14px) saturate(160%)',
    '::before': {
      content: '""',
      position: 'absolute',
      inset: 0,
      zIndex: -1,
      pointerEvents: 'none',
      backgroundImage:
        'radial-gradient(120% 80% at 0% 0%, rgba(255,255,255,0.35), transparent 55%), radial-gradient(90% 70% at 100% 100%, rgba(6,78,59,0.45), transparent 60%)',
    },
    '::after': {
      content: '""',
      position: 'absolute',
      top: '-40%',
      bottom: '-40%',
      width: '38%',
      left: '-60%',
      zIndex: -1,
      pointerEvents: 'none',
      backgroundImage: 'linear-gradient(100deg, transparent, rgba(167,243,208,0.55), transparent)',
      transform: 'skewX(-18deg)',
      animationName: { '0%': { left: '-60%' }, '60%': { left: '-60%' }, '100%': { left: '130%' } },
      animationDuration: '5.5s',
      animationTimingFunction: 'ease-in-out',
      animationDelay: '1s',
      animationIterationCount: 'infinite',
      '@media (prefers-reduced-motion: reduce)': { animationName: 'none' },
    },
  },
  // Só o KpiCard clicável reage ao mouse; cabeçalho e etapas não.
  vidroHover: {
    ':hover': {
      border: '1px solid rgba(209,250,229,0.9)',
      backgroundColor: 'transparent',
      boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.55), 0 14px 38px rgba(5,150,105,0.38)',
    },
  },
  // Pastilhas e faixas dentro do vidro: branco translúcido com texto branco (mesmos valores de deltaDestaque / iconeDestaque).
  vidroChip: { backgroundColor: 'rgba(255, 255, 255, 0.16)', color: '#ffffff', border: '1px solid rgba(255,255,255,0.22)' },
  vidroNumero: { backgroundColor: 'rgba(255, 255, 255, 0.18)', color: '#ffffff', border: '1px solid rgba(255,255,255,0.3)' },
  vidroTextoSuave: { color: 'rgba(255, 255, 255, 0.9)' },
});
