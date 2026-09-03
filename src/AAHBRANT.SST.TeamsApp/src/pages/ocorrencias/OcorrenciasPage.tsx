import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { AcidentesPage } from '../acidentes/AcidentesPage';
import { NaoConformidadesPage } from '../naoconformidades/NaoConformidadesPage';
import { OcorrenciasDashboardTab } from './dashboard/OcorrenciasDashboardTab';

type SecaoOcorrencias = 'acidentes' | 'incidentes' | 'quase-acidentes' | 'nao-conformidades' | 'dashboard';

const SECOES_VALIDAS: SecaoOcorrencias[] = [
  'acidentes',
  'incidentes',
  'quase-acidentes',
  'nao-conformidades',
  'dashboard',
];

// Ver TipoOcorrencia em lib/api.ts (1=Acidente, 2=Incidente, 3=Quase acidente).
const TIPO_POR_SECAO: Record<'acidentes' | 'incidentes' | 'quase-acidentes', number> = {
  acidentes: 1,
  incidentes: 2,
  'quase-acidentes': 3,
};

// Item "Ocorrências" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta virou uma
// única entrada de menu — Acidentes/Incidentes/Quase-acidentes (que já eram a mesma tela filtrada
// por tipo via ?tipo=) e Não Conformidades viraram abas aqui. tipoFixo trava o filtro de tipo em
// AcidentesPage (a escolha de tipo agora é a própria aba, não um seletor dentro da tela); o `key`
// força remontar o componente ao trocar de aba, já que o filtro nasce de state interno, não de URL.
export function OcorrenciasPage() {
  const [searchParams] = useSearchParams();
  const secaoInicial = searchParams.get('secao');
  const [secao, setSecao] = useState<SecaoOcorrencias>(
    SECOES_VALIDAS.includes(secaoInicial as SecaoOcorrencias) ? (secaoInicial as SecaoOcorrencias) : 'acidentes',
  );
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <TabList
        selectedValue={secao}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setSecao(data.value as SecaoOcorrencias)}
        className={estilosAba.lista}
      >
        <Tab value="acidentes">Acidentes</Tab>
        <Tab value="incidentes">Incidentes</Tab>
        <Tab value="quase-acidentes">Quase-acidentes</Tab>
        <Tab value="nao-conformidades">Não conformidades</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {(secao === 'acidentes' || secao === 'incidentes' || secao === 'quase-acidentes') && (
        <AcidentesPage key={secao} tipoFixo={TIPO_POR_SECAO[secao]} />
      )}
      {secao === 'nao-conformidades' && <NaoConformidadesPage mostrarTitulo={false} />}
      {secao === 'dashboard' && <OcorrenciasDashboardTab />}
    </div>
  );
}
