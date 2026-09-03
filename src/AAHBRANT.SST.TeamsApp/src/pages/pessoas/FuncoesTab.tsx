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
import { api, type Funcao, type NovaFuncao } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const funcaoVazia: NovaFuncao = { nome: '', cboCodigo: '', descricao: '' };

// A matriz de EPI por função fica no módulo EPI (ver MatrizEpiTab.tsx em pages/epi) — aqui é só o
// cadastro (CRUD) da função em si, usado também por Trabalhadores/Equipes.
export function FuncoesTab() {
  const estilos = usePageStyles();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [novaFuncao, setNovaFuncao] = useState<NovaFuncao>(funcaoVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const listaFuncoes = await api.funcoes.listar();
      setFuncoes(listaFuncoes);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.funcoes.criar(novaFuncao);
      setNovaFuncao(funcaoVazia);
      await carregar();
      sucessoToast('Função criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar função.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta função? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.funcoes.excluir(id);
      await carregar();
      sucessoToast('Função excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir função.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Funções cadastradas</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={novaFuncao.nome} onChange={(_, d) => setNovaFuncao({ ...novaFuncao, nome: d.value })} />
        </Field>
        <Field label="Código CBO">
          <Input
            value={novaFuncao.cboCodigo ?? ''}
            onChange={(_, d) => setNovaFuncao({ ...novaFuncao, cboCodigo: d.value })}
          />
        </Field>
        <Field label="Descrição">
          <Input
            value={novaFuncao.descricao ?? ''}
            onChange={(_, d) => setNovaFuncao({ ...novaFuncao, descricao: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar função
        </Button>
      </div>

      <Text size={200}>A matriz de EPI de cada função é definida em EPI → Matriz de EPI por Função.</Text>

      {carregandoLista ? (
        <ListaCarregando />
      ) : funcoes.length === 0 ? (
        <EstadoVazio mensagem="Nenhuma função cadastrada ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>CBO</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {funcoes.map((funcao) => (
            <TableRow key={funcao.id}>
              <TableCell>{funcao.nome}</TableCell>
              <TableCell>{funcao.cboCodigo}</TableCell>
              <TableCell>{funcao.descricao}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(funcao.id)}
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
