import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Button, Field, Input, Select, Textarea, CampoData,
  DetailPageLayout, WorkflowActions, Card, StatusChip, FormSection, FormGrid, Campo, FormRodape,
  DataTable, FeedbackInline, SeletorPesquisavel, Carregando,
  type AcaoWorkflow, type Coluna, type Tom,
} from '@ui';
import {
  api,
  origemNaoConformidadeLabel,
  prioridadeAcaoLabel,
  statusAcaoPlanoLabel,
  statusNaoConformidadeLabel,
  tipoAcaoPlanoLabel,
  StatusNaoConformidade,
  StatusAcaoPlano,
  type AcaoPlano,
  type NaoConformidadeDetalhe,
  type NovaAcaoPlano,
  type Usuario,
} from '../../lib/api';

function novaAcaoInicial(): Omit<NovaAcaoPlano, 'origemTipo' | 'origemId'> {
  return { tipo: 1, descricao: '', responsavelUsuarioId: '', prioridade: 3, prazo: '' };
}

function respostaInicial() {
  return { descricaoAcao: '', responsavelExecucaoId: '', prioridade: 3, prazo: '', justificativaPrazo: '' };
}

// Tom do chip por estado do fluxo. EmAnalise (6) não está aqui de propósito: o fluxo atual do
// backend não emite esse estado, então cai no 'neutro' do fallback em vez de ganhar cor própria.
const tomStatus: Record<number, Tom> = {
  [StatusNaoConformidade.Aberta]: 'neutro',
  [StatusNaoConformidade.Enviada]: 'info',
  [StatusNaoConformidade.Devolvida]: 'alerta',
  [StatusNaoConformidade.EmAndamento]: 'atencao',
  [StatusNaoConformidade.AguardandoValidacao]: 'info',
  [StatusNaoConformidade.Encerrada]: 'ok',
};

const tomAcaoPlano: Record<number, Tom> = {
  [StatusAcaoPlano.Pendente]: 'atencao',
  [StatusAcaoPlano.EmAndamento]: 'atencao',
  [StatusAcaoPlano.Concluido]: 'ok',
  [StatusAcaoPlano.Vencido]: 'alerta',
};

