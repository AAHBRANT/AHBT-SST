import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { RiscosDashboardTab } from './dashboard/RiscosDashboardTab';
import { AtividadesTab } from './AtividadesTab';
import { PerigosTab } from './PerigosTab';
import { MatrizRiscoTab } from './MatrizRiscoTab';
import { RiscosTab } from './RiscosTab';

type AbaRiscos = 'dashboard' | 'matriz' | 'perigos' | 'atividades' | 'avaliacoes';

export function RiscosPage() {
  const [aba, setAba] = useState<AbaRiscos>('dashboard');

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Riscos
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaRiscos)}
        style={{ marginBottom: 16 }}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="matriz">Matriz de Risco</Tab>
        <Tab value="perigos">Perigos</Tab>
        <Tab value="atividades">Atividades</Tab>
        <Tab value="avaliacoes">Avaliações de Risco</Tab>
      </TabList>

      {aba === 'dashboard' && <RiscosDashboardTab />}
      {aba === 'matriz' && <MatrizRiscoTab />}
      {aba === 'perigos' && <PerigosTab />}
      {aba === 'atividades' && <AtividadesTab />}
      {aba === 'avaliacoes' && <RiscosTab />}
    </div>
  );
}
