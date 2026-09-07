import { useState, type ReactNode } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Button, makeStyles, mergeClasses } from '@fluentui/react-components';
import { ChevronDown16Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';
import { transicaoNormal } from '../../tokens/movimento';

const useStyles = makeStyles({
  lista: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm },
  acao: { border: `1px solid ${designTokens.colorCardBorder}`, borderRadius: tokensUi.raio.md, overflow: 'hidden', backgroundColor: designTokens.colorSurface },
  botao: { width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '10px', padding: '10px 12px', border: 0, background: 'transparent', textAlign: 'left', cursor: 'pointer', font: 'inherit', color: 'inherit', ':hover': { backgroundColor: designTokens.colorNeutralLight }, ':disabled': { opacity: 0.5, cursor: 'default' } },
  descricao: { display: 'block', color: designTokens.colorNeutralMedium, marginTop: '2px' },
  seta: { color: designTokens.colorNeutralMedium, flexShrink: 0, transitionProperty: 'transform', transitionDuration: '200ms' },
  setaAberta: { transform: 'rotate(180deg)' },
  destrutivo: { color: tokensUi.status.alerta.tinta },
  primario: { color: designTokens.colorPrimary },
  formulario: { padding: tokensUi.espaco.md, borderTop: `1px solid ${tokensUi.bordaSuave}`, backgroundColor: designTokens.colorNeutralLight, display: 'flex', flexDirection: 'column', gap: '10px' },
  botaoDestrutivo: { backgroundColor: tokensUi.status.alerta.tinta, color: '#ffffff', ':hover': { backgroundColor: tokensUi.status.alerta.tinta, color: '#ffffff', filter: 'brightness(0.92)' } },
  rotulo: { fontWeight: 700 },
});

export interface AcaoWorkflow {
  chave: string;
  rotulo: string;
  descricao?: string;
  tom?: 'primario' | 'neutro' | 'destrutivo';
  habilitada?: boolean;
  formulario?: ReactNode;
  aoExecutar: () => void | Promise<void>;
  rotuloExecutar?: string;
}

export interface WorkflowActionsProps { acoes: AcaoWorkflow[]; processando?: boolean }

// Ações do fluxo de um registro (spec §3, §4.3), extraídas de NaoConformidadeDetalhePage. A página
// passa só as ações permitidas no estado atual; cada uma abre o próprio formulário inline (origem: cresce
// do botão), uma por vez. Sem formulário, o clique executa direto.
export function WorkflowActions({ acoes, processando }: WorkflowActionsProps) {
  const e = useStyles(); const tipo = useTipografia();
  const [aberta, setAberta] = useState<string | null>(null);
  return (
    <div className={e.lista}>
      {acoes.map((a) => {
        const estaAberta = aberta === a.chave;
        const habilitada = a.habilitada ?? true;
        return (
          <div key={a.chave} className={e.acao}>
            <button type="button" className={mergeClasses(e.botao, a.tom === 'destrutivo' && e.destrutivo, a.tom === 'primario' && e.primario)} disabled={!habilitada || processando}
              aria-expanded={a.formulario ? estaAberta : undefined}
              onClick={() => { if (a.formulario) setAberta(estaAberta ? null : a.chave); else void a.aoExecutar(); }}>
              <span>
                <span className={mergeClasses(tipo.corpo, e.rotulo)}>{a.rotulo}</span>
                {a.descricao && <span className={mergeClasses(tipo.legenda, e.descricao)}>{a.descricao}</span>}
              </span>
              {a.formulario && <ChevronDown16Regular className={mergeClasses(e.seta, estaAberta && e.setaAberta)} />}
            </button>
            <AnimatePresence initial={false}>
              {a.formulario && estaAberta && (
                <motion.div key="f" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={transicaoNormal} style={{ overflow: 'hidden' }}>
                  <div className={e.formulario}>
                    {a.formulario}
                    <Button appearance={a.tom === 'destrutivo' ? 'secondary' : 'primary'} className={a.tom === 'destrutivo' ? e.botaoDestrutivo : undefined} disabled={processando}
                      onClick={async () => { await a.aoExecutar(); setAberta(null); }}>
                      {a.rotuloExecutar ?? a.rotulo}
                    </Button>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        );
      })}
    </div>
  );
}
