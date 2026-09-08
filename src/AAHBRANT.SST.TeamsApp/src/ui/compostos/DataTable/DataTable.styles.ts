// Estilos da peça DataTable: linha-como-cartão, densidades, cabeçalho fixo e linha expansível (spec §3).
import { makeStyles } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

export const useDataTableStyles = makeStyles({
  wrap: { overflowX: 'auto' },
  // Com cabeçalho fixo a tabela não rola horizontalmente dentro do card: overflow visível é
  // necessário para que "position: sticky" resolva contra o scroll da página (main.content),
  // não contra este wrapper (que nunca rola verticalmente).
  wrapCabecalhoFixo: { overflowX: 'visible', overflowY: 'visible' },
  tabela: { width: '100%', borderCollapse: 'separate', borderSpacing: '0 8px', marginTop: '-8px' },
  compacta: { borderSpacing: '0 6px', marginTop: '-6px' },
  cabecalhoFixo: { '& thead th': { position: 'sticky', top: 0, backgroundColor: designTokens.colorSurface, zIndex: 1 } },
  th: { textAlign: 'left', fontSize: '12px', lineHeight: '16px', fontWeight: 600, color: designTokens.colorNeutralMedium, padding: '6px 14px 10px', borderBottom: `1px solid ${designTokens.colorCardBorder}`, whiteSpace: 'nowrap' },
  // Linha-como-cartão (pedido do usuário 03/09): vivia como sobrescrita global .fui-TableRow.fui-TableRow
  // em index.css; passa a ser estilo interno desta peça e o hack sai na Onda 3.
  tr: { boxShadow: tokensUi.sombraLinha, transitionProperty: 'transform, box-shadow', transitionDuration: tokensUi.duracao.rapido, transitionTimingFunction: tokensUi.curva },
  trClicavel: { cursor: 'pointer', ':hover': { transform: 'translateY(-1px)', boxShadow: tokensUi.sombraLinhaHover }, ':focus-visible': { outline: `2px solid ${tokensUi.chrome.ativoFundo}`, outlineOffset: '2px' } },
  td: { backgroundColor: designTokens.colorSurface, padding: '12px 14px', borderTop: `1px solid ${tokensUi.bordaSuave}`, borderBottom: `1px solid ${tokensUi.bordaSuave}`, verticalAlign: 'middle' },
  tdCompacta: { padding: '8px 12px' },
  tdPrimeira: { borderLeft: `1px solid ${tokensUi.bordaSuave}`, borderRadius: `${tokensUi.raio.md} 0 0 ${tokensUi.raio.md}` },
  tdUltima: { borderRight: `1px solid ${tokensUi.bordaSuave}`, borderRadius: `0 ${tokensUi.raio.md} ${tokensUi.raio.md} 0` },
  direita: { textAlign: 'right' },
  centro: { textAlign: 'center' },
  acoes: { display: 'inline-flex', gap: '4px', justifyContent: 'flex-end' },
  // Área expansível empilha o que o consumidor passar: sem isto, título e conteúdo colam.
  expandida: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm, backgroundColor: designTokens.colorNeutralLight, borderRadius: `0 0 ${tokensUi.raio.md} ${tokensUi.raio.md}`, padding: `${tokensUi.espaco.md} ${tokensUi.espaco.lg}` },
});
