import { useEffect, useState } from 'react';
import {
  Button,
  Checkbox,
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
import { api, type NovaPermissaoTrabalhoRequisito, type PermissaoTrabalhoRequisito } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function requisitoVazio(permissaoTrabalhoId: string): NovaPermissaoTrabalhoRequisito {
  return { permissaoTrabalhoId, descricao: '' };
}

// Checklist de requisitos (não é literal de um catálogo pré-existente — ver disclosure em
// CriarPermissaoTrabalhoRequisitoCommand.cs). É o gate consultado por AutorizarPermissaoTrabalhoCommand:
// a PT só pode ser autorizada quando todos os requisitos estiverem marcados como atendidos.
export function PermissaoTrabalhoRequisitosTab({ permissaoTrabalhoId }: { permissaoTrabalhoId: string }) {
  const estilos = usePageStyles();
  const [requisitos, setRequisitos] = useState<PermissaoTrabalhoRequisito[]>([]);
  const [novoRequisito, setNovoRequisito] = useState<NovaPermissaoTrabalhoRequisito>(() =>
    requisitoVazio(permissaoTrabalhoId),
  );
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setRequisitos(await api.permissaoTrabalhoRequisitos.listar(permissaoTrabalhoId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar requisitos.');
    }
  }

  useEffect(() => {
    carregar();
    setNovoRequisito(requisitoVazio(permissaoTrabalhoId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [permissaoTrabalhoId]);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.permissaoTrabalhoRequisitos.criar(novoRequisito);
      setNovoRequisito(requisitoVazio(permissaoTrabalhoId));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar requisito.');
    } finally {
      setCarregando(false);
    }
  }

  async function alternarAtendido(requisito: PermissaoTrabalhoRequisito, atendido: boolean) {
    try {
      setErro(null);
      await api.permissaoTrabalhoRequisitos.marcar(requisito.id, atendido);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao marcar requisito.');
    }
  }

  async function excluir(id: string) {
    try {
      await api.permissaoTrabalhoRequisitos.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir requisito.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Requisitos para autorização</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Descrição do requisito">
          <Input
            value={novoRequisito.descricao}
            onChange={(_, d) => setNovoRequisito({ ...novoRequisito, descricao: d.value })}
          />
        </Field>
      </div>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar requisito
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Atendido</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {requisitos.map((requisito) => (
            <TableRow key={requisito.id}>
              <TableCell>
                <Checkbox
                  checked={requisito.atendido}
                  onChange={(_, d) => alternarAtendido(requisito, !!d.checked)}
                />
              </TableCell>
              <TableCell>{requisito.descricao}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(requisito.id)}
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
