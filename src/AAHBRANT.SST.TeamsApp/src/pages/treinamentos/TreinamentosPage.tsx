import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { CursosTreinamentoTab } from '../pessoas/CursosTreinamentoTab';
import { MatrizTreinamentoTab } from '../pessoas/MatrizTreinamentoTab';
import { TurmasTab } from './TurmasTab';

// Item "Treinamentos" da sidebar (02/09): saiu de dentro de PessoasPage (onde só cabia por
// conveniência, ao lado de Trabalhadores/Funções, que não têm nada a ver) e virou módulo próprio —
// cada item da sidebar deve abrir só o que é dele. Matriz de Treinamento por Função fica junto
// porque não tem link próprio e só faz sentido junto de Treinamentos. Aba "Turmas" (04/09, pedido
// do usuário): reformulação do fluxo de realização — turma com participantes pré-selecionados,
// presença biométrica, fotos obrigatórias e encerramento com certificado individual automático.
type AbaTreinamentos = 'turmas' | 'cursos' | 'matriz';

export function TreinamentosPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaTreinamentos>('turmas');
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            Treinamentos
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaTreinamentos)}
        className={estilosAba.lista}
      >
        <Tab value="turmas">Turmas</Tab>
        <Tab value="cursos">Catálogo de Cursos</Tab>
        <Tab value="matriz">Matriz de Treinamento por Função</Tab>
      </TabList>

      {aba === 'turmas' && <TurmasTab />}
      {aba === 'cursos' && <CursosTreinamentoTab />}
      {aba === 'matriz' && <MatrizTreinamentoTab />}
    </div>
  );
}
