import type { ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

// Célula de lista em dois níveis (título forte + detalhe discreto), com um visual opcional à
// esquerda: iniciais da pessoa, foto do EPI, selo da NR. Padrão das listas aprovado em 25/11/2026
// (docs/mockups/listas-gsst-2026-11-25.html).
const useEstilos = makeStyles({
  raiz: { display: 'flex', alignItems: 'center', gap: '10px', minWidth: 0 },
  textos: { minWidth: 0, display: 'flex', flexDirection: 'column', gap: '1px' },
  titulo: { fontWeight: 700, fontSize: '13.5px', lineHeight: '18px', color: designTokens.colorNeutralDark },
  tituloLeve: { fontWeight: 500 },
  detalhe: { fontSize: '11.5px', lineHeight: '15px', color: designTokens.colorNeutralMedium },
  iniciais: {
    flexShrink: 0, width: '34px', height: '34px', borderRadius: tokensUi.raio.full, display: 'grid', placeItems: 'center',
    fontSize: '12px', fontWeight: 700, color: designTokens.colorPrimary,
    backgroundColor: `color-mix(in srgb, ${designTokens.colorSurface} 88%, ${designTokens.colorPrimary})`,
  },
  selo: {
    flexShrink: 0, minWidth: '50px', textAlign: 'center', padding: '6px 8px', borderRadius: tokensUi.raio.sm,
    fontSize: '11px', fontWeight: 900, letterSpacing: '0.02em', color: designTokens.colorPrimary,
    backgroundColor: `color-mix(in srgb, ${designTokens.colorSurface} 88%, ${designTokens.colorPrimary})`,
  },
});

export interface CelulaDuplaProps {
  titulo: ReactNode;
  detalhe?: ReactNode;
  /** Visual à esquerda (ex.: <Iniciais nome=… />, foto, <SeloTexto>NR-35</SeloTexto>). */
  inicio?: ReactNode;
  /** Título em peso normal — para quando a coluna não é a principal da linha. */
  tituloLeve?: boolean;
}

export function CelulaDupla({ titulo, detalhe, inicio, tituloLeve }: CelulaDuplaProps) {
  const e = useEstilos();
  return (
    <div className={e.raiz}>
      {inicio}
      <div className={e.textos}>
        <span className={tituloLeve ? `${e.titulo} ${e.tituloLeve}` : e.titulo}>{titulo}</span>
        {detalhe && <span className={e.detalhe}>{detalhe}</span>}
      </div>
    </div>
  );
}

/** Iniciais do nome (primeiro e último) num círculo marsala claro. */
export function Iniciais({ nome }: { nome: string }) {
  const e = useEstilos();
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  const texto = partes.length === 0 ? '?' : (partes[0][0] + (partes.length > 1 ? partes[partes.length - 1][0] : '')).toUpperCase();
  return <span className={e.iniciais} aria-hidden="true">{texto}</span>;
}

/** Selo curto de destaque (ex.: a norma do curso, "NR-35"). */
export function SeloTexto({ children }: { children: ReactNode }) {
  const e = useEstilos();
  return <span className={e.selo}>{children}</span>;
}
