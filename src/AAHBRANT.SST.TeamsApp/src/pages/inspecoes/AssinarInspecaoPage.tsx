import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { FeedbackInline, PageHeader } from '@ui';
import { api, tipoInspecaoLabel, type InspecaoDetalhe } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque (docs/Motor-Assinatura-Eletronica.md §5, etapa 14): mesmo padrão de
// AssinarPtPage/AssinarDdsPage, resolvendo só o cabeçalho e a navegação específicos da Inspeção — o
// quiosque em si vem de AssinaturaQuiosque. Backend já cria o DocumentoAssinatura ao encerrar a
// inspeção (EncerrarInspecaoCommand); esta página só falta pra alguém de fato assinar.
// Onda 2 Task 10 (camada ui/): PageHeader substitui o card+toolbar manual; erro vira FeedbackInline.
export function AssinarInspecaoPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<InspecaoDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.inspecoes
      .obterDetalhe(id)
      .then(setDetalhe)
      .catch(() => setErro('Falha ao carregar os dados da inspeção.'));
  }, [id]);

  if (!id) {
    return <FeedbackInline tom="erro">Inspeção não encontrada.</FeedbackInline>;
  }

  const inspecao = detalhe?.inspecao;

  return (
    <div>
      <PageHeader
        titulo={`Assinatura eletrônica — ${inspecao ? tipoInspecaoLabel[inspecao.tipoInspecao] : 'Carregando...'}`}
        subtitulo={inspecao && `Obra: ${inspecao.obraNome} · Data: ${inspecao.data?.slice(0, 10)}. Ao assinar, a inspeção é marcada como Concluída.`}
        voltarPara={`/prevencao/inspecoes/${id}`}
        rotuloVoltar="Voltar para a inspeção"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {/* Assinou: volta ao detalhe com o "Imprimir adesivo" já aberto (toda inspeção sai com adesivo). */}
      <AssinaturaQuiosque
        entidadeTipo="Inspecao"
        entidadeId={id}
        obraId={inspecao?.obraId ?? ''}
        aoFinalizarAssinatura={() => navigate(`/prevencao/inspecoes/${id}?adesivo=imprimir`, { replace: true })}
      />
    </div>
  );
}
