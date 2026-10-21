import { useState, type ReactElement } from 'react';
import { motion } from 'framer-motion';
import { designTokens, escalonado, tokensUi, type Tom } from '@ui';

// Gradiente do card em destaque (visual Cristal Executivo, escolhido pelo usuário em 21/10). Fixo nos
// dois temas: o texto por cima é sempre branco, então o vinho não pode clarear no tema escuro.
const GRADIENTE_DESTAQUE = 'linear-gradient(120deg, #670000 0%, #b8322b 55%, #e0785f 100%)';

export interface KpiFichaCartaoProps {
  rotulo: string;
  valor: string;
  detalhe?: string;
  /** Cor do ícone e, quando não for neutro, da linha de detalhe. */
  tom: Tom;
  tomDetalhe?: Tom;
  icone: ReactElement;
  indice?: number;
  destaque?: boolean;
}

// Indicador da ficha do trabalhador no formato aprovado no protótipo (21/10): rótulo e ícone em cima,
// número grande, linha de detalhe embaixo e o primeiro card em gradiente. Fica em pages/pessoas porque
// só a ficha usa este formato; o KpiCard de @ui continua igual nas outras 14 telas.
export function KpiFichaCartao({ rotulo, valor, detalhe, tom, tomDetalhe = 'neutro', icone, indice = 0, destaque = false }: KpiFichaCartaoProps) {
  const [sobre, setSobre] = useState(false);
  const corTexto = destaque ? designTokens.colorWhite : undefined;
  const corSuave = destaque ? 'rgba(255, 255, 255, 0.82)' : designTokens.colorNeutralMedium;
  return (
    <motion.div
      variants={escalonado(indice)}
      initial="inicial"
      animate="visivel"
      onMouseEnter={() => setSobre(true)}
      onMouseLeave={() => setSobre(false)}
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        padding: '18px 18px 16px',
        borderRadius: tokensUi.raio.lg,
        border: destaque ? '1px solid transparent' : `1px solid ${designTokens.colorCardBorder}`,
        background: destaque ? GRADIENTE_DESTAQUE : designTokens.colorSurface,
        color: corTexto,
        boxShadow: destaque
          ? '0 20px 40px -22px rgba(163, 20, 15, 0.75)'
          : sobre
            ? '0 22px 44px -26px rgba(20, 24, 40, 0.45)'
            : designTokens.cardShadow,
        transform: sobre ? 'translateY(-3px)' : 'none',
        transition: `transform ${tokensUi.duracao.entrada} ${tokensUi.curva}, box-shadow ${tokensUi.duracao.entrada} ${tokensUi.curva}`,
        minWidth: 0,
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: tokensUi.espaco.sm }}>
        <span style={{ fontSize: 12.5, fontWeight: 600, color: corSuave }}>{rotulo}</span>
        <span
          aria-hidden="true"
          style={{
            width: 32,
            height: 32,
            borderRadius: 9,
            display: 'grid',
            placeItems: 'center',
            flexShrink: 0,
            backgroundColor: destaque ? 'rgba(255, 255, 255, 0.18)' : tokensUi.status[tom].fundo,
            color: destaque ? designTokens.colorWhite : tokensUi.status[tom].tinta,
          }}
        >
          {icone}
        </span>
      </div>
      <div style={{ fontSize: 34, lineHeight: 1.05, fontWeight: 700, letterSpacing: '-0.02em', fontVariantNumeric: 'tabular-nums' }}>
        {valor}
      </div>
      {detalhe && (
        <div
          style={{
            fontSize: 12,
            fontWeight: tomDetalhe === 'neutro' ? 400 : 600,
            color: destaque || tomDetalhe === 'neutro' ? corSuave : tokensUi.status[tomDetalhe].tinta,
          }}
        >
          {detalhe}
        </div>
      )}
    </motion.div>
  );
}
