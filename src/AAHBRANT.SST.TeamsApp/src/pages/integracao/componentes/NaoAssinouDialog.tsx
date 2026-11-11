import { useState } from 'react';
import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, FeedbackInline, Field, Textarea } from '@ui';

interface Props {
  nome: string;
  /** Ex.: "NR-18". */
  codigoCurso: string;
  aoFechar: () => void;
  /** Recebe o motivo (já com trim). Deve lançar erro com a mensagem do servidor se falhar. */
  aoConfirmar: (motivo: string) => Promise<void>;
}

export const MOTIVO_MINIMO = 5;

// "Não assinou" (decisão do usuário, 11/11): a pessoa não assinou o curso, então fica sem o certificado
// daquele curso e fora da lista de presença da etapa. O motivo é obrigatório e fica registrado.
export function NaoAssinouDialog({ nome, codigoCurso, aoFechar, aoConfirmar }: Props) {
  const [motivo, setMotivo] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const valido = motivo.trim().length >= MOTIVO_MINIMO;

  async function confirmar() {
    if (!valido || salvando) return;
    try {
      setSalvando(true);
      setErro(null);
      await aoConfirmar(motivo.trim());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível registrar. Tente de novo.');
      setSalvando(false);
    }
  }

  return (
    <Dialog open onOpenChange={(_, d) => !d.open && !salvando && aoFechar()}>
      <DialogSurface style={{ maxWidth: 520 }}>
        <DialogBody>
          <DialogTitle>Não assinou — {nome}</DialogTitle>
          <DialogContent>
            <FeedbackInline tom="aviso">
              Esta pessoa não receberá o certificado de {codigoCurso} e não entrará na lista de presença.
            </FeedbackInline>
            {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
            <Field
              label="Motivo (obrigatório)"
              validationState={motivo.length > 0 && !valido ? 'error' : 'none'}
              validationMessage={motivo.length > 0 && !valido ? `Descreva o motivo com pelo menos ${MOTIVO_MINIMO} caracteres.` : undefined}
            >
              <Textarea value={motivo} onChange={(_, d) => setMotivo(d.value)} rows={3} placeholder="Ex.: saiu da obra antes do fim do curso" />
            </Field>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={aoFechar} disabled={salvando}>Voltar</Button>
            <Button appearance="primary" onClick={() => void confirmar()} disabled={!valido || salvando}>
              {salvando ? 'Registrando…' : 'Confirmar: não assinou'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
