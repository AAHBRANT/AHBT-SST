import { makeStyles } from '@fluentui/react-components';

// Os seis passos tipográficos (spec §1.2). Componentes de ui/ usam estas classes; páginas usam os
// componentes. Fora daqui, tamanho de fonte à mão é proibido.
export const useTipografia = makeStyles({
  display: { fontSize: '28px', lineHeight: '32px', fontWeight: 800, letterSpacing: '-0.01em', fontVariantNumeric: 'tabular-nums' },
  titulo: { fontSize: '20px', lineHeight: '26px', fontWeight: 700 },
  subtitulo: { fontSize: '16px', lineHeight: '22px', fontWeight: 600 },
  corpo: { fontSize: '14px', lineHeight: '20px', fontWeight: 500 },
  legenda: { fontSize: '12px', lineHeight: '16px', fontWeight: 600 },
  micro: { fontSize: '11px', lineHeight: '14px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em' },
});
