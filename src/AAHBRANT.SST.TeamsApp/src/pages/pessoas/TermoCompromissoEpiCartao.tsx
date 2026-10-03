import { useEffect, useState } from 'react';
import { Card, StatusChip, Text } from '@ui';
import { Button } from '@fluentui/react-components';
import { Signature24Regular } from '@fluentui/react-icons';
import { api, SituacaoTermoCompromissoEpi, type TermoCompromissoEpi } from '../../lib/api';
import { TermoCompromissoEpiDialog } from '../../components/assinatura/TermoCompromissoEpiDialog';

function formatarData(iso?: string | null) {
  if (!iso) return '';
  const [ano, mes, dia] = iso.slice(0, 10).split('-');
  return `${dia}/${mes}/${ano}`;
}

// Cartão do Termo de Recebimento e Compromisso de Uso no perfil do funcionário (aba "EPI & Matriz"):
// mostra a situação do termo e abre o diálogo de "Assinatura de termo de recebimento e compromisso"
// (assinar agora por digital/facial ou registrar que já assinou em papel).
export function TermoCompromissoEpiCartao({ trabalhadorId, trabalhadorNome }: { trabalhadorId: string; trabalhadorNome?: string }) {
  const [termo, setTermo] = useState<TermoCompromissoEpi | null>(null);
  const [aberto, setAberto] = useState(false);

  async function carregar() {
    setTermo(await api.termosCompromissoEpi.obter(trabalhadorId).catch(() => null));
  }

  useEffect(() => {
    void carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  return (
    <Card
      titulo="Termo de Recebimento e Compromisso de Uso"
      acoes={
        <Button appearance="primary" icon={<Signature24Regular />} onClick={() => setAberto(true)}>
          Assinatura de termo de recebimento e compromisso
        </Button>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {termo?.situacao === SituacaoTermoCompromissoEpi.Digital && <StatusChip tom="ok">Termo assinado digitalmente</StatusChip>}
        {termo?.situacao === SituacaoTermoCompromissoEpi.Manual && (
          <StatusChip tom="info">Termo assinado em papel em {formatarData(termo.dataAssinatura)}</StatusChip>
        )}
        {termo?.situacao === SituacaoTermoCompromissoEpi.Pendente && <StatusChip tom="atencao">Termo pendente</StatusChip>}
        <Text size={200}>
          O termo é assinado uma vez por funcionário. Se ele já assinou em papel, registre aqui — o sistema não gera assinatura
          eletrônica no lugar dele.
        </Text>
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
