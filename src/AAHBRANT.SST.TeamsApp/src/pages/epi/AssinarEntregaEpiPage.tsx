import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Card, FeedbackInline, PageHeader, Text } from '@ui';
import { api, type CatalogoEpi, type EntregaEpi, type Trabalhador } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';

// Tela de quiosque para a ficha de entrega de EPI, mesmo padrão de AssinarDdsPage.tsx/AssinarPtPage.tsx
// (etapa 14 do Motor de Assinatura Eletrônica): só resolve cabeçalho e navegação; o quiosque em si é
// o componente genérico AssinaturaQuiosque, aqui com entidadeTipo="EntregaEpi".
// Onda 2 Task 19 (camada ui/): PageHeader substitui o card+botão de voltar manuais; erro vira
// FeedbackInline; a foto do EPI (que não cabe em PageHeader, sem slot de mídia) fica num Card
// compacto próprio, entre o cabeçalho e o quiosque.
export function AssinarEntregaEpiPage() {
  const { id } = useParams<{ id: string }>();
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
    return <FeedbackInline tom="erro">Entrega de EPI não encontrada.</FeedbackInline>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo={`Assinatura eletrônica — ${epi?.nome ?? 'Carregando...'}`}
        voltarPara="/epi"
        rotuloVoltar="Voltar para EPI"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {epi && (
        <Card densidade="compacta">
          <div style={{ display: 'flex', gap: 16, alignItems: 'center' }}>
            <FotoCatalogoEpi catalogoEpiId={epi.id} temFoto={epi.temFoto} tamanho={96} />
            {entrega && (
              <Text>
                Funcionário: {trabalhador?.nome ?? entrega.trabalhadorId} · Entrega: {entrega.dataEntrega?.slice(0, 10)}
              </Text>
            )}
          </div>
        </Card>
      )}

      <AssinaturaQuiosque entidadeTipo="EntregaEpi" entidadeId={id} />
    </div>
  );
}
