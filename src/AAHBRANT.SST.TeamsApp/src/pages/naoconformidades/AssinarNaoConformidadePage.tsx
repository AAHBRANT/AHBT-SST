import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { FeedbackInline, PageHeader } from '@ui';
import { api, type NaoConformidadeDetalhe } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque da ocorrência (Não Conformidade) — mesmo padrão de AssinarInspecaoPage. O backend
// já cria o DocumentoAssinatura ao encerrar (EncerrarNaoConformidadeCommand); faltava a tela para
// alguém de fato assinar. A NC não guarda obra própria: o reconhecimento facial recebe vazio, como
// nas demais telas de assinatura que não têm obra no detalhe.
export function AssinarNaoConformidadePage() {
  const { id } = useParams<{ id: string }>();
  const [detalhe, setDetalhe] = useState<NaoConformidadeDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.naoConformidades
      .obterDetalhe(id)
      .then(setDetalhe)
      .catch(() => setErro('Falha ao carregar os dados da ocorrência.'));
  }, [id]);

  if (!id) {
    return <FeedbackInline tom="erro">Ocorrência não encontrada.</FeedbackInline>;
  }

  const nc = detalhe?.naoConformidade;

  return (
    <div>
      <PageHeader
        titulo="Assinatura eletrônica — Ocorrência"
        subtitulo={nc?.descricao}
        voltarPara={`/nao-conformidades/${id}`}
        rotuloVoltar="Voltar para a ocorrência"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <AssinaturaQuiosque entidadeTipo="NaoConformidade" entidadeId={id} obraId="" />
    </div>
  );
}
