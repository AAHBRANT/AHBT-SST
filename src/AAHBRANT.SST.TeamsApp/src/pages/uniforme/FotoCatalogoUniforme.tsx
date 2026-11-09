import { useEffect, useState } from 'react';
import { ImageAdd24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { designTokens } from '@ui';
import { referenciaUniforme } from './referenciasUniforme';

interface FotoCatalogoUniformeProps {
  catalogoUniformeId: string;
  temFoto: boolean;
  tamanho?: number;
  referenciaVisual?: string | null;
  destaque?: boolean;
}

// Miniatura da foto da peça de uniforme — mesmo padrão de FotoCatalogoEpi.tsx (decisão do usuário,
// 2026-09-07: adicionar foto também no catálogo de uniforme). O binário nunca vem embutido no
// CatalogoUniformeDto (só o flag temFoto); baixa sob demanda via api.catalogosUniforme.baixarFoto e
// mostra um placeholder (sem quebrar layout) quando não há foto ainda.
export function FotoCatalogoUniforme({ catalogoUniformeId, temFoto, tamanho = 48, referenciaVisual, destaque = false }: FotoCatalogoUniformeProps) {
  const referencia = referenciaUniforme(referenciaVisual);
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (referencia || !temFoto || !catalogoUniformeId) {
      setUrl(null);
      return;
    }
    let cancelado = false;
    let urlCriada: string | null = null;
    api.catalogosUniforme
      .baixarFoto(catalogoUniformeId)
      .then((blob) => {
        if (cancelado) return;
        urlCriada = URL.createObjectURL(blob);
        setUrl(urlCriada);
      })
      .catch(() => {
        if (!cancelado) setUrl(null);
      });
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [catalogoUniformeId, temFoto, referencia]);

  // Mostra só a foto da frente recortada da ficha (public/uniformes/*-frente.jpg), inteira e sem corte.
  if (referencia) return <img src={referencia.frente} alt={`Foto de uniforme · ${referencia.cor}`}
    className={destaque ? 'uniforme-foto-destaque' : undefined}
    style={destaque ? undefined : { width: tamanho, height: tamanho, objectFit: 'contain', borderRadius: 8, flexShrink: 0, backgroundColor: designTokens.colorNeutralLight }} />;
  const estiloBase = {
    width: destaque ? '100%' : tamanho,
    height: destaque ? undefined : tamanho,
    aspectRatio: destaque ? '5 / 4' : undefined,
    borderRadius: 8,
    flexShrink: 0,
  };

  if (url) {
    return <img src={url} alt="Foto da peça de uniforme" style={{ ...estiloBase, objectFit: 'contain' as const, backgroundColor: designTokens.colorNeutralLight }} />;
  }

  return (
    <div
      style={{
        ...estiloBase,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: designTokens.colorNeutralLight,
        color: designTokens.colorNeutralMedium,
        border: `1px dashed ${designTokens.colorCardBorder}`,
      }}
    >
      <ImageAdd24Regular fontSize={Math.round(tamanho * 0.5)} />
    </div>
  );
}
