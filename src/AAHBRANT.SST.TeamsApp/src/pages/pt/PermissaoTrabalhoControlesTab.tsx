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
import { api, type NovaPermissaoTrabalhoControle, type PermissaoTrabalhoControle } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function controleVazio(permissaoTrabalhoId: string): NovaPermissaoTrabalhoControle {
  return { permissaoTrabalhoId, descricao: '' };
}

export function PermissaoTrabalhoControlesTab({ permissaoTrabalhoId }: { permissaoTrabalhoId: string }) {
  const estilos = usePageStyles();
  const [controles, setControles] = useState<PermissaoTrabalhoControle[]>([]);
  const [novoControle, setNovoControle] = useState<NovaPermissaoTrabalhoControle>(() =>
    controleVazio(permissaoTrabalhoId),
  );
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setControles(await api.permissaoTrabalhoControles.listar(permissaoTrabalhoId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar controles.');
    }
  }

  useEffect(() => {
    carregar();
    setNovoControle(controleVazio(permissaoTrabalhoId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [permissaoTrabalhoId]);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.permissaoTrabalhoControles.criar(novoControle);
      setNovoControle(controleVazio(permissaoTrabalhoId));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar controle.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.permissaoTrabalhoControles.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir controle.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Controles / medidas de segurança</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Descrição do controle">
          <Input
            value={novoControle.descricao}
            onChange={(_, d) => setNovoControle({ ...novoControle, descricao: d.value })}
          />
        </Field>
      </div>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar controle
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {controles.map((controle) => (
            <TableRow key={controle.id}>
              <TableCell>{controle.descricao}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(controle.id)}
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
