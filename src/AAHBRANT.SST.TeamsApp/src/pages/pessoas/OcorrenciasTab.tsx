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
  gravidadeAcidenteLabel,
  statusAcidenteLabel,
  tipoOcorrenciaLabel,
  type OcorrenciaPerfil,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const corBadgeGravidade: Record<number, 'success' | 'warning' | 'severe' | 'danger'> = {
  1: 'success',
  2: 'warning',
  3: 'severe',
  4: 'severe',
  5: 'danger',
};

export function OcorrenciasTab({ ocorrencias }: { ocorrencias: OcorrenciaPerfil[] }) {
  const estilos = usePageStyles();

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Ocorrências registradas</Text>
      </div>

      {ocorrencias.length === 0 ? (
        <Text>Nenhuma ocorrência registrada para este trabalhador.</Text>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Data</TableHeaderCell>
              <TableHeaderCell>Local</TableHeaderCell>
              <TableHeaderCell>Gravidade</TableHeaderCell>
              <TableHeaderCell>Dias afastado</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {ocorrencias.map((ocorrencia) => (
              <TableRow key={ocorrencia.id}>
                <TableCell>{tipoOcorrenciaLabel[ocorrencia.tipo]}</TableCell>
                <TableCell>{ocorrencia.data?.slice(0, 10)}</TableCell>
                <TableCell>{ocorrencia.local}</TableCell>
                <TableCell>
                  <Badge color={corBadgeGravidade[ocorrencia.gravidade]} appearance="tint">
                    {gravidadeAcidenteLabel[ocorrencia.gravidade]}
                  </Badge>
                </TableCell>
                <TableCell>{ocorrencia.houveAfastamento ? ocorrencia.diasAfastamento ?? 0 : '—'}</TableCell>
                <TableCell>{statusAcidenteLabel[ocorrencia.status]}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
