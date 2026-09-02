import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { RiscosDashboardTab } from './dashboard/RiscosDashboardTab';
import { AtividadesTab } from './AtividadesTab';
import { MatrizRiscoTab } from './MatrizRiscoTab';
import { ImportarLoteTab } from './ImportarLoteTab';

type AbaRiscos = 'dashboard' | 'matriz' | 'atividades' | 'importar';

export function RiscosPage() {
  const [aba, setAba] = useState<AbaRiscos>('matriz');
  const estilosAba = usePillTabStyles();

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
        className={estilosAba.lista}
      >
        <Tab value="matriz">Matriz de Risco</Tab>
        <Tab value="atividades">Atividades</Tab>
        <Tab value="importar">Importar em Lote</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'matriz' && <MatrizRiscoTab />}
      {aba === 'atividades' && <AtividadesTab />}
      {aba === 'importar' && <ImportarLoteTab />}
      {aba === 'dashboard' && <RiscosDashboardTab />}
    </div>
  );
}
