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
import { usePageStyles, useCheckboxChipStyles } from '../pageStyles';

// Matriz de obrigatoriedade de treinamento por função — mesmo princípio de MatrizEpiTab.tsx
// (EpiPage), aqui em Pessoas por não existir um módulo próprio de Treinamento equivalente ao de EPI.
// Base do Motor de Aplicabilidade Legal para treinamentos obrigatórios gerados a partir de um
// RequisitoLegal aplicável, mas também editável manualmente, igual à matriz de EPI.
export function MatrizTreinamentoTab() {
  const estilos = usePageStyles();
  const estilosChip = useCheckboxChipStyles();
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
      const vinculados = await api.funcoes.listarTreinamentosObrigatorios(funcao.id);
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
      await api.funcoes.definirTreinamentosObrigatorios(funcaoId, vinculosSelecionados);
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
        Clique numa função para editar quais cursos são obrigatórios para ela. Novas funções são cadastradas na
        aba Funções; novos cursos, na aba Treinamentos.
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
                            className={estilosChip.chip}
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
