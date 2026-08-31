import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { AsosTab } from './AsosTab';
import { PcmsoTab } from './PcmsoTab';
import { ExamesComplementaresTab } from './ExamesComplementaresTab';
import { AptidoesTab } from './AptidoesTab';

type AbaSaudeOcupacional = 'aso' | 'pcmso' | 'exames' | 'aptidoes';

const ABAS_VALIDAS: AbaSaudeOcupacional[] = ['aso', 'pcmso', 'exames', 'aptidoes'];

// Aba "Saúde Ocupacional" (PR-SST-003) do pilar Operação (movida pra cá em 28/08 — antes era item
// de 1º nível na sidebar, mesmo padrão de EpiPage.tsx). ASO/Exames Complementares/Aptidões têm
// abas próprias aqui dentro porque são dado operacional/cross-worker; a versão somente-leitura por
// trabalhador continua em PerfilGeralTab.tsx (aba "Geral & ASO").
//
// Suporta abrir já numa aba específica via URL (?aba=pcmso) — usado pelos itens "PCMSO" (grupo
// Gestão de SST) e "ASO & Exames" (grupo Pessoas) do menu lateral, que apontam pra essa mesma tela.
export function SaudeOcupacionalPage() {
  const [searchParams] = useSearchParams();
  const abaInicial = searchParams.get('aba');
  const [aba, setAba] = useState<AbaSaudeOcupacional>(
    ABAS_VALIDAS.includes(abaInicial as AbaSaudeOcupacional) ? (abaInicial as AbaSaudeOcupacional) : 'aso',
  );
  const estilosAba = usePillTabStyles();

  return (
    <div>
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
