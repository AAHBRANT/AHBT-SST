import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PgrDashboardTab } from './dashboard/PgrDashboardTab';
import { PgrsTab } from './PgrsTab';

type AbaPgrs = 'dashboard' | 'registros';

export function PgrsPage() {
  const [aba, setAba] = useState<AbaPgrs>('dashboard');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          PGR
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPgrs)}
        className={estilosAba.lista}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="registros">PGRs</Tab>
      </TabList>

      {aba === 'dashboard' && <PgrDashboardTab />}
      {aba === 'registros' && <PgrsTab />}
    </div>
  );
}
