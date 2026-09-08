import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Abas, PageHeader, useAbaNaUrl } from '@ui';
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
//
// Onda 2 Task 3 (camada ui/): página-pilar sem aninhamento (só um nível de abas, igual EpiPage) —
// `Abas` + `useAbaNaUrl('aba', ...)` sincroniza a aba com a URL nos dois sentidos (spec §3), mesmo
// param que a página já lia (`?aba=`) antes da migração, preservando os links antigos.
const ABAS = ['trabalhadores', 'funcoes', 'dashboard'] as const;
type AbaPessoas = (typeof ABAS)[number];

const ABAS_MOVIDAS_PARA_TREINAMENTOS = ['cursos', 'matrizTreinamento'];

export function PessoasPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const abaBruta = searchParams.get('aba');

  // Link antigo (?aba=cursos / ?aba=matrizTreinamento) — redireciona pro módulo próprio novo em vez
  // de simplesmente ignorar o parâmetro e cair em "Trabalhadores" sem explicação.
  useEffect(() => {
    if (abaBruta && ABAS_MOVIDAS_PARA_TREINAMENTOS.includes(abaBruta)) {
      navigate('/treinamentos', { replace: true });
    }
  }, [abaBruta, navigate]);

  const [aba, setAba] = useAbaNaUrl<AbaPessoas>('aba', ABAS, 'trabalhadores');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Pessoas" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Pessoas"
        abas={[
          { valor: 'trabalhadores', rotulo: 'Funcionários' },
          { valor: 'funcoes', rotulo: 'Funções' },
          { valor: 'dashboard', rotulo: 'Dashboard' },
        ]}
      />

      {aba === 'trabalhadores' && <TrabalhadoresTab />}
      {aba === 'funcoes' && <FuncoesTab />}
      {aba === 'dashboard' && <PessoasDashboardTab />}
    </div>
  );
}
