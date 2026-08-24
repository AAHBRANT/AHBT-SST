import { AnimatePresence, motion } from 'framer-motion';
import { Badge, Text } from '@fluentui/react-components';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusPt, statusPtLabel, type PermissaoTrabalho } from '../../../lib/api';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';

export interface PtComContexto extends PermissaoTrabalho {
  obraNome: string;
}

interface PtVencidaPanelProps {
  permissoes: PtComContexto[];
}

const hojeISO = new Date().toISOString().slice(0, 10);

function diasVencido(validade: string): number {
  const diffMs = new Date(hojeISO).getTime() - new Date(validade).getTime();
  return Math.round(diffMs / (1000 * 60 * 60 * 24));
}

export function PtVencidaPanel({ permissoes }: PtVencidaPanelProps) {
  const estilos = useDashboardStyles();

  const vencidas = permissoes
    .filter((p) => !!p.validade && p.validade < hojeISO && p.status !== StatusPt.Encerrada)
    .sort((a, b) => diasVencido(b.validade!) - diasVencido(a.validade!));

  return (
    <div className={estilos.motorPainel}>
      <div className={estilos.motorCabecalho}>
        <div>
          <Text weight="semibold" size={400}>
            Permissões de Trabalho com Validade Vencida
          </Text>
          <div>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
              PTs ainda não encerradas cuja validade já passou — a atividade pode estar sendo executada sem
              autorização válida, ordenadas da mais atrasada para a menos atrasada.
            </Text>
          </div>
        </div>
        <Badge appearance="tint" color={vencidas.length === 0 ? 'success' : 'danger'}>
          {vencidas.length} PT(s) com validade vencida
        </Badge>
      </div>

      <div className={estilos.motorLista}>
        {vencidas.length === 0 && (
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            Nenhuma PT com validade vencida para os filtros selecionados.
          </Text>
        )}
        <AnimatePresence initial={false}>
          {vencidas.map((pt, indice) => (
            <motion.div
              key={pt.id}
              layout
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
              className={`${estilos.motorLinha} ${estilos.motorLinhaBloqueada}`}
            >
              <div>
                <Text weight="semibold">{pt.atividadeNome}</Text>
                <div>
                  <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
                    {pt.obraNome} · {pt.local} · {statusPtLabel[pt.status]} · vencida há {diasVencido(pt.validade!)}{' '}
                    dia(s)
                  </Text>
                </div>
              </div>
              <Badge appearance="tint" color="danger" icon={<Warning24Filled />}>
                Validade: {pt.validade!.slice(0, 10)}
              </Badge>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}
