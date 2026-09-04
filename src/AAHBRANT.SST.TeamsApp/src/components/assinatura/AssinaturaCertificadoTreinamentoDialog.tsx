import { useEffect, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Text,
  tokens,
} from '@fluentui/react-components';
import { Checkmark24Filled, PersonBoard24Regular } from '@fluentui/react-icons';
import { api, MetodoAutenticacaoAssinatura, type DocumentoAssinatura } from '../../lib/api';
import { usePageStyles } from '../../pages/pageStyles';
import { AssinaturaQuiosque } from './AssinaturaQuiosque';

function extrairMensagemErro(e: unknown, fallback: string): string {
  if (!(e instanceof Error)) return fallback;
  const trechoJson = e.message.match(/\{.*\}$/);
  if (trechoJson) {
    try {
      const corpo = JSON.parse(trechoJson[0]) as { erro?: string };
      if (typeof corpo.erro === 'string') return corpo.erro;
    } catch {
      // corpo não era JSON — cai no fallback abaixo
    }
  }
  return e.message || fallback;
}

export interface AssinaturaCertificadoTreinamentoDialogProps {
  open: boolean;
  onClose: () => void;
  treinamentoId: string;
  cursoNome: string;
  dataRealizacao: string;
  cargaHorariaRealizada: number;
  obraId: string;
}

// Popup de assinatura disparado logo após "Adicionar treinamento" em TreinamentosTab.tsx — mesmo
// padrão de AssinaturaEntregaEpiDialog.tsx (EPI): o certificado exige DUAS assinaturas, não só a do
// trabalhador. Assinatura do INSTRUTOR/RESPONSÁVEL (quem está logado, ministrando/registrando o
// treinamento) é em um clique via MetodoAutenticacaoAssinatura.SessaoLogada; a do TRABALHADOR que
// recebeu o treinamento reaproveita o AssinaturaQuiosque já existente (digital via leitor Futronic).
// Documento é criado com entidadeTipo="Treinamento"/treinamentoId — o mesmo já usado pela tela
// dedicada /treinamentos/:id/assinar (CriarDocumentoAssinaturaCommand é idempotente).
export function AssinaturaCertificadoTreinamentoDialog({
  open,
  onClose,
  treinamentoId,
  cursoNome,
  dataRealizacao,
  cargaHorariaRealizada,
  obraId,
}: AssinaturaCertificadoTreinamentoDialogProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [assinandoInstrutor, setAssinandoInstrutor] = useState(false);
  const [erroInstrutor, setErroInstrutor] = useState<string | null>(null);

  async function carregarDocumento() {
    try {
      await api.assinatura.criar('Treinamento', treinamentoId);
      const doc = await api.assinatura.obter('Treinamento', treinamentoId);
      setDocumento(doc);
    } catch (e) {
      setErroInstrutor(extrairMensagemErro(e, 'Falha ao preparar a assinatura.'));
    }
  }

  useEffect(() => {
    if (!open) return;
    setDocumento(null);
    setErroInstrutor(null);
    carregarDocumento();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, treinamentoId]);

  const instrutorJaAssinou = documento?.signatarios.some(
    (s) => s.metodoAutenticacao === MetodoAutenticacaoAssinatura.SessaoLogada
  );

  async function assinarComoInstrutor() {
    if (!documento) return;
    try {
      setAssinandoInstrutor(true);
      setErroInstrutor(null);
      await api.assinatura.assinarComSessao(documento.id);
      await carregarDocumento();
    } catch (e) {
      setErroInstrutor(extrairMensagemErro(e, 'Falha ao assinar como instrutor/responsável.'));
    } finally {
      setAssinandoInstrutor(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura do certificado de treinamento</DialogTitle>
          <DialogContent>
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Dados do treinamento
              </Text>
              <Text style={{ display: 'block' }}>Curso: {cursoNome}</Text>
              <Text style={{ display: 'block' }}>Realização: {dataRealizacao.slice(0, 10).split('-').reverse().join('/')}</Text>
              <Text style={{ display: 'block' }}>Carga horária realizada: {cargaHorariaRealizada}h</Text>
              <Text size={200} style={{ display: 'block', marginTop: 4 }}>
                O certificado só fica completo com as duas assinaturas abaixo: quem ministrou/registrou o
                treinamento e o funcionário que participou.
              </Text>
            </div>

            <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                Assinatura do instrutor / responsável
              </Text>
              {erroInstrutor && <Text className={estilos.erro}>{erroInstrutor}</Text>}
              {instrutorJaAssinou ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                  <Text>Assinado.</Text>
                </div>
              ) : (
                <Button
                  appearance="primary"
                  icon={<PersonBoard24Regular />}
                  onClick={assinarComoInstrutor}
                  disabled={assinandoInstrutor || !documento}
                >
                  Assinar como instrutor/responsável
                </Button>
              )}
            </div>

            <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
              Assinatura do funcionário
            </Text>
            <AssinaturaQuiosque entidadeTipo="Treinamento" entidadeId={treinamentoId} obraId={obraId} />
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              Fechar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
