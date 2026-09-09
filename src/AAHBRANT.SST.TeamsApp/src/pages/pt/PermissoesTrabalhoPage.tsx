import { Abas, useAbaNaUrl, PageHeader } from '@ui';
import { PtDashboardTab } from './dashboard/PtDashboardTab';
import { PermissoesTrabalhoTab } from './PermissoesTrabalhoTab';

const ABAS = ['registros', 'dashboard'] as const;
type AbaPt = (typeof ABAS)[number];

// Onda 2 Task 7 (camada ui/): mesmo padrão de EpiPage.tsx (piloto 1) — abas na URL (?aba=), sobrevive
// a voltar/F5.
export function PermissoesTrabalhoPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaPt>('aba', ABAS, 'registros');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Permissão de Trabalho" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Permissão de Trabalho"
        abas={[
          { valor: 'registros', rotulo: 'PTs' },
          { valor: 'dashboard', rotulo: 'Dashboard' },
        ]}
      />

      {aba === 'registros' && <PermissoesTrabalhoTab />}
      {aba === 'dashboard' && <PtDashboardTab />}
    </div>
  );
}
