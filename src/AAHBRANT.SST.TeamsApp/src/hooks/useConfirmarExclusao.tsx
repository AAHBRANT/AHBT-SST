import { useCallback, useRef, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
} from '@fluentui/react-components';

interface OpcoesConfirmacao {
  titulo?: string;
  mensagem: string;
  rotuloConfirmar?: string;
}

// Hook compartilhado pra todo fluxo de exclusão do sistema (pedido do usuário, 31/08: nenhuma das
// 39 telas com botão de excluir tinha confirmação — um clique acidental apagava na hora, inclusive
// em cadastro de trabalhador e PGR). Uso: chamar `await confirmar('mensagem')` dentro da função de
// exclusão, antes de bater na API; se o usuário cancelar, a promise resolve `false` e a função
// retorna sem fazer nada. Renderizar `{dialogElement}` uma vez em qualquer lugar do JSX da tela.
export function useConfirmarExclusao() {
  const [aberto, setAberto] = useState(false);
  const [opcoes, setOpcoes] = useState<OpcoesConfirmacao>({ mensagem: '' });
  const resolverRef = useRef<((valor: boolean) => void) | null>(null);

  const confirmar = useCallback((opcoesOuMensagem: OpcoesConfirmacao | string) => {
    const opcoesFinal = typeof opcoesOuMensagem === 'string' ? { mensagem: opcoesOuMensagem } : opcoesOuMensagem;
    setOpcoes(opcoesFinal);
    setAberto(true);
    return new Promise<boolean>((resolve) => {
      resolverRef.current = resolve;
    });
  }, []);

  function responder(valor: boolean) {
    setAberto(false);
    resolverRef.current?.(valor);
    resolverRef.current = null;
  }

  const dialogElement = (
    <Dialog
      open={aberto}
      onOpenChange={(_, dados) => {
        if (!dados.open) responder(false);
      }}
    >
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{opcoes.titulo ?? 'Confirmar exclusão'}</DialogTitle>
          <DialogContent>{opcoes.mensagem}</DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => responder(false)}>
              Cancelar
            </Button>
            <Button appearance="primary" onClick={() => responder(true)}>
              {opcoes.rotuloConfirmar ?? 'Excluir'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );

  return { confirmar, dialogElement };
}
