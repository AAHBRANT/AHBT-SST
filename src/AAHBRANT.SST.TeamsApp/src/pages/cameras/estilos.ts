import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '@ui';

// Estilos da tela Monitorar em .ts: o makeStyles não é exportado por @ui e o lint proíbe o import
// direto do Fluent em .tsx de pages/ (mesmo padrão de pages/pageStyles.ts).
export { mergeClasses };

export const useStyles = makeStyles({
  colunas: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.xl, alignItems: 'flex-start' },
  colunaVideo: { flex: '999 1 560px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md },
  colunaPainel: { flex: '1 1 320px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg },
  caixaVideo: {
    position: 'relative',
    aspectRatio: '16 / 9',
    width: '100%',
    borderRadius: tokensUi.raio.lg,
    border: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: designTokens.colorNeutralLight,
    overflow: 'hidden',
  },
  video: { width: '100%', height: '100%', objectFit: 'cover', display: 'block' },
  espera: {
    position: 'absolute',
    inset: 0,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    gap: tokensUi.espaco.md,
    padding: tokensUi.espaco.lg,
    textAlign: 'center',
    color: designTokens.colorNeutralMedium,
    backgroundColor: designTokens.colorNeutralLight,
  },
  selo: {
    position: 'absolute',
    padding: '6px 12px',
    borderRadius: tokensUi.raio.sm,
    color: designTokens.colorNeutralDark,
    backgroundColor: `color-mix(in srgb, ${designTokens.colorSurface} 88%, transparent)`,
    border: `1px solid ${designTokens.colorCardBorder}`,
  },
  seloTopo: { left: tokensUi.espaco.lg, top: tokensUi.espaco.lg },
  // Linha abaixo do vídeo: qual câmera do aparelho está em uso e o botão de virar.
  linhaLado: {
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: tokensUi.espaco.md,
  },
  botaoVirar: { minHeight: '44px' },
  seloBase: { right: tokensUi.espaco.lg, bottom: tokensUi.espaco.lg },
  linha: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokensUi.espaco.sm },
  cardInterno: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md },
  rotuloMedio: { color: designTokens.colorNeutralMedium },
  indicadores: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: tokensUi.espaco.md },
  lista: { listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md },
  itemOcorrencia: { display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '2px', minWidth: 0 },
  // Trabalhador identificado pelo rosto na última análise.
  faixaRosto: {
    display: 'flex',
    alignItems: 'center',
    gap: tokensUi.espaco.md,
    minWidth: 0,
    padding: `${tokensUi.espaco.sm} ${tokensUi.espaco.md}`,
    borderRadius: tokensUi.raio.sm,
    backgroundColor: tokensUi.status.ok.fundo,
    color: designTokens.colorNeutralDark,
  },
  faixaRostoTextos: { display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '4px', minWidth: 0 },
  verTodas: {
    display: 'inline-flex',
    alignItems: 'center',
    minHeight: '44px',
    color: designTokens.colorPrimary,
    fontWeight: 600,
  },
  botao: { minHeight: '48px', width: '100%' },
  topoOcorrencias: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: tokensUi.espaco.md },
});
