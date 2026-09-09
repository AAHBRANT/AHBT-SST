import { AnimatePresence, motion } from 'framer-motion';
import { Card, designTokens, Legenda, StatusChip, Text } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import { tipoEntidadeVinculadaLabel, tipoTagLabel, type TagIdentificacao } from '../../../lib/api';

interface TagsPerdidasPanelProps {
  tags: TagIdentificacao[];
}

// Onda 2 Task 9 (camada ui/): painel de Tags Perdidas do dashboard — motorPainel→Card,
// Badge→StatusChip (item 5, 8 do Guia), mesmo padrão de AreasBloqueadasPanel.tsx neste módulo e de
// AprVencidaPanel.tsx (Task 11).
export function TagsPerdidasPanel({ tags }: TagsPerdidasPanelProps) {
  const perdidas = [...tags].sort((a, b) => a.uid.localeCompare(b.uid));

  return (
    <Card
      titulo="Tags de Identificação Perdidas"
      subtitulo="Tags NTAG/QR/RFID marcadas como perdidas — podem representar um risco de identificação indevida se ainda estiverem vinculadas a uma área, ativo ou trabalhador."
      acoes={
        <StatusChip tom={perdidas.length === 0 ? 'ok' : 'alerta'}>
          {perdidas.length} tag(s) perdida(s)
        </StatusChip>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
        {perdidas.length === 0 && <Legenda>Nenhuma tag perdida para os filtros selecionados.</Legenda>}
        <AnimatePresence initial={false}>
          {perdidas.map((tag, indice) => (
            <motion.div
              key={tag.id}
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
                <Text weight="semibold">{tag.uid}</Text>
                <div>
                  <Legenda>
                    {tipoTagLabel[tag.tipo]}
                    {tag.entidadeVinculadaTipo
                      ? ` · vinculada a ${tipoEntidadeVinculadaLabel[tag.entidadeVinculadaTipo]}`
                      : ' · sem vínculo'}
                  </Legenda>
                </div>
              </div>
              <StatusChip tom="alerta" icone={<Warning24Filled aria-hidden="true" />}>
                Perdida
              </StatusChip>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </Card>
  );
}
