import { useEffect, useState } from 'react';
import { ImageAdd24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { designTokens } from '../../theme';

interface FotoCatalogoEpcProps {
  catalogoEpcId: string;
  temFoto: boolean;
  tamanho?: number;
}

export function FotoCatalogoEpc({ catalogoEpcId, temFoto, tamanho = 48 }: FotoCatalogoEpcProps) {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!temFoto || !catalogoEpcId) {
      setUrl(null);
      return;
    }
    let cancelado = false;
    let urlCriada: string | null = null;
    api.catalogosEpc
      .baixarFoto(catalogoEpcId)
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
  }, [catalogoEpcId, temFoto]);

  const estiloBase = {
    width: tamanho,
    height: tamanho,
    borderRadius: 8,
    flexShrink: 0,
  };

  if (url) {
    return <img src={url} alt="Foto do EPC" style={{ ...estiloBase, objectFit: 'cover' as const }} />;
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
