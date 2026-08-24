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
import { api, type CursoTreinamento, type NovoCursoTreinamento } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const cursoVazio: NovoCursoTreinamento = {
  nome: '',
  normaReferencia: '',
  cargaHorariaMinima: 0,
  validadeEmMeses: 12,
};

export function CursosTreinamentoTab() {
  const estilos = usePageStyles();
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novoCurso, setNovoCurso] = useState<NovoCursoTreinamento>(cursoVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setCursos(await api.cursosTreinamento.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar cursos de treinamento.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.cursosTreinamento.criar(novoCurso);
      setNovoCurso(cursoVazio);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar curso de treinamento.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.cursosTreinamento.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir curso de treinamento.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Cursos de treinamento (catálogo)</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={novoCurso.nome} onChange={(_, d) => setNovoCurso({ ...novoCurso, nome: d.value })} />
        </Field>
        <Field label="Norma de referência">
          <Input
            value={novoCurso.normaReferencia ?? ''}
            onChange={(_, d) => setNovoCurso({ ...novoCurso, normaReferencia: d.value })}
          />
        </Field>
        <Field label="Carga horária mínima (h)">
          <Input
            type="number"
            value={String(novoCurso.cargaHorariaMinima)}
            onChange={(_, d) => setNovoCurso({ ...novoCurso, cargaHorariaMinima: Number(d.value) })}
          />
        </Field>
        <Field label="Validade (meses)">
          <Input
            type="number"
            value={String(novoCurso.validadeEmMeses)}
            onChange={(_, d) => setNovoCurso({ ...novoCurso, validadeEmMeses: Number(d.value) })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar curso
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Norma</TableHeaderCell>
            <TableHeaderCell>CH mínima</TableHeaderCell>
            <TableHeaderCell>Validade (meses)</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {cursos.map((curso) => (
            <TableRow key={curso.id}>
              <TableCell>{curso.nome}</TableCell>
              <TableCell>{curso.normaReferencia}</TableCell>
              <TableCell>{curso.cargaHorariaMinima}h</TableCell>
              <TableCell>{curso.validadeEmMeses}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(curso.id)}
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
