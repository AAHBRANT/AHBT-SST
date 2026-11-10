import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '@ui';

// Estilos da aba Alertas de EPI (.ts porque o lint proíbe makeStyles/Fluent direto em .tsx de pages/).
export { mergeClasses };

const TOQUE = '44px';

export const useEstilosAlertas = makeStyles({
  raiz: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.xl, minWidth: 0 },
  filtros: { display: 'flex', flexWrap: 'wrap', alignItems: 'flex-end', gap: tokensUi.espaco.md },
  campoFiltro: { minWidth: 'min(180px, 100%)', flex: '0 1 200px', '& select': { minHeight: TOQUE } },
  resumo: { color: designTokens.colorNeutralMedium, paddingBottom: tokensUi.espaco.md },
  grade: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(min(340px, 100%), 1fr))',
    gap: tokensUi.espaco.lg,
  },
  cartao: {
    minWidth: 0,
    display: 'flex',
    flexDirection: 'column',
    overflow: 'hidden',
    borderRadius: tokensUi.raio.lg,
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
  },
  areaFoto: { position: 'relative' },
  chipsFoto: {
    position: 'absolute',
    left: tokensUi.espaco.md,
    right: tokensUi.espaco.md,
    top: tokensUi.espaco.md,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: tokensUi.espaco.sm,
    flexWrap: 'wrap',
  },
  corpo: { padding: tokensUi.espaco.lg, display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md },
  textos: { display: 'flex', flexDirection: 'column', gap: '4px' },
  apoio: { color: designTokens.colorNeutralMedium },
  telegram: { display: 'inline-flex', alignItems: 'center', gap: tokensUi.espaco.sm },
  telegramOk: { color: tokensUi.status.ok.tinta },
  telegramAviso: { color: tokensUi.status.atencao.tinta },
  faixaTrabalhador: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
    minWidth: 0,
    padding: `${tokensUi.espaco.sm} ${tokensUi.espaco.md}`,
    borderRadius: tokensUi.raio.sm,
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorNeutralDark,
  },
  // Faixa com avatar (identificado pelo rosto / não identificado): avatar à esquerda, textos à direita.
  faixaComAvatar: { flexDirection: 'row', alignItems: 'center', gap: tokensUi.espaco.md },
  faixaRosto: { backgroundColor: tokensUi.status.ok.fundo },
  faixaTextos: { display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '4px', minWidth: 0 },
  acoes: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.sm },
  botao: { flex: '1 1 140px', minHeight: TOQUE },
  rodape: { color: designTokens.colorNeutralMedium },
  dialogoCorpo: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md },
  fotoCaixa: {
    position: 'relative',
    aspectRatio: '16 / 9',
    width: '100%',
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorNeutralMedium,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    textAlign: 'center',
  },
  fotoImg: { width: '100%', height: '100%', objectFit: 'cover', display: 'block' },
});
