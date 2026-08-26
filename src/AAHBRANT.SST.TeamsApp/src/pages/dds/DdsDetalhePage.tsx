import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Checkbox,
  Radio,
  RadioGroup,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import {
  ArrowDownload24Regular,
  ArrowLeft24Regular,
  LockClosed24Regular,
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
  type Trabalhador,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

export function DdsDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<DdsDetalhe | null>(null);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [participanteSelecionado, setParticipanteSelecionado] = useState('');
  const [fotoTipo, setFotoTipo] = useState<number>(TipoFotoParticipante.Pessoa);
  const [fotoArquivo, setFotoArquivo] = useState<File | null>(null);
  const [fotoPreviewUrl, setFotoPreviewUrl] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoPdf, setBaixandoPdf] = useState(false);
  const [baixandoFotoId, setBaixandoFotoId] = useState<string | null>(null);
  const [enviandoTelegram, setEnviandoTelegram] = useState(false);
  const [resultadoTelegram, setResultadoTelegram] = useState<string | null>(null);
  const inputFotoRef = useRef<HTMLInputElement>(null);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const det = await api.dds.obterDetalhe(id);
      setDetalhe(det);
      const listaTrabalhadores = await api.trabalhadores.listar(det.dds.obraId);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar DDS.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function marcarItem(itemId: string, verificado: boolean) {
    try {
      setErro(null);
      await api.dds.marcarItem(itemId, verificado);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao marcar item do checklist.');
    }
  }

  function selecionarFoto(arquivo: File | null) {
    setFotoArquivo(arquivo);
    setFotoPreviewUrl((urlAnterior) => {
      if (urlAnterior) URL.revokeObjectURL(urlAnterior);
      return arquivo ? URL.createObjectURL(arquivo) : null;
    });
  }

  async function registrarParticipante() {
    if (!id || !participanteSelecionado || !fotoArquivo) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.registrarParticipante(id, participanteSelecionado, fotoTipo, fotoArquivo);
      setParticipanteSelecionado('');
      selecionarFoto(null);
      if (inputFotoRef.current) inputFotoRef.current.value = '';
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
    } finally {
      setProcessando(false);
    }
  }

  async function baixarPdf() {
    if (!id || !dds) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.dds.baixarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `dds-${dds.data?.slice(0, 10)}.pdf`;
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

  if (!id) {
    return <Text>DDS não encontrado.</Text>;
  }

  const dds = detalhe?.dds;
  const somenteLeitura = dds?.status !== StatusDds.EmAndamento;
  const participantesRegistrados = new Set(detalhe?.participantes.map((p) => p.trabalhadorId));
  const trabalhadoresDisponiveis = trabalhadores.filter((t) => !participantesRegistrados.has(t.id));

  return (
    <div>
      <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => navigate('/prevencao/dds')} style={{ marginBottom: 12 }}>
        Voltar para DDS
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {dds ? (
          <>
            <Text size={500} weight="semibold">
              {dds.topicoPrincipal}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Obra: {dds.obraNome}</Text>
              <Text>Data: {dds.data?.slice(0, 10)}</Text>
              <Text>Responsável: {dds.responsavelUsuarioNome}</Text>
              <Badge appearance="tint">{statusDdsLabel[dds.status]}</Badge>
            </div>
            <div style={{ marginTop: 8 }}>
              <Text>Atividades do dia: {dds.atividadesNomes.join(', ')}</Text>
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, alignItems: 'center' }}>
              <Text>
                Checklist verificado: {dds.itensVerificados}/{dds.totalItensChecklist}
              </Text>
              <Text>Participantes: {dds.totalParticipantes}</Text>
            </div>

            <div className={estilos.formActions} style={{ marginTop: 16 }}>
              {!somenteLeitura && (
                <Button appearance="primary" icon={<LockClosed24Regular />} onClick={encerrar} disabled={processando}>
                  Encerrar DDS
                </Button>
              )}
              <Button icon={<Signature24Regular />} onClick={() => navigate(`/prevencao/dds/${id}/assinar`)}>
                Assinar DDS
              </Button>
              <Button icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf}>
                Baixar PDF
              </Button>
              <Button icon={<Send24Regular />} onClick={enviarTelegram} disabled={enviandoTelegram}>
                Enviar via Telegram
              </Button>
            </div>
            {resultadoTelegram && (
              <Text style={{ display: 'block', marginTop: 8 }}>{resultadoTelegram}</Text>
            )}
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Checklist de verificação</Text>
        </div>

        {detalhe?.itensChecklist.length === 0 ? (
          <Text>Nenhum item de checklist gerado — revise a Matriz de Riscos das atividades selecionadas.</Text>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {detalhe?.itensChecklist.map((item) => (
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
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Participantes</Text>
        </div>

        {!somenteLeitura && (
          <div style={{ marginBottom: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
            <div className={estilos.formActions}>
              <Select
                value={participanteSelecionado}
                onChange={(_, d) => setParticipanteSelecionado(d.value)}
                style={{ minWidth: 240 }}
              >
                <option value="">Selecione um trabalhador</option>
                {trabalhadoresDisponiveis.map((trabalhador) => (
                  <option key={trabalhador.id} value={trabalhador.id}>
                    {trabalhador.nome} ({trabalhador.matricula})
                  </option>
                ))}
              </Select>
              <RadioGroup
                layout="horizontal"
                value={String(fotoTipo)}
                onChange={(_, d) => setFotoTipo(Number(d.value))}
              >
                <Radio value={String(TipoFotoParticipante.Pessoa)} label="Foto da pessoa" />
                <Radio value={String(TipoFotoParticipante.DocumentoAssinado)} label="Documento assinado" />
              </RadioGroup>
            </div>
            <div className={estilos.formActions} style={{ alignItems: 'center' }}>
              <input
                ref={inputFotoRef}
                type="file"
                accept="image/*"
                capture="environment"
                onChange={(e) => selecionarFoto(e.target.files?.[0] ?? null)}
              />
              {fotoPreviewUrl && (
                <img
                  src={fotoPreviewUrl}
                  alt="Pré-visualização da foto"
                  style={{ height: 48, width: 48, objectFit: 'cover', borderRadius: 4 }}
                />
              )}
              <Button
                appearance="primary"
                icon={<PersonAdd24Regular />}
                onClick={registrarParticipante}
                disabled={processando || !participanteSelecionado || !fotoArquivo}
              >
                Registrar presença
              </Button>
            </div>
            <Text size={200}>A foto (da pessoa presente ou do documento assinado) é obrigatória para registrar a presença.</Text>
          </div>
        )}

        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Evidência</TableHeaderCell>
              <TableHeaderCell>Telegram</TableHeaderCell>
              <TableHeaderCell />
            </TableRow>
          </TableHeader>
          <TableBody>
            {detalhe?.participantes.map((participante) => (
              <TableRow key={participante.id}>
                <TableCell>{participante.trabalhadorNome}</TableCell>
                <TableCell>{tipoFotoParticipanteLabel[participante.fotoTipo]}</TableCell>
                <TableCell>
                  {participante.telegramConfirmadoEm ? (
                    <Badge color="success" appearance="tint">
                      Ciência confirmada
                    </Badge>
                  ) : participante.telegramEnviadoEm ? (
                    <Badge color="warning" appearance="tint">
                      Enviado, aguardando confirmação
                    </Badge>
                  ) : (
                    <Badge color="informative" appearance="tint">
                      Não enviado
                    </Badge>
                  )}
                </TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    size="small"
                    icon={<ArrowDownload24Regular />}
                    onClick={() => baixarFotoParticipante(participante.id, participante.trabalhadorNome)}
                    disabled={baixandoFotoId === participante.id}
                  >
                    Baixar foto
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
