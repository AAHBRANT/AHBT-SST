import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { CursosTreinamentoTab } from '../pessoas/CursosTreinamentoTab';
import { MatrizTreinamentoTab } from '../pessoas/MatrizTreinamentoTab';
import { TurmasTab } from './TurmasTab';

type AbaTreinamentos = 'turmas' | 'cursos' | 'matriz';

const ABAS: AbaTreinamentos[] = ['turmas', 'cursos', 'matriz'];

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

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Treinamentos"
        abas={[
          { valor: 'turmas', rotulo: 'Turmas' },
          { valor: 'cursos', rotulo: 'Catálogo de Cursos' },
          { valor: 'matriz', rotulo: 'Matriz de Treinamento por Função' },
        ]}
      />

      {aba === 'turmas' && <TurmasTab />}
      {aba === 'cursos' && <CursosTreinamentoTab />}
      {aba === 'matriz' && <MatrizTreinamentoTab />}
    </div>
  );
}
