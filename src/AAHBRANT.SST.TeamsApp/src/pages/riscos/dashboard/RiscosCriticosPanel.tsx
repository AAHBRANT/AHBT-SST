import { AnimatePresence, motion } from 'framer-motion';
import { Badge, Text } from '@fluentui/react-components';
import { Warning24Filled } from '@fluentui/react-icons';
import {
  nivelRiscoLabel,
  StatusControleRisco,
  statusControleRiscoLabel,
  type Atividade,
  type Perigo,
  type Risco,
} from '../../../lib/api';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';

interface RiscosCriticosPanelProps {
  riscos: Risco[];
  atividades: Atividade[];
  perigos: Perigo[];
}

const hojeISO = new Date().toISOString().slice(0, 10);

export function RiscosCriticosPanel({ riscos, atividades, perigos }: RiscosCriticosPanelProps) {
  const estilos = useDashboardStyles();

  const nomeAtividade = (id: string) => atividades.find((a) => a.id === id)?.nome ?? id;
  const nomePerigo = (id: string) => perigos.find((p) => p.id === id)?.nome ?? id;

  const criticos = riscos
    .filter((r) => r.nivelRisco >= 4 && r.status !== StatusControleRisco.Concluido)
    .sort((a, b) => b.nivelRisco - a.nivelRisco);

  return (
    <div className={estilos.motorPainel}>
      <div className={estilos.motorCabecalho}>
        <div>
          <Text weight="semibold" size={400}>
            Riscos Críticos em Aberto
          </Text>
          <div>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
              Riscos Alto/Crítico cujo controle ainda não foi concluído — permanecem aqui até o plano de ação ser
              encerrado.
            </Text>
          </div>
        </div>
        <Badge appearance="tint" color={criticos.length === 0 ? 'success' : 'danger'}>
          {criticos.length} risco(s) crítico(s) em aberto
        </Badge>
      </div>

      <div className={estilos.motorLista}>
        {criticos.length === 0 && (
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            Nenhum risco Alto/Crítico em aberto para os filtros selecionados.
          </Text>
        )}
        <AnimatePresence initial={false}>
          {criticos.map((risco, indice) => {
            const vencido = !!risco.prazo && risco.prazo < hojeISO;
            return (
              <motion.div
                key={risco.id}
                layout
                initial={{ opacity: 0, x: -8 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
                className={`${estilos.motorLinha} ${estilos.motorLinhaBloqueada}`}
              >
                <div>
                  <Text weight="semibold">
                    {nomeAtividade(risco.atividadeId)} · {nomePerigo(risco.perigoId)}
                  </Text>
                  <div>
                    <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
                      {statusControleRiscoLabel[risco.status]}
                      {risco.prazo ? ` · prazo ${risco.prazo}${vencido ? ' (VENCIDO)' : ''}` : ' · sem prazo definido'}
                    </Text>
                  </div>
                </div>
                <Badge
                  appearance="tint"
                  color={risco.nivelRisco === 5 ? 'danger' : 'severe'}
                  icon={<Warning24Filled />}
                >
                  {nivelRiscoLabel[risco.nivelRisco]}
                </Badge>
              </motion.div>
            );
          })}
        </AnimatePresence>
      </div>
    </div>
  );
}
