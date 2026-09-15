import { useEffect, useState } from 'react';
import { ImageAdd24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { designTokens } from '@ui';

interface FotoCatalogoUniformeProps {
  catalogoUniformeId: string;
  temFoto: boolean;
  tamanho?: number;
}

// Miniatura da foto da peça de uniforme — mesmo padrão de FotoCatalogoEpi.tsx (decisão do usuário,
// 2026-09-07: adicionar foto também no catálogo de uniforme). O binário nunca vem embutido no
// CatalogoUniformeDto (só o flag temFoto); baixa sob demanda via api.catalogosUniforme.baixarFoto e
// mostra um placeholder (sem quebrar layout) quando não há foto ainda.
export function FotoCatalogoUniforme({ catalogoUniformeId, temFoto, tamanho = 48 }: FotoCatalogoUniformeProps) {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!temFoto || !catalogoUniformeId) {
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
  }, [catalogoUniformeId, temFoto]);

  const estiloBase = {
    width: tamanho,
    height: tamanho,
    borderRadius: 8,
    flexShrink: 0,
  };

  if (url) {
    return <img src={url} alt="Foto da peça de uniforme" style={{ ...estiloBase, objectFit: 'cover' as const }} />;
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
