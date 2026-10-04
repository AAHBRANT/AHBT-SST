import { useEffect, useMemo, useState } from 'react';
import { Input, makeStyles } from '@fluentui/react-components';
import type { Acidente, RegistroHhtMensal } from '../../lib/api';
import { Card, Legenda, StatusChip, designTokens, usePaletaGraficos } from '@ui';
import { RotuloEscopo } from './RotuloEscopo';

const useStyles = makeStyles({
  destaque: { display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: '10px' },
  numero: {
    fontSize: '38px',
    lineHeight: '40px',
    fontWeight: 800,
    letterSpacing: '-0.02em',
    fontVariantNumeric: 'tabular-nums',
    color: designTokens.colorPrimary,
  },
  trilho: {
    position: 'relative',
    height: '8px',
    borderRadius: '99px',
    backgroundColor: designTokens.colorCardBorder,
    marginTop: '4px',
  },
  preenchimento: { height: '100%', borderRadius: '99px' },
  marcaMeta: {
    position: 'absolute',
    right: 0,
    top: '-4px',
    width: '2px',
    height: '16px',
    backgroundColor: designTokens.colorNeutralDark,
  },
  apoio: { fontSize: '12px', fontWeight: 600, color: designTokens.colorNeutralMedium, marginTop: '6px', display: 'block' },
  linhas: { display: 'flex', flexDirection: 'column' },
  linha: {
    display: 'flex',
    justifyContent: 'space-between',
    padding: '7px 0',
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    fontSize: '13px',
    color: designTokens.colorNeutralMedium,
  },
  linhaValor: { fontWeight: 700, color: designTokens.colorNeutralDark, fontVariantNumeric: 'tabular-nums' },
  botaoMeta: {
    border: 'none',
    background: 'none',
    padding: 0,
    cursor: 'pointer',
    fontSize: '12.5px',
    fontWeight: 700,
    color: designTokens.colorPrimary,
    whiteSpace: 'nowrap',
  },
  semAcidente: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    padding: '10px 14px',
    borderRadius: '10px',
    backgroundColor: designTokens.colorSuccessWash,
  },
  semAcidenteNumero: {
    fontSize: '30px',
    lineHeight: '30px',
    fontWeight: 800,
    fontVariantNumeric: 'tabular-nums',
    color: designTokens.colorSuccess,
  },
  semAcidenteTexto: { fontSize: '13px', fontWeight: 600, color: designTokens.colorNeutralDark },
  semAcidenteDetalhe: { display: 'block', fontSize: '12px', fontWeight: 500, color: designTokens.colorNeutralMedium },
  grafico: { width: '100%', height: 'auto', display: 'block', marginTop: '4px' },
});

const CHAVE_META_LOCALSTORAGE = 'sst.tg.metaTaxaGravidade';

const formatar = (valor: number) =>
  valor.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

interface TaxaGravidadeCardProps {
  acidentes: Acidente[];
  registrosHht: RegistroHhtMensal[];
  /** Texto do escopo (ex.: "todas as obras") exibido no subtítulo. */
  escopo: string;
  className?: string;
}

const MESES_CURTOS = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];

