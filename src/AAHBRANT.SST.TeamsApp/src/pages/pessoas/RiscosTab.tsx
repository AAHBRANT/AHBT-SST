import {
  Badge,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import {
  nivelRiscoLabel,
  statusControleRiscoLabel,
  type RiscoExpostoPerfil,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const corBadgeNivel: Record<number, 'success' | 'informative' | 'warning' | 'severe' | 'danger'> = {
  1: 'success',
  2: 'informative',
  3: 'warning',
  4: 'severe',
  5: 'danger',
};

export function RiscosTab({ riscos }: { riscos: RiscoExpostoPerfil[] }) {
  const estilos = usePageStyles();

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Riscos expostos (PGR)</Text>
      </div>

      {riscos.length === 0 ? (
        <Text>Nenhum risco vinculado a este trabalhador.</Text>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Perigo</TableHeaderCell>
              <TableHeaderCell>Atividade</TableHeaderCell>
              <TableHeaderCell>Ambiente</TableHeaderCell>
              <TableHeaderCell>Consequência</TableHeaderCell>
              <TableHeaderCell>P × S</TableHeaderCell>
              <TableHeaderCell>Nível de risco</TableHeaderCell>
              <TableHeaderCell>Controles</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {riscos.map((risco) => (
              <TableRow key={risco.riscoId}>
                <TableCell>{risco.perigoNome}</TableCell>
                <TableCell>{risco.atividadeNome}</TableCell>
                <TableCell>{risco.ambiente ?? '—'}</TableCell>
                <TableCell>{risco.consequencia ?? '—'}</TableCell>
                <TableCell>
                  {risco.probabilidade} × {risco.severidade}
                </TableCell>
                <TableCell>
                  <Badge color={corBadgeNivel[risco.nivelRisco]} appearance="tint">
                    {nivelRiscoLabel[risco.nivelRisco]}
                  </Badge>
                </TableCell>
                <TableCell>
                  {risco.controlesExistentes ?? '—'}
                  {risco.controlesAdicionais ? ` / ${risco.controlesAdicionais}` : ''}
                </TableCell>
                <TableCell>{statusControleRiscoLabel[risco.status]}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
