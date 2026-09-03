import { useEffect, useState } from 'react';
import { ImageAdd24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { designTokens } from '../../theme';

interface FotoCatalogoEpiProps {
  catalogoEpiId: string;
  temFoto: boolean;
  tamanho?: number;
}

// Miniatura da foto do item de EPI (pedido do usuário, 03/09) — usada no catálogo, na entrega e no
// popup de assinatura, pra o trabalhador reconhecer visualmente o que está recebendo. O binário
// nunca vem embutido no CatalogoEpiDto (só o flag temFoto); baixa sob demanda via
// api.catalogosEpi.baixarFoto e mostra um placeholder (sem quebrar layout) quando não há foto ainda.
export function FotoCatalogoEpi({ catalogoEpiId, temFoto, tamanho = 48 }: FotoCatalogoEpiProps) {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!temFoto || !catalogoEpiId) {
      setUrl(null);
      return;
    }
    let cancelado = false;
    let urlCriada: string | null = null;
    api.catalogosEpi
      .baixarFoto(catalogoEpiId)
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
  }, [catalogoEpiId, temFoto]);

  const estiloBase = {
    width: tamanho,
    height: tamanho,
    borderRadius: 8,
    flexShrink: 0,
  };

  if (url) {
    return <img src={url} alt="Foto do EPI" style={{ ...estiloBase, objectFit: 'cover' as const }} />;
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
