import { makeStyles, mergeClasses, shorthands, tokens } from '@fluentui/react-components';
import { designTokens, tokensUi } from '@ui';

// Estilos da tela "Realizar integração", copiados do mockup aprovado (scripts/mockups/
// realizar-integracao-2026-11-11-v4.html): cabeçalho vinho, trilha de 5 etapas, linhas de pessoas com
// alvo de toque grande (luva e sol) e barra fixa embaixo no celular. Cores só por tokens do design
// system; os breakpoints seguem a largura da área de conteúdo do app (com a barra lateral aberta).
const LARGO = '@media (min-width: 1180px)';
const MEDIO = '@media (min-width: 760px)';
const ESTREITO = '@media (max-width: 560px)';

export const useEstilosIntegracao = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg, minWidth: 0, paddingBottom: '8px' },
  topo: { display: 'flex', alignItems: 'center', gap: tokensUi.espaco.md, flexWrap: 'wrap' },
  migalha: { fontSize: '12px', color: designTokens.colorNeutralMedium },
  layout: {
    display: 'grid',
    gap: tokensUi.espaco.lg,
    gridTemplateColumns: 'minmax(0, 1fr)',
    [LARGO]: { gridTemplateColumns: 'minmax(0, 1fr) 300px', alignItems: 'start' },
  },
  principal: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg, minWidth: 0 },
  lateral: { display: 'none', [LARGO]: { display: 'block', position: 'sticky', top: '16px' } },
  rodapePrincipal: { display: 'block', [LARGO]: { display: 'none' } },

  // Cabeçalho (cartão vinho)
  cab: {
    background: 'linear-gradient(135deg, var(--sst-color-primary), color-mix(in srgb, var(--sst-color-primary) 55%, #000))',
    color: '#ffffff',
    borderRadius: tokensUi.raio.lg,
    padding: '18px',
  },
  cabLinha: { display: 'flex', gap: '12px', alignItems: 'flex-start', justifyContent: 'space-between', flexWrap: 'wrap' },
  cabEyebrow: { fontSize: '11px', letterSpacing: '0.06em', textTransform: 'uppercase', opacity: 0.75 },
  cabTitulo: { fontSize: '20px', lineHeight: '26px', fontWeight: 700, margin: 0 },
  cabMeta: {
    display: 'grid',
    gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
    gap: '12px 18px',
    marginTop: '14px',
    [MEDIO]: { gridTemplateColumns: 'repeat(4, minmax(0, 1fr))' },
  },
  cabMetaRotulo: { display: 'block', fontSize: '11px', letterSpacing: '0.05em', textTransform: 'uppercase', opacity: 0.75 },
  cabMetaValor: { fontWeight: 600, overflowWrap: 'anywhere' },
  cabChip: {
    display: 'inline-flex',
    alignItems: 'center',
    padding: '4px 12px',
    borderRadius: tokensUi.raio.full,
    fontSize: '12px',
    fontWeight: 700,
    backgroundColor: 'rgba(255,255,255,0.16)',
    color: '#ffffff',
    whiteSpace: 'nowrap',
  },
  cabChipFinal: { backgroundColor: tokensUi.status.ok.fundo, color: tokensUi.status.ok.tinta },
  cabAviso: {
    marginTop: '14px',
    display: 'flex',
    gap: '10px',
    padding: '12px 14px',
    borderRadius: '8px',
    fontSize: '13px',
    lineHeight: '19px',
    backgroundColor: 'rgba(255,255,255,0.14)',
  },

  // Trilha das 5 etapas
  trilha: {
    display: 'flex',
    gap: '6px',
    margin: 0,
    padding: 0,
    listStyleType: 'none',
    [MEDIO]: { display: 'grid', gridTemplateColumns: 'repeat(5, minmax(0, 1fr))', gap: '8px' },
  },
  etapa: {
    display: 'flex',
    gap: '10px',
    alignItems: 'center',
    border: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: designTokens.colorSurface,
    borderRadius: '99px',
    minWidth: '48px',
    width: '48px',
    minHeight: '48px',
    padding: 0,
    justifyContent: 'center',
    flexShrink: 0,
    [MEDIO]: {
      width: 'auto',
      flexShrink: 1,
      minWidth: 0,
      minHeight: '64px',
      borderRadius: tokensUi.raio.lg,
      padding: '10px 12px',
      justifyContent: 'flex-start',
    },
  },
  etapaTexto: { display: 'none', minWidth: 0, [MEDIO]: { display: 'block' } },
  etapaNome: { display: 'block', fontSize: '13px', lineHeight: '17px', fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' },
  etapaSub: { display: 'block', fontSize: '11px', lineHeight: '14px', color: designTokens.colorNeutralMedium, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' },
  etapaFeita: {
    ...shorthands.borderColor(tokensUi.status.ok.tinta),
    backgroundColor: tokensUi.status.ok.fundo,
    color: tokensUi.status.ok.tinta,
  },
  etapaFeitaTexto: { color: tokensUi.status.ok.tinta },
  etapaAtual: {
    backgroundColor: designTokens.colorPrimary,
    ...shorthands.borderColor(designTokens.colorPrimary),
    color: '#ffffff',
    flexGrow: 1,
    width: 'auto',
    padding: '0 14px 0 8px',
    justifyContent: 'flex-start',
    minWidth: 0,
    [MEDIO]: { flexGrow: 0 },
  },
  etapaAtualTexto: { display: 'block', color: '#ffffff' },
  etapaAtualSub: { color: 'rgba(255,255,255,0.85)' },
  etapaTravada: { backgroundColor: designTokens.colorNeutralLight, color: designTokens.colorNeutralMedium },
  etapaPulada: { ...shorthands.borderStyle('dashed'), backgroundColor: 'transparent', color: designTokens.colorNeutralMedium },
  legendaTrilha: { fontSize: '12px', color: designTokens.colorNeutralMedium, marginTop: '8px' },

  // Número redondo (cabeçalho de seção e trilha)
  numero: {
    width: '28px',
    height: '28px',
    borderRadius: '50%',
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: '12px',
    fontWeight: 700,
    border: `1px solid ${designTokens.colorCardBorder}`,
    color: designTokens.colorNeutralMedium,
    flexShrink: 0,
  },
  numeroFeito: { backgroundColor: tokensUi.status.ok.fundo, color: tokensUi.status.ok.tinta, border: 'none' },
  numeroAtual: { backgroundColor: designTokens.colorPrimary, color: '#ffffff', border: 'none' },
  numeroNaTrilhaAtual: { backgroundColor: '#ffffff', color: 'var(--sst-color-primary)', border: 'none' },

  // Seções
  secTitulo: { display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' },
  secNome: { fontSize: '16px', lineHeight: '22px', fontWeight: 700, margin: 0 },
  mudo: { color: designTokens.colorNeutralMedium, fontSize: '12px' },
  tituloMini: {
    fontSize: '11px',
    fontWeight: 700,
    letterSpacing: '0.05em',
    textTransform: 'uppercase',
    color: designTokens.colorNeutralMedium,
    margin: '14px 0 6px',
  },

  // Botões de ação grandes (44 a 52 px)
  btnGrande: { minHeight: '52px', fontSize: '15px', padding: '0 22px', fontWeight: 600, whiteSpace: 'normal', textAlign: 'center', lineHeight: '18px' },
  btnMedio: { minHeight: '44px', fontWeight: 600 },
  btnCheio: { width: '100%' },

  // Pessoas (início)
  pessoasGrade: {
    display: 'grid',
    gap: '14px',
    gridTemplateColumns: 'minmax(0, 1fr)',
    [MEDIO]: { gridTemplateColumns: 'minmax(0, 5fr) minmax(0, 6fr)' },
  },
  listaPessoas: { display: 'flex', flexDirection: 'column', gap: '8px' },
  linhaPessoa: {
    display: 'grid',
    gridTemplateColumns: '44px minmax(0, 1fr) 22px',
    alignItems: 'center',
    gap: '12px',
    width: '100%',
    padding: '10px 12px',
    minHeight: '64px',
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '8px',
    backgroundColor: designTokens.colorSurface,
    textAlign: 'left',
    cursor: 'pointer',
    color: 'inherit',
    font: 'inherit',
    ':hover': { backgroundColor: designTokens.colorNeutralLight },
    ':focus-visible': { outline: `2px solid ${tokens.colorBrandStroke1}`, outlineOffset: '2px' },
  },
  linhaPessoaSel: {
    ...shorthands.borderColor(designTokens.colorPrimary),
    backgroundColor: 'color-mix(in srgb, var(--sst-color-primary) 12%, var(--sst-color-surface))',
    boxShadow: `inset 0 0 0 1px var(--sst-color-primary)`,
  },
  avatar: {
    width: '44px',
    height: '44px',
    borderRadius: '50%',
    backgroundColor: 'color-mix(in srgb, var(--sst-color-primary) 12%, var(--sst-color-surface))',
    color: tokens.colorBrandForeground2,
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontWeight: 700,
    fontSize: '15px',
    flexShrink: 0,
  },
  avatarSel: { backgroundColor: designTokens.colorPrimary, color: '#ffffff' },
  avatarPequeno: { width: '36px', height: '36px', fontSize: '13px' },
  nomePessoa: { fontWeight: 700, display: 'block' },
  funcaoPessoa: { fontSize: '12px', color: designTokens.colorNeutralMedium },
  pontos: { display: 'flex', gap: '4px', marginTop: '5px', flexWrap: 'wrap' },
  ponto: { fontSize: '10px', fontWeight: 700, borderRadius: '6px', padding: '1px 6px' },
  radio: { width: '20px', height: '20px', borderRadius: '50%', border: `2px solid ${designTokens.colorCardBorder}` },
  radioSel: { border: '6px solid var(--sst-color-primary)' },
  detalhePessoa: {
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.lg,
    backgroundColor: designTokens.colorNeutralLight,
    padding: '14px',
  },
  detalheTopo: { display: 'flex', gap: '12px', alignItems: 'center', marginBottom: '10px' },
  linhaEtapaPessoa: {
    display: 'grid',
    gridTemplateColumns: '78px minmax(0, 1fr) auto',
    gap: '10px',
    alignItems: 'center',
    padding: '10px 0',
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    [ESTREITO]: { gridTemplateColumns: '70px minmax(0, 1fr)' },
  },
  codigoEtapaPessoa: { fontWeight: 700, fontSize: '12px', color: tokens.colorBrandForeground2 },
  campo: { display: 'flex', flexDirection: 'column', gap: '6px', marginBottom: '14px' },
  campoRotulo: { fontSize: '12px', fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase', color: designTokens.colorNeutralMedium },

  // Carga e períodos
  carga: { backgroundColor: designTokens.colorNeutralLight, borderRadius: '8px', padding: '10px 12px' },
  cargaLinha: { display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: '8px', flexWrap: 'wrap' },
  cargaValor: { fontSize: '20px', lineHeight: '26px', fontWeight: 700 },
  chipAberto: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '5px',
    padding: '2px 9px',
    borderRadius: tokensUi.raio.full,
    fontSize: '11px',
    fontWeight: 700,
    border: `1px dashed ${tokensUi.status.atencao.tinta}`,
    color: tokensUi.status.atencao.tinta,
  },
  periodos: { display: 'flex', flexDirection: 'column', gap: '6px', marginTop: '10px' },
  periodo: {
    display: 'grid',
    gridTemplateColumns: 'minmax(0, 1fr) auto',
    alignItems: 'center',
    gap: '8px',
    padding: '6px 10px',
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '8px',
    backgroundColor: designTokens.colorSurface,
    fontSize: '13px',
  },
  periodoForm: { display: 'flex', gap: '8px', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '10px' },
  restr: {
    display: 'flex',
    gap: '8px',
    alignItems: 'center',
    fontSize: '12px',
    fontWeight: 600,
    padding: '8px 10px',
    borderRadius: '8px',
    marginBottom: '12px',
    backgroundColor: 'color-mix(in srgb, var(--sst-color-primary) 12%, var(--sst-color-surface))',
    color: tokens.colorBrandForeground2,
  },
  kv: {
    display: 'grid',
    gridTemplateColumns: '96px minmax(0, 1fr)',
    gap: '4px 12px',
    fontSize: '13px',
    margin: '12px 0 0',
  },
  kvRotulo: { color: designTokens.colorNeutralMedium, margin: 0 },
  kvValor: { margin: 0 },

  // Fotos
  fotosNota: { fontSize: '11px', color: designTokens.colorNeutralMedium, marginTop: '8px', textAlign: 'center' },
  seloFoto: { display: 'inline-flex', alignItems: 'center', gap: '6px' },

  // Linhas de participante (assinatura e OS)
  linhaParticipante: {
    display: 'grid',
    gridTemplateColumns: '36px minmax(0, 1fr) auto',
    gap: '12px',
    alignItems: 'center',
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '8px',
    padding: '10px 12px',
    marginBottom: '8px',
    backgroundColor: designTokens.colorSurface,
    [ESTREITO]: { gridTemplateColumns: '36px minmax(0, 1fr)' },
  },
  acoesLinha: { display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'flex-end', [ESTREITO]: { gridColumn: '1 / -1', justifyContent: 'stretch' } },
  linhaParticipanteBotao: { [ESTREITO]: { gridColumn: '1 / -1' } },
  grupo: {
    display: 'grid',
    gridTemplateColumns: 'minmax(0, 1fr) auto',
    gap: '10px 14px',
    alignItems: 'center',
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderLeft: `4px solid ${tokensUi.status.alerta.tinta}`,
    borderRadius: '8px',
    padding: '12px 14px',
    marginBottom: '10px',
    backgroundColor: designTokens.colorSurface,
    [ESTREITO]: { gridTemplateColumns: 'minmax(0, 1fr)' },
  },
  grupoNomes: { fontSize: '13px', marginTop: '4px' },

  // Resumo lateral e barra fixa
  resumoLinha: {
    display: 'flex',
    justifyContent: 'space-between',
    gap: '8px',
    padding: '8px 0',
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    fontSize: '13px',
    ':first-of-type': { borderTop: 'none' },
  },
  faltaLista: { margin: '6px 0 0', paddingLeft: '18px', fontSize: '13px' },
  barra: {
    display: 'flex',
    position: 'sticky',
    bottom: 0,
    zIndex: 20,
    gap: '10px',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '10px 16px',
    backgroundColor: designTokens.colorSurface,
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    [ESTREITO]: { flexDirection: 'column', alignItems: 'stretch' },
    [LARGO]: { display: 'none' },
  },
  barraResumo: {
    flexGrow: 1,
    minWidth: 0,
    fontSize: '12px',
    display: '-webkit-box',
    WebkitLineClamp: 2,
    WebkitBoxOrient: 'vertical',
    overflow: 'hidden',
  },

  // Resumo da integração concluída
  linhaResumo: {
    display: 'grid',
    gridTemplateColumns: '36px minmax(0, 1fr) auto',
    gap: '12px',
    alignItems: 'center',
    padding: '12px 0',
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    ':first-of-type': { borderTop: 'none' },
    [ESTREITO]: { gridTemplateColumns: '36px minmax(0, 1fr)' },
  },
  selo: { display: 'flex', gap: '8px', alignItems: 'flex-start', marginTop: '14px', fontSize: '12px', lineHeight: '17px', color: designTokens.colorNeutralMedium },
});

// pages/ não importa o Fluent direto (spec 2026-09-07 §2.2): quem precisa de mergeClasses pega daqui.
export { mergeClasses };

export type TomPonto = 'valida' | 'neutra' | 'marca';

/** Cores do selo de cada etapa na lista de pessoas do início (válida, não exigida/a confirmar, nesta integração). */
export function corDoPonto(tom: TomPonto): { backgroundColor: string; color: string } {
  if (tom === 'valida') return { backgroundColor: tokens.colorPaletteGreenBackground2, color: tokens.colorPaletteGreenForeground2 };
  if (tom === 'neutra') return { backgroundColor: tokens.colorNeutralBackground3, color: tokens.colorNeutralForeground2 };
  return { backgroundColor: 'color-mix(in srgb, var(--sst-color-primary) 12%, var(--sst-color-surface))', color: tokens.colorBrandForeground2 };
}