// TG = (Dias Perdidos + Dias Debitados) × 1.000.000 / HHT — NBR 14280. Cálculo client-side,
// consistente com todos os outros KPIs do app (nenhum endpoint de agregação dedicado).
// A meta de comparação é um valor de negócio que este sistema não pode inventar — fica salva em
// localStorage, definida pelo próprio usuário no card (decisão de 2026-08-26).
// Card próprio ao lado do ASO desde 04/10: saiu da faixa de KPIs para ter espaço de mostrar a meta,
// a evolução mensal e a composição do cálculo.
export function TaxaGravidadeCard({ acidentes, registrosHht, escopo, className }: TaxaGravidadeCardProps) {
  const estilos = useStyles();
  const paleta = usePaletaGraficos();
  const [meta, setMeta] = useState<number | null>(null);
  const [editandoMeta, setEditandoMeta] = useState(false);
  const [rascunhoMeta, setRascunhoMeta] = useState('');

  useEffect(() => {
    try {
      const salvo = window.localStorage.getItem(CHAVE_META_LOCALSTORAGE);
      if (salvo) setMeta(Number(salvo));
    } catch {
      // localStorage indisponível (ex.: modo privado) — segue sem meta salva.
    }
  }, []);

  function salvarMeta() {
    const valor = Number(rascunhoMeta);
    if (!rascunhoMeta || Number.isNaN(valor) || valor <= 0) {
      setEditandoMeta(false);
      return;
    }
    setMeta(valor);
    try {
      window.localStorage.setItem(CHAVE_META_LOCALSTORAGE, String(valor));
    } catch {
      // Segue apenas em memória se localStorage indisponível.
    }
    setEditandoMeta(false);
  }

  const { diasPerdidos, diasDebitados, hht, taxaGravidade } = useMemo(() => {
    const perdidos = acidentes.reduce((soma, a) => soma + (a.diasAfastamento ?? 0), 0);
    const debitados = acidentes.reduce((soma, a) => soma + (a.diasDebitados ?? 0), 0);
    const horas = registrosHht.reduce((soma, r) => soma + r.horasHomemTrabalhadas, 0);
    const tg = horas > 0 ? ((perdidos + debitados) * 1_000_000) / horas : null;
    return { diasPerdidos: perdidos, diasDebitados: debitados, hht: horas, taxaGravidade: tg };
  }, [acidentes, registrosHht]);

  // Evolução mensal: a TG de cada mês dos últimos 6, só nos meses com horas-homem lançadas.
  const evolucao = useMemo(() => {
    const hoje = new Date();
    return Array.from({ length: 6 }, (_, i) => {
      const data = new Date(hoje.getFullYear(), hoje.getMonth() - (5 - i), 1);
      const ano = data.getFullYear();
      const mes = data.getMonth() + 1;
      const horas = registrosHht
        .filter((r) => r.ano === ano && r.mes === mes)
        .reduce((soma, r) => soma + r.horasHomemTrabalhadas, 0);
      const dias = acidentes
        .filter((a) => {
          const d = new Date(a.data);
          return d.getFullYear() === ano && d.getMonth() + 1 === mes;
        })
        .reduce((soma, a) => soma + (a.diasAfastamento ?? 0) + (a.diasDebitados ?? 0), 0);
      return { rotulo: MESES_CURTOS[data.getMonth()], valor: horas > 0 ? (dias * 1_000_000) / horas : null };
    });
  }, [acidentes, registrosHht]);

  // Dias corridos desde o último acidente com afastamento. Sem nenhum registro, não inventa um número.
  const semAfastamento = useMemo(() => {
    const comAfastamento = acidentes
      .filter((a) => a.houveAfastamento)
      .sort((a, b) => b.data.localeCompare(a.data));
    const ultimo = comAfastamento[0];
    if (!ultimo) return null;
    const hoje = new Date();
    // A data do acidente é só o dia (sem fuso): lida como UTC, igual ao 'hoje' abaixo, para não perder um dia.
    const diaDoAcidente = new Date(ultimo.data.slice(0, 10));
    const dias = Math.max(0, Math.round((Date.UTC(hoje.getFullYear(), hoje.getMonth(), hoje.getDate()) - diaDoAcidente.getTime()) / 86_400_000));
    return { dias, data: diaDoAcidente.toLocaleDateString('pt-BR', { timeZone: 'UTC' }), obra: ultimo.obraNome };
  }, [acidentes]);

  const dentroDaMeta = meta !== null && taxaGravidade !== null ? taxaGravidade <= meta : null;
  const corMeta = dentroDaMeta === false ? paleta.alerta : paleta.ok;

  return (
    <Card
      className={className}
      titulo={
        <>
          Taxa de Gravidade <RotuloEscopo tipo="acumulado" />
        </>
      }
      subtitulo={`NBR 14280 · ${escopo}`}
      acoes={
        editandoMeta ? (
          <Input
            size="small"
            type="number"
            min={1}
            autoFocus
            value={rascunhoMeta}
            onChange={(_, d) => setRascunhoMeta(d.value)}
            onBlur={salvarMeta}
            onKeyDown={(e) => e.key === 'Enter' && salvarMeta()}
            aria-label="Meta da taxa de gravidade"
            style={{ width: 90 }}
          />
        ) : (
          <button
            type="button"
            className={estilos.botaoMeta}
            onClick={() => {
              setRascunhoMeta(meta !== null ? String(meta) : '');
              setEditandoMeta(true);
            }}
          >
            {meta !== null ? 'Alterar meta' : 'Definir meta'}
          </button>
        )
      }
    >
      <div className={estilos.semAcidente}>
        {semAfastamento ? (
          <>
            <span className={estilos.semAcidenteNumero}>{semAfastamento.dias}</span>
            <span className={estilos.semAcidenteTexto}>
              dias sem acidente com afastamento
              <small className={estilos.semAcidenteDetalhe}>
                último: {semAfastamento.data}
                {semAfastamento.obra ? ` · ${semAfastamento.obra}` : ''}
              </small>
            </span>
          </>
        ) : (
          <span className={estilos.semAcidenteTexto}>Nenhum acidente com afastamento registrado.</span>
        )}
      </div>

      {taxaGravidade === null ? (
        <Legenda>
          Sem horas-homem trabalhadas lançadas: não dá para calcular a taxa. Lance o HHT mensal para ver o indicador.
        </Legenda>
      ) : (
        <>
          <div>
            <div className={estilos.destaque}>
              <span className={estilos.numero}>{formatar(taxaGravidade)}</span>
              {dentroDaMeta === null ? null : dentroDaMeta ? (
                <StatusChip tom="ok">Dentro da meta</StatusChip>
              ) : (
                <StatusChip tom="alerta">Acima da meta</StatusChip>
              )}
            </div>
            {meta !== null && (
              <>
                <div
                  className={estilos.trilho}
                  role="img"
                  aria-label={`Taxa de gravidade ${formatar(taxaGravidade)} para uma meta de ${formatar(meta)}`}
                >
                  <div
                    className={estilos.preenchimento}
                    style={{ width: `${Math.min(100, (taxaGravidade / meta) * 100)}%`, backgroundColor: corMeta }}
                  />
                  <span className={estilos.marcaMeta} />
                </div>
                <span className={estilos.apoio}>Meta: {formatar(meta)}</span>
              </>
            )}
            {meta === null && <span className={estilos.apoio}>Defina uma meta para comparar a taxa com o limite.</span>}
          </div>

          <Evolucao pontos={evolucao} cor={paleta.marca} />

          <div className={estilos.linhas}>
            <div className={estilos.linha}>
              <span>Dias perdidos</span>
              <span className={estilos.linhaValor}>{diasPerdidos}</span>
            </div>
            <div className={estilos.linha}>
              <span>Dias debitados</span>
              <span className={estilos.linhaValor}>{diasDebitados}</span>
            </div>
            <div className={estilos.linha}>
              <span>Horas-homem (HHT)</span>
              <span className={estilos.linhaValor}>{hht.toLocaleString('pt-BR')} h</span>
            </div>
          </div>
        </>
      )}
    </Card>
  );
}

