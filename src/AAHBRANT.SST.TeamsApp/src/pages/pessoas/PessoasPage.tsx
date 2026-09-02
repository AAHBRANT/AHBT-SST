import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PessoasDashboardTab } from './dashboard/PessoasDashboardTab';
import { TrabalhadoresTab } from './TrabalhadoresTab';
import { FuncoesTab } from './FuncoesTab';

// Setores e Equipes removidos da navegação por pedido do usuário (30/08) — não são necessários por
// enquanto. Os componentes (SetoresTab/EquipesTab) e os dados continuam existindo, só a aba de
// gestão saiu da UI.
//
// Treinamentos e Matriz de Treinamento por Função saíram daqui em 02/09 e viraram o módulo próprio
// TreinamentosPage (/treinamentos) — cada item da sidebar deve abrir só o que é dele; Trabalhadores
// e Treinamentos não têm nada a ver entre si, só compartilhavam esta tela por conveniência técnica.
type AbaPessoas = 'dashboard' | 'trabalhadores' | 'funcoes';

const ABAS_VALIDAS: AbaPessoas[] = ['dashboard', 'trabalhadores', 'funcoes'];
const ABAS_MOVIDAS_PARA_TREINAMENTOS = ['cursos', 'matrizTreinamento'];

export function PessoasPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const abaInicial = searchParams.get('aba');
  const [aba, setAba] = useState<AbaPessoas>(
    ABAS_VALIDAS.includes(abaInicial as AbaPessoas) ? (abaInicial as AbaPessoas) : 'trabalhadores',
  );
  const estilosAba = usePillTabStyles();

  // Link antigo (?aba=cursos / ?aba=matrizTreinamento) — redireciona pro módulo próprio novo em vez
  // de simplesmente ignorar o parâmetro e cair em "Trabalhadores" sem explicação.
  useEffect(() => {
    if (abaInicial && ABAS_MOVIDAS_PARA_TREINAMENTOS.includes(abaInicial)) {
      navigate('/treinamentos', { replace: true });
    }
  }, [abaInicial, navigate]);

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
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {aba === 'trabalhadores' && <TrabalhadoresTab />}
      {aba === 'funcoes' && <FuncoesTab />}
      {aba === 'dashboard' && <PessoasDashboardTab />}
    </div>
  );
}
