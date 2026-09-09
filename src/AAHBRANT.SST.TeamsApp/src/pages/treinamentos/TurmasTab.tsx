import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  Card,
  CampoData,
  ChipCheckboxGroup,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  Legenda,
  PageHeader,
  PainelLateral,
  Select,
  StatusChip,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  statusSessaoTreinamentoLabel,
  StatusSessaoTreinamento,
  type CursoTreinamento,
  type NovaSessaoTreinamento,
  type Obra,
  type SessaoTreinamento,
  type Trabalhador,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

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

// Mesmo mapeamento de tom já usado em DdsDetalhePage.tsx/DdsSemanalDetalhePage.tsx para o par
// EmAndamento/Concluída (Guia de conversão item 5).
const tomPorStatusTurma: Record<number, Tom> = {
  [StatusSessaoTreinamento.EmAndamento]: 'atencao',
  [StatusSessaoTreinamento.Concluida]: 'ok',
};

// Turmas de treinamento (pedido do usuário, 04/09) — o responsável abre a turma já com os
// participantes selecionados (ao contrário do DDS, onde o participante só aparece na hora da
// biometria). O registro de presença por biometria, as fotos obrigatórias e o encerramento
// acontecem na tela de detalhe (SessaoTreinamentoDetalhePage), depois de criada a turma.
// Onda 3 Task 22.5 (camada ui/): mesmo padrão de CursosTreinamentoTab.tsx (aba-irmã, mesma página
// Treinamentos) — formulário de criação em PainelLateral aberto por um botão no PageHeader; seleção
// de participantes em ChipCheckboxGroup (mesmo componente de "Responsáveis" em AprsTab.tsx e
// "Atividades do dia" em DdsSemanalDetalhePage.tsx); lista em DataTable com StatusChip de status.
export function TurmasTab() {
  const navigate = useNavigate();
  const [turmas, setTurmas] = useState<SessaoTreinamento[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [trabalhadoresDaObra, setTrabalhadoresDaObra] = useState<Trabalhador[]>([]);
  const [novaTurma, setNovaTurma] = useState<NovaSessaoTreinamento>(turmaVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
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

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovaTurma(turmaVazia());
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.sessoesTreinamento.criar(novaTurma);
      setNovaTurma(turmaVazia());
      await carregar();
      sucessoToast('Turma criada com sucesso.');
      setPainelAberto(false);
      navigate(`/treinamentos/turma/${id}`);
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar turma.');
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

  const colunas: Coluna<SessaoTreinamento>[] = [
    { chave: 'certificado', rotulo: 'Nº certificado', render: (t) => t.numeroCertificado },
    { chave: 'curso', rotulo: 'Curso', render: (t) => t.cursoTreinamentoNome || nomeCurso(t.cursoTreinamentoId) },
    { chave: 'obra', rotulo: 'Obra', render: (t) => t.obraNome },
    { chave: 'data', rotulo: 'Data', render: (t) => t.dataRealizacao?.slice(0, 10) },
    {
      chave: 'presenca',
      rotulo: 'Presença',
      render: (t) => `${t.totalPresencasConfirmadas}/${t.totalParticipantes}`,
    },
    { chave: 'fotos', rotulo: 'Fotos', render: (t) => `${t.totalFotosEvidencia}/3` },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (t) => <StatusChip tom={tomPorStatusTurma[t.status] ?? 'neutro'}>{statusSessaoTreinamentoLabel[t.status]}</StatusChip>,
    },
  ];

  return (
    <>
      <PageHeader
        titulo="Turmas de treinamento"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Nova turma
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card>
        <DataTable
          aria-label="Turmas de treinamento"
          colunas={colunas}
          linhas={turmas}
          chaveLinha={(t) => t.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma turma de treinamento criada ainda.',
            acao: { rotulo: 'Nova turma', aoClicar: () => setPainelAberto(true) },
          }}
          aoClicarLinha={(t) => navigate(`/treinamentos/turma/${t.id}`)}
        />
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Nova turma"
        subtitulo="Nada é salvo até você criar a turma."
        largura="lg"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !podeCriar}>
              Criar turma
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormGrid>
          <Campo span={6}>
            <Field label="Obra">
              <Select
                value={novaTurma.obraId}
                onChange={(_, d) => setNovaTurma({ ...novaTurma, obraId: d.value, trabalhadoresIds: [] })}
              >
                <option value="">Selecione</option>
                {obras.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={6}>
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
          </Campo>
          <Campo span={6}>
            <Field label="Data de realização">
              <CampoData
                value={novaTurma.dataRealizacao}
                onChange={(_, d) => setNovaTurma({ ...novaTurma, dataRealizacao: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Carga horária (h)">
              <Input
                type="number"
                value={String(novaTurma.cargaHorariaRealizada)}
                onChange={(_, d) => setNovaTurma({ ...novaTurma, cargaHorariaRealizada: Number(d.value) })}
              />
            </Field>
          </Campo>
          <Campo span={12}>
            <Field label="Instituição / instrutor">
              <Input
                value={novaTurma.instituicaoInstrutor ?? ''}
                onChange={(_, d) => setNovaTurma({ ...novaTurma, instituicaoInstrutor: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={12}>
            <Legenda>O número do certificado é gerado automaticamente ao criar a turma.</Legenda>
          </Campo>
          <Campo span={12}>
            <Field label={`Participantes${novaTurma.obraId ? ` (${novaTurma.trabalhadoresIds.length} selecionado(s))` : ''}`}>
              {!novaTurma.obraId ? (
                <Legenda>Selecione a obra para listar os funcionários disponíveis.</Legenda>
              ) : trabalhadoresDaObra.length === 0 ? (
                <Legenda>Nenhum funcionário cadastrado nesta obra.</Legenda>
              ) : (
                <ChipCheckboxGroup
                  aria-label="Participantes"
                  opcoes={trabalhadoresDaObra.map((t) => ({ id: t.id, rotulo: `${t.nome} (${t.matricula})` }))}
                  selecionados={novaTurma.trabalhadoresIds}
                  aoMudar={(ids) => setNovaTurma((atual) => ({ ...atual, trabalhadoresIds: ids }))}
                />
              )}
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </>
  );
}
