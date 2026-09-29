import { pendenciasGrade } from '../../lib/dadosFoto';
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Field,
  Input,
  Card,
  DetailPageLayout,
  WorkflowActions,
  StatusChip,
  FeedbackInline,
  Carregando,
  FormGrid,
  Campo,
  type AcaoWorkflow,
  type Tom,
} from '@ui';
import {
  ArrowDownload24Regular,
  Eye24Regular,
  Signature24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusDds,
  statusDdsLabel,
  type DdsDetalhe,
} from '../../lib/api';
import { ParticipantesDds } from './ParticipantesDds';
import { GradeFotosEvidencia } from '../../components/GradeFotosEvidencia';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';

const TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS = 3;

const tomStatusDia: Record<number, Tom> = {
  [StatusDds.EmAndamento]: 'atencao',
  [StatusDds.Concluido]: 'ok',
};

export function DdsDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<DdsDetalhe | null>(null);
  const [fotosEvidenciaPreview, setFotosEvidenciaPreview] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoPdf, setBaixandoPdf] = useState(false);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const det = await api.dds.obterDetalhe(id);
      setDetalhe(det);

      const previews = await Promise.all(
        det.fotosEvidencia.map(async (foto) => {
          try {
            const blob = await api.dds.baixarFotoEvidencia(foto.id);
            return [foto.id, URL.createObjectURL(blob)] as const;
          } catch {
            return null;
          }
        }),
      );
      setFotosEvidenciaPreview(Object.fromEntries(previews.filter((p): p is [string, string] => p !== null)));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar DDS.');
    }
  }

  async function anexarFotoEvidencia(ordem: number, arquivo: File) {
    if (!id) return;
    try {
      setErro(null);
      await api.dds.anexarFotoEvidencia(id, ordem, arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao anexar foto de evidência.');
    }
  }

  async function removerFotoEvidencia(fotoId: string) {
    try {
      setErro(null);
      await api.dds.removerFotoEvidencia(fotoId);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao remover foto de evidência.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function encerrar() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.encerrar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar DDS.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function baixarPdf() {
    if (!id || !detalhe) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.dds.baixarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = nomeArquivoPdf(detalhe);
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o PDF do DDS.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  function nomeArquivoPdf(det: DdsDetalhe) {
    return `dds-${det.dds.data?.slice(0, 10)}.pdf`;
  }

  // Abre o mesmo PDF do "Baixar PDF" numa janela, sem baixar.
  function visualizarPdf() {
    if (!id || !detalhe) return;
    void visualizar({ titulo: 'PDF do DDS', nomeArquivo: nomeArquivoPdf(detalhe), obter: () => api.dds.baixarPdf(id) });
  }

  if (!id) return <FeedbackInline tom="erro">DDS não encontrado.</FeedbackInline>;

  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  const dds = detalhe.dds;
  const somenteLeitura = dds.status !== StatusDds.EmAndamento;
  const totalFotosEvidencia = detalhe.fotosEvidencia.length;
  const faltamFotosEvidencia = Math.max(0, TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS - totalFotosEvidencia);

  const tituloDds =
    dds.temasAtividades.length > 0
      ? dds.temasAtividades.map((t) => t.atividadeNome + (t.perigoNome ? ` — ${t.perigoNome}` : '')).join(' · ')
      : dds.temaLivreNome
        ? `Tema livre: ${dds.temaLivreNome}`
        : 'DDS do dia';

  // Só a transição de estado real (encerrar) entra em WorkflowActions — assinar/baixar
  // são ações utilitárias sempre disponíveis, não mudam o status, então ficam no cabeçalho.
  const acoesWorkflow: AcaoWorkflow[] = [];
  if (!somenteLeitura) {
    acoesWorkflow.push({
      chave: 'encerrar',
      rotulo: 'Finalizar DDS',
      finalizacao: true,
      pendencias: pendenciasGrade(detalhe.fotosEvidencia),
      descricao: faltamFotosEvidencia > 0 ? `Faltam ${faltamFotosEvidencia} foto(s) de evidência.` : undefined,
      tom: 'primario',
      habilitada: true,
      aoExecutar: encerrar,
    });
  }

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: tituloDds,
        subtitulo: `Obra: ${dds.obraNome} · Data: ${dds.data?.slice(0, 10)} · Responsável: ${dds.responsavelUsuarioNome}`,
        status: <StatusChip tom={tomStatusDia[dds.status] ?? 'neutro'}>{statusDdsLabel[dds.status]}</StatusChip>,
        voltarPara: dds.ddsSemanalId ? `/prevencao/dds/semana/${dds.ddsSemanalId}` : '/prevencao/dds',
        rotuloVoltar: 'Voltar para a semana',
        acoes: (
          <>
            <Button icon={<Signature24Regular />} onClick={() => navigate(`/prevencao/dds/dia/${id}/assinar`)}>
              Assinar DDS
            </Button>
            <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={visualizarPdf} aria-label="Visualizar PDF">
              Visualizar PDF
            </BotaoAcao>
            <BotaoAcao tom="baixar" icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf} aria-label="Baixar PDF">
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
                <Field label="Atividades do dia"><Input value={dds.atividadesNomes.join(', ') || 'DDS do dia'} readOnly /></Field>
              </Campo>
              <Campo span={12}>
                <Field label="Presenças confirmadas"><Input value={String(dds.totalParticipantes)} readOnly /></Field>
              </Campo>
              <Campo span={12}>
                <Field label="Evidências fotográficas"><Input value={`${totalFotosEvidencia}/${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS}`} readOnly /></Field>
              </Campo>
            </FormGrid>
          </Card>
          {acoesWorkflow.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis">
              <WorkflowActions acoes={acoesWorkflow} processando={processando} erro={erro} />
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

      <ParticipantesDds key={dds.id} detalhe={detalhe} somenteLeitura={somenteLeitura} aoAtualizar={setDetalhe} />

      <Card>
        <GradeFotosEvidencia
          contextoFoto={{ obraId: dds.obraId, obraNome: dds.obraNome }}
          titulo="Evidências fotográficas"
          subtitulo={`${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS} fotos são obrigatórias para liberar o encerramento deste registro diário.`}
          total={TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS}
          fotos={
            detalhe.fotosEvidencia
              .map((f) => ({ ordem: f.ordem, id: f.id, dadosFoto: f.dadosFoto, url: fotosEvidenciaPreview[f.id] }))
          }
          somenteLeitura={somenteLeitura}
          onSelecionarFoto={anexarFotoEvidencia}
          onRemoverFoto={removerFotoEvidencia}
          onErroValidacao={setErro}
        />
      </Card>

    </DetailPageLayout>
  );
}
