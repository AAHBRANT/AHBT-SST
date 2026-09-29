import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { FeedbackInline, PageHeader } from '@ui';
import { api, type AprDetalhe } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Assinatura eletrônica da APR (digital ou reconhecimento facial) — mesmo padrão de AssinarPtPage:
// resolve só o cabeçalho/navegação da APR; o quiosque vem de AssinaturaQuiosque (entidadeTipo "Apr").
// A aba "Assinaturas / ciência" continua existindo para o registro manual de ciência.
export function AssinarAprPage() {
  const { id } = useParams<{ id: string }>();
  const [detalhe, setDetalhe] = useState<AprDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.aprs
      .obterDetalhe(id)
      .then(setDetalhe)
      .catch(() => setErro('Falha ao carregar os dados da APR.'));
  }, [id]);

  if (!id) {
    return <FeedbackInline tom="erro">APR não encontrada.</FeedbackInline>;
  }

  const apr = detalhe?.apr;

  return (
    <div>
      <PageHeader
        titulo={`Assinatura eletrônica — ${apr?.atividadeNome ?? 'Carregando...'}`}
        subtitulo={apr && `${apr.numeroApr ? `APR ${apr.numeroApr} · ` : ''}Local: ${apr.local} · Data: ${apr.data?.slice(0, 10)}`}
        voltarPara={`/operacao/apr/${id}`}
        rotuloVoltar="Voltar para a APR"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <AssinaturaQuiosque entidadeTipo="Apr" entidadeId={id} obraId={apr?.obraId ?? ''} />
    </div>
  );
}
