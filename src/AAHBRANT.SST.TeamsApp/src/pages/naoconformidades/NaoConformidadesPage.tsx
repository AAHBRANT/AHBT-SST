import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { NaoConformidadesDashboardTab } from './dashboard/NaoConformidadesDashboardTab';
import { NaoConformidadesTab } from './NaoConformidadesTab';

type AbaNaoConformidades = 'dashboard' | 'registros';

export function NaoConformidadesPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaNaoConformidades>('registros');
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            Não Conformidades
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaNaoConformidades)}
        className={estilosAba.lista}
      >
        <Tab value="registros">Não Conformidades</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'registros' && <NaoConformidadesTab />}
      {aba === 'dashboard' && <NaoConformidadesDashboardTab />}
    </div>
  );
}
