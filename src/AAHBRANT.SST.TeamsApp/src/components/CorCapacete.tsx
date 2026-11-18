import { useId, type KeyboardEvent } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { Checkmark12Filled, Info16Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi, useTipografia } from '@ui';
import {
  CORES_CAPACETE_OPCOES,
  corCapaceteHex,
  corCapaceteNome,
  normalizarCorCapacete,
} from '../lib/corCapacete';

// Peças da cor do capacete (mockup 2026-11-14): a amostra desenhada, a pílula "capacete + nome" e o
// seletor de cores. A cor real do capacete só aparece na amostra; o nome vem sempre escrito ao lado,
// então ninguém depende de distinguir cor. O contorno usa o cinza médio do tema para o capacete branco
// (e o azul no tema escuro) continuarem visíveis nos dois temas.

const useStyles = makeStyles({
  capa: { flexShrink: 0, display: 'block' },
  vazioAmostra: {
    flexShrink: 0,
    borderRadius: '50%',
    border: `2px dashed ${designTokens.colorNeutralMedium}`,
    boxSizing: 'border-box',
  },
  pilula: { display: 'inline-flex', alignItems: 'center', gap: tokensUi.espaco.sm, fontWeight: 600, whiteSpace: 'nowrap' },
  pilulaSem: { color: designTokens.colorNeutralMedium, fontWeight: 500 },
  rotulo: { display: 'block', fontSize: '14px', lineHeight: '20px', fontWeight: 600, marginBottom: '6px' },
  grupo: {
    display: 'grid',
    gridTemplateColumns: 'repeat(4, minmax(0, 1fr))',
    gap: tokensUi.espaco.md,
    '@media (max-width: 700px)': { gridTemplateColumns: 'repeat(2, minmax(0, 1fr))' },
  },
  opcao: {
    position: 'relative',
    display: 'flex',
    alignItems: 'center',
    gap: tokensUi.espaco.md,
    minHeight: '56px',
    padding: `${tokensUi.espaco.sm} ${tokensUi.espaco.md}`,
    paddingRight: '34px',
    borderRadius: tokensUi.raio.md,
    border: `2px solid ${designTokens.colorCardBorder}`,
    backgroundColor: designTokens.colorSurface,
    color: designTokens.colorNeutralDark,
    fontFamily: 'inherit',
    fontSize: '14px',
    lineHeight: '18px',
    fontWeight: 700,
    textAlign: 'left',
    cursor: 'pointer',
    ':hover': { border: `2px solid ${designTokens.colorNeutralMedium}` },
    ':focus-visible': { outline: `2px solid ${designTokens.colorPrimary}`, outlineOffset: '2px' },
  },
  opcaoMarcada: {
    border: `2px solid ${designTokens.colorPrimary}`,
    backgroundColor: 'color-mix(in srgb, var(--sst-color-surface) 94%, var(--sst-color-primary))',
    ':hover': { border: `2px solid ${designTokens.colorPrimary}` },
  },
  marca: {
    position: 'absolute',
    right: '8px',
    top: '8px',
    width: '18px',
    height: '18px',
    borderRadius: '50%',
    backgroundColor: designTokens.colorPrimary,
    color: designTokens.colorWhite,
    display: 'grid',
    placeItems: 'center',
  },
  sugestao: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: tokensUi.espaco.sm,
    marginTop: tokensUi.espaco.md,
    padding: `${tokensUi.espaco.md} ${tokensUi.espaco.md}`,
    borderRadius: tokensUi.raio.sm,
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorNeutralDark,
  },
  usar: {
    background: 'none',
    border: 'none',
    padding: 0,
    fontFamily: 'inherit',
    fontSize: 'inherit',
    fontWeight: 700,
    color: tokensUi.status.ok.tinta,
    textDecoration: 'underline',
    cursor: 'pointer',
    minHeight: '24px',
    ':focus-visible': { outline: `2px solid ${designTokens.colorPrimary}`, outlineOffset: '2px' },
  },
});

export function IconeCapacete({ cor, largura = 24 }: { cor: number; largura?: number }) {
  const estilos = useStyles();
  const hex = corCapaceteHex[normalizarCorCapacete(cor)] ?? 'transparent';
  const altura = Math.round((largura * 26) / 34);
  const contorno = { stroke: designTokens.colorNeutralMedium, strokeWidth: 1.6, strokeLinejoin: 'round' as const };
  return (
    <svg className={estilos.capa} width={largura} height={altura} viewBox="0 0 34 26" aria-hidden="true" focusable="false">
      <path d="M4 20c0-8 5-15 13-15s13 7 13 15z" fill={hex} {...contorno} />
      <path d="M2 21h30v3H2z" fill={hex} {...contorno} />
      <path d="M17 5v9" stroke={designTokens.colorNeutralMedium} strokeOpacity={0.6} strokeWidth={1.4} />
    </svg>
  );
}

