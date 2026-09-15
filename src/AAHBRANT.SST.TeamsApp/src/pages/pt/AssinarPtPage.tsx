import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { FeedbackInline, PageHeader } from '@ui';
import { api, type PermissaoTrabalhoDetalhe } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque (docs/Motor-Assinatura-Eletronica.md §5, etapa 14): mesmo padrão de AssinarDdsPage,
// resolvendo só o cabeçalho e a navegação específicos da PT — o quiosque em si vem de AssinaturaQuiosque.
// Onda 2 Task 7 (camada ui/): PageHeader substitui o card+toolbar manual; erro vira FeedbackInline.
export function AssinarPtPage() {
  const { id } = useParams<{ id: string }>();
  const [detalhe, setDetalhe] = useState<PermissaoTrabalhoDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.permissoesTrabalho
      .obterDetalhe(id)
      .then(setDetalhe)
      .catch(() => setErro('Falha ao carregar os dados da Permissão de Trabalho.'));
  }, [id]);

  if (!id) {
    return <FeedbackInline tom="erro">Permissão de Trabalho não encontrada.</FeedbackInline>;
  }

  const pt = detalhe?.permissaoTrabalho;

  return (
    <div>
      <PageHeader
        titulo={`Assinatura eletrônica — ${pt?.atividadeNome ?? 'Carregando...'}`}
        subtitulo={pt && `Local: ${pt.local} · Data: ${pt.data?.slice(0, 10)}`}
        voltarPara={`/operacao/pt/${id}`}
        rotuloVoltar="Voltar para a PT"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <AssinaturaQuiosque entidadeTipo="PermissaoTrabalho" entidadeId={id} obraId={pt?.obraId ?? ''} />
    </div>
  );
}
