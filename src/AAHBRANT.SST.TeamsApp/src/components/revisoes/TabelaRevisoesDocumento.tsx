import { useState } from 'react';
import {
  Button,
  DataTable,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Spinner,
  StatusChip,
  Text,
  type Coluna,
} from '@ui';
import { PaginasPdf } from '../PaginasPdf';

export interface LinhaRevisaoDocumento {
  id: string;
  numeroRevisao: number;
  dataRevisao: string;
  motivo: string;
  temDocumento?: boolean;
  criadoEmUtc?: string;
  criadoPorNome?: string | null;
}

// Histórico de revisões com o PDF de cada uma (PGR e PCMSO, 10/10/2026). O PDF abre na própria tela
// (PaginasPdf, mesmo motivo do VisualizadorDocumentoPdf: <iframe> com PDF fica em branco no Teams).
export function TabelaRevisoesDocumento({
  revisoes,
  carregando,
  obterDocumento,
  documento,
}: {
  revisoes: LinhaRevisaoDocumento[];
  carregando: boolean;
  obterDocumento: (revisaoId: string) => Promise<Blob | null>;
  documento: 'PGR' | 'PCMSO';
}) {
  const [aberta, setAberta] = useState<LinhaRevisaoDocumento | null>(null);
  const [arquivo, setArquivo] = useState<Blob | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const maisRecenteComPdf = revisoes.filter((r) => r.temDocumento).sort((a, b) => b.numeroRevisao - a.numeroRevisao)[0];

  async function abrir(revisao: LinhaRevisaoDocumento) {
    setAberta(revisao);
    setArquivo(null);
    setErro(null);
    try {
      const blob = await obterDocumento(revisao.id);
      if (!blob) setErro('O PDF desta revisão não foi encontrado.');
      else setArquivo(blob);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao abrir o PDF.');
    }
  }

  const colunas: Coluna<LinhaRevisaoDocumento>[] = [
    {
      chave: 'numeroRevisao',
      rotulo: 'Rev.',
      render: (r) => (
        <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
          <Text weight="semibold">{String(r.numeroRevisao).padStart(2, '0')}</Text>
          {maisRecenteComPdf?.id === r.id && <StatusChip tom="ok">atual</StatusChip>}
        </span>
      ),
    },
    { chave: 'dataRevisao', rotulo: 'Data', render: (r) => r.dataRevisao?.slice(0, 10) ?? '' },
    { chave: 'motivo', rotulo: 'Motivo' },
    {
      chave: 'enviado',
      rotulo: 'Registrado por',
      render: (r) =>
        r.criadoEmUtc ? `${r.criadoPorNome ?? '—'} · ${new Date(r.criadoEmUtc).toLocaleDateString('pt-BR')}` : '—',
    },
    {
      chave: 'pdf',
      rotulo: 'PDF',
      render: (r) =>
        r.temDocumento ? (
          <Button appearance="subtle" onClick={() => abrir(r)}>
            Abrir
          </Button>
        ) : (
          <Text size={200}>sem PDF</Text>
        ),
    },
  ];

  return (
    <>
      <DataTable
        aria-label={`Histórico de revisões do ${documento}`}
        colunas={colunas}
        linhas={revisoes}
        chaveLinha={(r) => r.id}
        carregando={carregando}
        vazio={{ titulo: 'Nenhuma revisão registrada ainda.' }}
      />
      <Dialog open={aberta !== null} onOpenChange={(_, d) => !d.open && setAberta(null)}>
        <DialogSurface style={{ maxWidth: 960, width: '95vw' }}>
          <DialogBody>
            <DialogTitle>
              {documento} · revisão {aberta ? String(aberta.numeroRevisao).padStart(2, '0') : ''}
            </DialogTitle>
            <DialogContent>
              {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
              {!erro && !arquivo && <Spinner label="Carregando PDF..." />}
              {arquivo && <PaginasPdf arquivo={arquivo} altura="70vh" />}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setAberta(null)}>
                Fechar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
}
