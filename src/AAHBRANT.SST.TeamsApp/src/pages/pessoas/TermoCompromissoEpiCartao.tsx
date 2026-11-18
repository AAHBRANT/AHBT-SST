import { useEffect, useState } from 'react';
import { BotaoAcao, Button, Card, FeedbackInline, Legenda, Text, designTokens, tokensUi, useTipografia, type Tom } from '@ui';
import { ArrowDownload24Regular, Checkmark24Regular, Clock24Regular, Eye24Regular, Signature24Regular } from '@fluentui/react-icons';
import { api, SituacaoTermoCompromissoEpi, type TermoCompromissoEpi } from '../../lib/api';
import { TermoCompromissoEpiDialog } from '../../components/assinatura/TermoCompromissoEpiDialog';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';

function formatarData(iso?: string | null) {
  if (!iso) return '';
  const [ano, mes, dia] = iso.slice(0, 10).split('-');
  return `${dia}/${mes}/${ano}`;
}

// Cartão unificado de Documentação de EPI (NR-06): Termo de Recebimento e Compromisso de Uso + atalhos
// para visualizar e baixar a Ficha de Controle de EPI (NR-06) em um único card sem repetição visual.
export function TermoCompromissoEpiCartao({ trabalhadorId, trabalhadorNome }: { trabalhadorId: string; trabalhadorNome?: string }) {
  const [termo, setTermo] = useState<TermoCompromissoEpi | null>(null);
  const [aberto, setAberto] = useState(false);
  const [baixandoFicha, setBaixandoFicha] = useState(false);
  const [erroFicha, setErroFicha] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const tipo = useTipografia();
  const nomeArquivoFicha = `ficha-epi-${trabalhadorId}.pdf`;

  async function carregar() {
    setTermo(await api.termosCompromissoEpi.obter(trabalhadorId).catch(() => null));
  }

  async function baixarFicha() {
    try {
      setErroFicha(null);
      setBaixandoFicha(true);
      salvarBlob(await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId), nomeArquivoFicha);
    } catch (e) {
      setErroFicha(e instanceof Error ? e.message : 'Falha ao baixar a ficha de EPI.');
    } finally {
      setBaixandoFicha(false);
    }
  }

  useEffect(() => {
    void carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  const pendente = !termo || termo.situacao === SituacaoTermoCompromissoEpi.Pendente;
  const tom: Tom = pendente ? 'atencao' : termo?.situacao === SituacaoTermoCompromissoEpi.Digital ? 'ok' : 'info';
  const situacao = pendente
    ? 'Pendente de assinatura'
    : termo?.situacao === SituacaoTermoCompromissoEpi.Digital
      ? `Assinado digitalmente${termo.dataAssinatura ? ` em ${formatarData(termo.dataAssinatura)}` : ''}`
      : `Assinado em papel em ${formatarData(termo?.dataAssinatura)}`;

  return (
    <Card
      titulo="Documentação de EPI (NR-06)"
      acoes={
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <BotaoAcao
            tom="ver"
            icon={<Eye24Regular />}
            onClick={() =>
              void visualizar({
                titulo: 'Ficha de EPI do funcionário',
                nomeArquivo: nomeArquivoFicha,
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
            onClick={baixarFicha}
            disabled={baixandoFicha}
            aria-label="Baixar ficha de EPI em PDF"
          >
            Baixar ficha (PDF)
          </BotaoAcao>
        </div>
      }
    >
      {dialogoVisualizador}
      {erroFicha && (
        <div style={{ marginBottom: tokensUi.espaco.md }}>
          <FeedbackInline tom="erro" aoFechar={() => setErroFicha(null)}>
            {erroFicha}
          </FeedbackInline>
        </div>
      )}
      <div style={{ display: 'flex', alignItems: 'center', gap: tokensUi.espaco.lg, flexWrap: 'wrap' }}>
        <span
          aria-hidden="true"
          style={{
            width: 48,
            height: 48,
            borderRadius: tokensUi.raio.md,
            display: 'grid',
            placeItems: 'center',
            flexShrink: 0,
            backgroundColor: tokensUi.status[tom].fundo,
            color: tokensUi.status[tom].tinta,
          }}
        >
          {pendente ? <Clock24Regular /> : <Checkmark24Regular />}
        </span>
        <div style={{ flex: '1 1 260px', minWidth: 0 }}>
          <div className={tipo.micro} style={{ color: designTokens.colorNeutralMedium }}>
            Termo de recebimento e compromisso de uso
          </div>
          <Text weight="semibold" size={400} style={{ display: 'block', color: pendente ? tokensUi.status.atencao.tinta : undefined }}>
            {situacao}
          </Text>
          <Legenda>
            O termo é assinado uma vez por funcionário. Se ele já assinou em papel, registre aqui — o sistema não gera
            assinatura eletrônica no lugar dele.
          </Legenda>
        </div>
        <Button appearance={pendente ? 'primary' : 'secondary'} icon={<Signature24Regular />} onClick={() => setAberto(true)}>
          {pendente ? 'Assinar termo' : 'Gerenciar termo'}
        </Button>
      </div>
      <TermoCompromissoEpiDialog
        open={aberto}
        onClose={() => setAberto(false)}
        trabalhadorId={trabalhadorId}
        trabalhadorNome={trabalhadorNome}
        aoAlterar={() => void carregar()}
      />
    </Card>
  );
}
