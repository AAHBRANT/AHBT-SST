import { Abas, useAbaNaUrl, PageHeader } from '@ui';
import { CatalogoTab } from './CatalogoTab';
import { EntregasTab } from './EntregasTab';
import { EstoqueTab } from './EstoqueTab';
import { MatrizEpiTab } from './MatrizEpiTab';

const ABAS = ['entregas', 'catalogo', 'estoque', 'matriz'] as const;
type AbaEpi = (typeof ABAS)[number];

// Módulo dedicado de EPI (sidebar fixa própria, fora dos 4 pilares) — decisão confirmada com o
// usuário: catálogo/estoque, entregas e a matriz de EPI por função são dado operacional/compartilhado,
// não pessoal, então não seguem a convenção "vira aba no perfil da pessoa" (ver EntregasEpiTab.tsx em
// Pessoas, que ficou só como histórico somente-leitura apontando para cá). A matriz de EPI por função
// fica aqui (não em Operação → Pessoas → Funções) por ser conceitualmente parte do módulo EPI.
// Piloto 1 da camada ui/ (spec §5.1): as abas passam a viver na URL (?aba=), então voltar e F5
// preservam a aba.
export function EpiPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaEpi>('aba', ABAS, 'entregas');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="EPI — Equipamentos de Proteção Individual" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de EPI"
        abas={[
          { valor: 'entregas', rotulo: 'Entregas' },
          { valor: 'catalogo', rotulo: 'Catálogo' },
          { valor: 'estoque', rotulo: 'Estoque' },
          { valor: 'matriz', rotulo: 'Matriz de EPI por Função' },
        ]}
      />

      {aba === 'entregas' && <EntregasTab aoNavegarParaMatriz={() => setAba('matriz')} />}
      {aba === 'catalogo' && <CatalogoTab />}
      {aba === 'estoque' && <EstoqueTab />}
      {aba === 'matriz' && <MatrizEpiTab />}
    </div>
  );
}
