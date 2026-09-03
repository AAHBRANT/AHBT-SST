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
import { api, type CatalogoEpi, type Funcao } from '../../lib/api';
import { usePageStyles, useCheckboxChipStyles } from '../pageStyles';

// Matriz de EPI por função: define quais EPIs são obrigatórios para cada função (usado para
// filtrar o select de EPI em Entregas). O cadastro da função em si (nome/CBO/descrição) fica em
// Operação → Pessoas → Funções; aqui só listamos as funções já cadastradas para editar a matriz.
export function MatrizEpiTab() {
  const estilos = usePageStyles();
  const estilosChip = useCheckboxChipStyles();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [episCatalogo, setEpisCatalogo] = useState<CatalogoEpi[]>([]);
  const [erro, setErro] = useState<string | null>(null);
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
        <Text weight="semibold">Matriz de EPI por função</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Text size={200}>
        Clique numa função para editar quais EPIs são obrigatórios para ela. Novas funções são cadastradas em
        Operação → Pessoas → Funções.
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
                      <Text weight="semibold">Matriz de EPI — {funcao.nome}</Text>
                      {episCatalogo.length === 0 ? (
                        <Text>Nenhum EPI cadastrado no catálogo ainda.</Text>
                      ) : (
                        episCatalogo.map((epi) => (
                          <Checkbox
                            key={epi.id}
                            className={estilosChip.chip}
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
