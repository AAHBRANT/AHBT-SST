import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { AprDashboardTab } from './dashboard/AprDashboardTab';
import { AprsTab } from './AprsTab';

type AbaAprs = 'dashboard' | 'registros';

export function AprsPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaAprs>('registros');
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            APR
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaAprs)}
        className={estilosAba.lista}
      >
        <Tab value="registros">APRs</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'registros' && <AprsTab />}
      {aba === 'dashboard' && <AprDashboardTab />}
    </div>
  );
}
