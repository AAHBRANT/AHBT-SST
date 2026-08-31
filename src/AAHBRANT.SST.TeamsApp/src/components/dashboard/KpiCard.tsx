import type { ReactElement } from 'react';
import { motion } from 'framer-motion';
import { usePageStyles } from '../../pages/pageStyles';
import { useDashboardStyles } from './dashboardStyles';

interface KpiCardProps {
  rotulo: string;
  valor: number | string;
  cor: string;
  icone?: ReactElement;
}

export function KpiCard({ rotulo, valor, cor, icone }: KpiCardProps) {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();
  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
      className={estilosPagina.card}
    >
      {icone && (
        <div className={estilos.kpiIcone} style={{ color: cor }}>
          {icone}
        </div>
      )}
      <div className={estilos.kpiValor} style={{ color: cor }}>
        {valor}
      </div>
      <div className={estilos.kpiRotulo}>{rotulo}</div>
    </motion.div>
  );
}
