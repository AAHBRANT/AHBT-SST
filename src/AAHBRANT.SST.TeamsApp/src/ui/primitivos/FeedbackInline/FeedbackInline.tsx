import type { ReactNode } from 'react';
import { Button, MessageBar, MessageBarActions, MessageBarBody, makeStyles } from '@fluentui/react-components';
import { DismissRegular } from '@fluentui/react-icons';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({ root: { marginBottom: tokensUi.espaco.lg, borderRadius: tokensUi.raio.md } });

export interface FeedbackInlineProps {
  tom: 'erro' | 'aviso' | 'sucesso' | 'info';
  children: ReactNode;
  acao?: { rotulo: string; aoClicar: () => void };
  aoFechar?: () => void;
}

const intentPorTom = { erro: 'error', aviso: 'warning', sucesso: 'success', info: 'info' } as const;

// Mensagem de feedback dentro da página (spec §3). Wrapper de MessageBar do Fluent (0 usos até aqui).
// Substitui <Text className={estilos.erro}> — erro sem ícone, sem ação e sem fechar.
export function FeedbackInline({ tom, children, acao, aoFechar }: FeedbackInlineProps) {
  const estilos = useStyles();
  return (
    <MessageBar
      intent={intentPorTom[tom]}
      className={estilos.root}
      role={tom === 'erro' || tom === 'aviso' ? 'alert' : 'status'}
    >
      <MessageBarBody>{children}</MessageBarBody>
      {(acao || aoFechar) && (
        <MessageBarActions containerAction={aoFechar && <Button appearance="transparent" icon={<DismissRegular />} aria-label="Fechar" onClick={aoFechar} />}>
          {acao && <Button size="small" onClick={acao.aoClicar}>{acao.rotulo}</Button>}
        </MessageBarActions>
      )}
    </MessageBar>
  );
}
