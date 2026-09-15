import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { ObrasPage } from '../ObrasPage';
import { ControleAcessoTab } from './ControleAcessoTab';
import { TrilhaAuditoriaTab } from './TrilhaAuditoriaTab';
import { PainelAssinaturasTab } from './PainelAssinaturasTab';
import { IntegracaoGrhTab } from './IntegracaoGrhTab';
import { TagsIdentificacaoTab } from '../identificacao/TagsIdentificacaoTab';
import { LeitorNfcTab } from '../identificacao/LeitorNfcTab';

const ABAS_VALIDAS = ['obras', 'acesso', 'auditoria', 'assinaturas', 'grh', 'tags', 'leitor'] as const;
type AbaAdministracao = (typeof ABAS_VALIDAS)[number];

// Obras virou aba daqui (pedido do usuário, 01/09) — antes era aba de Operação (ver App.tsx pro
// redirecionamento legado). Administração deixou de ser grupo expansível na sidebar (ver
// AppShell.tsx) e virou item único, então essa página concentra tudo — mesmo padrão de
// PillarLayout, só que com abas controladas por estado local em vez de sub-rotas, já que nenhuma
// dessas telas precisa de URL própria. "Configurações" chegou a existir como aba/rota própria mas
// foi removida (pedido do usuário, 01/09) por não ter conteúdo real ainda — sem gaveta vazia.
//
// Onda 2 Task 17 (camada ui/): `?aba=` (usado pelos redirecionamentos legados de /operacao/obras e
// /obras, ver App.tsx) já era lido na montagem via useState — vira `useAbaNaUrl` para sincronizar
// nos dois sentidos (spec §3), mesmo padrão de PessoasPage/EpiPage. Página de rota única (não é
// aninhada em outra página-pilar), então o PageHeader "Administração" é sempre renderizado.
export function AdministracaoPage() {
  const [aba, setAba] = useAbaNaUrl<AbaAdministracao>('aba', ABAS_VALIDAS, 'obras');

  return (
    <div>
      <PageHeader titulo="Administração" />

      <Abas
        nivel="pilar"
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Administração"
        abas={[
          { valor: 'obras', rotulo: 'Obras' },
          { valor: 'acesso', rotulo: 'Controle de Acesso' },
          { valor: 'auditoria', rotulo: 'Trilha de Auditoria' },
          { valor: 'assinaturas', rotulo: 'Assinaturas' },
          { valor: 'grh', rotulo: 'Integração G-RH' },
          { valor: 'tags', rotulo: 'Tags (NFC/QR)' },
          { valor: 'leitor', rotulo: 'Leitor / Teste NFC' },
        ]}
      />

      {aba === 'obras' && <ObrasPage />}
      {aba === 'acesso' && <ControleAcessoTab />}
      {aba === 'auditoria' && <TrilhaAuditoriaTab />}
      {aba === 'assinaturas' && <PainelAssinaturasTab />}
      {aba === 'grh' && <IntegracaoGrhTab />}
      {aba === 'tags' && <TagsIdentificacaoTab />}
      {aba === 'leitor' && <LeitorNfcTab />}
    </div>
  );
}
