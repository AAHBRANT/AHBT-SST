import type { ReactNode } from 'react';
import { mergeClasses } from '@fluentui/react-components';
import { useDashboardStyles } from './dashboardStyles';

// Faixa única de indicadores, no mesmo desenho da tela Início (pedido do usuário, 16/10): os KpiCards
// ficam colados numa só moldura, separados por linha fina, sem a borda vinho de card avulso.
export function FaixaKpis({ children }: { children: ReactNode }) {
  const estilos = useDashboardStyles();
  return <div className={mergeClasses(estilos.faixaKpis)} style={{ marginBottom: 16 }}>{children}</div>;
}
