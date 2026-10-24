import { Abas, DashboardTopo, PageHeader, useAbaNaUrl } from '@ui';
import { TreinamentosDashboardTab } from './dashboard/TreinamentosDashboardTab';
import { CertificadosTab } from './CertificadosTab';
import { CursosTreinamentoTab } from '../pessoas/CursosTreinamentoTab';
import { MatrizTreinamentoTab } from '../pessoas/MatrizTreinamentoTab';
import { TurmasTab } from './TurmasTab';
import { CursosObrigatoriosObraTab } from './CursosObrigatoriosObraTab';
import { RegrasCursoObrigatorioTab } from './RegrasCursoObrigatorioTab';

type AbaTreinamentos = 'turmas' | 'certificados' | 'cursos' | 'obrigatorios' | 'regras' | 'matriz';

const ABAS: AbaTreinamentos[] = ['turmas', 'certificados', 'cursos', 'obrigatorios', 'regras', 'matriz'];

// Item "Treinamentos" da sidebar (02/09): saiu de dentro de PessoasPage (onde só cabia por
// conveniência, ao lado de Trabalhadores/Funções, que não têm nada a ver) e virou módulo próprio —
// cada item da sidebar deve abrir só o que é dele. Matriz de Treinamento por Função fica junto
// porque não tem link próprio e só faz sentido junto de Treinamentos. Aba "Turmas" (04/09, pedido
// do usuário): reformulação do fluxo de realização — turma com participantes pré-selecionados,
// presença biométrica, fotos obrigatórias e encerramento com certificado individual automático.
// Onda 2 Task 21 (camada ui/, conversões 3 e 7): Text size={500} → PageHeader.titulo; TabList/
// usePillTabStyles+useSubTabStyles → Abas + useAbaNaUrl, mesmo padrão de EpiPage.tsx (piloto 1).
export function TreinamentosPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaTreinamentos>('aba', ABAS, 'turmas');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Treinamentos" />}

      <DashboardTopo>
        <TreinamentosDashboardTab />
      </DashboardTopo>

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Treinamentos"
        abas={[
          { valor: 'turmas', rotulo: 'Turmas' },
          { valor: 'certificados', rotulo: 'Certificados' },
          { valor: 'cursos', rotulo: 'Catálogo de Cursos' },
          // Cursos obrigatórios por obra a partir do PGR (21/10). A matriz global continua valendo
          // para a obra que ainda não tem nenhum curso aprovado por esse fluxo.
          { valor: 'obrigatorios', rotulo: 'Cursos obrigatórios por obra' },
          { valor: 'regras', rotulo: 'Regras por NR' },
          { valor: 'matriz', rotulo: 'Matriz de Treinamento por Função' },
        ]}
      />

      {aba === 'turmas' && <TurmasTab />}
      {aba === 'certificados' && <CertificadosTab />}
      {aba === 'cursos' && <CursosTreinamentoTab />}
      {aba === 'obrigatorios' && <CursosObrigatoriosObraTab />}
      {aba === 'regras' && <RegrasCursoObrigatorioTab />}
      {aba === 'matriz' && <MatrizTreinamentoTab />}
    </div>
  );
}
