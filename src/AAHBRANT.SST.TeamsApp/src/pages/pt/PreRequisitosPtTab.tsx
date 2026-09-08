import { useEffect, useState } from 'react';
import { Card, Checkbox, DataTable, FeedbackInline, type Coluna } from '@ui';
import { api, itemPreRequisitoPtLabel, type PermissaoTrabalhoPreRequisito } from '../../lib/api';

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

  const colunas: Coluna<PermissaoTrabalhoPreRequisito>[] = [
    {
      chave: 'atendido',
      rotulo: 'Atendido',
      render: (item) => (
        <Checkbox
          checked={item.atendido}
          disabled={processandoId === item.id}
          onChange={(_, d) => alternar(item, !!d.checked)}
        />
      ),
    },
    { chave: 'item', rotulo: 'Item', render: (item) => itemPreRequisitoPtLabel[item.item] },
  ];

  return (
    <Card titulo="Pré-requisitos para liberação">
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <DataTable
        aria-label="Pré-requisitos para liberação"
        colunas={colunas}
        linhas={itens}
        chaveLinha={(item) => item.id}
        vazio={{ titulo: 'Nenhum pré-requisito cadastrado.' }}
      />
    </Card>
  );
}
