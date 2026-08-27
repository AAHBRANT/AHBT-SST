import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { IdentificacaoDashboardTab } from './dashboard/IdentificacaoDashboardTab';
import { AreasSstTab } from './AreasSstTab';
import { TagsIdentificacaoTab } from './TagsIdentificacaoTab';
import { LeitorNfcTab } from './LeitorNfcTab';

type AbaIdentificacao = 'dashboard' | 'areas' | 'tags' | 'leitor';

export function IdentificacaoPage() {
  const [aba, setAba] = useState<AbaIdentificacao>('dashboard');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Identificação
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaIdentificacao)}
        className={estilosAba.lista}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="areas">Áreas</Tab>
        <Tab value="tags">Tags (NFC/QR)</Tab>
        <Tab value="leitor">Leitor / Teste NFC</Tab>
      </TabList>

      {aba === 'dashboard' && <IdentificacaoDashboardTab />}
      {aba === 'areas' && <AreasSstTab />}
      {aba === 'tags' && <TagsIdentificacaoTab />}
      {aba === 'leitor' && <LeitorNfcTab />}
    </div>
  );
}
