import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { InspecoesDashboardTab } from './dashboard/InspecoesDashboardTab';
import { ChecklistModelosTab } from './ChecklistModelosTab';
import { InspecoesTab } from './InspecoesTab';

const ABAS = ['execucoes', 'checklists', 'dashboard'] as const;
type AbaInspecoes = (typeof ABAS)[number];

// Onda 2 Task 10 (camada ui/): página-pilar de Inspeções — mesmo padrão de EpiPage.tsx (piloto 1):
// aba sincronizada com a URL via useAbaNaUrl, nível pilar/modulo conforme mostrarTitulo.
export function InspecoesPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaInspecoes>('aba', ABAS, 'execucoes');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Inspeções" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Inspeções"
        abas={[
          { valor: 'execucoes', rotulo: 'Inspeções' },
          { valor: 'checklists', rotulo: 'Catálogo de inspeções' },
          { valor: 'dashboard', rotulo: 'Dashboard' },
        ]}
      />

      {aba === 'execucoes' && <InspecoesTab />}
      {aba === 'checklists' && <ChecklistModelosTab />}
      {aba === 'dashboard' && <InspecoesDashboardTab />}
    </div>
  );
}
