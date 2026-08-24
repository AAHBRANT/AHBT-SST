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
import { nivelRiscoLabel, statusControleRiscoLabel, type AtividadeCaracterizada } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const corBadgeNivel: Record<number, 'success' | 'informative' | 'warning' | 'severe' | 'danger'> = {
  1: 'success',
  2: 'informative',
  3: 'warning',
  4: 'severe',
  5: 'danger',
};

// Caracterização das atividades / inventário e classificação de riscos (§16) — leitura agregada
// dos registros de Atividade/Risco já cadastrados para a Obra do PGR, sem CRUD próprio aqui.
export function InventarioTab({ atividades }: { atividades: AtividadeCaracterizada[] }) {
  const estilos = usePageStyles();

  if (atividades.length === 0) {
    return (
      <div className={estilos.card}>
        <Text>Nenhuma atividade com avaliação de risco cadastrada para a obra deste PGR.</Text>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {atividades.map((atividade) => (
        <div key={atividade.atividadeId} className={estilos.card}>
          <div className={estilos.toolbar}>
            <Text weight="semibold">{atividade.atividadeNome}</Text>
          </div>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Perigo</TableHeaderCell>
                <TableHeaderCell>Nível de risco</TableHeaderCell>
                <TableHeaderCell>Controles existentes</TableHeaderCell>
                <TableHeaderCell>Controles adicionais</TableHeaderCell>
                <TableHeaderCell>Status</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {atividade.riscos.map((risco) => (
                <TableRow key={risco.riscoId}>
                  <TableCell>{risco.perigoNome}</TableCell>
                  <TableCell>
                    <Badge color={corBadgeNivel[risco.nivelRisco]} appearance="tint">
                      {nivelRiscoLabel[risco.nivelRisco]}
                    </Badge>
                  </TableCell>
                  <TableCell>{risco.controlesExistentes}</TableCell>
                  <TableCell>{risco.controlesAdicionais}</TableCell>
                  <TableCell>{statusControleRiscoLabel[risco.status]}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ))}
    </div>
  );
}
