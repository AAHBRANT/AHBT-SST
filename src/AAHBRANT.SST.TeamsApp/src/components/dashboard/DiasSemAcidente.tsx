import { useEffect, useMemo, useRef, useState } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { TipoOcorrencia, type Acidente, type Obra } from '../../lib/api';
import { designTokens } from '@ui';
import { RotuloEscopo } from './RotuloEscopo';

// Contador "Sem acidente" do card de Ocorrências (Início, 28/10/2026): folha de calendário de
// geladeira que vira para cima (ontem → hoje). Conta SÓ ocorrências do tipo Acidente — incidente,
// quase-acidente e os demais tipos não zeram (decisão do usuário). Com "Todas as obras" mostra a
// obra com menos dias (a que mais precisa de atenção) e as outras em chips.

const DIA_MS = 86_400_000;

// Início da contagem (decisão do usuário, 28/10/2026): a contagem de dias sem acidente começa no
// dia em que o contador entrou no sistema, para todas as obras. Acidentes anteriores a esta data
// não entram (nem no recorde); um acidente depois dela recomeça a contagem a partir dele.
const INICIO_CONTAGEM = '2026-10-28';

const useStyles = makeStyles({
  caixa: {
    position: 'relative',
    overflow: 'hidden',
    display: 'grid',
    gridTemplateColumns: 'auto minmax(0, 1fr)',
    columnGap: '16px',
    rowGap: '6px',
    alignItems: 'center',
    padding: '14px 16px',
    borderRadius: '10px',
    backgroundImage:
      'linear-gradient(140deg, color-mix(in srgb, #10b981 16%, var(--sst-color-surface)), color-mix(in srgb, #059669 8%, var(--sst-color-surface)))',
    border: '1px solid color-mix(in srgb, #10b981 38%, transparent)',
  },
  flip: { position: 'relative', width: '92px', height: '106px', perspective: '520px', gridRow: 'span 3' },
  argolas: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    display: 'flex',
    justifyContent: 'space-between',
    padding: '0 20px',
    zIndex: 4,
    pointerEvents: 'none',
  },
  argola: {
    width: '7px',
    height: '15px',
    borderRadius: '4px',
    backgroundImage: 'linear-gradient(180deg, #d7d2cc, #8f8780)',
    boxShadow: 'inset 0 -1px 0 rgba(0,0,0,0.25)',
  },
  folha: {
    position: 'absolute',
    left: 0,
    right: 0,
    top: '8px',
    bottom: 0,
    borderRadius: '8px',
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    overflow: 'hidden',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    transformOrigin: '50% 0',
    backfaceVisibility: 'hidden',
  },
  folhaBase: {
    boxShadow: `0 3px 0 -1px ${designTokens.colorSurface}, 0 3px 0 0 ${designTokens.colorCardBorder}, 0 6px 0 -2px ${designTokens.colorSurface}, 0 6px 0 -1px ${designTokens.colorCardBorder}, 0 10px 16px rgba(0,0,0,0.10)`,
  },
  folhaVirando: {
    zIndex: 3,
    boxShadow: '0 2px 6px rgba(0,0,0,0.12)',
    animationName: { from: { transform: 'rotateX(0deg)' }, to: { transform: 'rotateX(-178deg)' } },
    animationDuration: '0.95s',
    animationTimingFunction: 'cubic-bezier(.55,.05,.35,1)',
    animationDelay: '0.45s',
    animationFillMode: 'forwards',
    '@media (prefers-reduced-motion: reduce)': { display: 'none' },
  },
  faixa: {
    width: '100%',
    backgroundColor: 'var(--sst-botao-marca)',
    color: '#ffffff',
    fontSize: '9px',
    fontWeight: 700,
    letterSpacing: '0.14em',
    textAlign: 'center',
    padding: '9px 0 4px',
  },
  faixaZero: { backgroundColor: designTokens.colorAlert },
  numero: {
    fontSize: '46px',
    fontWeight: 800,
    lineHeight: 1,
    marginTop: '5px',
    color: designTokens.colorNeutralDark,
    fontVariantNumeric: 'tabular-nums',
  },
  numeroZero: { color: designTokens.colorAlert },
  unidade: { fontSize: '10px', color: designTokens.colorNeutralMedium, textTransform: 'uppercase', letterSpacing: '0.1em' },
  titulo: { fontWeight: 700, fontSize: '14px', color: designTokens.colorNeutralDark },
  meta: { fontSize: '12px', color: designTokens.colorNeutralMedium },
  trilho: {
    height: '5px',
    borderRadius: '3px',
    backgroundColor: 'color-mix(in srgb, #10b981 18%, transparent)',
    overflow: 'hidden',
    marginTop: '6px',
  },
  trilhoBarra: { height: '100%', borderRadius: '3px', backgroundColor: 'var(--sst-botao-marca)', transitionProperty: 'width', transitionDuration: '1s' },
  selo: {
    justifySelf: 'start',
    display: 'inline-flex',
    alignItems: 'center',
    gap: '6px',
    fontSize: '12px',
    fontWeight: 700,
    color: '#ffffff',
    backgroundColor: 'var(--sst-botao-marca)',
    borderRadius: '99px',
    padding: '3px 10px',
    animationName: { from: { transform: 'scale(0.4)', opacity: 0 }, to: { transform: 'scale(1)', opacity: 1 } },
    animationDuration: '0.6s',
    animationTimingFunction: 'cubic-bezier(.3,1.6,.5,1)',
  },
  outras: { gridColumn: '1 / -1', display: 'flex', flexWrap: 'wrap', gap: '6px', marginTop: '4px' },
  outra: {
    fontSize: '11.5px',
    padding: '3px 9px',
    borderRadius: '10px',
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    color: designTokens.colorNeutralDark,
    fontVariantNumeric: 'tabular-nums',
  },
  confete: {
    position: 'absolute',
    top: '-8px',
    width: '7px',
    height: '11px',
    borderRadius: '2px',
    pointerEvents: 'none',
    opacity: 0.9,
    animationName: {
      from: { transform: 'translateY(0) rotate(0deg)', opacity: 0.9 },
      to: { transform: 'translateY(190px) rotate(540deg)', opacity: 0 },
    },
    animationDuration: '1.8s',
    animationTimingFunction: 'ease-in',
    animationFillMode: 'forwards',
    '@media (prefers-reduced-motion: reduce)': { display: 'none' },
  },
});

