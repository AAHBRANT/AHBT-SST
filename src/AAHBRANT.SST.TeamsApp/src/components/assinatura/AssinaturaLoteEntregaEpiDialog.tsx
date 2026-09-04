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
import { Checkmark24Filled, Fingerprint24Regular, PersonBoard24Regular, Warning24Regular } from '@fluentui/react-icons';
import { api, MetodoAutenticacaoAssinatura, type DocumentoAssinatura } from '../../lib/api';
import { usePageStyles } from '../../pages/pageStyles';
import { FotoCatalogoEpi } from '../../pages/epi/FotoCatalogoEpi';
import { clausulasTermoCompromisso } from './termoEpi';
import {
  capturarDigitalLocal,
  estaAgenteLocalDisponivel,
  obterDispositivoLocal,
} from '../../lib/agenteBiometricoLocal';

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

export interface ItemAssinaturaLote {
  entregaId: string;
  catalogoEpiId: string;
  catalogoEpiNome: string;
  epiTemFoto: boolean;
  quantidade: number;
}

export interface AssinaturaLoteEntregaEpiDialogProps {
  open: boolean;
  onClose: () => void;
  itens: ItemAssinaturaLote[];
  trabalhadorNome: string;
  dataEntrega: string;
  numeroListaPresencaNr6?: string | null;
  dataTreinamentoNr6?: string | null;
}

// Tela de Entrega Rápida (pedido do usuário, 04/09) — versão "em lote" de AssinaturaEntregaEpiDialog:
// em vez de uma assinatura por item, captura a digital UMA VEZ (capturarDigitalLocal) e aplica o
// mesmo resultado (trabalhadorId/score) a cada DocumentoAssinatura do lote — o backend já é
// per-documento (RegistrarAssinaturaBiometriaLocalCommand), então isso não exige nenhuma mudança
// no Motor de Assinatura Eletrônica, só orquestra N chamadas do endpoint que já existe.
export function AssinaturaLoteEntregaEpiDialog({
  open,
  onClose,
  itens,
  trabalhadorNome,
  dataEntrega,
  numeroListaPresencaNr6,
  dataTreinamentoNr6,
}: AssinaturaLoteEntregaEpiDialogProps) {
  const estilos = usePageStyles();
  const [documentos, setDocumentos] = useState<Record<string, DocumentoAssinatura>>({});
  const [carregando, setCarregando] = useState(true);
  const [assinandoEntregador, setAssinandoEntregador] = useState(false);
  const [assinandoBiometria, setAssinandoBiometria] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [agenteLocalDisponivel, setAgenteLocalDisponivel] = useState(false);

  useEffect(() => {
    estaAgenteLocalDisponivel().then(setAgenteLocalDisponivel);
  }, []);

  async function carregarDocumentos() {
    try {
      setErro(null);
      const entradas = await Promise.all(
        itens.map(async (item) => {
          await api.assinatura.criar('EntregaEpi', item.entregaId);
          const doc = await api.assinatura.obter('EntregaEpi', item.entregaId);
          return [item.entregaId, doc] as const;
        }),
      );
      const mapa: Record<string, DocumentoAssinatura> = {};
      for (const [entregaId, doc] of entradas) {
        if (doc) mapa[entregaId] = doc;
      }
      setDocumentos(mapa);
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao preparar as assinaturas do lote.'));
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    if (!open) return;
    setDocumentos({});
    setErro(null);
    setCarregando(true);
    carregarDocumentos();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  function jaAssinouComo(entregaId: string, metodo: number): boolean {
    return documentos[entregaId]?.signatarios.some((s) => s.metodoAutenticacao === metodo) ?? false;
  }

  const totalAssinadosEntregador = itens.filter((i) => jaAssinouComo(i.entregaId, MetodoAutenticacaoAssinatura.SessaoLogada)).length;
  const totalAssinadosBiometria = itens.filter((i) => jaAssinouComo(i.entregaId, MetodoAutenticacaoAssinatura.Biometria)).length;

  async function assinarLoteComoEntregador() {
    try {
      setAssinandoEntregador(true);
      setErro(null);
      for (const item of itens) {
        const doc = documentos[item.entregaId];
        if (!doc || jaAssinouComo(item.entregaId, MetodoAutenticacaoAssinatura.SessaoLogada)) continue;
        await api.assinatura.assinarComSessao(doc.id);
      }
      await carregarDocumentos();
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao assinar o lote como entregador.'));
    } finally {
      setAssinandoEntregador(false);
    }
  }

  async function assinarLoteComBiometria() {
    try {
      setAssinandoBiometria(true);
      setErro(null);
      const dispositivo = await obterDispositivoLocal();
      const captura = await capturarDigitalLocal();
      for (const item of itens) {
        const doc = documentos[item.entregaId];
        if (!doc || jaAssinouComo(item.entregaId, MetodoAutenticacaoAssinatura.Biometria)) continue;
        await api.assinatura.autenticarBiometriaLocal(
          doc.id,
          dispositivo.dispositivoId,
          dispositivo.segredoDispositivo,
          captura.trabalhadorId,
          captura.score,
        );
      }
      await carregarDocumentos();
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha na autenticação via biometria local.'));
    } finally {
      setAssinandoBiometria(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura do lote de entrega de EPI</DialogTitle>
          <DialogContent>
            {erro && <Text className={estilos.erro}>{erro}</Text>}

            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Itens entregues a {trabalhadorNome}
              </Text>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {itens.map((item) => (
                  <div key={item.entregaId} style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                    <FotoCatalogoEpi catalogoEpiId={item.catalogoEpiId} temFoto={item.epiTemFoto} tamanho={48} />
                    <div style={{ flex: 1 }}>
                      <Text style={{ display: 'block' }}>
                        {item.catalogoEpiNome} (qtd. {item.quantidade})
                      </Text>
                    </div>
                    {jaAssinouComo(item.entregaId, MetodoAutenticacaoAssinatura.SessaoLogada) &&
                      jaAssinouComo(item.entregaId, MetodoAutenticacaoAssinatura.Biometria) && (
                        <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                      )}
                  </div>
                ))}
              </div>
              <Text size={200} style={{ display: 'block', marginTop: 8, color: tokens.colorNeutralForeground3 }}>
                Data de entrega: {dataEntrega.slice(0, 10).split('-').reverse().join('/')}
              </Text>
            </div>

            <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                Assinatura do entregador ({totalAssinadosEntregador}/{itens.length})
              </Text>
              <Button
                appearance="primary"
                icon={<PersonBoard24Regular />}
                onClick={assinarLoteComoEntregador}
                disabled={carregando || assinandoEntregador || totalAssinadosEntregador === itens.length}
              >
                Assinar lote como entregador
              </Button>
            </div>

            <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                Assinatura do receptor — digital ({totalAssinadosBiometria}/{itens.length})
              </Text>
              {agenteLocalDisponivel ? (
                <Button
                  appearance="primary"
                  size="large"
                  icon={<Fingerprint24Regular />}
                  onClick={assinarLoteComBiometria}
                  disabled={carregando || assinandoBiometria || totalAssinadosBiometria === itens.length}
                >
                  Autenticar com digital (lote inteiro)
                </Button>
              ) : (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Warning24Regular />
                  <Text>Leitor Futronic não encontrado nesta máquina.</Text>
                </div>
              )}
            </div>

            <div className={estilos.card}>
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
