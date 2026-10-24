import { DashboardTopo, PageHeader } from '@ui';
import { NaoConformidadesDashboardTab } from './dashboard/NaoConformidadesDashboardTab';
import { NaoConformidadesTab } from './NaoConformidadesTab';

// Dashboard no topo da página (pedido do usuário, 05/10): sem abas, só a lista de registros embaixo.
export function NaoConformidadesPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Não Conformidades" />}

      {/* Aninhada em Ocorrências (mostrarTitulo=false): lá já existe o dashboard de Ocorrências no topo. */}
      {mostrarTitulo && (
        <DashboardTopo>
          <NaoConformidadesDashboardTab />
        </DashboardTopo>
      )}

      <NaoConformidadesTab />
    </div>
  );
}
