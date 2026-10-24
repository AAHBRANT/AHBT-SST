import { useState } from 'react';
import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Field, Textarea } from '@fluentui/react-components';
import { FeedbackInline } from '@ui';
import { api } from '../../lib/api';

interface RevogarConsentimentoBiometriaDialogProps {
  trabalhadorId: string;
  aberto: boolean;
  aoFechar: () => void;
  aoRevogar: () => void;
}

// LGPD (12/10): o trabalhador pode retirar o consentimento de biometria a qualquer momento. O motivo vai
// para a trilha de auditoria. A digital e o facial dele deixam de funcionar; as assinaturas já feitas valem.
export function RevogarConsentimentoBiometriaDialog({ trabalhadorId, aberto, aoFechar, aoRevogar }: RevogarConsentimentoBiometriaDialogProps) {
  const [motivo, setMotivo] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function revogar() {
    try {
      setEnviando(true);
      setErro(null);
      await api.trabalhadores.revogarConsentimentoBiometria(trabalhadorId, motivo.trim());
      setMotivo('');
      aoRevogar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível revogar o consentimento.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Dialog open={aberto} onOpenChange={(_, d) => { if (!d.open && !enviando) aoFechar(); }}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Revogar consentimento de biometria</DialogTitle>
          <DialogContent style={{ display: 'grid', gap: 12 }}>
            <span>
              O trabalhador deixa de assinar por digital e por reconhecimento facial: o cadastro facial é removido e
              as digitais e fotos de cadastro são arquivadas. As assinaturas já feitas continuam válidas. Para voltar a
              usar biometria, será preciso novo consentimento e novo cadastro.
            </span>
            {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
            <Field label="Motivo" required hint="Fica registrado na trilha de auditoria.">
              <Textarea value={motivo} onChange={(_, d) => setMotivo(d.value)} maxLength={500} placeholder="Ex.: pedido do próprio trabalhador em 12/10." />
            </Field>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" disabled={enviando} onClick={aoFechar}>Cancelar</Button>
            <Button appearance="primary" disabled={enviando || motivo.trim().length < 5} onClick={() => void revogar()}>
              {enviando ? 'Revogando…' : 'Revogar consentimento'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
