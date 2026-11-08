import type { ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { Field, Select } from '@ui';
import type { Obra } from '../../lib/api';

// Barra do topo das telas PGR e PCMSO (06/11/2026): filtro de obra à esquerda, botões de "novo" à direita.
const useStyles = makeStyles({
  barra: {
    display: 'flex',
    alignItems: 'flex-end',
    gap: '12px',
    flexWrap: 'wrap',
    marginBottom: '16px',
  },
  filtro: { minWidth: '240px' },
  acoes: { marginLeft: 'auto', display: 'flex', gap: '8px', flexWrap: 'wrap' },
});

export function BarraObraAcoes({
  obras,
  obraId,
  aoMudarObra,
  children,
}: {
  obras: Obra[];
  obraId: string;
  aoMudarObra: (id: string) => void;
  children: ReactNode;
}) {
  const s = useStyles();
  return (
    <div className={s.barra}>
      <Field label="Obra" className={s.filtro}>
        <Select value={obraId} onChange={(_, d) => aoMudarObra(d.value)}>
          <option value="">Todas as obras</option>
          {obras.map((o) => (
            <option key={o.id} value={o.id}>
              {o.nome}
            </option>
          ))}
        </Select>
      </Field>
      <div className={s.acoes}>{children}</div>
    </div>
  );
}
