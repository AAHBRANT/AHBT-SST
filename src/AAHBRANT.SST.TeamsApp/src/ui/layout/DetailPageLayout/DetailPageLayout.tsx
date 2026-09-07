import type { ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';
import { PageHeader, type PageHeaderProps } from '../../compostos/PageHeader/PageHeader';

const useStyles = makeStyles({
  grid: { display: 'grid', gridTemplateColumns: '1fr 320px', gap: tokensUi.espaco.lg, alignItems: 'start',
    '@media (max-width: 1100px)': { gridTemplateColumns: '1fr' } },
  lateral: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg, position: 'sticky', top: 0,
    '@media (max-width: 1100px)': { order: -1, position: 'static' } },
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
