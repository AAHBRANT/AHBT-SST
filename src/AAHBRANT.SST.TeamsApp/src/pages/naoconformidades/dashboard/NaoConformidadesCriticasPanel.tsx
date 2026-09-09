import { AnimatePresence, motion } from 'framer-motion';
import { Card, designTokens, Legenda, StatusChip, Text } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import {
  origemNaoConformidadeLabel,
  StatusNaoConformidade,
  statusNaoConformidadeLabel,
  type NaoConformidade,
} from '../../../lib/api';

interface NaoConformidadesCriticasPanelProps {
  naoConformidades: NaoConformidade[];
}

const hojeISO = new Date().toISOString().slice(0, 10);

function diasVencido(prazo: string): number {
  const diffMs = new Date(hojeISO).getTime() - new Date(prazo).getTime();
  return Math.round(diffMs / (1000 * 60 * 60 * 24));
}

// Onda 2 Task 15 (camada ui/): painel de NCs com prazo vencido do dashboard — mesmo padrão de
// AprVencidaPanel.tsx (Task 11)/RiscosCriticosPanel.tsx (Task 13): motorPainel/motorCabecalho/
// motorLista cru vira Card + StatusChip. Usa o primitivo Legenda (já em master, PR #33) para o
// texto secundário, em vez da ponte designTokens.colorNeutralMedium que os dois pilotos anteriores
// precisaram usar antes dele existir — só o fundo/borda da linha ainda vem de designTokens (bridge
// sancionada, importada de @ui — spec §1.6), que não tem componente próprio para isso.
export function NaoConformidadesCriticasPanel({ naoConformidades }: NaoConformidadesCriticasPanelProps) {
  const vencidas = naoConformidades
    .filter((nc) => !!nc.prazo && nc.prazo < hojeISO && nc.status !== StatusNaoConformidade.Encerrada)
    .sort((a, b) => diasVencido(b.prazo!) - diasVencido(a.prazo!));

  return (
    <Card
      titulo="Não Conformidades com Prazo Vencido"
      subtitulo="NCs ainda não encerradas cujo prazo já passou — permanecem aqui até o encerramento, ordenadas da mais atrasada para a menos atrasada."
      acoes={
        <StatusChip tom={vencidas.length === 0 ? 'ok' : 'alerta'}>
          {vencidas.length} NC(s) com prazo vencido
        </StatusChip>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
        {vencidas.length === 0 && (
          <Legenda>Nenhuma não conformidade com prazo vencido para os filtros selecionados.</Legenda>
        )}
        <AnimatePresence initial={false}>
          {vencidas.map((nc, indice) => (
            <motion.div
              key={nc.id}
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
                <Text weight="semibold">{nc.descricao}</Text>
                <div>
                  <Legenda>
                    {origemNaoConformidadeLabel[nc.origemDeteccao]} · {statusNaoConformidadeLabel[nc.status]} ·
                    vencida há {diasVencido(nc.prazo!)} dia(s)
                  </Legenda>
                </div>
              </div>
              <StatusChip tom="alerta" icone={<Warning24Filled aria-hidden="true" />}>
                Prazo: {nc.prazo!.slice(0, 10)}
              </StatusChip>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </Card>
  );
}
