import { Card, DataTable, EstadoVazio, StatusChip, type Coluna, type Tom } from '@ui';
import { nivelRiscoLabel, statusControleRiscoLabel, type AtividadeCaracterizada, type RiscoClassificado } from '../../lib/api';

// Guia de conversão item 5 (mapeamento 1:1 pelo nome semântico do Fluent, preservando as mesmas
// cores da versão anterior): success→ok, informative→info, warning→atencao, severe→alerta,
// danger→alerta (5 valores do Fluent colapsam nos 4 tons de StatusChip; o rótulo textual continua
// distinguindo severo de perigoso, só a intensidade de cor colide — mesmo padrão já usado em
// AprEtapasTab.tsx/nivelRiscoAprCor).
const tomPorNivel: Record<number, Tom> = {
  1: 'ok',
  2: 'info',
  3: 'atencao',
  4: 'alerta',
  5: 'alerta',
};

const colunasRiscos: Coluna<RiscoClassificado>[] = [
  { chave: 'perigo', rotulo: 'Perigo', render: (r) => r.perigoNome },
  {
    chave: 'nivel',
    rotulo: 'Nível de risco',
    render: (r) => <StatusChip tom={tomPorNivel[r.nivelRisco] ?? 'neutro'}>{nivelRiscoLabel[r.nivelRisco]}</StatusChip>,
  },
  { chave: 'controlesExistentes', rotulo: 'Controles existentes', render: (r) => r.controlesExistentes ?? '' },
  { chave: 'controlesAdicionais', rotulo: 'Controles adicionais', render: (r) => r.controlesAdicionais ?? '' },
  { chave: 'status', rotulo: 'Status', render: (r) => statusControleRiscoLabel[r.status] },
];

// Onda 2 Task 8 (camada ui/): caracterização das atividades / inventário e classificação de riscos
// (§16) — leitura agregada dos registros de Atividade/Risco já cadastrados para a Obra do PGR, sem
// CRUD próprio aqui. Uma DataTable por atividade (cada atividade já era um <Table> próprio antes da
// migração), sem `aoClicarLinha`/`acoesLinha` — é só leitura.
export function InventarioTab({ atividades }: { atividades: AtividadeCaracterizada[] }) {
  if (atividades.length === 0) {
    return <EstadoVazio titulo="Nenhuma atividade com avaliação de risco cadastrada para a obra deste PGR." />;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {atividades.map((atividade) => (
        <Card key={atividade.atividadeId} titulo={atividade.atividadeNome}>
          <DataTable
            aria-label={`Riscos da atividade ${atividade.atividadeNome}`}
            colunas={colunasRiscos}
            linhas={atividade.riscos}
            chaveLinha={(r) => r.riscoId}
            vazio={{ titulo: 'Nenhum risco classificado para esta atividade.' }}
          />
        </Card>
      ))}
    </div>
  );
}
