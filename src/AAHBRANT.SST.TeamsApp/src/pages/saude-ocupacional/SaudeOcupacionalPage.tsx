import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles, useSubTabStyles } from '../pageStyles';
import { AsosTab } from './AsosTab';
import { PcmsoTab } from './PcmsoTab';
import { ExamesComplementaresTab } from './ExamesComplementaresTab';
import { AptidoesTab } from './AptidoesTab';

type AbaSaudeOcupacional = 'aso' | 'pcmso' | 'exames' | 'aptidoes';

const ABAS_VALIDAS: AbaSaudeOcupacional[] = ['aso', 'pcmso', 'exames', 'aptidoes'];

// Módulo "Saúde Ocupacional" (PR-SST-003), item de 1º nível próprio na sidebar (saiu do pilar
// Operação em 02/09 — cada item da sidebar deve abrir só o que é dele, ver AppShell.tsx). ASO/
// Exames Complementares/Aptidões têm abas próprias aqui dentro porque são dado operacional/
// cross-worker; a versão somente-leitura por trabalhador continua em PerfilGeralTab.tsx (aba
// "Geral & ASO").
//
// Suporta abrir já numa aba específica via URL (?aba=pcmso) — usado pelos itens "PCMSO" (grupo
// Gestão de SST) e "ASO & Exames" (grupo Pessoas) do menu lateral, que apontam pra essa mesma tela.
export function SaudeOcupacionalPage({
  abaInicial: abaInicialProp,
  mostrarTitulo = true,
}: { abaInicial?: AbaSaudeOcupacional; mostrarTitulo?: boolean } = {}) {
  const [searchParams] = useSearchParams();
  const abaDaUrl = searchParams.get('aba');
  const abaPadrao =
    abaInicialProp ?? (ABAS_VALIDAS.includes(abaDaUrl as AbaSaudeOcupacional) ? (abaDaUrl as AbaSaudeOcupacional) : 'aso');
  const [aba, setAba] = useState<AbaSaudeOcupacional>(abaPadrao);
  const estilosPillTab = usePillTabStyles();
  const estilosSubTab = useSubTabStyles();
  const estilosAba = mostrarTitulo ? estilosPillTab : estilosSubTab;

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            Saúde Ocupacional
          </Text>
        </div>
      )}

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaSaudeOcupacional)}
        className={estilosAba.lista}
      >
        <Tab value="pcmso">PCMSO</Tab>
        <Tab value="aso">ASO</Tab>
        <Tab value="exames">Exames Complementares</Tab>
        <Tab value="aptidoes">Aptidões</Tab>
      </TabList>

      {aba === 'pcmso' && <PcmsoTab />}
      {aba === 'aso' && <AsosTab />}
      {aba === 'exames' && <ExamesComplementaresTab />}
      {aba === 'aptidoes' && <AptidoesTab />}
    </div>
  );
}
