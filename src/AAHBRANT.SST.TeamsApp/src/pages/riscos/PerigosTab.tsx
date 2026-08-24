import { useEffect, useState } from 'react';
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
import { api, type NovoPerigo, type Perigo } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const perigoVazio: NovoPerigo = { nome: '', agente: '', fonte: '', descricao: '' };

export function PerigosTab() {
  const estilos = usePageStyles();
  const [perigos, setPerigos] = useState<Perigo[]>([]);
  const [novoPerigo, setNovoPerigo] = useState<NovoPerigo>(perigoVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setPerigos(await api.perigos.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar perigos.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.perigos.criar(novoPerigo);
      setNovoPerigo(perigoVazio);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar perigo.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.perigos.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir perigo.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Perigos cadastrados</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={novoPerigo.nome} onChange={(_, d) => setNovoPerigo({ ...novoPerigo, nome: d.value })} />
        </Field>
        <Field label="Agente">
          <Input
            value={novoPerigo.agente ?? ''}
            onChange={(_, d) => setNovoPerigo({ ...novoPerigo, agente: d.value })}
          />
        </Field>
        <Field label="Fonte">
          <Input
            value={novoPerigo.fonte ?? ''}
            onChange={(_, d) => setNovoPerigo({ ...novoPerigo, fonte: d.value })}
          />
        </Field>
        <Field label="Descrição">
          <Input
            value={novoPerigo.descricao ?? ''}
            onChange={(_, d) => setNovoPerigo({ ...novoPerigo, descricao: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar perigo
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Agente</TableHeaderCell>
            <TableHeaderCell>Fonte</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {perigos.map((perigo) => (
            <TableRow key={perigo.id}>
              <TableCell>{perigo.nome}</TableCell>
              <TableCell>{perigo.agente}</TableCell>
              <TableCell>{perigo.fonte}</TableCell>
              <TableCell>{perigo.descricao}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(perigo.id)}
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
