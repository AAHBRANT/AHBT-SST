import { AnimatePresence, motion } from 'framer-motion';
import { Badge, Text } from '@fluentui/react-components';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusInspecao, statusInspecaoLabel, tipoInspecaoLabel, type Inspecao } from '../../../lib/api';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';

interface InspecoesNaoConformesPanelProps {
  inspecoes: Inspecao[];
}

export function InspecoesNaoConformesPanel({ inspecoes }: InspecoesNaoConformesPanelProps) {
  const estilos = useDashboardStyles();

  const comNaoConformidades = inspecoes
    .filter((i) => i.itensNaoConformes > 0)
    .sort((a, b) => b.itensNaoConformes - a.itensNaoConformes);

  return (
    <div className={estilos.motorPainel}>
      <div className={estilos.motorCabecalho}>
        <div>
          <Text weight="semibold" size={400}>
            Inspeções com Itens Não Conformes
          </Text>
          <div>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
              Execuções de checklist com ao menos 1 item não conforme, ordenadas da mais crítica para a menos
              crítica.
            </Text>
          </div>
        </div>
        <Badge appearance="tint" color={comNaoConformidades.length === 0 ? 'success' : 'danger'}>
          {comNaoConformidades.length} inspeção(ões) com item(ns) não conforme(s)
        </Badge>
      </div>

      <div className={estilos.motorLista}>
        {comNaoConformidades.length === 0 && (
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            Nenhuma inspeção com item não conforme para os filtros selecionados.
          </Text>
        )}
        <AnimatePresence initial={false}>
          {comNaoConformidades.map((inspecao, indice) => (
            <motion.div
              key={inspecao.id}
              layout
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
              className={`${estilos.motorLinha} ${estilos.motorLinhaBloqueada}`}
            >
              <div>
                <Text weight="semibold">
                  {inspecao.obraNome} · {tipoInspecaoLabel[inspecao.tipoInspecao] ?? inspecao.tipoInspecao}
                </Text>
                <div>
                  <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
                    {inspecao.checklistModeloNome} · {statusInspecaoLabel[inspecao.status]}
                    {inspecao.status === StatusInspecao.EmAndamento ? ' · ainda em andamento' : ''}
                  </Text>
                </div>
              </div>
              <Badge appearance="tint" color="danger" icon={<Warning24Filled />}>
                {inspecao.itensNaoConformes} de {inspecao.totalItens} não conforme(s)
              </Badge>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}
