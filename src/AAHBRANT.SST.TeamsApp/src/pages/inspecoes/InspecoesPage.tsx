import { Abas, DashboardTopo, PageHeader, useAbaNaUrl } from '@ui';
import { InspecoesDashboardTab } from './dashboard/InspecoesDashboardTab';
import { ChecklistModelosTab } from './ChecklistModelosTab';
import { CartoesInspecoes } from './CartoesInspecoes';

// 'alojamento' e 'veiculos' deixaram de ser sub-abas: viraram cards dentro de "Inspeções" (?cartao=...).
// Links antigos com ?aba=alojamento|veiculos caem na aba "Inspeções" (valor inválido volta ao padrão).
const ABAS = ['execucoes', 'checklists'] as const;
type AbaInspecoes = (typeof ABAS)[number];

// Onda 2 Task 10 (camada ui/): página-pilar de Inspeções — mesmo padrão de EpiPage.tsx (piloto 1):
// aba sincronizada com a URL via useAbaNaUrl, nível pilar/modulo conforme mostrarTitulo.
export function InspecoesPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaInspecoes>('aba', ABAS, 'execucoes');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Inspeções" />}

      <DashboardTopo>
        <InspecoesDashboardTab />
      </DashboardTopo>

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Inspeções"
        abas={[
          { valor: 'execucoes', rotulo: 'Inspeções' },
          { valor: 'checklists', rotulo: 'Catálogo de inspeções' },
        ]}
      />

      {aba === 'execucoes' && <CartoesInspecoes />}
      {aba === 'checklists' && <ChecklistModelosTab />}
    </div>
  );
}
