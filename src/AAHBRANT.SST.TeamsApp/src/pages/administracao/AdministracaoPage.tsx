import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { ControleAcessoTab } from './ControleAcessoTab';
import { TrilhaAuditoriaTab } from './TrilhaAuditoriaTab';
import { PainelAssinaturasTab } from './PainelAssinaturasTab';

type AbaAdministracao = 'acesso' | 'auditoria' | 'assinaturas';

export function AdministracaoPage() {
  const [aba, setAba] = useState<AbaAdministracao>('acesso');
  const estilosAba = usePillTabStyles();

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Administração
        </Text>
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaAdministracao)}
        className={estilosAba.lista}
      >
        <Tab value="acesso">Controle de Acesso</Tab>
        <Tab value="auditoria">Trilha de Auditoria</Tab>
        <Tab value="assinaturas">Assinaturas</Tab>
      </TabList>

      {aba === 'acesso' && <ControleAcessoTab />}
      {aba === 'auditoria' && <TrilhaAuditoriaTab />}
      {aba === 'assinaturas' && <PainelAssinaturasTab />}
    </div>
  );
}
