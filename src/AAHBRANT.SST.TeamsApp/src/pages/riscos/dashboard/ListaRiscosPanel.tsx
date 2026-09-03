import {
  Badge,
  Button,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Delete24Regular } from '@fluentui/react-icons';
import { api, nivelRiscoLabel, type Atividade, type Perigo, type Risco } from '../../../lib/api';
import { usePageStyles } from '../../pageStyles';
import { useConfirmarExclusao } from '../../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../../hooks/useSucessoToast';
import { EstadoVazio } from '../../../components/EstadoVazio';

interface ListaRiscosPanelProps {
  riscos: Risco[];
  atividades: Atividade[];
  perigos: Perigo[];
  aoExcluir: () => void;
}

const corNivel: Record<number, 'informative' | 'success' | 'warning' | 'severe' | 'danger'> = {
  1: 'informative',
  2: 'success',
  3: 'warning',
  4: 'severe',
  5: 'danger',
};

export function ListaRiscosPanel({ riscos, atividades, perigos, aoExcluir }: ListaRiscosPanelProps) {
  const estilos = usePageStyles();
  const { confirmar, dialogElement } = useConfirmarExclusao();
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

  return (
    <div className={estilos.card} style={{ marginTop: 16 }}>
      {dialogElement}
      <Text weight="semibold">Todos os riscos avaliados ({riscos.length})</Text>
      <div style={{ maxHeight: 480, overflowY: 'auto', marginTop: 12 }}>
        {ordenados.length === 0 ? (
          <EstadoVazio mensagem="Nenhum risco avaliado para os filtros selecionados." />
        ) : (
          <Table noNativeElements>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Atividade</TableHeaderCell>
                <TableHeaderCell>Perigo</TableHeaderCell>
                <TableHeaderCell>Nível</TableHeaderCell>
                <TableHeaderCell></TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {ordenados.map((risco) => (
                <TableRow key={risco.id}>
                  <TableCell>{nomeAtividade(risco.atividadeId)}</TableCell>
                  <TableCell>{nomePerigo(risco.perigoId)}</TableCell>
                  <TableCell>
                    <Badge appearance="tint" color={corNivel[risco.nivelRisco]}>
                      {nivelRiscoLabel[risco.nivelRisco]}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={() => excluir(risco.id)}
                      aria-label="Excluir"
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </div>
  );
}
