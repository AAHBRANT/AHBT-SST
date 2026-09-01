import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { usePillTabStyles } from '../pageStyles';
import { ObrasPage } from '../ObrasPage';
import { ControleAcessoTab } from './ControleAcessoTab';
import { TrilhaAuditoriaTab } from './TrilhaAuditoriaTab';
import { PainelAssinaturasTab } from './PainelAssinaturasTab';

type AbaAdministracao = 'obras' | 'acesso' | 'auditoria' | 'assinaturas';

const ABAS_VALIDAS: AbaAdministracao[] = ['obras', 'acesso', 'auditoria', 'assinaturas'];

// Obras virou aba daqui (pedido do usuário, 01/09) — antes era aba de Operação (ver App.tsx pro
// redirecionamento legado). Administração deixou de ser grupo expansível na sidebar (ver
// AppShell.tsx) e virou item único, então essa página concentra tudo — mesmo padrão de
// PillarLayout, só que com abas controladas por estado local em vez de sub-rotas, já que nenhuma
// dessas telas precisa de URL própria. "Configurações" chegou a existir como aba/rota própria mas
// foi removida (pedido do usuário, 01/09) por não ter conteúdo real ainda — sem gaveta vazia.
export function AdministracaoPage() {
  // Suporta abrir já numa aba específica via URL (?aba=obras) — usado pelos redirecionamentos
  // legados de /operacao/obras e /obras (ver App.tsx).
  const [searchParams] = useSearchParams();
  const abaInicial = searchParams.get('aba');
  const [aba, setAba] = useState<AbaAdministracao>(
    ABAS_VALIDAS.includes(abaInicial as AbaAdministracao) ? (abaInicial as AbaAdministracao) : 'obras',
  );
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
        <Tab value="obras">Obras</Tab>
        <Tab value="acesso">Controle de Acesso</Tab>
        <Tab value="auditoria">Trilha de Auditoria</Tab>
        <Tab value="assinaturas">Assinaturas</Tab>
      </TabList>

      {aba === 'obras' && <ObrasPage />}
      {aba === 'acesso' && <ControleAcessoTab />}
      {aba === 'auditoria' && <TrilhaAuditoriaTab />}
      {aba === 'assinaturas' && <PainelAssinaturasTab />}
    </div>
  );
}
