import type { ReactElement } from 'react';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { Grid24Regular } from '@fluentui/react-icons';
import { tokensUi, type Tom } from '../../tokens/tokens';
import { useAbasStyles } from './Abas.styles';

export interface AbaItem<T extends string> { valor: T; rotulo: string; contador?: number; ponto?: { tom: Tom; titulo: string }; icone?: ReactElement }

export interface AbasProps<T extends string> {
  nivel: 'pilar' | 'modulo' | 'interno';
  abas: readonly AbaItem<T>[];
  valor: T;
  aoMudar: (valor: T) => void;
  'aria-label'?: string;
}

// Abas em três níveis (spec §3, §4.1). Encapsula TabList com os estilos que viviam em pageStyles
// (usePillTabStyles → pilar, useSubTabStyles → modulo) e o terceiro nível que não tinha estilo.
export function Abas<T extends string>({ nivel, abas, valor, aoMudar, 'aria-label': ariaLabel }: AbasProps<T>) {
  const estilos = useAbasStyles();
  return (
    <TabList selectedValue={valor} onTabSelect={(_: SelectTabEvent, d: SelectTabData) => aoMudar(d.value as T)} className={estilos[nivel]} aria-label={ariaLabel}>
      {abas.map((a) => (
        <Tab key={a.valor} value={a.valor} icon={a.icone ?? <Grid24Regular />}>
          {a.rotulo}
          {a.ponto && (
            <span
              role="img"
              aria-label={a.ponto.titulo}
              title={a.ponto.titulo}
              style={{ width: 8, height: 8, borderRadius: '50%', marginLeft: 8, display: 'inline-block', background: tokensUi.status[a.ponto.tom].tinta }}
            />
          )}
          {a.contador !== undefined && <span className={estilos.contador}>{a.contador}</span>}
        </Tab>
      ))}
    </TabList>
  );
}
