import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Checkbox,
  Field,
  Select,
  Input,
  Card,
  DetailPageLayout,
  WorkflowActions,
  StatusChip,
  FeedbackInline,
  Carregando,
  Legenda,
  DataTable,
  FormGrid,
  Campo,
  type AcaoWorkflow,
  type Coluna,
  type Tom,
} from '@ui';
import {
  ArrowDownload24Regular,
  Checkmark24Filled,
  Fingerprint24Regular,
  PersonAdd24Regular,
  Send24Regular,
  Signature24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusDds,
  statusDdsLabel,
  TipoFotoParticipante,
  tipoFotoParticipanteLabel,
  type DdsDetalhe,
  type DdsParticipante,
  type Trabalhador,
} from '../../lib/api';
import { capturarDigitalLocal, estaAgenteLocalDisponivel, obterDispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';

const TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS = 3;

const tomStatusDia: Record<number, Tom> = {
  [StatusDds.EmAndamento]: 'atencao',
  [StatusDds.Concluido]: 'ok',
};

function tomTelegram(p: DdsParticipante): Tom {
  if (p.telegramConfirmadoEm) return 'ok';
  if (p.telegramEnviadoEm) return 'atencao';
  return 'info';
}

function rotuloTelegram(p: DdsParticipante): string {
  if (p.telegramConfirmadoEm) return 'Ciência confirmada';
  if (p.telegramEnviadoEm) return 'Enviado, aguardando confirmação';
  return 'Não enviado';
}

// Onda 2 (Task 14) — candidata a DetailPageLayout (conversões 1, 2, 3, 4, 5): cabeçalho com
// voltar/título/status/ações utilitárias (assinar/baixar/telegram); lateral com o resumo e a única
// transição de estado real (Encerrar DDS) em WorkflowActions — as demais ações (assinar, baixar,
// telegram) não são transição de estado, ficam no cabeçalho. Participantes é o único <Table> cru
// do arquivo → DataTable; badges de status (geral + Telegram) → StatusChip.
export function DdsDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<DdsDetalhe | null>(null);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [participanteSelecionado, setParticipanteSelecionado] = useState('');
  const [agenteDisponivel, setAgenteDisponivel] = useState<boolean | null>(null);
  const [dispositivoLocal, setDispositivoLocal] = useState<{ dispositivoId: string; segredoDispositivo: string } | null>(null);
  const [validandoBiometria, setValidandoBiometria] = useState(false);
  const [biometriaValidada, setBiometriaValidada] = useState<{ trabalhadorId: string; score: number } | null>(null);
  const [fotosEvidenciaPreview, setFotosEvidenciaPreview] = useState<Record<string, string>>({});
  const [anexandoFotoEvidencia, setAnexandoFotoEvidencia] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoPdf, setBaixandoPdf] = useState(false);
  const [baixandoFotoId, setBaixandoFotoId] = useState<string | null>(null);
  const [enviandoTelegram, setEnviandoTelegram] = useState(false);
  const [resultadoTelegram, setResultadoTelegram] = useState<string | null>(null);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const det = await api.dds.obterDetalhe(id);
      setDetalhe(det);
      const listaTrabalhadores = await api.trabalhadores.listar(det.dds.obraId);
      setTrabalhadores(listaTrabalhadores);

      const previews = await Promise.all(
        det.fotosEvidencia.map(async (foto) => {
          try {
            const blob = await api.dds.baixarFotoEvidencia(foto.id);
            return [foto.id, URL.createObjectURL(blob)] as const;
          } catch {
            return null;
          }
        }),
      );
      setFotosEvidenciaPreview(Object.fromEntries(previews.filter((p): p is [string, string] => p !== null)));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar DDS.');
    }
  }

  async function anexarFotoEvidencia(arquivo: File) {
    if (!id) return;
    try {
      setAnexandoFotoEvidencia(true);
      setErro(null);
      await api.dds.anexarFotoEvidencia(id, arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao anexar foto de evidência.');
    } finally {
      setAnexandoFotoEvidencia(false);
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

  // A validação biométrica é por participante selecionado — trocar a seleção invalida a
  // captura anterior, evitando registrar a presença de outra pessoa por engano.
  useEffect(() => {
    setBiometriaValidada(null);
  }, [participanteSelecionado]);

  async function marcarItem(itemId: string, verificado: boolean) {
    try {
      setErro(null);
      await api.dds.marcarItem(itemId, verificado);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao marcar item do checklist.');
    }
  }

  async function validarBiometria() {
    if (!participanteSelecionado) return;
    try {
      setValidandoBiometria(true);
      setErro(null);
      const captura = await capturarDigitalLocal();
      if (captura.trabalhadorId !== participanteSelecionado) {
        setBiometriaValidada(null);
        setErro('A digital capturada não corresponde ao participante selecionado.');
        return;
      }
      setBiometriaValidada({ trabalhadorId: captura.trabalhadorId, score: captura.score });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha na validação biométrica.');
    } finally {
      setValidandoBiometria(false);
    }
  }

  async function registrarParticipante() {
    if (!id || !participanteSelecionado || !dispositivoLocal) return;
    if (!biometriaValidada || biometriaValidada.trabalhadorId !== participanteSelecionado) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.registrarParticipante(
        id,
        participanteSelecionado,
        dispositivoLocal.dispositivoId,
        dispositivoLocal.segredoDispositivo,
        biometriaValidada.score,
      );
      setParticipanteSelecionado('');
      setBiometriaValidada(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar participante.');
    } finally {
      setProcessando(false);
    }
  }

  async function baixarFotoParticipante(participanteId: string, trabalhadorNome: string) {
    try {
      setBaixandoFotoId(participanteId);
      setErro(null);
      const blob = await api.dds.baixarFotoParticipante(participanteId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `dds-${trabalhadorNome.replace(/\s+/g, '-').toLowerCase()}`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a foto do participante.');
    } finally {
      setBaixandoFotoId(null);
    }
  }

  async function encerrar() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.encerrar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar DDS.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function baixarPdf() {
    if (!id || !detalhe) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.dds.baixarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `dds-${detalhe.dds.data?.slice(0, 10)}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o PDF do DDS.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  async function enviarTelegram() {
    if (!id) return;
    try {
      setEnviandoTelegram(true);
      setErro(null);
      setResultadoTelegram(null);
      const resultado = await api.dds.enviarTelegram(id);
      setResultadoTelegram(
        `Enviado para ${resultado.enviados} de ${resultado.totalParticipantes} participantes` +
          (resultado.semVinculo > 0 ? ` — ${resultado.semVinculo} sem vínculo de Telegram.` : '.'),
      );
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar o DDS via Telegram.');
    } finally {
      setEnviandoTelegram(false);
    }
  }

  if (!id) return <FeedbackInline tom="erro">DDS não encontrado.</FeedbackInline>;

  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  const dds = detalhe.dds;
  const somenteLeitura = dds.status !== StatusDds.EmAndamento;
  const participantesRegistrados = new Set(detalhe.participantes.map((p) => p.trabalhadorId));
  const trabalhadoresDisponiveis = trabalhadores.filter((t) => !participantesRegistrados.has(t.id));
  const totalFotosEvidencia = detalhe.fotosEvidencia.length;
  const faltamFotosEvidencia = Math.max(0, TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS - totalFotosEvidencia);
  const biometriaConfirmada = !!biometriaValidada && biometriaValidada.trabalhadorId === participanteSelecionado;

  const tituloDds =
    dds.temasAtividades.length > 0
      ? dds.temasAtividades.map((t) => t.atividadeNome + (t.perigoNome ? ` — ${t.perigoNome}` : '')).join(' · ')
      : dds.temaLivreNome
        ? `Tema livre: ${dds.temaLivreNome}`
        : 'DDS do dia';

  // Só a transição de estado real (encerrar) entra em WorkflowActions — assinar/baixar/telegram
  // são ações utilitárias sempre disponíveis, não mudam o status, então ficam no cabeçalho.
  const acoesWorkflow: AcaoWorkflow[] = [];
  if (!somenteLeitura) {
    acoesWorkflow.push({
      chave: 'encerrar',
      rotulo: 'Encerrar DDS',
      descricao: faltamFotosEvidencia > 0 ? `Faltam ${faltamFotosEvidencia} foto(s) de evidência.` : undefined,
      tom: 'primario',
      habilitada: faltamFotosEvidencia === 0,
      aoExecutar: encerrar,
    });
  }

  const colunasParticipantes: Coluna<DdsParticipante>[] = [
    { chave: 'nome', rotulo: 'Nome', render: (p) => p.trabalhadorNome },
    { chave: 'evidencia', rotulo: 'Evidência', render: (p) => tipoFotoParticipanteLabel[p.fotoTipo] },
    {
      chave: 'telegram',
      rotulo: 'Telegram',
      render: (p) => <StatusChip tom={tomTelegram(p)}>{rotuloTelegram(p)}</StatusChip>,
    },
  ];

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: tituloDds,
        subtitulo: `Obra: ${dds.obraNome} · Data: ${dds.data?.slice(0, 10)} · Responsável: ${dds.responsavelUsuarioNome}`,
        status: <StatusChip tom={tomStatusDia[dds.status] ?? 'neutro'}>{statusDdsLabel[dds.status]}</StatusChip>,
        voltarPara: dds.ddsSemanalId ? `/prevencao/dds/semana/${dds.ddsSemanalId}` : '/prevencao/dds',
        rotuloVoltar: 'Voltar para a semana',
        acoes: (
          <>
            <Button icon={<Signature24Regular />} onClick={() => navigate(`/prevencao/dds/dia/${id}/assinar`)}>
              Assinar DDS
            </Button>
            <Button icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf}>
              Baixar PDF
            </Button>
            <Button icon={<Send24Regular />} onClick={enviarTelegram} disabled={enviandoTelegram}>
              Enviar via Telegram
            </Button>
          </>
        ),
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              <Campo span={12}>
                <Field label="Atividades do dia"><Input value={dds.atividadesNomes.join(', ') || 'DDS do dia'} readOnly /></Field>
              </Campo>
              <Campo span={12}>
                <Field label="Checklist verificado"><Input value={`${dds.itensVerificados}/${dds.totalItensChecklist}`} readOnly /></Field>
              </Campo>
              <Campo span={12}>
                <Field label="Participantes"><Input value={String(dds.totalParticipantes)} readOnly /></Field>
              </Campo>
              <Campo span={12}>
                <Field label="Evidências fotográficas"><Input value={`${totalFotosEvidencia}/${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS}`} readOnly /></Field>
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
      {resultadoTelegram && <FeedbackInline tom="sucesso">{resultadoTelegram}</FeedbackInline>}

      <Card titulo="Checklist de verificação">
        {detalhe.itensChecklist.length === 0 ? (
          <Legenda>Nenhum item de checklist gerado — revise a Matriz de Riscos das atividades selecionadas.</Legenda>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {detalhe.itensChecklist.map((item) => (
              <Checkbox
                key={item.id}
                label={item.descricao}
                checked={item.verificado}
                disabled={somenteLeitura}
                onChange={(_, d) => marcarItem(item.id, !!d.checked)}
              />
            ))}
          </div>
        )}
      </Card>

      <Card
        titulo={`Evidências fotográficas (${totalFotosEvidencia}/${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS})`}
        subtitulo={`${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS} fotos são obrigatórias para liberar o encerramento deste registro diário.`}
      >
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'center' }}>
          {detalhe.fotosEvidencia
            .slice()
            .sort((a, b) => a.ordem - b.ordem)
            .map((foto) => (
              <img
                key={foto.id}
                src={fotosEvidenciaPreview[foto.id]}
                alt={`Evidência ${foto.ordem}`}
                style={{ height: 96, width: 96, objectFit: 'cover', borderRadius: 4 }}
              />
            ))}
          {!somenteLeitura && totalFotosEvidencia < TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS && (
            <SeletorFotoCamera
              rotulo="Tirar foto de evidência"
              desabilitado={anexandoFotoEvidencia}
              aoSelecionarArquivo={anexarFotoEvidencia}
              aoErroValidacao={setErro}
            />
          )}
        </div>
      </Card>

      <Card titulo="Participantes">
        {!somenteLeitura && (
          <div style={{ marginBottom: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
            <FormGrid>
              <Campo span={6}>
                <Field label="Funcionário">
                  <Select
                    value={participanteSelecionado}
                    onChange={(_, d) => setParticipanteSelecionado(d.value)}
                  >
                    <option value="">Selecione um funcionário</option>
                    {trabalhadoresDisponiveis.map((trabalhador) => (
                      <option key={trabalhador.id} value={trabalhador.id}>
                        {trabalhador.nome} ({trabalhador.matricula})
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
            </FormGrid>
            {agenteDisponivel === false && (
              <FeedbackInline tom="aviso">
                Leitor Futronic não encontrado nesta máquina. Verifique se o leitor está conectado e se o
                Agente Biométrico está em execução, depois recarregue esta página.
              </FeedbackInline>
            )}
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
              <Button
                appearance="primary"
                icon={<Fingerprint24Regular />}
                onClick={validarBiometria}
                disabled={!agenteDisponivel || !dispositivoLocal || !participanteSelecionado || validandoBiometria}
              >
                {validandoBiometria ? 'Validando biometria...' : 'Validar Biometria'}
              </Button>
              {biometriaConfirmada && (
                <StatusChip tom="ok" icone={<Checkmark24Filled />}>
                  Biometria validada
                </StatusChip>
              )}
              <Button
                appearance="primary"
                icon={<PersonAdd24Regular />}
                onClick={registrarParticipante}
                disabled={processando || !participanteSelecionado || !biometriaConfirmada}
              >
                Registrar presença
              </Button>
            </div>
            <Legenda>A validação biométrica do participante selecionado é obrigatória para registrar a presença.</Legenda>
          </div>
        )}

        <DataTable
          aria-label="Participantes do DDS"
          colunas={colunasParticipantes}
          linhas={detalhe.participantes}
          chaveLinha={(p) => p.id}
          vazio={{ titulo: 'Nenhum participante registrado ainda.' }}
          acoesLinha={(p) =>
            p.fotoTipo !== TipoFotoParticipante.Biometria ? (
              <Button
                appearance="subtle"
                size="small"
                icon={<ArrowDownload24Regular />}
                onClick={() => baixarFotoParticipante(p.id, p.trabalhadorNome)}
                disabled={baixandoFotoId === p.id}
              >
                Baixar foto
              </Button>
            ) : null
          }
        />
      </Card>
    </DetailPageLayout>
  );
}
