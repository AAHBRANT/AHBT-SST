import { Abas, useAbaNaUrl, PageHeader } from '@ui';
import { CatalogoEpcTab } from './CatalogoEpcTab';
import { EstoqueEpcTab } from './EstoqueEpcTab';
import { InstalacoesTab } from './InstalacoesTab';

const ABAS = ['instalacoes', 'catalogo', 'estoque'] as const;
type AbaEpc = (typeof ABAS)[number];

// Módulo dedicado de EPC — aba própria, separada de EPI (pedido do usuário, 04/09). Mesma estrutura
// de catálogo/estoque do EPI, mas sem Matriz (decisão confirmada: EPC não tem matriz por função) e
// com "Instalações" no lugar de "Entregas", já que o EPC é instalado numa Obra, não entregue/
// assinado por um trabalhador.
// Onda 3 Task 22.5 (camada ui/): mesmo padrão de EpiPage.tsx (piloto 1) — abas sincronizadas com a
// URL (?aba=) via useAbaNaUrl, voltar e F5 preservam a aba.
export function EpcPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaEpc>('aba', ABAS, 'instalacoes');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="EPC — Equipamentos de Proteção Coletiva" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de EPC"
        abas={[
          { valor: 'instalacoes', rotulo: 'Instalações' },
          { valor: 'catalogo', rotulo: 'Catálogo' },
          { valor: 'estoque', rotulo: 'Estoque' },
        ]}
      />

      {aba === 'instalacoes' && <InstalacoesTab />}
      {aba === 'catalogo' && <CatalogoEpcTab />}
      {aba === 'estoque' && <EstoqueEpcTab />}
    </div>
  );
}
