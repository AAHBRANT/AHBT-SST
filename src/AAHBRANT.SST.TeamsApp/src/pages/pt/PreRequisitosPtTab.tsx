import { useEffect, useState } from 'react';
import { Checkbox, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { api, itemPreRequisitoPtLabel, type PermissaoTrabalhoPreRequisito } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// §2 do formulário — 6 itens fixos, já semeados na criação da PT (ver disclosure em
// CriarPermissaoTrabalhoCommand.cs). Este comando só alterna Atendido, não cria/exclui linha.
export function PreRequisitosPtTab({
  permissaoTrabalhoId,
  itens,
  aoAtualizar,
}: {
  permissaoTrabalhoId: string;
  itens: PermissaoTrabalhoPreRequisito[];
  aoAtualizar: () => Promise<void>;
}) {
  const estilos = usePageStyles();
  const [erro, setErro] = useState<string | null>(null);
  const [processandoId, setProcessandoId] = useState<string | null>(null);

  useEffect(() => setErro(null), [permissaoTrabalhoId]);

  async function alternar(item: PermissaoTrabalhoPreRequisito, atendido: boolean) {
    try {
      setProcessandoId(item.id);
      setErro(null);
      await api.permissoesTrabalho.marcarPreRequisito(permissaoTrabalhoId, item.id, atendido);
      await aoAtualizar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao marcar pré-requisito.');
    } finally {
      setProcessandoId(null);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Pré-requisitos para liberação</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Atendido</TableHeaderCell>
            <TableHeaderCell>Item</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {itens.map((item) => (
            <TableRow key={item.id}>
              <TableCell>
                <Checkbox
                  checked={item.atendido}
                  disabled={processandoId === item.id}
                  onChange={(_, d) => alternar(item, !!d.checked)}
                />
              </TableCell>
              <TableCell>{itemPreRequisitoPtLabel[item.item]}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
