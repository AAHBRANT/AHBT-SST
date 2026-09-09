import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  DetailPageLayout,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  StatusChip,
  Text,
  Textarea,
  WorkflowActions,
  type AcaoWorkflow,
  type Coluna,
  type Tom,
} from '@ui';
import { Save24Regular } from '@fluentui/react-icons';
import {
  api,
  metodologiaInvestigacaoLabel,
  prioridadeAcaoLabel,
  statusAcaoPlanoLabel,
  statusAcidenteLabel,
  tipoAcaoPlanoLabel,
  tipoOcorrenciaLabel,
  StatusAcaoPlano,
  StatusAcidente,
  type AcaoPlano,
  type AcidenteDetalhe,
  type NovaAcaoPlano,
  type Usuario,
} from '../../lib/api';

function novaAcaoInicial(): Omit<NovaAcaoPlano, 'origemTipo' | 'origemId'> {
  return { tipo: 1, descricao: '', responsavelUsuarioId: '', prioridade: 3, prazo: '' };
}

// Registrado/EmInvestigacao/Concluido — mesmo padrão de progressão neutro→atencao→ok já usado em
// StatusPcmsoDocumento (Task 12) e replicado em AcidentesPage.tsx (mesmo enum, mesma leitura).
const tomPorStatusAcidente: Record<number, Tom> = {
  [StatusAcidente.Registrado]: 'neutro',
  [StatusAcidente.EmInvestigacao]: 'atencao',
  [StatusAcidente.Concluido]: 'ok',
};

// AcaoPlano reaproveita o enum de StatusControleRisco (Pendente/EmAndamento/Concluido/Vencido, ver
// disclosure em AcaoPlano.cs) — mesma leitura de severidade já usada nos painéis de vencido de
// PGR/Riscos: pendente é neutro (ainda não começou), em andamento chama atenção, concluído é ok,
// vencido é alerta (mesma família "vermelho" de prazo estourado usada em toda a frente).
const tomPorStatusAcaoPlano: Record<number, Tom> = {
  [StatusAcaoPlano.Pendente]: 'neutro',
  [StatusAcaoPlano.EmAndamento]: 'atencao',
  [StatusAcaoPlano.Concluido]: 'ok',
  [StatusAcaoPlano.Vencido]: 'alerta',
};

