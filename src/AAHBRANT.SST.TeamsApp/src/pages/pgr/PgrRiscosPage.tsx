import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PgrsTab } from './PgrsTab';
import { PgrDashboardTab } from './dashboard/PgrDashboardTab';
import { RiscosPage } from '../riscos/RiscosPage';

// Item "PGR / GRO" da sidebar (02/09): PGR e Riscos juntos porque fazem sentido juntos de verdade
// (Riscos é a matriz de risco que o PGR consome — pedido explícito do usuário), não porque é
// tecnicamente conveniente. Cada item que só "cabia" aqui por conveniência (Inspeções, DDS) saiu —
// cada um já tem link próprio na sidebar (ver App.tsx/AppShell.tsx). Suporta abrir direto na aba
// Riscos via ?aba=riscos — usado pelo redirecionamento do antigo item "Riscos" da sidebar.
//
// Flatteneado pra uma única linha de abas (pedido do usuário, 02/09, via protótipo comentável): a
// versão anterior tinha duas camadas — abas "PGR"/"Riscos" e, dentro de "PGR", outras abas
// "PGRs"/"Dashboard" — com a palavra "PGR" duplicada (título de PgrsPage repetindo o nome da aba
// pai). Dashboard sempre por último, mesmo padrão adotado nos outros módulos.
type AbaPgrGro = 'pgrs' | 'riscos' | 'dashboard';

const ABAS_VALIDAS: AbaPgrGro[] = ['pgrs', 'riscos', 'dashboard'];

export function PgrRiscosPage() {
  const [searchParams] = useSearchParams();
  const abaInicial = searchParams.get('aba');
  const [aba, setAba] = useState<AbaPgrGro>(
    ABAS_VALIDAS.includes(abaInicial as AbaPgrGro) ? (abaInicial as AbaPgrGro) : 'pgrs',
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
        <Tab value="pgrs">PGRs</Tab>
        <Tab value="riscos">Riscos</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'pgrs' && <PgrsTab />}
      {aba === 'riscos' && <RiscosPage />}
      {aba === 'dashboard' && <PgrDashboardTab />}
    </div>
  );
}
