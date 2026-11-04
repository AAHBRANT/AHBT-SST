import type { ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

// Faixa "falta assinar" (opção C aprovada em 04/11/2026): fica acima do conteúdo do detalhe e leva o
// botão Assinar (BotaoAcao tom="assinar") ao lado da mensagem, para ninguém precisar procurar.
const useStyles = makeStyles({
  faixa: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    columnGap: '12px',
    rowGap: '8px',
    padding: '10px 14px',
    borderRadius: tokensUi.raio.sm,
    backgroundColor: 'var(--sst-faixa-assinar-fundo)',
    color: 'var(--sst-faixa-assinar-tinta)',
    border: '1px solid var(--sst-acao-assinar-fundo)',
  },
  texto: { flexGrow: 1, flexBasis: '220px', minWidth: 0, fontWeight: 600 },
});

export interface FaixaAssinaturaProps {
  /** Mensagem curta, ex.: "Falta a assinatura desta ocorrência." */
  children: ReactNode;
  /** Normalmente <BotaoAcao tom="assinar" …>. */
  acao: ReactNode;
}

export function FaixaAssinatura({ children, acao }: FaixaAssinaturaProps) {
  const e = useStyles();
  return (
    <div className={e.faixa} role="status">
      <span className={e.texto}>{children}</span>
      {acao}
    </div>
  );
}
