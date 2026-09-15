import { useEffect, useState } from 'react';
import { Spinner, Text } from '@fluentui/react-components';
import { SeletorFotoCamera } from './SeletorFotoCamera';
import { PaginasPdf } from './PaginasPdf';
import { EstadoVazio } from '../ui';
import { usePageStyles } from '../pages/pageStyles';

interface VisualizadorDocumentoPdfProps {
  id: string;
  obterDocumento: () => Promise<Blob | null>;
  enviarDocumento: (arquivo: File) => Promise<void>;
}

// Mostra o PDF já cadastrado direto na tela, com rolagem por todas as páginas — pedido do usuário
// (09/09): nada de botão "abrir em outro lugar", o documento tem que aparecer inteiro assim que a
// aba é aberta. A renderização é feita em canvas pelo pdf.js (PaginasPdf) porque <iframe>/<object>
// com PDF fica em branco dentro do Teams (13/09) — ver comentário em PaginasPdf.tsx.
export function VisualizadorDocumentoPdf({ id, obterDocumento, enviarDocumento }: VisualizadorDocumentoPdfProps) {
  const estilos = usePageStyles();
  const [carregando, setCarregando] = useState(true);
  const [arquivo, setArquivo] = useState<Blob | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  async function carregar() {
    try {
      setCarregando(true);
      setErro(null);
      setArquivo(await obterDocumento());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar o documento.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
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
      {!arquivo ? (
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
          <PaginasPdf arquivo={arquivo} />
        </>
      )}
    </div>
  );
}
