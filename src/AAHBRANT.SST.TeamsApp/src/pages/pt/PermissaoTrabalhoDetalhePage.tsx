import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Abas,
  Button,
  Campo,
  Card,
  Carregando,
  DetailPageLayout,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  StatusChip,
  Textarea,
  WorkflowActions,
  type AcaoWorkflow,
  type Tom,
} from '@ui';
import { CampoData } from '../../components/CampoData';
import { ArrowDownload24Regular, Signature24Regular } from '@fluentui/react-icons';
import { api, StatusPt, statusPtLabel, type PermissaoTrabalhoDetalhe } from '../../lib/api';
import { PreRequisitosPtTab } from './PreRequisitosPtTab';
import { TiposTrabalhoPtTab } from './TiposTrabalhoPtTab';
import { VerificacoesPtTab } from './VerificacoesPtTab';
import { EpiEpcPtTab } from './EpiEpcPtTab';
import { RiscosCriticosPtTab } from './RiscosCriticosPtTab';

const ABAS_PT = ['preRequisitos', 'tiposTrabalho', 'verificacoes', 'episEpcs', 'riscosCriticos'] as const;
type AbaPt = (typeof ABAS_PT)[number];

// Mesmo mapeamento de tons já usado em AprDetalhePage.tsx (Task 11) — PT não tem estado equivalente
// a "reprovada", então Suspensa (estado de cautela/pausa) ocupa o tom 'atencao'.
const tomPorStatusPt: Record<number, Tom> = {
  [StatusPt.EmElaboracao]: 'neutro',
  [StatusPt.Autorizada]: 'ok',
  [StatusPt.Suspensa]: 'atencao',
  [StatusPt.Encerrada]: 'info',
};

