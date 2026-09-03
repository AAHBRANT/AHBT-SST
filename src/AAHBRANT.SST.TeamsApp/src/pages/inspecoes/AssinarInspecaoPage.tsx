import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Text } from '@fluentui/react-components';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { api, tipoInspecaoLabel, type InspecaoDetalhe } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque (docs/Motor-Assinatura-Eletronica.md §5, etapa 14): mesmo padrão de
// AssinarPtPage/AssinarDdsPage, resolvendo só o cabeçalho e a navegação específicos da Inspeção — o
// quiosque em si vem de AssinaturaQuiosque. Backend já cria o DocumentoAssinatura ao encerrar a
// inspeção (EncerrarInspecaoCommand); esta página só falta pra alguém de fato assinar.
export function AssinarInspecaoPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
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
    return <Text>Inspeção não encontrada.</Text>;
  }

  const inspecao = detalhe?.inspecao;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate(`/prevencao/inspecoes/${id}`)}
        style={{ marginBottom: 12 }}
      >
        Voltar para a inspeção
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Assinatura eletrônica — {inspecao ? tipoInspecaoLabel[inspecao.tipoInspecao] : 'Carregando...'}
        </Text>
        {inspecao && (
          <Text style={{ display: 'block', marginTop: 4 }}>
            Obra: {inspecao.obraNome} · Data: {inspecao.data?.slice(0, 10)}
          </Text>
        )}
      </div>

      <AssinaturaQuiosque entidadeTipo="Inspecao" entidadeId={id} />
    </div>
  );
}
