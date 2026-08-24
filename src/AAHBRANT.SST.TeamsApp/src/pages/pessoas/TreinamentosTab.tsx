import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type NovoTreinamento, type Treinamento } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function treinamentoVazio(trabalhadorId: string): NovoTreinamento {
  return {
    trabalhadorId,
    cursoTreinamentoId: '',
    dataRealizacao: '',
    dataValidade: '',
    cargaHorariaRealizada: 0,
    instituicaoInstrutor: '',
    numeroCertificado: '',
  };
}

export function TreinamentosTab({ trabalhadorId }: { trabalhadorId: string }) {
  const estilos = usePageStyles();
  const [treinamentos, setTreinamentos] = useState<Treinamento[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novoTreinamento, setNovoTreinamento] = useState<NovoTreinamento>(() => treinamentoVazio(trabalhadorId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaCursos] = await Promise.all([
        api.treinamentos.listar(trabalhadorId),
        api.cursosTreinamento.listar(),
      ]);
      setTreinamentos(lista);
      setCursos(listaCursos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar treinamentos.');
    }
  }

  function nomeCurso(id: string) {
    return cursos.find((c) => c.id === id)?.nome ?? id;
  }

  function vencido(dataValidade: string) {
    return new Date(dataValidade) < new Date(new Date().toDateString());
  }

  useEffect(() => {
    carregar();
    setNovoTreinamento(treinamentoVazio(trabalhadorId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.treinamentos.criar(novoTreinamento);
      setNovoTreinamento(treinamentoVazio(trabalhadorId));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar treinamento.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.treinamentos.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir treinamento.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Treinamentos do trabalhador</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Curso">
          <Select
            value={novoTreinamento.cursoTreinamentoId}
            onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, cursoTreinamentoId: d.value })}
          >
            <option value="">Selecione</option>
            {cursos.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Data de realização">
          <Input
            type="date"
            value={novoTreinamento.dataRealizacao}
            onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataRealizacao: d.value })}
          />
        </Field>
        <Field label="Validade">
          <Input
            type="date"
            value={novoTreinamento.dataValidade}
            onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataValidade: d.value })}
          />
        </Field>
        <Field label="Carga horária realizada (h)">
          <Input
            type="number"
            value={String(novoTreinamento.cargaHorariaRealizada)}
            onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, cargaHorariaRealizada: Number(d.value) })}
          />
        </Field>
        <Field label="Instituição / instrutor">
          <Input
            value={novoTreinamento.instituicaoInstrutor ?? ''}
            onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, instituicaoInstrutor: d.value })}
          />
        </Field>
        <Field label="Número do certificado">
          <Input
            value={novoTreinamento.numeroCertificado ?? ''}
            onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, numeroCertificado: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar treinamento
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Curso</TableHeaderCell>
            <TableHeaderCell>Realização</TableHeaderCell>
            <TableHeaderCell>Validade</TableHeaderCell>
            <TableHeaderCell>Certificado</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {treinamentos.map((treinamento) => (
            <TableRow key={treinamento.id}>
              <TableCell>{nomeCurso(treinamento.cursoTreinamentoId)}</TableCell>
              <TableCell>{treinamento.dataRealizacao?.slice(0, 10)}</TableCell>
              <TableCell>
                {treinamento.dataValidade?.slice(0, 10)}
                {vencido(treinamento.dataValidade) && (
                  <Badge color="danger" appearance="tint" style={{ marginLeft: 8 }}>
                    Vencido
                  </Badge>
                )}
              </TableCell>
              <TableCell>{treinamento.numeroCertificado}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(treinamento.id)}
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
