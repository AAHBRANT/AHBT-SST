import type { ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../tokens/tokens';
import { useTipografia } from '../tokens/tipografia';

const useStyles = makeStyles({
  root: { marginBottom: tokensUi.espaco.xxl },
  titulo: { marginBottom: tokensUi.espaco.md, paddingBottom: tokensUi.espaco.sm, borderBottom: `1px solid ${tokensUi.bordaSuave}` },
  corpo: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.lg, alignItems: 'flex-start' },
});

// Bloco da galeria: título + exemplos de uma peça lado a lado.
export function Secao({ titulo, children }: { titulo: string; children: ReactNode }) {
  const estilos = useStyles();
  const tipo = useTipografia();
  return (
    <section className={estilos.root}>
      <h2 className={`${tipo.subtitulo} ${estilos.titulo}`}>{titulo}</h2>
      <div className={estilos.corpo}>{children}</div>
    </section>
  );
}
