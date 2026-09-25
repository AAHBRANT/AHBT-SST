import { useCallback, useRef, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Spinner,
} from '@fluentui/react-components';
import { ArrowDownload24Regular, Dismiss24Regular } from '@fluentui/react-icons';
import { FeedbackInline } from '../ui';
import { PaginasPdf } from './PaginasPdf';

export interface OpcoesVisualizacao {
  titulo: string;
  /** Nome sugerido ao baixar pelo botão da própria janela. */
  nomeArquivo: string;
  /** Mesma chamada que o botão "Baixar" da tela já usa. */
  obter: () => Promise<Blob>;
}

// Salva o blob como arquivo — mesmo mecanismo que cada tela já usava no botão "Baixar".
export function salvarBlob(blob: Blob, nomeArquivo: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = nomeArquivo;
  link.click();
  URL.revokeObjectURL(url);
}

// Visualizar documento sem baixar (pedido do usuário, 25/09/2026: "DDS, EPI, certificados... devemos
// ter a opção de visualizar"). Janela única para todas as telas: PDF desenhado em canvas pelo pdf.js
// (PaginasPdf), porque <iframe> com PDF fica em branco dentro do Teams — era exatamente o que
// acontecia no preview de certificado. Imagem (certificado escaneado) aparece como <img>.
// "Baixar" reaproveita o arquivo já carregado, sem nova ida ao servidor.
export function useVisualizadorPdf() {
  const [aberto, setAberto] = useState(false);
  const [opcoes, setOpcoes] = useState<OpcoesVisualizacao | null>(null);
  const [arquivo, setArquivo] = useState<Blob | null>(null);
  const [urlImagem, setUrlImagem] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const requisicaoRef = useRef(0);

  const limparImagem = useCallback(() => {
    setUrlImagem((atual) => {
      if (atual) URL.revokeObjectURL(atual);
      return null;
    });
  }, []);

  const visualizar = useCallback(
    async (entrada: OpcoesVisualizacao) => {
      const requisicao = ++requisicaoRef.current;
      limparImagem();
      setOpcoes(entrada);
      setArquivo(null);
      setErro(null);
      setAberto(true);
      try {
        const blob = await entrada.obter();
        if (requisicao !== requisicaoRef.current) return;
        setArquivo(blob);
        if (blob.type.startsWith('image/')) setUrlImagem(URL.createObjectURL(blob));
      } catch (e) {
        if (requisicao === requisicaoRef.current)
          setErro(e instanceof Error ? e.message : 'Falha ao carregar o documento.');
      }
    },
    [limparImagem],
  );

  function fechar() {
    requisicaoRef.current++;
    setAberto(false);
    setArquivo(null);
    limparImagem();
  }

  const dialogoVisualizador = (
    <Dialog open={aberto} onOpenChange={(_, d) => { if (!d.open) fechar(); }}>
      <DialogSurface style={{ maxWidth: 'min(1100px, 96vw)', width: '96vw' }}>
        <DialogBody>
          <DialogTitle
            action={<Button appearance="subtle" aria-label="Fechar" icon={<Dismiss24Regular />} onClick={fechar} />}
          >
            {opcoes?.titulo}
          </DialogTitle>
          <DialogContent>
            {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
            {!erro && !arquivo && (
              <div style={{ padding: 48, textAlign: 'center' }}>
                <Spinner label="Carregando documento..." />
              </div>
            )}
            {arquivo && urlImagem && (
              <div style={{ maxHeight: '75vh', overflow: 'auto', textAlign: 'center' }}>
                <img src={urlImagem} alt={opcoes?.titulo ?? 'Documento'} style={{ maxWidth: '100%' }} />
              </div>
            )}
            {arquivo && !urlImagem && <PaginasPdf arquivo={arquivo} altura="75vh" />}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={fechar}>Fechar</Button>
            <Button
              appearance="primary"
              icon={<ArrowDownload24Regular />}
              disabled={!arquivo}
              onClick={() => arquivo && opcoes && salvarBlob(arquivo, opcoes.nomeArquivo)}
            >
              Baixar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );

  return { visualizar, dialogoVisualizador };
}
