import { useEffect, useRef, useState } from 'react';
import { Spinner, Text } from '@fluentui/react-components';
import { SeletorFotoCamera } from './SeletorFotoCamera';
import { EstadoVazio } from '../ui';
import { usePageStyles } from '../pages/pageStyles';

interface VisualizadorDocumentoPdfProps {
  id: string;
  obterDocumento: () => Promise<Blob | null>;
  enviarDocumento: (arquivo: File) => Promise<void>;
}

// Mostra o PDF já cadastrado direto na tela (iframe + Blob URL), com rolagem nativa do navegador
// por todas as páginas — pedido do usuário (09/09): nada de botão "abrir em outro lugar", o
// documento tem que aparecer inteiro assim que a aba é aberta.
export function VisualizadorDocumentoPdf({ id, obterDocumento, enviarDocumento }: VisualizadorDocumentoPdfProps) {
  const estilos = usePageStyles();
  const [carregando, setCarregando] = useState(true);
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const blobUrlRef = useRef<string | null>(null);

  async function carregar() {
    try {
      setCarregando(true);
      setErro(null);
      const blob = await obterDocumento();
      if (blobUrlRef.current) {
        URL.revokeObjectURL(blobUrlRef.current);
        blobUrlRef.current = null;
      }
      if (!blob) {
        setBlobUrl(null);
        return;
      }
      const url = URL.createObjectURL(blob);
      blobUrlRef.current = url;
      setBlobUrl(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar o documento.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    return () => {
      if (blobUrlRef.current) URL.revokeObjectURL(blobUrlRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function enviar(arquivo: File) {
    try {
      setErro(null);
      await enviarDocumento(arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar o documento.');
    }
  }

  if (carregando) {
    return (
      <div style={{ padding: 32, textAlign: 'center' }}>
        <Spinner label="Carregando documento..." />
      </div>
    );
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}
      {!blobUrl ? (
        <>
          <EstadoVazio titulo="Nenhum documento anexado ainda." />
          <div style={{ textAlign: 'center', marginTop: 8 }}>
            <SeletorFotoCamera
              rotulo="Anexar documento"
              tiposAceitos="application/pdf"
              tamanhoMaximoMb={20}
              permitirCamera={false}
              aoSelecionarArquivo={enviar}
              aoErroValidacao={setErro}
            />
          </div>
        </>
      ) : (
        <>
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 8 }}>
            <SeletorFotoCamera
              rotulo="Substituir documento"
              tiposAceitos="application/pdf"
              tamanhoMaximoMb={20}
              permitirCamera={false}
              aoSelecionarArquivo={enviar}
              aoErroValidacao={setErro}
            />
          </div>
          <iframe src={blobUrl} title="Documento" style={{ width: '100%', height: '80vh', border: 'none' }} />
        </>
      )}
    </div>
  );
}
