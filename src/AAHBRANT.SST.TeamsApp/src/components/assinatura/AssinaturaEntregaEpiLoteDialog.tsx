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
import { capturarDigitalLocal, estaAgenteLocalDisponivel, obterDispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { MutacaoEnfileiradaOfflineError } from '../../lib/offline/syncEngine';
import { usePageStyles } from '../../pages/pageStyles';
import { FotoCatalogoEpi } from '../../pages/epi/FotoCatalogoEpi';
import { SeletorFotoCamera } from '../SeletorFotoCamera';
import { clausulasTermoCompromisso } from './termoCompromissoEpi';

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

export interface ItemLoteAssinaturaEpi {
  entregaId: string;
  catalogoEpiId: string;
  epiNome: string;
  epiTemFoto: boolean;
  quantidade: number;
}

export interface AssinaturaEntregaEpiLoteDialogProps {
  open: boolean;
  onClose: () => void;
  itens: ItemLoteAssinaturaEpi[];
  obraId: string;
  trabalhadorNome: string;
  dataEntrega: string;
  numeroListaPresencaNr6?: string | null;
  dataTreinamentoNr6?: string | null;
}

// Variante em lote de AssinaturaEntregaEpiDialog.tsx — mesmo fluxo (entregador em sessão logada,
// receptor via digital/facial), mas cobrindo N entregas do carrinho de uma vez (ver
// CarrinhoEntregaEpi.tsx/EntregasTab.tsx). Decisão de escopo (confirmada com o usuário): sem mudar
// o banco — o Motor de Assinatura Eletrônica continua criando um DocumentoAssinatura por
// EntidadeTipo="EntregaEpi"/EntidadeId=entregaId, exatamente como antes (ExportarFichaEpiTrabalhadorQuery
// e o resto do motor não precisam mudar nada). A única diferença é que a MESMA interação física do
// trabalhador — um toque no leitor Futronic, ou uma foto facial — é propagada para todos os
// documentos do lote em sequência, então ele assina uma vez só na tela, mesmo que isso grave N
// DocumentoSignatario por trás.
export function AssinaturaEntregaEpiLoteDialog({
  open,
  onClose,
  itens,
  obraId,
  trabalhadorNome,
  dataEntrega,
  numeroListaPresencaNr6,
  dataTreinamentoNr6,
}: AssinaturaEntregaEpiLoteDialogProps) {
  const estilos = usePageStyles();
  const [documentos, setDocumentos] = useState<Record<string, DocumentoAssinatura>>({});
  const [carregandoDocs, setCarregandoDocs] = useState(false);
  const [assinandoEntregador, setAssinandoEntregador] = useState(false);
  const [erroEntregador, setErroEntregador] = useState<string | null>(null);
  const [processandoReceptor, setProcessandoReceptor] = useState(false);
  const [erroReceptor, setErroReceptor] = useState<string | null>(null);
  const [ultimoAssinante, setUltimoAssinante] = useState<string | null>(null);
  const [agenteLocalDisponivel, setAgenteLocalDisponivel] = useState(false);
  const [dispositivoLocal, setDispositivoLocal] = useState<{ dispositivoId: string; segredoDispositivo: string } | null>(null);
  const [pendenteFacial, setPendenteFacial] = useState(false);

  useEffect(() => {
    estaAgenteLocalDisponivel().then(async (disponivel) => {
      setAgenteLocalDisponivel(disponivel);
      if (disponivel) setDispositivoLocal(await obterDispositivoLocal());
    });
  }, []);

  // Um DocumentoAssinatura por entregaId (idempotente — CriarDocumentoAssinaturaCommand devolve o
  // mesmo id se a tela for reaberta), carregados em sequência para não disparar N requisições
  // simultâneas à toa.
  async function carregarDocumentos() {
    try {
      setCarregandoDocs(true);
      setErroEntregador(null);
      setErroReceptor(null);
      const mapa: Record<string, DocumentoAssinatura> = {};
      for (const item of itens) {
        await api.assinatura.criar('EntregaEpi', item.entregaId);
        const doc = await api.assinatura.obter('EntregaEpi', item.entregaId);
        if (doc) mapa[item.entregaId] = doc;
      }
      setDocumentos(mapa);
    } catch (e) {
      setErroEntregador(extrairMensagemErro(e, 'Falha ao preparar a assinatura.'));
    } finally {
      setCarregandoDocs(false);
    }
  }

  useEffect(() => {
    if (!open) return;
    setDocumentos({});
    setErroEntregador(null);
    setErroReceptor(null);
    setUltimoAssinante(null);
    setPendenteFacial(false);
    carregarDocumentos();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, itens.map((i) => i.entregaId).join(',')]);

  function assinouComo(entregaId: string, metodo: number): boolean {
    return documentos[entregaId]?.signatarios.some((s) => s.metodoAutenticacao === metodo) ?? false;
  }

  // Receptor pode assinar por digital (Biometria) OU reconhecimento facial — qualquer método que
  // não seja SessaoLogada (exclusivo do entregador) conta como "o receptor já assinou".
  function receptorAssinou(entregaId: string): boolean {
    return documentos[entregaId]?.signatarios.some((s) => s.metodoAutenticacao !== MetodoAutenticacaoAssinatura.SessaoLogada) ?? false;
  }

  const entregadorAssinouTodos = itens.length > 0 && itens.every((i) => assinouComo(i.entregaId, MetodoAutenticacaoAssinatura.SessaoLogada));
  const receptorAssinouTodos = itens.length > 0 && itens.every((i) => receptorAssinou(i.entregaId));

  async function assinarComoEntregador() {
    try {
      setAssinandoEntregador(true);
      setErroEntregador(null);
      for (const item of itens) {
        const doc = documentos[item.entregaId];
        if (!doc || assinouComo(item.entregaId, MetodoAutenticacaoAssinatura.SessaoLogada)) continue;
        await api.assinatura.assinarComSessao(doc.id);
      }
      await carregarDocumentos();
    } catch (e) {
      setErroEntregador(extrairMensagemErro(e, 'Falha ao assinar como entregador.'));
    } finally {
      setAssinandoEntregador(false);
    }
  }

  // Uma única captura de digital, propagada para o documento de cada item do lote que ainda não
  // tem a assinatura do receptor — o trabalhador toca o leitor uma vez, não uma vez por EPI.
  async function assinarComBiometriaLocal() {
    if (!dispositivoLocal) return;
    try {
      setProcessandoReceptor(true);
      setErroReceptor(null);
      setUltimoAssinante(null);
      const captura = await capturarDigitalLocal();
      let nome: string | null = null;
      for (const item of itens) {
        const doc = documentos[item.entregaId];
        if (!doc || receptorAssinou(item.entregaId)) continue;
        const signatario = await api.assinatura.autenticarBiometriaLocal(
          doc.id,
          dispositivoLocal.dispositivoId,
          dispositivoLocal.segredoDispositivo,
          captura.trabalhadorId,
          captura.score,
        );
        nome = signatario.trabalhadorNome;
      }
      if (nome) setUltimoAssinante(nome);
      await carregarDocumentos();
    } catch (e) {
      setErroReceptor(extrairMensagemErro(e, 'Falha na autenticação via biometria local.'));
    } finally {
      setProcessandoReceptor(false);
    }
  }

  // Mesma ideia da digital: uma foto facial só, propagada para todo item do lote ainda sem
  // assinatura do receptor. Se a conexão cair no meio do lote, os itens já autenticados continuam
  // assinados — só avisa que o restante ficou pendente de sincronização.
  async function assinarComFacial(arquivo: File) {
    try {
      setErroReceptor(null);
      setPendenteFacial(false);
      setUltimoAssinante(null);
      let nome: string | null = null;
      for (const item of itens) {
        const doc = documentos[item.entregaId];
        if (!doc || receptorAssinou(item.entregaId)) continue;
        const signatario = await api.assinatura.autenticarFacial(doc.id, obraId, arquivo);
        nome = signatario.trabalhadorNome;
      }
      if (nome) setUltimoAssinante(nome);
      await carregarDocumentos();
    } catch (e) {
      if (e instanceof MutacaoEnfileiradaOfflineError) {
        setPendenteFacial(true);
        return;
      }
      setErroReceptor(extrairMensagemErro(e, 'Falha na autenticação facial.'));
    }
  }

  const totalSignatarios = Object.values(documentos).reduce((soma, d) => soma + d.signatarios.length, 0);

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura da entrega de EPI</DialogTitle>
          <DialogContent>
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Itens entregues ({itens.length})
              </Text>
              <Text style={{ display: 'block', marginBottom: 12 }}>
                Funcionário: {trabalhadorNome} · Data de entrega: {dataEntrega.slice(0, 10).split('-').reverse().join('/')}
              </Text>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                {itens.map((item) => (
                  <div key={item.entregaId} style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                    <FotoCatalogoEpi catalogoEpiId={item.catalogoEpiId} temFoto={item.epiTemFoto} tamanho={48} />
                    <div>
                      <Text weight="semibold" style={{ display: 'block' }}>{item.epiNome}</Text>
                      <Text size={200}>Quantidade: {item.quantidade}</Text>
                    </div>
                  </div>
                ))}
              </div>
              <Text size={200} style={{ display: 'block', marginTop: 8 }}>
                O horário de cada assinatura fica registrado abaixo, na tabela de assinaturas registradas.
              </Text>
            </div>

            <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                Assinatura do entregador
              </Text>
              {erroEntregador && <Text className={estilos.erro}>{erroEntregador}</Text>}
              {entregadorAssinouTodos ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                  <Text>Assinado.</Text>
                </div>
              ) : (
                <Button
                  appearance="primary"
                  icon={<PersonBoard24Regular />}
                  onClick={assinarComoEntregador}
                  disabled={assinandoEntregador || carregandoDocs || Object.keys(documentos).length === 0}
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
            {erroReceptor && <Text className={estilos.erro}>{erroReceptor}</Text>}

            {receptorAssinouTodos ? (
              <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                  <Text weight="semibold">Todos os itens assinados pelo receptor.</Text>
                </div>
              </div>
            ) : agenteLocalDisponivel && dispositivoLocal ? (
              <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
                <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                  Digital (leitor local — Futronic FS80H)
                </Text>
                <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
                  Um único toque no leitor assina todos os itens do carrinho ainda pendentes.
                </Text>
                <Button
                  appearance="primary"
                  size="large"
                  icon={<Fingerprint24Regular />}
                  onClick={assinarComBiometriaLocal}
                  disabled={processandoReceptor || carregandoDocs || Object.keys(documentos).length === 0}
                >
                  Autenticar com digital
                </Button>
                {ultimoAssinante && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 12 }}>
                    <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                    <Text>Assinatura registrada: {ultimoAssinante}</Text>
                  </div>
                )}
              </div>
            ) : (
              <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Warning24Regular />
                  <Text weight="semibold">Leitor Futronic não encontrado nesta máquina</Text>
                </div>
                <Text style={{ display: 'block', marginTop: 8 }}>
                  Verifique se o leitor está conectado e se o Agente Biométrico está em execução, depois
                  recarregue esta página — ou use o reconhecimento facial abaixo.
                </Text>
              </div>
            )}

            {!receptorAssinouTodos && (
              <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
                <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                  Reconhecimento Facial (Azure)
                </Text>
                <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
                  Uma única foto assina todos os itens do carrinho ainda pendentes.
                </Text>
                {pendenteFacial && (
                  <Text style={{ display: 'block', marginBottom: 8 }}>
                    Sem internet — a foto foi salva neste dispositivo e será verificada assim que a conexão voltar.
                  </Text>
                )}
                <SeletorFotoCamera
                  aoSelecionarArquivo={assinarComFacial}
                  rotulo="Assinar com reconhecimento facial"
                  desabilitado={carregandoDocs || Object.keys(documentos).length === 0}
                  modoCamera="user"
                />
              </div>
            )}

            <div className={estilos.card}>
              <div className={estilos.toolbar}>
                <Text weight="semibold">Assinaturas registradas ({totalSignatarios})</Text>
              </div>
              <Text size={200} style={{ display: 'block', marginTop: 4 }}>
                {entregadorAssinouTodos && receptorAssinouTodos
                  ? 'Entregador e receptor assinaram todos os itens deste lote.'
                  : 'Cada item do carrinho vira uma assinatura própria — acompanhe pelos indicadores acima até os dois lados ficarem completos.'}
              </Text>
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
