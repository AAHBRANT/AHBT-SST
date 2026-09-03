import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Field,
  Input,
  Tab,
  TabList,
  Text,
  Textarea,
  type SelectTabData,
  type SelectTabEvent,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import {
  ArrowClockwise24Regular,
  ArrowDownload24Regular,
  ArrowLeft24Regular,
  CheckmarkCircle24Regular,
  LockClosed24Regular,
  Pause24Regular,
  Signature24Regular,
} from '@fluentui/react-icons';
import { api, StatusPt, statusPtLabel, type PermissaoTrabalhoDetalhe } from '../../lib/api';
import { usePageStyles, usePillTabStyles } from '../pageStyles';
import { PreRequisitosPtTab } from './PreRequisitosPtTab';
import { TiposTrabalhoPtTab } from './TiposTrabalhoPtTab';
import { VerificacoesPtTab } from './VerificacoesPtTab';
import { EpiEpcPtTab } from './EpiEpcPtTab';
import { RiscosCriticosPtTab } from './RiscosCriticosPtTab';

type AbaPt = 'preRequisitos' | 'tiposTrabalho' | 'verificacoes' | 'episEpcs' | 'riscosCriticos';

// Reformulação literal PT REV.01 (planilha do usuário, 2026-08-29) — mesmo padrão da APR REV.02:
// cabeçalho + ações de fluxo (liberar/suspender/revalidar/encerrar) seguidas das seções fixas do
// formulário (§2 a §6), assinatura/ciência da equipe em página própria (AssinarPtPage) e exportação
// em PDF idêntica ao documento.
export function PermissaoTrabalhoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const estilosAba = usePillTabStyles();
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

  async function autorizar() {
    if (!id || !autorizadoPorUsuarioId) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.autorizar(id, autorizadoPorUsuarioId, responsavelSstUsuarioId || null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao liberar PT.');
    } finally {
      setProcessando(false);
    }
  }

  async function suspender() {
    if (!id || !suspensaPorUsuarioId || !motivoSuspensao) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.suspender(id, motivoSuspensao, suspensaPorUsuarioId);
      setMotivoSuspensao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao suspender PT.');
    } finally {
      setProcessando(false);
    }
  }

  async function revalidar() {
    if (!id || !revalidadaPorUsuarioId || !novaValidade) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.revalidar(id, novaValidade, null, revalidadaPorUsuarioId);
      setNovaValidade('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao revalidar PT.');
    } finally {
      setProcessando(false);
    }
  }

  async function encerrar() {
    if (!id || !encerradaPorUsuarioId) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.encerrar(id, encerradaPorUsuarioId, observacoesEncerramento || null);
      setObservacoesEncerramento('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar PT.');
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

  if (!id) {
    return <Text>Permissão de Trabalho não encontrada.</Text>;
  }

  const pt = detalhe?.permissaoTrabalho;

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 12 }}>
        <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => navigate('/operacao/pt')}>
          Voltar para PT
        </Button>
        <Button appearance="secondary" icon={<ArrowDownload24Regular />} onClick={exportarPdf} disabled={exportando}>
          Exportar PDF
        </Button>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {pt ? (
          <>
            <Text size={500} weight="semibold">
              {pt.numeroPt ? `${pt.numeroPt} — ` : ''}
              {pt.descricaoAtividade}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              {pt.obraNome && <Text>Obra: {pt.obraNome}</Text>}
              <Text>Local: {pt.local}</Text>
              <Text>Data: {pt.data?.slice(0, 10)}</Text>
              {pt.validade && <Text>Validade: {pt.validade.slice(0, 10)}</Text>}
              <Badge appearance="tint">{statusPtLabel[pt.status]}</Badge>
            </div>
            {pt.empresaExecutante && <Text style={{ marginTop: 4 }}>Empresa executante: {pt.empresaExecutante}</Text>}
            {(pt.responsavelExecucaoUsuarioNome || pt.responsavelAreaUsuarioNome) && (
              <Text style={{ marginTop: 4 }}>
                Resp. execução: {pt.responsavelExecucaoUsuarioNome ?? '-'} · Resp. área:{' '}
                {pt.responsavelAreaUsuarioNome ?? '-'}
              </Text>
            )}

            {pt.status === StatusPt.Autorizada && (
              <Text style={{ marginTop: 8 }}>
                Liberada por {pt.autorizadoPorUsuarioNome ?? pt.autorizadoPorUsuarioId} em{' '}
                {pt.dataAutorizacao?.slice(0, 10)}
              </Text>
            )}
            {pt.status === StatusPt.Suspensa && (
              <Text style={{ marginTop: 8 }}>
                Suspensa por {pt.suspensaPorUsuarioNome ?? pt.suspensaPorUsuarioId} em{' '}
                {pt.dataSuspensao?.slice(0, 10)} — Motivo: {pt.motivoSuspensao}
              </Text>
            )}
            {pt.status === StatusPt.Encerrada && (
              <Text style={{ marginTop: 8 }}>
                Encerrada por {pt.encerradaPorUsuarioNome ?? pt.encerradaPorUsuarioId} em{' '}
                {pt.dataEncerramento?.slice(0, 10)}
                {pt.observacoesEncerramento && ` — ${pt.observacoesEncerramento}`}
              </Text>
            )}
            {detalhe && detalhe.responsaveis.length > 0 && (
              <Text style={{ marginTop: 8 }}>
                Equipe executante: {detalhe.responsaveis.map((r) => r.trabalhadorNome).join(', ')}
              </Text>
            )}

            <div style={{ marginTop: 16 }}>
              <Button icon={<Signature24Regular />} onClick={() => navigate(`/operacao/pt/${id}/assinar`)}>
                Assinar PT (ciência da equipe)
              </Button>
            </div>

            {(pt.status === StatusPt.EmElaboracao || pt.status === StatusPt.Suspensa) && (
              <div style={{ display: 'flex', gap: 16, marginTop: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
                <Field label="ID do usuário emitente/liberador (GUID)">
                  <Input value={autorizadoPorUsuarioId} onChange={(_, d) => setAutorizadoPorUsuarioId(d.value)} />
                </Field>
                <Field label="ID do responsável SST (opcional)">
                  <Input
                    value={responsavelSstUsuarioId}
                    onChange={(_, d) => setResponsavelSstUsuarioId(d.value)}
                  />
                </Field>
                <Button
                  appearance="primary"
                  icon={<CheckmarkCircle24Regular />}
                  onClick={autorizar}
                  disabled={processando || !autorizadoPorUsuarioId}
                >
                  Liberar atividade
                </Button>
              </div>
            )}

            {pt.status === StatusPt.Autorizada && (
              <div style={{ display: 'flex', gap: 16, marginTop: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
                <Field label="ID do usuário (GUID)">
                  <Input value={suspensaPorUsuarioId} onChange={(_, d) => setSuspensaPorUsuarioId(d.value)} />
                </Field>
                <Field label="Motivo da suspensão">
                  <Textarea value={motivoSuspensao} onChange={(_, d) => setMotivoSuspensao(d.value)} />
                </Field>
                <Button
                  appearance="secondary"
                  icon={<Pause24Regular />}
                  onClick={suspender}
                  disabled={processando || !suspensaPorUsuarioId || !motivoSuspensao}
                >
                  Suspender
                </Button>
              </div>
            )}

            {(pt.status === StatusPt.Autorizada || pt.status === StatusPt.Suspensa) && (
              <div style={{ display: 'flex', gap: 16, marginTop: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
                <Field label="ID do usuário (GUID)">
                  <Input value={revalidadaPorUsuarioId} onChange={(_, d) => setRevalidadaPorUsuarioId(d.value)} />
                </Field>
                <Field label="Nova validade">
                  <CampoData value={novaValidade} onChange={(_, d) => setNovaValidade(d.value)} />
                </Field>
                <Button
                  appearance="secondary"
                  icon={<ArrowClockwise24Regular />}
                  onClick={revalidar}
                  disabled={processando || !revalidadaPorUsuarioId || !novaValidade}
                >
                  Revalidar
                </Button>
              </div>
            )}

            {(pt.status === StatusPt.Autorizada || pt.status === StatusPt.Suspensa) && (
              <div style={{ display: 'flex', gap: 16, marginTop: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
                <Field label="ID do usuário que encerra (GUID)">
                  <Input value={encerradaPorUsuarioId} onChange={(_, d) => setEncerradaPorUsuarioId(d.value)} />
                </Field>
                <Field label="Observações">
                  <Textarea value={observacoesEncerramento} onChange={(_, d) => setObservacoesEncerramento(d.value)} />
                </Field>
                <Button
                  appearance="secondary"
                  icon={<LockClosed24Regular />}
                  onClick={encerrar}
                  disabled={processando || !encerradaPorUsuarioId}
                >
                  Encerrar
                </Button>
              </div>
            )}
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPt)}
        className={estilosAba.lista}
      >
        <Tab value="preRequisitos">Pré-requisitos</Tab>
        <Tab value="tiposTrabalho">Tipos de trabalho</Tab>
        <Tab value="verificacoes">Verificações</Tab>
        <Tab value="episEpcs">EPIs/EPCs</Tab>
        <Tab value="riscosCriticos">Riscos críticos</Tab>
      </TabList>

      {detalhe && pt && (
        <>
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
        </>
      )}
    </div>
  );
}
