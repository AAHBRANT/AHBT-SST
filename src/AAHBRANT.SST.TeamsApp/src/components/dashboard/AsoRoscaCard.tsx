import { useEffect, useState } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { Card, designTokens } from '@ui';
import { RotuloEscopo } from './RotuloEscopo';

// Card de ASO do Início — "Rosca" escolhida pelo usuário em 28/10/2026: % de aptos no centro e a
// lista ordenada do mais urgente para o menos, cada linha com quantidade, % e barrinha. Passar o
// mouse numa linha destaca a fatia. Linguagem positiva ("a renovar"), sem "vencido".

export interface FatiaAso {
  chave: string;
  rotulo: string;
  valor: number;
  cor: string;
}

interface AsoRoscaCardProps {
  /** Fatias já na ordem da lista (mais urgente primeiro). A de chave `aptos` vai para o centro. */
  fatias: FatiaAso[];
  /** Ordem das fatias na rosca (normalmente aptos primeiro). */
  ordemNaRosca: string[];
}

const useStyles = makeStyles({
  // Rosca em cima e lista embaixo, ocupando a largura do card: com 4 das 12 colunas, lado a lado
  // a legenda quebrava palavra por palavra.
  grade: {
    display: 'grid',
    gridTemplateColumns: '1fr',
    justifyItems: 'center',
    gap: '16px',
  },
  segmento: { transitionProperty: 'stroke-dasharray, opacity, stroke-width', transitionDuration: '1s, 0.2s, 0.2s' },
  apagado: { opacity: 0.22 },
  realce: { strokeWidth: '22' },
  pct: { fontSize: '34px', fontWeight: 800, fill: designTokens.colorNeutralDark },
  sub: { fontSize: '11px', fill: designTokens.colorNeutralMedium },
  lista: { display: 'grid', gap: '4px', margin: 0, padding: 0, listStyle: 'none', width: '100%' },
  linha: {
    display: 'grid',
    gridTemplateColumns: '10px minmax(0, 1fr) 34px 40px',
    columnGap: '8px',
    rowGap: '3px',
    alignItems: 'center',
    fontSize: '12.5px',
    padding: '5px 6px',
    borderRadius: '6px',
    color: designTokens.colorNeutralDark,
    ':hover': { backgroundColor: designTokens.colorNeutralLight },
  },
  ponto: { width: '10px', height: '10px', borderRadius: '50%' },
  valor: { textAlign: 'right', fontWeight: 700, fontVariantNumeric: 'tabular-nums' },
  pctLinha: { textAlign: 'right', color: designTokens.colorNeutralMedium, fontSize: '11.5px', fontVariantNumeric: 'tabular-nums' },
  trilho: { gridColumn: '2 / -1', height: '4px', borderRadius: '2px', backgroundColor: designTokens.colorNeutralLight, overflow: 'hidden' },
  barra: { height: '100%', transitionProperty: 'width', transitionDuration: '0.9s' },
});

export function AsoRoscaCard({ fatias, ordemNaRosca }: AsoRoscaCardProps) {
  const e = useStyles();
  const [realce, setRealce] = useState<string | null>(null);
  const [animado, setAnimado] = useState(false);
  useEffect(() => {
    const id = requestAnimationFrame(() => setAnimado(true));
    return () => cancelAnimationFrame(id);
  }, []);

  const total = fatias.reduce((s, f) => s + f.valor, 0);
  const pct = (v: number) => (total > 0 ? Math.round((v / total) * 100) : 0);
  const aptos = fatias.find((f) => f.chave === 'aptos')?.valor ?? 0;
  const R = 62;
  const C = 2 * Math.PI * R;
  const folga = 3;
  const naRosca = ordemNaRosca.map((c) => fatias.find((f) => f.chave === c)).filter((f): f is FatiaAso => !!f);
  // Comprimento e início de cada fatia no círculo, calculados antes do JSX.
  const segmentos = naRosca.map((f, i) => {
    const comprimento = total > 0 ? (f.valor / total) * C : 0;
    const inicio = naRosca.slice(0, i).reduce((soma, g) => soma + (total > 0 ? (g.valor / total) * C : 0), 0);
    return { f, comprimento, deslocamento: -inicio };
  });

  return (
    <Card
      titulo={
        <>
          Status de aptidão ocupacional (ASO) <RotuloEscopo tipo="hoje" />
        </>
      }
      subtitulo="ASO mais recente de cada funcionário, já considerando a validade"
    >
      <div className={e.grade}>
        <svg viewBox="0 0 168 168" width="168" height="168" role="img" aria-label={`${pct(aptos)}% aptos de ${total} funcionários`}>
          <circle cx="84" cy="84" r={R} fill="none" stroke={designTokens.colorNeutralLight} strokeWidth="18" />
          {segmentos.map(({ f, comprimento, deslocamento }) => {
            if (f.valor === 0) return null;
            return (
              <circle
                key={f.chave}
                className={mergeClasses(e.segmento, realce !== null && realce !== f.chave && e.apagado, realce === f.chave && e.realce)}
                cx="84"
                cy="84"
                r={R}
                fill="none"
                stroke={f.cor}
                strokeWidth="18"
                strokeDasharray={animado ? `${Math.max(0, comprimento - folga)} ${C}` : `0 ${C}`}
                strokeDashoffset={deslocamento}
                transform="rotate(-90 84 84)"
              >
                <title>{`${f.rotulo}: ${f.valor}`}</title>
              </circle>
            );
          })}
          <text className={e.pct} x="84" y="86" textAnchor="middle">
            {pct(aptos)}%
          </text>
          <text className={e.sub} x="84" y="104" textAnchor="middle">
            aptos de {total}
          </text>
        </svg>
        <ul className={e.lista}>
          {fatias.map((f) => (
            <li
              key={f.chave}
              className={e.linha}
              onMouseEnter={() => setRealce(f.chave)}
              onMouseLeave={() => setRealce(null)}
            >
              <i className={e.ponto} style={{ backgroundColor: f.cor }} />
              <span>{f.rotulo}</span>
              <b className={e.valor}>{f.valor}</b>
              <span className={e.pctLinha}>{pct(f.valor)}%</span>
              <div className={e.trilho}>
                <div className={e.barra} style={{ width: animado ? `${pct(f.valor)}%` : 0, backgroundColor: f.cor }} />
              </div>
            </li>
          ))}
        </ul>
      </div>
    </Card>
  );
}
