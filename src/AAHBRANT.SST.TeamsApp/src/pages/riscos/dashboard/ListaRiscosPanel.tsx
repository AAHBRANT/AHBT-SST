import { Button, Card, DataTable, StatusChip, useConfirmar, type Coluna, type Tom } from '@ui';
import { Delete24Regular } from '@fluentui/react-icons';
import { api, nivelRiscoLabel, type Atividade, type Perigo, type Risco } from '../../../lib/api';
import { useSucessoToast } from '../../../hooks/useSucessoToast';

interface ListaRiscosPanelProps {
  riscos: Risco[];
  atividades: Atividade[];
  perigos: Perigo[];
  aoExcluir: () => void;
}

// Guia de conversão §5, mesmo padrão do item 5 (5 tons Fluent → 4 tons @ui, Alto/Crítico colapsados
// em "alerta").
const tomPorNivel: Record<number, Tom> = { 1: 'ok', 2: 'info', 3: 'atencao', 4: 'alerta', 5: 'alerta' };

// Camada ui/ (Onda 2, Task 13): painel de dashboard, lista simples de riscos avaliados. Table cru ->
// DataTable, Badge -> StatusChip, useConfirmarExclusao -> useConfirmar. Altura limitada + rolagem
// interna preservadas (mesmo precedente de painel de dashboard: RiscosCriticosPanel/AprVencidaPanel
// também limitam a lista a uma caixa com scroll próprio, em vez de deixar o dashboard crescer sem
// limite) — a caixa envolve o DataTable, já que a peça em si não tem essa opção embutida.
export function ListaRiscosPanel({ riscos, atividades, perigos, aoExcluir }: ListaRiscosPanelProps) {
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  const nomeAtividade = (id: string) => atividades.find((a) => a.id === id)?.nome ?? id;
  const nomePerigo = (id: string) => perigos.find((p) => p.id === id)?.nome ?? id;

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este risco? Essa ação não pode ser desfeita.'))) return;
    await api.riscos.excluir(id);
    sucessoToast('Risco excluído com sucesso.');
    aoExcluir();
  }

  const ordenados = [...riscos].sort((a, b) => b.nivelRisco - a.nivelRisco);

  const colunas: Coluna<Risco>[] = [
    { chave: 'atividade', rotulo: 'Atividade', render: (r) => nomeAtividade(r.atividadeId) },
    { chave: 'perigo', rotulo: 'Perigo', render: (r) => nomePerigo(r.perigoId) },
    {
      chave: 'nivel',
      rotulo: 'Nível',
      render: (r) => <StatusChip tom={tomPorNivel[r.nivelRisco]}>{nivelRiscoLabel[r.nivelRisco]}</StatusChip>,
    },
  ];

  return (
    <div style={{ marginTop: 16 }}>
      {dialogElement}
      <Card titulo={`Todos os riscos avaliados (${riscos.length})`}>
        <div style={{ maxHeight: 480, overflowY: 'auto', marginTop: 12 }}>
          <DataTable
            aria-label="Todos os riscos avaliados"
            colunas={colunas}
            linhas={ordenados}
            chaveLinha={(r) => r.id}
            vazio={{ titulo: 'Nenhum risco avaliado para os filtros selecionados.' }}
            acoesLinha={(r) => (
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(r.id)} aria-label="Excluir" />
            )}
          />
        </div>
      </Card>
    </div>
  );
}
