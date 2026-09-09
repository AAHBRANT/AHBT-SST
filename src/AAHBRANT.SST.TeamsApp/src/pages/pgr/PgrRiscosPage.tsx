import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Abas, PageHeader, useAbaNaUrl } from '@ui';
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

const ABAS_VALIDAS: readonly AbaPgrGro[] = ['pgrs', 'matriz', 'atividades', 'importar', 'dashboardPgr', 'dashboardRiscos'];

// Onda 2 Task 8 (camada ui/): só o título (guia item 3) está no escopo literal desta task — o
// conteúdo das abas "matriz"/"atividades"/"importar"/"dashboardRiscos" pertence ao módulo `riscos`
// (Task 13, ainda não migrado). A barra de abas em si, porém, precisa sair do `TabList`/`pageStyles`
// crus (senão o arquivo continuaria importando `@fluentui/react-components` direto, quebrando o gate
// de lint mesmo só tocando o título) — convertida para `Abas` seguindo exatamente o padrão já usado
// em `EpiPage.tsx`/piloto 1. O remapeamento legado `?aba=riscos→matriz` não cabe em `useAbaNaUrl`
// (ele só aceita valores já válidos), então é feito manualmente antes: se a URL ainda tiver o valor
// antigo, reescreve para "matriz" e deixa o hook ler o valor corrigido.
export function PgrRiscosPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [searchParams, setSearchParams] = useSearchParams();

  useEffect(() => {
    if (searchParams.get('aba') === 'riscos') {
      setSearchParams(
        (p) => {
          const novo = new URLSearchParams(p);
          novo.set('aba', 'matriz');
          return novo;
        },
        { replace: true },
      );
    }
  }, [searchParams, setSearchParams]);

  const [aba, setAba] = useAbaNaUrl<AbaPgrGro>('aba', ABAS_VALIDAS, 'pgrs');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="PGR / GRO" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        aria-label="Seções de PGR e Riscos"
        valor={aba}
        aoMudar={setAba}
        abas={[
          { valor: 'pgrs', rotulo: 'PGRs' },
          { valor: 'matriz', rotulo: 'Matriz de Risco' },
          { valor: 'atividades', rotulo: 'Atividades' },
          { valor: 'importar', rotulo: 'Importar em Lote' },
          { valor: 'dashboardPgr', rotulo: 'Dashboard PGR' },
          { valor: 'dashboardRiscos', rotulo: 'Dashboard Riscos' },
        ]}
      />

      {aba === 'pgrs' && <PgrsTab />}
      {aba === 'matriz' && <MatrizRiscoTab />}
      {aba === 'atividades' && <AtividadesTab />}
      {aba === 'importar' && <ImportarLoteTab />}
      {aba === 'dashboardPgr' && <PgrDashboardTab />}
      {aba === 'dashboardRiscos' && <RiscosDashboardTab />}
    </div>
  );
}
