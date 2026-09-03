import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { InspecoesDashboardTab } from './dashboard/InspecoesDashboardTab';
import { ChecklistModelosTab } from './ChecklistModelosTab';
import { InspecoesTab } from './InspecoesTab';

type AbaInspecoes = 'dashboard' | 'execucoes' | 'checklists';

export function InspecoesPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaInspecoes>('execucoes');
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            Inspeções
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaInspecoes)}
        className={estilosAba.lista}
      >
        <Tab value="execucoes">Execuções</Tab>
        <Tab value="checklists">Checklists</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'execucoes' && <InspecoesTab />}
      {aba === 'checklists' && <ChecklistModelosTab />}
      {aba === 'dashboard' && <InspecoesDashboardTab />}
    </div>
  );
}
