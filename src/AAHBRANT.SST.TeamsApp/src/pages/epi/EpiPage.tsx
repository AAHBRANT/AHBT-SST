import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { CatalogoTab } from './CatalogoTab';
import { EntregasTab } from './EntregasTab';

type AbaEpi = 'catalogo' | 'entregas';

// Módulo dedicado de EPI (sidebar fixa própria, fora dos 4 pilares) — decisão confirmada com o
// usuário: catálogo/estoque e entregas de EPI são dado operacional/compartilhado, não pessoal,
// então não seguem a convenção "vira aba no perfil da pessoa" (ver EntregasEpiTab.tsx em Pessoas,
// que ficou só como histórico somente-leitura apontando para cá).
export function EpiPage() {
  const [aba, setAba] = useState<AbaEpi>('entregas');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          EPI — Equipamentos de Proteção Individual
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaEpi)}
        className={estilosAba.lista}
      >
        <Tab value="entregas">Entregas</Tab>
        <Tab value="catalogo">Catálogo e estoque</Tab>
      </TabList>

      {aba === 'entregas' && <EntregasTab />}
      {aba === 'catalogo' && <CatalogoTab />}
    </div>
  );
}
