import type { ReactNode } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { Card } from '../Card/Card';
import { transicaoNormal } from '../../tokens/movimento';

export interface PainelCriacaoInlineProps {
  aberto: boolean;
  titulo: ReactNode;
  children: ReactNode;
}

// Formulário de criação embutido na página (spec 2026-09-11, Onda A): substitui o PainelLateral
// para "Novo X" — cresce acima da lista/tabela em vez de cobrir a tela com um drawer, sem o vão
// vazio que um drawer de altura total cria quando o formulário é curto (achado em tablet,
// AtividadesTab). Mesmo mecanismo de "formulário cresce de onde foi acionado" que
// ui/layout/WorkflowActions/WorkflowActions.tsx já usa (altura + opacidade, transicaoNormal).
export function PainelCriacaoInline({ aberto, titulo, children }: PainelCriacaoInlineProps) {
  return (
    <AnimatePresence initial={false}>
      {aberto && (
        <motion.div
          key="painel-criacao-inline"
          initial={{ height: 0, opacity: 0 }}
          animate={{ height: 'auto', opacity: 1 }}
          exit={{ height: 0, opacity: 0 }}
          transition={transicaoNormal}
          style={{ overflow: 'hidden' }}
        >
          <Card titulo={titulo}>{children}</Card>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
