import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PtDashboardTab } from './dashboard/PtDashboardTab';
import { PermissoesTrabalhoTab } from './PermissoesTrabalhoTab';

type AbaPt = 'dashboard' | 'registros';

export function PermissoesTrabalhoPage() {
  const [aba, setAba] = useState<AbaPt>('dashboard');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Permissão de Trabalho
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPt)}
        className={estilosAba.lista}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="registros">PTs</Tab>
      </TabList>

      {aba === 'dashboard' && <PtDashboardTab />}
      {aba === 'registros' && <PermissoesTrabalhoTab />}
    </div>
  );
}
