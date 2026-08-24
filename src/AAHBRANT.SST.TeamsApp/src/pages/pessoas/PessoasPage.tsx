import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { PessoasDashboardTab } from './dashboard/PessoasDashboardTab';
import { TrabalhadoresTab } from './TrabalhadoresTab';
import { FuncoesTab } from './FuncoesTab';
import { SetoresTab } from './SetoresTab';
import { EquipesTab } from './EquipesTab';
import { CursosTreinamentoTab } from './CursosTreinamentoTab';
import { CatalogoEpiTab } from './CatalogoEpiTab';

type AbaPessoas = 'dashboard' | 'trabalhadores' | 'funcoes' | 'setores' | 'equipes' | 'cursos' | 'epi';

export function PessoasPage() {
  const [aba, setAba] = useState<AbaPessoas>('dashboard');

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
        style={{ marginBottom: 16 }}
      >
        <Tab value="dashboard">Dashboard</Tab>
        <Tab value="trabalhadores">Trabalhadores</Tab>
        <Tab value="funcoes">Funções</Tab>
        <Tab value="setores">Setores</Tab>
        <Tab value="equipes">Equipes</Tab>
        <Tab value="cursos">Cursos de treinamento</Tab>
        <Tab value="epi">Catálogo de EPI</Tab>
      </TabList>

      {aba === 'dashboard' && <PessoasDashboardTab />}
      {aba === 'trabalhadores' && <TrabalhadoresTab />}
      {aba === 'funcoes' && <FuncoesTab />}
      {aba === 'setores' && <SetoresTab />}
      {aba === 'equipes' && <EquipesTab />}
      {aba === 'cursos' && <CursosTreinamentoTab />}
      {aba === 'epi' && <CatalogoEpiTab />}
    </div>
  );
}
