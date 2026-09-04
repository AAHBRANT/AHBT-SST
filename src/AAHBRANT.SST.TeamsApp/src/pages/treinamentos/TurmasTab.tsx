import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Checkbox,
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
import { CampoData } from '../../components/CampoData';
import { Add24Regular, ArrowRight24Regular } from '@fluentui/react-icons';
import {
  api,
  statusSessaoTreinamentoLabel,
  type CursoTreinamento,
  type NovaSessaoTreinamento,
  type Obra,
  type SessaoTreinamento,
  type Trabalhador,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function turmaVazia(): NovaSessaoTreinamento {
  return {
    obraId: '',
    cursoTreinamentoId: '',
    dataRealizacao: '',
    cargaHorariaRealizada: 0,
    instituicaoInstrutor: '',
    trabalhadoresIds: [],
  };
}

// Turmas de treinamento (pedido do usuário, 04/09) — o responsável abre a turma já com os
// participantes selecionados (ao contrário do DDS, onde o participante só aparece na hora da
// biometria). O registro de presença por biometria, as fotos obrigatórias e o encerramento
// acontecem na tela de detalhe (SessaoTreinamentoDetalhePage), depois de criada a turma.
export function TurmasTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [turmas, setTurmas] = useState<SessaoTreinamento[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [trabalhadoresDaObra, setTrabalhadoresDaObra] = useState<Trabalhador[]>([]);
  const [novaTurma, setNovaTurma] = useState<NovaSessaoTreinamento>(turmaVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaTurmas, listaObras, listaCursos] = await Promise.all([
        api.sessoesTreinamento.listar(),
        api.obras.listar(),
        api.cursosTreinamento.listar(),
      ]);
      setTurmas(listaTurmas);
      setObras(listaObras);
      setCursos(listaCursos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar turmas de treinamento.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  useEffect(() => {
    if (!novaTurma.obraId) {
      setTrabalhadoresDaObra([]);
      return;
    }
    api.trabalhadores
      .listar(novaTurma.obraId)
      .then(setTrabalhadoresDaObra)
      .catch(() => setTrabalhadoresDaObra([]));
  }, [novaTurma.obraId]);

  function alternarParticipante(trabalhadorId: string, marcado: boolean) {
    setNovaTurma((atual) => ({
      ...atual,
      trabalhadoresIds: marcado
        ? [...atual.trabalhadoresIds, trabalhadorId]
        : atual.trabalhadoresIds.filter((id) => id !== trabalhadorId),
    }));
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.sessoesTreinamento.criar(novaTurma);
      setNovaTurma(turmaVazia());
      await carregar();
      sucessoToast('Turma criada com sucesso.');
      navigate(`/treinamentos/turma/${id}`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar turma.');
    } finally {
      setCarregando(false);
    }
  }

  function nomeCurso(id: string) {
    return cursos.find((c) => c.id === id)?.nome ?? id;
  }

  const podeCriar =
    !!novaTurma.obraId &&
    !!novaTurma.cursoTreinamentoId &&
    !!novaTurma.dataRealizacao &&
    novaTurma.cargaHorariaRealizada > 0 &&
    novaTurma.trabalhadoresIds.length > 0;

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova turma</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da turma</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col3}>
            <Field label="Obra">
              <Select value={novaTurma.obraId} onChange={(_, d) => setNovaTurma({ ...novaTurma, obraId: d.value, trabalhadoresIds: [] })}>
                <option value="">Selecione</option>
                {obras.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Curso">
              <Select
                value={novaTurma.cursoTreinamentoId}
                onChange={(_, d) => setNovaTurma({ ...novaTurma, cursoTreinamentoId: d.value })}
              >
                <option value="">Selecione</option>
                {cursos.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Data de realização">
              <CampoData
                value={novaTurma.dataRealizacao}
                onChange={(_, d) => setNovaTurma({ ...novaTurma, dataRealizacao: d.value })}
              />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Carga horária (h)">
              <Input
                type="number"
                value={String(novaTurma.cargaHorariaRealizada)}
                onChange={(_, d) => setNovaTurma({ ...novaTurma, cargaHorariaRealizada: Number(d.value) })}
              />
            </Field>
          </div>
          <div className={estilos.col12}>
            <Field label="Instituição / instrutor">
              <Input
                value={novaTurma.instituicaoInstrutor ?? ''}
                onChange={(_, d) => setNovaTurma({ ...novaTurma, instituicaoInstrutor: d.value })}
              />
            </Field>
          </div>
        </div>
        <Text size={200} style={{ display: 'block' }}>
          O número do certificado é gerado automaticamente ao criar a turma.
        </Text>

        <div className={`${estilos.sectionTitle}`}>
          Participantes {novaTurma.obraId && `(${novaTurma.trabalhadoresIds.length} selecionado(s))`}
        </div>
        {!novaTurma.obraId ? (
          <Text size={200}>Selecione a obra para listar os funcionários disponíveis.</Text>
        ) : trabalhadoresDaObra.length === 0 ? (
          <Text size={200}>Nenhum funcionário cadastrado nesta obra.</Text>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 260, overflowY: 'auto' }}>
            {trabalhadoresDaObra.map((t) => (
              <Checkbox
                key={t.id}
                label={`${t.nome} (${t.matricula})`}
                checked={novaTurma.trabalhadoresIds.includes(t.id)}
                onChange={(_, d) => alternarParticipante(t.id, !!d.checked)}
              />
            ))}
          </div>
        )}

        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !podeCriar}>
            Criar turma
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Turmas de treinamento</Text>
        </div>

        {carregandoLista ? (
          <ListaCarregando />
        ) : turmas.length === 0 ? (
          <EstadoVazio mensagem="Nenhuma turma de treinamento criada ainda." />
        ) : (
          <Table noNativeElements>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Nº certificado</TableHeaderCell>
                <TableHeaderCell>Curso</TableHeaderCell>
                <TableHeaderCell>Obra</TableHeaderCell>
                <TableHeaderCell>Data</TableHeaderCell>
                <TableHeaderCell>Presença</TableHeaderCell>
                <TableHeaderCell>Fotos</TableHeaderCell>
                <TableHeaderCell>Status</TableHeaderCell>
                <TableHeaderCell></TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {turmas.map((t) => (
                <TableRow key={t.id} onClick={() => navigate(`/treinamentos/turma/${t.id}`)} style={{ cursor: 'pointer' }}>
                  <TableCell>{t.numeroCertificado}</TableCell>
                  <TableCell>{t.cursoTreinamentoNome || nomeCurso(t.cursoTreinamentoId)}</TableCell>
                  <TableCell>{t.obraNome}</TableCell>
                  <TableCell>{t.dataRealizacao?.slice(0, 10)}</TableCell>
                  <TableCell>
                    {t.totalPresencasConfirmadas}/{t.totalParticipantes}
                  </TableCell>
                  <TableCell>{t.totalFotosEvidencia}/3</TableCell>
                  <TableCell>
                    <Badge appearance="tint" color={t.status === 2 ? 'success' : 'warning'}>
                      {statusSessaoTreinamentoLabel[t.status]}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Button appearance="subtle" icon={<ArrowRight24Regular />} aria-label="Abrir turma" />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </div>
  );
}
