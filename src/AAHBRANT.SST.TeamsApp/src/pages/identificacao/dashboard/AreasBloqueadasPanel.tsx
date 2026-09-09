import { AnimatePresence, motion } from 'framer-motion';
import { Card, designTokens, Legenda, StatusChip, Text } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import { StatusArea, tipoAreaLabel, type AreaSst } from '../../../lib/api';

export interface AreaComContexto extends AreaSst {
  obraNome: string;
}

interface AreasBloqueadasPanelProps {
  areas: AreaComContexto[];
}

// Onda 2 Task 9 (camada ui/): painel de Áreas Bloqueadas do dashboard — motorPainel→Card,
// Badge→StatusChip (item 5, 8 do Guia), mesmo padrão de AprVencidaPanel.tsx (Task 11). A animação
// de entrada por linha (framer-motion) segue inline: não há componente @ui dedicado a esta forma.
export function AreasBloqueadasPanel({ areas }: AreasBloqueadasPanelProps) {
  const bloqueadas = [...areas]
    .filter((a) => a.status === StatusArea.Bloqueada)
    .sort((a, b) => a.nome.localeCompare(b.nome));

  return (
    <Card
      titulo="Áreas Bloqueadas"
      subtitulo="Áreas de SST atualmente com status Bloqueada — representam um impedimento operacional ativo até a liberação."
      acoes={
        <StatusChip tom={bloqueadas.length === 0 ? 'ok' : 'alerta'}>
          {bloqueadas.length} área(s) bloqueada(s)
        </StatusChip>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
        {bloqueadas.length === 0 && <Legenda>Nenhuma área bloqueada para os filtros selecionados.</Legenda>}
        <AnimatePresence initial={false}>
          {bloqueadas.map((area, indice) => (
            <motion.div
              key={area.id}
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
                <Text weight="semibold">
                  {area.codigo} · {area.nome}
                </Text>
                <div>
                  <Legenda>
                    {area.obraNome} · {tipoAreaLabel[area.tipo]}
                    {area.detalhesLocalizacao ? ` · ${area.detalhesLocalizacao}` : ''}
                  </Legenda>
                </div>
              </div>
              <StatusChip tom="alerta" icone={<Warning24Filled aria-hidden="true" />}>
                Bloqueada
              </StatusChip>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </Card>
  );
}
