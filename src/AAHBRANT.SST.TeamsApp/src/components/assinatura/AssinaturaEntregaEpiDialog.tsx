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
import { FotoCatalogoEpi } from '../../pages/epi/FotoCatalogoEpi';
import { clausulasTermoCompromisso } from './termoEpi';

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

export interface AssinaturaEntregaEpiDialogProps {
  open: boolean;
  onClose: () => void;
  entregaId: string;
  trabalhadorNome: string;
  epiNome: string;
  catalogoEpiId: string;
  epiTemFoto: boolean;
  quantidade: number;
  dataEntrega: string;
  numeroListaPresencaNr6?: string | null;
  dataTreinamentoNr6?: string | null;
}

// Popup de assinatura disparado logo após "Registrar entrega" em EntregasTab.tsx — layout dependente
// da obra fica para reformulação futura (fora de escopo aqui). Assinatura do ENTREGADOR (técnico
// logado) é em um clique via MetodoAutenticacaoAssinatura.SessaoLogada (novo endpoint
// /assinar/sessao); a do RECEPTOR (trabalhador) reaproveita o AssinaturaQuiosque já existente
// (crachá/PIN ou biometria). Documento é criado com entidadeTipo="EntregaEpi"/entregaId — o mesmo
// criado pelo AssinaturaQuiosque ao montar (CriarDocumentoAssinaturaCommand é idempotente).
export function AssinaturaEntregaEpiDialog({
  open,
  onClose,
  entregaId,
  trabalhadorNome,
  epiNome,
  catalogoEpiId,
  epiTemFoto,
  quantidade,
  dataEntrega,
  numeroListaPresencaNr6,
  dataTreinamentoNr6,
}: AssinaturaEntregaEpiDialogProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [assinandoEntregador, setAssinandoEntregador] = useState(false);
  const [erroEntregador, setErroEntregador] = useState<string | null>(null);

  async function carregarDocumento() {
    try {
      await api.assinatura.criar('EntregaEpi', entregaId);
      const doc = await api.assinatura.obter('EntregaEpi', entregaId);
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
          <DialogTitle>Assinatura da entrega de EPI</DialogTitle>
          <DialogContent>
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Itens entregues
              </Text>
              {/* Foto do EPI em destaque (pedido do usuário, 03/09) — o trabalhador reconhece
                  visualmente o que está recebendo antes de assinar, não só o nome. */}
              <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
                <FotoCatalogoEpi catalogoEpiId={catalogoEpiId} temFoto={epiTemFoto} tamanho={96} />
                <div>
                  <Text style={{ display: 'block' }}>Funcionário: {trabalhadorNome}</Text>
                  <Text style={{ display: 'block' }}>EPI: {epiNome}</Text>
                  <Text style={{ display: 'block' }}>Quantidade: {quantidade}</Text>
                  <Text style={{ display: 'block' }}>
                    Data de entrega: {dataEntrega.slice(0, 10).split('-').reverse().join('/')}
                  </Text>
                  <Text size={200} style={{ display: 'block', marginTop: 4 }}>
                    O horário de cada assinatura fica registrado abaixo, na tabela de assinaturas registradas.
                  </Text>
                </div>
              </div>
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
                Termo de Recebimento e Compromisso de Uso
              </Text>
              <ol style={{ margin: 0, paddingLeft: 20 }}>
                {clausulasTermoCompromisso(numeroListaPresencaNr6, dataTreinamentoNr6).map((clausula, indice) => (
                  <li key={indice} style={{ marginBottom: 6 }}>
                    <Text size={200}>{clausula}</Text>
                  </li>
                ))}
              </ol>
            </div>

            <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
              Assinatura do receptor
            </Text>
            <AssinaturaQuiosque entidadeTipo="EntregaEpi" entidadeId={entregaId} />
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
