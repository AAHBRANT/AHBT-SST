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
import { Checkmark24Filled } from '@fluentui/react-icons';
import {
  api,
  MetodoAutenticacaoAssinatura,
  metodoAutenticacaoAssinaturaLabel,
  type DocumentoAssinatura,
  type OrdemServicoEmitida,
} from '../../lib/api';
import { credencialLeitura } from '../../lib/agenteBiometricoLocal';
import { usePageStyles } from '../../pages/pageStyles';
import { useAgenteBiometricoLocal } from '../treinamentos/useAgenteBiometricoLocal';
import { AssinaturaBiometricaBotoes } from './AssinaturaBiometricaBotoes';
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
  /**
   * Dentro de uma integração (Realizar integração, etapa da OS): nome do instrutor da integração, que é quem
   * assina a vez 2. Sem isto (Pessoas > aba OS), a vez 2 é do técnico ou engenheiro de segurança da obra.
   */
  instrutorNome?: string | null;
}

// Assinatura da Ordem de Serviço (03/11/2026), na ordem aprovada: primeiro o funcionário, por digital ou
// facial (AssinaturaQuiosque); depois o técnico, também por digital ou facial (16/11/2026: a assinatura pelo
// login saiu e o servidor a recusa para OS nova). Dentro da integração quem assina é o instrutor dela; fora,
// o técnico/engenheiro de segurança da obra. A OS só fica válida com as duas — o servidor recusa a do técnico
// antes da do funcionário e fecha o documento depois dela. Mesmo padrão de AssinaturaCertificadoTreinamentoDialog.tsx.
export function AssinaturaOrdemServicoDialog({ open, onClose, ordemServico, trabalhadorId, trabalhadorNome, instrutorNome }: AssinaturaOrdemServicoDialogProps) {
  const estilos = usePageStyles();
  const { agenteDisponivel, dispositivoLocal } = useAgenteBiometricoLocal();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
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
  // Técnico: qualquer outra pessoa que assinou (biometria, desde 16/11) ou a assinatura antiga por login (OS de antes continuam válidas).
  const assinaturaTecnico = documento?.signatarios.find(
    (s) => s.trabalhadorId !== trabalhadorId || s.metodoAutenticacao === MetodoAutenticacaoAssinatura.SessaoLogada
  );
  const tecnicoAssinou = !!assinaturaTecnico;
  const dentroDaIntegracao = !!instrutorNome;
  const tituloVez2 = dentroDaIntegracao ? 'Instrutor da integração' : 'Técnico ou engenheiro de segurança da obra';

  useEffect(() => {
    if (!open) return;
    setDocumento(null);
    setErro(null);
    void carregarDocumento();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, ordemServico.id]);

  // A assinatura do funcionário acontece dentro do quiosque; consulta de novo até ela chegar para
  // liberar a vez do técnico.
  useEffect(() => {
    if (!open || funcionarioAssinou) return;
    const id = window.setInterval(() => void carregarDocumento(), 3000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, funcionarioAssinou, ordemServico.id]);

  const assinado = (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
      <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
      <Text>
        Assinado{assinaturaTecnico ? ` — ${assinaturaTecnico.trabalhadorNome}, ${metodoAutenticacaoAssinaturaLabel[assinaturaTecnico.metodoAutenticacao] ?? ''}` : '.'}
      </Text>
    </div>
  );

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>Assinatura da Ordem de Serviço {ordemServico.numero}</DialogTitle>
          <DialogContent>
            <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
              A OS só fica válida com as duas assinaturas: primeiro o funcionário; depois{' '}
              {dentroDaIntegracao ? 'o instrutor da integração' : 'o técnico ou engenheiro de segurança da obra'}. Ambos por digital ou facial.
            </Text>
            {erro && <Text className={estilos.erro} style={{ display: 'block', marginBottom: 8 }}>{erro}</Text>}

            <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>1. Funcionário — {trabalhadorNome}</Text>
            {funcionarioAssinou
              ? <div className={estilos.card} style={{ marginBottom: 16 }}>{assinado}</div>
              : <div style={{ marginBottom: 16 }}><AssinaturaQuiosque entidadeTipo="OrdemServico" entidadeId={ordemServico.id} obraId={ordemServico.obraId} /></div>}

            <div className={estilos.card} style={{ maxWidth: 480 }}>
              <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>2. {tituloVez2}</Text>
              {dentroDaIntegracao && <Text style={{ display: 'block', marginBottom: 12 }}>{instrutorNome}</Text>}
              {tecnicoAssinou ? (
                <div style={{ marginTop: dentroDaIntegracao ? 0 : 8 }}>{assinado}</div>
              ) : (
                <div style={{ marginTop: dentroDaIntegracao ? 0 : 8 }}>
                  <AssinaturaBiometricaBotoes
                    quem="os"
                    agenteDisponivel={agenteDisponivel}
                    dispositivoLocal={dispositivoLocal}
                    desabilitado={!documento || !funcionarioAssinou}
                    aoJaAssinou={() => void carregarDocumento()}
                    aoDigital={async (captura, leitor) => {
                      if (!documento) return;
                      await api.assinatura.assinarTecnicoOsDigital(
                        documento.id,
                        credencialLeitura(leitor, captura.leituras[0]),
                        captura.trabalhadorId,
                        captura.score,
                        captura.imagemPng,
                      );
                      await carregarDocumento();
                    }}
                    aoFacial={async (foto) => {
                      if (!documento) return;
                      await api.assinatura.assinarTecnicoOsFacial(documento.id, foto);
                      await carregarDocumento();
                    }}
                  />
                  {!funcionarioAssinou && (
                    <Text size={200} style={{ display: 'block', marginTop: 6 }}>Liberado depois da assinatura do funcionário.</Text>
                  )}
                </div>
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
