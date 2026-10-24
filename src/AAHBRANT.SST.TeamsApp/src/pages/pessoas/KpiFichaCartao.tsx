import { useEffect, useState, type MouseEvent, type ReactElement } from 'react';
import { motion, useReducedMotion } from 'framer-motion';
import { designTokens, escalonado, tokensUi, type Tom } from '@ui';

// Gradiente do card em destaque (visual Cristal Executivo, escolhido pelo usuário em 21/10). Fixo nos
// dois temas: o texto por cima é sempre branco, então o vinho não pode clarear no tema escuro.
const GRADIENTE_DESTAQUE = 'linear-gradient(120deg, #670000 0%, #b8322b 55%, #e0785f 100%)';
// Borda de luz que gira em volta do card no hover — mesma faixa coral→vinho do protótipo.
const GRADIENTE_BORDA = 'conic-gradient(from 0deg, transparent 0 70%, #e0785f 82%, #670000 92%, transparent)';
const DURACAO_CONTAGEM_MS = 800;

// Separa o número inicial do resto ("78%" → 78 e "%") para a contagem animada; valores sem número
// no começo ("—") aparecem direto.
function separarValor(valor: string): { numero: number | null; sufixo: string } {
  const m = valor.match(/^(\d+)(.*)$/);
  return m ? { numero: Number(m[1]), sufixo: m[2] } : { numero: null, sufixo: valor };
}

function useContagem(alvo: number | null, desligada: boolean): number | null {
  const [atual, setAtual] = useState(0);
  const semAnimacao = alvo === null || desligada || alvo === 0;
  useEffect(() => {
    if (semAnimacao || alvo === null) return;
    let quadro = 0;
    const inicio = performance.now();
    const passo = (agora: number) => {
      const k = Math.min(1, (agora - inicio) / DURACAO_CONTAGEM_MS);
      setAtual(Math.round(alvo * (1 - Math.pow(1 - k, 4))));
      if (k < 1) quadro = requestAnimationFrame(passo);
    };
    quadro = requestAnimationFrame(passo);
    return () => cancelAnimationFrame(quadro);
  }, [alvo, semAnimacao]);
  return semAnimacao ? alvo : atual;
}

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

// Indicador da ficha do trabalhador no formato e na animação aprovados no protótipo (21/10): rótulo e
// ícone em cima, número grande que conta até o valor, linha de detalhe embaixo, luz que acompanha o
// cursor, borda de luz girando no hover e o primeiro card em gradiente. Fica em pages/pessoas porque só
// a ficha usa este formato; o KpiCard de @ui continua igual nas outras telas.
export function KpiFichaCartao({ rotulo, valor, detalhe, tom, tomDetalhe = 'neutro', icone, indice = 0, destaque = false }: KpiFichaCartaoProps) {
  const reduzirMovimento = useReducedMotion() ?? false;
  const [sobre, setSobre] = useState(false);
  const [luz, setLuz] = useState<{ x: number; y: number } | null>(null);
  const { numero, sufixo } = separarValor(valor);
  const contado = useContagem(numero, reduzirMovimento);

  const corTexto = destaque ? designTokens.colorWhite : undefined;
  const corSuave = destaque ? 'rgba(255, 255, 255, 0.82)' : designTokens.colorNeutralMedium;
  const corLuz = destaque ? 'rgba(255, 255, 255, 0.18)' : 'color-mix(in srgb, var(--sst-color-primary) 8%, transparent)';

  function moverLuz(e: MouseEvent<HTMLDivElement>) {
    if (reduzirMovimento) return;
    const r = e.currentTarget.getBoundingClientRect();
    setLuz({ x: e.clientX - r.left, y: e.clientY - r.top });
  }

  return (
    <motion.div
      variants={escalonado(indice)}
      initial="inicial"
      animate="visivel"
      onMouseEnter={() => setSobre(true)}
      onMouseLeave={() => {
        setSobre(false);
        setLuz(null);
      }}
      onMouseMove={moverLuz}
      style={{
        position: 'relative',
        overflow: 'hidden',
        borderRadius: tokensUi.raio.lg,
        // A borda é o próprio fundo do contêiner aparecendo 1px em volta do miolo — é por ela que a
        // luz girando passa.
        padding: 1,
        background: destaque ? 'transparent' : designTokens.colorCardBorder,
        boxShadow: destaque
          ? '0 20px 40px -22px rgba(163, 20, 15, 0.75)'
          : sobre
            ? '0 22px 44px -26px rgba(20, 24, 40, 0.45)'
            : designTokens.cardShadow,
        transform: sobre && !reduzirMovimento ? 'translateY(-3px)' : 'none',
        transition: `transform ${tokensUi.duracao.entrada} ${tokensUi.curva}, box-shadow ${tokensUi.duracao.entrada} ${tokensUi.curva}`,
        minWidth: 0,
      }}
    >
      {!destaque && sobre && !reduzirMovimento && (
        <motion.span
          aria-hidden="true"
          initial={{ opacity: 0, rotate: 0 }}
          animate={{ opacity: 1, rotate: 360 }}
          transition={{ opacity: { duration: 0.4 }, rotate: { duration: 3, ease: 'linear', repeat: Infinity } }}
          style={{
            position: 'absolute',
            left: '50%',
            top: '50%',
            width: '250%',
            aspectRatio: '1',
            marginLeft: '-125%',
            marginTop: '-125%',
            background: GRADIENTE_BORDA,
            pointerEvents: 'none',
          }}
        />
      )}

      <div
        style={{
          position: 'relative',
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
          height: '100%',
          padding: '17px 17px 15px',
          borderRadius: `calc(${tokensUi.raio.lg} - 1px)`,
          background: destaque ? GRADIENTE_DESTAQUE : designTokens.colorSurface,
          color: corTexto,
          overflow: 'hidden',
        }}
      >
        <span
          aria-hidden="true"
          style={{
            position: 'absolute',
            inset: 0,
            pointerEvents: 'none',
            opacity: luz ? 1 : 0,
            transition: `opacity ${tokensUi.duracao.entrada}`,
            background: luz ? `radial-gradient(360px circle at ${luz.x}px ${luz.y}px, ${corLuz}, transparent 45%)` : 'none',
          }}
        />
        <div style={{ position: 'relative', display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: tokensUi.espaco.sm }}>
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
        <div
          aria-label={valor}
          style={{ position: 'relative', fontSize: 34, lineHeight: 1.05, fontWeight: 700, letterSpacing: '-0.02em', fontVariantNumeric: 'tabular-nums' }}
        >
          {numero === null ? valor : `${contado ?? numero}${sufixo}`}
        </div>
        {detalhe && (
          <div
            style={{
              position: 'relative',
              fontSize: 12,
              fontWeight: tomDetalhe === 'neutro' ? 400 : 600,
              color: destaque || tomDetalhe === 'neutro' ? corSuave : tokensUi.status[tomDetalhe].tinta,
            }}
          >
            {detalhe}
          </div>
        )}
      </div>
    </motion.div>
  );
}
