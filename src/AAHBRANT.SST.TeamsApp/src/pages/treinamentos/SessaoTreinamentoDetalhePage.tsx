import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
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
  Fingerprint24Regular,
  Signature24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusSessaoTreinamento,
  statusSessaoTreinamentoLabel,
  type SessaoTreinamentoDetalhe,
  type ParticipanteSessaoTreinamento,
} from '../../lib/api';
import { capturarDigitalLocal, estaAgenteLocalDisponivel, obterDispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { GradeFotosEvidencia } from '../../components/GradeFotosEvidencia';

const TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS = 3;

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
    return `Confirmada às ${new Date(p.presencaConfirmadaEm).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}`;
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
  const [agenteDisponivel, setAgenteDisponivel] = useState<boolean | null>(null);
  const [dispositivoLocal, setDispositivoLocal] = useState<{ dispositivoId: string; segredoDispositivo: string } | null>(null);
  const [lendoDigital, setLendoDigital] = useState(false);
  const [mensagemPresenca, setMensagemPresenca] = useState<{ tipo: 'success' | 'info' | 'erro'; texto: string } | null>(null);
  const [fotosEvidenciaPreview, setFotosEvidenciaPreview] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [baixandoAta, setBaixandoAta] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const det = await api.sessoesTreinamento.obterDetalhe(id);
      setDetalhe(det);

      const previews = await Promise.all(
        det.fotosEvidencia.map(async (foto) => {
          try {
            const blob = await api.sessoesTreinamento.baixarFotoEvidencia(foto.id);
            return [foto.id, URL.createObjectURL(blob)] as const;
          } catch {
            return null;
          }
        }),
      );
      setFotosEvidenciaPreview(Object.fromEntries(previews.filter((p): p is [string, string] => p !== null)));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a turma de treinamento.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(() => {
    estaAgenteLocalDisponivel().then(async (disponivel) => {
      setAgenteDisponivel(disponivel);
      if (disponivel) {
        const dispositivo = await obterDispositivoLocal();
        setDispositivoLocal(dispositivo);
      }
    });
  }, []);

  async function anexarFotoEvidencia(ordem: number, arquivo: File) {
    if (!id) return;
    try {
      setErro(null);
      await api.sessoesTreinamento.anexarFotoEvidencia(id, ordem, arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao anexar foto de evidência.');
    }
  }

  async function removerFotoEvidencia(fotoId: string) {
    try {
      setErro(null);
      await api.sessoesTreinamento.removerFotoEvidencia(fotoId);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao remover foto de evidência.');
    }
  }

  // Fluxo em fila (pedido do usuário, 04/09): um único leitor compartilhado — cada participante
  // simplesmente encosta o dedo, sem precisar ser selecionado antes. O agente local já faz o match
  // 1:N (contra todo mundo cadastrado) e devolve QUEM ele reconheceu; aqui só se confere se essa
  // pessoa está inscrita nesta turma e ainda não confirmou presença, e se registra.
  async function lerProximaDigital() {
    if (!id || !dispositivoLocal) return;
    try {
      setLendoDigital(true);
      setErro(null);
      setMensagemPresenca(null);
      const captura = await capturarDigitalLocal();

      const participante = detalhe?.participantes.find((p) => p.trabalhadorId === captura.trabalhadorId);
      if (!participante) {
        setMensagemPresenca({ tipo: 'erro', texto: 'Esta pessoa não está inscrita nesta turma.' });
        return;
      }
      if (participante.presencaConfirmadaEm) {
        setMensagemPresenca({ tipo: 'info', texto: `Presença de ${participante.trabalhadorNome} já havia sido confirmada.` });
        return;
      }

      await api.sessoesTreinamento.registrarPresenca(
        id,
        captura.trabalhadorId,
        dispositivoLocal.dispositivoId,
        dispositivoLocal.segredoDispositivo,
        captura.score,
      );
      setMensagemPresenca({ tipo: 'success', texto: `Presença de ${participante.trabalhadorNome} confirmada.` });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha na validação biométrica.');
    } finally {
      setLendoDigital(false);
    }
  }

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
      const blob = await api.sessoesTreinamento.baixarAta(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ata-turma-treinamento-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ata em PDF.');
    } finally {
      setBaixandoAta(false);
    }
  }

  async function baixarCertificado(treinamentoId: string) {
    try {
      setBaixandoId(treinamentoId);
      const blob = await api.treinamentos.baixarCertificado(treinamentoId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `certificado-treinamento-${treinamentoId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o certificado em PDF.');
    } finally {
      setBaixandoId(null);
    }
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
      rotulo: 'Encerrar treinamento e gerar certificados',
      descricao: faltamFotosEvidencia > 0 ? `Faltam ${faltamFotosEvidencia} foto(s) de evidência.` : undefined,
      tom: 'primario',
      habilitada: faltamFotosEvidencia === 0,
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
          <Button icon={<ArrowDownload24Regular />} onClick={baixarAta} disabled={baixandoAta}>
            Baixar ata / anexo de evidências
          </Button>
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
              <WorkflowActions acoes={acoesWorkflow} processando={processando} />
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

      <Card>
        <GradeFotosEvidencia
          titulo="Evidências fotográficas"
          subtitulo={`${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS} fotos da turma são obrigatórias para liberar o encerramento.`}
          total={TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS}
          fotos={detalhe.fotosEvidencia
            .filter((f) => fotosEvidenciaPreview[f.id])
            .map((f) => ({ ordem: f.ordem, id: f.id, url: fotosEvidenciaPreview[f.id] }))}
          somenteLeitura={somenteLeitura}
          onSelecionarFoto={anexarFotoEvidencia}
          onRemoverFoto={removerFotoEvidencia}
          onErroValidacao={setErro}
        />
      </Card>

      <Card titulo="Participantes">
        {!somenteLeitura && agenteDisponivel === false && (
          <FeedbackInline tom="aviso">
            Leitor Futronic não encontrado nesta máquina. Verifique se o leitor está conectado e se o Agente
            Biométrico está em execução, depois recarregue esta página.
          </FeedbackInline>
        )}

        {!somenteLeitura && (
          <div style={{ marginBottom: 16, display: 'flex', flexDirection: 'column', gap: 8 }}>
            <Legenda>
              Um leitor só, em fila: cada participante encosta o dedo e o sistema reconhece quem é — não precisa
              selecionar ninguém antes.
            </Legenda>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
              <Button
                appearance="primary"
                icon={<Fingerprint24Regular />}
                onClick={lerProximaDigital}
                disabled={!agenteDisponivel || !dispositivoLocal || lendoDigital}
              >
                {lendoDigital ? 'Lendo digital...' : 'Ler digital do próximo participante'}
              </Button>
            </div>
            {mensagemPresenca && (
              <FeedbackInline tom={mensagemPresenca.tipo === 'success' ? 'sucesso' : mensagemPresenca.tipo === 'erro' ? 'erro' : 'info'}>
                {mensagemPresenca.texto}
              </FeedbackInline>
            )}
          </div>
        )}

        <DataTable
          aria-label="Participantes da turma"
          colunas={colunasParticipantes}
          linhas={detalhe.participantes}
          chaveLinha={(p) => p.id}
          vazio={{ titulo: 'Nenhum participante inscrito nesta turma.' }}
          acoesLinha={(p) =>
            p.treinamentoGeradoId ? (
              <div style={{ display: 'flex', gap: 4 }}>
                {!(p.certificadoAssinadoPeloTrabalhadorEm && p.certificadoAssinadoPeloInstrutorEm) && (
                  <Button
                    appearance="subtle"
                    size="small"
                    icon={<Signature24Regular />}
                    onClick={() => navigate(`/treinamentos/${p.treinamentoGeradoId}/assinar`)}
                  >
                    Assinar
                  </Button>
                )}
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<ArrowDownload24Regular />}
                  onClick={() => baixarCertificado(p.treinamentoGeradoId!)}
                  disabled={baixandoId === p.treinamentoGeradoId}
                >
                  Baixar
                </Button>
              </div>
            ) : null
          }
        />
      </Card>
    </DetailPageLayout>
  );
}
