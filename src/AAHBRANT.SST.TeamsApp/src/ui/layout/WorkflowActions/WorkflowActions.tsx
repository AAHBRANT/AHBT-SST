import { useId, useState, type ReactNode } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Button, Dialog, DialogSurface, DialogBody, DialogTitle, DialogContent, DialogActions, Spinner, makeStyles, mergeClasses } from '@fluentui/react-components';
import { ChevronDown16Regular, CheckmarkCircle24Regular, Warning24Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';
import { transicaoNormal } from '../../tokens/movimento';

const useStyles = makeStyles({
  lista: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm },
  acao: { border: `1px solid ${designTokens.colorCardBorder}`, borderRadius: tokensUi.raio.md, overflow: 'hidden', backgroundColor: designTokens.colorSurface },
  botao: { width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '10px', padding: '10px 12px', border: 0, background: 'transparent', textAlign: 'left', cursor: 'pointer', font: 'inherit', color: 'inherit', ':hover': { backgroundColor: designTokens.colorNeutralLight }, ':disabled': { opacity: 0.5, cursor: 'default' } },
  descricao: { display: 'block', color: designTokens.colorNeutralMedium, marginTop: '2px' },
  seta: { color: designTokens.colorNeutralMedium, flexShrink: 0, transitionProperty: 'transform', transitionDuration: tokensUi.duracao.normal },
  setaAberta: { transform: 'rotate(180deg)' },
  destrutivo: { color: tokensUi.status.alerta.tinta },
  primario: { color: designTokens.colorPrimary },
  formulario: { padding: tokensUi.espaco.md, borderTop: `1px solid ${tokensUi.bordaSuave}`, backgroundColor: designTokens.colorNeutralLight, display: 'flex', flexDirection: 'column', gap: '10px' },
  botaoDestrutivo: { backgroundColor: tokensUi.status.alerta.tinta, color: designTokens.colorWhite, ':hover': { backgroundColor: tokensUi.status.alerta.tinta, color: designTokens.colorWhite, filter: 'brightness(0.92)' } },
  rotulo: { fontWeight: 700 },
  finalizacao: { padding: tokensUi.espaco.md, border: `2px solid ${designTokens.colorPrimary}`, borderRadius: tokensUi.raio.md, display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm, backgroundColor: designTokens.colorSurface },
  finalizar: { minHeight: '52px', width: '100%', fontWeight: 800, whiteSpace: 'normal', textAlign: 'center', padding: tokensUi.espaco.md },
  pendente: { border: `2px solid ${tokensUi.status.atencao.tinta}` },
  aviso: { padding: tokensUi.espaco.sm, borderRadius: tokensUi.raio.sm, backgroundColor: tokensUi.status.atencao.fundo, color: tokensUi.status.atencao.tinta, fontWeight: 600 },
});

export interface AcaoWorkflow {
  /** Única dentro da lista — é a chave React e o identificador do formulário aberto. */
  chave: string;
  rotulo: string;
  descricao?: string;
  tom?: 'primario' | 'neutro' | 'destrutivo';
  habilitada?: boolean;
  formulario?: ReactNode;
  /**
   * Deve tratar e exibir os próprios erros. Retornar `false` (ou lançar) mantém o formulário
   * aberto — use para validação local que não deve fechar o formulário sem sucesso real. Retorno
   * vazio (`void`/`undefined`) fecha normalmente após `fecharAposSucesso`.
   */
  aoExecutar: () => void | boolean | Promise<void | boolean>;
  rotuloExecutar?: string;
  finalizacao?: boolean;
  pendencias?: string[];
}

export interface WorkflowActionsProps { acoes: AcaoWorkflow[]; processando?: boolean; erro?: string | null }

