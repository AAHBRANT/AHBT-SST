import { useEffect, useState } from 'react';
import { Card, FeedbackInline } from '@ui';
import { api, type PcmsoRevisao } from '../../lib/api';
import { TabelaRevisoesDocumento } from './TabelaRevisoesDocumento';

// Aba "Revisões" do PCMSO (10/10/2026): histórico com o PDF de cada revisão. A revisão nova entra pelo
// botão "Nova revisão" da aba PCMSO.
export function PcmsoRevisoesTab({ pcmsoId }: { pcmsoId: string }) {
  const [revisoes, setRevisoes] = useState<PcmsoRevisao[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let ativo = true;
    setCarregando(true);
    api.pcmsos
      .listarRevisoes(pcmsoId)
      .then((lista) => {
        if (ativo) setRevisoes(lista);
      })
      .catch((e: unknown) => {
        if (ativo) setErro(e instanceof Error ? e.message : 'Falha ao carregar revisões.');
      })
      .finally(() => {
        if (ativo) setCarregando(false);
      });
    return () => {
      ativo = false;
    };
  }, [pcmsoId]);

  return (
    <Card titulo="Histórico de revisões">
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <TabelaRevisoesDocumento
        documento="PCMSO"
        revisoes={revisoes}
        carregando={carregando}
        obterDocumento={(revisaoId) => api.pcmsos.obterDocumentoRevisao(revisaoId)}
      />
    </Card>
  );
}
