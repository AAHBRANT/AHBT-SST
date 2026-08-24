import { AnimatePresence, motion } from 'framer-motion';
import { Badge, Text } from '@fluentui/react-components';
import { Warning24Filled } from '@fluentui/react-icons';
import { tipoEntidadeVinculadaLabel, tipoTagLabel, type TagIdentificacao } from '../../../lib/api';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';

interface TagsPerdidasPanelProps {
  tags: TagIdentificacao[];
}

export function TagsPerdidasPanel({ tags }: TagsPerdidasPanelProps) {
  const estilos = useDashboardStyles();

  const perdidas = [...tags].sort((a, b) => a.uid.localeCompare(b.uid));

  return (
    <div className={estilos.motorPainel}>
      <div className={estilos.motorCabecalho}>
        <div>
          <Text weight="semibold" size={400}>
            Tags de Identificação Perdidas
          </Text>
          <div>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
              Tags NTAG/QR/RFID marcadas como perdidas — podem representar um risco de identificação indevida se
              ainda estiverem vinculadas a uma área, ativo ou trabalhador.
            </Text>
          </div>
        </div>
        <Badge appearance="tint" color={perdidas.length === 0 ? 'success' : 'danger'}>
          {perdidas.length} tag(s) perdida(s)
        </Badge>
      </div>

      <div className={estilos.motorLista}>
        {perdidas.length === 0 && (
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            Nenhuma tag perdida para os filtros selecionados.
          </Text>
        )}
        <AnimatePresence initial={false}>
          {perdidas.map((tag, indice) => (
            <motion.div
              key={tag.id}
              layout
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
              className={`${estilos.motorLinha} ${estilos.motorLinhaBloqueada}`}
            >
              <div>
                <Text weight="semibold">{tag.uid}</Text>
                <div>
                  <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
                    {tipoTagLabel[tag.tipo]}
                    {tag.entidadeVinculadaTipo
                      ? ` · vinculada a ${tipoEntidadeVinculadaLabel[tag.entidadeVinculadaTipo]}`
                      : ' · sem vínculo'}
                  </Text>
                </div>
              </div>
              <Badge appearance="tint" color="danger" icon={<Warning24Filled />}>
                Perdida
              </Badge>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}