const CORES_CONFETE = ['#16a34a', '#f59e0b', '#3b82f6', '#ef4444', '#a855f7', '#ebe9ad'];

interface Contagem {
  obraId: string;
  obraNome: string;
  dias: number;
  recorde: number;
  /** Maior intervalo ANTES da sequência atual (para saber se ela já é um recorde novo). */
  recordeAnterior: number;
  ultimo: string | null;
  /** Houve Acidente hoje: só nesse caso o calendário fica vermelho. */
  acidenteHoje: boolean;
}

/** Dia (YYYY-MM-DD) em milissegundos UTC, para contar dias corridos sem erro de fuso. */
const diaMs = (iso: string) => Date.parse(iso.slice(0, 10) + 'T00:00:00Z');

function hojeMs() {
  const h = new Date();
  return Date.UTC(h.getFullYear(), h.getMonth(), h.getDate());
}

// Dias desde o último Acidente da obra (ou desde o início da contagem) e o recorde: maior intervalo
// entre dois acidentes desde o início da contagem, incluindo a sequência atual.
function contarDiasSemAcidente(acidentes: Acidente[], obras: Obra[]): Contagem[] {
  const hoje = hojeMs();
  const inicio = diaMs(INICIO_CONTAGEM);
  const resultado: Contagem[] = [];
  for (const obra of obras) {
    const datas = acidentes
      .filter((a) => a.obraId === obra.id && a.tipo === TipoOcorrencia.Acidente)
      .map((a) => diaMs(a.data))
      .filter((d) => d >= inicio)
      .sort((a, b) => a - b);
    const pontos = [inicio, ...datas];
    const ultimoPonto = pontos[pontos.length - 1];
    const dias = Math.max(0, Math.round((hoje - ultimoPonto) / DIA_MS));
    let recordeAnterior = 0;
    for (let i = 1; i < pontos.length; i += 1) recordeAnterior = Math.max(recordeAnterior, Math.round((pontos[i] - pontos[i - 1]) / DIA_MS));
    const recorde = Math.max(dias, recordeAnterior);
    const ultimo = datas.length > 0 ? new Date(datas[datas.length - 1]).toLocaleDateString('pt-BR', { timeZone: 'UTC' }) : null;
    const acidenteHoje = datas.length > 0 && datas[datas.length - 1] === hoje;
    resultado.push({ obraId: obra.id, obraNome: obra.nome, dias, recorde, recordeAnterior, ultimo, acidenteHoje });
  }
  return resultado;
}

