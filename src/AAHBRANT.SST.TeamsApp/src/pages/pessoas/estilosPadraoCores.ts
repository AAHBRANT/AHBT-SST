import { makeStyles } from '@fluentui/react-components';
import { designTokens, tokensUi } from '@ui';

// Estilos do quadro "Aplicar padrão de cores" (.ts porque o lint proíbe makeStyles/Fluent direto em .tsx de pages/).
export const useEstilosPadraoCores = makeStyles({
  apoio: { color: designTokens.colorNeutralMedium },
  intro: { marginBottom: tokensUi.espaco.md },
  // Tabela no desktop; lista empilhada (como o mockup do celular) abaixo de 700px.
  soDesktop: { '@media (max-width: 700px)': { display: 'none' } },
  soMovel: { display: 'none', '@media (max-width: 700px)': { display: 'block' } },
  leitor: {
    position: 'absolute',
    width: '1px',
    height: '1px',
    overflow: 'hidden',
    clip: 'rect(0 0 0 0)',
    whiteSpace: 'nowrap',
  },
  itemMovel: {
    display: 'grid',
    gridTemplateColumns: 'auto minmax(0, 1fr)',
    gap: tokensUi.espaco.md,
    alignItems: 'center',
    padding: `${tokensUi.espaco.md} 0`,
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    ':first-child': { borderTop: 'none' },
  },
  nomeMovel: { fontWeight: 700 },
  trocaMovel: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: tokensUi.espaco.sm,
    marginTop: tokensUi.espaco.xs,
    fontSize: '13px',
    lineHeight: '18px',
  },
  seta: { color: designTokens.colorNeutralMedium, padding: '0 4px' },
});
