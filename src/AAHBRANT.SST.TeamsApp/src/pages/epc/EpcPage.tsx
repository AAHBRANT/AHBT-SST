import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { CatalogoEpcTab } from './CatalogoEpcTab';
import { EstoqueEpcTab } from './EstoqueEpcTab';
import { InstalacoesTab } from './InstalacoesTab';

type AbaEpc = 'instalacoes' | 'catalogo' | 'estoque';

// Módulo dedicado de EPC — aba própria, separada de EPI (pedido do usuário, 04/09). Mesma estrutura
// de catálogo/estoque do EPI, mas sem Matriz (decisão confirmada: EPC não tem matriz por função) e
// com "Instalações" no lugar de "Entregas", já que o EPC é instalado numa Obra, não entregue/
// assinado por um trabalhador.
export function EpcPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useState<AbaEpc>('instalacoes');
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
        <Tab value="instalacoes">Instalações</Tab>
        <Tab value="catalogo">Catálogo</Tab>
        <Tab value="estoque">Estoque</Tab>
      </TabList>

      {aba === 'instalacoes' && <InstalacoesTab />}
      {aba === 'catalogo' && <CatalogoEpcTab />}
      {aba === 'estoque' && <EstoqueEpcTab />}
    </div>
  );
}
