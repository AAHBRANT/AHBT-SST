import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { AlertasDashboardTab } from './dashboard/AlertasDashboardTab';
import { AlertasListaTab } from './AlertasListaTab';
import { AlertasConfiguracaoTab } from './AlertasConfiguracaoTab';

type AbaAlertas = 'dashboard' | 'lista' | 'configuracao';

export function AlertasPage() {
  const [aba, setAba] = useState<AbaAlertas>('dashboard');

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Alertas
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaAlertas)}
        style={{ marginBottom: 16 }}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="lista">Lista</Tab>
        <Tab value="configuracao">Configurações</Tab>
      </TabList>

      {aba === 'dashboard' && <AlertasDashboardTab />}
      {aba === 'lista' && <AlertasListaTab />}
      {aba === 'configuracao' && <AlertasConfiguracaoTab />}
    </div>
  );
}
