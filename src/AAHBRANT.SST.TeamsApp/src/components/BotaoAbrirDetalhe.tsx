import { Open24Regular } from '@fluentui/react-icons';
import { BotaoAcao } from '@ui';

// Listas de documentos (pedido do usuário, 18/11/2026): documento finalizado abre o PDF ao clicar na
// linha — não há por que voltar à tela de preencher. A tela de detalhe/edição continua a um clique por
// este botão, que fica na coluna de ações de cada linha (antes do Excluir, quando houver).
export function BotaoAbrirDetalhe({ aoAbrir, rotulo = 'Abrir' }: { aoAbrir: () => void; rotulo?: string }) {
  return (
    <BotaoAcao
      tom="ver"
      icon={<Open24Regular />}
      aria-label={rotulo}
      onClick={(evento) => {
        evento.stopPropagation();
        aoAbrir();
      }}
    >
      Abrir
    </BotaoAcao>
  );
}
