import { AnimatePresence, motion } from 'framer-motion';
import { Badge, Text } from '@fluentui/react-components';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusControleRisco, statusControleRiscoLabel, type PlanoAcaoItem } from '../../../lib/api';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';

export interface AcaoPlanoComContexto extends PlanoAcaoItem {
  pgrNome: string;
  obraNome: string;
}

interface PgrPlanoAcaoVencidoPanelProps {
  acoes: AcaoPlanoComContexto[];
}

const hojeISO = new Date().toISOString().slice(0, 10);

function diasVencido(prazo: string): number {
  const diffMs = new Date(hojeISO).getTime() - new Date(prazo).getTime();
  return Math.round(diffMs / (1000 * 60 * 60 * 24));
}

export function PgrPlanoAcaoVencidoPanel({ acoes }: PgrPlanoAcaoVencidoPanelProps) {
  const estilos = useDashboardStyles();

  const vencidas = acoes
    .filter((a) => !!a.prazo && a.prazo < hojeISO && a.status !== StatusControleRisco.Concluido)
    .sort((a, b) => diasVencido(b.prazo!) - diasVencido(a.prazo!));

  return (
    <div className={estilos.motorPainel}>
      <div className={estilos.motorCabecalho}>
        <div>
          <Text weight="semibold" size={400}>
            Ações do Plano de Ação (PGR) com Prazo Vencido
          </Text>
          <div>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
              Itens do plano de ação de qualquer PGR ainda não concluídos cujo prazo já passou, ordenados da mais
              atrasada para a menos atrasada.
            </Text>
          </div>
        </div>
        <Badge appearance="tint" color={vencidas.length === 0 ? 'success' : 'danger'}>
          {vencidas.length} ação(ões) com prazo vencido
        </Badge>
      </div>

      <div className={estilos.motorLista}>
        {vencidas.length === 0 && (
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            Nenhuma ação do plano de ação com prazo vencido para os filtros selecionados.
          </Text>
        )}
        <AnimatePresence initial={false}>
          {vencidas.map((acao, indice) => (
            <motion.div
              key={acao.id}
              layout
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
              className={`${estilos.motorLinha} ${estilos.motorLinhaBloqueada}`}
            >
              <div>
                <Text weight="semibold">{acao.descricao}</Text>
                <div>
                  <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
                    {acao.obraNome} · {acao.pgrNome} · {statusControleRiscoLabel[acao.status]} · vencida há{' '}
                    {diasVencido(acao.prazo!)} dia(s)
                  </Text>
                </div>
              </div>
              <Badge appearance="tint" color="danger" icon={<Warning24Filled />}>
                Prazo: {acao.prazo!.slice(0, 10)}
              </Badge>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}
