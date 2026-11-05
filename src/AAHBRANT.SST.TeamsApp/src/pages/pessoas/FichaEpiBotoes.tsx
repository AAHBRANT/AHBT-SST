import { useState } from 'react';
import { ArrowDownload24Regular, Eye24Regular } from '@fluentui/react-icons';
import { BotaoAcao, Card, FeedbackInline, Text } from '@ui';
import { api } from '../../lib/api';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';

// Ficha de controle de EPI (NR-06) do trabalhador, no perfil (aba EPI): mesma ficha do módulo EPI,
// gerada pelo endpoint ficha-trabalhador/{id}/pdf — aqui só os atalhos de ver e baixar.
export function FichaEpiBotoes({ trabalhadorId }: { trabalhadorId: string }) {
  const [baixando, setBaixando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const nomeArquivo = `ficha-epi-${trabalhadorId}.pdf`;

  async function baixar() {
    try {
      setErro(null);
      setBaixando(true);
      salvarBlob(await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId), nomeArquivo);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ficha de EPI.');
    } finally {
      setBaixando(false);
    }
  }

  return (
    <Card
      densidade="compacta"
      titulo="Ficha de EPI (NR-06)"
      acoes={
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <BotaoAcao
            tom="ver"
            icon={<Eye24Regular />}
            onClick={() =>
              void visualizar({
                titulo: 'Ficha de EPI do funcionário',
                nomeArquivo,
                obter: () => api.entregasEpi.baixarFichaTrabalhador(trabalhadorId),
              })
            }
            aria-label="Visualizar ficha de EPI"
          >
            Visualizar ficha
          </BotaoAcao>
          <BotaoAcao
            tom="baixar"
            icon={<ArrowDownload24Regular />}
            onClick={baixar}
            disabled={baixando}
            aria-label="Baixar ficha de EPI em PDF"
          >
            Baixar ficha (PDF)
          </BotaoAcao>
        </div>
      }
    >
      {dialogoVisualizador}
      <Text style={{ color: 'var(--sst-color-neutral-medium)' }}>
        Todas as entregas e devoluções de EPI do funcionário, com as assinaturas, no modelo de ficha da NR-06.
      </Text>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
    </Card>
  );
}
