import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { NaoConformidadesDashboardTab } from './dashboard/NaoConformidadesDashboardTab';
import { NaoConformidadesTab } from './NaoConformidadesTab';

type AbaNaoConformidades = 'dashboard' | 'registros';

export function NaoConformidadesPage() {
  const [aba, setAba] = useState<AbaNaoConformidades>('registros');
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
        <Tab value="registros">Não Conformidades</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'registros' && <NaoConformidadesTab />}
      {aba === 'dashboard' && <NaoConformidadesDashboardTab />}
    </div>
  );
}
