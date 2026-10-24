import { DashboardTopo, PageHeader } from '@ui';
import { IdentificacaoDashboardTab } from './dashboard/IdentificacaoDashboardTab';
import { AreasSstTab } from './AreasSstTab';

// Dashboard no topo da página (pedido do usuário, 05/10): sem abas, só a lista de Áreas embaixo.
export function IdentificacaoPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Identificação" />}

      <DashboardTopo>
        <IdentificacaoDashboardTab />
      </DashboardTopo>

      <AreasSstTab />
    </div>
  );
}
