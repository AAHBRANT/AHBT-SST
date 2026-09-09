import { Abas, useAbaNaUrl, PageHeader } from '@ui';
import { NaoConformidadesDashboardTab } from './dashboard/NaoConformidadesDashboardTab';
import { NaoConformidadesTab } from './NaoConformidadesTab';

const ABAS = ['registros', 'dashboard'] as const;
type AbaNaoConformidades = (typeof ABAS)[number];

// Onda 2 Task 15 (camada ui/): mesmo padrão de EpiPage.tsx (piloto 1) — Abas + useAbaNaUrl em vez
// de TabList/usePillTabStyles/useSubTabStyles cru. Aninhada em OcorrenciasPage.tsx com
// mostrarTitulo={false}, então nivel="modulo" nesse caso (sub-aba de uma sub-aba, mesmo julgamento
// já usado em EpiPage.tsx/AprsPage.tsx).
export function NaoConformidadesPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaNaoConformidades>('aba', ABAS, 'registros');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Não Conformidades" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Não Conformidades"
        abas={[
          { valor: 'registros', rotulo: 'Não Conformidades' },
          { valor: 'dashboard', rotulo: 'Dashboard' },
        ]}
      />

      {aba === 'registros' && <NaoConformidadesTab />}
      {aba === 'dashboard' && <NaoConformidadesDashboardTab />}
    </div>
  );
}
