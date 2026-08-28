import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { AsosTab } from './AsosTab';
import { PcmsoTab } from './PcmsoTab';
import { ExamesComplementaresTab } from './ExamesComplementaresTab';
import { AptidoesTab } from './AptidoesTab';

type AbaSaudeOcupacional = 'aso' | 'pcmso' | 'exames' | 'aptidoes';

// Módulo dedicado de Saúde Ocupacional (PR-SST-003) — sidebar fixa própria, mesmo padrão de
// EpiPage.tsx. ASO/Exames Complementares/Aptidões são cross-worker (gestão operacional); a versão
// somente-leitura por trabalhador continua em PerfilGeralTab.tsx (aba "Geral & ASO").
export function SaudeOcupacionalPage() {
  const [aba, setAba] = useState<AbaSaudeOcupacional>('aso');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Saúde Ocupacional
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaSaudeOcupacional)}
        className={estilosAba.lista}
      >
        <Tab value="aso">ASO</Tab>
        <Tab value="pcmso">PCMSO</Tab>
        <Tab value="exames">Exames Complementares</Tab>
        <Tab value="aptidoes">Aptidões</Tab>
      </TabList>

      {aba === 'aso' && <AsosTab />}
      {aba === 'pcmso' && <PcmsoTab />}
      {aba === 'exames' && <ExamesComplementaresTab />}
      {aba === 'aptidoes' && <AptidoesTab />}
    </div>
  );
}
