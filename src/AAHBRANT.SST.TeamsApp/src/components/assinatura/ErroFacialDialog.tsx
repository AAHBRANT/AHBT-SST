import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Text,
} from '@fluentui/react-components';

interface ErroFacialDialogProps {
  mensagem: string | null;
  aoFechar: () => void;
}

// Erros de captura facial precisam interromper o fluxo e ficar visíveis para o operador ajustar
// iluminação, distância ou enquadramento. O texto original do backend é preservado para orientar
// exatamente a correção necessária.
export function ErroFacialDialog({ mensagem, aoFechar }: ErroFacialDialogProps) {
  return (
    <Dialog open={Boolean(mensagem)} onOpenChange={(_, data) => !data.open && aoFechar()}>
      <DialogSurface style={{ maxWidth: 480 }}>
        <DialogBody>
          <DialogTitle>Erro</DialogTitle>
          <DialogContent>
            <Text>Erro: {mensagem}</Text>
          </DialogContent>
          <DialogActions>
            <Button appearance="primary" onClick={aoFechar}>
              Fechar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
