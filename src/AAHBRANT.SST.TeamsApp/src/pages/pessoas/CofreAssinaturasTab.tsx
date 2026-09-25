import { useState } from 'react';
import { Button, Card, DataTable, FeedbackInline, type Coluna } from '@ui';
import { ArrowDownload24Regular, Eye24Regular, Image24Regular } from '@fluentui/react-icons';
import { api, metodoAutenticacaoAssinaturaLabel, type AssinaturaPerfil } from '../../lib/api';
import { AssinaturaTab } from './AssinaturaTab';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';

interface CofreAssinaturasTabProps {
  trabalhadorId: string;
  assinaturas: AssinaturaPerfil[];
}

export function CofreAssinaturasTab({ trabalhadorId, assinaturas }: CofreAssinaturasTabProps) {
  const [erro, setErro] = useState<string | null>(null);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [abrindoFotoId, setAbrindoFotoId] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  async function baixarComprovante(documentoAssinaturaId: string) {
    try {
      setErro(null);
      setBaixandoId(documentoAssinaturaId);
      salvarBlob(await api.assinatura.baixarPdf(documentoAssinaturaId), `comprovante-assinatura-${documentoAssinaturaId}.pdf`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o comprovante em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  // Mesmo comprovante do "Baixar", aberto na janela de visualização.
  function visualizarComprovante(a: AssinaturaPerfil) {
    void visualizar({
      titulo: `Comprovante de assinatura — ${a.entidadeTipo}`,
      nomeArquivo: `comprovante-assinatura-${a.documentoAssinaturaId}.pdf`,
      obter: () => api.assinatura.baixarPdf(a.documentoAssinaturaId),
    });
  }

  async function abrirFotoEvidencia(signatarioId: string) {
    try {
      setErro(null);
      setAbrindoFotoId(signatarioId);
      const blob = await api.assinatura.baixarFotoEvidencia(signatarioId);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao abrir a foto da assinatura.');
    } finally {
      setAbrindoFotoId(null);
    }
  }

  const colunas: Coluna<AssinaturaPerfil>[] = [
    { chave: 'documento', rotulo: 'Documento', render: (a) => a.entidadeTipo },
    { chave: 'metodo', rotulo: 'Método', render: (a) => metodoAutenticacaoAssinaturaLabel[a.metodo] },
    { chave: 'dataHora', rotulo: 'Data/Hora', render: (a) => new Date(a.assinadoEm).toLocaleString('pt-BR') },
    { chave: 'ip', rotulo: 'IP', render: (a) => a.ipAddress ?? 'Não registrado' },
    {
      chave: 'evidencia',
      rotulo: 'Evidência',
      render: (a) => a.temFotoEvidencia
        ? `Foto facial${a.fotoEvidenciaHash ? ` · ${a.fotoEvidenciaHash.slice(0, 12)}...` : ''}`
        : 'Sem foto',
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card titulo="Cofre de assinaturas">
        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
        {dialogoVisualizador}

        <DataTable
          aria-label="Cofre de assinaturas"
          colunas={colunas}
          linhas={assinaturas}
          chaveLinha={(a) => a.signatarioId}
          vazio={{ titulo: 'Nenhuma assinatura registrada para este funcionário.' }}
          acoesLinha={(a) => (
            <>
              <Button
                appearance="subtle"
                icon={<Image24Regular />}
                onClick={() => abrirFotoEvidencia(a.signatarioId)}
                disabled={!a.temFotoEvidencia || abrindoFotoId === a.signatarioId}
                aria-label="Abrir foto da assinatura"
                title={a.temFotoEvidencia ? 'Abrir foto capturada na assinatura' : 'Foto não disponível'}
              />
              <Button
                appearance="subtle"
                icon={<Eye24Regular />}
                onClick={() => visualizarComprovante(a)}
                disabled={!a.temPdf}
                aria-label="Visualizar comprovante"
                title={a.temPdf ? 'Visualizar comprovante' : 'PDF ainda não disponível'}
              />
              <Button
                appearance="subtle"
                icon={<ArrowDownload24Regular />}
                onClick={() => baixarComprovante(a.documentoAssinaturaId)}
                disabled={!a.temPdf || baixandoId === a.documentoAssinaturaId}
                aria-label="Baixar comprovante"
                title={a.temPdf ? 'Baixar comprovante em PDF' : 'PDF ainda não disponível'}
              />
            </>
          )}
        />
      </Card>

      <AssinaturaTab trabalhadorId={trabalhadorId} />
    </div>
  );
}
