import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { DdsSemanalPage } from './DdsSemanalPage';
import { CatalogoTemasDdsPage } from './CatalogoTemasDdsPage';

// Item "DDS" da sidebar (02/09): DDS + Temas de DDS juntos porque fazem sentido juntos de verdade
// (o catálogo de temas é usado na hora de conduzir o DDS do dia) — Temas de DDS não tem link
// próprio na sidebar. Suporta abrir direto na aba de temas via ?aba=temas-dds — usado pelo
// redirecionamento da antiga sub-rota /prevencao/temas-dds.
const ABAS = ['dds', 'temas-dds'] as const;
type AbaDds = (typeof ABAS)[number];

// Onda 2 (Task 14): usePillTabStyles/useSubTabStyles + <Text size={500}> cru → Abas + PageHeader,
// mesmo padrão de EpiPage.tsx (piloto 1) — `aba` sincronizada com `?aba=` nos dois sentidos.
export function DdsPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaDds>('aba', ABAS, 'dds');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="DDS" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de DDS"
        abas={[
          { valor: 'dds', rotulo: 'DDS' },
          { valor: 'temas-dds', rotulo: 'Temas de DDS' },
        ]}
      />

      {aba === 'dds' && <DdsSemanalPage />}
      {aba === 'temas-dds' && <CatalogoTemasDdsPage />}
    </div>
  );
}
