import { Abas, useAbaNaUrl, PageHeader } from '@ui';
import { CatalogoAtividadesTab } from '../catalogo/CatalogoAtividadesTab';
import { AprDashboardTab } from './dashboard/AprDashboardTab';
import { AprsTab } from './AprsTab';

const ABAS_APRS = ['registros', 'catalogo', 'dashboard'] as const;
type AbaAprs = (typeof ABAS_APRS)[number];

// Onda 2 Task 11 (camada ui/): página-pilar de APR — mesmo padrão de EpiPage/piloto 1 (spec §5.1):
// a aba passa a viver na URL (?aba=), então voltar e F5 preservam a aba.
export function AprsPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaAprs>('aba', ABAS_APRS, 'registros');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="APR" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de APR"
        abas={[
          { valor: 'registros', rotulo: 'APRs' },
          { valor: 'catalogo', rotulo: 'Catálogo' },
          { valor: 'dashboard', rotulo: 'Dashboard' },
        ]}
      />

      {aba === 'registros' && <AprsTab />}
      {aba === 'catalogo' && <CatalogoAtividadesTab tipo="apr" />}
      {aba === 'dashboard' && <AprDashboardTab />}
    </div>
  );
}
