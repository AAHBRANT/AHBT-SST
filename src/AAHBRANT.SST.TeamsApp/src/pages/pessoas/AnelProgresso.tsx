import type { ReactNode } from 'react';
import { designTokens, tokensUi, type Tom } from '@ui';

export interface AnelProgressoProps {
  /** Fração preenchida, de 0 a 1. */
  fracao: number;
  tom: Tom;
  centro: ReactNode;
  legenda?: ReactNode;
  tamanho?: number;
  espessura?: number;
  'aria-label'?: string;
}

// Anel de progresso da ficha do trabalhador (Proposta 1 de cards, 21/10): dias de validade do ASO e
// presença em DDS. É uma leitura de estado, não gráfico de série — por isso fica aqui e não em @ui/graficos.
export function AnelProgresso({ fracao, tom, centro, legenda, tamanho = 128, espessura = 11, 'aria-label': ariaLabel }: AnelProgressoProps) {
  const raio = (tamanho - espessura) / 2;
  const circunferencia = 2 * Math.PI * raio;
  const preenchido = Math.max(0, Math.min(1, fracao));
  return (
    <div role="img" aria-label={ariaLabel} style={{ position: 'relative', width: tamanho, height: tamanho, flexShrink: 0 }}>
      <svg width={tamanho} height={tamanho} viewBox={`0 0 ${tamanho} ${tamanho}`} style={{ transform: 'rotate(-90deg)' }} aria-hidden="true">
        <circle cx={tamanho / 2} cy={tamanho / 2} r={raio} fill="none" stroke={designTokens.colorNeutralLight} strokeWidth={espessura} />
        <circle
          cx={tamanho / 2}
          cy={tamanho / 2}
          r={raio}
          fill="none"
          stroke={tokensUi.status[tom].tinta}
          strokeWidth={espessura}
          strokeLinecap="round"
          strokeDasharray={circunferencia}
          strokeDashoffset={circunferencia * (1 - preenchido)}
          style={{ transition: `stroke-dashoffset ${tokensUi.duracao.entrada} ${tokensUi.curva}` }}
        />
      </svg>
      <div
        style={{
          position: 'absolute',
          inset: 0,
          display: 'grid',
          placeContent: 'center',
          textAlign: 'center',
          lineHeight: 1.1,
        }}
      >
        <span style={{ fontSize: 26, fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{centro}</span>
        {legenda && <span style={{ fontSize: 11, color: designTokens.colorNeutralMedium, marginTop: 3 }}>{legenda}</span>}
      </div>
    </div>
  );
}
