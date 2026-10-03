import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Card, FeedbackInline, PageHeader, Text } from '@ui';
import { api, type Trabalhador } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Quiosque de assinatura do Termo de Recebimento e Compromisso de Uso do EPI (03/10), mesmo padrão
// de AssinarEntregaEpiPage: só resolve cabeçalho e navegação; a captura (digital Futronic ou
// reconhecimento facial) é o componente genérico AssinaturaQuiosque com entidadeTipo
// "TermoCompromissoEpi" e entidadeId = o funcionário. O backend recusa assinatura por sessão logada
// nesse tipo — o termo só vale se for o próprio funcionário.
export function AssinarTermoEpiPage() {
  const { trabalhadorId } = useParams<{ trabalhadorId: string }>();
  const [trabalhador, setTrabalhador] = useState<Trabalhador | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!trabalhadorId) return;
    api.trabalhadores
      .listar()
      .then((lista) => setTrabalhador(lista.find((t) => t.id === trabalhadorId) ?? null))
      .catch(() => setErro('Falha ao carregar os dados do funcionário.'));
  }, [trabalhadorId]);

  if (!trabalhadorId) {
    return <FeedbackInline tom="erro">Funcionário não encontrado.</FeedbackInline>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="Assinatura do termo de recebimento e compromisso de uso"
        voltarPara="/epi"
        rotuloVoltar="Voltar para EPI"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card densidade="compacta">
        <Text>Funcionário: {trabalhador?.nome ?? 'Carregando...'}</Text>
      </Card>

      {trabalhador && (
        <AssinaturaQuiosque entidadeTipo="TermoCompromissoEpi" entidadeId={trabalhadorId} obraId={trabalhador.obraId} />
      )}
    </div>
  );
}
