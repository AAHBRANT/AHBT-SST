import { AnimatePresence, motion } from 'framer-motion';
import { Card, designTokens, StatusChip, Text } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusApr, statusAprLabel, type Apr } from '../../../lib/api';

export interface AprComContexto extends Apr {
  obraNome: string;
}

interface AprVencidaPanelProps {
  aprs: AprComContexto[];
}

const hojeISO = new Date().toISOString().slice(0, 10);

function diasVencido(validade: string): number {
  const diffMs = new Date(hojeISO).getTime() - new Date(validade).getTime();
  return Math.round(diffMs / (1000 * 60 * 60 * 24));
}

// Onda 2 Task 11 (camada ui/): painel de APRs vencidas do dashboard — Card + StatusChip. A animação
// de entrada por linha (framer-motion) e o layout da linha seguem inline: não há componente @ui
// dedicado a esta forma de lista, e `designTokens` (bridge sancionado, importado de `@ui`) cobre a
// única cor que faltava (texto secundário, antes um hex de fallback cru).
export function AprVencidaPanel({ aprs }: AprVencidaPanelProps) {
  const vencidas = aprs
    .filter(
      (a) =>
        !!a.validade &&
        a.validade < hojeISO &&
        a.status !== StatusApr.Encerrada &&
        a.status !== StatusApr.Reprovada,
    )
    .sort((a, b) => diasVencido(b.validade!) - diasVencido(a.validade!));

  return (
    <Card
      titulo="APRs com Validade Vencida"
      subtitulo="APRs ainda não encerradas ou reprovadas cuja validade já passou — a atividade pode estar sendo executada sob uma análise de risco desatualizada, ordenadas da mais atrasada para a menos atrasada."
      acoes={
        <StatusChip tom={vencidas.length === 0 ? 'ok' : 'alerta'}>
          {vencidas.length} APR(s) com validade vencida
        </StatusChip>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
        {vencidas.length === 0 && (
          <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
            Nenhuma APR com validade vencida para os filtros selecionados.
          </Text>
        )}
        <AnimatePresence initial={false}>
          {vencidas.map((apr, indice) => (
            <motion.div
              key={apr.id}
              layout
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
              style={{
                display: 'grid',
                gridTemplateColumns: '1fr auto',
                alignItems: 'center',
                gap: 12,
                padding: '10px 14px',
                borderRadius: 6,
                backgroundColor: designTokens.colorNeutralLight,
                borderLeft: `3px solid ${designTokens.colorAlert}`,
              }}
            >
              <div>
                <Text weight="semibold">{apr.atividadeNome}</Text>
                <div>
                  <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                    {apr.obraNome} · {apr.local} · {statusAprLabel[apr.status]} · vencida há {diasVencido(apr.validade!)} dia(s)
                  </Text>
                </div>
              </div>
              <StatusChip tom="alerta" icone={<Warning24Filled aria-hidden="true" />}>
                Validade: {apr.validade!.slice(0, 10)}
              </StatusChip>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </Card>
  );
}
