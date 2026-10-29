import { makeStyles } from '@fluentui/react-components';
import { Card, Legenda, designTokens, usePaletaGraficos } from '@ui';
import { RotuloEscopo } from './RotuloEscopo';

export interface ConformidadeObra {
  id: string;
  nome: string;
  /** Média dos indicadores disponíveis da obra (0 a 100). */
  geral: number;
  epi: number | null;
  treinamentos: number | null;
  aso: number | null;
}

interface ConformidadePorObraCardProps {
  linhas: ConformidadeObra[];
  /** Clique numa obra (28/10/2026): cada linha é clicável sozinha. */
  aoAbrirObra?: (obra: ConformidadeObra) => void;
}

const useStyles = makeStyles({
  lista: { display: 'flex', flexDirection: 'column', gap: '20px' },
  linha: {
    display: 'grid',
    gridTemplateColumns: 'minmax(0, 1fr) 44px',
    columnGap: '12px',
    rowGap: '6px',
    alignItems: 'center',
    padding: '8px 10px',
    margin: '-8px -10px',
    borderRadius: '8px',
    cursor: 'pointer',
    transitionProperty: 'background-color',
    transitionDuration: '150ms',
    ':hover': { backgroundColor: 'color-mix(in srgb, var(--sst-botao-marca) 9%, var(--sst-color-surface))' },
    ':focus-visible': { outline: 'none', boxShadow: 'inset 0 0 0 2px var(--sst-botao-marca)' },
  },
  nome: {
    fontSize: '13px',
    fontWeight: 700,
    color: designTokens.colorNeutralDark,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  valor: {
    fontSize: '14px',
    fontWeight: 800,
    textAlign: 'right',
    fontVariantNumeric: 'tabular-nums',
    gridRow: '1 / span 2',
    gridColumn: 2,
  },
  trilho: {
    height: '8px',
    borderRadius: '99px',
    backgroundColor: designTokens.colorCardBorder,
    overflow: 'hidden',
  },
  preenchimento: { height: '100%', borderRadius: '99px' },
  detalhe: {
    fontSize: '12px',
    color: designTokens.colorNeutralMedium,
    fontWeight: 600,
    fontVariantNumeric: 'tabular-nums',
  },
});

const texto = (rotulo: string, valor: number | null) => `${rotulo} ${valor === null ? '—' : `${valor}%`}`;

// O pior indicador vai primeiro e em destaque: a média sozinha esconde o problema (uma obra com ASO em 50% e
// o resto em 100% parece "ok" na média).
function indicadoresDaObra(obra: ConformidadeObra) {
  const todos = [
    { chave: 'EPI', valor: obra.epi },
    { chave: 'Trein.', valor: obra.treinamentos },
    { chave: 'ASO', valor: obra.aso },
  ];
  const disponiveis = todos.filter((i) => i.valor !== null);
  const pior = disponiveis.length > 0 ? disponiveis.reduce((a, b) => ((b.valor as number) < (a.valor as number) ? b : a)) : null;
  return { pior, demais: todos.filter((i) => i !== pior) };
}

// "Obras em dia" (28/10/2026, antes "Conformidade por obra"): média de EPI, treinamentos e ASO em dia.
// Nome positivo e ordem da obra mais em dia para a menos, por decisão do usuário; o pior indicador
// de cada obra continua aparecendo embaixo dela. Cada obra é clicável.
export function ConformidadePorObraCard({ linhas, aoAbrirObra }: ConformidadePorObraCardProps) {
  const estilos = useStyles();
  const paleta = usePaletaGraficos();
  const corDaFaixa = (pct: number) => (pct >= 85 ? paleta.ok : pct >= 70 ? paleta.atencao : paleta.alerta);

  return (
    <Card
      titulo={
        <>
          Obras em dia <RotuloEscopo tipo="hoje" />
        </>
      }
      subtitulo="Quanto cada obra está em dia com EPI, treinamentos e ASO"
    >
      {linhas.length === 0 ? (
        <Legenda>Sem dados suficientes para calcular a conformidade das obras.</Legenda>
      ) : (
        <div className={estilos.lista}>
          {linhas.map((obra) => (
            <div
              key={obra.id}
              className={estilos.linha}
              role="link"
              tabIndex={0}
              aria-label={`${obra.nome}: ${obra.geral}% em dia`}
              onClick={() => aoAbrirObra?.(obra)}
              onKeyDown={(evento) => {
                if (evento.key === 'Enter' || evento.key === ' ') {
                  evento.preventDefault();
                  aoAbrirObra?.(obra);
                }
              }}
            >
              <div className={estilos.nome} title={obra.nome}>
                {obra.nome}
              </div>
              <div className={estilos.valor} style={{ color: corDaFaixa(obra.geral) }}>
                {obra.geral}%
              </div>
              <div>
                <div
                  className={estilos.trilho}
                  role="img"
                  aria-label={`${obra.nome}: ${obra.geral}% em dia`}
                >
                  <div
                    className={estilos.preenchimento}
                    style={{ width: `${obra.geral}%`, backgroundColor: corDaFaixa(obra.geral) }}
                  />
                </div>
                <div className={estilos.detalhe} style={{ marginTop: 4 }}>
                  {(() => {
                    const { pior, demais } = indicadoresDaObra(obra);
                    return (
                      <>
                        {pior && (
                          <>
                            Mais baixo:{' '}
                            <strong style={{ color: paleta.alerta }}>{texto(pior.chave, pior.valor)}</strong>
                            {demais.length > 0 ? ' · ' : ''}
                          </>
                        )}
                        {demais.map((i) => texto(i.chave, i.valor)).join(' · ')}
                      </>
                    );
                  })()}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </Card>
  );
}
