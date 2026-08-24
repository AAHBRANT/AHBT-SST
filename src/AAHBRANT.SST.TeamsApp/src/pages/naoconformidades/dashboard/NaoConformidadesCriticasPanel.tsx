import { AnimatePresence, motion } from 'framer-motion';
import { Badge, Text } from '@fluentui/react-components';
import { Warning24Filled } from '@fluentui/react-icons';
import {
  origemNaoConformidadeLabel,
  StatusNaoConformidade,
  statusNaoConformidadeLabel,
  type NaoConformidade,
} from '../../../lib/api';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';

interface NaoConformidadesCriticasPanelProps {
  naoConformidades: NaoConformidade[];
}

const hojeISO = new Date().toISOString().slice(0, 10);

function diasVencido(prazo: string): number {
  const diffMs = new Date(hojeISO).getTime() - new Date(prazo).getTime();
  return Math.round(diffMs / (1000 * 60 * 60 * 24));
}

export function NaoConformidadesCriticasPanel({ naoConformidades }: NaoConformidadesCriticasPanelProps) {
  const estilos = useDashboardStyles();

  const vencidas = naoConformidades
    .filter((nc) => !!nc.prazo && nc.prazo < hojeISO && nc.status !== StatusNaoConformidade.Encerrada)
    .sort((a, b) => diasVencido(b.prazo!) - diasVencido(a.prazo!));

  return (
    <div className={estilos.motorPainel}>
      <div className={estilos.motorCabecalho}>
        <div>
          <Text weight="semibold" size={400}>
            Não Conformidades com Prazo Vencido
          </Text>
          <div>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
              NCs ainda não encerradas cujo prazo já passou — permanecem aqui até o encerramento, ordenadas da
              mais atrasada para a menos atrasada.
            </Text>
          </div>
        </div>
        <Badge appearance="tint" color={vencidas.length === 0 ? 'success' : 'danger'}>
          {vencidas.length} NC(s) com prazo vencido
        </Badge>
      </div>

      <div className={estilos.motorLista}>
        {vencidas.length === 0 && (
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            Nenhuma não conformidade com prazo vencido para os filtros selecionados.
          </Text>
        )}
        <AnimatePresence initial={false}>
          {vencidas.map((nc, indice) => (
            <motion.div
              key={nc.id}
              layout
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
              className={`${estilos.motorLinha} ${estilos.motorLinhaBloqueada}`}
            >
              <div>
                <Text weight="semibold">{nc.descricao}</Text>
                <div>
                  <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
                    {origemNaoConformidadeLabel[nc.origemDeteccao]} · {statusNaoConformidadeLabel[nc.status]} ·
                    vencida há {diasVencido(nc.prazo!)} dia(s)
                  </Text>
                </div>
              </div>
              <Badge appearance="tint" color="danger" icon={<Warning24Filled />}>
                Prazo: {nc.prazo!.slice(0, 10)}
              </Badge>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}
