import { AnimatePresence, motion } from 'framer-motion';
import { Card, Legenda, StatusChip } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusInspecao, statusInspecaoLabel, tipoInspecaoLabel, type Inspecao } from '../../../lib/api';

interface InspecoesNaoConformesPanelProps {
  inspecoes: Inspecao[];
}

// Onda 2 Task 10 (camada ui/): painel de inspeções com itens não conformes do dashboard — Card +
// StatusChip, mesmo padrão de AprVencidaPanel.tsx (Task 11)/RiscosCriticosPanel.tsx. A animação de
// entrada por linha (framer-motion) segue inline: não há componente @ui dedicado a esta forma de
// lista. Texto secundário usa `Legenda` (primitivo de @ui, PR #33) em vez do hex de fallback cru que
// os pilotos anteriores usavam como ponte antes desse primitivo existir.
export function InspecoesNaoConformesPanel({ inspecoes }: InspecoesNaoConformesPanelProps) {
  const comNaoConformidades = inspecoes
    .filter((i) => i.itensNaoConformes > 0)
    .sort((a, b) => b.itensNaoConformes - a.itensNaoConformes);

  return (
    <Card
      titulo="Inspeções com Itens Não Conformes"
      subtitulo="Execuções de checklist com ao menos 1 item não conforme, ordenadas da mais crítica para a menos crítica."
      acoes={
        <StatusChip tom={comNaoConformidades.length === 0 ? 'ok' : 'alerta'}>
          {comNaoConformidades.length} inspeção(ões) com item(ns) não conforme(s)
        </StatusChip>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
        {comNaoConformidades.length === 0 && (
          <Legenda>Nenhuma inspeção com item não conforme para os filtros selecionados.</Legenda>
        )}
        <AnimatePresence initial={false}>
          {comNaoConformidades.map((inspecao, indice) => (
            <motion.div
              key={inspecao.id}
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
              }}
            >
              <div>
                <div style={{ fontWeight: 600 }}>
                  {inspecao.obraNome} · {tipoInspecaoLabel[inspecao.tipoInspecao] ?? inspecao.tipoInspecao}
                </div>
                <Legenda>
                  {inspecao.checklistModeloNome} · {statusInspecaoLabel[inspecao.status]}
                  {inspecao.status === StatusInspecao.EmAndamento ? ' · ainda em andamento' : ''}
                </Legenda>
              </div>
              <StatusChip tom="alerta" icone={<Warning24Filled aria-hidden="true" />}>
                {inspecao.itensNaoConformes} de {inspecao.totalItens} não conforme(s)
              </StatusChip>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </Card>
  );
}
