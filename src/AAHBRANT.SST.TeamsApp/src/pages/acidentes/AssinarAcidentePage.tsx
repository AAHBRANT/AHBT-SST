import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { FeedbackInline, PageHeader } from '@ui';
import { api, tipoOcorrenciaLabel, type AcidenteDetalhe } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque da ocorrência (acidente, incidente, quase acidente) — mesmo padrão de
// AssinarInspecaoPage: o cabeçalho e a navegação são da ocorrência; o quiosque (digital, facial) vem de
// AssinaturaQuiosque com entidadeTipo "Acidente". O documento de assinatura é criado ao abrir a tela.
export function AssinarAcidentePage() {
  const { id } = useParams<{ id: string }>();
  const [detalhe, setDetalhe] = useState<AcidenteDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.acidentes
      .obterDetalhe(id)
      .then(setDetalhe)
      .catch(() => setErro('Falha ao carregar os dados da ocorrência.'));
  }, [id]);

  if (!id) {
    return <FeedbackInline tom="erro">Ocorrência não encontrada.</FeedbackInline>;
  }

  const a = detalhe?.acidente;

  return (
    <div>
      <PageHeader
        titulo={`Assinatura eletrônica — ${a ? tipoOcorrenciaLabel[a.tipo] : 'Carregando...'}`}
        subtitulo={a && `Obra: ${a.obraNome ?? '—'} · Local: ${a.local}.`}
        voltarPara={`/acidentes/${id}`}
        rotuloVoltar="Voltar para a ocorrência"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <AssinaturaQuiosque entidadeTipo="Acidente" entidadeId={id} obraId={a?.obraId ?? ''} />
    </div>
  );
}
