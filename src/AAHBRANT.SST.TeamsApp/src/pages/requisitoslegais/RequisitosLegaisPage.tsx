import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { RequisitosLegaisTab } from './RequisitosLegaisTab';
import { QuestionarioAplicabilidadeTab } from './QuestionarioAplicabilidadeTab';

const ABAS = ['requisitos', 'questionario'] as const;
type AbaRequisitosLegais = (typeof ABAS)[number];

// Módulo de Requisitos Legais — Motor de Aplicabilidade Legal (requisito do usuário, 2026-08-29).
// Fase 1 (fundação de dados): cadastro dos requisitos/critérios e do questionário de aplicabilidade
// por obra. O cruzamento automático (o "motor" em si, que decide Aplicável/Não aplicável/Em análise
// por obra e gera as obrigações derivadas) é uma fase seguinte, ainda não implementada.
// Onda 2 Task 18 (camada ui/): mesmo padrão de EpiPage.tsx (piloto 1) — aba sincronizada com a URL
// via useAbaNaUrl('aba', ...), nível de Abas trocando conforme a página é raiz ou aninhada em
// GestaoSstPage.tsx (que renderiza com mostrarTitulo={false}).
export function RequisitosLegaisPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaRequisitosLegais>('aba', ABAS, 'requisitos');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Requisitos Legais" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Requisitos Legais"
        abas={[
          { valor: 'requisitos', rotulo: 'Requisitos e critérios' },
          { valor: 'questionario', rotulo: 'Questionário de aplicabilidade' },
        ]}
      />

      {aba === 'requisitos' && <RequisitosLegaisTab />}
      {aba === 'questionario' && <QuestionarioAplicabilidadeTab />}
    </div>
  );
}
