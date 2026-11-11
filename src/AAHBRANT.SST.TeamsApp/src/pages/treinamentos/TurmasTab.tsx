import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  Card,
  Checkbox,
  CampoData,
  ListaSelecaoMultipla,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  Input,
  Legenda,
  PageHeader,
  PainelCriacaoInline,
  Select,
  StatusChip,
  Textarea,
  designTokens,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, CheckmarkCircle24Regular, Delete24Regular, LockClosed16Regular } from '@fluentui/react-icons';
import {
  api,
  statusSessaoTreinamentoLabel,
  StatusSessaoTreinamento,
  type CursoTreinamento,
  type NovaSessaoTreinamento,
  type Obra,
  type PadraoTurmaTreinamento,
  type SessaoTreinamento,
  type Trabalhador,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { hoje } from '../../lib/valoresPadrao';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { ExcluirTurmaDialog, type AlvoExclusaoTurma } from '../../components/treinamentos/ExcluirTurmaDialog';

// Limite do servidor por exclusão em lote (ExcluirSessoesTreinamentoLoteCommandValidator.MaximoPorLote).
const MAXIMO_POR_LOTE = 100;

function turmaVazia(): NovaSessaoTreinamento {
  return {
    obraId: '',
    cursoTreinamentoId: '',
    // Data do dia por padrão (19/10); o técnico pode trocar.
    dataRealizacao: hoje(),
    cargaHorariaRealizada: 0,
    instituicaoInstrutor: '',
    trabalhadoresIds: [],
    conteudoProgramatico: '',
    local: '',
    instrutorRegistroProfissional: '',
    validadeEmMeses: null,
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
// Treinamentos) — seleção de participantes em ChipCheckboxGroup (mesmo componente de
// "Responsáveis" em AprsTab.tsx e "Atividades do dia" em DdsSemanalDetalhePage.tsx); lista em
// DataTable com StatusChip de status. Onda A do spec de formulário inline (2026-09-11): o
// PainelLateral saiu — mesmo padrão de AtividadesTab.tsx/InspecoesTab.tsx. Agora é um
// PainelCriacaoInline, que cresce acima da lista em vez de cobrir a tela com uma gaveta.
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
  const [padrao, setPadrao] = useState<PadraoTurmaTreinamento | null>(null);
  const [nomeUsuario, setNomeUsuario] = useState('');
  const sucessoToast = useSucessoToast();
  // Selecao em massa para exclusao definitiva (mockup turma-excluir-2026-11-10): so Administrador.
  const souAdministrador = useSouAdministrador();
  const [selecionando, setSelecionando] = useState(false);
  const [selecionadas, setSelecionadas] = useState<Set<string>>(new Set());
  const [alvoExclusao, setAlvoExclusao] = useState<AlvoExclusaoTurma | null>(null);
  const [avisoLimite, setAvisoLimite] = useState(false);
  const travada = (t: SessaoTreinamento) => (t.totalCertificadosEmitidos ?? 0) > 0;
  const podemSelecionar = turmas.filter((t) => !travada(t));
  const todasMarcadas = podemSelecionar.length > 0 && podemSelecionar.slice(0, MAXIMO_POR_LOTE).every((t) => selecionadas.has(t.id));

  function alternar(id: string) {
    setAvisoLimite(false);
    setSelecionadas((atual) => {
      const novo = new Set(atual);
      if (novo.has(id)) novo.delete(id);
      else if (novo.size >= MAXIMO_POR_LOTE) setAvisoLimite(true);
      else novo.add(id);
      return novo;
    });
  }

  function alternarTodas() {
    if (todasMarcadas) {
      setSelecionadas(new Set());
      setAvisoLimite(false);
      return;
    }
    setSelecionadas(new Set(podemSelecionar.slice(0, MAXIMO_POR_LOTE).map((t) => t.id)));
    setAvisoLimite(podemSelecionar.length > MAXIMO_POR_LOTE);
  }

  function alternarSelecao() {
    setSelecionando((v) => !v);
    setSelecionadas(new Set());
    setAvisoLimite(false);
  }
  // Sem padrão carregado ainda, trava: o servidor decide quem edita (Técnico de Segurança/Admin).
  const podeEditar = padrao?.podeEditar ?? false;

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
    api.usuarios
      .eu()
      .then((u) => setNomeUsuario(u.nome))
      .catch(() => setNomeUsuario(''));
  }, []);

  // Ao escolher curso (ou trocar a obra), puxa o padrão da NR: carga horária mínima, validade,
  // conteúdo programático e local. O instrutor sugerido é quem está logado.
  useEffect(() => {
    if (!novaTurma.cursoTreinamentoId) {
      setPadrao(null);
      return;
    }
    let ativo = true;
    api.sessoesTreinamento
      .padrao(novaTurma.cursoTreinamentoId, novaTurma.obraId || undefined)
      .then((p) => {
        if (!ativo) return;
        setPadrao(p);
        setNovaTurma((atual) => ({
          ...atual,
          cargaHorariaRealizada: p.cargaHorariaMinima,
          validadeEmMeses: p.validadeEmMeses,
          conteudoProgramatico: p.conteudoProgramatico ?? '',
          local: p.local ?? '',
          instituicaoInstrutor: p.podeEditar ? atual.instituicaoInstrutor || nomeUsuario : '',
        }));
      })
      .catch(() => {
        if (ativo) setPadrao(null);
      });
    return () => {
      ativo = false;
    };
  }, [novaTurma.cursoTreinamentoId, novaTurma.obraId, nomeUsuario]);

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
    setPadrao(null);
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

  const minimo = padrao?.cargaHorariaMinima ?? 0;
  const abaixoDoMinimo = !!padrao && novaTurma.cargaHorariaRealizada < minimo;
  const rotuloNorma = padrao?.norma ?? 'curso';

  const podeCriar =
    !!novaTurma.obraId &&
    !!novaTurma.cursoTreinamentoId &&
    !!novaTurma.dataRealizacao &&
    novaTurma.cargaHorariaRealizada > 0 &&
    !abaixoDoMinimo &&
    novaTurma.trabalhadoresIds.length > 0;

  const colunaSelecao: Coluna<SessaoTreinamento> = {
    chave: 'selecao',
    largura: '56px',
    rotulo: (
      <Checkbox
        aria-label="Selecionar todas que podem ser excluídas"
        checked={todasMarcadas}
        onChange={alternarTodas}
      />
    ),
    render: (t) =>
      travada(t) ? (
        <span aria-label="Não pode ser excluída" title="Não pode ser excluída">
          <LockClosed16Regular />
        </span>
      ) : (
        <span onClick={(ev) => ev.stopPropagation()}>
          <Checkbox aria-label={`Selecionar ${t.numeroCertificado ?? 'turma'}`} checked={selecionadas.has(t.id)} onChange={() => alternar(t.id)} />
        </span>
      ),
  };

  const colunas: Coluna<SessaoTreinamento>[] = [
    ...(souAdministrador && selecionando ? [colunaSelecao] : []),
    { chave: 'certificado', rotulo: 'Nº certificado', render: (t) => t.numeroCertificado },
    {
      chave: 'curso',
      rotulo: 'Curso',
      render: (t) => (
        <>
          {t.cursoTreinamentoNome || nomeCurso(t.cursoTreinamentoId)}
          {souAdministrador && selecionando && travada(t) && (
            <div>
              <StatusChip tom="neutro" icone={<LockClosed16Regular />}>
                {t.totalCertificadosEmitidos === 1 ? '1 certificado' : `${t.totalCertificadosEmitidos} certificados`}
              </StatusChip>
            </div>
          )}
        </>
      ),
    },
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
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="Turmas de treinamento"
        acoes={
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {souAdministrador && (
            <Button
              appearance="secondary"
              icon={<CheckmarkCircle24Regular />}
              aria-pressed={selecionando}
              onClick={alternarSelecao}
            >
              {selecionando ? 'Selecionando' : 'Selecionar'}
            </Button>
          )}
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-turma"
          >
            {painelAberto ? 'Fechar' : 'Nova turma'}
          </Button>
          </div>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {souAdministrador && selecionando && (
        <Legenda>Selecione turmas para excluir de uma vez. Quem tem certificado fica travada.</Legenda>
      )}
      {souAdministrador && selecionando && avisoLimite && (
        <FeedbackInline tom="aviso" aoFechar={() => setAvisoLimite(false)}>
          Limite de {MAXIMO_POR_LOTE} turmas por exclusão: a seleção foi limitada às primeiras {MAXIMO_POR_LOTE}. Exclua este grupo e selecione as demais depois.
        </FeedbackInline>
      )}
      {souAdministrador && selecionando && selecionadas.size > 0 && (
        <div
          style={{
            display: 'flex', gap: 10, alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap',
            padding: '10px 14px', borderRadius: 14, background: 'var(--sst-acao-assinar-fundo)', color: designTokens.colorWhite,
          }}
        >
          <b>{selecionadas.size === 1 ? '1 turma selecionada' : `${selecionadas.size} turmas selecionadas`}</b>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <Button appearance="outline" style={{ color: designTokens.colorWhite, borderColor: designTokens.colorWhite }} onClick={() => setSelecionadas(new Set())}>
              Limpar seleção
            </Button>
            <Button
              appearance="secondary"
              icon={<Delete24Regular />}
              onClick={() => setAlvoExclusao({ tipo: 'lote', turmas: turmas.filter((t) => selecionadas.has(t.id)) })}
            >
              Excluir selecionadas ({selecionadas.size})
            </Button>
          </div>
        </div>
      )}

      <div id="painel-nova-turma">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova turma">
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
                  naoFutura
                  value={novaTurma.dataRealizacao}
                  onChange={(_, d) => setNovaTurma({ ...novaTurma, dataRealizacao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field
                label="Carga horária (h)"
                validationState={abaixoDoMinimo ? 'error' : 'none'}
                validationMessage={
                  abaixoDoMinimo
                    ? `A ${rotuloNorma} exige no mínimo ${minimo}h. Abaixo disso o certificado não tem validade legal.`
                    : padrao
                      ? `Mínimo ${rotuloNorma}: ${minimo}h. Pode aumentar.`
                      : undefined
                }
              >
                <Input
                  type="number"
                  min={minimo}
                  disabled={!podeEditar}
                  value={String(novaTurma.cargaHorariaRealizada)}
                  onChange={(_, d) => setNovaTurma({ ...novaTurma, cargaHorariaRealizada: Number(d.value) })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Validade (meses)">
                <Input
                  type="number"
                  min={1}
                  disabled={!podeEditar}
                  value={novaTurma.validadeEmMeses ? String(novaTurma.validadeEmMeses) : ''}
                  onChange={(_, d) => setNovaTurma({ ...novaTurma, validadeEmMeses: Number(d.value) || null })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Instrutor (Técnico de Segurança)">
                <Input
                  disabled={!podeEditar}
                  value={novaTurma.instituicaoInstrutor ?? ''}
                  onChange={(_, d) => setNovaTurma({ ...novaTurma, instituicaoInstrutor: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Registro profissional">
                <Input
                  placeholder="Ex.: MTE 12345/PB"
                  disabled={!podeEditar}
                  value={novaTurma.instrutorRegistroProfissional ?? ''}
                  onChange={(_, d) => setNovaTurma({ ...novaTurma, instrutorRegistroProfissional: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Local de realização">
                <Input
                  disabled={!podeEditar}
                  value={novaTurma.local ?? ''}
                  onChange={(_, d) => setNovaTurma({ ...novaTurma, local: d.value })}
                />
              </Field>
            </Campo>
            {padrao && (
              <Campo span={12}>
                <FeedbackInline tom="info">
                  Preenchido com o padrão {padrao.norma ? `da ${padrao.norma}` : 'do curso'}. O conteúdo fica gravado nesta turma: se o
                  curso mudar depois, os certificados desta turma continuam com este conteúdo.
                  {!podeEditar && ' Só o Técnico de Segurança pode alterar carga horária, validade, instrutor, local e conteúdo.'}
                </FeedbackInline>
              </Campo>
            )}
            <Campo span={12}>
              <Field label="Conteúdo programático (um tópico por linha — vai no verso do certificado)">
                <Textarea
                  rows={7}
                  disabled={!podeEditar}
                  value={novaTurma.conteudoProgramatico ?? ''}
                  onChange={(_, d) => setNovaTurma({ ...novaTurma, conteudoProgramatico: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={12}>
              <Legenda>
                Nada é salvo até você criar a turma. Cada participante recebe o próprio número de certificado ao encerrar a turma.
              </Legenda>
            </Campo>
            <Campo span={12}>
              <Field label={`Participantes${novaTurma.obraId ? ` (${novaTurma.trabalhadoresIds.length} selecionado(s))` : ''}`}>
                {!novaTurma.obraId ? (
                  <Legenda>Selecione a obra para listar os funcionários disponíveis.</Legenda>
                ) : trabalhadoresDaObra.length === 0 ? (
                  <Legenda>Nenhum funcionário cadastrado nesta obra.</Legenda>
                ) : (
                  <ListaSelecaoMultipla
                    aria-label="Participantes"
                    opcoes={trabalhadoresDaObra.map((t) => ({
                      id: t.id,
                      rotulo: t.matricula ? `${t.nome} (${t.matricula})` : t.nome,
                    }))}
                    selecionados={novaTurma.trabalhadoresIds}
                    aoMudar={(atualizar) => setNovaTurma((atual) => ({ ...atual, trabalhadoresIds: atualizar(atual.trabalhadoresIds) }))}
                  />
                )}
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !podeCriar}>
              Criar turma
            </Button>
          </FormRodape>
        </PainelCriacaoInline>
      </div>

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
          aoClicarLinha={(t) => {
            if (selecionando && souAdministrador) {
              if (!travada(t)) alternar(t.id);
              return;
            }
            navigate(`/treinamentos/turma/${t.id}`);
          }}
        />
      </Card>
      {alvoExclusao && (
        <ExcluirTurmaDialog
          alvo={alvoExclusao}
          aoFechar={() => setAlvoExclusao(null)}
          aoExcluir={(resultados) => {
            const n = resultados.filter((r) => r.resultado === 'excluida').length;
            setAlvoExclusao(null);
            setSelecionadas(new Set());
            void carregar();
            sucessoToast(n === 1 ? 'Turma excluída definitivamente.' : 'Turmas excluídas definitivamente.');
          }}
        />
      )}
    </div>
  );
}
