import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Text } from '@fluentui/react-components';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { api, type CatalogoEpi, type EntregaEpi, type Trabalhador } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';

// Tela de quiosque para a ficha de entrega de EPI, mesmo padrão de AssinarDdsPage.tsx (etapa 14 do
// Motor de Assinatura Eletrônica): só resolve cabeçalho e navegação; o quiosque em si é o
// componente genérico AssinaturaQuiosque, aqui com entidadeTipo="EntregaEpi".
export function AssinarEntregaEpiPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [entrega, setEntrega] = useState<EntregaEpi | null>(null);
  const [epi, setEpi] = useState<CatalogoEpi | null>(null);
  const [trabalhador, setTrabalhador] = useState<Trabalhador | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.entregasEpi
      .obterPorId(id)
      .then(async (det) => {
        setEntrega(det);
        const [epis, trabalhadores] = await Promise.all([api.catalogosEpi.listar(), api.trabalhadores.listar()]);
        setEpi(epis.find((e) => e.id === det.catalogoEpiId) ?? null);
        setTrabalhador(trabalhadores.find((t) => t.id === det.trabalhadorId) ?? null);
      })
      .catch(() => setErro('Falha ao carregar os dados da entrega de EPI.'));
  }, [id]);

  if (!id) {
    return <Text>Entrega de EPI não encontrada.</Text>;
  }

  return (
    <div>
      <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => navigate('/epi')} style={{ marginBottom: 12 }}>
        Voltar para EPI
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
          {epi && <FotoCatalogoEpi catalogoEpiId={epi.id} temFoto={epi.temFoto} tamanho={96} />}
          <div>
            <Text size={500} weight="semibold">
              Assinatura eletrônica — {epi?.nome ?? 'Carregando...'}
            </Text>
            {entrega && (
              <Text style={{ display: 'block', marginTop: 4 }}>
                Funcionário: {trabalhador?.nome ?? entrega.trabalhadorId} · Entrega: {entrega.dataEntrega?.slice(0, 10)}
              </Text>
            )}
          </div>
        </div>
      </div>

      <AssinaturaQuiosque entidadeTipo="EntregaEpi" entidadeId={id} />
    </div>
  );
}
