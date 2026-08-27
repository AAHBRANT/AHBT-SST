import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { NaoConformidadesDashboardTab } from './dashboard/NaoConformidadesDashboardTab';
import { NaoConformidadesTab } from './NaoConformidadesTab';

type AbaNaoConformidades = 'dashboard' | 'registros';

export function NaoConformidadesPage() {
  const [aba, setAba] = useState<AbaNaoConformidades>('dashboard');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Não Conformidades
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaNaoConformidades)}
        className={estilosAba.lista}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="registros">Não Conformidades</Tab>
      </TabList>

      {aba === 'dashboard' && <NaoConformidadesDashboardTab />}
      {aba === 'registros' && <NaoConformidadesTab />}
    </div>
  );
}
