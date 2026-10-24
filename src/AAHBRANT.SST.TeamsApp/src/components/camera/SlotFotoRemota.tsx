import { useEffect, useState } from 'react';
import { SlotFoto, type SlotFotoProps } from './SlotFoto';

interface SlotFotoRemotaProps extends Omit<SlotFotoProps, 'url' | 'carregandoMiniatura'> {
  /** Id do registro dono da foto (catálogo de EPI/EPC/Uniforme, obra, funcionário, etc.). */
  id: string;
  temFoto: boolean;
  /** Função de download já existente em `api.*` (ex.: `api.catalogosEpi.baixarFoto`). */
  baixarFoto: (id: string) => Promise<Blob>;
}

// SlotFoto que busca a própria miniatura sob demanda (mesmo padrão de FotoCatalogoEpi/Epc, mas já
// com o visual/ação unificados de SlotFoto) — usado em qualquer lugar que só tem "id + temFoto" e
// não quer manter um mapa de object URLs próprio (14/09).
export function SlotFotoRemota({ id, temFoto, baixarFoto, aoSelecionarArquivo, ...resto }: SlotFotoRemotaProps) {
  const [url, setUrl] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(temFoto);

  useEffect(() => {
    if (!temFoto) {
      setUrl(null);
      setCarregando(false);
      return undefined;
    }
    let cancelado = false;
    let urlCriada: string | null = null;
    setCarregando(true);
    baixarFoto(id)
      .then((blob) => {
        if (cancelado) return;
        urlCriada = URL.createObjectURL(blob);
        setUrl(urlCriada);
      })
      .catch(() => {
        if (!cancelado) setUrl(null);
      })
      .finally(() => {
        if (!cancelado) setCarregando(false);
      });
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [id, temFoto, baixarFoto]);

  // Ao SUBSTITUIR uma foto já existente, `id`/`temFoto` não mudam de valor — o efeito acima não
  // dispararia de novo e a miniatura ficaria presa na versão anterior. Em vez de esperar um
  // round-trip de download, usa o próprio arquivo escolhido pra atualizar a prévia na hora.
  async function aoSelecionarArquivoInterno(arquivo: File) {
    await aoSelecionarArquivo(arquivo);
    setUrl((atual) => {
      if (atual) URL.revokeObjectURL(atual);
      return URL.createObjectURL(arquivo);
    });
  }

  return <SlotFoto url={url} carregandoMiniatura={carregando} aoSelecionarArquivo={aoSelecionarArquivoInterno} {...resto} />;
}
