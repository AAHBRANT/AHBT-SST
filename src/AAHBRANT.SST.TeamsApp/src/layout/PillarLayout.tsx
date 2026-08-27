import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pages/pageStyles';

export interface AbaPillar {
  valor: string;
  rotulo: string;
}

interface PillarLayoutProps {
  titulo: string;
  prefixo: string;
  abas: AbaPillar[];
}

// Layout compartilhado pelos módulos-pilar com abas internas (Conformidade/Prevenção/Operação):
// título do módulo + abas superiores que navegam entre sub-rotas (ver App.tsx e memória
// project_sst_gsst_ia_aprovada). A aba ativa é derivada do 2º segmento da URL, então rotas de
// detalhe (ex: /prevencao/pgr/:id) mantêm a aba correta destacada.
export function PillarLayout({ titulo, prefixo, abas }: PillarLayoutProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const estilosAba = usePillTabStyles();
  const segmentos = location.pathname.split('/').filter(Boolean);
  const abaAtual = segmentos[1] ?? abas[0]?.valor;

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          {titulo}
        </Text>
      </div>

      <TabList
        selectedValue={abaAtual}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => navigate(`/${prefixo}/${data.value}`)}
        className={estilosAba.lista}
      >
        {abas.map((aba) => (
          <Tab key={aba.valor} value={aba.valor}>
            {aba.rotulo}
          </Tab>
        ))}
      </TabList>

      <Outlet />
    </div>
  );
}
