import { useState } from 'react';
import { motion } from 'framer-motion';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { Card, designTokens, tokensUi } from '@ui';

export interface TipoOcorrenciaResumo {
  /** Chave da série no gráfico (ex.: 'acidente'). */
  chave: string;
  rotulo: string;
  cor: string;
  /** Quantidade no período selecionado nos filtros da tela. */
  total: number;
}

export type PontoOcorrencias = { rotulo: string } & Record<string, number | string>;

export interface RegistroRecente {
  id: string;
  /** Mesma chave de TipoOcorrenciaResumo.chave. */
  chave: string;
  titulo: string;
  meta: string;
  quando: string;
}

interface OcorrenciasCardProps {
  tipos: TipoOcorrenciaResumo[];
  serie: PontoOcorrencias[];
  /** Registros mais recentes primeiro. */
  recentes: RegistroRecente[];
  subtitulo: string;
  aoAbrirOcorrencias: () => void;
}

const useStyles = makeStyles({
  resumo: { display: 'flex', flexWrap: 'wrap', gap: '8px', marginBottom: '14px' },
  chip: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '6px 12px',
    borderRadius: tokensUi.raio.md,
    border: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorNeutralMedium,
    cursor: 'pointer',
    minWidth: 0,
    fontFamily: 'inherit',
    ':hover': { borderColor: designTokens.colorNeutralMedium },
  },
  chipTotal: {
    backgroundColor: designTokens.colorPrimary,
    borderColor: designTokens.colorPrimary,
    color: '#ffffff',
    ':hover': { borderColor: designTokens.colorPrimary, filter: 'brightness(1.15)' },
  },
  chipSelecionado: {
    backgroundColor: designTokens.colorSurface,
    borderColor: designTokens.colorPrimary,
    boxShadow: `inset 0 0 0 1px ${designTokens.colorPrimary}`,
  },
  chipEsmaecido: { opacity: 0.5 },
  ponto: { width: '10px', height: '10px', borderRadius: '3px', flexShrink: 0 },
  valor: { fontSize: '18px', fontWeight: 800, lineHeight: '22px', fontVariantNumeric: 'tabular-nums', color: 'inherit' },
  valorNeutro: { color: designTokens.colorNeutralDark },
  rotulo: { fontSize: '12px', fontWeight: 600, lineHeight: '15px' },
  botaoLink: {
    border: 'none',
    background: 'none',
    padding: 0,
    cursor: 'pointer',
    fontSize: '12.5px',
    fontWeight: 700,
    color: designTokens.colorPrimary,
    whiteSpace: 'nowrap',
  },
  recentes: { marginTop: '14px', borderTop: `1px solid ${designTokens.colorCardBorder}`, paddingTop: '12px' },
  recentesTopo: { display: 'flex', justifyContent: 'space-between', gap: '8px', marginBottom: '6px' },
  recentesTitulo: { fontSize: '13px', fontWeight: 700, color: designTokens.colorNeutralDark },
  registro: {
    display: 'grid',
    gridTemplateColumns: '10px minmax(0, 1fr) auto',
    gap: '10px',
    padding: '8px 0',
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    alignItems: 'start',
    ':first-of-type': { borderTop: 'none' },
  },
  registroPonto: { width: '9px', height: '9px', borderRadius: '3px', marginTop: '5px' },
  registroTitulo: {
    fontSize: '13px',
    fontWeight: 600,
    color: designTokens.colorNeutralDark,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  registroMeta: { fontSize: '12px', color: designTokens.colorNeutralMedium },
  registroQuando: { fontSize: '11.5px', color: designTokens.colorNeutralMedium, whiteSpace: 'nowrap' },
  vazio: { fontSize: '13px', color: designTokens.colorNeutralMedium, padding: '6px 0' },
});

