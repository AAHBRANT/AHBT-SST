import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, makeStyles, mergeClasses } from '@fluentui/react-components';
import { ArrowLeft16Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  root: { display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: tokensUi.espaco.lg, marginBottom: tokensUi.espaco.lg, flexWrap: 'wrap' },
  titulo: { margin: 0, display: 'flex', alignItems: 'center', gap: tokensUi.espaco.sm, flexWrap: 'wrap' },
  subtitulo: { color: designTokens.colorNeutralMedium, marginTop: tokensUi.espaco.xs },
  acoes: { display: 'flex', alignItems: 'center', gap: tokensUi.espaco.sm, flexWrap: 'wrap' },
  voltar: { marginBottom: '6px', color: designTokens.colorNeutralMedium },
});

export interface PageHeaderProps {
  titulo: ReactNode;
  subtitulo?: ReactNode;
  status?: ReactNode;
  acoes?: ReactNode;
  filtros?: ReactNode;
  voltarPara?: string;
  rotuloVoltar?: string;
}

// Cabeçalho de página (spec §3): título + subtítulo à esquerda, filtros e ações à direita, link de
// voltar opcional acima. Substitui o par toolbar + <Text size={500} weight="semibold"> repetido em
// toda página e o boolean mostrarTitulo — a página-pilar simplesmente não renderiza PageHeader no filho.
export function PageHeader({ titulo, subtitulo, status, acoes, filtros, voltarPara, rotuloVoltar }: PageHeaderProps) {
  const estilos = useStyles();
  const tipo = useTipografia();
  const navigate = useNavigate();
  return (
    <div>
      {voltarPara && (
        <Button appearance="subtle" size="small" icon={<ArrowLeft16Regular />} className={estilos.voltar} onClick={() => navigate(voltarPara)}>
          {rotuloVoltar ?? 'Voltar'}
        </Button>
      )}
      <div className={estilos.root}>
        <div>
          <h1 className={mergeClasses(tipo.titulo, estilos.titulo)}>{titulo}{status}</h1>
          {subtitulo && <div className={mergeClasses(tipo.corpo, estilos.subtitulo)}>{subtitulo}</div>}
        </div>
        {(filtros || acoes) && <div className={estilos.acoes}>{filtros}{acoes}</div>}
      </div>
    </div>
  );
}
