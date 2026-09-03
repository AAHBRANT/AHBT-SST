import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { PessoasPage } from './PessoasPage';
import { SaudeOcupacionalPage } from '../saude-ocupacional/SaudeOcupacionalPage';

type SecaoPessoas = 'trabalhadores' | 'aso';

const SECOES_VALIDAS: SecaoPessoas[] = ['trabalhadores', 'aso'];

// Item "Pessoas" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta virou uma única
// entrada de menu. "Trabalhadores" e "Histórico" apontavam pra mesma tela (não existe listagem de
// histórico separada — ver AppShell.tsx) e viraram uma aba só ("Trabalhadores", que já traz Funções
// e Dashboard como sub-abas); "ASO & Exames" vira a outra aba, reaproveitando SaudeOcupacionalPage.
export function PessoasPillarPage() {
  const [searchParams] = useSearchParams();
  const secaoInicial = searchParams.get('secao');
  const [secao, setSecao] = useState<SecaoPessoas>(
    SECOES_VALIDAS.includes(secaoInicial as SecaoPessoas) ? (secaoInicial as SecaoPessoas) : 'trabalhadores',
  );
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <TabList
        selectedValue={secao}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setSecao(data.value as SecaoPessoas)}
        className={estilosAba.lista}
      >
        <Tab value="trabalhadores">Trabalhadores</Tab>
        <Tab value="aso">ASO & Exames</Tab>
      </TabList>

      {secao === 'trabalhadores' && <PessoasPage mostrarTitulo={false} />}
      {secao === 'aso' && <SaudeOcupacionalPage abaInicial="aso" mostrarTitulo={false} />}
    </div>
  );
}
