import { Fragment, useEffect, useState } from 'react';
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
import { api, type CatalogoEpi, type Funcao, type NovaFuncao } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const funcaoVazia: NovaFuncao = { nome: '', cboCodigo: '', descricao: '' };

export function FuncoesTab() {
  const estilos = usePageStyles();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [novaFuncao, setNovaFuncao] = useState<NovaFuncao>(funcaoVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [episCatalogo, setEpisCatalogo] = useState<CatalogoEpi[]>([]);
  const [expandidoId, setExpandidoId] = useState<string | null>(null);
  const [vinculosSelecionados, setVinculosSelecionados] = useState<string[]>([]);
  const [salvandoMatriz, setSalvandoMatriz] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaFuncoes, listaEpis] = await Promise.all([api.funcoes.listar(), api.catalogosEpi.listar()]);
      setFuncoes(listaFuncoes);
      setEpisCatalogo(listaEpis);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.');
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
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar função.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.funcoes.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir função.');
    }
  }

  async function alternarExpansao(funcao: Funcao) {
    if (expandidoId === funcao.id) {
      setExpandidoId(null);
      return;
    }
    try {
      setErro(null);
      const vinculados = await api.funcoes.listarEpis(funcao.id);
      setVinculosSelecionados(vinculados.map((e) => e.id));
      setExpandidoId(funcao.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de EPI da função.');
    }
  }

  function alternarEpi(catalogoEpiId: string, marcado: boolean) {
    setVinculosSelecionados((atual) =>
      marcado ? [...atual, catalogoEpiId] : atual.filter((id) => id !== catalogoEpiId)
    );
  }

  async function salvarMatriz(funcaoId: string) {
    try {
      setSalvandoMatriz(true);
      setErro(null);
      await api.funcoes.definirEpis(funcaoId, vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de EPI.');
    } finally {
      setSalvandoMatriz(false);
    }
  }

  return (
    <div className={estilos.card}>
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

      <Text size={200}>Clique numa linha para editar a matriz de EPI daquela função.</Text>

      <Table>
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
            <Fragment key={funcao.id}>
              <TableRow onClick={() => alternarExpansao(funcao)} style={{ cursor: 'pointer' }}>
                <TableCell>{funcao.nome}</TableCell>
                <TableCell>{funcao.cboCodigo}</TableCell>
                <TableCell>{funcao.descricao}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(e) => {
                      e.stopPropagation();
                      excluir(funcao.id);
                    }}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
              {expandidoId === funcao.id && (
                <TableRow>
                  <TableCell colSpan={4}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: '8px 0' }}>
                      <Text weight="semibold">Matriz de EPI — {funcao.nome}</Text>
                      {episCatalogo.length === 0 ? (
                        <Text>Nenhum EPI cadastrado no catálogo ainda.</Text>
                      ) : (
                        episCatalogo.map((epi) => (
                          <Checkbox
                            key={epi.id}
                            label={epi.fabricante ? `${epi.nome} (${epi.fabricante})` : epi.nome}
                            checked={vinculosSelecionados.includes(epi.id)}
                            onChange={(_, d) => alternarEpi(epi.id, !!d.checked)}
                          />
                        ))
                      )}
                      <div>
                        <Button appearance="primary" onClick={() => salvarMatriz(funcao.id)} disabled={salvandoMatriz}>
                          Salvar matriz
                        </Button>
                      </div>
                    </div>
                  </TableCell>
                </TableRow>
              )}
            </Fragment>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
