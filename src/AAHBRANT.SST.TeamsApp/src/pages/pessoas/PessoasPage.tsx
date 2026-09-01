import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PessoasDashboardTab } from './dashboard/PessoasDashboardTab';
import { TrabalhadoresTab } from './TrabalhadoresTab';
import { FuncoesTab } from './FuncoesTab';
import { CursosTreinamentoTab } from './CursosTreinamentoTab';
import { MatrizTreinamentoTab } from './MatrizTreinamentoTab';

// Setores e Equipes removidos da navegação por pedido do usuário (30/08) — não são necessários por
// enquanto. Os componentes (SetoresTab/EquipesTab) e os dados continuam existindo, só a aba de
// gestão saiu da UI.
type AbaPessoas = 'dashboard' | 'trabalhadores' | 'funcoes' | 'cursos' | 'matrizTreinamento';

const ABAS_VALIDAS: AbaPessoas[] = ['dashboard', 'trabalhadores', 'funcoes', 'cursos', 'matrizTreinamento'];

export function PessoasPage() {
  // Suporta abrir já numa aba específica via URL (?aba=cursos) — usado pelo item "Treinamentos" da
  // gaveta Gestão de SST (ver AppShell.tsx): catálogo de cursos e matriz por função já são abas
  // daqui, não uma tela própria de Treinamentos.
  const [searchParams] = useSearchParams();
  const abaInicial = searchParams.get('aba');
  const [aba, setAba] = useState<AbaPessoas>(
    ABAS_VALIDAS.includes(abaInicial as AbaPessoas) ? (abaInicial as AbaPessoas) : 'trabalhadores',
  );
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
        <Tab value="trabalhadores">Trabalhadores</Tab>
        <Tab value="funcoes">Funções</Tab>
        <Tab value="cursos">Cursos de treinamento</Tab>
        <Tab value="matrizTreinamento">Matriz de Treinamento por Função</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'trabalhadores' && <TrabalhadoresTab />}
      {aba === 'funcoes' && <FuncoesTab />}
      {aba === 'cursos' && <CursosTreinamentoTab />}
      {aba === 'matrizTreinamento' && <MatrizTreinamentoTab />}
      {aba === 'dashboard' && <PessoasDashboardTab />}
    </div>
  );
}
