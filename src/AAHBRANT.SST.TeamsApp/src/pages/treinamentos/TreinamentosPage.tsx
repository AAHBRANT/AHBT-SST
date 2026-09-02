import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { CursosTreinamentoTab } from '../pessoas/CursosTreinamentoTab';
import { MatrizTreinamentoTab } from '../pessoas/MatrizTreinamentoTab';

// Item "Treinamentos" da sidebar (02/09): saiu de dentro de PessoasPage (onde só cabia por
// conveniência, ao lado de Trabalhadores/Funções, que não têm nada a ver) e virou módulo próprio —
// cada item da sidebar deve abrir só o que é dele. Matriz de Treinamento por Função fica junto
// porque não tem link próprio e só faz sentido junto de Treinamentos.
type AbaTreinamentos = 'cursos' | 'matriz';

export function TreinamentosPage() {
  const [aba, setAba] = useState<AbaTreinamentos>('cursos');
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
        <Tab value="cursos">Treinamentos</Tab>
        <Tab value="matriz">Matriz de Treinamento por Função</Tab>
      </TabList>

      {aba === 'cursos' && <CursosTreinamentoTab />}
      {aba === 'matriz' && <MatrizTreinamentoTab />}
    </div>
  );
}
