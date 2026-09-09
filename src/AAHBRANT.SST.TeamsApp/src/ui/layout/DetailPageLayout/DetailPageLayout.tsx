import type { ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';
import { PageHeader, type PageHeaderProps } from '../../compostos/PageHeader/PageHeader';

const useStyles = makeStyles({
  grid: { display: 'grid', gridTemplateColumns: '1fr 320px', gap: tokensUi.espaco.lg, alignItems: 'start',
    '@media (max-width: 1100px)': { gridTemplateColumns: '1fr' } },
  // Lateral mais alta que a viewport (ações com formulário aberto) rola dentro de si; sem isso o
  // sticky prendia o topo e o rodapé ficava inalcançável. 112px = 64px do header + 24px×2 de
  // padding do conteúdo. overflowX: hidden porque overflowY: auto faz o eixo X computar 'auto'
  // também — sem isso a sombra dos cards gerava barra horizontal parasita dentro da lateral.
  // Abaixo de 1100px a lateral empilha acima do conteúdo: aí a altura máxima e a rolagem própria
  // TÊM de ser desfeitas, senão ela vira uma caixa de rolagem de altura de viewport na frente do
  // conteúdo da página e obriga a rolar dentro dela antes de alcançar o corpo.
  lateral: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg, position: 'sticky', top: 0,
    maxHeight: 'calc(100vh - 112px)', overflowY: 'auto', overflowX: 'hidden',
    // Os dois eixos voltam a 'visible' juntos: com um eixo 'hidden', o outro 'visible' computa
    // como 'auto' e a lateral continuaria sendo um contexto de rolagem.
    '@media (max-width: 1100px)': { order: -1, position: 'static', maxHeight: 'none', overflowY: 'visible', overflowX: 'visible' } },
  principal: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg, minWidth: 0 },
});

export interface DetailPageLayoutProps {
  cabecalho: PageHeaderProps;
  lateral?: ReactNode;
  children: ReactNode;
}

// Página de detalhe (spec §3, §4.3): cabeçalho com voltar/título/status/ações; lateral fixa à direita
// com resumo e ações do fluxo; conteúdo em seções à esquerda. Abaixo de 1100px a lateral sobe.
// É a forma das 6 páginas de detalhe de ~500 linhas (NC, PCMSO, DDS, Inspeção, Reunião CIPA, PT).
export function DetailPageLayout({ cabecalho, lateral, children }: DetailPageLayoutProps) {
  const e = useStyles();
  return (
    <div>
      <PageHeader {...cabecalho} />
      <div className={e.grid}>
        <div className={e.principal}>{children}</div>
        {lateral && <aside className={e.lateral}>{lateral}</aside>}
      </div>
    </div>
  );
}
