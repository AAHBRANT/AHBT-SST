import { useEffect, useState } from 'react';
import {
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
import { api, statusObraLabel, StatusObra, type NovaObra, type Obra } from '../lib/api';
import { usePageStyles } from './pageStyles';

const obraVazia: NovaObra = {
  codigo: '',
  nome: '',
  cliente: '',
  status: StatusObra.Planejada,
  dataInicio: '',
  dataPrevisaoTermino: '',
  endereco: '',
  cidade: '',
  uf: '',
};

export function ObrasPage() {
  const estilos = usePageStyles();
  const [obras, setObras] = useState<Obra[]>([]);
  const [novaObra, setNovaObra] = useState<NovaObra>(obraVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setObras(await api.obras.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar obras.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.obras.criar({
        ...novaObra,
        dataInicio: novaObra.dataInicio || null,
        dataPrevisaoTermino: novaObra.dataPrevisaoTermino || null,
      });
      setNovaObra(obraVazia);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar obra.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.obras.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir obra.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Obras cadastradas</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Código">
          <Input value={novaObra.codigo} onChange={(_, d) => setNovaObra({ ...novaObra, codigo: d.value })} />
        </Field>
        <Field label="Nome">
          <Input value={novaObra.nome} onChange={(_, d) => setNovaObra({ ...novaObra, nome: d.value })} />
        </Field>
        <Field label="Cliente">
          <Input
            value={novaObra.cliente ?? ''}
            onChange={(_, d) => setNovaObra({ ...novaObra, cliente: d.value })}
          />
        </Field>
        <Field label="Status">
          <Select
            value={novaObra.status}
            onChange={(_, d) => setNovaObra({ ...novaObra, status: Number(d.value) })}
          >
            {Object.entries(statusObraLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Data de início">
          <Input
            type="date"
            value={novaObra.dataInicio ?? ''}
            onChange={(_, d) => setNovaObra({ ...novaObra, dataInicio: d.value })}
          />
        </Field>
        <Field label="Previsão de término">
          <Input
            type="date"
            value={novaObra.dataPrevisaoTermino ?? ''}
            onChange={(_, d) => setNovaObra({ ...novaObra, dataPrevisaoTermino: d.value })}
          />
        </Field>
        <Field label="Endereço">
          <Input
            value={novaObra.endereco ?? ''}
            onChange={(_, d) => setNovaObra({ ...novaObra, endereco: d.value })}
          />
        </Field>
        <Field label="Cidade">
          <Input value={novaObra.cidade ?? ''} onChange={(_, d) => setNovaObra({ ...novaObra, cidade: d.value })} />
        </Field>
        <Field label="UF">
          <Input
            value={novaObra.uf ?? ''}
            maxLength={2}
            onChange={(_, d) => setNovaObra({ ...novaObra, uf: d.value.toUpperCase() })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar obra
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Código</TableHeaderCell>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Cliente</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell>Cidade/UF</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {obras.map((obra) => (
            <TableRow key={obra.id}>
              <TableCell>{obra.codigo}</TableCell>
              <TableCell>{obra.nome}</TableCell>
              <TableCell>{obra.cliente}</TableCell>
              <TableCell>{statusObraLabel[obra.status]}</TableCell>
              <TableCell>
                {obra.cidade}
                {obra.uf ? `/${obra.uf}` : ''}
              </TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(obra.id)}
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
