import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PgrsPage } from './PgrsPage';
import { RiscosPage } from '../riscos/RiscosPage';

// Item "PGR / GRO" da sidebar (02/09): PGR e Riscos juntos porque fazem sentido juntos de verdade
// (Riscos é a matriz de risco que o PGR consome — pedido explícito do usuário), não porque é
// tecnicamente conveniente. Cada item que só "cabia" aqui por conveniência (Inspeções, DDS) saiu —
// cada um já tem link próprio na sidebar (ver App.tsx/AppShell.tsx). Suporta abrir direto na aba
// Riscos via ?aba=riscos — usado pelo redirecionamento do antigo item "Riscos" da sidebar.
type AbaPgrGro = 'pgr' | 'riscos';

const ABAS_VALIDAS: AbaPgrGro[] = ['pgr', 'riscos'];

export function PgrRiscosPage() {
  const [searchParams] = useSearchParams();
  const abaInicial = searchParams.get('aba');
  const [aba, setAba] = useState<AbaPgrGro>(
    ABAS_VALIDAS.includes(abaInicial as AbaPgrGro) ? (abaInicial as AbaPgrGro) : 'pgr',
  );
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          PGR / GRO
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPgrGro)}
        className={estilosAba.lista}
      >
        <Tab value="pgr">PGR</Tab>
        <Tab value="riscos">Riscos</Tab>
      </TabList>

      {aba === 'pgr' && <PgrsPage />}
      {aba === 'riscos' && <RiscosPage />}
    </div>
  );
}
