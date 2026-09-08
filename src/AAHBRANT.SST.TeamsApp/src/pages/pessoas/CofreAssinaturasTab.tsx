import { useState } from 'react';
import { Button, Card, DataTable, FeedbackInline, type Coluna } from '@ui';
import { ArrowDownload24Regular } from '@fluentui/react-icons';
import { api, metodoAutenticacaoAssinaturaLabel, type AssinaturaPerfil } from '../../lib/api';
import { AssinaturaTab } from './AssinaturaTab';

interface CofreAssinaturasTabProps {
  trabalhadorId: string;
  assinaturas: AssinaturaPerfil[];
}

export function CofreAssinaturasTab({ trabalhadorId, assinaturas }: CofreAssinaturasTabProps) {
  const [erro, setErro] = useState<string | null>(null);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);

  async function baixarComprovante(documentoAssinaturaId: string) {
    try {
      setErro(null);
      setBaixandoId(documentoAssinaturaId);
      const blob = await api.assinatura.baixarPdf(documentoAssinaturaId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `comprovante-assinatura-${documentoAssinaturaId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o comprovante em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  const colunas: Coluna<AssinaturaPerfil>[] = [
    { chave: 'documento', rotulo: 'Documento', render: (a) => a.entidadeTipo },
    { chave: 'metodo', rotulo: 'Método', render: (a) => metodoAutenticacaoAssinaturaLabel[a.metodo] },
    { chave: 'dataHora', rotulo: 'Data/Hora', render: (a) => new Date(a.assinadoEm).toLocaleString('pt-BR') },
    { chave: 'ip', rotulo: 'IP', render: (a) => a.ipAddress ?? 'Não registrado' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card titulo="Cofre de assinaturas">
        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

        <DataTable
          aria-label="Cofre de assinaturas"
          colunas={colunas}
          linhas={assinaturas}
          chaveLinha={(a) => a.documentoAssinaturaId}
          vazio={{ titulo: 'Nenhuma assinatura registrada para este funcionário.' }}
          acoesLinha={(a) => (
            <Button
              appearance="subtle"
              icon={<ArrowDownload24Regular />}
              onClick={() => baixarComprovante(a.documentoAssinaturaId)}
              disabled={!a.temPdf || baixandoId === a.documentoAssinaturaId}
              aria-label="Baixar comprovante"
              title={a.temPdf ? 'Baixar comprovante em PDF' : 'PDF ainda não disponível'}
            />
          )}
        />
      </Card>

      <AssinaturaTab trabalhadorId={trabalhadorId} />
    </div>
  );
}
