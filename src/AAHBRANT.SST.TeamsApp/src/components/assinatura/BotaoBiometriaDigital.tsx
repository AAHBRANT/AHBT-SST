import type { ReactNode } from 'react';
import { Button, makeStyles } from '@fluentui/react-components';
import { Fingerprint24Regular } from '@fluentui/react-icons';

// Botão padrão de captura por digital (leitor Futronic). Mesmo porte, peso e sombra do botão
// "Facial Azure" (SeletorFotoCamera, variante facialAzure) — pedido do usuário (29/09): os dois
// métodos ficam lado a lado com o mesmo padrão visual, para reconhecer rápido cada um. A digital
// usa o vinho da marca (#670000) e o facial o verde, então a cor continua diferenciando os dois.
const useEstilos = makeStyles({
  botao: {
    minHeight: '52px',
    minWidth: '148px',
    border: '1px solid transparent',
    borderRadius: '5px',
    backgroundColor: '#670000',
    color: '#ffffff',
    fontWeight: 800,
    boxShadow: 'inset 0 -3px 0 rgba(0, 0, 0, .18)',
    whiteSpace: 'nowrap',
    ':hover': {
      backgroundColor: '#4d0000',
      color: '#ffffff',
    },
    ':hover:active': {
      backgroundColor: '#390000',
      color: '#ffffff',
    },
    ':disabled': {
      backgroundColor: 'var(--colorNeutralBackgroundDisabled)',
      color: 'var(--colorNeutralForegroundDisabled)',
      boxShadow: 'none',
    },
  },
});

interface BotaoBiometriaDigitalProps {
  onClick: () => void;
  disabled?: boolean;
  children?: ReactNode;
}

export function BotaoBiometriaDigital({ onClick, disabled, children = 'Autenticar com digital' }: BotaoBiometriaDigitalProps) {
  const estilos = useEstilos();
  return (
    <Button
      appearance="primary"
      className={estilos.botao}
      size="large"
      icon={<Fingerprint24Regular />}
      onClick={onClick}
      disabled={disabled}
    >
      {children}
    </Button>
  );
}
