import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { AlertasDashboardTab } from './dashboard/AlertasDashboardTab';
import { AlertasListaTab } from './AlertasListaTab';
import { AlertasConfiguracaoTab } from './AlertasConfiguracaoTab';

const ABAS_ALERTAS = ['lista', 'configuracao', 'dashboard'] as const;
type AbaAlertas = (typeof ABAS_ALERTAS)[number];

// Onda 2 Task 16 (camada ui/): item de 1º nível próprio na sidebar, rota direta `/alertas` (sem
// nesting em outra página-pilar — confirmado em App.tsx). Mesmo padrão de CipaPage.tsx: aba
// sincronizada com a URL (?aba=) via useAbaNaUrl, para voltar/F5 preservarem a aba.
export function AlertasPage() {
  const [aba, setAba] = useAbaNaUrl<AbaAlertas>('aba', ABAS_ALERTAS, 'lista');

  return (
    <div>
      <PageHeader titulo="Alertas" />

      <Abas
        nivel="pilar"
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Alertas"
        abas={[
          { valor: 'lista', rotulo: 'Lista' },
          { valor: 'configuracao', rotulo: 'Configurações' },
          { valor: 'dashboard', rotulo: 'Dashboard' },
        ]}
      />

      {aba === 'lista' && <AlertasListaTab />}
      {aba === 'configuracao' && <AlertasConfiguracaoTab />}
      {aba === 'dashboard' && <AlertasDashboardTab />}
    </div>
  );
}
