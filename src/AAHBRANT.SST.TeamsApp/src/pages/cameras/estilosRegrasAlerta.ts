import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '@ui';

// Estilos da Etapa 3 (Regras de alerta) e dos blocos de modo sombra da aba Alertas.
// .ts porque o lint proíbe makeStyles/Fluent direto em .tsx de pages/.
export { mergeClasses };

export const useEstilosRegrasAlerta = makeStyles({
  campoNumero: { maxWidth: '230px', '& input': { textAlign: 'right', fontWeight: 600 } },
  dica: { color: designTokens.colorNeutralMedium, marginTop: '6px', fontSize: '12px', lineHeight: '16px', fontWeight: 500 },
  dicaErro: { color: tokensUi.status.alerta.tinta, fontWeight: 600 },
  // Caixa azul do modo sombra (teste), diferente da escolha permanente do Telegram.
  caixaSombra: {
    marginTop: tokensUi.espaco.lg,
    padding: `${tokensUi.espaco.sm} ${tokensUi.espaco.lg} ${tokensUi.espaco.md}`,
    borderRadius: '12px',
    backgroundColor: tokensUi.status.info.fundo,
    border: `1px solid color-mix(in srgb, ${tokensUi.status.info.tinta} 25%, transparent)`,
  },
  caixaSombraTexto: { color: designTokens.colorNeutralDark, opacity: 0.8 },
  destaqueInfo: { color: tokensUi.status.info.tinta, fontWeight: 700 },
  // Telegram em pausa (Etapa 1) enquanto o modo sombra estiver ligado.
  pausado: {
    display: 'flex',
    alignItems: 'flex-start',
    gap: tokensUi.espaco.sm,
    marginTop: '6px',
    padding: `${tokensUi.espaco.sm} 10px`,
    borderRadius: tokensUi.raio.sm,
    fontSize: '12px',
    lineHeight: '16px',
    fontWeight: 600,
    color: tokensUi.status.atencao.tinta,
    backgroundColor: tokensUi.status.atencao.fundo,
    border: `1px solid color-mix(in srgb, ${tokensUi.status.atencao.tinta} 35%, transparent)`,
  },
  // Frase de resultado no fim da seção.
  resultado: {
    display: 'flex',
    alignItems: 'flex-start',
    gap: '10px',
    marginTop: tokensUi.espaco.xl,
    marginBottom: tokensUi.espaco.lg,
    padding: `${tokensUi.espaco.md} 14px`,
    borderRadius: '12px',
    backgroundColor: designTokens.colorNeutralLight,
    border: `1px solid ${designTokens.colorCardBorder}`,
    fontWeight: 600,
  },
  resultadoIcone: { flexShrink: 0, marginTop: '1px', display: 'inline-flex' },
  resultadoAvisa: { color: tokensUi.status.ok.tinta },
  resultadoSombra: { color: tokensUi.status.info.tinta },
  resultadoMudo: { color: tokensUi.status.neutro.tinta },
  // ---- Aba Alertas ----
  linhaSombra: {
    display: 'flex',
    flexDirection: 'column',
    gap: '6px',
    padding: `10px ${tokensUi.espaco.md}`,
    borderRadius: tokensUi.raio.md,
    backgroundColor: tokensUi.status.info.fundo,
    border: `1px solid color-mix(in srgb, ${tokensUi.status.info.tinta} 25%, transparent)`,
  },
  linhaSombraTitulo: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    fontWeight: 700,
    color: tokensUi.status.info.tinta,
  },
  confirmacao: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '6px', fontWeight: 700 },
  confirmacaoSim: { color: tokensUi.status.ok.tinta },
  confirmacaoNao: { color: tokensUi.status.neutro.tinta },
  confirmacaoDetalhe: { flexBasis: '100%', paddingLeft: '22px', fontWeight: 500, color: designTokens.colorNeutralMedium },
});
