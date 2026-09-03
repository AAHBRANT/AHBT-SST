import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { AlertasDashboardTab } from './dashboard/AlertasDashboardTab';
import { AlertasListaTab } from './AlertasListaTab';
import { AlertasConfiguracaoTab } from './AlertasConfiguracaoTab';

type AbaAlertas = 'dashboard' | 'lista' | 'configuracao';

export function AlertasPage() {
  const [aba, setAba] = useState<AbaAlertas>('lista');
  const estilosAba = usePillTabStyles();

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
        className={estilosAba.lista}
      >
        <Tab value="lista">Lista</Tab>
        <Tab value="configuracao">Configurações</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'lista' && <AlertasListaTab />}
      {aba === 'configuracao' && <AlertasConfiguracaoTab />}
      {aba === 'dashboard' && <AlertasDashboardTab />}
    </div>
  );
}
