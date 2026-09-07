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
import { FotoCatalogoEpc } from '../../pages/epc/FotoCatalogoEpc';

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

export interface AssinaturaEntregaEpcDialogProps {
  open: boolean;
  onClose: () => void;
  entregaId: string;
  trabalhadorNome: string;
  itemNome: string;
  catalogoEpcId: string;
  itemTemFoto: boolean;
  quantidade: number;
  dataEntrega: string;
}

// Popup de assinatura disparado logo após "Registrar entrega" em EntregaEpcTab.tsx — mesma
// estrutura de AssinaturaEntregaUniformeDialog.tsx/AssinaturaEntregaEpiDialog.tsx (assinatura do
// entregador em 1 clique via sessão logada; assinatura do receptor via AssinaturaQuiosque).
//
// Termo de Recebimento reaproveita o mesmo modelo institucional do EPI/Uniforme (decisão do
// usuário, 2026-09-07: "os 3 devem ser bem parecidos") — mesma redação, trocando só "EPI(s)"/
// "uniforme" por "item(ns) de EPC". Mesma omissão das cláusulas de CA e NR-6 já feita para
// uniforme (EPC também não tem CA nem exige treinamento NR-6). Por ter alterado a redação
// institucional, recomenda-se uma checagem rápida do time jurídico/QSMS antes do uso real em
// produção.
function clausulasTermoRecebimento(): string[] {
  return [
    'Declaro ter recebido do Consórcio Ponte Rio Cuiá o(s) item(ns) de EPC relacionado(s) nesta ficha, nas datas e quantidades ali indicadas, todos em perfeitas condições de uso.',
    'Comprometo-me a utilizar o(s) item(ns) de EPC exclusivamente para a finalidade a que se destina(m), durante toda a execução das minhas atividades laborais, zelando por sua guarda, conservação e higienização adequadas, e a comunicar imediatamente ao Setor de Segurança do Trabalho qualquer dano, extravio ou alteração que o(s) torne impróprio(s) para uso.',
    'Comprometo-me a devolver o(s) item(ns) de EPC sempre que solicitado, inclusive nos casos de substituição, troca de função, mudança de atividade ou rescisão do meu contrato de trabalho.',
    'Estou ciente de que o descumprimento das obrigações aqui assumidas constitui falta funcional, passível de sanções disciplinares que poderão variar, a critério do empregador, de advertência por escrito até a rescisão contratual por justa causa, sem prejuízo de demais medidas legais cabíveis, conforme disposto no Art. 158 da CLT.',
  ];
}

export function AssinaturaEntregaEpcDialog({
  open,
  onClose,
  entregaId,
  trabalhadorNome,
  itemNome,
  catalogoEpcId,
  itemTemFoto,
  quantidade,
  dataEntrega,
}: AssinaturaEntregaEpcDialogProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [assinandoEntregador, setAssinandoEntregador] = useState(false);
  const [erroEntregador, setErroEntregador] = useState<string | null>(null);

  async function carregarDocumento() {
    try {
      await api.assinatura.criar('EntregaEpc', entregaId);
      const doc = await api.assinatura.obter('EntregaEpc', entregaId);
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
          <DialogTitle>Assinatura da entrega de EPC</DialogTitle>
          <DialogContent>
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                Item entregue
              </Text>
              <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
                <FotoCatalogoEpc catalogoEpcId={catalogoEpcId} temFoto={itemTemFoto} tamanho={96} />
                <div>
                  <Text style={{ display: 'block' }}>Funcionário: {trabalhadorNome}</Text>
                  <Text style={{ display: 'block' }}>Item: {itemNome}</Text>
                  <Text style={{ display: 'block' }}>Quantidade: {quantidade}</Text>
                  <Text style={{ display: 'block' }}>
                    Data de entrega: {dataEntrega.slice(0, 10).split('-').reverse().join('/')}
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
                {clausulasTermoRecebimento().map((clausula, indice) => (
                  <li key={indice} style={{ marginBottom: 6 }}>
                    <Text size={200}>{clausula}</Text>
                  </li>
                ))}
              </ol>
            </div>

            <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
              Assinatura do receptor
            </Text>
            <AssinaturaQuiosque entidadeTipo="EntregaEpc" entidadeId={entregaId} />
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
