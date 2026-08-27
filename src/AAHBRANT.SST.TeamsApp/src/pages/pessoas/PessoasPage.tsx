import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PessoasDashboardTab } from './dashboard/PessoasDashboardTab';
import { TrabalhadoresTab } from './TrabalhadoresTab';
import { FuncoesTab } from './FuncoesTab';
import { SetoresTab } from './SetoresTab';
import { EquipesTab } from './EquipesTab';

type AbaPessoas = 'dashboard' | 'trabalhadores' | 'funcoes' | 'setores' | 'equipes';

export function PessoasPage() {
  const [aba, setAba] = useState<AbaPessoas>('dashboard');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Pessoas
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPessoas)}
        className={estilosAba.lista}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="trabalhadores">Trabalhadores</Tab>
        <Tab value="funcoes">Funções</Tab>
        <Tab value="setores">Setores</Tab>
        <Tab value="equipes">Equipes</Tab>
      </TabList>

      {aba === 'dashboard' && <PessoasDashboardTab />}
      {aba === 'trabalhadores' && <TrabalhadoresTab />}
      {aba === 'funcoes' && <FuncoesTab />}
      {aba === 'setores' && <SetoresTab />}
      {aba === 'equipes' && <EquipesTab />}
    </div>
  );
}
