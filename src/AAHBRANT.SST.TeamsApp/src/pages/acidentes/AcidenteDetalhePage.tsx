import { pendenciasGrade } from '../../lib/dadosFoto';
import { GradeFotosEvidencia } from '../../components/GradeFotosEvidencia';
import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  BotaoAcao,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  useConfirmar,
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
  Legenda,
} from '@ui';
import { ArrowDownload24Regular, Eye24Regular, Save24Regular } from '@fluentui/react-icons';
import { MapaCorporal } from '../../components/mapaCorporal/MapaCorporal';
import { CartaoReuniaoAnalise } from './CartaoReuniaoAnalise';
import { CampoComoSeResolve, payloadResolucao, pendenciaResolucao, resolucaoVazia, type ResolucaoEmEdicao } from './CampoComoSeResolve';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
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
  TipoOcorrencia,
  type AcaoPlano,
  type Acidente,
  type AcidenteDetalhe,
  type AtualizarAcidentePayload,
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
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});
  const [detalhe, setDetalhe] = useState<AcidenteDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [metodologia, setMetodologia] = useState<string>('');
  const [causas, setCausas] = useState<string>('');
  const [regioesCorpo, setRegioesCorpo] = useState<string[]>([]);
  const [novaAcao, setNovaAcao] = useState(novaAcaoInicial());
  // Reabrir desfaz uma conclusão feita por engano. Não existe mais conclusão manual (24/10/2026): a ação
  // conclui quando o fato vinculado acontece (treinamento, inspeção pelo responsável designado, EPI, DDS, reunião).
  const { confirmar, dialogElement: dialogoConfirmacao } = useConfirmar();
  // "Como se resolve": na nova ação e no Alterar de uma ação existente.
  const [resolucaoNova, setResolucaoNova] = useState<ResolucaoEmEdicao>(resolucaoVazia());
  const [acaoAlterando, setAcaoAlterando] = useState<AcaoPlano | null>(null);
  const [resolucaoAlterando, setResolucaoAlterando] = useState<ResolucaoEmEdicao>(resolucaoVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoPdf, setBaixandoPdf] = useState(false);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  useEffect(() => {
    let ativo = true;
    const urls: string[] = [];
    void Promise.all((detalhe?.fotos ?? []).map(async f => {
      try {
        const blob = await api.acidentes.baixarFoto(f.id);
        if (!ativo) return null;
        const url = URL.createObjectURL(blob); urls.push(url);
        return [f.id, url] as const;
      } catch { return null; }
    })).then(entries => { if (ativo) setFotoUrls(Object.fromEntries(entries.filter(e => e !== null))); });
    return () => { ativo = false; urls.forEach(url => URL.revokeObjectURL(url)); };
  }, [detalhe]);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaUsuarios] = await Promise.all([api.acidentes.obterDetalhe(id), api.usuarios.listar()]);
      setDetalhe(det);
      setUsuarios(listaUsuarios);
      setMetodologia(det.acidente.metodologiaInvestigacao ? String(det.acidente.metodologiaInvestigacao) : '');
      setCausas(det.acidente.causas ?? '');
      setRegioesCorpo(det.acidente.regioesCorpo ?? []);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar acidente.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // O PUT regrava a ocorrência inteira: parte do que está gravado e troca só o que a tela editou.
  // Sem regioesCorpo no corpo, a API mantém o mapa corporal gravado.
  function dadosGravados(a: Acidente): AtualizarAcidentePayload {
    return {
      tipo: a.tipo,
      obraId: a.obraId,
      trabalhadorId: a.trabalhadorId,
      trabalhadoresIds: a.envolvidos.map((e) => e.trabalhadorId),
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
      // Exigido na incapacidade permanente parcial; nos demais casos a API recalcula pela tabela.
      diasDebitadosInformados: a.diasDebitados || null,
      metodologiaInvestigacao: a.metodologiaInvestigacao,
      causas: a.causas,
    };
  }

  async function salvarInvestigacao() {
    if (!id || !detalhe) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.acidentes.atualizar(id, {
        ...dadosGravados(detalhe.acidente),
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

  async function salvarRegioesCorpo() {
    if (!id || !detalhe) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.acidentes.atualizar(id, { ...dadosGravados(detalhe.acidente), regioesCorpo });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar as partes do corpo atingidas.');
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
    if (!novaAcao.responsavelUsuarioId) {
      setErro('Designe quem vai resolver a ação.');
      return;
    }
    const pendencia = pendenciaResolucao(resolucaoNova);
    if (pendencia) {
      setErro(pendencia);
      return;
    }
    const resolucao = payloadResolucao(resolucaoNova);
    try {
      setProcessando(true);
      setErro(null);
      await api.acoesPlano.criar({
        origemTipo: 'Acidente',
        origemId: id,
        ...novaAcao,
        responsavelUsuarioId: novaAcao.responsavelUsuarioId || null,
        prazo: novaAcao.prazo || null,
        tipoResolucao: resolucao.tipoResolucao,
        resolucaoCursoTreinamentoId: resolucao.cursoTreinamentoId,
        resolucaoTipoInspecao: resolucao.tipoInspecao,
        resolucaoTemaInspecaoId: resolucao.temaInspecaoId,
        resolucaoCatalogoEpiId: resolucao.catalogoEpiId,
      });
      setNovaAcao(novaAcaoInicial());
      setResolucaoNova(resolucaoVazia());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar ação do plano.');
    } finally {
      setProcessando(false);
    }
  }

  function abrirAlterarResolucao(acao: AcaoPlano) {
    setErro(null);
    setResolucaoAlterando({
      tipoResolucao: acao.tipoResolucao ?? null,
      cursoTreinamentoId: acao.resolucaoCursoTreinamentoId ?? '',
      tipoInspecao: acao.resolucaoTipoInspecao ? String(acao.resolucaoTipoInspecao) : '',
      temaInspecaoId: acao.resolucaoTemaInspecaoId ?? '',
      catalogoEpiId: acao.resolucaoCatalogoEpiId ?? '',
    });
    setAcaoAlterando(acao);
  }

  async function salvarResolucao() {
    if (!acaoAlterando) return;
    const pendencia = pendenciaResolucao(resolucaoAlterando);
    if (pendencia) {
      setErro(pendencia);
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.acidentes.definirResolucaoAcao(acaoAlterando.id, payloadResolucao(resolucaoAlterando));
      setAcaoAlterando(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao alterar como a ação se resolve.');
    } finally {
      setProcessando(false);
    }
  }

  async function reabrirAcao(acao: AcaoPlano) {
    const ok = await confirmar({
      titulo: 'Reabrir ação?',
      mensagem: `"${acao.descricao}" volta para Pendente e o registro de conclusão é apagado. Use quando ela foi concluída por engano.`,
      rotuloConfirmar: 'Reabrir ação',
      tom: 'neutro',
    });
    if (!ok) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.acidentes.reabrirAcao(acao.id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao reabrir a ação do plano.');
    } finally {
      setProcessando(false);
    }
  }

  function nomeArquivoPdf(det: AcidenteDetalhe) {
    return `investigacao-${tipoOcorrenciaLabel[det.acidente.tipo].toLowerCase().replace(/\s+/g, '-')}-${det.acidente.data?.slice(0, 10)}.pdf`;
  }

  // Relatório de Investigação de Ocorrência — sai a qualquer momento; o próprio PDF mostra a situação.
  async function baixarPdf() {
    if (!id || !detalhe) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.acidentes.baixarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = nomeArquivoPdf(detalhe);
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o relatório de investigação.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  function visualizarPdf() {
    if (!id || !detalhe) return;
    void visualizar({ titulo: 'Relatório de investigação', nomeArquivo: nomeArquivoPdf(detalhe), obter: () => api.acidentes.baixarPdf(id) });
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
      rotulo: a.status === StatusAcidente.EmInvestigacao ? 'Finalizar investigação do acidente' : 'Iniciar investigação',
      finalizacao: a.status === StatusAcidente.EmInvestigacao,
      pendencias: a.status === StatusAcidente.EmInvestigacao ? [
        ...(a.tipo === 1 ? pendenciasGrade(detalhe.fotos ?? []) : []),
        ...(detalhe.reuniao?.realizadaEm ? [] : ['Registre a reunião de análise como realizada.']),
        ...(detalhe.acoesPlano.some(acao => acao.status !== StatusAcaoPlano.Concluido) ? ['Conclua as ações do plano que estão pendentes.'] : []),
      ] : [],
      descricao: 'Confira se as ações do plano em aberto já foram concluídas antes de avançar.',
      tom: 'primario',
      habilitada: true,
      aoExecutar: avancarStatus,
    });
  }

  const colunasAcoes: Coluna<AcaoPlano>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (acao) => tipoAcaoPlanoLabel[acao.tipo] },
    {
      chave: 'descricao',
      rotulo: 'Descrição',
      render: (acao) => (
        <div style={{ display: 'grid', gap: 2 }}>
          <span>{acao.descricao}</span>
          {acao.fundamentacao && <Legenda>{acao.fundamentacao}</Legenda>}
        </div>
      ),
    },
    {
      chave: 'resolucao',
      rotulo: 'Como se resolve',
      render: (acao) => (
        <div style={{ display: 'grid', gap: 2 }}>
          <span>
            <StatusChip tom={acao.tipoResolucaoAutomatica ? 'info' : 'atencao'}>{acao.tipoResolucaoAutomatica ? 'Automática' : 'Sem vínculo'}</StatusChip>
          </span>
          {acao.tipoResolucaoAutomatica ? (
            <Legenda>{acao.resolucaoAutomatica}</Legenda>
          ) : acao.status === StatusAcaoPlano.Concluido ? (
            acao.descricaoConclusao && <Legenda>"{acao.descricaoConclusao}"</Legenda>
          ) : (
            <Legenda>Defina como se resolve (Alterar). Sem isso a ação não conclui.</Legenda>
          )}
        </div>
      ),
    },
    { chave: 'responsavel', rotulo: 'Responsável', render: (acao) => acao.responsavelUsuarioNome ?? '—' },
    { chave: 'prioridade', rotulo: 'Prioridade', render: (acao) => prioridadeAcaoLabel[acao.prioridade] },
    { chave: 'prazo', rotulo: 'Prazo', render: (acao) => acao.prazo?.slice(0, 10) ?? '—' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (acao) => {
        const concluida = acao.status === StatusAcaoPlano.Concluido;
        const quem = acao.concluidoPorUsuarioNome ?? acao.validadoPorUsuarioNome;
        if (!concluida && acao.tipoResolucaoAutomatica)
          return (
            <div style={{ display: 'grid', gap: 2 }}>
              <span>
                <StatusChip tom="atencao">{acao.rotuloAguardando ?? 'Aguardando'}</StatusChip>
              </span>
              {acao.progressoResolucao && <Legenda>{acao.progressoResolucao}</Legenda>}
            </div>
          );
        return (
          <div style={{ display: 'grid', gap: 2 }}>
            <span>
              <StatusChip tom={tomPorStatusAcaoPlano[acao.status] ?? 'neutro'}>
                {concluida && acao.dataConclusao
                  ? `Concluída ${acao.dataConclusao.slice(8, 10)}/${acao.dataConclusao.slice(5, 7)}`
                  : statusAcaoPlanoLabel[acao.status]}
              </StatusChip>
            </span>
            {concluida && quem && <Legenda>por {quem}</Legenda>}
          </div>
        );
      },
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
        acoes: (
          <>
            <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={visualizarPdf} aria-label="Visualizar relatório de investigação">
              Visualizar relatório
            </BotaoAcao>
            <BotaoAcao tom="baixar" icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf} aria-label="Baixar relatório de investigação em PDF">
              Baixar PDF
            </BotaoAcao>
            {dialogoVisualizador}
          </>
        ),
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
              {a.envolvidos.length > 0 && (
                <Campo span={12}>
                  <Field label={a.envolvidos.length > 1 ? `Funcionários envolvidos (${a.envolvidos.length})` : 'Funcionário'}>
                    <Textarea value={a.envolvidos.map((e) => e.nome).join('\n')} readOnly rows={Math.min(a.envolvidos.length, 6)} />
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
              <WorkflowActions acoes={acoes} processando={processando} erro={erro} />
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

      <CartaoReuniaoAnalise
        acidenteId={a.id}
        reuniao={detalhe.reuniao ?? null}
        usuarios={usuarios}
        somenteLeitura={a.status === StatusAcidente.Concluido}
        aoAtualizar={carregar}
      />

      <Card titulo={a.tipo === 1 ? 'Fotos do local do acidente' : 'Fotos do local'}>
        <GradeFotosEvidencia titulo={a.tipo === 1 ? 'Três fotos obrigatórias' : 'Até três fotos (opcionais)'} total={3}
          opcional={a.tipo !== 1}
          subtitulo="Registre a visão geral, o ponto da ocorrência e um detalhe relevante. Confira os dados antes de sair do local."
          contextoFoto={{ obraId: a.obraId, obraNome: a.obraNome ?? '', local: a.local }}
          fotos={(detalhe.fotos ?? []).map(f => ({ ...f, url: fotoUrls[f.id] }))}
          somenteLeitura={a.status === StatusAcidente.Concluido}
          onSelecionarFoto={async (ordem, arquivo) => { await api.acidentes.anexarFoto(a.id, ordem, arquivo); await carregar(); }}
          onRemoverFoto={async fotoId => { try { await api.acidentes.removerFoto(fotoId); await carregar(); } catch (e) { setErro(e instanceof Error ? e.message : 'Falha ao remover foto.'); } }}
          onErroValidacao={setErro} />
      </Card>
      <Text style={{ display: 'block' }}>{a.descricao}</Text>

      {(a.tipo === TipoOcorrencia.Acidente || a.tipo === TipoOcorrencia.DoencaOcupacional || Boolean(a.lesao) || regioesCorpo.length > 0) && (
        <Card titulo="Partes do corpo atingidas" subtitulo="Saem no relatório de investigação, junto com a lesão.">
          <MapaCorporal
            selecionadas={regioesCorpo}
            aoAlterar={a.status === StatusAcidente.Concluido ? undefined : setRegioesCorpo}
          />
          {a.status !== StatusAcidente.Concluido && (
            <FormRodape>
              <Button
                appearance="primary"
                icon={<Save24Regular />}
                onClick={salvarRegioesCorpo}
                disabled={processando || [...regioesCorpo].sort().join() === [...(a.regioesCorpo ?? [])].sort().join()}
              >
                Salvar partes do corpo
              </Button>
            </FormRodape>
          )}
        </Card>
      )}

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
              <Field label="Responsável" required dica="Quem vai resolver a ação. Para inspeção, só vale a inspeção finalizada por esta pessoa.">
                <Select
                  value={novaAcao.responsavelUsuarioId ?? ''}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, responsavelUsuarioId: d.value })}
                >
                  <option value="">Selecione</option>
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
            <CampoComoSeResolve valor={resolucaoNova} aoMudar={setResolucaoNova} />
          </FormGrid>
        </FormSection>
        <FormRodape>
          <Button appearance="primary" onClick={criarAcao} disabled={processando}>
            Adicionar ação
          </Button>
        </FormRodape>
      </Card>

      <Card titulo="Ações do plano">
        <DataTable
          aria-label="Ações do plano"
          colunas={colunasAcoes}
          linhas={detalhe.acoesPlano}
          chaveLinha={(acao) => acao.id}
          vazio={{ titulo: 'Nenhuma ação do plano cadastrada ainda.' }}
          acoesLinha={(acao) => {
            if (a.status === StatusAcidente.Concluido) return null;
            const concluida = acao.status === StatusAcaoPlano.Concluido || !!acao.dataValidacao;
            if (concluida)
              return (
                <Button appearance="subtle" onClick={() => void reabrirAcao(acao)} disabled={processando}>
                  Reabrir
                </Button>
              );
            // DDS e reunião se resolvem pelo próprio fluxo; não têm botão.
            if (acao.tipoResolucaoAutomatica === 'Dds' || acao.tipoResolucaoAutomatica === 'Reuniao') return null;
            return (
              <Button
                appearance={acao.tipoResolucao ? 'subtle' : 'primary'}
                size="small"
                onClick={() => abrirAlterarResolucao(acao)}
                disabled={processando}
              >
                {acao.tipoResolucao ? 'Alterar' : 'Definir como se resolve'}
              </Button>
            );
          }}
        />
      </Card>
      {dialogoConfirmacao}
      <Dialog open={!!acaoAlterando} onOpenChange={(_, d) => { if (!d.open) setAcaoAlterando(null); }}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Como esta ação se resolve?</DialogTitle>
            <DialogContent>
              <div style={{ display: 'grid', gap: 10 }}>
                <Text>"{acaoAlterando?.descricao}"</Text>
                <FormGrid>
                  <CampoComoSeResolve valor={resolucaoAlterando} aoMudar={setResolucaoAlterando} />
                </FormGrid>
                {erro && acaoAlterando && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setAcaoAlterando(null)} disabled={processando}>
                Cancelar
              </Button>
              <Button appearance="primary" onClick={() => void salvarResolucao()} disabled={processando}>
                Salvar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </DetailPageLayout>
  );
}
