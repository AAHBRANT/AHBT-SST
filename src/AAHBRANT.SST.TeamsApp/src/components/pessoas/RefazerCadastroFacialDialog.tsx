import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Textarea,
  Text,
} from '@fluentui/react-components';
import { FeedbackInline } from '@ui';

interface RefazerCadastroFacialDialogProps {
  aberto: boolean;
  nomeTrabalhador?: string;
  // Devolve o erro (texto) se o backend recusar; o diálogo só fecha quando dá certo.
  aoConfirmar: (motivo: string) => Promise<void>;
  aoCancelar: () => void;
}

const MOTIVO_MINIMO = 5;

// Ação destrutiva e de dado biométrico: exige motivo (vai para a trilha de auditoria) e avisa o que
// será apagado. Quem chama só mostra isto para Administrador; o backend confere de novo.
export function RefazerCadastroFacialDialog({ aberto, nomeTrabalhador, aoConfirmar, aoCancelar }: RefazerCadastroFacialDialogProps) {
  const [motivo, setMotivo] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  function fechar() {
    if (enviando) return;
    setMotivo('');
    setErro(null);
    aoCancelar();
  }

  async function confirmar() {
    setEnviando(true);
    setErro(null);
    try {
      await aoConfirmar(motivo.trim());
      setMotivo('');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível refazer o cadastro facial.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Dialog open={aberto} onOpenChange={(_, d) => !d.open && fechar()}>
      <DialogSurface style={{ maxWidth: 480 }}>
        <DialogBody>
          <DialogTitle>Refazer o cadastro facial{nomeTrabalhador ? ` de ${nomeTrabalhador}` : ''}?</DialogTitle>
          <DialogContent>
            <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 12, overflowWrap: 'anywhere' }}>
              <FeedbackInline tom="aviso">
                A foto atual e o cadastro no Azure Face serão apagados. Até a nova captura, o trabalhador não
                poderá assinar por reconhecimento facial.
              </FeedbackInline>
              <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 4 }}>
                <label htmlFor="motivo-refazer-facial">
                  <Text weight="semibold">Motivo (obrigatório)</Text>
                </label>
                <Textarea
                  id="motivo-refazer-facial"
                  value={motivo}
                  maxLength={500}
                  style={{ width: '100%' }}
                  placeholder="Ex.: foto de cadastro desfocada, baixa confiança no reconhecimento"
                  onChange={(_, d) => setMotivo(d.value)}
                />
              </div>
              {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={fechar} disabled={enviando}>
              Cancelar
            </Button>
            <Button appearance="primary" onClick={confirmar} disabled={enviando || motivo.trim().length < MOTIVO_MINIMO}>
              {enviando ? 'Apagando…' : 'Apagar e refazer'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
