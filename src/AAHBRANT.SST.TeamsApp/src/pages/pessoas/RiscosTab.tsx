import { Card, DataTable, StatusChip, type Coluna, type Tom } from '@ui';
import {
  nivelRiscoLabel,
  statusControleRiscoLabel,
  type RiscoExpostoPerfil,
} from '../../lib/api';

// Nível de risco 1-5, mesmo mapeamento usado em InventarioTab.tsx (guia de conversão da Onda 2,
// seção "Badge→StatusChip"): a união de tons do Fluent (5 valores) não bate 1:1 com a de StatusChip,
// mapeada aqui por julgamento semântico, não por posição.
const tomPorNivel: Record<number, Tom> = {
  1: 'ok',
  2: 'info',
  3: 'atencao',
  4: 'alerta',
  5: 'alerta',
};

// Sub-aba somente-leitura de TrabalhadorDetalhePage (aba "Riscos & OS") — riscos do PGR aos quais o
// funcionário está exposto pela atividade/função. Camada ui/ (Onda 2, Task 1): Card com título de
// seção (não PageHeader — é conteúdo aninhado numa página que já tem cabeçalho próprio) + DataTable.
export function RiscosTab({ riscos }: { riscos: RiscoExpostoPerfil[] }) {
  const colunas: Coluna<RiscoExpostoPerfil>[] = [
    { chave: 'perigo', rotulo: 'Perigo', render: (r) => r.perigoNome },
    { chave: 'atividade', rotulo: 'Atividade', render: (r) => r.atividadeNome },
    { chave: 'ambiente', rotulo: 'Ambiente', render: (r) => r.ambiente ?? '—' },
    { chave: 'consequencia', rotulo: 'Consequência', render: (r) => r.consequencia ?? '—' },
    { chave: 'ps', rotulo: 'P × S', render: (r) => `${r.probabilidade} × ${r.severidade}` },
    {
      chave: 'nivel',
      rotulo: 'Nível de risco',
      render: (r) => (
        <StatusChip tom={tomPorNivel[r.nivelRisco] ?? 'neutro'}>{nivelRiscoLabel[r.nivelRisco]}</StatusChip>
      ),
    },
    {
      chave: 'controles',
      rotulo: 'Controles',
      render: (r) => `${r.controlesExistentes ?? '—'}${r.controlesAdicionais ? ` / ${r.controlesAdicionais}` : ''}`,
    },
    { chave: 'status', rotulo: 'Status', render: (r) => statusControleRiscoLabel[r.status] },
  ];

  return (
    <Card titulo="Riscos expostos (PGR)">
      <DataTable
        aria-label="Riscos expostos (PGR)"
        colunas={colunas}
        linhas={riscos}
        chaveLinha={(r) => r.riscoId}
        vazio={{ titulo: 'Nenhum risco vinculado a este funcionário.' }}
      />
    </Card>
  );
}
