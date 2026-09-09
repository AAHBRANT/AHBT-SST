import { AnimatePresence, motion } from 'framer-motion';
import { Card, designTokens, Legenda, StatusChip, Text } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusControleRisco, statusControleRiscoLabel, type PlanoAcaoItem } from '../../../lib/api';

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

// Onda 2 Task 8 (camada ui/): painel de ações do plano de ação (PGR) com prazo vencido — mesmo padrão
// de AprVencidaPanel.tsx (Task 11): Card + StatusChip, animação de entrada por linha (framer-motion)
// preservada inline (não há componente @ui dedicado a esta forma de lista). Usa `Legenda` (já em
// `@ui` desde a Task 3/PR #33) para o texto secundário, em vez da ponte `designTokens.colorNeutralMedium`
// que os pilotos anteriores usaram antes de `Legenda` existir.
export function PgrPlanoAcaoVencidoPanel({ acoes }: PgrPlanoAcaoVencidoPanelProps) {
  const vencidas = acoes
    .filter((a) => !!a.prazo && a.prazo < hojeISO && a.status !== StatusControleRisco.Concluido)
    .sort((a, b) => diasVencido(b.prazo!) - diasVencido(a.prazo!));

  return (
    <Card
      titulo="Ações do Plano de Ação (PGR) com Prazo Vencido"
      subtitulo="Itens do plano de ação de qualquer PGR ainda não concluídos cujo prazo já passou, ordenados da mais atrasada para a menos atrasada."
      acoes={
        <StatusChip tom={vencidas.length === 0 ? 'ok' : 'alerta'}>
          {vencidas.length} ação(ões) com prazo vencido
        </StatusChip>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
        {vencidas.length === 0 && <Legenda>Nenhuma ação do plano de ação com prazo vencido para os filtros selecionados.</Legenda>}
        <AnimatePresence initial={false}>
          {vencidas.map((acao, indice) => (
            <motion.div
              key={acao.id}
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
                <Text weight="semibold">{acao.descricao}</Text>
                <div>
                  <Legenda>
                    {acao.obraNome} · {acao.pgrNome} · {statusControleRiscoLabel[acao.status]} · vencida há{' '}
                    {diasVencido(acao.prazo!)} dia(s)
                  </Legenda>
                </div>
              </div>
              <StatusChip tom="alerta" icone={<Warning24Filled aria-hidden="true" />}>
                Prazo: {acao.prazo!.slice(0, 10)}
              </StatusChip>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </Card>
  );
}