interface DiasSemAcidenteProps {
  acidentes: Acidente[];
  /** Obras consideradas (as ativas do escopo atual). */
  obras: Obra[];
  /** true quando uma obra está filtrada: mostra só ela, sem os chips das outras. */
  obraFiltrada: boolean;
}

export function DiasSemAcidente({ acidentes, obras, obraFiltrada }: DiasSemAcidenteProps) {
  const e = useStyles();
  const contagens = useMemo(() => contarDiasSemAcidente(acidentes, obras), [acidentes, obras]);
  const principal = contagens.length > 0 ? contagens.reduce((a, b) => (b.dias < a.dias ? b : a)) : null;
  const dias = principal?.dias ?? null;

  // Folha que vira: na primeira vez, de ontem (dias-1) para hoje; depois, do número anterior.
  const anterior = useRef<number | null>(null);
  const [virada, setVirada] = useState<{ de: number; chave: number } | null>(null);
  useEffect(() => {
    if (dias === null) return;
    const de = anterior.current === null ? Math.max(0, dias - 1) : anterior.current;
    anterior.current = dias;
    if (de !== dias) setVirada({ de, chave: Date.now() });
  }, [dias]);

  if (!principal || dias === null) {
    return (
      <div className={e.caixa}>
        <div className={e.titulo} style={{ gridColumn: '1 / -1' }}>
          Sem acidente registrado nas obras
        </div>
      </div>
    );
  }

  // Vermelho só no dia de um acidente; o 0 do primeiro dia da contagem fica verde.
  const zero = principal.acidenteHoje;
  // Recorde novo só quando já houve acidente antes (há uma marca a bater) e a sequência atual passou dela.
  const novoRecorde = principal.ultimo !== null && principal.recordeAnterior > 0 && dias > principal.recordeAnterior;
  const marco = novoRecorde ? 'Novo recorde da obra! 🎉' : dias > 0 && dias % 30 === 0 ? `${dias} dias sem acidente! 🎉` : null;
  const pct = principal.recorde > 0 ? Math.min(100, (dias / principal.recorde) * 100) : 0;
  const outras = obraFiltrada ? [] : contagens.filter((c) => c.obraId !== principal.obraId);
  const folha = (n: number, classe: string, chave?: number) => (
    <div key={chave} className={mergeClasses(e.folha, classe)}>
      <span className={mergeClasses(e.faixa, zero && e.faixaZero)}>SEM ACIDENTE</span>
      <b className={mergeClasses(e.numero, zero && e.numeroZero)}>{n}</b>
      <small className={e.unidade}>{n === 1 ? 'dia' : 'dias'}</small>
    </div>
  );

  return (
    <div className={e.caixa}>
      {marco &&
        CORES_CONFETE.flatMap((cor, i) => [0, 1, 2, 3].map((j) => (
          <i
            key={`${i}-${j}`}
            className={e.confete}
            style={{ left: `${((i * 4 + j) * 37) % 100}%`, backgroundColor: cor, animationDelay: `${((i + j) % 9) * 70}ms` }}
          />
        )))}
      <div className={e.flip} role="img" aria-label={`${dias} dias sem acidente`}>
        <div className={e.argolas}>
          <i className={e.argola} />
          <i className={e.argola} />
        </div>
        {folha(dias, e.folhaBase)}
        {virada && virada.de !== dias && folha(virada.de, e.folhaVirando, virada.chave)}
      </div>
      <div className={e.titulo}>
        Sem acidente{obraFiltrada ? ' na obra' : ` — ${principal.obraNome}`} <RotuloEscopo tipo="hoje" />
      </div>
      <div>
        <div className={e.meta}>
          Conta só ocorrências do tipo Acidente
          {principal.ultimo
            ? ` · último em ${principal.ultimo}`
            : ` · contagem iniciada em ${new Date(diaMs(INICIO_CONTAGEM)).toLocaleDateString('pt-BR', { timeZone: 'UTC' })}`}
          {principal.recorde > 0 ? ` · recorde da obra: ${principal.recorde} dias` : ''}
        </div>
        <div className={e.trilho} title={`${Math.round(pct)}% do recorde`}>
          <div className={e.trilhoBarra} style={{ width: `${pct}%` }} />
        </div>
      </div>
      {marco && <span className={e.selo}>{marco}</span>}
      {outras.length > 0 && (
        <div className={e.outras}>
          {outras.map((c) => (
            <span key={c.obraId} className={e.outra}>
              {c.obraNome}: <b>{c.dias}</b> dias
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
