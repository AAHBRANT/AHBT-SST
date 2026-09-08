import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  titulo: { color: designTokens.colorNeutralMedium, marginTop: tokensUi.espaco.xl, marginBottom: '14px', paddingBottom: tokensUi.espaco.sm, borderBottom: `1px solid ${tokensUi.bordaSuave}` },
  primeira: { marginTop: 0 },
  rodape: { marginTop: tokensUi.espaco.xl, paddingTop: tokensUi.espaco.lg, borderTop: `1px solid ${tokensUi.bordaSuave}`, display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: tokensUi.espaco.lg, flexWrap: 'wrap' },
  info: { color: designTokens.colorNeutralMedium, maxWidth: '700px' },
});

// Seção de formulário (spec §3): rótulo pequeno em versalete que divide um formulário longo em blocos
// nomeados. Formaliza usePageStyles.sectionTitle/sectionTitleFirst.
export function FormSection({ titulo, numero, primeira, children }: { titulo: ReactNode; numero?: number; primeira?: boolean; children: ReactNode }) {
  const e = useStyles(); const tipo = useTipografia();
  return (
    <>
      <div className={mergeClasses(tipo.micro, e.titulo, primeira && e.primeira)}>{numero !== undefined ? `${numero}. ` : ''}{titulo}</div>
      {children}
    </>
  );
}

// Rodapé de formulário longo: texto de ajuda à esquerda, ações à direita. Formaliza usePageStyles.footer.
export function FormRodape({ info, children }: { info?: ReactNode; children: ReactNode }) {
  const e = useStyles(); const tipo = useTipografia();
  return (
    <div className={e.rodape}>
      {info ? <div className={mergeClasses(tipo.legenda, e.info)}>{info}</div> : <span />}
      <div style={{ display: 'flex', gap: 8 }}>{children}</div>
    </div>
  );
}
