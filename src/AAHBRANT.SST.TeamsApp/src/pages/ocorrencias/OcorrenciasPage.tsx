import { Abas, useAbaNaUrl } from '@ui';
import { AcidentesPage } from '../acidentes/AcidentesPage';
import { NaoConformidadesPage } from '../naoconformidades/NaoConformidadesPage';
import { OcorrenciasDashboardTab } from './dashboard/OcorrenciasDashboardTab';

type SecaoOcorrencias = 'acidentes' | 'nao-conformidades' | 'dashboard';

const SECOES_VALIDAS: SecaoOcorrencias[] = ['acidentes', 'nao-conformidades', 'dashboard'];

// Item "Ocorrências" da sidebar (pedido do usuário, 02/09, réplica de mockup, com fusão pedida em
// 03/09): Acidentes/Incidentes/Quase-acidentes eram 3 abas separadas apontando pra mesma tela
// (AcidentesPage, filtrada por Tipo) — o usuário achou confuso ter 3 abas idênticas e pediu pra
// juntar numa aba só. AcidentesPage já suporta isso nativamente: sem `tipoFixo`, ela mostra um
// filtro de Tipo e a coluna Tipo na tabela (era o comportamento de antes de 02/09, quando só existia
// essa aba única). O valor da seção continua "acidentes" (não "ocorrencias") pra não quebrar os
// redirecionamentos legados /acidentes e /melhoria/acidentes em App.tsx.
// Onda 2 Task 21 (camada ui/, conversão 7): TabList/usePillTabStyles → Abas nivel="pilar" +
// useAbaNaUrl('secao', ...), mesma casca já aplicada em GestaoSstPage.tsx/OperacaoPage.tsx.
export function OcorrenciasPage() {
  const [secao, setSecao] = useAbaNaUrl<SecaoOcorrencias>('secao', SECOES_VALIDAS, 'acidentes');

  return (
    <div>
      <Abas
        nivel="pilar"
        valor={secao}
        aoMudar={setSecao}
        aria-label="Seções de Ocorrências"
        abas={[
          { valor: 'acidentes', rotulo: 'Acidentes / Incidentes / Quase-acidentes' },
          { valor: 'nao-conformidades', rotulo: 'Não conformidades' },
          { valor: 'dashboard', rotulo: 'Dashboard' },
        ]}
      />

      {secao === 'acidentes' && <AcidentesPage />}
      {secao === 'nao-conformidades' && <NaoConformidadesPage mostrarTitulo={false} />}
      {secao === 'dashboard' && <OcorrenciasDashboardTab />}
    </div>
  );
}
