import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { ControleAcessoTab } from './ControleAcessoTab';
import { TrilhaAuditoriaTab } from './TrilhaAuditoriaTab';

type AbaAdministracao = 'acesso' | 'auditoria';

export function AdministracaoPage() {
  const [aba, setAba] = useState<AbaAdministracao>('acesso');

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
        style={{ marginBottom: 16 }}
      >
        <Tab value="acesso">Controle de Acesso</Tab>
        <Tab value="auditoria">Trilha de Auditoria</Tab>
      </TabList>

      {aba === 'acesso' && <ControleAcessoTab />}
      {aba === 'auditoria' && <TrilhaAuditoriaTab />}
    </div>
  );
}
