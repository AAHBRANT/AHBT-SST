import { Fragment, useEffect, useState } from 'react';
import {
  Checkbox,
  Button,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { api, type CatalogoEpc, type Funcao } from '../../lib/api';
import { usePageStyles, useCheckboxChipStyles } from '../pageStyles';

// Matriz de EPC por função — mesmo padrão de MatrizUniformeTab.tsx/MatrizEpiTab.tsx. Define quais
// itens de EPC são obrigatórios para cada função.
export function MatrizEpcTab() {
  const estilos = usePageStyles();
  const estilosChip = useCheckboxChipStyles();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoEpc[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [expandidoId, setExpandidoId] = useState<string | null>(null);
  const [vinculosSelecionados, setVinculosSelecionados] = useState<string[]>([]);
  const [salvandoMatriz, setSalvandoMatriz] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaFuncoes, listaItens] = await Promise.all([api.funcoes.listar(), api.catalogosEpc.listar()]);
      setFuncoes(listaFuncoes);
      setItensCatalogo(listaItens);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function alternarExpansao(funcao: Funcao) {
    if (expandidoId === funcao.id) {
      setExpandidoId(null);
      return;
    }
    try {
      setErro(null);
      const vinculados = await api.funcoes.listarEpcs(funcao.id);
      setVinculosSelecionados(vinculados.map((i) => i.id));
      setExpandidoId(funcao.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de EPC da função.');
    }
  }

  function alternarItem(catalogoEpcId: string, marcado: boolean) {
    setVinculosSelecionados((atual) =>
      marcado ? [...atual, catalogoEpcId] : atual.filter((id) => id !== catalogoEpcId)
    );
  }

  async function salvarMatriz(funcaoId: string) {
    try {
      setSalvandoMatriz(true);
      setErro(null);
      await api.funcoes.definirEpcs(funcaoId, vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de EPC.');
    } finally {
      setSalvandoMatriz(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Matriz de EPC por função</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Text size={200}>
        Clique numa função para editar quais itens de EPC são obrigatórios para ela. Novas funções são
        cadastradas em Operação → Pessoas → Funções.
      </Text>

      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>CBO</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {funcoes.map((funcao) => (
            <Fragment key={funcao.id}>
              <TableRow onClick={() => alternarExpansao(funcao)} style={{ cursor: 'pointer' }}>
                <TableCell>{funcao.nome}</TableCell>
                <TableCell>{funcao.cboCodigo}</TableCell>
                <TableCell>{funcao.descricao}</TableCell>
              </TableRow>
              {expandidoId === funcao.id && (
                <TableRow>
                  <TableCell colSpan={3}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: '8px 0' }}>
                      <Text weight="semibold">Matriz de EPC — {funcao.nome}</Text>
                      {itensCatalogo.length === 0 ? (
                        <Text>Nenhum item cadastrado no catálogo ainda.</Text>
                      ) : (
                        itensCatalogo.map((item) => (
                          <Checkbox
                            key={item.id}
                            className={estilosChip.chip}
                            label={item.categoria ? `${item.nome} (${item.categoria})` : item.nome}
                            checked={vinculosSelecionados.includes(item.id)}
                            onChange={(_, d) => alternarItem(item.id, !!d.checked)}
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
