import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Text } from '@fluentui/react-components';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { api, type PermissaoTrabalhoDetalhe } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque (docs/Motor-Assinatura-Eletronica.md §5, etapa 14): mesmo padrão de AssinarDdsPage,
// resolvendo só o cabeçalho e a navegação específicos da PT — o quiosque em si vem de AssinaturaQuiosque.
export function AssinarPtPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
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
    return <Text>Permissão de Trabalho não encontrada.</Text>;
  }

  const pt = detalhe?.permissaoTrabalho;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate(`/operacao/pt/${id}`)}
        style={{ marginBottom: 12 }}
      >
        Voltar para a PT
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Assinatura eletrônica — {pt?.atividadeNome ?? 'Carregando...'}
        </Text>
        {pt && (
          <Text style={{ display: 'block', marginTop: 4 }}>
            Local: {pt.local} · Data: {pt.data?.slice(0, 10)}
          </Text>
        )}
      </div>

      <AssinaturaQuiosque entidadeTipo="PermissaoTrabalho" entidadeId={id} />
    </div>
  );
}