function AmostraSemPadrao({ tamanho }: { tamanho: number }) {
  const estilos = useStyles();
  return <span className={estilos.vazioAmostra} style={{ width: tamanho, height: tamanho }} aria-hidden="true" />;
}

// "Capacete colorido + nome da cor" (lista de Funções, quadro de conferência, avisos).
export function PilulaCorCapacete({ cor }: { cor: number | null | undefined }) {
  const estilos = useStyles();
  const c = normalizarCorCapacete(cor);
  return (
    <span className={mergeClasses(estilos.pilula, c === 0 && estilos.pilulaSem)}>
      {c === 0 ? <AmostraSemPadrao tamanho={18} /> : <IconeCapacete cor={c} largura={24} />}
      {corCapaceteNome[c]}
    </span>
  );
}

export interface SugestaoDoPadrao {
  cor: number;
  // Complemento depois do nome da cor, por exemplo "(execução em geral)".
  detalhe?: string;
}

interface SeletorCorCapaceteProps {
  valor: number;
  aoMudar: (cor: number) => void;
  // Rótulo visível acima das amostras (também nomeia o grupo para leitores de tela).
  rotulo?: string;
  // Nome da função em edição: vira o texto "Sugestão do padrão AAHBRANT para ...".
  nomeFuncao?: string;
  sugestao?: SugestaoDoPadrao | null;
}

// Grupo de opções (radio) com amostra e nome. Setas movem a seleção, como em qualquer radio nativo.
export function SeletorCorCapacete({ valor, aoMudar, rotulo, nomeFuncao, sugestao }: SeletorCorCapaceteProps) {
  const estilos = useStyles();
  const idRotulo = useId();
  const tipo = useTipografia();
  const atual = normalizarCorCapacete(valor);

  function aoTeclar(e: KeyboardEvent<HTMLButtonElement>) {
    const passo = e.key === 'ArrowRight' || e.key === 'ArrowDown' ? 1 : e.key === 'ArrowLeft' || e.key === 'ArrowUp' ? -1 : 0;
    if (passo === 0) return;
    e.preventDefault();
    const total = CORES_CAPACETE_OPCOES.length;
    const proxima = CORES_CAPACETE_OPCOES[(CORES_CAPACETE_OPCOES.indexOf(atual) + passo + total) % total];
    aoMudar(proxima);
    const botoes = e.currentTarget.parentElement?.querySelectorAll<HTMLButtonElement>('button[role="radio"]');
    botoes?.[proxima]?.focus();
  }

  return (
    <div>
      {rotulo && (
        <span id={idRotulo} className={estilos.rotulo}>
          {rotulo}
        </span>
      )}
      <div
        className={estilos.grupo}
        role="radiogroup"
        aria-labelledby={rotulo ? idRotulo : undefined}
        aria-label={rotulo ? undefined : 'Cor do capacete'}
      >
        {CORES_CAPACETE_OPCOES.map((c) => {
          const marcada = c === atual;
          return (
            <button
              key={c}
              type="button"
              role="radio"
              aria-checked={marcada}
              tabIndex={marcada ? 0 : -1}
              className={mergeClasses(estilos.opcao, marcada && estilos.opcaoMarcada)}
              onClick={() => aoMudar(c)}
              onKeyDown={aoTeclar}
            >
              {c === 0 ? <AmostraSemPadrao tamanho={26} /> : <IconeCapacete cor={c} largura={34} />}
              <span>{corCapaceteNome[c]}</span>
              {marcada && (
                <span className={estilos.marca} aria-hidden="true">
                  <Checkmark12Filled />
                </span>
              )}
            </button>
          );
        })}
      </div>
      {sugestao && sugestao.cor > 0 && (
        <div className={mergeClasses(tipo.corpo, estilos.sugestao)} role="status">
          <Info16Regular aria-hidden="true" />
          <span>
            {nomeFuncao ? `Sugestão do padrão AAHBRANT para "${nomeFuncao}": ` : 'Sugestão do padrão AAHBRANT: '}
            <strong>{corCapaceteNome[sugestao.cor]}</strong>
            {sugestao.detalhe ? ` (${sugestao.detalhe}).` : '.'}
          </span>
          {sugestao.cor !== atual && (
            <button type="button" className={estilos.usar} onClick={() => aoMudar(sugestao.cor)}>
              Usar a sugestão
            </button>
          )}
        </div>
      )}
    </div>
  );
}
