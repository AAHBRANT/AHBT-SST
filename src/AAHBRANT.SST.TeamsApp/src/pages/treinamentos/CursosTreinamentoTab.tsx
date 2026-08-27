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
import { Add24Regular, Delete24Regular, Edit24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type NovoCursoTreinamento } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const cursoVazio: NovoCursoTreinamento = {
  nome: '',
  normaReferencia: '',
  cargaHorariaMinima: 0,
  validadeEmMeses: 12,
  conteudoProgramatico: '',
};

export function CursosTreinamentoTab() {
  const estilos = usePageStyles();
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [form, setForm] = useState<NovoCursoTreinamento>(cursoVazio);
  const [editandoId, setEditandoId] = useState<string | null>(null);
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

  function iniciarEdicao(curso: CursoTreinamento) {
    setEditandoId(curso.id);
    setForm({
      nome: curso.nome,
      normaReferencia: curso.normaReferencia ?? '',
      cargaHorariaMinima: curso.cargaHorariaMinima,
      validadeEmMeses: curso.validadeEmMeses,
      conteudoProgramatico: curso.conteudoProgramatico ?? '',
    });
  }

  function cancelarEdicao() {
    setEditandoId(null);
    setForm(cursoVazio);
  }

  async function salvar() {
    try {
      setCarregando(true);
      setErro(null);
      if (editandoId) {
        await api.cursosTreinamento.atualizar(editandoId, { id: editandoId, ...form });
      } else {
        await api.cursosTreinamento.criar(form);
      }
      cancelarEdicao();
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar curso de treinamento.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.cursosTreinamento.excluir(id);
      if (editandoId === id) cancelarEdicao();
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir curso de treinamento.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">{editandoId ? 'Editar curso de treinamento' : 'Cursos de treinamento (catálogo)'}</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={form.nome} onChange={(_, d) => setForm({ ...form, nome: d.value })} />
        </Field>
        <Field label="Norma de referência">
          <Input
            value={form.normaReferencia ?? ''}
            onChange={(_, d) => setForm({ ...form, normaReferencia: d.value })}
          />
        </Field>
        <Field label="Carga horária mínima (h)">
          <Input
            type="number"
            value={String(form.cargaHorariaMinima)}
            onChange={(_, d) => setForm({ ...form, cargaHorariaMinima: Number(d.value) })}
          />
        </Field>
        <Field label="Validade (meses)">
          <Input
            type="number"
            value={String(form.validadeEmMeses)}
            onChange={(_, d) => setForm({ ...form, validadeEmMeses: Number(d.value) })}
          />
        </Field>
        <Field
          label="Conteúdo programático (verso do certificado)"
          hint="Um tópico por linha. Usado na página 2 do certificado de conclusão; deixe em branco para gerar apenas a frente."
        >
          <Textarea
            resize="vertical"
            rows={4}
            value={form.conteudoProgramatico ?? ''}
            onChange={(_, d) => setForm({ ...form, conteudoProgramatico: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        {editandoId && (
          <Button appearance="secondary" onClick={cancelarEdicao} disabled={carregando}>
            Cancelar edição
          </Button>
        )}
        <Button appearance="primary" icon={editandoId ? undefined : <Add24Regular />} onClick={salvar} disabled={carregando}>
          {editandoId ? 'Salvar alterações' : 'Adicionar curso'}
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Norma</TableHeaderCell>
            <TableHeaderCell>CH mínima</TableHeaderCell>
            <TableHeaderCell>Validade (meses)</TableHeaderCell>
            <TableHeaderCell>Conteúdo programático</TableHeaderCell>
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
              <TableCell>{curso.conteudoProgramatico ? 'Cadastrado' : '—'}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Edit24Regular />}
                  onClick={() => iniciarEdicao(curso)}
                  aria-label="Editar"
                />
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
