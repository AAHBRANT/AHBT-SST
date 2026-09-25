import type { MouseEvent, ReactElement, ReactNode } from 'react';
import { Button, makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

// Cor por função da ação (pedido do usuário, 25/09/2026 — "opção C, marca AAHBRANT"): botão de
// ação nunca fica sem fundo, para cada ação ser reconhecida de relance.
//   ver       → contorno vinho       (Visualizar, Abrir, Editar e demais ações neutras)
//   baixar    → vinho sólido         (Baixar, Exportar PDF)
//   excluir   → vermelho sólido      (Excluir — só Administrador vê)
//   atencao   → âmbar sólido         (Anexar arquivo que está faltando)
// Cores de marca vêm de --sst-acao-marca-* (index.css): no tema escuro (padrão do app) o vinho
// #670000 puro sumiria sobre o fundo, então lá o sólido e o contorno clareiam.
const MARCA_FUNDO = 'var(--sst-acao-marca-fundo)';
const MARCA_FUNDO_HOVER = 'var(--sst-acao-marca-fundo-hover)';
const MARCA_TINTA = 'var(--sst-acao-marca-tinta)';
const MARCA_WASH = 'var(--sst-acao-marca-wash)';

export type TomBotaoAcao = 'ver' | 'baixar' | 'excluir' | 'atencao';

const useStyles = makeStyles({
  base: {
    minWidth: '32px',
    borderRadius: tokensUi.raio.sm,
    transitionProperty: 'background-color, filter',
    transitionDuration: tokensUi.duracao.rapido,
  },
  soIcone: { width: '32px', height: '32px', padding: 0 },
  ver: {
    backgroundColor: 'transparent',
    color: MARCA_TINTA,
    border: `1px solid ${MARCA_TINTA}`,
    ':hover': { backgroundColor: MARCA_WASH, color: MARCA_TINTA, border: `1px solid ${MARCA_TINTA}` },
    ':hover:active': { backgroundColor: MARCA_WASH, color: MARCA_TINTA, filter: 'brightness(0.9)' },
  },
  baixar: {
    backgroundColor: MARCA_FUNDO,
    color: designTokens.colorWhite,
    border: `1px solid ${MARCA_FUNDO}`,
    ':hover': { backgroundColor: MARCA_FUNDO_HOVER, color: designTokens.colorWhite, border: `1px solid ${MARCA_FUNDO_HOVER}` },
    ':hover:active': { backgroundColor: MARCA_FUNDO_HOVER, color: designTokens.colorWhite, filter: 'brightness(0.9)' },
  },
  excluir: {
    backgroundColor: tokensUi.status.alerta.tinta,
    color: designTokens.colorWhite,
    border: `1px solid ${tokensUi.status.alerta.tinta}`,
    ':hover': { backgroundColor: tokensUi.status.alerta.tinta, color: designTokens.colorWhite, filter: 'brightness(0.9)' },
    ':hover:active': { backgroundColor: tokensUi.status.alerta.tinta, color: designTokens.colorWhite, filter: 'brightness(0.8)' },
  },
  atencao: {
    backgroundColor: tokensUi.status.atencao.tinta,
    color: designTokens.colorWhite,
    border: `1px solid ${tokensUi.status.atencao.tinta}`,
    ':hover': { backgroundColor: tokensUi.status.atencao.tinta, color: designTokens.colorWhite, filter: 'brightness(0.9)' },
    ':hover:active': { backgroundColor: tokensUi.status.atencao.tinta, color: designTokens.colorWhite, filter: 'brightness(0.8)' },
  },
  desabilitado: { opacity: 0.45 },
});

export interface BotaoAcaoProps {
  tom: TomBotaoAcao;
  /** Obrigatório: em botão só de ícone é o que o leitor de tela e o tooltip mostram. */
  'aria-label': string;
  icon?: ReactElement;
  onClick?: (evento: MouseEvent<HTMLButtonElement>) => void;
  disabled?: boolean;
  title?: string;
  size?: 'small' | 'medium';
  className?: string;
  children?: ReactNode;
  'aria-expanded'?: boolean;
  'aria-controls'?: string;
}

// Botão de ação de linha/documento. Sem children vira quadrado 32×32 só com ícone (title = rótulo);
// com children vira botão com texto, na mesma cor. Substitui <Button appearance="subtle" icon=…>.
export function BotaoAcao({ tom, className, children, title, disabled, ...resto }: BotaoAcaoProps) {
  const e = useStyles();
  return (
    <Button
      {...resto}
      disabled={disabled}
      title={title ?? resto['aria-label']}
      appearance="secondary"
      className={mergeClasses(e.base, !children && e.soIcone, e[tom], disabled && e.desabilitado, className)}
    >
      {children}
    </Button>
  );
}
