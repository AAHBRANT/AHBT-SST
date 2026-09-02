import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Text } from '@fluentui/react-components';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { api, type Dds } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque (docs/Motor-Assinatura-Eletronica.md §5, etapa 6 e 14): fica ao lado do registro de
// participante por foto que já existe em DdsDetalhePage — não o substitui. O quiosque em si (leitura de
// crachá/QR + PIN e lista de assinaturas) foi extraído para AssinaturaQuiosque (etapa 14), reutilizável
// por outros módulos; esta página só resolve o cabeçalho e a navegação específicos do DDS.
export function AssinarDdsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
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
    return <Text>DDS não encontrado.</Text>;
  }

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate(`/prevencao/dds/dia/${id}`)}
        style={{ marginBottom: 12 }}
      >
        Voltar para o DDS
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Assinatura eletrônica — {dds ? (dds.atividadesNomes.join(', ') || 'DDS do dia') : 'Carregando...'}
        </Text>
        {dds && (
          <Text style={{ display: 'block', marginTop: 4 }}>
            Obra: {dds.obraNome} · Data: {dds.data?.slice(0, 10)}
          </Text>
        )}
      </div>

      <AssinaturaQuiosque entidadeTipo="Dds" entidadeId={id} />
    </div>
  );
}
