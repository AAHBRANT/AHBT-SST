import { Abas, useAbaNaUrl } from '@ui';
import { AprsPage } from '../apr/AprsPage';
import { PermissoesTrabalhoPage } from '../pt/PermissoesTrabalhoPage';
import { InspecoesPage } from '../inspecoes/InspecoesPage';
import { IdentificacaoPage } from '../identificacao/IdentificacaoPage';
import { CipaPage } from '../cipa/CipaPage';
import { EpiPage } from '../epi/EpiPage';
import { DdsPage } from '../dds/DdsPage';

type SecaoOperacao = 'apr' | 'pt' | 'inspecoes' | 'cipa' | 'epi' | 'dds' | 'identificacao';

const SECOES_VALIDAS: SecaoOperacao[] = ['apr', 'pt', 'inspecoes', 'cipa', 'epi', 'dds', 'identificacao'];

// Item "Operação" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta virou uma
// única entrada de menu — APR, PT, Inspeções e Identificação (rotulada "Outros controles
// operacionais", mesmo nome já usado na sidebar) viraram abas aqui. CIPA, EPI/EPC e DDS entraram
// aqui em 03/09 (pedido do usuário) — saíram de Gestão de SST, ver GestaoSstPage.tsx.
// Onda 2 Task 21 (camada ui/, conversão 7): TabList/usePillTabStyles → Abas nivel="pilar" +
// useAbaNaUrl('secao', ...) — mesma casca já aplicada em GestaoSstPage.tsx; EpiPage/MatrizEpiTab já
// migradas (piloto 1) como filhas, só a casca da própria página-pilar faltava.
export function OperacaoPage() {
  const [secao, setSecao] = useAbaNaUrl<SecaoOperacao>('secao', SECOES_VALIDAS, 'apr');

  return (
    <div>
      <Abas
        nivel="pilar"
        valor={secao}
        aoMudar={setSecao}
        aria-label="Seções de Operação"
        abas={[
          { valor: 'apr', rotulo: 'APR' },
          { valor: 'pt', rotulo: 'PT' },
          { valor: 'inspecoes', rotulo: 'Inspeções' },
          { valor: 'cipa', rotulo: 'CIPA' },
          { valor: 'epi', rotulo: 'EPI / EPC' },
          { valor: 'dds', rotulo: 'DDS' },
          { valor: 'identificacao', rotulo: 'Outros controles operacionais' },
        ]}
      />

      {secao === 'apr' && <AprsPage mostrarTitulo={false} />}
      {secao === 'pt' && <PermissoesTrabalhoPage mostrarTitulo={false} />}
      {secao === 'inspecoes' && <InspecoesPage mostrarTitulo={false} />}
      {secao === 'cipa' && <CipaPage mostrarTitulo={false} />}
      {secao === 'epi' && <EpiPage mostrarTitulo={false} />}
      {secao === 'dds' && <DdsPage mostrarTitulo={false} />}
      {secao === 'identificacao' && <IdentificacaoPage mostrarTitulo={false} />}
    </div>
  );
}
