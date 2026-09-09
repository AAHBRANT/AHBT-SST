import { AnimatePresence, motion } from 'framer-motion';
import { Card, designTokens, Legenda, StatusChip, Text } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusPt, statusPtLabel, type PermissaoTrabalho } from '../../../lib/api';

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

// Onda 2 Task 7 (camada ui/): painel de PTs vencidas do dashboard — mesmo padrão de
// AprVencidaPanel.tsx (Task 11): Card + StatusChip. O texto secundário usa `Legenda` (primitivo
// criado na Task 2/PR #33, já em master) em vez da ponte pragmática `designTokens.colorNeutralMedium`
// que a Task 11 registrou como achado não-bloqueante — corrigido aqui por já existir a peça certa.
export function PtVencidaPanel({ permissoes }: PtVencidaPanelProps) {
  const vencidas = permissoes
    .filter((p) => !!p.validade && p.validade < hojeISO && p.status !== StatusPt.Encerrada)
    .sort((a, b) => diasVencido(b.validade!) - diasVencido(a.validade!));

  return (
    <Card
      titulo="Permissões de Trabalho com Validade Vencida"
      subtitulo="PTs ainda não encerradas cuja validade já passou — a atividade pode estar sendo executada sem autorização válida, ordenadas da mais atrasada para a menos atrasada."
      acoes={
        <StatusChip tom={vencidas.length === 0 ? 'ok' : 'alerta'}>
          {vencidas.length} PT(s) com validade vencida
        </StatusChip>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
        {vencidas.length === 0 && <Legenda>Nenhuma PT com validade vencida para os filtros selecionados.</Legenda>}
        <AnimatePresence initial={false}>
          {vencidas.map((pt, indice) => (
            <motion.div
              key={pt.id}
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
                <Text weight="semibold">{pt.atividadeNome}</Text>
                <div>
                  <Legenda>
                    {pt.obraNome} · {pt.local} · {statusPtLabel[pt.status]} · vencida há {diasVencido(pt.validade!)}{' '}
                    dia(s)
                  </Legenda>
                </div>
              </div>
              <StatusChip tom="alerta" icone={<Warning24Filled aria-hidden="true" />}>
                Validade: {pt.validade!.slice(0, 10)}
              </StatusChip>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </Card>
  );
}
