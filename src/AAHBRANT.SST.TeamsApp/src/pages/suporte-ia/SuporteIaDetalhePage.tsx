import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Card,
  Carregando,
  DetailPageLayout,
  Field,
  FeedbackInline,
  FormGrid,
  Campo,
  Input,
  StatusChip,
  Textarea,
  WorkflowActions,
  type AcaoWorkflow,
  type Tom,
} from '@ui';
import {
  api,
  resultadoTriagemSuporteIaLabel,
  severidadeSolicitacaoSuporteIaLabel,
  statusSolicitacaoSuporteIaLabel,
  tipoSolicitacaoSuporteIaLabel,
  StatusSolicitacaoSuporteIa,
  type SuporteIaSolicitacao,
} from '../../lib/api';
import { EsteiraSuporteIa } from './EsteiraSuporteIa';
import { etapaAtivaPorStatus } from './statusEtapaSuporteIa';

const tomStatus: Record<number, Tom> = {
  [StatusSolicitacaoSuporteIa.Recebida]: 'neutro',
  [StatusSolicitacaoSuporteIa.Respondida]: 'info',
  [StatusSolicitacaoSuporteIa.Encaminhada]: 'atencao',
  [StatusSolicitacaoSuporteIa.EmAnaliseTecnica]: 'atencao',
  [StatusSolicitacaoSuporteIa.AguardandoValidacao]: 'info',
  [StatusSolicitacaoSuporteIa.Resolvida]: 'ok',
  [StatusSolicitacaoSuporteIa.Cancelada]: 'alerta',
  [StatusSolicitacaoSuporteIa.Reaberta]: 'alerta',
};

