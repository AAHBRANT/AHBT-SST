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
  Textarea,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular, Dismiss24Regular, Edit24Regular } from '@fluentui/react-icons';
import { api, type CatalogoTemaDds } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

// Catálogo de temas livres de DDS (ex.: "Outubro Amarelo") — administração própria, separada da
// tela de conduzir o DDS do dia (DdsSemanalDetalhePage só lista/seleciona um tema já cadastrado).
export function CatalogoTemasDdsPage() {
  const estilos = usePageStyles();
  const [temas, setTemas] = useState<CatalogoTemaDds[]>([]);
  const [nome, setNome] = useState('');
  const [descricao, setDescricao] = useState('');
  const [editandoId, setEditandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setTemas(await api.catalogoTemasDds.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os temas de DDS.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function limparFormulario() {
    setNome('');
    setDescricao('');
    setEditandoId(null);
  }

  function iniciarEdicao(tema: CatalogoTemaDds) {
    setEditandoId(tema.id);
    setNome(tema.nome);
    setDescricao(tema.descricao ?? '');
  }

  async function salvar() {
    if (!nome.trim()) {
      setErro('Informe o nome do tema.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      if (editandoId) {
        await api.catalogoTemasDds.atualizar(editandoId, nome.trim(), descricao.trim() || null);
        sucessoToast('Tema atualizado com sucesso.');
      } else {
        await api.catalogoTemasDds.criar(nome.trim(), descricao.trim() || null);
        sucessoToast('Tema criado com sucesso.');
      }
      limparFormulario();
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar o tema.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este tema de DDS? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogoTemasDds.excluir(id);
      await carregar();
      sucessoToast('Tema excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir o tema.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">{editandoId ? 'Editar tema' : 'Novo tema de DDS'}</Text>
        {editandoId && (
          <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={limparFormulario}>
            Cancelar edição
          </Button>
        )}
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={nome} onChange={(_, d) => setNome(d.value)} />
        </Field>
        <Field label="Descrição">
          <Textarea value={descricao} onChange={(_, d) => setDescricao(d.value)} resize="vertical" />
        </Field>
      </div>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={salvar} disabled={carregando}>
          {editandoId ? 'Salvar alterações' : 'Criar tema'}
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : temas.length === 0 ? (
        <EstadoVazio mensagem="Nenhum tema de DDS cadastrado ainda." />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Descrição</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {temas.map((tema) => (
              <TableRow key={tema.id}>
                <TableCell>{tema.nome}</TableCell>
                <TableCell>{tema.descricao}</TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    <Button appearance="subtle" icon={<Edit24Regular />} onClick={() => iniciarEdicao(tema)} aria-label="Editar" />
                    <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(tema.id)} aria-label="Excluir" />
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
