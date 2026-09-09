import { AnimatePresence, motion } from 'framer-motion';
import { Card, designTokens, Legenda, StatusChip, Text, tokensUi } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import {
  nivelRiscoLabel,
  StatusControleRisco,
  statusControleRiscoLabel,
  type Atividade,
  type Perigo,
  type Risco,
} from '../../../lib/api';

interface RiscosCriticosPanelProps {
  riscos: Risco[];
  atividades: Atividade[];
  perigos: Perigo[];
}

const hojeISO = new Date().toISOString().slice(0, 10);

// Camada ui/ (Onda 2, Task 13): painel de dashboard, mesma forma de AprVencidaPanel.tsx (Task 11) —
// Card + StatusChip + motion.div do framer-motion preservado. criticos.length===0 vira tom "ok";
// nível 4 (Alto) e 5 (Crítico) ambos tom "alerta" (mesmo colapso já usado em AprEtapasTab/
// MatrizRiscoTab desta task — o rótulo textual continua distinguindo os dois). Texto de meta com hex
// de fallback cru (var(--colorNeutralForeground3, #6D6D6D)) vira Legenda de @ui.
export function RiscosCriticosPanel({ riscos, atividades, perigos }: RiscosCriticosPanelProps) {
  const nomeAtividade = (id: string) => atividades.find((a) => a.id === id)?.nome ?? id;
  const nomePerigo = (id: string) => perigos.find((p) => p.id === id)?.nome ?? id;

  const criticos = riscos
    .filter((r) => r.nivelRisco >= 4 && r.status !== StatusControleRisco.Concluido)
    .sort((a, b) => b.nivelRisco - a.nivelRisco);

  return (
    <Card
      titulo="Riscos Críticos em Aberto"
      subtitulo="Riscos Alto/Crítico cujo controle ainda não foi concluído — permanecem aqui até o plano de ação ser encerrado."
      acoes={
        <StatusChip tom={criticos.length === 0 ? 'ok' : 'alerta'}>
          {criticos.length} risco(s) crítico(s) em aberto
        </StatusChip>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {criticos.length === 0 && (
          <Legenda>Nenhum risco Alto/Crítico em aberto para os filtros selecionados.</Legenda>
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
                style={{
                  display: 'grid',
                  gridTemplateColumns: '1fr auto',
                  alignItems: 'center',
                  gap: 12,
                  padding: '10px 14px',
                  borderRadius: 6,
                  backgroundColor: designTokens.colorNeutralLight,
                  borderLeft: `3px solid ${tokensUi.status.alerta.tinta}`,
                }}
              >
                <div>
                  <Text weight="semibold">
                    {nomeAtividade(risco.atividadeId)} · {nomePerigo(risco.perigoId)}
                  </Text>
                  <div>
                    <Legenda>
                      {statusControleRiscoLabel[risco.status]}
                      {risco.prazo ? ` · prazo ${risco.prazo}${vencido ? ' (VENCIDO)' : ''}` : ' · sem prazo definido'}
                    </Legenda>
                  </div>
                </div>
                <StatusChip tom="alerta" icone={<Warning24Filled aria-hidden="true" />}>
                  {nivelRiscoLabel[risco.nivelRisco]}
                </StatusChip>
              </motion.div>
            );
          })}
        </AnimatePresence>
      </div>
    </Card>
  );
}
