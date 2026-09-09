import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type Trabalhador, type Treinamento } from '../../lib/api';
import { AssinaturaQuiosque } from '../../components/assinatura/AssinaturaQuiosque';
import { Button, FeedbackInline, PageHeader } from '@ui';

// Tela de quiosque para o certificado de treinamento, mesmo padrão de AssinarEntregaEpiPage.tsx/
// AssinarPtPage.tsx (Motor de Assinatura Eletrônica): só resolve cabeçalho e navegação; o quiosque em
// si é o componente genérico AssinaturaQuiosque, aqui com entidadeTipo="Treinamento".
// Onda 2 Task 21 (camada ui/, conversões 2 e 4): card+toolbar manual → PageHeader; erro solto →
// FeedbackInline. Sem `voltarPara` fixo (diferente de AssinarPtPage.tsx) — treinamento não tem uma
// única página de detalhe canônica (acessado de vários pontos: lista de Treinamentos, notificações
// etc.), então o botão "Voltar" preserva o navigate(-1) original (volta pro histórico do navegador).
export function AssinarTreinamentoPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
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
    return <FeedbackInline tom="erro">Treinamento não encontrado.</FeedbackInline>;
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

      <PageHeader
        titulo={`Assinatura eletrônica — ${curso?.nome ?? 'Carregando...'}`}
        subtitulo={
          treinamento
            ? `Funcionário: ${trabalhador?.nome ?? treinamento.trabalhadorId} · Realização: ${treinamento.dataRealizacao?.slice(0, 10)}`
            : undefined
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <AssinaturaQuiosque entidadeTipo="Treinamento" entidadeId={id} />
    </div>
  );
}
