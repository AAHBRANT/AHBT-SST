import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PgrRiscosPage } from '../pgr/PgrRiscosPage';
import { SaudeOcupacionalPage } from '../saude-ocupacional/SaudeOcupacionalPage';
import { TreinamentosPage } from '../treinamentos/TreinamentosPage';
import { EpiPage } from '../epi/EpiPage';
import { CipaPage } from '../cipa/CipaPage';
import { DdsPage } from '../dds/DdsPage';
import { EmConstrucaoPage } from '../EmConstrucaoPage';
import { RequisitosLegaisPage } from '../requisitoslegais/RequisitosLegaisPage';

type SecaoGestaoSst =
  | 'pgr'
  | 'pcmso'
  | 'treinamentos'
  | 'epi'
  | 'cipa'
  | 'dds'
  | 'documentos'
  | 'requisitos-legais';

const SECOES_VALIDAS: SecaoGestaoSst[] = [
  'pgr',
  'pcmso',
  'treinamentos',
  'epi',
  'cipa',
  'dds',
  'documentos',
  'requisitos-legais',
];

// Item "Gestão de SST" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta com os
// itens do grupo virou uma única entrada de menu — os antigos itens (PGR/GRO, PCMSO, Treinamentos,
// EPI/EPC, CIPA, DDS, Documentos & Procedimentos, Requisitos Legais) viraram abas aqui. Cada aba
// continua sendo o mesmo componente de página de sempre (mostrarTitulo=false pra não duplicar o
// nome, que já aparece na aba); as abas que cada um já tinha (ex.: PGR/GRO tem PGRs | Matriz de
// Risco | ...) continuam aparecendo por baixo, como sub-abas.
export function GestaoSstPage() {
  const [searchParams] = useSearchParams();
  const secaoInicial = searchParams.get('secao');
  const [secao, setSecao] = useState<SecaoGestaoSst>(
    SECOES_VALIDAS.includes(secaoInicial as SecaoGestaoSst) ? (secaoInicial as SecaoGestaoSst) : 'pgr',
  );
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <TabList
        selectedValue={secao}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setSecao(data.value as SecaoGestaoSst)}
        className={estilosAba.lista}
      >
        <Tab value="pgr">PGR / GRO</Tab>
        <Tab value="pcmso">PCMSO</Tab>
        <Tab value="treinamentos">Treinamentos</Tab>
        <Tab value="epi">EPI / EPC</Tab>
        <Tab value="cipa">CIPA</Tab>
        <Tab value="dds">DDS</Tab>
        <Tab value="documentos">Documentos & Procedimentos</Tab>
        <Tab value="requisitos-legais">Requisitos Legais</Tab>
      </TabList>

      {secao === 'pgr' && <PgrRiscosPage mostrarTitulo={false} />}
      {secao === 'pcmso' && <SaudeOcupacionalPage abaInicial="pcmso" mostrarTitulo={false} />}
      {secao === 'treinamentos' && <TreinamentosPage mostrarTitulo={false} />}
      {secao === 'epi' && <EpiPage mostrarTitulo={false} />}
      {secao === 'cipa' && <CipaPage mostrarTitulo={false} />}
      {secao === 'dds' && <DdsPage mostrarTitulo={false} />}
      {secao === 'documentos' && (
        <EmConstrucaoPage
          titulo="Documentos & Procedimentos"
          descricao="O módulo de Gestão Documental foi removido do sistema em 28/08 (junto com a Matriz Legal antiga). Esse item está reservado no menu, mas precisa ser reconstruído do zero."
          mostrarTitulo={false}
        />
      )}
      {secao === 'requisitos-legais' && <RequisitosLegaisPage mostrarTitulo={false} />}
    </div>
  );
}
