import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { AcidentesPage } from '../acidentes/AcidentesPage';
import { NaoConformidadesPage } from '../naoconformidades/NaoConformidadesPage';
import { OcorrenciasDashboardTab } from './dashboard/OcorrenciasDashboardTab';

type SecaoOcorrencias = 'acidentes' | 'nao-conformidades' | 'dashboard';

const SECOES_VALIDAS: SecaoOcorrencias[] = ['acidentes', 'nao-conformidades', 'dashboard'];

// Item "Ocorrências" da sidebar (pedido do usuário, 02/09, réplica de mockup, com fusão pedida em
// 03/09): Acidentes/Incidentes/Quase-acidentes eram 3 abas separadas apontando pra mesma tela
// (AcidentesPage, filtrada por Tipo) — o usuário achou confuso ter 3 abas idênticas e pediu pra
// juntar numa aba só. AcidentesPage já suporta isso nativamente: sem `tipoFixo`, ela mostra um
// filtro de Tipo e a coluna Tipo na tabela (era o comportamento de antes de 02/09, quando só existia
// essa aba única). O valor da seção continua "acidentes" (não "ocorrencias") pra não quebrar os
// redirecionamentos legados /acidentes e /melhoria/acidentes em App.tsx.
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
        <Tab value="acidentes">Acidentes / Incidentes / Quase-acidentes</Tab>
        <Tab value="nao-conformidades">Não conformidades</Tab>
        <Tab value="dashboard">Dashboard</Tab>
      </TabList>

      {secao === 'acidentes' && <AcidentesPage />}
      {secao === 'nao-conformidades' && <NaoConformidadesPage mostrarTitulo={false} />}
      {secao === 'dashboard' && <OcorrenciasDashboardTab />}
    </div>
  );
}
