import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { CatalogoEpcTab } from './CatalogoEpcTab';
import { EntregaEpcTab } from './EntregaEpcTab';
import { EstoqueEpcTab } from './EstoqueEpcTab';
import { MatrizEpcTab } from './MatrizEpcTab';

type AbaEpc = 'entrega' | 'catalogo' | 'estoque' | 'matriz';

// Módulo EPC (Equipamento de Proteção Coletiva) — mesmo padrão arquitetural de EPI/Uniforme
// (decisão do usuário, 2026-09-07: "os 3 devem ser bem parecidos"). Antes só existia como
// checklist fixo (ItemEpcPt) dentro da Permissão de Trabalho — este é um catálogo de itens de
// verdade, com estoque por Obra e entrega rastreada. Vive como aba própria dentro de Operação,
// separada da aba "EPI" (que deixou de dizer "EPI / EPC" no rótulo, já que EPC ganhou o próprio
// espaço em vez de compartilhar o rótulo sem ter cadastro próprio).
export function EpcPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaEpc>('entrega');
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            EPC — Equipamentos de Proteção Coletiva
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaEpc)}
        className={estilosAba.lista}
      >
        <Tab value="entrega">Entrega</Tab>
        <Tab value="catalogo">Catálogo</Tab>
        <Tab value="estoque">Estoque</Tab>
        <Tab value="matriz">Matriz por Função</Tab>
      </TabList>

      {aba === 'entrega' && <EntregaEpcTab aoNavegarParaMatriz={() => setAba('matriz')} />}
      {aba === 'catalogo' && <CatalogoEpcTab />}
      {aba === 'estoque' && <EstoqueEpcTab />}
      {aba === 'matriz' && <MatrizEpcTab />}
    </div>
  );
}
