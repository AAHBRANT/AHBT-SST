import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { DimensionamentoCipaTab } from './DimensionamentoCipaTab';
import { ProcessoEleitoralCipaTab } from './ProcessoEleitoralCipaTab';
import { MembrosCipaTab } from './MembrosCipaTab';
import { ReunioesCipaTab } from './ReunioesCipaTab';
import { InspecoesCipaTab } from './InspecoesCipaTab';
import { SipatTab } from './SipatTab';

type AbaCipa = 'dimensionamento' | 'eleicao' | 'membros' | 'reunioes' | 'inspecoes' | 'sipat';

// Módulo CIPA (NR-5, requisito do usuário, 31/08/2026), item de 1º nível próprio na sidebar (saiu
// do pilar Operação em 02/09 — cada item da sidebar deve abrir só o que é dele, ver AppShell.tsx).
// Disclosure completo (dimensionamento sempre manual, apuração manual sem urna digital, PGR/GRO
// integrado via botão "Gerar Não Conformidade" e não automaticamente) em Domain/Entidades/Cipa/Cipa.cs.
export function CipaPage() {
  const [aba, setAba] = useState<AbaCipa>('dimensionamento');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          CIPA
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaCipa)}
        className={estilosAba.lista}
      >
        <Tab value="dimensionamento">Dimensionamento</Tab>
        <Tab value="eleicao">Processo Eleitoral</Tab>
        <Tab value="membros">Membros</Tab>
        <Tab value="reunioes">Reuniões</Tab>
        <Tab value="inspecoes">Inspeções</Tab>
        <Tab value="sipat">SIPAT</Tab>
      </TabList>

      {aba === 'dimensionamento' && <DimensionamentoCipaTab />}
      {aba === 'eleicao' && <ProcessoEleitoralCipaTab />}
      {aba === 'membros' && <MembrosCipaTab />}
      {aba === 'reunioes' && <ReunioesCipaTab />}
      {aba === 'inspecoes' && <InspecoesCipaTab />}
      {aba === 'sipat' && <SipatTab />}
    </div>
  );
}
