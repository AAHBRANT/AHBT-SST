import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { AprsPage } from '../apr/AprsPage';
import { PermissoesTrabalhoPage } from '../pt/PermissoesTrabalhoPage';
import { InspecoesPage } from '../inspecoes/InspecoesPage';
import { IdentificacaoPage } from '../identificacao/IdentificacaoPage';
import { CipaPage } from '../cipa/CipaPage';
import { EpiPage } from '../epi/EpiPage';
import { EpcPage } from '../epc/EpcPage';
import { DdsPage } from '../dds/DdsPage';

type SecaoOperacao = 'apr' | 'pt' | 'inspecoes' | 'cipa' | 'epi' | 'epc' | 'dds' | 'identificacao';

const SECOES_VALIDAS: SecaoOperacao[] = ['apr', 'pt', 'inspecoes', 'cipa', 'epi', 'epc', 'dds', 'identificacao'];

// Item "Operação" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta virou uma
// única entrada de menu — APR, PT, Inspeções e Identificação (rotulada "Outros controles
// operacionais", mesmo nome já usado na sidebar) viraram abas aqui. CIPA, EPI/EPC e DDS entraram
// aqui em 03/09 (pedido do usuário) — saíram de Gestão de SST, ver GestaoSstPage.tsx. EPC ganhou
// aba própria em 04/09 (pedido do usuário) — antes vivia junto com EPI na mesma aba "EPI / EPC".
export function OperacaoPage() {
  const [searchParams] = useSearchParams();
  const secaoInicial = searchParams.get('secao');
  const [secao, setSecao] = useState<SecaoOperacao>(
    SECOES_VALIDAS.includes(secaoInicial as SecaoOperacao) ? (secaoInicial as SecaoOperacao) : 'apr',
  );
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <TabList
        selectedValue={secao}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setSecao(data.value as SecaoOperacao)}
        className={estilosAba.lista}
      >
        <Tab value="apr">APR</Tab>
        <Tab value="pt">PT</Tab>
        <Tab value="inspecoes">Inspeções</Tab>
        <Tab value="cipa">CIPA</Tab>
        <Tab value="epi">EPI</Tab>
        <Tab value="epc">EPC</Tab>
        <Tab value="dds">DDS</Tab>
        <Tab value="identificacao">Outros controles operacionais</Tab>
      </TabList>

      {secao === 'apr' && <AprsPage mostrarTitulo={false} />}
      {secao === 'pt' && <PermissoesTrabalhoPage mostrarTitulo={false} />}
      {secao === 'inspecoes' && <InspecoesPage mostrarTitulo={false} />}
      {secao === 'cipa' && <CipaPage mostrarTitulo={false} />}
      {secao === 'epi' && <EpiPage mostrarTitulo={false} />}
      {secao === 'epc' && <EpcPage mostrarTitulo={false} />}
      {secao === 'dds' && <DdsPage mostrarTitulo={false} />}
      {secao === 'identificacao' && <IdentificacaoPage mostrarTitulo={false} />}
    </div>
  );
}
