import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Text } from '@fluentui/react-components';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type Trabalhador, type Treinamento } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';

// Tela de quiosque para o certificado de treinamento, mesmo padrão de AssinarEntregaEpiPage.tsx
// (Motor de Assinatura Eletrônica): só resolve cabeçalho e navegação; o quiosque em si é o
// componente genérico AssinaturaQuiosque, aqui com entidadeTipo="Treinamento".
export function AssinarTreinamentoPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [treinamento, setTreinamento] = useState<Treinamento | null>(null);
  const [curso, setCurso] = useState<CursoTreinamento | null>(null);
  const [trabalhador, setTrabalhador] = useState<Trabalhador | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.treinamentos
      .obterPorId(id)
      .then(async (det) => {
        setTreinamento(det);
        const [cursos, trabalhadores] = await Promise.all([api.cursosTreinamento.listar(), api.trabalhadores.listar()]);
        setCurso(cursos.find((c) => c.id === det.cursoTreinamentoId) ?? null);
        setTrabalhador(trabalhadores.find((t) => t.id === det.trabalhadorId) ?? null);
      })
      .catch(() => setErro('Falha ao carregar os dados do treinamento.'));
  }, [id]);

  if (!id) {
    return <Text>Treinamento não encontrado.</Text>;
  }

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate(-1)}
        style={{ marginBottom: 12 }}
      >
        Voltar
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Assinatura eletrônica — {curso?.nome ?? 'Carregando...'}
        </Text>
        {treinamento && (
          <Text style={{ display: 'block', marginTop: 4 }}>
            Funcionário: {trabalhador?.nome ?? treinamento.trabalhadorId} · Realização:{' '}
            {treinamento.dataRealizacao?.slice(0, 10)}
          </Text>
        )}
      </div>

      <AssinaturaQuiosque entidadeTipo="Treinamento" entidadeId={id} obraId={trabalhador?.obraId ?? ''} />
    </div>
  );
}
