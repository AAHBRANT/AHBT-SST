import { Abas, useAbaNaUrl, PageHeader } from '@ui';
import { CatalogoUniformeTab } from './CatalogoUniformeTab';
import { EntregaUniformeTab } from './EntregaUniformeTab';
import { EstoqueUniformeTab } from './EstoqueUniformeTab';
import { MatrizUniformeTab } from './MatrizUniformeTab';

const ABAS = ['entrega', 'catalogo', 'estoque', 'matriz'] as const;
type AbaUniforme = (typeof ABAS)[number];

// Módulo Uniforme — mesmo padrão arquitetural do EPI (docs/superpowers/specs/2026-09-07-modulo-
// uniforme-design.md). Vive como aba dentro de Operação, ao lado de EPI/EPC (não item de 1º nível
// na sidebar — decisão do usuário, 2026-09-07). Tamanho por trabalhador NÃO é aba daqui — é seção
// do perfil do trabalhador (TamanhosUniformeSecao.tsx em pages/pessoas), corrigido na revisão final
// do branch para bater com a spec original.
// Camada ui/ (conversão 7): as abas passam a viver na URL (?aba=), então voltar e F5 preservam a aba.
export function UniformePage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaUniforme>('aba', ABAS, 'entrega');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Uniforme" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Uniforme"
        abas={[
          { valor: 'entrega', rotulo: 'Entrega' },
          { valor: 'catalogo', rotulo: 'Catálogo' },
          { valor: 'estoque', rotulo: 'Estoque' },
          { valor: 'matriz', rotulo: 'Matriz por Função' },
        ]}
      />

      {aba === 'entrega' && <EntregaUniformeTab aoNavegarParaMatriz={() => setAba('matriz')} />}
      {aba === 'catalogo' && <CatalogoUniformeTab />}
      {aba === 'estoque' && <EstoqueUniformeTab />}
      {aba === 'matriz' && <MatrizUniformeTab />}
    </div>
  );
}
