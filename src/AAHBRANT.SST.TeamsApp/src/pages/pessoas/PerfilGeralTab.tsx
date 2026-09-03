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
import { resultadoAsoLabel, tipoExameAsoLabel, type Aso } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const corResultado: Record<number, 'success' | 'warning' | 'danger' | 'informative'> = {
  1: 'success',
  2: 'warning',
  3: 'danger',
  4: 'informative',
};

function vencido(dataValidade: string) {
  return new Date(dataValidade) < new Date(new Date().toDateString());
}

export function PerfilGeralTab({ asos }: { asos: Aso[] }) {
  const estilos = usePageStyles();

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Histórico de ASOs</Text>
      </div>

      {asos.length === 0 ? (
        <Text>Nenhum ASO registrado.</Text>
      ) : (
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Exame</TableHeaderCell>
              <TableHeaderCell>Validade</TableHeaderCell>
              <TableHeaderCell>Resultado</TableHeaderCell>
              <TableHeaderCell>Médico</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {asos.map((aso) => (
              <TableRow key={aso.id}>
                <TableCell>{tipoExameAsoLabel[aso.tipo]}</TableCell>
                <TableCell>{aso.dataExame?.slice(0, 10)}</TableCell>
                <TableCell>
                  {aso.dataValidade?.slice(0, 10)}
                  {vencido(aso.dataValidade) && (
                    <Badge color="danger" appearance="tint" style={{ marginLeft: 8 }}>
                      Vencido
                    </Badge>
                  )}
                </TableCell>
                <TableCell>
                  <Badge color={corResultado[aso.resultadoStatus]} appearance="tint">
                    {resultadoAsoLabel[aso.resultadoStatus]}
                  </Badge>
                </TableCell>
                <TableCell>
                  {aso.medicoNome ?? '—'}
                  {aso.medicoCrm ? ` (CRM ${aso.medicoCrm})` : ''}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
