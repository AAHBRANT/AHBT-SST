import { makeStyles } from '@fluentui/react-components';
import { Card, Legenda, designTokens, usePaletaGraficos } from '@ui';

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
}

const useStyles = makeStyles({
  lista: { display: 'flex', flexDirection: 'column', gap: '14px' },
  linha: {
    display: 'grid',
    gridTemplateColumns: 'minmax(0, 1fr) 44px',
    columnGap: '12px',
    rowGap: '6px',
    alignItems: 'center',
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

// Conformidade por obra (pedido do usuário, 04/10): média de EPI, treinamentos e ASO em dia, com as
// obras mais atrasadas primeiro — o card existe para apontar onde agir, não para premiar a melhor.
export function ConformidadePorObraCard({ linhas }: ConformidadePorObraCardProps) {
  const estilos = useStyles();
  const paleta = usePaletaGraficos();
  const corDaFaixa = (pct: number) => (pct >= 85 ? paleta.ok : pct >= 70 ? paleta.atencao : paleta.alerta);

  return (
    <Card titulo="Conformidade por obra" subtitulo="Média de EPI, treinamentos e ASO em dia, da mais atrasada para a melhor">
      {linhas.length === 0 ? (
        <Legenda>Sem dados suficientes para calcular a conformidade das obras.</Legenda>
      ) : (
        <div className={estilos.lista}>
          {linhas.map((obra) => (
            <div key={obra.id} className={estilos.linha}>
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
                  aria-label={`${obra.nome}: ${obra.geral}% de conformidade`}
                >
                  <div
                    className={estilos.preenchimento}
                    style={{ width: `${obra.geral}%`, backgroundColor: corDaFaixa(obra.geral) }}
                  />
                </div>
                <div className={estilos.detalhe} style={{ marginTop: 4 }}>
                  {texto('EPI', obra.epi)} · {texto('Trein.', obra.treinamentos)} · {texto('ASO', obra.aso)}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </Card>
  );
}
