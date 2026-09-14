import { useEffect, useRef, useState } from 'react';
import { Spinner, Text } from '@fluentui/react-components';
import { usePageStyles } from '../pages/pageStyles';

interface PaginasPdfProps {
  arquivo: Blob;
  /** Altura da área rolável. Padrão: 80vh. */
  altura?: string;
}

// Renderiza todas as páginas de um PDF em <canvas>, uma abaixo da outra, com rolagem nativa.
//
// Por que não <iframe>/<object> com o PDF: dentro do Teams (desktop e web) o WebView não tem o
// visualizador de PDF do navegador — a área fica simplesmente em branco, enquanto no Edge/Chrome
// o mesmo PDF aparece normal (13/09, PGR em hml: "no navegador aparece, no Teams não"). O pdf.js
// desenha o documento com Canvas 2D, que funciona em qualquer WebView, sem depender de plugin.
//
// O pdf.js (~400 KB) e o worker dele só são baixados quando alguém abre um documento — import
// dinâmico — para não inflar o bundle principal (que também tem limite no precache do service
// worker, ver vite.config.ts).
export function PaginasPdf({ arquivo, altura = '80vh' }: PaginasPdfProps) {
  const estilos = usePageStyles();
  const paginasRef = useRef<HTMLDivElement>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [totalPaginas, setTotalPaginas] = useState(0);

  useEffect(() => {
    let cancelado = false;
    const container = paginasRef.current;
    if (!container) return undefined;

    (async () => {
      try {
        setCarregando(true);
        setErro(null);
        container.replaceChildren();

        const [pdfjs, worker] = await Promise.all([
          import('pdfjs-dist'),
          import('pdfjs-dist/build/pdf.worker.min.mjs?url'),
        ]);
        pdfjs.GlobalWorkerOptions.workerSrc = worker.default;

        const dados = new Uint8Array(await arquivo.arrayBuffer());
        const tarefa = pdfjs.getDocument({ data: dados });
        const documento = await tarefa.promise;
        if (cancelado) {
          void tarefa.destroy();
          return;
        }
        setTotalPaginas(documento.numPages);

        // Largura útil do container (descontando o padding) — cada página é escalada para ocupar
        // a largura toda, como um leitor de PDF normal. devicePixelRatio evita texto borrado em
        // tela de alta densidade (o canvas é desenhado maior e exibido menor via CSS).
        const larguraUtil = Math.max(container.clientWidth - 32, 320);
        const densidade = window.devicePixelRatio || 1;

        for (let numero = 1; numero <= documento.numPages; numero++) {
          if (cancelado) break;
          const pagina = await documento.getPage(numero);
          const base = pagina.getViewport({ scale: 1 });
          const viewport = pagina.getViewport({ scale: larguraUtil / base.width });

          const canvas = document.createElement('canvas');
          canvas.width = Math.floor(viewport.width * densidade);
          canvas.height = Math.floor(viewport.height * densidade);
          canvas.style.width = `${Math.floor(viewport.width)}px`;
          canvas.style.height = `${Math.floor(viewport.height)}px`;
          canvas.style.display = 'block';
          canvas.style.margin = '0 auto 12px';
          canvas.style.background = '#ffffff';
          canvas.style.boxShadow = '0 1px 4px rgba(0, 0, 0, 0.25)';
          canvas.setAttribute('aria-label', `Página ${numero} de ${documento.numPages}`);
          canvas.setAttribute('data-pagina-pdf', String(numero));
          container.appendChild(canvas);

          await pagina.render({
            canvas,
            viewport,
            transform: densidade !== 1 ? [densidade, 0, 0, densidade, 0, 0] : undefined,
          }).promise;
          pagina.cleanup();

          // A primeira página já na tela é o suficiente para tirar o spinner; as demais vão
          // aparecendo conforme renderizam.
          if (numero === 1) setCarregando(false);
        }

        void tarefa.destroy();
      } catch (e) {
        if (!cancelado) {
          setErro(e instanceof Error ? e.message : 'Falha ao exibir o documento.');
        }
      } finally {
        if (!cancelado) setCarregando(false);
      }
    })();

    return () => {
      cancelado = true;
    };
  }, [arquivo]);

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}
      {carregando && !erro && (
        <div style={{ padding: 32, textAlign: 'center' }}>
          <Spinner label="Exibindo documento..." />
        </div>
      )}
      <div
        ref={paginasRef}
        role="document"
        aria-label={totalPaginas ? `Documento PDF, ${totalPaginas} páginas` : 'Documento PDF'}
        style={{
          height: carregando && !erro ? 0 : altura,
          overflowY: 'auto',
          overflowX: 'hidden',
          padding: carregando && !erro ? 0 : 16,
          boxSizing: 'border-box',
          background: '#e6e6e6',
          borderRadius: 6,
        }}
      />
    </div>
  );
}
