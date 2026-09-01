import { useCallback } from 'react';
import { Toast, ToastTitle, useToastController } from '@fluentui/react-components';
import { ID_TOASTER_GLOBAL } from '../lib/toaster';

// Antes desta mudança (31/08), nenhuma tela do sistema confirmava visualmente que salvar/excluir
// tinha funcionado — a única pista era a lista recarregar em silêncio. O <Toaster> global fica
// montado uma vez em layout/AppShell.tsx; este hook só dispara notificações nele.
export function useSucessoToast() {
  const { dispatchToast } = useToastController(ID_TOASTER_GLOBAL);

  return useCallback(
    (mensagem: string) => {
      dispatchToast(
        <Toast>
          <ToastTitle>{mensagem}</ToastTitle>
        </Toast>,
        { intent: 'success', timeout: 3000 },
      );
    },
    [dispatchToast],
  );
}
