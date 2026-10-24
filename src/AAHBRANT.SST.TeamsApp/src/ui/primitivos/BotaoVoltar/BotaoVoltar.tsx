import { Button } from '@fluentui/react-components';
import { ArrowLeft20Regular } from '@fluentui/react-icons';

export interface BotaoVoltarProps {
  /** Diga para onde leva ("Voltar para CIPA"), não só "Voltar". */
  rotulo?: string;
  onClick: () => void;
  className?: string;
}

// Voltar padrão do sistema (catálogo de botões, 21/10/2026): contorno vinho, ícone de 20 px e o nome
// da tela de destino. Substitui o antigo subtle/small cinza de 12 px, que sumia na tela — toda tela
// que volta (PageHeader, abas com drill-down, página de assinatura) usa este botão.
export function BotaoVoltar({ rotulo = 'Voltar', onClick, className }: BotaoVoltarProps) {
  return (
    <Button appearance="outline" size="small" icon={<ArrowLeft20Regular />} className={className} onClick={onClick}>
      {rotulo}
    </Button>
  );
}
