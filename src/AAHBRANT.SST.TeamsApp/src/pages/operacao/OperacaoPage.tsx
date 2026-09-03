import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { AprsPage } from '../apr/AprsPage';
import { PermissoesTrabalhoPage } from '../pt/PermissoesTrabalhoPage';
import { InspecoesPage } from '../inspecoes/InspecoesPage';
import { IdentificacaoPage } from '../identificacao/IdentificacaoPage';

type SecaoOperacao = 'apr' | 'pt' | 'inspecoes' | 'identificacao';

const SECOES_VALIDAS: SecaoOperacao[] = ['apr', 'pt', 'inspecoes', 'identificacao'];

// Item "Operação" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta virou uma
// única entrada de menu — APR, PT, Inspeções e Identificação (rotulada "Outros controles
// operacionais", mesmo nome já usado na sidebar) viraram abas aqui.
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
        <Tab value="identificacao">Outros controles operacionais</Tab>
      </TabList>

      {secao === 'apr' && <AprsPage mostrarTitulo={false} />}
      {secao === 'pt' && <PermissoesTrabalhoPage mostrarTitulo={false} />}
      {secao === 'inspecoes' && <InspecoesPage mostrarTitulo={false} />}
      {secao === 'identificacao' && <IdentificacaoPage mostrarTitulo={false} />}
    </div>
  );
}
