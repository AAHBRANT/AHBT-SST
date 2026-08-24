import { AnimatePresence, motion } from 'framer-motion';
import { Badge, Text } from '@fluentui/react-components';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusArea, tipoAreaLabel, type AreaSst } from '../../../lib/api';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';

export interface AreaComContexto extends AreaSst {
  obraNome: string;
}

interface AreasBloqueadasPanelProps {
  areas: AreaComContexto[];
}

export function AreasBloqueadasPanel({ areas }: AreasBloqueadasPanelProps) {
  const estilos = useDashboardStyles();

  const bloqueadas = [...areas]
    .filter((a) => a.status === StatusArea.Bloqueada)
    .sort((a, b) => a.nome.localeCompare(b.nome));

  return (
    <div className={estilos.motorPainel}>
      <div className={estilos.motorCabecalho}>
        <div>
          <Text weight="semibold" size={400}>
            Áreas Bloqueadas
          </Text>
          <div>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
              Áreas de SST atualmente com status Bloqueada — representam um impedimento operacional ativo até a
              liberação.
            </Text>
          </div>
        </div>
        <Badge appearance="tint" color={bloqueadas.length === 0 ? 'success' : 'danger'}>
          {bloqueadas.length} área(s) bloqueada(s)
        </Badge>
      </div>

      <div className={estilos.motorLista}>
        {bloqueadas.length === 0 && (
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            Nenhuma área bloqueada para os filtros selecionados.
          </Text>
        )}
        <AnimatePresence initial={false}>
          {bloqueadas.map((area, indice) => (
            <motion.div
              key={area.id}
              layout
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
              className={`${estilos.motorLinha} ${estilos.motorLinhaBloqueada}`}
            >
              <div>
                <Text weight="semibold">
                  {area.codigo} · {area.nome}
                </Text>
                <div>
                  <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
                    {area.obraNome} · {tipoAreaLabel[area.tipo]}
                    {area.detalhesLocalizacao ? ` · ${area.detalhesLocalizacao}` : ''}
                  </Text>
                </div>
              </div>
              <Badge appearance="tint" color="danger" icon={<Warning24Filled />}>
                Bloqueada
              </Badge>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}
