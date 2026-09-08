import { useState } from 'react';
import { Card, DataTable, FeedbackInline, Select, StatusChip, type Coluna, type Tom } from '@ui';
import {
  RespostaVerificacaoPt,
  api,
  itemVerificacaoPtLabel,
  respostaVerificacaoPtLabel,
  type PermissaoTrabalhoVerificacao,
} from '../../lib/api';

// Mapeamento 1:1 pelo nome semântico do Fluent (Guia de conversão item 5): success→ok, danger→alerta,
// subtle→neutro.
const tomPorResposta: Record<number, Tom> = {
  [RespostaVerificacaoPt.Conforme]: 'ok',
  [RespostaVerificacaoPt.NaoConforme]: 'alerta',
  [RespostaVerificacaoPt.NaoAplicavel]: 'neutro',
};

// §4 do formulário — 15 itens fixos, já semeados na criação da PT. Qualquer item marcado Não
// Conforme bloqueia a liberação (ver disclosure em AutorizarPermissaoTrabalhoCommand.cs) — regra de
// ouro literal do documento.
export function VerificacoesPtTab({
  permissaoTrabalhoId,
  itens,
  aoAtualizar,
}: {
  permissaoTrabalhoId: string;
  itens: PermissaoTrabalhoVerificacao[];
  aoAtualizar: () => Promise<void>;
}) {
  const [erro, setErro] = useState<string | null>(null);
  const [processandoId, setProcessandoId] = useState<string | null>(null);

  async function responder(item: PermissaoTrabalhoVerificacao, resposta: number) {
    try {
      setProcessandoId(item.id);
      setErro(null);
      await api.permissoesTrabalho.responderVerificacao(permissaoTrabalhoId, item.id, resposta);
      await aoAtualizar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao responder verificação.');
    } finally {
      setProcessandoId(null);
    }
  }

  const colunas: Coluna<PermissaoTrabalhoVerificacao>[] = [
    { chave: 'item', rotulo: 'Item', render: (item) => itemVerificacaoPtLabel[item.item] },
    {
      chave: 'resposta',
      rotulo: 'Resposta',
      render: (item) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Select
            value={item.resposta ?? ''}
            disabled={processandoId === item.id}
            onChange={(_, d) => responder(item, Number(d.value))}
          >
            <option value="">Não respondido</option>
            {Object.entries(respostaVerificacaoPtLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
          {item.resposta != null && (
            <StatusChip tom={tomPorResposta[item.resposta] ?? 'neutro'}>
              {respostaVerificacaoPtLabel[item.resposta]}
            </StatusChip>
          )}
        </div>
      ),
    },
  ];

  return (
    <Card titulo="Verificações pré-início">
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <DataTable
        aria-label="Verificações pré-início"
        colunas={colunas}
        linhas={itens}
        chaveLinha={(item) => item.id}
        vazio={{ titulo: 'Nenhuma verificação cadastrada.' }}
      />
    </Card>
  );
}
