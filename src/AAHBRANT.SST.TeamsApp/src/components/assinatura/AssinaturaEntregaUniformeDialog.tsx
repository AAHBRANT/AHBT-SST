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

export interface AssinaturaEntregaUniformeDialogProps {
  open: boolean;
  onClose: () => void;
  entregaId: string;
  trabalhadorNome: string;
  pecaNome: string;
  tamanho: string;
  quantidade: number;
  dataEntrega: string;
}

// Popup de assinatura disparado logo após "Registrar entrega" em EntregaUniformeTab.tsx — mesma
// estrutura de AssinaturaEntregaEpiDialog.tsx (assinatura do entregador em 1 clique via sessão
// logada; assinatura do receptor via AssinaturaQuiosque, crachá/PIN ou biometria). Termo de
// Recebimento é um rascunho próprio (ver aviso jurídico no topo da Task 11 do plano de
// implementação) — não uma transcrição de modelo oficial como o do EPI.
export function AssinaturaEntregaUniformeDialog({
  open,
  onClose,
  entregaId,
  trabalhadorNome,
  pecaNome,
  tamanho,
  quantidade,
  dataEntrega,
}: AssinaturaEntregaUniformeDialogProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [assinandoEntregador, setAssinandoEntregador] = useState(false);
  const [erroEntregador, setErroEntregador] = useState<string | null>(null);

  async function carregarDocumento() {
    try {
      await api.assinatura.criar('EntregaUniforme', entregaId);
      const doc = await api.assinatura.obter('EntregaUniforme', entregaId);
      setDocumento(doc);
    } catch (e) {
      setErroEntregador(extrairMensagemErro(e, 'Falha ao preparar a assinatura.'));
    }
  }

  useEffect(() => {
    if (!open) return;
    setDocumento(null);
    setErroEntregador(null);
    carregarDocumento();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, entregaId]);

  const entregadorJaAssinou = documento?.signatarios.some(
    (s) => s.metodoAutenticacao === MetodoAutenticacaoAssinatura.SessaoLogada
  );

  async function assinarComoEntregador() {
    if (!documento) return;
    try {
      setAssinandoEntregador(true);
      setErroEntregador(null);
      await api.assinatura.assinarComSessao(documento.id);
      await carregarDocumento();
    } catch (e) {
      setErroEntregador(extrairMensagemErro(e, 'Falha ao assinar como entregador.'));
    } finally {
      setAssinandoEntregador(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura da entrega de uniforme</DialogTitle>
          <DialogContent>
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Item entregue
              </Text>
              <Text style={{ display: 'block' }}>Funcionário: {trabalhadorNome}</Text>
              <Text style={{ display: 'block' }}>
                Peça: {pecaNome} — tamanho {tamanho}
              </Text>
              <Text style={{ display: 'block' }}>Quantidade: {quantidade}</Text>
              <Text style={{ display: 'block' }}>
                Data de entrega: {dataEntrega.slice(0, 10).split('-').reverse().join('/')}
              </Text>
            </div>

            <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                Assinatura do entregador
              </Text>
              {erroEntregador && <Text className={estilos.erro}>{erroEntregador}</Text>}
              {entregadorJaAssinou ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                  <Text>Assinado.</Text>
                </div>
              ) : (
                <Button
                  appearance="primary"
                  icon={<PersonBoard24Regular />}
                  onClick={assinarComoEntregador}
                  disabled={assinandoEntregador || !documento}
                >
                  Assinar como entregador
                </Button>
              )}
            </div>

            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Termo de Recebimento de Uniforme
              </Text>
              <ol style={{ margin: 0, paddingLeft: 20 }}>
                <li style={{ marginBottom: 6 }}>
                  <Text size={200}>
                    Declaro ter recebido a(s) peça(s) de uniforme relacionada(s) nesta ficha, na quantidade e
                    tamanho indicados, em condições adequadas de uso.
                  </Text>
                </li>
                <li style={{ marginBottom: 6 }}>
                  <Text size={200}>
                    Comprometo-me a utilizar o uniforme conforme o padrão da obra/empresa durante o horário de
                    trabalho, zelando por sua guarda, conservação e higienização adequadas.
                  </Text>
                </li>
                <li style={{ marginBottom: 6 }}>
                  <Text size={200}>
                    Comprometo-me a comunicar imediatamente ao responsável qualquer dano ou extravio que torne a
                    peça imprópria para uso, e a devolvê-la sempre que solicitado, inclusive em caso de
                    desligamento.
                  </Text>
                </li>
              </ol>
            </div>

            <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
              Assinatura do receptor
            </Text>
            <AssinaturaQuiosque entidadeTipo="EntregaUniforme" entidadeId={entregaId} />
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
