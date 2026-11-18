import { Abas, useAbaNaUrl } from '@ui';
import {
  Box24Regular,
  ClipboardTaskListLtr24Regular,
  DocumentLock24Regular,
  Home24Regular,
  PeopleCommunity24Regular,
  ShieldCheckmark24Regular,
  Signature24Regular,
  Tag24Regular,
  VehicleTruckProfile24Regular,
} from '@fluentui/react-icons';
import { AprsPage } from '../apr/AprsPage';
import { PermissoesTrabalhoPage } from '../pt/PermissoesTrabalhoPage';
import { InspecoesPage } from '../inspecoes/InspecoesPage';
import { IdentificacaoPage } from '../identificacao/IdentificacaoPage';
import { CipaPage } from '../cipa/CipaPage';
import { EpiPage } from '../epi/EpiPage';
import { UniformePage } from '../uniforme/UniformePage';
import { EpcPage } from '../epc/EpcPage';
import { DdsPage } from '../dds/DdsPage';

type SecaoOperacao = 'apr' | 'pt' | 'inspecoes' | 'cipa' | 'epi' | 'uniforme' | 'epc' | 'dds' | 'identificacao';

const SECOES_VALIDAS: SecaoOperacao[] = ['apr', 'pt', 'inspecoes', 'cipa', 'epi', 'uniforme', 'epc', 'dds', 'identificacao'];

// Item "Operação" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta virou uma
// única entrada de menu — APR, PT, Inspeções e Identificação (rotulada "Outros controles
// operacionais", mesmo nome já usado na sidebar) viraram abas aqui. CIPA, EPI/EPC e DDS entraram
// aqui em 03/09 (pedido do usuário) — saíram de Gestão de SST, ver GestaoSstPage.tsx. Uniforme
// entrou em 2026-09-07 (módulo novo, mesmo padrão do EPI) como aba própria, incorporado via merge
// tardio da branch worktree-modulo-uniforme (nunca tinha sido integrado). EPC ganhou aba própria
// em 04/09 (pedido do usuário) — antes vivia junto com EPI na mesma aba "EPI / EPC".
// Onda 2 Task 21 (camada ui/, conversão 7): TabList/usePillTabStyles → Abas nivel="pilar" +
// useAbaNaUrl('secao', ...) — mesma casca já aplicada em GestaoSstPage.tsx; EpiPage/MatrizEpiTab já
// migradas (piloto 1) como filhas, só a casca da própria página-pilar faltava.
export function OperacaoPage() {
  const [secao, setSecao] = useAbaNaUrl<SecaoOperacao>('secao', SECOES_VALIDAS, 'apr');
  const emEquipamentos = secao === 'epi' || secao === 'uniforme' || secao === 'epc';
  const secaoPilar = emEquipamentos ? 'epi' : secao;

  return (
    <div>
      <Abas
        nivel="pilar"
        valor={secaoPilar}
        aoMudar={setSecao}
        aria-label="Seções de Operação"
        abas={[
          { valor: 'apr', rotulo: 'APR', icone: <Signature24Regular /> },
          { valor: 'pt', rotulo: 'PT', icone: <DocumentLock24Regular /> },
          { valor: 'inspecoes', rotulo: 'Inspeções', icone: <ClipboardTaskListLtr24Regular /> },
          { valor: 'cipa', rotulo: 'CIPA', icone: <PeopleCommunity24Regular /> },
          { valor: 'epi', rotulo: 'EPI, Uniforme & EPC', icone: <ShieldCheckmark24Regular /> },
          { valor: 'dds', rotulo: 'DDS', icone: <VehicleTruckProfile24Regular /> },
          { valor: 'identificacao', rotulo: 'Áreas de SST', icone: <Tag24Regular /> },
        ]}
      />

      {emEquipamentos && (
        <Abas
          nivel="interno"
          valor={secao}
          aoMudar={setSecao}
          aria-label="Equipamentos e Vestimenta"
          abas={[
            { valor: 'epi', rotulo: 'EPI (Proteção Individual)', icone: <ShieldCheckmark24Regular /> },
            { valor: 'uniforme', rotulo: 'Uniforme & Vestimenta', icone: <Box24Regular /> },
            { valor: 'epc', rotulo: 'EPC (Proteção Coletiva)', icone: <Home24Regular /> },
          ]}
        />
      )}

      {secao === 'apr' && <AprsPage mostrarTitulo={false} />}
      {secao === 'pt' && <PermissoesTrabalhoPage mostrarTitulo={false} />}
      {secao === 'inspecoes' && <InspecoesPage mostrarTitulo={false} />}
      {secao === 'cipa' && <CipaPage mostrarTitulo={false} />}
      {secao === 'epi' && <EpiPage mostrarTitulo={false} />}
      {secao === 'uniforme' && <UniformePage mostrarTitulo={false} />}
      {secao === 'epc' && <EpcPage mostrarTitulo={false} />}
      {secao === 'dds' && <DdsPage mostrarTitulo={false} />}
      {secao === 'identificacao' && <IdentificacaoPage mostrarTitulo={false} />}
    </div>
  );
}
