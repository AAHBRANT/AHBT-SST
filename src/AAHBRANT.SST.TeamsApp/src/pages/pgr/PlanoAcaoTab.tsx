import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  StatusControleRisco,
  statusControleRiscoLabel,
  type NovoPlanoAcaoItem,
  type PlanoAcaoItem,
  type RiscoClassificado,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function itemVazio(pgrId: string): NovoPlanoAcaoItem {
  return {
    pgrId,
    riscoId: null,
    descricao: '',
    responsavelUsuarioId: null,
    prazo: null,
    status: StatusControleRisco.Pendente,
  };
}

const corBadgeStatus: Record<number, 'informative' | 'warning' | 'success'> = {
  1: 'informative',
  2: 'warning',
  3: 'success',
};

export function PlanoAcaoTab({ pgrId, riscosDisponiveis }: { pgrId: string; riscosDisponiveis: RiscoClassificado[] }) {
  const estilos = usePageStyles();
  const [itens, setItens] = useState<PlanoAcaoItem[]>([]);
  const [novoItem, setNovoItem] = useState<NovoPlanoAcaoItem>(() => itemVazio(pgrId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setItens(await api.planoAcao.listar(pgrId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar plano de ação.');
    }
  }

  useEffect(() => {
    carregar();
    setNovoItem(itemVazio(pgrId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pgrId]);

  function nomePerigo(riscoId?: string | null) {
    if (!riscoId) return '—';
    return riscosDisponiveis.find((r) => r.riscoId === riscoId)?.perigoNome ?? riscoId;
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.planoAcao.criar(novoItem);
      setNovoItem(itemVazio(pgrId));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar item do plano de ação.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.planoAcao.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir item do plano de ação.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Plano de ação</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Risco relacionado">
          <Select
            value={novoItem.riscoId ?? ''}
            onChange={(_, d) => setNovoItem({ ...novoItem, riscoId: d.value || null })}
          >
            <option value="">Nenhum</option>
            {riscosDisponiveis.map((risco) => (
              <option key={risco.riscoId} value={risco.riscoId}>
                {risco.perigoNome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Descrição da ação">
          <Input
            value={novoItem.descricao}
            onChange={(_, d) => setNovoItem({ ...novoItem, descricao: d.value })}
          />
        </Field>
        <Field label="Prazo">
          <Input
            type="date"
            value={novoItem.prazo ?? ''}
            onChange={(_, d) => setNovoItem({ ...novoItem, prazo: d.value || null })}
          />
        </Field>
        <Field label="Status">
          <Select
            value={novoItem.status}
            onChange={(_, d) => setNovoItem({ ...novoItem, status: Number(d.value) })}
          >
            {Object.entries(statusControleRiscoLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar item
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell>Risco</TableHeaderCell>
            <TableHeaderCell>Prazo</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {itens.map((item) => (
            <TableRow key={item.id}>
              <TableCell>{item.descricao}</TableCell>
              <TableCell>{nomePerigo(item.riscoId)}</TableCell>
              <TableCell>{item.prazo?.slice(0, 10)}</TableCell>
              <TableCell>
                <Badge color={corBadgeStatus[item.status]} appearance="tint">
                  {statusControleRiscoLabel[item.status]}
                </Badge>
              </TableCell>
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
    </div>
  );
}
