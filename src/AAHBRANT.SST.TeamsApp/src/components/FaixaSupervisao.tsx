import { FeedbackInline } from '@ui';

// Responsável x Supervisão (06/11/2026): aparece quando o usuário abre uma obra que ele só
// acompanha. Explica em uma frase por que os botões de criar/editar não estão na tela.
export function FaixaSupervisao({ obraNome }: { obraNome?: string | null }) {
  return (
    <FeedbackInline tom="aviso">
      Você está <strong>acompanhando</strong> {obraNome ? <>a obra <strong>{obraNome}</strong></> : 'esta obra'}. Pode consultar e
      baixar os documentos. As alterações são feitas pelo técnico responsável.
    </FeedbackInline>
  );
}
