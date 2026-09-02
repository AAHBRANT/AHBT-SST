import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { DdsSemanalPage } from './DdsSemanalPage';
import { CatalogoTemasDdsPage } from './CatalogoTemasDdsPage';

// Item "DDS" da sidebar (02/09): DDS + Temas de DDS juntos porque fazem sentido juntos de verdade
// (o catálogo de temas é usado na hora de conduzir o DDS do dia) — Temas de DDS não tem link
// próprio na sidebar. Suporta abrir direto na aba de temas via ?aba=temas-dds — usado pelo
// redirecionamento da antiga sub-rota /prevencao/temas-dds.
type AbaDds = 'dds' | 'temas-dds';

const ABAS_VALIDAS: AbaDds[] = ['dds', 'temas-dds'];

export function DdsPage() {
  const [searchParams] = useSearchParams();
  const abaInicial = searchParams.get('aba');
  const [aba, setAba] = useState<AbaDds>(
    ABAS_VALIDAS.includes(abaInicial as AbaDds) ? (abaInicial as AbaDds) : 'dds',
  );
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          DDS
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaDds)}
        className={estilosAba.lista}
      >
        <Tab value="dds">DDS</Tab>
        <Tab value="temas-dds">Temas de DDS</Tab>
      </TabList>

      {aba === 'dds' && <DdsSemanalPage />}
      {aba === 'temas-dds' && <CatalogoTemasDdsPage />}
    </div>
  );
}
