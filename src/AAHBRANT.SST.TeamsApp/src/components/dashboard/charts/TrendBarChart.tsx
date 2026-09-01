import { motion } from 'framer-motion';
import { Bar, BarChart, CartesianGrid, LabelList, ResponsiveContainer, Tooltip, XAxis } from 'recharts';
import { designTokens } from '../../../theme';

export interface PontoTendencia {
  rotulo: string;
  valor: number;
}

interface TrendBarChartProps {
  dados: PontoTendencia[];
  cor?: string;
}

export function TrendBarChart({ dados, cor = designTokens.colorWarning }: TrendBarChartProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, ease: 'easeOut' }}
      style={{ width: '100%', height: 200 }}
    >
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={dados} margin={{ top: 16, right: 8, bottom: 0, left: 0 }} barCategoryGap="30%">
          <CartesianGrid vertical={false} stroke={designTokens.colorCardBorder} />
          <XAxis dataKey="rotulo" tickLine={false} axisLine={false} fontSize={11} />
          <Tooltip formatter={(valor: unknown) => [String(valor), 'Ocorrências']} />
          <Bar dataKey="valor" fill={cor} radius={[4, 4, 0, 0]} maxBarSize={36} isAnimationActive animationDuration={700}>
            <LabelList dataKey="valor" position="top" fontSize={12} fontWeight={700} fill={designTokens.colorNeutralDark} />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </motion.div>
  );
}
