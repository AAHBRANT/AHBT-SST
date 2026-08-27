import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { InspecoesDashboardTab } from './dashboard/InspecoesDashboardTab';
import { ChecklistModelosTab } from './ChecklistModelosTab';
import { InspecoesTab } from './InspecoesTab';

type AbaInspecoes = 'dashboard' | 'execucoes' | 'checklists';

export function InspecoesPage() {
  const [aba, setAba] = useState<AbaInspecoes>('dashboard');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Inspeções
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaInspecoes)}
        className={estilosAba.lista}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="execucoes">Execuções</Tab>
        <Tab value="checklists">Checklists</Tab>
      </TabList>

      {aba === 'dashboard' && <InspecoesDashboardTab />}
      {aba === 'execucoes' && <InspecoesTab />}
      {aba === 'checklists' && <ChecklistModelosTab />}
    </div>
  );
}
