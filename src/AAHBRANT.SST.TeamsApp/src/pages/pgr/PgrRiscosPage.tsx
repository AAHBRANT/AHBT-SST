import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { PgrsTab } from './PgrsTab';
import { PgrDashboardTab } from './dashboard/PgrDashboardTab';
import { MatrizRiscoTab } from '../riscos/MatrizRiscoTab';
import { AtividadesTab } from '../riscos/AtividadesTab';
import { ImportarLoteTab } from '../riscos/ImportarLoteTab';
import { RiscosDashboardTab } from '../riscos/dashboard/RiscosDashboardTab';

// Item "PGR / GRO" da sidebar (02/09): PGR e Riscos juntos porque fazem sentido juntos de verdade
// (Riscos é a matriz de risco que o PGR consome — pedido explícito do usuário), não porque é
// tecnicamente conveniente.
//
// Flatteneado pra uma única linha de abas (pedido do usuário, 02/09, via protótipo comentável):
// tanto o lado PGR (PgrsPage) quanto o lado Riscos (RiscosPage) tinham título próprio duplicando o
// nome da aba pai ("PGR"/"Riscos" repetidos) e uma segunda barra de abas por baixo — as duas telas
// foram dissolvidas aqui dentro. "Dashboard PGR" e "Dashboard Riscos" ficam por último (mesmo padrão
// adotado nos outros módulos), com nomes distintos porque são dashboards diferentes.
//
// ?aba=riscos continua funcionando (usado pelo redirecionamento do antigo item "Riscos" da
// sidebar) — mapeado para "matriz", que era a aba inicial de RiscosPage.
type AbaPgrGro = 'pgrs' | 'matriz' | 'atividades' | 'importar' | 'dashboardPgr' | 'dashboardRiscos';

const ABAS_VALIDAS: AbaPgrGro[] = ['pgrs', 'matriz', 'atividades', 'importar', 'dashboardPgr', 'dashboardRiscos'];

export function PgrRiscosPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [searchParams] = useSearchParams();
  const abaInicial = searchParams.get('aba');
  const abaResolvida = abaInicial === 'riscos' ? 'matriz' : abaInicial;
  const [aba, setAba] = useState<AbaPgrGro>(
    ABAS_VALIDAS.includes(abaResolvida as AbaPgrGro) ? (abaResolvida as AbaPgrGro) : 'pgrs',
  );
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            PGR / GRO
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPgrGro)}
        className={estilosAba.lista}
      >
        <Tab value="pgrs">PGRs</Tab>
        <Tab value="matriz">Matriz de Risco</Tab>
        <Tab value="atividades">Atividades</Tab>
        <Tab value="importar">Importar em Lote</Tab>
        <Tab value="dashboardPgr">Dashboard PGR</Tab>
        <Tab value="dashboardRiscos">Dashboard Riscos</Tab>
      </TabList>

      {aba === 'pgrs' && <PgrsTab />}
      {aba === 'matriz' && <MatrizRiscoTab />}
      {aba === 'atividades' && <AtividadesTab />}
      {aba === 'importar' && <ImportarLoteTab />}
      {aba === 'dashboardPgr' && <PgrDashboardTab />}
      {aba === 'dashboardRiscos' && <RiscosDashboardTab />}
    </div>
  );
}
