import { useState } from 'react';
import {
  Button,
  Field,
  Input,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type NovaPermissaoTrabalhoRiscoCritico, type PermissaoTrabalhoRiscoCritico } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';

function riscoVazio(permissaoTrabalhoId: string): NovaPermissaoTrabalhoRiscoCritico {
  return { permissaoTrabalhoId, riscoCondicao: '', controleComplementar: '', responsavelEvidencia: '' };
}

// §6 do formulário, "Riscos críticos e controles complementares" — tabela livre, sem catálogo fixo.
export function RiscosCriticosPtTab({
  permissaoTrabalhoId,
  itens,
  aoAtualizar,
}: {
  permissaoTrabalhoId: string;
  itens: PermissaoTrabalhoRiscoCritico[];
  aoAtualizar: () => Promise<void>;
}) {
  const estilos = usePageStyles();
  const [novoRisco, setNovoRisco] = useState<NovaPermissaoTrabalhoRiscoCritico>(() =>
    riscoVazio(permissaoTrabalhoId),
  );
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function criar() {
    if (!novoRisco.riscoCondicao.trim()) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.permissoesTrabalho.criarRiscoCritico({
        ...novoRisco,
        controleComplementar: novoRisco.controleComplementar || null,
        responsavelEvidencia: novoRisco.responsavelEvidencia || null,
      });
      setNovoRisco(riscoVazio(permissaoTrabalhoId));
      await aoAtualizar();
      sucessoToast('Risco crítico adicionado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao adicionar risco crítico.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este risco crítico? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.permissoesTrabalho.excluirRiscoCritico(id);
      await aoAtualizar();
      sucessoToast('Risco crítico excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir risco crítico.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Riscos críticos / controles complementares</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados do Risco Crítico</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col4}>
          <Field label="Risco / condição">
            <Input
              value={novoRisco.riscoCondicao}
              onChange={(_, d) => setNovoRisco({ ...novoRisco, riscoCondicao: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Controle complementar">
            <Input
              value={novoRisco.controleComplementar ?? ''}
              onChange={(_, d) => setNovoRisco({ ...novoRisco, controleComplementar: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Responsável / evidência">
            <Input
              value={novoRisco.responsavelEvidencia ?? ''}
              onChange={(_, d) => setNovoRisco({ ...novoRisco, responsavelEvidencia: d.value })}
            />
          </Field>
        </div>
      </div>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar risco crítico
        </Button>
      </div>

      {itens.length === 0 ? (
        <EstadoVazio mensagem="Nenhum risco crítico cadastrado ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Risco / condição</TableHeaderCell>
            <TableHeaderCell>Controle complementar</TableHeaderCell>
            <TableHeaderCell>Responsável / evidência</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {itens.map((item) => (
            <TableRow key={item.id}>
              <TableCell>{item.riscoCondicao}</TableCell>
              <TableCell>{item.controleComplementar ?? '-'}</TableCell>
              <TableCell>{item.responsavelEvidencia ?? '-'}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(item.id)}
                  aria-label="Excluir"
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      )}
    </div>
  );
}
