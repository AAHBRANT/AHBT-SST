import { useEffect, useState } from 'react';
import { Button, Card, Legenda, Text, designTokens, tokensUi, useTipografia, type Tom } from '@ui';
import { Checkmark24Regular, Clock24Regular, Signature24Regular } from '@fluentui/react-icons';
import { api, SituacaoTermoCompromissoEpi, type TermoCompromissoEpi } from '../../lib/api';
import { TermoCompromissoEpiDialog } from '../../components/assinatura/TermoCompromissoEpiDialog';

function formatarData(iso?: string | null) {
  if (!iso) return '';
  const [ano, mes, dia] = iso.slice(0, 10).split('-');
  return `${dia}/${mes}/${ano}`;
}

// Cartão do Termo de Recebimento e Compromisso de Uso no perfil do funcionário (aba EPI): mostra a
// situação do termo em primeiro lugar (Proposta 1 de cards, 21/10) e abre o diálogo de "Assinatura de
// termo de recebimento e compromisso" (assinar agora por digital/facial ou registrar que já assinou em papel).
export function TermoCompromissoEpiCartao({ trabalhadorId, trabalhadorNome }: { trabalhadorId: string; trabalhadorNome?: string }) {
  const [termo, setTermo] = useState<TermoCompromissoEpi | null>(null);
  const [aberto, setAberto] = useState(false);
  const tipo = useTipografia();

  async function carregar() {
    setTermo(await api.termosCompromissoEpi.obter(trabalhadorId).catch(() => null));
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
    <Card>
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
