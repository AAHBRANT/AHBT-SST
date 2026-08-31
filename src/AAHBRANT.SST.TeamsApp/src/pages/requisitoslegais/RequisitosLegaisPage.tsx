import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { RequisitosLegaisTab } from './RequisitosLegaisTab';
import { QuestionarioAplicabilidadeTab } from './QuestionarioAplicabilidadeTab';

type AbaRequisitosLegais = 'requisitos' | 'questionario';

// Módulo de Requisitos Legais — Motor de Aplicabilidade Legal (requisito do usuário, 2026-08-29).
// Fase 1 (fundação de dados): cadastro dos requisitos/critérios e do questionário de aplicabilidade
// por obra. O cruzamento automático (o "motor" em si, que decide Aplicável/Não aplicável/Em análise
// por obra e gera as obrigações derivadas) é uma fase seguinte, ainda não implementada.
export function RequisitosLegaisPage() {
  const [aba, setAba] = useState<AbaRequisitosLegais>('requisitos');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Requisitos Legais
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaRequisitosLegais)}
        className={estilosAba.lista}
      >
        <Tab value="requisitos">Requisitos e critérios</Tab>
        <Tab value="questionario">Questionário de aplicabilidade</Tab>
      </TabList>

      {aba === 'requisitos' && <RequisitosLegaisTab />}
      {aba === 'questionario' && <QuestionarioAplicabilidadeTab />}
    </div>
  );
}
