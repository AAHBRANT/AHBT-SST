import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { CursosTreinamentoTab } from './CursosTreinamentoTab';
import { MatrizTreinamentoTab } from './MatrizTreinamentoTab';

type AbaTreinamentos = 'cursos' | 'matriz';

// Módulo dedicado de Treinamentos (PR-SST-002), sidebar fixa própria fora dos pilares — mesmo
// padrão do módulo EPI (ver EpiPage.tsx): catálogo de cursos/NRs e a matriz por função são dado
// operacional compartilhado entre funções, não pessoal, então não viram aba dentro de Pessoas.
export function TreinamentosPage() {
  const [aba, setAba] = useState<AbaTreinamentos>('matriz');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Treinamentos
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaTreinamentos)}
        className={estilosAba.lista}
      >
        <Tab value="matriz">Matriz de treinamento</Tab>
        <Tab value="cursos">Cursos de treinamento</Tab>
      </TabList>

      {aba === 'matriz' && <MatrizTreinamentoTab />}
      {aba === 'cursos' && <CursosTreinamentoTab />}
    </div>
  );
}
