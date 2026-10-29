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
import { Checkmark24Filled, PersonBoard24Regular, Warning24Regular } from '@fluentui/react-icons';
import { BotaoBiometriaDigital } from './BotaoBiometriaDigital';
import { api, MetodoAutenticacaoAssinatura, type DocumentoAssinatura } from '../../lib/api';
import { capturarDigitalLocal, credencialLeitura, estaAgenteLocalDisponivel, obterDispositivoLocal, type DispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { tocarBipeAssinaturaAceita } from '../../lib/bipeAssinatura';
import { MutacaoEnfileiradaOfflineError } from '../../lib/offline/syncEngine';
import { usePageStyles } from '../../pages/pageStyles';
import { FotoCatalogoUniforme } from '../../pages/uniforme/FotoCatalogoUniforme';
import { SeletorFotoCamera } from '../SeletorFotoCamera';
import { ErroFacialDialog } from './ErroFacialDialog';

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

// Termo de Recebimento e Compromisso de Uso do uniforme — redação idêntica à que já existia em
// AssinaturaEntregaUniformeDialog.tsx (derivada do termo oficial do EPI, decisão de 2026-09-07).
function clausulasTermoRecebimento(nomeObra?: string | null): string[] {
  return [
    `Declaro ter recebido do ${nomeObra || 'empregador'} a(s) peça(s) de uniforme relacionada(s) nesta ficha, nas datas e quantidades ali indicadas, todas em perfeitas condições de uso.`,
    'Comprometo-me a utilizar o(s) uniforme(s) exclusivamente para a finalidade a que se destina(m), durante toda a execução das minhas atividades laborais, zelando por sua guarda, conservação e higienização adequadas, e a comunicar imediatamente ao Setor de Segurança do Trabalho qualquer dano, extravio ou alteração que o(s) torne impróprio(s) para uso.',
    'Comprometo-me a devolver o(s) uniforme(s) sempre que solicitado, inclusive nos casos de substituição, troca de função, mudança de atividade ou rescisão do meu contrato de trabalho.',
    'Estou ciente de que o descumprimento das obrigações aqui assumidas constitui falta funcional, passível de sanções disciplinares que poderão variar, a critério do empregador, de advertência por escrito até a rescisão contratual por justa causa, sem prejuízo de demais medidas legais cabíveis, conforme disposto no Art. 158 da CLT.',
  ];
}

export interface ItemLoteAssinaturaUniforme {
  entregaId: string;
  catalogoUniformeId: string;
  pecaNome: string;
  pecaTemFoto: boolean;
  tamanho: string;
  quantidade: number;
}

export interface AssinaturaEntregaUniformeLoteDialogProps {
  open: boolean;
  onClose: () => void;
  itens: ItemLoteAssinaturaUniforme[];
  obraId: string;
  trabalhadorNome: string;
  dataEntrega: string;
}

// Variante em lote para o carrinho de uniforme, espelho de AssinaturaEntregaEpiLoteDialog.tsx: o
// motor continua criando um DocumentoAssinatura por EntidadeTipo="EntregaUniforme"/entregaId, mas a
// MESMA interação física do trabalhador (um toque no leitor Futronic ou uma foto facial) é
// propagada para os documentos do lote em sequência.
export function AssinaturaEntregaUniformeLoteDialog({
  open,
  onClose,
  itens,
  obraId,
  trabalhadorNome,
  dataEntrega,
}: AssinaturaEntregaUniformeLoteDialogProps) {
  const estilos = usePageStyles();
  const [documentos, setDocumentos] = useState<Record<string, DocumentoAssinatura>>({});
  const [nomeObra, setNomeObra] = useState<string | null>(null);
  const [carregandoDocs, setCarregandoDocs] = useState(false);
  const [assinandoEntregador, setAssinandoEntregador] = useState(false);
  const [erroEntregador, setErroEntregador] = useState<string | null>(null);
  const [processandoReceptor, setProcessandoReceptor] = useState(false);
  const [erroReceptor, setErroReceptor] = useState<string | null>(null);
  const [erroFacialReceptor, setErroFacialReceptor] = useState<string | null>(null);
  const [ultimoAssinante, setUltimoAssinante] = useState<string | null>(null);
  const [agenteLocalDisponivel, setAgenteLocalDisponivel] = useState(false);
  const [dispositivoLocal, setDispositivoLocal] = useState<DispositivoLocal | null>(null);
  const [pendenteFacial, setPendenteFacial] = useState(false);

  useEffect(() => {
    estaAgenteLocalDisponivel().then(async (disponivel) => {
      setAgenteLocalDisponivel(disponivel);
      if (disponivel) setDispositivoLocal(await obterDispositivoLocal());
    });
  }, []);

  async function carregarDocumentos() {
    try {
      setCarregandoDocs(true);
      setErroEntregador(null);
      setErroReceptor(null);
      const mapa: Record<string, DocumentoAssinatura> = {};
      for (const item of itens) {
        await api.assinatura.criar('EntregaUniforme', item.entregaId);
        const doc = await api.assinatura.obter('EntregaUniforme', item.entregaId);
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
    setErroFacialReceptor(null);
    setUltimoAssinante(null);
    setPendenteFacial(false);
    carregarDocumentos();
    api.obras.listar().then((obras) => setNomeObra(obras.find((o) => o.id === obraId)?.nome ?? null)).catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, itens.map((i) => i.entregaId).join(',')]);

  function assinouComo(entregaId: string, metodo: number): boolean {
    return documentos[entregaId]?.signatarios.some((s) => s.metodoAutenticacao === metodo) ?? false;
  }

  // Qualquer método diferente de SessaoLogada (exclusivo do entregador) conta como assinatura do receptor.
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

  async function assinarComBiometriaLocal() {
    if (!dispositivoLocal) return;
    try {
      setProcessandoReceptor(true);
      setErroReceptor(null);
      setUltimoAssinante(null);
      // Uma leitura só para o lote: o agente assina um desafio por documento ainda pendente.
      const pendentes = itens.flatMap((item) => {
        const doc = documentos[item.entregaId];
        return doc && !receptorAssinou(item.entregaId) ? [doc] : [];
      });
      const captura = await capturarDigitalLocal(dispositivoLocal.dispositivoId, pendentes.length);
      let nome: string | null = null;
      for (const [indice, doc] of pendentes.entries()) {
        const signatario = await api.assinatura.autenticarBiometriaLocal(
          doc.id,
          credencialLeitura(dispositivoLocal, captura.leituras[indice]),
          captura.trabalhadorId,
          captura.score,
          captura.imagemPng,
        );
        nome = signatario.trabalhadorNome;
      }
      if (nome) {
        setUltimoAssinante(nome);
        tocarBipeAssinaturaAceita();
      }
      await carregarDocumentos();
    } catch (e) {
      setErroReceptor(extrairMensagemErro(e, 'Falha na autenticação via biometria local.'));
    } finally {
      setProcessandoReceptor(false);
    }
  }

  async function assinarComFacial(arquivo: File) {
    try {
      setErroReceptor(null);
      setErroFacialReceptor(null);
      setPendenteFacial(false);
      setUltimoAssinante(null);
      let nome: string | null = null;
      for (const item of itens) {
        const doc = documentos[item.entregaId];
        if (!doc || receptorAssinou(item.entregaId)) continue;
        const signatario = await api.assinatura.autenticarFacial(doc.id, obraId, arquivo);
        nome = signatario.trabalhadorNome;
      }
      if (nome) {
        setUltimoAssinante(nome);
        tocarBipeAssinaturaAceita();
      }
      await carregarDocumentos();
    } catch (e) {
      if (e instanceof MutacaoEnfileiradaOfflineError) {
        setPendenteFacial(true);
        return;
      }
      setErroFacialReceptor(extrairMensagemErro(e, 'Falha na autenticação facial.'));
    }
  }

  const totalSignatarios = Object.values(documentos).reduce((soma, d) => soma + d.signatarios.length, 0);

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura da entrega de uniforme</DialogTitle>
          <DialogContent>
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Peças entregues ({itens.length})
              </Text>
              <Text style={{ display: 'block', marginBottom: 12 }}>
                Funcionário: {trabalhadorNome} · Data de entrega: {dataEntrega.slice(0, 10).split('-').reverse().join('/')}
              </Text>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                {itens.map((item) => (
                  <div key={item.entregaId} style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                    <FotoCatalogoUniforme catalogoUniformeId={item.catalogoUniformeId} temFoto={item.pecaTemFoto} tamanho={48} />
                    <div>
                      <Text weight="semibold" style={{ display: 'block' }}>{item.pecaNome}</Text>
                      <Text size={200}>Tamanho {item.tamanho} · Quantidade: {item.quantidade}</Text>
                    </div>
                  </div>
                ))}
              </div>
              <Text size={200} style={{ display: 'block', marginTop: 8 }}>
                O horário de cada assinatura fica registrado no documento de cada peça.
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
                {clausulasTermoRecebimento(nomeObra).map((clausula, indice) => (
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
                  <Text weight="semibold">Todas as peças assinadas pelo receptor.</Text>
                </div>
              </div>
            ) : agenteLocalDisponivel && dispositivoLocal ? (
              <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
                <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
                  Digital (leitor local — Futronic FS80H)
                </Text>
                <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
                  Um único toque no leitor assina todas as peças do carrinho ainda pendentes.
                </Text>
                <BotaoBiometriaDigital
                  onClick={assinarComBiometriaLocal}
                  disabled={processandoReceptor || carregandoDocs || Object.keys(documentos).length === 0}
                />
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
                  Facial Azure
                </Text>
                <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
                  Uma única foto assina todas as peças do carrinho ainda pendentes.
                </Text>
                {pendenteFacial && (
                  <Text style={{ display: 'block', marginBottom: 8 }}>
                    Sem internet — a foto foi salva neste dispositivo e será verificada assim que a conexão voltar.
                  </Text>
                )}
                <SeletorFotoCamera
                  aoSelecionarArquivo={assinarComFacial}
                  aoErroValidacao={setErroFacialReceptor}
                  rotulo="Facial Azure"
                  desabilitado={carregandoDocs || Object.keys(documentos).length === 0}
                  tamanho="medium"
                  variante="facialAzure"
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
                  ? 'Entregador e receptor assinaram todas as peças deste lote.'
                  : 'Cada peça do carrinho vira uma assinatura própria — acompanhe pelos indicadores acima até os dois lados ficarem completos.'}
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
      <ErroFacialDialog mensagem={erroFacialReceptor} aoFechar={() => setErroFacialReceptor(null)} />
    </Dialog>
  );
}
