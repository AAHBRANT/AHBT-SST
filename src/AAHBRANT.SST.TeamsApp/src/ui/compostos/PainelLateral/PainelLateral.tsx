import type { ReactNode } from 'react';
import { Button, DrawerBody, DrawerFooter, DrawerHeader, DrawerHeaderTitle, OverlayDrawer, makeStyles, mergeClasses } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  md: { width: '400px', maxWidth: '94vw' },
  lg: { width: '560px', maxWidth: '94vw' },
  cabecalho: { borderBottom: `1px solid ${tokensUi.bordaSuave}` },
  subtitulo: { color: designTokens.colorNeutralMedium, marginTop: tokensUi.espaco.xs },
  rodape: { borderTop: `1px solid ${tokensUi.bordaSuave}`, justifyContent: 'flex-end', gap: tokensUi.espaco.sm },
});

export interface PainelLateralProps {
  aberto: boolean;
  aoFechar: () => void;
  titulo: ReactNode;
  subtitulo?: ReactNode;
  largura?: 'md' | 'lg';
  rodape?: ReactNode;
  children: ReactNode;
}

// Painel que desliza da borda direita (spec §3, §4.2): formulários de criação saem de cima da tabela
// e vêm para aqui, com a lista visível atrás. Wrapper de OverlayDrawer (o Fluent já anima com a
// curva certa); absorve o estilo de pages/pessoas/TrabalhadoresGaveta.tsx.
export function PainelLateral({ aberto, aoFechar, titulo, subtitulo, largura = 'md', rodape, children }: PainelLateralProps) {
  const e = useStyles(); const tipo = useTipografia();
  return (
    <OverlayDrawer open={aberto} position="end" onOpenChange={(_, d) => { if (!d.open) aoFechar(); }} className={e[largura]}>
      <DrawerHeader className={e.cabecalho}>
        <DrawerHeaderTitle action={<Button appearance="subtle" aria-label="Fechar" icon={<Dismiss24Regular />} onClick={aoFechar} />}>
          <span className={tipo.subtitulo}>{titulo}</span>
          {subtitulo && <div className={mergeClasses(tipo.legenda, e.subtitulo)}>{subtitulo}</div>}
        </DrawerHeaderTitle>
      </DrawerHeader>
      <DrawerBody>{children}</DrawerBody>
      {rodape && <DrawerFooter className={e.rodape}>{rodape}</DrawerFooter>}
    </OverlayDrawer>
  );
}