// Detalhe e tratativa de uma não conformidade — o fluxo mais completo do app: Aberta → Enviada →
// EmAndamento → AguardandoValidacao → Encerrada, com Devolvida como desvio em dois pontos. Segunda
// página na camada ui/ (piloto 2 da Onda 1, spec §5.1): nada de Fluent cru nem de pageStyles aqui —
// DetailPageLayout com resumo e ações do fluxo na lateral, cada transição em WorkflowActions com o
// próprio formulário inline, plano de ação em DataTable. A página só monta as ações permitidas no
// estado atual; toda a validação de transição continua no backend e o erro devolvido é exibido como
// veio, no FeedbackInline único acima do conteúdo (ver api.ts request()).
export function NaoConformidadeDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [detalhe, setDetalhe] = useState<NaoConformidadeDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novaAcao, setNovaAcao] = useState(novaAcaoInicial());
  const [usuarioValidador, setUsuarioValidador] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [resposta, setResposta] = useState(respostaInicial());
  const [descricaoConclusao, setDescricaoConclusao] = useState('');
  const [motivoDevolucao, setMotivoDevolucao] = useState('');
  const [observacoesEncerramento, setObservacoesEncerramento] = useState('');

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaUsuarios] = await Promise.all([
        api.naoConformidades.obterDetalhe(id),
        api.usuarios.listar(),
      ]);
      setDetalhe(det);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar não conformidade.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // As cinco transições abaixo entram no WorkflowActions e seguem o contrato dele: `false` mantém o
  // formulário aberto, retorno vazio fecha. Por isso todo caminho que só define `erro` (validação
  // local ou falha da API) devolve `false` — senão o acordeão fecharia e a mensagem apareceria
  // acima do conteúdo, longe do campo que o usuário estava preenchendo.
  async function enviar() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.naoConformidades.enviar(id);
      await carregar();
    } catch (e) {
      setErro(
        e instanceof Error ? e.message : 'Falha ao enviar. Defina o responsável pela tratativa antes.',
      );
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function responder() {
    if (!id) return;
    if (!resposta.descricaoAcao.trim()) {
      setErro('Informe a ação que será realizada.');
      return false;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.naoConformidades.responder(id, {
        descricaoAcao: resposta.descricaoAcao,
        responsavelExecucaoId: resposta.responsavelExecucaoId || null,
        prioridade: resposta.prioridade,
        prazo: resposta.prazo || null,
        justificativaPrazo: resposta.justificativaPrazo || null,
      });
      setResposta(respostaInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao responder a ocorrência.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function registrarConclusao() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.naoConformidades.registrarConclusao(id, descricaoConclusao || null);
      setDescricaoConclusao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar conclusão.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function devolver() {
    if (!id) return;
    if (!motivoDevolucao.trim()) {
      setErro('Informe o motivo da devolução.');
      return false;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.naoConformidades.devolver(id, motivoDevolucao);
      setMotivoDevolucao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao devolver a ocorrência.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function encerrar() {
    if (!id) return;
    if (!usuarioValidador) {
      setErro('Selecione o usuário responsável pela validação.');
      return false;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.naoConformidades.encerrar(id, usuarioValidador, observacoesEncerramento || null);
      setObservacoesEncerramento('');
      await carregar();
    } catch (e) {
      setErro(
        e instanceof Error
          ? e.message
          : 'Falha ao encerrar. Confira se todas as ações do plano já foram concluídas.',
      );
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function criarAcao() {
    if (!id) return;
    if (!novaAcao.descricao.trim()) {
      setErro('Informe a descrição da ação do plano.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.acoesPlano.criar({
        origemTipo: 'NaoConformidade',
        origemId: id,
        ...novaAcao,
        responsavelUsuarioId: novaAcao.responsavelUsuarioId || null,
        prazo: novaAcao.prazo || null,
      });
      setNovaAcao(novaAcaoInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar ação do plano.');
    } finally {
      setProcessando(false);
    }
  }

  async function validarAcao(acaoId: string) {
    if (!usuarioValidador) {
      setErro('Selecione o usuário responsável pela validação.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.acoesPlano.validar(acaoId, usuarioValidador);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao validar ação do plano.');
    } finally {
      setProcessando(false);
    }
  }

  const opcoesUsuarios = useMemo(() => usuarios.map((u) => ({ id: u.id, rotulo: u.nome })), [usuarios]);

  // Antes de qualquer retorno antecipado: as ações do fluxo e as colunas abaixo dependem de `nc`.
  const nc = detalhe?.naoConformidade;

  // Só as transições permitidas no estado atual chegam à lateral — quem decide o que é permitido
  // continua sendo o backend; aqui é só o espelho do estado para não oferecer o impossível.
  const acoes: AcaoWorkflow[] = [];
  if (nc?.status === StatusNaoConformidade.Aberta) {
    acoes.push({
      chave: 'enviar',
      rotulo: 'Enviar ao responsável',
      descricao: 'Inicia a tratativa',
      tom: 'primario',
      aoExecutar: enviar,
    });
  }
  if (nc?.status === StatusNaoConformidade.Enviada || nc?.status === StatusNaoConformidade.Devolvida) {
    acoes.push({
      chave: 'responder',
      rotulo: 'Responder ocorrência',
      descricao: 'Defina a ação, o executor e o prazo',
      tom: 'primario',
      rotuloExecutar: 'Responder',
      aoExecutar: responder,
      formulario: (
        <>
          <Field label="Ação a ser realizada" required>
            <Input
              value={resposta.descricaoAcao}
              onChange={(_, d) => setResposta({ ...resposta, descricaoAcao: d.value })}
            />
          </Field>
          <Field label="Executor">
            <SeletorPesquisavel
              placeholder="Manter responsável atual"
              opcaoVazia="Manter responsável atual"
              opcoes={opcoesUsuarios}
              valor={resposta.responsavelExecucaoId}
              aoMudar={(usuarioId) => setResposta({ ...resposta, responsavelExecucaoId: usuarioId })}
            />
          </Field>
          <Field label="Prioridade">
            <Select
              value={String(resposta.prioridade)}
              onChange={(_, d) => setResposta({ ...resposta, prioridade: Number(d.value) })}
            >
              {Object.entries(prioridadeAcaoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>{rotulo}</option>
              ))}
            </Select>
          </Field>
          <Field label="Prazo" hint="Sugerido pela prioridade se em branco">
            <CampoData value={resposta.prazo} onChange={(_, d) => setResposta({ ...resposta, prazo: d.value })} />
          </Field>
          <Field label="Justificativa do prazo" hint="Opcional">
            <Input
              value={resposta.justificativaPrazo}
              onChange={(_, d) => setResposta({ ...resposta, justificativaPrazo: d.value })}
            />
          </Field>
        </>
      ),
    });
  }
  if (nc?.status === StatusNaoConformidade.EmAndamento) {
    acoes.push({
      chave: 'concluir',
      rotulo: 'Registrar conclusão',
      descricao: 'Envia para validação do inspetor',
      tom: 'primario',
      rotuloExecutar: 'Registrar conclusão',
      aoExecutar: registrarConclusao,
      formulario: (
        <Field label="Descrição da conclusão" hint="Opcional">
          <Textarea value={descricaoConclusao} onChange={(_, d) => setDescricaoConclusao(d.value)} />
        </Field>
      ),
    });
  }
  if (nc?.status === StatusNaoConformidade.AguardandoValidacao) {
    acoes.push({
      chave: 'encerrar',
      rotulo: 'Encerrar',
      descricao: 'Valida e encerra a não conformidade',
      tom: 'primario',
      aoExecutar: encerrar,
      formulario: (
        <>
          <Field label="Validar como" required>
            <SeletorPesquisavel
              placeholder="Selecione um usuário"
              opcaoVazia="Selecione um usuário"
              opcoes={opcoesUsuarios}
              valor={usuarioValidador}
              aoMudar={setUsuarioValidador}
            />
          </Field>
          <Field label="Observações de encerramento" hint="Opcional">
            <Input
              value={observacoesEncerramento}
              onChange={(_, d) => setObservacoesEncerramento(d.value)}
            />
          </Field>
        </>
      ),
    });
    acoes.push({
      chave: 'devolver',
      rotulo: 'Devolver ao emitente',
      descricao: 'A ocorrência volta para quem registrou',
      tom: 'destrutivo',
      rotuloExecutar: 'Devolver',
      aoExecutar: devolver,
      formulario: (
        <Field label="Motivo da devolução" required>
          <Textarea value={motivoDevolucao} onChange={(_, d) => setMotivoDevolucao(d.value)} />
        </Field>
      ),
    });
  }

  const colunasAcoes: Coluna<AcaoPlano>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoAcaoPlanoLabel[a.tipo] },
    { chave: 'descricao', rotulo: 'Descrição' },
    { chave: 'responsavel', rotulo: 'Responsável', render: (a) => a.responsavelUsuarioNome ?? '—' },
    { chave: 'prioridade', rotulo: 'Prioridade', render: (a) => prioridadeAcaoLabel[a.prioridade] },
    // Largura fixa: sem ela a data ISO quebra em duas linhas quando a descrição é longa.
    { chave: 'prazo', rotulo: 'Prazo', largura: '108px', render: (a) => a.prazo?.slice(0, 10) ?? '—' },
    {
      chave: 'status',
      rotulo: 'Situação',
      render: (a) => (
        <StatusChip tom={tomAcaoPlano[a.status] ?? 'neutro'}>{statusAcaoPlanoLabel[a.status]}</StatusChip>
      ),
    },
  ];

  if (!id) return <FeedbackInline tom="erro">Não conformidade não encontrada.</FeedbackInline>;
  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre e a falha
  // (API fora, id inexistente) não teria onde ser lida — era o que o <Text className={erro}> acima
  // do card fazia na versão anterior desta página.
  if (!detalhe || !nc) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: nc.descricao,
        subtitulo: [origemNaoConformidadeLabel[nc.origemDeteccao], nc.local, nc.atividadeNome]
          .filter(Boolean)
          .join(' · '),
        status: <StatusChip tom={tomStatus[nc.status] ?? 'neutro'}>{statusNaoConformidadeLabel[nc.status]}</StatusChip>,
        voltarPara: '/ocorrencias?secao=nao-conformidades',
        rotuloVoltar: 'Não conformidades',
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              {nc.requisitoRelacionado && (
                <Campo span={12}>
                  <Field label="Requisito"><Input value={nc.requisitoRelacionado} readOnly /></Field>
                </Campo>
              )}
              <Campo span={12}>
                <Field label="Responsável"><Input value={nc.responsavelUsuarioNome ?? '—'} readOnly /></Field>
              </Campo>
              <Campo span={12}>
                <Field label="Prazo"><Input value={nc.prazo?.slice(0, 10) ?? '—'} readOnly /></Field>
              </Campo>
            </FormGrid>
          </Card>
          {nc.status === StatusNaoConformidade.Devolvida && nc.motivoDevolucao && (
            <FeedbackInline tom="aviso">Motivo da devolução: {nc.motivoDevolucao}</FeedbackInline>
          )}
          {acoes.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis">
              <WorkflowActions acoes={acoes} processando={processando} />
            </Card>
          )}
        </>
      }
    >
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card
        titulo="Plano de ação"
        subtitulo={`${detalhe.acoesPlano.length} ${detalhe.acoesPlano.length === 1 ? 'ação' : 'ações'}`}
        acoes={
          <Field label="Validar como">
            <SeletorPesquisavel
              placeholder="Selecione um usuário"
              opcaoVazia="Selecione um usuário"
              opcoes={opcoesUsuarios}
              valor={usuarioValidador}
              aoMudar={setUsuarioValidador}
            />
          </Field>
        }
      >
        <DataTable
          aria-label="Ações do plano"
          densidade="compacta"
          colunas={colunasAcoes}
          linhas={detalhe.acoesPlano}
          chaveLinha={(a) => a.id}
          vazio={{ titulo: 'Nenhuma ação no plano', descricao: 'Adicione a primeira ação abaixo.' }}
          acoesLinha={(a) =>
            a.status !== StatusAcaoPlano.Concluido && !a.dataValidacao ? (
              <Button size="small" appearance="subtle" onClick={() => validarAcao(a.id)} disabled={processando}>
                Validar
              </Button>
            ) : null
          }
        />
        <FormSection titulo="Nova ação" numero={1}>
          <FormGrid>
            <Campo span={2}>
              <Field label="Tipo">
                <Select
                  value={String(novaAcao.tipo)}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, tipo: Number(d.value) })}
                >
                  {Object.entries(tipoAcaoPlanoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>{rotulo}</option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Descrição" required>
                <Input
                  value={novaAcao.descricao}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, descricao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Responsável">
                <SeletorPesquisavel
                  placeholder="Nenhum"
                  opcaoVazia="Nenhum"
                  opcoes={opcoesUsuarios}
                  valor={novaAcao.responsavelUsuarioId ?? ''}
                  aoMudar={(usuarioId) => setNovaAcao({ ...novaAcao, responsavelUsuarioId: usuarioId })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Prioridade">
                <Select
                  value={String(novaAcao.prioridade)}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, prioridade: Number(d.value) })}
                >
                  {Object.entries(prioridadeAcaoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>{rotulo}</option>
                  ))}
                </Select>
              </Field>
            </Campo>
            {/* Spans preservados do layout anterior (2+3+3+2+2=12). O brief pedia span 1 aqui, mas
                1/12 do conteúdo dá ~68px — menos que a largura intrínseca de um campo dd/mm/aaaa,
                e o campo passaria a empurrar os vizinhos em vez de respeitar o span declarado. */}
            <Campo span={2}>
              <Field label="Prazo">
                <CampoData
                  value={novaAcao.prazo ?? ''}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, prazo: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape info="Ações concluídas exigem validação antes do encerramento da não conformidade.">
            <Button appearance="primary" onClick={criarAcao} disabled={processando}>
              Adicionar ação
            </Button>
          </FormRodape>
        </FormSection>
      </Card>
    </DetailPageLayout>
  );
}
