import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Badge, Button, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import {
  ArrowDownload24Regular,
  ArrowLeft24Regular,
  Checkmark24Filled,
  Fingerprint24Regular,
  LockClosed24Regular,
  Signature24Regular,
} from '@fluentui/react-icons';
import { api, StatusSessaoTreinamento, statusSessaoTreinamentoLabel, type SessaoTreinamentoDetalhe } from '../../lib/api';
import { capturarDigitalLocal, estaAgenteLocalDisponivel, obterDispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { GradeFotosEvidencia } from '../../components/GradeFotosEvidencia';
import { usePageStyles } from '../pageStyles';

const TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS = 3;

// Detalhe da turma de treinamento (04/09) — mesmo padrão de DdsDetalhePage.tsx, adaptado: os
// participantes já vêm pré-inscritos (não há dropdown de "adicionar participante"), cada linha da
// tabela confirma a própria presença por biometria. Encerrar gera 1 certificado por participante
// que confirmou presença (ver EncerrarSessaoTreinamentoCommand); a assinatura dupla desse
// certificado reaproveita a tela/diálogo que já existe para Treinamento.
export function SessaoTreinamentoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
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

  if (!id) {
    return <Text>Turma não encontrada.</Text>;
  }

  const sessao = detalhe?.sessao;
  const somenteLeitura = sessao?.status !== StatusSessaoTreinamento.EmAndamento;
  const totalFotosEvidencia = detalhe?.fotosEvidencia.length ?? 0;
  const faltamFotosEvidencia = Math.max(0, TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS - totalFotosEvidencia);

  return (
    <div>
      <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => navigate(-1)} style={{ marginBottom: 12 }}>
        Voltar
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {sessao ? (
          <>
            <Text size={500} weight="semibold">
              {sessao.cursoTreinamentoNome}
            </Text>
            <Text size={200} style={{ display: 'block' }}>
              Nº certificado: {sessao.numeroCertificado}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Obra: {sessao.obraNome}</Text>
              <Text>Data: {sessao.dataRealizacao?.slice(0, 10)}</Text>
              <Text>Carga horária: {sessao.cargaHorariaRealizada}h</Text>
              <Badge appearance="tint" color={sessao.status === StatusSessaoTreinamento.Concluida ? 'success' : 'warning'}>
                {statusSessaoTreinamentoLabel[sessao.status]}
              </Badge>
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, alignItems: 'center' }}>
              <Text>
                Presenças confirmadas: {sessao.totalPresencasConfirmadas}/{sessao.totalParticipantes}
              </Text>
            </div>

            <div className={estilos.formActions} style={{ marginTop: 16 }}>
              {!somenteLeitura && (
                <Button
                  appearance="primary"
                  icon={<LockClosed24Regular />}
                  onClick={encerrar}
                  disabled={processando || faltamFotosEvidencia > 0}
                  title={faltamFotosEvidencia > 0 ? `Faltam ${faltamFotosEvidencia} foto(s) de evidência.` : undefined}
                >
                  Encerrar treinamento e gerar certificados
                </Button>
              )}
              <Button icon={<ArrowDownload24Regular />} onClick={baixarAta} disabled={baixandoAta}>
                Baixar ata / anexo de evidências
              </Button>
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <GradeFotosEvidencia
          titulo="Evidências fotográficas"
          subtitulo={`${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS} fotos da turma são obrigatórias para liberar o encerramento.`}
          total={TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS}
          fotos={
            detalhe?.fotosEvidencia
              .filter((f) => fotosEvidenciaPreview[f.id])
              .map((f) => ({ ordem: f.ordem, id: f.id, url: fotosEvidenciaPreview[f.id] })) ?? []
          }
          somenteLeitura={somenteLeitura}
          onSelecionarFoto={anexarFotoEvidencia}
          onRemoverFoto={removerFotoEvidencia}
          onErroValidacao={setErro}
        />
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Participantes</Text>
        </div>

        {!somenteLeitura && agenteDisponivel === false && (
          <Text style={{ display: 'block', color: 'var(--colorPaletteRedForeground1)', marginBottom: 8 }}>
            Leitor Futronic não encontrado nesta máquina. Verifique se o leitor está conectado e se o Agente
            Biométrico está em execução, depois recarregue esta página.
          </Text>
        )}

        {!somenteLeitura && (
          <div style={{ marginBottom: 16 }}>
            <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
              Um leitor só, em fila: cada participante encosta o dedo e o sistema reconhece quem é —
              não precisa selecionar ninguém antes.
            </Text>
            <Button
              appearance="primary"
              icon={<Fingerprint24Regular />}
              onClick={lerProximaDigital}
              disabled={!agenteDisponivel || !dispositivoLocal || lendoDigital}
            >
              {lendoDigital ? 'Lendo digital...' : 'Ler digital do próximo participante'}
            </Button>
            {mensagemPresenca && (
              <Text
                style={{
                  display: 'block',
                  marginTop: 8,
                  color:
                    mensagemPresenca.tipo === 'success'
                      ? 'var(--colorPaletteGreenForeground1)'
                      : mensagemPresenca.tipo === 'erro'
                        ? 'var(--colorPaletteRedForeground1)'
                        : undefined,
                }}
              >
                {mensagemPresenca.texto}
              </Text>
            )}
          </div>
        )}

        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Matrícula</TableHeaderCell>
              <TableHeaderCell>Presença</TableHeaderCell>
              <TableHeaderCell>Certificado</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {detalhe?.participantes.map((participante) => (
              <TableRow key={participante.id}>
                <TableCell>{participante.trabalhadorNome}</TableCell>
                <TableCell>{participante.trabalhadorMatricula}</TableCell>
                <TableCell>
                  {participante.presencaConfirmadaEm ? (
                    <Badge color="success" appearance="tint" icon={<Checkmark24Filled />}>
                      Confirmada às {new Date(participante.presencaConfirmadaEm).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
                    </Badge>
                  ) : somenteLeitura ? (
                    <Badge color="danger" appearance="tint">
                      Ausente
                    </Badge>
                  ) : (
                    <Badge color="warning" appearance="tint">
                      Aguardando
                    </Badge>
                  )}
                </TableCell>
                <TableCell>
                  {participante.treinamentoGeradoId && (
                    <div style={{ display: 'flex', gap: 4 }}>
                      <Button
                        appearance="subtle"
                        size="small"
                        icon={<Signature24Regular />}
                        onClick={() => navigate(`/treinamentos/${participante.treinamentoGeradoId}/assinar`)}
                      >
                        Assinar
                      </Button>
                      <Button
                        appearance="subtle"
                        size="small"
                        icon={<ArrowDownload24Regular />}
                        onClick={() => baixarCertificado(participante.treinamentoGeradoId!)}
                        disabled={baixandoId === participante.treinamentoGeradoId}
                      >
                        Baixar
                      </Button>
                    </div>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