// Ações do fluxo de um registro (spec §3, §4.3), extraídas de NaoConformidadeDetalhePage. A página
// passa só as ações permitidas no estado atual; cada uma abre o próprio formulário inline (origem: cresce
// do botão), uma por vez. Sem formulário, o clique executa direto.
export function WorkflowActions({ acoes, processando, erro }: WorkflowActionsProps) {
  const e = useStyles(); const tipo = useTipografia();
  const [aberta, setAberta] = useState<string | null>(null);
  const [confirmando, setConfirmando] = useState<string | null>(null);
  const [executando, setExecutando] = useState(false);
  const [falhou, setFalhou] = useState(false);
  const descricaoId = useId();
  const confirmacao = acoes.find(a => a.chave === confirmando);
  const ocupado = !!processando || executando;

  // Contrato: aoExecutar trata e exibe o próprio erro (ex.: FeedbackInline na página). Se ainda
  // assim lançar — ou devolver false, o caso da validação local que só define o erro e retorna —
  // o formulário permanece aberto para nova tentativa em vez de fechar e levar a mensagem para
  // fora da vista do usuário.
  async function executar(a: AcaoWorkflow, fecharAposSucesso: boolean) {
    if (ocupado) return;
    setExecutando(true);
    setFalhou(false);
    try {
      const resultado = await a.aoExecutar();
      if (fecharAposSucesso && resultado !== false) setAberta(null);
      if (resultado !== false) setConfirmando(null);
      else setFalhou(true);
    } catch (erro) {
      setFalhou(true);
      if (import.meta.env.DEV) console.error('WorkflowActions: aoExecutar lançou erro', erro);
    } finally { setExecutando(false); }
  }

  return (
    <><div className={e.lista}>
      {acoes.map((a) => {
        const estaAberta = aberta === a.chave;
        const habilitada = a.habilitada ?? true;
        if (a.finalizacao) return (
          <section key={a.chave} className={mergeClasses(e.finalizacao, !!a.pendencias?.length && e.pendente)} aria-label="Finalização do registro">
            <strong>Finalização</strong>
            <span className={tipo.legenda}>{a.descricao || 'Confira os dados e finalize este registro aqui.'}</span>
            {!!a.pendencias?.length && <span className={e.aviso} role="status">{a.pendencias.length} pendência(s) para resolver antes de finalizar.</span>}
            <Button appearance="primary" className={e.finalizar} disabled={!habilitada || ocupado}
              icon={ocupado ? <Spinner size="tiny" /> : a.pendencias?.length ? <Warning24Regular /> : <CheckmarkCircle24Regular />}
              aria-expanded={a.formulario ? estaAberta : undefined}
              onClick={() => {
                setFalhou(false);
                if (a.formulario) setAberta(estaAberta ? null : a.chave);
                else setConfirmando(a.chave);
              }}>
              {ocupado ? 'Processando…' : a.rotulo}
            </Button>
            {!!a.pendencias?.length && <span className={tipo.legenda}>Clique no botão para ver o que falta. Seus dados permanecem salvos em andamento.</span>}
            <AnimatePresence initial={false}>
              {a.formulario && estaAberta && (
                <motion.div key="finalizacao-form" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={transicaoNormal} style={{ overflow: 'hidden', width: '100%' }}>
                  <div className={e.formulario}>
                    {a.formulario}
                    <Button appearance="primary" disabled={processando || ocupado} onClick={() => void executar(a, true)}>
                      {a.rotuloExecutar ?? a.rotulo}
                    </Button>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </section>
        );
        return (
          <div key={a.chave} className={e.acao}>
            <button type="button" className={mergeClasses(e.botao, a.tom === 'destrutivo' && e.destrutivo, a.tom === 'primario' && e.primario)} disabled={!habilitada || ocupado}
              aria-expanded={a.formulario ? estaAberta : undefined}
              onClick={() => { if (a.formulario) setAberta(estaAberta ? null : a.chave); else void executar(a, false); }}>
              <span>
                <span className={mergeClasses(tipo.corpo, e.rotulo)}>{a.rotulo}</span>
                {a.descricao && <span className={mergeClasses(tipo.legenda, e.descricao)}>{a.descricao}</span>}
              </span>
              {a.formulario && <ChevronDown16Regular aria-hidden="true" className={mergeClasses(e.seta, estaAberta && e.setaAberta)} />}
            </button>
            <AnimatePresence initial={false}>
              {a.formulario && estaAberta && (
                <motion.div key="f" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={transicaoNormal} style={{ overflow: 'hidden' }}>
                  <div className={e.formulario}>
                    {a.formulario}
                    <Button appearance={a.tom === 'destrutivo' ? 'secondary' : 'primary'} className={a.tom === 'destrutivo' ? e.botaoDestrutivo : undefined} disabled={processando}
                      onClick={() => void executar(a, true)}>
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
    <Dialog open={!!confirmacao} onOpenChange={(_, d) => { if (!d.open && !ocupado) setConfirmando(null); }}>
      <DialogSurface aria-describedby={descricaoId}>
        <DialogBody>
          <DialogTitle>{confirmacao?.pendencias?.length ? 'Resolva as pendências para finalizar' : confirmacao?.rotulo}</DialogTitle>
          <DialogContent id={descricaoId}>
            {confirmacao?.pendencias?.length ? <>
              <p>O registro continua em andamento. Confira estes itens antes de sair do local:</p>
              <ul>{confirmacao.pendencias.map((p, i) => <li key={i}>{p}</li>)}</ul>
              <p>Fotos sem localização precisam de nova captura no local. A data e a hora das fotos já registradas serão preservadas.</p>
            </> : <p>{confirmacao?.descricao || 'Confirme que os dados foram revisados para finalizar este registro.'}</p>}
            {falhou && <p role="alert" className={e.aviso}>{erro || 'Não foi possível finalizar. Confira as pendências informadas na página e tente novamente.'}</p>}
          </DialogContent>
          <DialogActions>
            <Button disabled={ocupado} onClick={() => setConfirmando(null)}>{confirmacao?.pendencias?.length ? 'Voltar e regularizar' : 'Continuar revisando'}</Button>
            {confirmacao && !confirmacao.pendencias?.length && <Button appearance="primary" disabled={ocupado} onClick={() => void executar(confirmacao, false)}>
              {ocupado ? 'Finalizando…' : confirmacao.rotulo}
            </Button>}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog></>
  );
}
