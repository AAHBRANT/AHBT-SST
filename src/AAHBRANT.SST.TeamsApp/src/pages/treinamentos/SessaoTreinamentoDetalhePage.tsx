import { pendenciasGrade } from '../../lib/dadosFoto';
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  BotaoAcao,
  Campo,
  Card,
  Carregando,
  DataTable,
  DetailPageLayout,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  Legenda,
  StatusChip,
  WorkflowActions,
  type AcaoWorkflow,
  type Coluna,
  type Tom,
} from '@ui';
import {
  ArrowDownload24Regular,
  Checkmark24Filled,
  Eye24Regular,
  Signature24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusSessaoTreinamento,
  statusSessaoTreinamentoLabel,
  type SessaoTreinamentoDetalhe,
  type ParticipanteSessaoTreinamento,
} from '../../lib/api';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { formatarHoraBrasilia } from '../../components/assinatura/assinaturaDigital';
import { BlocoInstrutorTurma } from './BlocoInstrutorTurma';
import { BotaoExcluirTurma, ExcluirTurmaDialog } from '../../components/treinamentos/ExcluirTurmaDialog';
import { PresencaBiometricaTurma } from '../../components/treinamentos/PresencaBiometricaTurma';
import { FotosTurma, TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS } from '../../components/treinamentos/FotosTurma';
import { useAgenteBiometricoLocal } from '../../components/treinamentos/useAgenteBiometricoLocal';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';


const tomStatusTurma: Record<number, Tom> = {
  [StatusSessaoTreinamento.EmAndamento]: 'atencao',
  [StatusSessaoTreinamento.Concluida]: 'ok',
};

function tomPresenca(p: ParticipanteSessaoTreinamento, somenteLeitura: boolean): Tom {
  if (p.presencaConfirmadaEm) return 'ok';
  return somenteLeitura ? 'alerta' : 'atencao';
}

function rotuloPresenca(p: ParticipanteSessaoTreinamento, somenteLeitura: boolean): string {
  if (p.presencaConfirmadaEm) {
    return `Confirmada às ${formatarHoraBrasilia(p.presencaConfirmadaEm)}`;
  }
  return somenteLeitura ? 'Ausente' : 'Aguardando';
}

function tomCertificado(p: ParticipanteSessaoTreinamento): Tom {
  if (p.certificadoAssinadoPeloTrabalhadorEm && p.certificadoAssinadoPeloInstrutorEm) return 'ok';
  if (p.certificadoAssinadoPeloTrabalhadorEm || p.certificadoAssinadoPeloInstrutorEm) return 'atencao';
  return 'info';
}

function rotuloCertificado(p: ParticipanteSessaoTreinamento): string {
  if (p.certificadoAssinadoPeloTrabalhadorEm && p.certificadoAssinadoPeloInstrutorEm) return 'Assinado (trabalhador e instrutor)';
  if (p.certificadoAssinadoPeloTrabalhadorEm || p.certificadoAssinadoPeloInstrutorEm) return 'Assinatura parcial';
  return 'Assinatura pendente';
}

