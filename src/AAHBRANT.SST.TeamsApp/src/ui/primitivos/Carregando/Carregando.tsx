import { Skeleton, SkeletonItem, makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  coluna: { display: 'flex', flexDirection: 'column', gap: '10px', padding: '4px 0' },
  kpis: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: tokensUi.espaco.lg },
});

export interface CarregandoProps {
  variante?: 'lista' | 'card' | 'kpi' | 'detalhe';
  linhas?: number;
}

// Skeleton no formato do conteúdo que substitui (spec §3). Renomeia components/ListaCarregando.tsx e
// ganha variantes — a diferença entre "carregando" e "vazio" foi pedido do usuário em 31/08.
export function Carregando({ variante = 'lista', linhas = 4 }: CarregandoProps) {
  const estilos = useStyles();
  if (variante === 'kpi') {
    return (
      <Skeleton aria-label="Carregando indicadores" className={estilos.kpis}>
        {Array.from({ length: linhas }).map((_, i) => <SkeletonItem key={i} style={{ height: 96, borderRadius: 12 }} />)}
      </Skeleton>
    );
  }
  const altura = variante === 'card' ? 160 : variante === 'detalhe' ? 28 : 40;
  return (
    <Skeleton aria-label="Carregando" className={estilos.coluna}>
      {Array.from({ length: linhas }).map((_, i) => <SkeletonItem key={i} style={{ height: altura, borderRadius: variante === 'card' ? 12 : 6 }} />)}
    </Skeleton>
  );
}