// Onda 2 Task 20 (camada ui/): detalhe de um acidente/incidente/quase-acidente. DetailPageLayout com
// resumo na lateral e a única ação de fluxo (avançar status) em WorkflowActions — mesmo critério já
// usado em InspecaoDetalhePage.tsx (uma ação só, sem aprovar/reprovar). Retorno antecipado com OU erro
// OU skeleton na carga inicial, nunca cabeçalho+skeleton+erro empilhados (padrão já revisado em
// AprDetalhePage.tsx/PgrDetalhePage.tsx/InspecaoDetalhePage.tsx).
export function AcidenteDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [detalhe, setDetalhe] = useState<AcidenteDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [metodologia, setMetodologia] = useState<string>('');
  const [causas, setCausas] = useState<string>('');
  const [novaAcao, setNovaAcao] = useState(novaAcaoInicial());
  const [usuarioValidador, setUsuarioValidador] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaUsuarios] = await Promise.all([api.acidentes.obterDetalhe(id), api.usuarios.listar()]);
      setDetalhe(det);
      setUsuarios(listaUsuarios);
      setMetodologia(det.acidente.metodologiaInvestigacao ? String(det.acidente.metodologiaInvestigacao) : '');
      setCausas(det.acidente.causas ?? '');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar acidente.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function salvarInvestigacao() {
    if (!id || !detalhe) return;
    try {
      setProcessando(true);
      setErro(null);
      const a = detalhe.acidente;
      await api.acidentes.atualizar(id, {
        tipo: a.tipo,
        obraId: a.obraId,
        trabalhadorId: a.trabalhadorId,
        atividadeId: a.atividadeId,
        local: a.local,
        data: a.data,
        hora: a.hora,
        descricao: a.descricao,
        lesao: a.lesao,
        consequencia: a.consequencia,
        atendimento: a.atendimento,
        houveAfastamento: a.houveAfastamento,
        diasAfastamento: a.diasAfastamento,
        numeroCat: a.numeroCat,
        gravidade: a.gravidade,
        metodologiaInvestigacao: metodologia ? Number(metodologia) : null,
        causas: causas || null,
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar dados da investigação.');
    } finally {
      setProcessando(false);
    }
  }

  // Entra em WorkflowActions: retornar `false` mantém o formulário aberto (aqui não há formulário —
  // a ação executa direto — mas o contrato pede erro tratado sem lançar, mesmo padrão de
  // InspecaoDetalhePage.tsx/AprDetalhePage.tsx).
  async function avancarStatus() {
    if (!id) return false;
    try {
      setProcessando(true);
      setErro(null);
      await api.acidentes.avancarStatus(id);
      await carregar();
    } catch (e) {
      setErro(
        e instanceof Error
          ? e.message
          : 'Falha ao avançar status. Confira se todas as ações do plano já foram concluídas.',
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
        origemTipo: 'Acidente',
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

  if (!id) return <FeedbackInline tom="erro">Acidente não encontrado.</FeedbackInline>;

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre e a falha
  // (API fora, id inexistente) não teria onde ser lida.
  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Text>Carregando...</Text>
    );
  }

  const a = detalhe.acidente;
  // Acidentes/Incidentes/Quase-acidentes viraram abas de OcorrenciasPage (02/09) — volta pra aba
  // que corresponde ao tipo do registro aberto, não sempre pra "Acidentes".
  const secaoOcorrencia = a.tipo === 2 ? 'incidentes' : a.tipo === 3 ? 'quase-acidentes' : 'acidentes';

  const acoes: AcaoWorkflow[] = [];
  if (a.status !== StatusAcidente.Concluido) {
    acoes.push({
      chave: 'avancar-status',
      rotulo: `Avançar status (${statusAcidenteLabel[a.status]} → ${statusAcidenteLabel[a.status + 1]})`,
      descricao: 'Confira se as ações do plano em aberto já foram concluídas antes de avançar.',
      tom: 'primario',
      habilitada: true,
      aoExecutar: avancarStatus,
    });
  }

  const colunasAcoes: Coluna<AcaoPlano>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (acao) => tipoAcaoPlanoLabel[acao.tipo] },
    { chave: 'descricao', rotulo: 'Descrição' },
    { chave: 'responsavel', rotulo: 'Responsável', render: (acao) => acao.responsavelUsuarioNome ?? '—' },
    { chave: 'prioridade', rotulo: 'Prioridade', render: (acao) => prioridadeAcaoLabel[acao.prioridade] },
    { chave: 'prazo', rotulo: 'Prazo', render: (acao) => acao.prazo?.slice(0, 10) ?? '—' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (acao) => (
        <StatusChip tom={tomPorStatusAcaoPlano[acao.status] ?? 'neutro'}>{statusAcaoPlanoLabel[acao.status]}</StatusChip>
      ),
    },
  ];

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: `${tipoOcorrenciaLabel[a.tipo]} — ${a.local}`,
        status: (
          <StatusChip tom={tomPorStatusAcidente[a.status] ?? 'neutro'}>{statusAcidenteLabel[a.status]}</StatusChip>
        ),
        voltarPara: `/ocorrencias?secao=${secaoOcorrencia}`,
        rotuloVoltar: 'Voltar para Ocorrências',
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              <Campo span={12}>
                <Field label="Obra">
                  <Input value={a.obraNome ?? '—'} readOnly />
                </Field>
              </Campo>
              {a.trabalhadorNome && (
                <Campo span={12}>
                  <Field label="Funcionário">
                    <Input value={a.trabalhadorNome} readOnly />
                  </Field>
                </Campo>
              )}
              {a.atividadeNome && (
                <Campo span={12}>
                  <Field label="Atividade">
                    <Input value={a.atividadeNome} readOnly />
                  </Field>
                </Campo>
              )}
              <Campo span={12}>
                <Field label="Data">
                  <Input value={a.data?.slice(0, 10) ?? ''} readOnly />
                </Field>
              </Campo>
              {a.lesao && (
                <Campo span={12}>
                  <Field label="Lesão">
                    <Input value={a.lesao} readOnly />
                  </Field>
                </Campo>
              )}
              {a.consequencia && (
                <Campo span={12}>
                  <Field label="Consequência">
                    <Input value={a.consequencia} readOnly />
                  </Field>
                </Campo>
              )}
              {a.atendimento && (
                <Campo span={12}>
                  <Field label="Atendimento">
                    <Input value={a.atendimento} readOnly />
                  </Field>
                </Campo>
              )}
              {a.houveAfastamento && (
                <Campo span={12}>
                  <Field label="Afastamento">
                    <Input value={`${a.diasAfastamento ?? '—'} dia(s)`} readOnly />
                  </Field>
                </Campo>
              )}
              {a.numeroCat && (
                <Campo span={12}>
                  <Field label="CAT">
                    <Input value={a.numeroCat} readOnly />
                  </Field>
                </Campo>
              )}
            </FormGrid>
          </Card>
          {acoes.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis">
              <WorkflowActions acoes={acoes} processando={processando} />
            </Card>
          )}
        </>
      }
    >
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Text style={{ display: 'block' }}>{a.descricao}</Text>

      <Card titulo="Investigação (Seção 28 — análise de causas)">
        <FormSection titulo="Dados da Investigação" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Metodologia de investigação">
                <Select value={metodologia} onChange={(_, d) => setMetodologia(d.value)}>
                  <option value="">Não definida</option>
                  {Object.entries(metodologiaInvestigacaoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Causas identificadas">
                <Textarea value={causas} onChange={(_, d) => setCausas(d.value)} />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
        <FormRodape>
          <Button appearance="primary" icon={<Save24Regular />} onClick={salvarInvestigacao} disabled={processando}>
            Salvar investigação
          </Button>
        </FormRodape>
      </Card>

      <Card titulo="Nova ação do plano">
        <FormSection titulo="Nova Ação do Plano" numero={1} primeira>
          <FormGrid>
            <Campo span={2}>
              <Field label="Tipo">
                <Select
                  value={String(novaAcao.tipo)}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, tipo: Number(d.value) })}
                >
                  {Object.entries(tipoAcaoPlanoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Descrição" required>
                <Input value={novaAcao.descricao} onChange={(_, d) => setNovaAcao({ ...novaAcao, descricao: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Responsável">
                <Select
                  value={novaAcao.responsavelUsuarioId ?? ''}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, responsavelUsuarioId: d.value })}
                >
                  <option value="">Nenhum</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Prioridade">
                <Select
                  value={String(novaAcao.prioridade)}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, prioridade: Number(d.value) })}
                >
                  {Object.entries(prioridadeAcaoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Prazo">
                <CampoData
                  value={novaAcao.prazo ?? ''}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, prazo: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
        <FormRodape>
          <Button appearance="primary" onClick={criarAcao} disabled={processando}>
            Adicionar ação
          </Button>
        </FormRodape>
      </Card>

      <Card
        titulo="Ações do plano"
        acoes={
          <Field label="Validar como">
            <Select value={usuarioValidador} onChange={(_, d) => setUsuarioValidador(d.value)}>
              <option value="">Selecione um usuário</option>
              {usuarios.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
            </Select>
          </Field>
        }
      >
        <DataTable
          aria-label="Ações do plano"
          colunas={colunasAcoes}
          linhas={detalhe.acoesPlano}
          chaveLinha={(acao) => acao.id}
          vazio={{ titulo: 'Nenhuma ação do plano cadastrada ainda.' }}
          acoesLinha={(acao) =>
            acao.status !== StatusAcaoPlano.Concluido && !acao.dataValidacao ? (
              <Button appearance="subtle" onClick={() => validarAcao(acao.id)} disabled={processando}>
                Validar
              </Button>
            ) : null
          }
        />
      </Card>
    </DetailPageLayout>
  );
}
