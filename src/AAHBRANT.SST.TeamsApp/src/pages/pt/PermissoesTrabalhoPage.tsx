import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { PtDashboardTab } from './dashboard/PtDashboardTab';
import { PermissoesTrabalhoTab } from './PermissoesTrabalhoTab';

type AbaPt = 'dashboard' | 'registros';

export function PermissoesTrabalhoPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaPt>('registros');
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            Permissão de Trabalho
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPt)}
        className={estilosAba.lista}
      >
        <Tab value="registros">PTs</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'registros' && <PermissoesTrabalhoTab />}
      {aba === 'dashboard' && <PtDashboardTab />}
    </div>
  );
}
