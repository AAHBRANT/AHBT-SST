import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Checkbox,
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
import { api, type CatalogoEpi, type EntregaEpi, type NovaEntregaEpi } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function entregaVazia(trabalhadorId: string): NovaEntregaEpi {
  return {
    trabalhadorId,
    catalogoEpiId: '',
    dataEntrega: '',
    dataDevolucao: '',
    dataValidade: '',
    assinaturaColetada: false,
  };
}

export function EntregasEpiTab({ trabalhadorId }: { trabalhadorId: string }) {
  const estilos = usePageStyles();
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [novaEntrega, setNovaEntrega] = useState<NovaEntregaEpi>(() => entregaVazia(trabalhadorId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaEpis] = await Promise.all([
        api.entregasEpi.listar(trabalhadorId),
        api.catalogosEpi.listar(),
      ]);
      setEntregas(lista);
      setEpis(listaEpis);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de EPI.');
    }
  }

  function nomeEpi(id: string) {
    return epis.find((e) => e.id === id)?.nome ?? id;
  }

  function vencido(dataValidade?: string | null) {
    if (!dataValidade) return false;
    return new Date(dataValidade) < new Date(new Date().toDateString());
  }

  useEffect(() => {
    carregar();
    setNovaEntrega(entregaVazia(trabalhadorId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.entregasEpi.criar(novaEntrega);
      setNovaEntrega(entregaVazia(trabalhadorId));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar entrega de EPI.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.entregasEpi.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir entrega de EPI.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Entregas de EPI do trabalhador</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="EPI">
          <Select
            value={novaEntrega.catalogoEpiId}
            onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoEpiId: d.value })}
          >
            <option value="">Selecione</option>
            {epis.map((e) => (
              <option key={e.id} value={e.id}>
                {e.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Data de entrega">
          <Input
            type="date"
            value={novaEntrega.dataEntrega}
            onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataEntrega: d.value })}
          />
        </Field>
        <Field label="Validade">
          <Input
            type="date"
            value={novaEntrega.dataValidade ?? ''}
            onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataValidade: d.value })}
          />
        </Field>
        <Field label="Data de devolução">
          <Input
            type="date"
            value={novaEntrega.dataDevolucao ?? ''}
            onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataDevolucao: d.value })}
          />
        </Field>
        <Field label="Assinatura coletada">
          <Checkbox
            checked={novaEntrega.assinaturaColetada}
            onChange={(_, d) => setNovaEntrega({ ...novaEntrega, assinaturaColetada: !!d.checked })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar entrega
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>EPI</TableHeaderCell>
            <TableHeaderCell>Entrega</TableHeaderCell>
            <TableHeaderCell>Validade</TableHeaderCell>
            <TableHeaderCell>Devolução</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {entregas.map((entrega) => (
            <TableRow key={entrega.id}>
              <TableCell>{nomeEpi(entrega.catalogoEpiId)}</TableCell>
              <TableCell>{entrega.dataEntrega?.slice(0, 10)}</TableCell>
              <TableCell>
                {entrega.dataValidade?.slice(0, 10)}
                {vencido(entrega.dataValidade) && (
                  <Badge color="danger" appearance="tint" style={{ marginLeft: 8 }}>
                    Vencido
                  </Badge>
                )}
              </TableCell>
              <TableCell>{entrega.dataDevolucao?.slice(0, 10)}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(entrega.id)}
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
