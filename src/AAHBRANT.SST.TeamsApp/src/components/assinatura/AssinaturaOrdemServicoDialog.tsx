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
import { Checkmark24Filled, ShieldCheckmark24Regular } from '@fluentui/react-icons';
import { api, MetodoAutenticacaoAssinatura, type DocumentoAssinatura, type OrdemServicoEmitida } from '../../lib/api';
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

export interface AssinaturaOrdemServicoDialogProps {
  open: boolean;
  onClose: () => void;
  ordemServico: OrdemServicoEmitida;
  trabalhadorId: string;
  trabalhadorNome: string;
}

// Assinatura da Ordem de Serviço (03/11/2026), na ordem aprovada: primeiro o funcionário, por digital ou
// facial (AssinaturaQuiosque); depois o técnico de segurança, com o próprio login. A OS só fica válida com
// as duas — o servidor recusa a do técnico antes da do funcionário e fecha o documento depois dela.
// Mesmo padrão de AssinaturaCertificadoTreinamentoDialog.tsx.
export function AssinaturaOrdemServicoDialog({ open, onClose, ordemServico, trabalhadorId, trabalhadorNome }: AssinaturaOrdemServicoDialogProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [assinandoTecnico, setAssinandoTecnico] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function carregarDocumento() {
    try {
      setDocumento(await api.assinatura.obter('OrdemServico', ordemServico.id));
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao preparar a assinatura.'));
    }
  }

  const funcionarioAssinou = documento?.signatarios.some(
    (s) => s.trabalhadorId === trabalhadorId && s.metodoAutenticacao !== MetodoAutenticacaoAssinatura.SessaoLogada
  ) ?? false;
  const tecnicoAssinou = documento?.signatarios.some(
    (s) => s.metodoAutenticacao === MetodoAutenticacaoAssinatura.SessaoLogada
  ) ?? false;

  useEffect(() => {
    if (!open) return;
    setDocumento(null);
    setErro(null);
    void carregarDocumento();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, ordemServico.id]);

  // A assinatura do funcionário acontece dentro do quiosque; consulta de novo até ela chegar para
  // liberar o botão do técnico.
  useEffect(() => {
    if (!open || funcionarioAssinou) return;
    const id = window.setInterval(() => void carregarDocumento(), 3000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, funcionarioAssinou, ordemServico.id]);

  async function assinarComoTecnico() {
    if (!documento) return;
    try {
      setAssinandoTecnico(true);
      setErro(null);
      await api.assinatura.assinarComSessao(documento.id);
      await carregarDocumento();
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao assinar como técnico de segurança.'));
    } finally {
      setAssinandoTecnico(false);
    }
  }

  const assinado = (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
      <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
      <Text>Assinado.</Text>
    </div>
  );

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura da Ordem de Serviço {ordemServico.numero}</DialogTitle>
          <DialogContent>
            <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
              A OS só fica válida com as duas assinaturas: primeiro o funcionário, por digital ou facial; depois o técnico de
              segurança, com o próprio login.
            </Text>
            {erro && <Text className={estilos.erro} style={{ display: 'block', marginBottom: 8 }}>{erro}</Text>}

            <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>1. Funcionário — {trabalhadorNome}</Text>
            {funcionarioAssinou
              ? <div className={estilos.card} style={{ marginBottom: 16 }}>{assinado}</div>
              : <div style={{ marginBottom: 16 }}><AssinaturaQuiosque entidadeTipo="OrdemServico" entidadeId={ordemServico.id} obraId={ordemServico.obraId} /></div>}

            <div className={estilos.card} style={{ maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>2. Técnico de Segurança do Trabalho</Text>
              {tecnicoAssinou ? assinado : (
                <>
                  <Button
                    appearance="primary"
                    icon={<ShieldCheckmark24Regular />}
                    onClick={assinarComoTecnico}
                    disabled={assinandoTecnico || !documento || !funcionarioAssinou}
                  >
                    Assinar como técnico de segurança
                  </Button>
                  {!funcionarioAssinou && (
                    <Text size={200} style={{ display: 'block', marginTop: 6 }}>Liberado depois da assinatura do funcionário.</Text>
                  )}
                </>
              )}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>Fechar</Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
