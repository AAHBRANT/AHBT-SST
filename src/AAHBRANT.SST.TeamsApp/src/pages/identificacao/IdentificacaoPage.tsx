import { Abas, useAbaNaUrl, PageHeader } from '@ui';
import { IdentificacaoDashboardTab } from './dashboard/IdentificacaoDashboardTab';
import { AreasSstTab } from './AreasSstTab';

const ABAS_IDENTIFICACAO = ['areas', 'dashboard'] as const;
type AbaIdentificacao = (typeof ABAS_IDENTIFICACAO)[number];

// Onda 2 Task 9 (camada ui/): página-pilar de Identificação — mesmo padrão de AprsPage.tsx/EpiPage.tsx
// (spec §5.1): a aba passa a viver na URL (?aba=), então voltar e F5 preservam a aba.
export function IdentificacaoPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaIdentificacao>('aba', ABAS_IDENTIFICACAO, 'areas');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Identificação" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Identificação"
        abas={[
          { valor: 'areas', rotulo: 'Áreas' },
          { valor: 'dashboard', rotulo: 'Dashboard' },
        ]}
      />

      {aba === 'areas' && <AreasSstTab />}
      {aba === 'dashboard' && <IdentificacaoDashboardTab />}
    </div>
  );
}