// Resumo de todas as ocorrências num card só (pedido do usuário, 04/10): totais por tipo no topo —
// que também fazem papel de legenda e de filtro — e a evolução dos últimos 6 meses empilhada por tipo.
// Clicar num tipo filtra o gráfico e a lista de registros no próprio card; só o link do topo e o
// "Ver todos" abrem a tela de Ocorrências.
export function OcorrenciasCard({ tipos, serie, recentes, subtitulo, aoAbrirOcorrencias }: OcorrenciasCardProps) {
  const estilos = useStyles();
  const [selecionada, setSelecionada] = useState<string | null>(null);

  const tipoSelecionado = tipos.find((t) => t.chave === selecionada) ?? null;
  const total = tipos.reduce((soma, t) => soma + t.total, 0);
  const tiposNoGrafico = tipoSelecionado ? [tipoSelecionado] : tipos;
  const corDoTipo = (chave: string) => tipos.find((t) => t.chave === chave)?.cor ?? designTokens.colorNeutralMedium;
  const rotuloDoTipo = (chave: string) => tipos.find((t) => t.chave === chave)?.rotulo ?? '';

  const registros = recentes.filter((r) => !tipoSelecionado || r.chave === tipoSelecionado.chave).slice(0, tipoSelecionado ? 3 : 4);

  return (
    <Card
      titulo="Ocorrências — últimos 6 meses"
      subtitulo={
        tipoSelecionado
          ? `Mostrando só ${tipoSelecionado.rotulo.toLowerCase()}. Clique no tipo de novo ou em "Total" para ver tudo.`
          : subtitulo
      }
      acoes={
        <button type="button" className={estilos.botaoLink} onClick={aoAbrirOcorrencias}>
          {tipoSelecionado ? `Abrir ${tipoSelecionado.rotulo.toLowerCase()} em Ocorrências` : 'Abrir Ocorrências'}
        </button>
      }
    >
      <div className={estilos.resumo} role="group" aria-label="Filtrar ocorrências por tipo">
        <button
          type="button"
          aria-pressed={tipoSelecionado === null}
          className={mergeClasses(estilos.chip, estilos.chipTotal)}
          onClick={() => setSelecionada(null)}
        >
          <span className={estilos.valor}>{total}</span>
          <span className={estilos.rotulo}>Total no período</span>
        </button>
        {tipos.map((tipo) => {
          const ativo = tipo.chave === tipoSelecionado?.chave;
          return (
            <button
              key={tipo.chave}
              type="button"
              aria-pressed={ativo}
              className={mergeClasses(
                estilos.chip,
                ativo && estilos.chipSelecionado,
                tipoSelecionado && !ativo && estilos.chipEsmaecido,
              )}
              onClick={() => setSelecionada(ativo ? null : tipo.chave)}
            >
              <span className={estilos.ponto} style={{ backgroundColor: tipo.cor }} />
              <span className={mergeClasses(estilos.valor, estilos.valorNeutro)}>{tipo.total}</span>
              <span className={estilos.rotulo}>{tipo.rotulo}</span>
            </button>
          );
        })}
      </div>

      <motion.div
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.35, ease: 'easeOut' }}
        style={{ width: '100%', height: 220 }}
      >
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={serie} margin={{ top: 8, right: 8, bottom: 0, left: -18 }} barCategoryGap="30%">
            <CartesianGrid vertical={false} stroke={designTokens.colorCardBorder} />
            <XAxis dataKey="rotulo" tickLine={false} axisLine={false} fontSize={11} />
            <YAxis allowDecimals={false} tickLine={false} axisLine={false} fontSize={11} width={40} />
            <Tooltip cursor={{ fill: 'transparent' }} />
            {tiposNoGrafico.map((tipo) => (
              <Bar
                key={tipo.chave}
                dataKey={tipo.chave}
                name={tipo.rotulo}
                stackId="ocorrencias"
                fill={tipo.cor}
                radius={tipoSelecionado ? [4, 4, 0, 0] : undefined}
                maxBarSize={44}
                isAnimationActive
                animationDuration={700}
              />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </motion.div>

      <div className={estilos.recentes}>
        <div className={estilos.recentesTopo}>
          <span className={estilos.recentesTitulo}>
            {tipoSelecionado ? `Últimos registros: ${tipoSelecionado.rotulo.toLowerCase()}` : 'Últimos registros'}
          </span>
          <button type="button" className={estilos.botaoLink} onClick={aoAbrirOcorrencias}>
            {tipoSelecionado ? `Ver todos (${tipoSelecionado.total})` : 'Ver todos'}
          </button>
        </div>
        {registros.length === 0 ? (
          <div className={estilos.vazio}>Nenhum registro{tipoSelecionado ? ' deste tipo' : ''}.</div>
        ) : (
          registros.map((registro) => (
            <div key={registro.id} className={estilos.registro}>
              <span className={estilos.registroPonto} style={{ backgroundColor: corDoTipo(registro.chave) }} />
              <div style={{ minWidth: 0 }}>
                <div className={estilos.registroTitulo} title={registro.titulo}>
                  {registro.titulo}
                </div>
                <div className={estilos.registroMeta}>
                  {tipoSelecionado ? registro.meta : `${rotuloDoTipo(registro.chave)} · ${registro.meta}`}
                </div>
              </div>
              <span className={estilos.registroQuando}>{registro.quando}</span>
            </div>
          ))
        )}
      </div>
    </Card>
  );
}
