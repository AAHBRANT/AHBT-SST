import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { FeedbackInline, PageHeader } from '@ui';
import { api, type Dds } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque (docs/Motor-Assinatura-Eletronica.md §5, etapa 6 e 14): fica ao lado do registro de
// participante por foto que já existe em DdsDetalhePage — não o substitui. O quiosque em si (leitura de
// crachá/QR + PIN e lista de assinaturas) foi extraído para AssinaturaQuiosque (etapa 14), reutilizável
// por outros módulos; esta página só resolve o cabeçalho e a navegação específicos do DDS.
// Onda 2 (Task 14): conversões 2 (estilos.card/toolbar → PageHeader) e 4 (erro → FeedbackInline).
// AssinaturaQuiosque é componente compartilhado fora de src/pages/ (mesmo critério já usado nos
// *Dialog.tsx de assinatura no piloto 1) — fica como está, fora do escopo desta task.
export function AssinarDdsPage() {
  const { id } = useParams<{ id: string }>();
  const [dds, setDds] = useState<Dds | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.dds
      .obterDetalhe(id)
      .then((det) => setDds(det.dds))
      .catch(() => setErro('Falha ao carregar os dados do DDS.'));
  }, [id]);

  if (!id) {
    return <FeedbackInline tom="erro">DDS não encontrado.</FeedbackInline>;
  }

  return (
    <div>
      <PageHeader
        titulo={`Assinatura eletrônica — ${dds ? dds.atividadesNomes.join(', ') || 'DDS do dia' : 'Carregando...'}`}
        subtitulo={dds ? `Obra: ${dds.obraNome} · Data: ${dds.data?.slice(0, 10)}` : undefined}
        voltarPara={`/prevencao/dds/dia/${id}`}
        rotuloVoltar="Voltar para o DDS"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <AssinaturaQuiosque entidadeTipo="Dds" entidadeId={id} obraId={dds?.obraId ?? ''} />
    </div>
  );
}
