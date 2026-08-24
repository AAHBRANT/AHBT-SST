import { useState } from 'react';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { UsuariosTab } from './UsuariosTab';
import { PerfisAcessoTab } from './PerfisAcessoTab';
import { TrilhaAuditoriaTab } from './TrilhaAuditoriaTab';

type AbaAdministracao = 'usuarios' | 'perfis' | 'auditoria';

export function AdministracaoPage() {
  const [aba, setAba] = useState<AbaAdministracao>('usuarios');

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
        <Tab value="usuarios">Usuários</Tab>
        <Tab value="perfis">Perfis & Permissões</Tab>
        <Tab value="auditoria">Trilha de Auditoria</Tab>
      </TabList>

      {aba === 'usuarios' && <UsuariosTab />}
      {aba === 'perfis' && <PerfisAcessoTab />}
      {aba === 'auditoria' && <TrilhaAuditoriaTab />}
    </div>
  );
}
