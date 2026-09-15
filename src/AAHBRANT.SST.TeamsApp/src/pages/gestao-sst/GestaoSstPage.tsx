import { Abas, useAbaNaUrl } from '@ui';
import { PgrRiscosPage } from '../pgr/PgrRiscosPage';
import { SaudeOcupacionalPage } from '../saude-ocupacional/SaudeOcupacionalPage';
import { TreinamentosPage } from '../treinamentos/TreinamentosPage';
import { RequisitosLegaisPage } from '../requisitoslegais/RequisitosLegaisPage';
import { MateriaisApoioTab } from './MateriaisApoioTab';

type SecaoGestaoSst = 'pgr' | 'pcmso' | 'treinamentos' | 'documentos' | 'requisitos-legais';

const SECOES_VALIDAS: SecaoGestaoSst[] = ['pgr', 'pcmso', 'treinamentos', 'documentos', 'requisitos-legais'];

// Item "Gestão de SST" da sidebar (pedido do usuário, 02/09, réplica de mockup): a gaveta com os
// itens do grupo virou uma única entrada de menu — os antigos itens (PGR/GRO, PCMSO, Treinamentos,
// Documentos & Procedimentos, Requisitos Legais) viraram abas aqui. Cada aba continua sendo o mesmo
// componente de página de sempre (mostrarTitulo=false pra não duplicar o nome, que já aparece na
// aba); as abas que cada um já tinha (ex.: PGR/GRO tem PGRs | Matriz de Risco | ...) continuam
// aparecendo por baixo, como sub-abas. EPI/EPC, CIPA e DDS saíram daqui (pedido do usuário, 03/09) e
// viraram abas de Operação — ver OperacaoPage.tsx.
// Onda 2 Task 21 (camada ui/, conversão 7): TabList/usePillTabStyles → Abas nivel="pilar" +
// useAbaNaUrl('secao', ...). EmConstrucaoPage (removido nesta mesma task) → EstadoVazio
// variante="em-construcao", mesmo padrão já usado na galeria (ui/galeria/GaleriaPage.tsx).
export function GestaoSstPage() {
  const [secao, setSecao] = useAbaNaUrl<SecaoGestaoSst>('secao', SECOES_VALIDAS, 'pgr');

  return (
    <div>
      <Abas
        nivel="pilar"
        valor={secao}
        aoMudar={setSecao}
        aria-label="Seções de Gestão de SST"
        abas={[
          { valor: 'pgr', rotulo: 'PGR / GRO' },
          { valor: 'pcmso', rotulo: 'PCMSO' },
          { valor: 'treinamentos', rotulo: 'Treinamentos' },
          { valor: 'documentos', rotulo: 'Documentos & Procedimentos' },
          { valor: 'requisitos-legais', rotulo: 'Requisitos Legais' },
        ]}
      />

      {secao === 'pgr' && <PgrRiscosPage mostrarTitulo={false} />}
      {secao === 'pcmso' && <SaudeOcupacionalPage abaInicial="pcmso" mostrarTitulo={false} />}
      {secao === 'treinamentos' && <TreinamentosPage mostrarTitulo={false} />}
      {secao === 'documentos' && <MateriaisApoioTab />}
      {secao === 'requisitos-legais' && <RequisitosLegaisPage mostrarTitulo={false} />}
    </div>
  );
}
