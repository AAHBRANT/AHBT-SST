import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { AprDashboardTab } from './dashboard/AprDashboardTab';
import { AprsTab } from './AprsTab';

type AbaAprs = 'dashboard' | 'registros';

export function AprsPage() {
  const [aba, setAba] = useState<AbaAprs>('dashboard');

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          APR
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaAprs)}
        style={{ marginBottom: 16 }}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="registros">APRs</Tab>
      </TabList>

      {aba === 'dashboard' && <AprDashboardTab />}
      {aba === 'registros' && <AprsTab />}
    </div>
  );
}