// Reformulação literal PT REV.01 (planilha do usuário, 2026-08-29) — mesmo padrão da APR REV.02:
// cabeçalho + ações de fluxo (liberar/suspender/revalidar/encerrar) seguidas das seções fixas do
// formulário (§2 a §6), assinatura/ciência da equipe em página própria (AssinarPtPage) e exportação
// em PDF idêntica ao documento.
// Onda 2 Task 7 (camada ui/): DetailPageLayout com resumo e WorkflowActions na lateral, mesmo padrão
// de AprDetalhePage.tsx (Task 11) — aqui com 4 ações de fluxo em vez de 2 (aprovar/reprovar da APR),
// já que o ciclo de vida da PT tem mais transições (liberar, suspender, revalidar, encerrar).
export function PermissaoTrabalhoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [aba, setAba] = useState<AbaPt>('preRequisitos');
  const [detalhe, setDetalhe] = useState<PermissaoTrabalhoDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [exportando, setExportando] = useState(false);

  const [autorizadoPorUsuarioId, setAutorizadoPorUsuarioId] = useState('');
  const [responsavelSstUsuarioId, setResponsavelSstUsuarioId] = useState('');
  const [suspensaPorUsuarioId, setSuspensaPorUsuarioId] = useState('');
  const [motivoSuspensao, setMotivoSuspensao] = useState('');
  const [revalidadaPorUsuarioId, setRevalidadaPorUsuarioId] = useState('');
  const [novaValidade, setNovaValidade] = useState('');
  const [encerradaPorUsuarioId, setEncerradaPorUsuarioId] = useState('');
  const [observacoesEncerramento, setObservacoesEncerramento] = useState('');
  const [processando, setProcessando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setDetalhe(await api.permissoesTrabalho.obterDetalhe(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar Permissão de Trabalho.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // As 4 ações abaixo entram no WorkflowActions e seguem o mesmo contrato de AprDetalhePage.tsx:
  // `false` mantém o formulário aberto — tanto a guarda local (campo vazio) quanto a falha da API
  // devolvem `false`, senão o acordeão fecharia e a mensagem de erro ficaria fora da vista.
  async function autorizar() {
    if (!id || !autorizadoPorUsuarioId) return false;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.autorizar(id, autorizadoPorUsuarioId, responsavelSstUsuarioId || null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao liberar PT.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function suspender() {
    if (!id || !suspensaPorUsuarioId || !motivoSuspensao) return false;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.suspender(id, motivoSuspensao, suspensaPorUsuarioId);
      setMotivoSuspensao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao suspender PT.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function revalidar() {
    if (!id || !revalidadaPorUsuarioId || !novaValidade) return false;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.revalidar(id, novaValidade, null, revalidadaPorUsuarioId);
      setNovaValidade('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao revalidar PT.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function encerrar() {
    if (!id || !encerradaPorUsuarioId) return false;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.encerrar(id, encerradaPorUsuarioId, observacoesEncerramento || null);
      setObservacoesEncerramento('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar PT.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function exportarPdf() {
    if (!id) return;
    try {
      setExportando(true);
      setErro(null);
      const blob = await api.permissoesTrabalho.exportarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `pt-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao exportar PDF da PT.');
    } finally {
      setExportando(false);
    }
  }

  if (!id) return <FeedbackInline tom="erro">Permissão de Trabalho não encontrada.</FeedbackInline>;

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre e a falha
  // (API fora, id inexistente) não teria onde ser lida — mesmo padrão de AprDetalhePage.tsx.
  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  const pt = detalhe.permissaoTrabalho;

  const acoes: AcaoWorkflow[] = [];
  if (pt.status === StatusPt.EmElaboracao || pt.status === StatusPt.Suspensa) {
    acoes.push({
      chave: 'autorizar',
      rotulo: 'Liberar atividade',
      tom: 'primario',
      habilitada: true,
      aoExecutar: autorizar,
      formulario: (
        <>
          <Field label="ID do usuário emitente/liberador (GUID)" required>
            <Input value={autorizadoPorUsuarioId} onChange={(_, d) => setAutorizadoPorUsuarioId(d.value)} />
          </Field>
          <Field label="ID do responsável SST (opcional)">
            <Input value={responsavelSstUsuarioId} onChange={(_, d) => setResponsavelSstUsuarioId(d.value)} />
          </Field>
        </>
      ),
    });
  }
  if (pt.status === StatusPt.Autorizada) {
    acoes.push({
      chave: 'suspender',
      rotulo: 'Suspender',
      tom: 'destrutivo',
      habilitada: true,
      aoExecutar: suspender,
      formulario: (
        <>
          <Field label="ID do usuário (GUID)" required>
            <Input value={suspensaPorUsuarioId} onChange={(_, d) => setSuspensaPorUsuarioId(d.value)} />
          </Field>
          <Field label="Motivo da suspensão" required>
            <Textarea value={motivoSuspensao} onChange={(_, d) => setMotivoSuspensao(d.value)} />
          </Field>
        </>
      ),
    });
  }
  if (pt.status === StatusPt.Autorizada || pt.status === StatusPt.Suspensa) {
    acoes.push({
      chave: 'revalidar',
      rotulo: 'Revalidar',
      tom: 'neutro',
      habilitada: true,
      aoExecutar: revalidar,
      formulario: (
        <>
          <Field label="ID do usuário (GUID)" required>
            <Input value={revalidadaPorUsuarioId} onChange={(_, d) => setRevalidadaPorUsuarioId(d.value)} />
          </Field>
          <Field label="Nova validade" required>
            <CampoData value={novaValidade} onChange={(_, d) => setNovaValidade(d.value)} />
          </Field>
        </>
      ),
    });
    acoes.push({
      chave: 'encerrar',
      rotulo: 'Encerrar',
      tom: 'primario',
      habilitada: true,
      aoExecutar: encerrar,
      formulario: (
        <>
          <Field label="ID do usuário que encerra (GUID)" required>
            <Input value={encerradaPorUsuarioId} onChange={(_, d) => setEncerradaPorUsuarioId(d.value)} />
          </Field>
          <Field label="Observações">
            <Textarea value={observacoesEncerramento} onChange={(_, d) => setObservacoesEncerramento(d.value)} />
          </Field>
        </>
      ),
    });
  }

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: `${pt.numeroPt ? pt.numeroPt + ' — ' : ''}${pt.descricaoAtividade}`,
        status: <StatusChip tom={tomPorStatusPt[pt.status] ?? 'neutro'}>{statusPtLabel[pt.status]}</StatusChip>,
        voltarPara: '/operacao/pt',
        rotuloVoltar: 'Voltar para PT',
        acoes: (
          <>
            <Button
              appearance="secondary"
              icon={<Signature24Regular />}
              onClick={() => navigate(`/operacao/pt/${id}/assinar`)}
            >
              Assinar PT (ciência da equipe)
            </Button>
            <Button
              appearance="secondary"
              icon={<ArrowDownload24Regular />}
              onClick={exportarPdf}
              disabled={exportando}
            >
              Exportar PDF
            </Button>
          </>
        ),
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              {pt.obraNome && (
                <Campo span={12}>
                  <Field label="Obra">
                    <Input value={pt.obraNome} readOnly />
                  </Field>
                </Campo>
              )}
              <Campo span={12}>
                <Field label="Local">
                  <Input value={pt.local} readOnly />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Data">
                  <Input value={pt.data?.slice(0, 10) ?? ''} readOnly />
                </Field>
              </Campo>
              {pt.validade && (
                <Campo span={12}>
                  <Field label="Validade">
                    <Input value={pt.validade.slice(0, 10)} readOnly />
                  </Field>
                </Campo>
              )}
              {pt.empresaExecutante && (
                <Campo span={12}>
                  <Field label="Empresa executante">
                    <Input value={pt.empresaExecutante} readOnly />
                  </Field>
                </Campo>
              )}
              {(pt.responsavelExecucaoUsuarioNome || pt.responsavelAreaUsuarioNome) && (
                <Campo span={12}>
                  <Field label="Resp. execução / área">
                    <Input
                      value={`${pt.responsavelExecucaoUsuarioNome ?? '-'} · ${pt.responsavelAreaUsuarioNome ?? '-'}`}
                      readOnly
                    />
                  </Field>
                </Campo>
              )}
              {detalhe.responsaveis.length > 0 && (
                <Campo span={12}>
                  <Field label="Equipe executante">
                    <Input value={detalhe.responsaveis.map((r) => r.trabalhadorNome).join(', ')} readOnly />
                  </Field>
                </Campo>
              )}
              {pt.status === StatusPt.Autorizada && (
                <Campo span={12}>
                  <Field label="Liberação">
                    <Input
                      value={`Liberada por ${pt.autorizadoPorUsuarioNome ?? pt.autorizadoPorUsuarioId} em ${pt.dataAutorizacao?.slice(0, 10) ?? ''}`}
                      readOnly
                    />
                  </Field>
                </Campo>
              )}
              {pt.status === StatusPt.Suspensa && (
                <Campo span={12}>
                  <Field label="Suspensão">
                    <Input
                      value={`Suspensa por ${pt.suspensaPorUsuarioNome ?? pt.suspensaPorUsuarioId} em ${pt.dataSuspensao?.slice(0, 10) ?? ''} — Motivo: ${pt.motivoSuspensao}`}
                      readOnly
                    />
                  </Field>
                </Campo>
              )}
              {pt.status === StatusPt.Encerrada && (
                <Campo span={12}>
                  <Field label="Encerramento">
                    <Input
                      value={`Encerrada por ${pt.encerradaPorUsuarioNome ?? pt.encerradaPorUsuarioId} em ${pt.dataEncerramento?.slice(0, 10) ?? ''}${pt.observacoesEncerramento ? ` — ${pt.observacoesEncerramento}` : ''}`}
                      readOnly
                    />
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

      <Abas
        nivel="interno"
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções da PT"
        abas={[
          { valor: 'preRequisitos', rotulo: 'Pré-requisitos' },
          { valor: 'tiposTrabalho', rotulo: 'Tipos de trabalho' },
          { valor: 'verificacoes', rotulo: 'Verificações' },
          { valor: 'episEpcs', rotulo: 'EPIs/EPCs' },
          { valor: 'riscosCriticos', rotulo: 'Riscos críticos' },
        ]}
      />

      {aba === 'preRequisitos' && (
        <PreRequisitosPtTab permissaoTrabalhoId={id} itens={detalhe.preRequisitos} aoAtualizar={carregar} />
      )}
      {aba === 'tiposTrabalho' && (
        <TiposTrabalhoPtTab permissaoTrabalhoId={id} itens={detalhe.tiposTrabalho} aoAtualizar={carregar} />
      )}
      {aba === 'verificacoes' && (
        <VerificacoesPtTab permissaoTrabalhoId={id} itens={detalhe.verificacoes} aoAtualizar={carregar} />
      )}
      {aba === 'episEpcs' && (
        <EpiEpcPtTab
          permissaoTrabalhoId={id}
          pt={pt}
          episAtuais={detalhe.epis}
          epcsAtuais={detalhe.epcs}
          aoAtualizar={carregar}
        />
      )}
      {aba === 'riscosCriticos' && (
        <RiscosCriticosPtTab permissaoTrabalhoId={id} itens={detalhe.riscosCriticos} aoAtualizar={carregar} />
      )}
    </DetailPageLayout>
  );
}
