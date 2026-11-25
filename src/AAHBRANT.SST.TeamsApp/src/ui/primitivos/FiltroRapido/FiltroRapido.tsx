import { useId } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { motion } from 'framer-motion';
import { molas } from '../../tokens/movimento';
import { designTokens, tokensUi } from '../../tokens/tokens';

// Filtro rápido de lista: opções em pílula com contagem; a opção ativa ganha um fundo que desliza
// com mola (molas.assentar). Padrão aprovado em 25/11/2026 (docs/mockups/listas-gsst-2026-11-25.html).
const useEstilos = makeStyles({
  grupo: {
    display: 'inline-flex', flexWrap: 'wrap', gap: '2px', padding: '3px', borderRadius: tokensUi.raio.full,
    backgroundColor: tokensUi.status.neutro.fundo,
  },
  opcao: {
    position: 'relative', border: 'none', background: 'none', cursor: 'pointer', borderRadius: tokensUi.raio.full,
    padding: '6px 13px', fontFamily: 'inherit', fontSize: '12.5px', fontWeight: 500, color: designTokens.colorNeutralMedium,
    display: 'inline-flex', alignItems: 'center', gap: '6px',
    ':focus-visible': { outline: `2px solid ${tokensUi.chrome.ativoFundo}`, outlineOffset: '1px' },
  },
  ativa: { color: designTokens.colorNeutralDark },
  fundoAtivo: {
    position: 'absolute', inset: 0, borderRadius: tokensUi.raio.full, backgroundColor: designTokens.colorSurface,
    boxShadow: `0 1px 3px rgba(0,0,0,.08), 0 0 0 1px ${tokensUi.bordaSuave}`,
  },
  texto: { position: 'relative' },
  contagem: { position: 'relative', fontSize: '11px', fontVariantNumeric: 'tabular-nums', color: designTokens.colorNeutralMedium },
});

export interface OpcaoFiltro<V extends string> {
  valor: V;
  rotulo: string;
  contagem?: number;
}

export interface FiltroRapidoProps<V extends string> {
  opcoes: OpcaoFiltro<V>[];
  valor: V;
  aoMudar: (valor: V) => void;
  'aria-label': string;
}

export function FiltroRapido<V extends string>({ opcoes, valor, aoMudar, 'aria-label': ariaLabel }: FiltroRapidoProps<V>) {
  const e = useEstilos();
  const id = useId();
  return (
    <div role="group" aria-label={ariaLabel} className={e.grupo}>
      {opcoes.map((o) => {
        const ativa = o.valor === valor;
        return (
          <button key={o.valor} type="button" aria-pressed={ativa} className={mergeClasses(e.opcao, ativa && e.ativa)} onClick={() => aoMudar(o.valor)}>
            {ativa && <motion.span layoutId={`filtro-${id}`} className={e.fundoAtivo} transition={molas.assentar} />}
            <span className={e.texto}>{o.rotulo}</span>
            {o.contagem !== undefined && <span className={e.contagem}>{o.contagem}</span>}
          </button>
        );
      })}
    </div>
  );
}
