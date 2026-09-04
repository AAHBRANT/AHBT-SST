import { motion } from 'framer-motion';
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis } from 'recharts';
import { designTokens } from '../../../theme';

export interface PontoTendencia {
  rotulo: string;
  valor: number;
}

interface TrendLineChartProps {
  dados: PontoTendencia[];
  cor?: string;
}

export function TrendLineChart({ dados, cor = designTokens.colorPrimary }: TrendLineChartProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, ease: 'easeOut' }}
      style={{ width: '100%', height: 200 }}
    >
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={dados} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <defs>
            <linearGradient id="tendenciaGradiente" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={cor} stopOpacity={0.35} />
              <stop offset="100%" stopColor={cor} stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid vertical={false} stroke={designTokens.colorCardBorder} />
          <XAxis dataKey="rotulo" tickLine={false} axisLine={false} fontSize={11} />
          <Tooltip formatter={(valor: unknown) => [String(valor), 'Ocorrências']} />
          <Area
            type="monotone"
            dataKey="valor"
            stroke={cor}
            strokeWidth={2.4}
            fill="url(#tendenciaGradiente)"
            isAnimationActive
            animationDuration={700}
            dot={{ r: 3.2, fill: designTokens.colorSurface, stroke: cor, strokeWidth: 2 }}
            activeDot={{ r: 4, fill: cor }}
          />
        </AreaChart>
      </ResponsiveContainer>
    </motion.div>
  );
}
