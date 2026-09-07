import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
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

// Aba sincronizada com a URL nos dois sentidos (spec §3): hoje as páginas-pilar só leem ?secao= na
// montagem — voltar do navegador e F5 perdem a aba. Este hook lê e escreve o parâmetro; a URL é a
// fonte da verdade. Usa replace para não poluir o histórico a cada clique.
export function useAbaNaUrl<T extends string>(param: string, validas: readonly T[], padrao: T): [T, (v: T) => void] {
  const [params, setParams] = useSearchParams();
  const bruto = params.get(param);
  const atual = (validas as readonly string[]).includes(bruto ?? '') ? (bruto as T) : padrao;
  const definir = useCallback((v: T) => {
    setParams((p) => { const novo = new URLSearchParams(p); novo.set(param, v); return novo; }, { replace: true });
  }, [param, setParams]);
  return [atual, definir];
}
