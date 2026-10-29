import type { KeyboardEvent, ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { Button, Card, designTokens } from '@ui';
import { RotuloEscopo } from './RotuloEscopo';

// Card de Ocorrências do Início (28/10/2026): substitui as barras empilhadas (o usuário achou que
// nem todo mundo entende) pela pirâmide de segurança. Acidentes no topo e, na base, os atos e as
// condições inseguras — quanto mais registro na base, mais cedo o risco é tratado. Cada nível é
// clicável e abre as ocorrências já filtradas naquele tipo. A lista "Últimos registros" saiu.

export interface NivelPiramide {
  tipo: number;
  rotulo: string;
  cor: string;
  total: number;
}

interface PiramideOcorrenciasCardProps {
  /** Níveis do topo (Acidentes) para a base (Condições inseguras). */
  niveis: NivelPiramide[];
  /** Tipos que ficam fora da pirâmide (ex.: doença ocupacional), mostrados na nota. */
  foraDaPiramide: NivelPiramide[];
  subtitulo: string;
  /** Bloco acima da pirâmide (contador de dias sem acidente). */
  topo?: ReactNode;
  aoAbrirTipo: (tipo: number) => void;
  aoAbrirOcorrencias: () => void;
}

const useStyles = makeStyles({
  corpo: { display: 'flex', flexDirection: 'column', gap: '14px' },
  total: { fontSize: '12.5px', color: designTokens.colorNeutralMedium },
  totalNumero: { fontSize: '20px', fontWeight: 800, color: designTokens.colorNeutralDark, fontVariantNumeric: 'tabular-nums' },
  piramide: {
    display: 'grid',
    gridTemplateColumns: 'minmax(0, 1.15fr) minmax(0, 1fr)',
    gap: '14px',
    alignItems: 'center',
    '@media (max-width: 620px)': { gridTemplateColumns: '1fr' },
  },
  faixas: { display: 'grid', gap: '3px' },
  faixa: {
    height: '38px',
    transformOrigin: 'center',
    animationName: { from: { transform: 'scaleX(0)' }, to: { transform: 'scaleX(1)' } },
    animationDuration: '0.7s',
    animationTimingFunction: 'cubic-bezier(.2,.8,.2,1)',
    animationFillMode: 'both',
  },
  rotulos: { display: 'grid', gap: '3px' },
  rotulo: {
    height: '38px',
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    padding: '0 8px',
    borderRadius: '6px',
    fontSize: '13px',
    color: designTokens.colorNeutralMedium,
    cursor: 'pointer',
    borderBottom: `1px dashed ${designTokens.colorCardBorder}`,
    transitionProperty: 'background-color',
    transitionDuration: '150ms',
    ':last-child': { borderBottom: 'none' },
    ':hover': { backgroundColor: 'color-mix(in srgb, var(--sst-botao-marca) 9%, var(--sst-color-surface))', color: designTokens.colorNeutralDark },
    ':focus-visible': { outline: '2px solid var(--sst-botao-marca)', outlineOffset: '-2px' },
  },
  rotuloNumero: { fontSize: '22px', fontWeight: 800, minWidth: '30px', textAlign: 'right', fontVariantNumeric: 'tabular-nums' },
  seta: { marginLeft: 'auto', color: 'var(--sst-acao-marca-tinta)', fontWeight: 700 },
  nota: { fontSize: '12px', color: designTokens.colorNeutralMedium, lineHeight: '18px' },
});

export function PiramideOcorrenciasCard({ niveis, foraDaPiramide, subtitulo, topo, aoAbrirTipo, aoAbrirOcorrencias }: PiramideOcorrenciasCardProps) {
  const e = useStyles();
  const total = [...niveis, ...foraDaPiramide].reduce((soma, n) => soma + n.total, 0);
  const qtd = niveis.length;
  const larguraTopo = (i: number) => 20 + (80 / qtd) * i;
  const abrirComTeclado = (evento: KeyboardEvent<HTMLDivElement>, tipo: number) => {
    if (evento.key === 'Enter' || evento.key === ' ') {
      evento.preventDefault();
      aoAbrirTipo(tipo);
    }
  };

  return (
    <Card
      titulo={
        <>
          Ocorrências <RotuloEscopo tipo="periodo" />
        </>
      }
      subtitulo={subtitulo}
      acoes={
        <Button appearance="subtle" size="small" onClick={aoAbrirOcorrencias}>
          Abrir ocorrências
        </Button>
      }
    >
      <div className={e.corpo}>
        {topo}
        <div className={e.total}>
          <span className={e.totalNumero}>{total}</span> ocorrências no período
        </div>
        <div className={e.piramide}>
          <div className={e.faixas} aria-hidden="true">
            {niveis.map((n, i) => {
              const t = larguraTopo(i);
              const b = larguraTopo(i + 1);
              return (
                <div
                  key={n.tipo}
                  className={e.faixa}
                  style={{
                    backgroundColor: n.cor,
                    clipPath: `polygon(${(100 - t) / 2}% 0, ${(100 + t) / 2}% 0, ${(100 + b) / 2}% 100%, ${(100 - b) / 2}% 100%)`,
                    animationDelay: `${(qtd - 1 - i) * 90}ms`,
                  }}
                />
              );
            })}
          </div>
          <div className={e.rotulos}>
            {niveis.map((n) => (
              <div
                key={n.tipo}
                className={e.rotulo}
                role="link"
                tabIndex={0}
                aria-label={`${n.total} ${n.rotulo}: abrir as ocorrências desse tipo`}
                onClick={() => aoAbrirTipo(n.tipo)}
                onKeyDown={(evento) => abrirComTeclado(evento, n.tipo)}
              >
                <b className={e.rotuloNumero} style={{ color: n.cor }}>
                  {n.total}
                </b>
                <span>{n.rotulo}</span>
                <span className={e.seta} aria-hidden="true">
                  ›
                </span>
              </div>
            ))}
          </div>
        </div>
        <div className={e.nota}>
          Quanto mais registros na base (atos e condições inseguras), mais cedo o risco é tratado, antes de virar
          acidente no topo.
          {foraDaPiramide.map((n) => ` ${n.rotulo} no período: ${n.total}.`)}
        </div>
      </div>
    </Card>
  );
}