function formatarData(valor?: string | null) {
  if (!valor) return null;
  return new Date(valor).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// Tela de detalhe de um chamado do Suporte IA — aberta ao clicar num item da fila (SuporteIaTab).
// Segue o mesmo padrão de NaoConformidadeDetalhePage: DetailPageLayout + WorkflowActions na
// lateral, com só as transições permitidas no status atual (a validação real fica no backend).
export function SuporteIaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [solicitacao, setSolicitacao] = useState<SuporteIaSolicitacao | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [erroCarga, setErroCarga] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [notaConclusao, setNotaConclusao] = useState('');
  const [notaRecusa, setNotaRecusa] = useState('');
  const [comentarioValidacao, setComentarioValidacao] = useState('');

  async function carregar() {
    if (!id) return;
    try {
      setErroCarga(null);
      const dados = await api.suporteIa.obterDetalhe(id);
      setSolicitacao(dados);
    } catch (e) {
      setErroCarga(e instanceof Error ? e.message : 'Falha ao carregar o chamado.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function aprovar() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.suporteIa.aprovar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao aprovar o chamado.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function concluir() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.suporteIa.concluir(id, notaConclusao || null);
      setNotaConclusao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao concluir a execução.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function recusar() {
    if (!id) return;
    if (!notaRecusa.trim()) {
      setErro('Informe o motivo da recusa.');
      return false;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.suporteIa.recusar(id, notaRecusa);
      setNotaRecusa('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao recusar o chamado.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function validar(confirmado: boolean) {
    if (!id) return;
    if (!confirmado && !comentarioValidacao.trim()) {
      setErro('Explique o que ainda não foi resolvido.');
      return false;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.suporteIa.validar(id, confirmado, comentarioValidacao || null);
      setComentarioValidacao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar a validação.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  if (!id) return <FeedbackInline tom="erro">Chamado não encontrado.</FeedbackInline>;

  if (!solicitacao) {
    return erroCarga ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erroCarga}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={6} />
    );
  }

  const acoes: AcaoWorkflow[] = [];

  if (solicitacao.status === StatusSolicitacaoSuporteIa.Encaminhada || solicitacao.status === StatusSolicitacaoSuporteIa.Reaberta) {
    acoes.push({
      chave: 'aprovar',
      rotulo: 'Aprovar chamado',
      descricao: 'Assume a demanda técnica e inicia a execução',
      tom: 'primario',
      aoExecutar: aprovar,
    });
  }

  if (
    solicitacao.status === StatusSolicitacaoSuporteIa.EmAnaliseTecnica
  ) {
    acoes.push({
      chave: 'concluir',
      rotulo: 'Concluir execução',
      descricao: 'Envia para o solicitante confirmar a resolução',
      tom: 'primario',
      rotuloExecutar: 'Concluir',
      aoExecutar: concluir,
      formulario: (
        <Field label="Nota de fechamento" hint="Opcional">
          <Textarea value={notaConclusao} onChange={(_, d) => setNotaConclusao(d.value)} rows={4} />
        </Field>
      ),
    });
  }

  if (
    solicitacao.status === StatusSolicitacaoSuporteIa.Encaminhada ||
    solicitacao.status === StatusSolicitacaoSuporteIa.EmAnaliseTecnica ||
    solicitacao.status === StatusSolicitacaoSuporteIa.Reaberta
  ) {
    acoes.push({
      chave: 'recusar',
      rotulo: 'Recusar chamado',
      descricao: 'Encerra sem executar (duplicado, não procede etc.)',
      tom: 'destrutivo',
      rotuloExecutar: 'Recusar',
      aoExecutar: recusar,
      formulario: (
        <Field label="Motivo da recusa" required>
          <Textarea value={notaRecusa} onChange={(_, d) => setNotaRecusa(d.value)} rows={4} />
        </Field>
      ),
    });
  }

  const aguardandoValidacaoDoSolicitante =
    solicitacao.status === StatusSolicitacaoSuporteIa.Respondida ||
    solicitacao.status === StatusSolicitacaoSuporteIa.AguardandoValidacao;

  if (aguardandoValidacaoDoSolicitante && solicitacao.souSolicitante) {
    acoes.push({
      chave: 'confirmar',
      rotulo: 'Confirmar que resolveu',
      descricao: 'Encerra o chamado',
      tom: 'primario',
      aoExecutar: () => validar(true),
    });
    acoes.push({
      chave: 'reabrir',
      rotulo: 'Ainda não resolveu',
      descricao: 'Reabre o chamado para nova análise',
      tom: 'destrutivo',
      rotuloExecutar: 'Reabrir',
      aoExecutar: () => validar(false),
      formulario: (
        <Field label="O que ainda não foi resolvido?" required>
          <Textarea value={comentarioValidacao} onChange={(_, d) => setComentarioValidacao(d.value)} rows={4} />
        </Field>
      ),
    });
  }

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: solicitacao.titulo,
        subtitulo: [tipoSolicitacaoSuporteIaLabel[solicitacao.tipo], solicitacao.modulo].filter(Boolean).join(' · '),
        status: (
          <StatusChip tom={tomStatus[solicitacao.status] ?? 'neutro'}>
            {statusSolicitacaoSuporteIaLabel[solicitacao.status]}
          </StatusChip>
        ),
        voltarPara: '/suporte-ia',
        rotuloVoltar: 'Suporte IA',
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              <Campo span={12}>
                <Field label="Solicitante">
                  <Input value={solicitacao.solicitanteNome || solicitacao.solicitanteEmail || '—'} readOnly />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Severidade percebida">
                  <Input value={severidadeSolicitacaoSuporteIaLabel[solicitacao.severidadeInformada]} readOnly />
                </Field>
              </Campo>
              {solicitacao.responsavelNome && (
                <Campo span={12}>
                  <Field label="Responsável">
                    <Input value={solicitacao.responsavelNome} readOnly />
                  </Field>
                </Campo>
              )}
              <Campo span={12}>
                <Field label="Aberto em">
                  <Input value={formatarData(solicitacao.createdAtUtc) ?? '—'} readOnly />
                </Field>
              </Campo>
            </FormGrid>
          </Card>
          {acoes.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis">
              <WorkflowActions acoes={acoes} processando={processando} />
            </Card>
          )}
          {aguardandoValidacaoDoSolicitante && !solicitacao.souSolicitante && (
            <FeedbackInline tom="info">
              Aguardando o solicitante confirmar se a orientação ou correção resolveu o problema.
            </FeedbackInline>
          )}
        </>
      }
    >
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Pedido do cliente">
        <p>{solicitacao.descricao}</p>
      </Card>

      <Card titulo="Triagem da IA">
        <FeedbackInline tom={solicitacao.requerAlteracaoCodigo ? 'aviso' : 'sucesso'}>
          {resultadoTriagemSuporteIaLabel[solicitacao.resultadoTriagem]}
        </FeedbackInline>
        <p>{solicitacao.respostaAoUsuario}</p>
        {solicitacao.requerAlteracaoCodigo && (
          <>
            <Field label="Demanda reduzida"><Textarea value={solicitacao.demandaReduzida} readOnly rows={3} /></Field>
            <Field label="Solução proposta"><Textarea value={solicitacao.solucaoProposta} readOnly rows={3} /></Field>
          </>
        )}
      </Card>

      {solicitacao.notaFechamento && (
        <Card titulo="Nota do responsável">
          <p>{solicitacao.notaFechamento}</p>
        </Card>
      )}

      {solicitacao.comentarioValidacao && (
        <Card titulo="Comentário do solicitante">
          <p>{solicitacao.comentarioValidacao}</p>
        </Card>
      )}

      <EsteiraSuporteIa etapaAtiva={etapaAtivaPorStatus(solicitacao.status)} />
    </DetailPageLayout>
  );
}
