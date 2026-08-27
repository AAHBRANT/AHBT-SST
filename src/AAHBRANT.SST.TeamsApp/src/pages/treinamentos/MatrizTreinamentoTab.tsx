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
import { api, type CursoTreinamento, type Funcao } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// Matriz de Treinamento por função (PR-SST-002, item 1): define quais cursos/NRs são
// obrigatórios para cada função. O cadastro da função em si fica em Operação → Pessoas →
// Funções; aqui só listamos as funções já cadastradas para editar a matriz.
export function MatrizTreinamentoTab() {
  const estilos = usePageStyles();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [cursosCatalogo, setCursosCatalogo] = useState<CursoTreinamento[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [expandidoId, setExpandidoId] = useState<string | null>(null);
  const [vinculosSelecionados, setVinculosSelecionados] = useState<string[]>([]);
  const [salvandoMatriz, setSalvandoMatriz] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaFuncoes, listaCursos] = await Promise.all([api.funcoes.listar(), api.cursosTreinamento.listar()]);
      setFuncoes(listaFuncoes);
      setCursosCatalogo(listaCursos);
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
      const vinculados = await api.funcoes.listarTreinamentos(funcao.id);
      setVinculosSelecionados(vinculados.map((c) => c.id));
      setExpandidoId(funcao.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de treinamento da função.');
    }
  }

  function alternarCurso(cursoTreinamentoId: string, marcado: boolean) {
    setVinculosSelecionados((atual) =>
      marcado ? [...atual, cursoTreinamentoId] : atual.filter((id) => id !== cursoTreinamentoId)
    );
  }

  async function salvarMatriz(funcaoId: string) {
    try {
      setSalvandoMatriz(true);
      setErro(null);
      await api.funcoes.definirTreinamentos(funcaoId, vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de treinamento.');
    } finally {
      setSalvandoMatriz(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Matriz de treinamento por função</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Text size={200}>
        Clique numa função para editar quais treinamentos/NRs são obrigatórios para ela. Novas funções são
        cadastradas em Operação → Pessoas → Funções.
      </Text>

      <Table>
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
                      <Text weight="semibold">Matriz de treinamento — {funcao.nome}</Text>
                      {cursosCatalogo.length === 0 ? (
                        <Text>Nenhum curso cadastrado no catálogo ainda.</Text>
                      ) : (
                        cursosCatalogo.map((curso) => (
                          <Checkbox
                            key={curso.id}
                            label={curso.normaReferencia ? `${curso.nome} (${curso.normaReferencia})` : curso.nome}
                            checked={vinculosSelecionados.includes(curso.id)}
                            onChange={(_, d) => alternarCurso(curso.id, !!d.checked)}
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
