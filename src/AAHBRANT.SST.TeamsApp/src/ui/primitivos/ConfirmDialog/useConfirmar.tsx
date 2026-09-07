import { useCallback, useRef, useState } from 'react';
import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  // Destrutivo é ação, não estado — única exceção declarada à regra "status nunca sólido" (spec §3).
  destrutivo: { backgroundColor: tokensUi.status.alerta.tinta, color: '#ffffff', ':hover': { backgroundColor: tokensUi.status.alerta.tinta, color: '#ffffff', filter: 'brightness(0.92)' } },
});

export interface OpcoesConfirmacao {
  titulo?: string;
  mensagem: string;
  rotuloConfirmar?: string;
  tom?: 'destrutivo' | 'neutro';
}

// Confirmação por promise (spec §3). Absorve useConfirmarExclusao (39 telas, pedido de 31/08) mantendo
// a mesma API — e corrige o botão Excluir, que usava appearance="primary" (vinho, cara de ação
// principal) para uma ação destrutiva.
export function useConfirmar() {
  const e = useStyles();
  const [aberto, setAberto] = useState(false);
  const [opcoes, setOpcoes] = useState<OpcoesConfirmacao>({ mensagem: '' });
  const resolverRef = useRef<((v: boolean) => void) | null>(null);

  const confirmar = useCallback((entrada: OpcoesConfirmacao | string) => {
    setOpcoes(typeof entrada === 'string' ? { mensagem: entrada, tom: 'destrutivo' } : { tom: 'destrutivo', ...entrada });
    setAberto(true);
    return new Promise<boolean>((resolve) => { resolverRef.current = resolve; });
  }, []);

  function responder(v: boolean) { setAberto(false); resolverRef.current?.(v); resolverRef.current = null; }

  const destrutivo = (opcoes.tom ?? 'destrutivo') === 'destrutivo';
  const dialogElement = (
    <Dialog open={aberto} onOpenChange={(_, d) => { if (!d.open) responder(false); }}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{opcoes.titulo ?? (destrutivo ? 'Confirmar exclusão' : 'Confirmar')}</DialogTitle>
          <DialogContent>{opcoes.mensagem}</DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => responder(false)}>Cancelar</Button>
            <Button appearance={destrutivo ? 'secondary' : 'primary'} className={destrutivo ? e.destrutivo : undefined} onClick={() => responder(true)}>
              {opcoes.rotuloConfirmar ?? (destrutivo ? 'Excluir' : 'Confirmar')}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );

  return { confirmar, dialogElement };
}
