import type { ReactElement, ReactNode } from 'react';
import { Button, makeStyles } from '@fluentui/react-components';
import { Search24Regular, Wrench24Regular, DocumentAdd24Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  root: { display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', gap: '10px', padding: `${tokensUi.espaco.xxxl} ${tokensUi.espaco.xl}` },
  icone: { width: '52px', height: '52px', borderRadius: tokensUi.raio.lg, backgroundColor: designTokens.colorNeutralLight, color: designTokens.colorNeutralMedium, display: 'grid', placeItems: 'center' },
  titulo: { marginTop: '6px' },
  descricao: { color: designTokens.colorNeutralMedium, maxWidth: '420px', margin: 0 },
  acao: { marginTop: '6px' },
});

export interface EstadoVazioProps {
  icone?: ReactElement;
  titulo: ReactNode;
  descricao?: ReactNode;
  acao?: { rotulo: string; aoClicar: () => void };
  variante?: 'vazio' | 'sem-resultado' | 'em-construcao';
}

const iconePadrao = { vazio: <DocumentAdd24Regular />, 'sem-resultado': <Search24Regular />, 'em-construcao': <Wrench24Regular /> };

// Estado vazio com orientação (spec §3): um vazio sem ação é porta fechada; com ação é convite.
// Substitui components/EstadoVazio.tsx (só uma linha cinza) e pages/EmConstrucaoPage.tsx.
export function EstadoVazio({ icone, titulo, descricao, acao, variante = 'vazio' }: EstadoVazioProps) {
  const estilos = useStyles();
  const tipo = useTipografia();
  return (
    <div className={estilos.root}>
      <div className={estilos.icone}>{icone ?? iconePadrao[variante]}</div>
      <div className={`${tipo.subtitulo} ${estilos.titulo}`}>{titulo}</div>
      {descricao && <p className={`${tipo.corpo} ${estilos.descricao}`}>{descricao}</p>}
      {acao && <Button className={estilos.acao} onClick={acao.aoClicar}>{acao.rotulo}</Button>}
    </div>
  );
}
