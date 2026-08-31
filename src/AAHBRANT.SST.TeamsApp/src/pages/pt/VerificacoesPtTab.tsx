import { useState } from 'react';
import {
  Badge,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import {
  RespostaVerificacaoPt,
  api,
  itemVerificacaoPtLabel,
  respostaVerificacaoPtLabel,
  type PermissaoTrabalhoVerificacao,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const corBadgeResposta: Record<number, 'success' | 'danger' | 'subtle'> = {
  [RespostaVerificacaoPt.Conforme]: 'success',
  [RespostaVerificacaoPt.NaoConforme]: 'danger',
  [RespostaVerificacaoPt.NaoAplicavel]: 'subtle',
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
  const estilos = usePageStyles();
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

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Verificações pré-início</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Item</TableHeaderCell>
            <TableHeaderCell>Resposta</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {itens.map((item) => (
            <TableRow key={item.id}>
              <TableCell>{itemVerificacaoPtLabel[item.item]}</TableCell>
              <TableCell>
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
                    <Badge appearance="tint" color={corBadgeResposta[item.resposta]}>
                      {respostaVerificacaoPtLabel[item.resposta]}
                    </Badge>
                  )}
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
