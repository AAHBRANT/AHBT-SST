import type { ReactElement, ReactNode } from 'react';
import { Tooltip, makeStyles, mergeClasses } from '@fluentui/react-components';
import type { TooltipProps } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  conteudo: {
    maxWidth: '320px',
    padding: `${tokensUi.espaco.sm} ${tokensUi.espaco.md}`,
    borderRadius: tokensUi.raio.md,
    border: `1px solid ${tokensUi.bordaSuave}`,
    backgroundColor: designTokens.colorSurface,
    boxShadow: '0 12px 32px rgba(0, 0, 0, 0.32)',
    color: designTokens.colorNeutralDark,
    fontSize: '12px',
    lineHeight: '18px',
    fontWeight: 500,
  },
  titulo: {
    marginBottom: '2px',
    color: designTokens.colorNeutralDark,
    fontSize: '12px',
    lineHeight: '18px',
    fontWeight: 700,
  },
  detalhe: {
    color: designTokens.colorNeutralMedium,
    fontSize: '11px',
    lineHeight: '16px',
  },
});

export interface SstTooltipProps {
  children: ReactElement;
  conteudo: ReactNode;
  titulo?: ReactNode;
  detalhes?: ReactNode[];
  posicao?: TooltipProps['positioning'];
  relacao?: TooltipProps['relationship'];
  atrasoEntrada?: number;
  atrasoSaida?: number;
  comSeta?: boolean;
  classNameConteudo?: string;
}

// Tooltip padrão do SST: mantém a acessibilidade e posicionamento do Fluent, mas centraliza o visual
// do app para dicas ricas como tags, commits, status e botões de ícone.
export function SstTooltip({
  children,
  conteudo,
  titulo,
  detalhes,
  posicao = 'above',
  relacao = 'description',
  atrasoEntrada = 250,
  atrasoSaida = 150,
  comSeta = true,
  classNameConteudo,
}: SstTooltipProps) {
  const estilos = useStyles();
  const corpo = (
    <>
      {titulo && <div className={estilos.titulo}>{titulo}</div>}
      <div>{conteudo}</div>
      {detalhes?.map((detalhe, indice) => (
        <div className={estilos.detalhe} key={indice}>
          {detalhe}
        </div>
      ))}
    </>
  );

  return (
    <Tooltip
      content={{ children: corpo, className: mergeClasses(estilos.conteudo, classNameConteudo) }}
      positioning={posicao}
      relationship={relacao}
      showDelay={atrasoEntrada}
      hideDelay={atrasoSaida}
      withArrow={comSeta}
    >
      {children}
    </Tooltip>
  );
}
