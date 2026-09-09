import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { useAbasStyles } from './Abas.styles';

export interface AbaItem<T extends string> { valor: T; rotulo: string; contador?: number }

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
        <Tab key={a.valor} value={a.valor}>
          {a.rotulo}
          {a.contador !== undefined && <span className={estilos.contador}>{a.contador}</span>}
        </Tab>
      ))}
    </TabList>
  );
}