interface EvolucaoProps {
  pontos: Array<{ rotulo: string; valor: number | null }>;
  cor: string;
}

// Linha simples dos últimos 6 meses. Meses sem HHT ficam sem ponto; com menos de 2 pontos não há
// evolução a mostrar e o bloco some.
function Evolucao({ pontos, cor }: EvolucaoProps) {
  const estilos = useStyles();
  const validos = pontos
    .map((p, i) => ({ ...p, i }))
    .filter((p): p is { rotulo: string; valor: number; i: number } => p.valor !== null);
  if (validos.length < 2) return null;

  const largura = 300;
  const base = 76;
  const topo = 12;
  const minimo = Math.min(...validos.map((p) => p.valor));
  const maximo = Math.max(...validos.map((p) => p.valor));
  const faixa = maximo - minimo || 1;
  const x = (i: number) => 15 + (i * (largura - 30)) / (pontos.length - 1);
  const y = (v: number) => base - ((v - minimo) / faixa) * (base - topo);
  const linha = validos.map((p) => `${x(p.i)},${y(p.valor)}`).join(' ');
  const ultimo = validos[validos.length - 1];
  const area = `M${x(validos[0].i)} ${y(validos[0].valor)} ${validos.map((p) => `L${x(p.i)} ${y(p.valor)}`).join(' ')} L${x(ultimo.i)} ${base} L${x(validos[0].i)} ${base} Z`;

  return (
    <div>
      <span className={estilos.apoio} style={{ marginTop: 0 }}>
        Evolução nos últimos 6 meses
      </span>
      <svg
        className={estilos.grafico}
        viewBox={`0 0 ${largura} 92`}
        role="img"
        aria-label={`Taxa de gravidade mensal: de ${formatar(validos[0].valor)} em ${validos[0].rotulo} para ${formatar(ultimo.valor)} em ${ultimo.rotulo}`}
      >
        <line x1="0" x2={largura} y1={base} y2={base} stroke={designTokens.colorCardBorder} />
        <path d={area} fill={cor} opacity="0.1" />
        <polyline points={linha} fill="none" stroke={cor} strokeWidth="2" />
        <circle cx={x(ultimo.i)} cy={y(ultimo.valor)} r="4" fill={cor} />
        {pontos.map((p, i) => (
          <text key={p.rotulo + i} x={x(i)} y="90" fontSize="10" textAnchor="middle" fill={designTokens.colorNeutralMedium}>
            {p.rotulo}
          </text>
        ))}
      </svg>
    </div>
  );
}
