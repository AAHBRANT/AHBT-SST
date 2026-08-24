import { motion } from 'framer-motion';
import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { Text } from '@fluentui/react-components';

export interface FatiaDonut {
  rotulo: string;
  valor: number;
  cor: string;
}

interface StatusDonutChartProps {
  dados: FatiaDonut[];
  legendaCentral?: string;
}

export function StatusDonutChart({ dados, legendaCentral }: StatusDonutChartProps) {
  const total = dados.reduce((soma, d) => soma + d.valor, 0);

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.96 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.35, ease: 'easeOut' }}
      style={{ position: 'relative', width: '100%', height: 260 }}
    >
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie
            data={dados}
            dataKey="valor"
            nameKey="rotulo"
            innerRadius={64}
            outerRadius={92}
            paddingAngle={2}
            cornerRadius={4}
            isAnimationActive
            animationDuration={700}
          >
            {dados.map((fatia) => (
              <Cell key={fatia.rotulo} fill={fatia.cor} stroke="#FFFFFF" strokeWidth={2} />
            ))}
          </Pie>
          <Tooltip
            formatter={(valor: unknown, nome: unknown) => {
              const numerico = typeof valor === 'number' ? valor : Number(valor);
              const percentual = total > 0 ? Math.round((numerico / total) * 100) : 0;
              return [`${numerico} (${percentual}%)`, String(nome)];
            }}
          />
          <Legend verticalAlign="bottom" height={36} iconType="circle" iconSize={8} />
        </PieChart>
      </ResponsiveContainer>
      <div
        style={{
          position: 'absolute',
          top: '42%',
          left: '50%',
          transform: 'translate(-50%, -50%)',
          textAlign: 'center',
          pointerEvents: 'none',
        }}
      >
        <div style={{ fontSize: 26, fontWeight: 700, lineHeight: '28px' }}>{total}</div>
        {legendaCentral && (
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            {legendaCentral}
          </Text>
        )}
      </div>
    </motion.div>
  );
}
