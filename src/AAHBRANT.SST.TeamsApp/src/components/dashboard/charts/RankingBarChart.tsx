import { motion } from 'framer-motion';
import { Bar, BarChart, Cell, LabelList, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { designTokens } from '../../../theme';

export interface ItemRanking {
  rotulo: string;
  valor: number;
  cor?: string;
  detalhe?: string;
}

interface RankingBarChartProps {
  dados: ItemRanking[];
  corPadrao?: string;
  dominio?: [number | 'auto' | 'dataMin', number | 'auto' | 'dataMax'];
  valorReferencia?: number;
  sufixo?: string;
}

export function RankingBarChart({
  dados,
  corPadrao = '#3B82F6',
  dominio = [0, 'auto'],
  valorReferencia,
  sufixo = '',
}: RankingBarChartProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, ease: 'easeOut' }}
      style={{ width: '100%', height: 260 }}
    >
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={dados} layout="vertical" margin={{ top: 4, right: 32, bottom: 4, left: 4 }} barCategoryGap={10}>
          <XAxis type="number" domain={dominio} hide />
          <YAxis type="category" dataKey="rotulo" width={100} tickLine={false} axisLine={false} fontSize={12} />
          {valorReferencia !== undefined && (
            <ReferenceLine x={valorReferencia} stroke={designTokens.colorCardBorder} strokeDasharray="4 4" />
          )}
          <Tooltip
            formatter={(_valor: unknown, _nome: unknown, item: { payload?: ItemRanking }) => {
              const p = item.payload;
              const detalhe = p?.detalhe ? ` (${p.detalhe})` : '';
              return [`${p?.valor ?? 0}${sufixo}${detalhe}`, p?.rotulo ?? ''];
            }}
          />
          <Bar dataKey="valor" radius={[0, 4, 4, 0]} maxBarSize={22} isAnimationActive animationDuration={700}>
            {dados.map((d) => (
              <Cell key={d.rotulo} fill={d.cor ?? corPadrao} />
            ))}
            <LabelList dataKey="valor" position="right" formatter={(v: unknown) => `${v}${sufixo}`} fontSize={12} />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </motion.div>
  );
}
