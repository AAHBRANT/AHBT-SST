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
import { api, type Atividade, type NovaAtividade, type Obra } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const atividadeVazia: NovaAtividade = { obraId: '', nome: '', descricao: '' };

export function AtividadesTab() {
  const estilos = usePageStyles();
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novaAtividade, setNovaAtividade] = useState<NovaAtividade>(atividadeVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [ativs, obrs] = await Promise.all([api.atividades.listar(), api.obras.listar()]);
      setAtividades(ativs);
      setObras(obrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar atividades.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.atividades.criar(novaAtividade);
      setNovaAtividade(atividadeVazia);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar atividade.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.atividades.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir atividade.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Atividades cadastradas</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Obra">
          <Select
            value={novaAtividade.obraId}
            onChange={(_, d) => setNovaAtividade({ ...novaAtividade, obraId: d.value })}
          >
            <option value="">Selecione</option>
            {obras.map((obra) => (
              <option key={obra.id} value={obra.id}>
                {obra.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Nome da atividade">
          <Input
            value={novaAtividade.nome}
            onChange={(_, d) => setNovaAtividade({ ...novaAtividade, nome: d.value })}
          />
        </Field>
        <Field label="Descrição">
          <Input
            value={novaAtividade.descricao ?? ''}
            onChange={(_, d) => setNovaAtividade({ ...novaAtividade, descricao: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar atividade
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Obra</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {atividades.map((atividade) => (
            <TableRow key={atividade.id}>
              <TableCell>{atividade.nome}</TableCell>
              <TableCell>{nomeObra(atividade.obraId)}</TableCell>
              <TableCell>{atividade.descricao}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(atividade.id)}
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
