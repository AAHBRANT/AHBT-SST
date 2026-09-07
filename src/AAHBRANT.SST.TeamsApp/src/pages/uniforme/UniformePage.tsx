import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { CatalogoUniformeTab } from './CatalogoUniformeTab';
import { EntregaUniformeTab } from './EntregaUniformeTab';
import { EstoqueUniformeTab } from './EstoqueUniformeTab';
import { MatrizUniformeTab } from './MatrizUniformeTab';

type AbaUniforme = 'entrega' | 'catalogo' | 'estoque' | 'matriz';

// Módulo Uniforme — mesmo padrão arquitetural do EPI (docs/superpowers/specs/2026-09-07-modulo-
// uniforme-design.md). Vive como aba dentro de Operação, ao lado de EPI/EPC (não item de 1º nível
// na sidebar — decisão do usuário, 2026-09-07). Tamanho por trabalhador NÃO é aba daqui — é seção
// do perfil do trabalhador (TamanhosUniformeSecao.tsx em pages/pessoas), corrigido na revisão final
// do branch para bater com a spec original.
export function UniformePage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaUniforme>('entrega');
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            Uniforme
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaUniforme)}
        className={estilosAba.lista}
      >
        <Tab value="entrega">Entrega</Tab>
        <Tab value="catalogo">Catálogo</Tab>
        <Tab value="estoque">Estoque</Tab>
        <Tab value="matriz">Matriz por Função</Tab>
      </TabList>

      {aba === 'entrega' && <EntregaUniformeTab aoNavegarParaMatriz={() => setAba('matriz')} />}
      {aba === 'catalogo' && <CatalogoUniformeTab />}
      {aba === 'estoque' && <EstoqueUniformeTab />}
      {aba === 'matriz' && <MatrizUniformeTab />}
    </div>
  );
}
