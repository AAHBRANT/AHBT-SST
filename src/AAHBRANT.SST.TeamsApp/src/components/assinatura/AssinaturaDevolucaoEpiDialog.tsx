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

export interface AssinaturaDevolucaoEpiDialogProps {
  open: boolean;
  onClose: () => void;
  entregaId: string;
  obraId: string;
  trabalhadorNome: string;
  epiNome: string;
  quantidadeDevolucao: number;
  dataDevolucao: string;
}

// Popup de assinatura disparado logo após "Confirmar" devolução em EntregasTab.tsx — espelha
// AssinaturaEntregaEpiDialog.tsx, mas usa entidadeTipo="DevolucaoEpi" (mesmo EntregaEpi.Id como
// entidadeId — motor de assinatura é genérico via (EntidadeTipo, EntidadeId), não precisa de
// alteração de schema) e rotula os signatários como "responsável pela devolução" (técnico/consórcio
// logado, assina via sessão) e "trabalhador (devolução)" (empregado que devolve, via crachá/QR + PIN
// ou biometria no AssinaturaQuiosque) — mesma divisão sessão/quiosque já usada em entregador/receptor.
export function AssinaturaDevolucaoEpiDialog({
  open,
  onClose,
  entregaId,
  obraId,
  trabalhadorNome,
  epiNome,
  quantidadeDevolucao,
  dataDevolucao,
}: AssinaturaDevolucaoEpiDialogProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [assinandoResponsavel, setAssinandoResponsavel] = useState(false);
  const [erroResponsavel, setErroResponsavel] = useState<string | null>(null);

  async function carregarDocumento() {
    try {
      await api.assinatura.criar('DevolucaoEpi', entregaId);
      const doc = await api.assinatura.obter('DevolucaoEpi', entregaId);
      setDocumento(doc);
    } catch (e) {
      setErroResponsavel(extrairMensagemErro(e, 'Falha ao preparar a assinatura.'));
    }
  }

  useEffect(() => {
    if (!open) return;
    setDocumento(null);
    setErroResponsavel(null);
    carregarDocumento();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, entregaId]);

  const responsavelJaAssinou = documento?.signatarios.some(
    (s) => s.metodoAutenticacao === MetodoAutenticacaoAssinatura.SessaoLogada
  );

  async function assinarComoResponsavel() {
    if (!documento) return;
    try {
      setAssinandoResponsavel(true);
      setErroResponsavel(null);
      await api.assinatura.assinarComSessao(documento.id);
      await carregarDocumento();
    } catch (e) {
      setErroResponsavel(extrairMensagemErro(e, 'Falha ao assinar como responsável pela devolução.'));
    } finally {
      setAssinandoResponsavel(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura da devolução de EPI</DialogTitle>
          <DialogContent>
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Itens devolvidos
              </Text>
              <Text style={{ display: 'block' }}>Trabalhador: {trabalhadorNome}</Text>
              <Text style={{ display: 'block' }}>EPI: {epiNome}</Text>
              <Text style={{ display: 'block' }}>Quantidade devolvida: {quantidadeDevolucao}</Text>
              <Text style={{ display: 'block' }}>
                Data de devolução: {dataDevolucao.slice(0, 10).split('-').reverse().join('/')}
              </Text>
              <Text size={200} style={{ display: 'block', marginTop: 4 }}>
                O horário de cada assinatura fica registrado abaixo, na tabela de assinaturas registradas.
              </Text>
            </div>

            <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                Assinatura do responsável pela devolução
              </Text>
              {erroResponsavel && <Text className={estilos.erro}>{erroResponsavel}</Text>}
              {responsavelJaAssinou ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                  <Text>Assinado.</Text>
                </div>
              ) : (
                <Button
                  appearance="primary"
                  icon={<PersonBoard24Regular />}
                  onClick={assinarComoResponsavel}
                  disabled={assinandoResponsavel || !documento}
                >
                  Assinar como responsável pela devolução
                </Button>
              )}
            </div>

            <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
              Assinatura do trabalhador (devolução)
            </Text>
            <AssinaturaQuiosque entidadeTipo="DevolucaoEpi" entidadeId={entregaId} obraId={obraId} />
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