// Detalhe da turma de treinamento (04/09) — mesmo padrão de DdsDetalhePage.tsx, adaptado: os
// participantes já vêm pré-inscritos (não há dropdown de "adicionar participante"), cada linha da
// tabela confirma a própria presença por biometria. Encerrar gera 1 certificado por participante
// que confirmou presença (ver EncerrarSessaoTreinamentoCommand); a assinatura dupla desse
// certificado reaproveita a tela/diálogo que já existe para Treinamento.
// Onda 3 Task 22.5 (camada ui/): candidata a DetailPageLayout, mesmo julgamento já registrado em
// DdsDetalhePage.tsx (Onda 2 Task 14) — cabeçalho com voltar/título/status/ação utilitária (baixar
// ata); lateral com o resumo e a única transição de estado real (Encerrar) em WorkflowActions. A
// leitura biométrica em fila não é transição de estado (fica no corpo, como em DdsDetalhePage), só
// Encerrar entra em WorkflowActions. Tabela de participantes → DataTable; badges de presença/
// certificado → StatusChip.
export function SessaoTreinamentoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<SessaoTreinamentoDetalhe | null>(null);
  const { agenteDisponivel, dispositivoLocal } = useAgenteBiometricoLocal();
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [baixandoAta, setBaixandoAta] = useState(false);
  // Exclusao definitiva: so Administrador ve o botao (o servidor tambem recusa os demais).
  const souAdministrador = useSouAdministrador();
  const [excluindoTurma, setExcluindoTurma] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const det = await api.sessoesTreinamento.obterDetalhe(id);
      setDetalhe(det);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a turma de treinamento.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function encerrar() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.sessoesTreinamento.encerrar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar a turma.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function baixarAta() {
    if (!id) return;
    try {
      setBaixandoAta(true);
      salvarBlob(await api.sessoesTreinamento.baixarAta(id), `lista-presenca-treinamento-${id}.pdf`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a lista de presença em PDF.');
    } finally {
      setBaixandoAta(false);
    }
  }

  async function baixarCertificado(treinamentoId: string) {
    try {
      setBaixandoId(treinamentoId);
      salvarBlob(await api.treinamentos.baixarCertificado(treinamentoId), `certificado-treinamento-${treinamentoId}.pdf`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o certificado em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  // Mesmos documentos dos botões "Baixar", abertos na janela de visualização.
  function visualizarAta() {
    if (!id) return;
    const sessaoId = id;
    void visualizar({
      titulo: 'Lista de presença de treinamento',
      nomeArquivo: `lista-presenca-treinamento-${sessaoId}.pdf`,
      obter: () => api.sessoesTreinamento.baixarAta(sessaoId),
    });
  }

  function visualizarCertificado(treinamentoId: string, trabalhadorNome: string) {
    void visualizar({
      titulo: `Certificado — ${trabalhadorNome}`,
      nomeArquivo: `certificado-treinamento-${treinamentoId}.pdf`,
      obter: () => api.treinamentos.baixarCertificado(treinamentoId),
    });
  }

  if (!id) return <FeedbackInline tom="erro">Turma não encontrada.</FeedbackInline>;

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre (mesmo
  // padrão já revisado em DdsDetalhePage.tsx/DdsSemanalDetalhePage.tsx).
  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  const sessao = detalhe.sessao;
  const somenteLeitura = sessao.status !== StatusSessaoTreinamento.EmAndamento;
  const totalFotosEvidencia = detalhe.fotosEvidencia.length;
  const faltamFotosEvidencia = Math.max(0, TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS - totalFotosEvidencia);

  // Só a transição de estado real (encerrar) entra em WorkflowActions — baixar ata é ação
  // utilitária sempre disponível, não muda o status, fica no cabeçalho.
  const acoesWorkflow: AcaoWorkflow[] = [];
  if (!somenteLeitura) {
    acoesWorkflow.push({
      chave: 'encerrar',
      rotulo: 'Finalizar treinamento e gerar certificados',
      finalizacao: true,
      pendencias: pendenciasGrade(detalhe.fotosEvidencia),
      descricao: faltamFotosEvidencia > 0 ? `Faltam ${faltamFotosEvidencia} foto(s) de evidência.` : undefined,
      tom: 'primario',
      habilitada: true,
      aoExecutar: encerrar,
    });
  }

  const colunasParticipantes: Coluna<ParticipanteSessaoTreinamento>[] = [
    { chave: 'nome', rotulo: 'Nome', render: (p) => p.trabalhadorNome },
    { chave: 'matricula', rotulo: 'Matrícula', render: (p) => p.trabalhadorMatricula },
    {
      chave: 'presenca',
      rotulo: 'Presença',
      render: (p) => (
        <StatusChip tom={tomPresenca(p, somenteLeitura)} icone={p.presencaConfirmadaEm ? <Checkmark24Filled /> : undefined}>
          {rotuloPresenca(p, somenteLeitura)}
        </StatusChip>
      ),
    },
    {
      chave: 'certificado',
      rotulo: 'Certificado',
      render: (p) =>
        p.treinamentoGeradoId ? (
          <StatusChip
            tom={tomCertificado(p)}
            icone={p.certificadoAssinadoPeloTrabalhadorEm && p.certificadoAssinadoPeloInstrutorEm ? <Checkmark24Filled /> : undefined}
          >
            {rotuloCertificado(p)}
          </StatusChip>
        ) : (
          <Legenda>—</Legenda>
        ),
    },
  ];

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: sessao.cursoTreinamentoNome,
        subtitulo: `Nº certificado: ${sessao.numeroCertificado} · Obra: ${sessao.obraNome} · Data: ${sessao.dataRealizacao?.slice(0, 10)} · Carga horária: ${sessao.cargaHorariaRealizada}h`,
        status: <StatusChip tom={tomStatusTurma[sessao.status] ?? 'neutro'}>{statusSessaoTreinamentoLabel[sessao.status]}</StatusChip>,
        voltarPara: '/treinamentos',
        rotuloVoltar: 'Voltar para Turmas',
        acoes: (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={visualizarAta} aria-label="Visualizar lista de presença">
              Visualizar lista de presença
            </BotaoAcao>
            <BotaoAcao
              tom="baixar"
              icon={<ArrowDownload24Regular />}
              onClick={baixarAta}
              disabled={baixandoAta}
              aria-label="Baixar lista de presença"
            >
              Baixar lista de presença
            </BotaoAcao>
            {souAdministrador && <BotaoExcluirTurma onClick={() => setExcluindoTurma(true)} />}
          </div>
        ),
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              <Campo span={12}>
                <Field label="Instituição / instrutor">
                  <Input value={sessao.instituicaoInstrutor || '—'} readOnly />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Presenças confirmadas">
                  <Input value={`${sessao.totalPresencasConfirmadas}/${sessao.totalParticipantes}`} readOnly />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Evidências fotográficas">
                  <Input value={`${totalFotosEvidencia}/${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS}`} readOnly />
                </Field>
              </Campo>
            </FormGrid>
          </Card>
          {acoesWorkflow.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis">
              <WorkflowActions acoes={acoesWorkflow} processando={processando} erro={erro} />
            </Card>
          )}
        </>
      }
    >
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <BlocoInstrutorTurma
        detalhe={detalhe}
        agenteDisponivel={agenteDisponivel}
        dispositivoLocal={dispositivoLocal}
        aoAtualizar={carregar}
      />

      <Card>
        <FotosTurma detalhe={detalhe} somenteLeitura={somenteLeitura} aoAtualizar={carregar} aoErro={setErro} />
      </Card>

      <Card titulo="Participantes">
        <PresencaBiometricaTurma
          detalhe={detalhe}
          somenteLeitura={somenteLeitura}
          agenteDisponivel={agenteDisponivel}
          dispositivoLocal={dispositivoLocal}
          aoAtualizar={carregar}
          aoErro={setErro}
        />

        <DataTable
          aria-label="Participantes da turma"
          colunas={colunasParticipantes}
          linhas={detalhe.participantes}
          chaveLinha={(p) => p.id}
          vazio={{ titulo: 'Nenhum participante inscrito nesta turma.' }}
          acoesLinha={(p) =>
            p.treinamentoGeradoId ? (
              <div style={{ display: 'flex', gap: 6 }}>
                {!(p.certificadoAssinadoPeloTrabalhadorEm && p.certificadoAssinadoPeloInstrutorEm) && (
                  <BotaoAcao
                    tom="assinar"
                    size="small"
                    icon={<Signature24Regular />}
                    onClick={() => navigate(`/treinamentos/${p.treinamentoGeradoId}/assinar`)}
                    aria-label="Assinar treinamento"
                  >
                    Assinar
                  </BotaoAcao>
                )}
                <BotaoAcao
                  tom="ver"
                  size="small"
                  icon={<Eye24Regular />}
                  onClick={() => visualizarCertificado(p.treinamentoGeradoId!, p.trabalhadorNome)}
                  aria-label="Visualizar certificado"
                >
                  Visualizar
                </BotaoAcao>
                <BotaoAcao
                  tom="baixar"
                  size="small"
                  icon={<ArrowDownload24Regular />}
                  onClick={() => baixarCertificado(p.treinamentoGeradoId!)}
                  disabled={baixandoId === p.treinamentoGeradoId}
                  aria-label="Baixar certificado"
                >
                  Baixar
                </BotaoAcao>
              </div>
            ) : null
          }
        />
      </Card>
      {dialogoVisualizador}
      {excluindoTurma && (
        <ExcluirTurmaDialog
          alvo={{ tipo: 'unica', turmaId: sessao.id }}
          aoFechar={() => setExcluindoTurma(false)}
          aoExcluir={() => navigate('/treinamentos')}
        />
      )}
    </DetailPageLayout>
  );
}
