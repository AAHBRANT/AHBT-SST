import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Text,
  Textarea,
} from '@fluentui/react-components';
import { ArrowDownload24Regular, Checkmark24Filled, Signature24Regular } from '@fluentui/react-icons';
import { FeedbackInline, StatusChip } from '@ui';
import { api, MetodoAutenticacaoAssinatura, SituacaoTermoCompromissoEpi, type TermoCompromissoEpi } from '../../lib/api';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { CampoData } from '../CampoData';
import { salvarBlob } from '../useVisualizadorPdf';
import { clausulasTermoCompromisso } from './termoCompromissoEpi';

export interface TermoCompromissoEpiDialogProps {
  open: boolean;
  onClose: () => void;
  trabalhadorId: string;
  /** Opcional: a aba do perfil só conhece o id do funcionário. */
  trabalhadorNome?: string;
  /** Chamado depois que o termo muda de situação (registro manual ou remoção), para a tela recarregar. */
  aoAlterar?: () => void;
}

function formatarData(iso?: string | null) {
  if (!iso) return '—';
  const [ano, mes, dia] = iso.slice(0, 10).split('-');
  return `${dia}/${mes}/${ano}`;
}

function formatarDataHora(iso?: string | null) {
  if (!iso) return '—';
  const utc = /[zZ]|[+-]\d{2}:\d{2}$/.test(iso) ? iso : `${iso}Z`;
  return new Date(utc).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo', dateStyle: 'short', timeStyle: 'short' });
}

function rotuloMetodo(metodo?: number | null) {
  if (metodo === MetodoAutenticacaoAssinatura.ReconhecimentoFacial) return 'biometria facial';
  if (metodo === MetodoAutenticacaoAssinatura.Biometria) return 'biometria digital';
  return 'assinatura eletrônica';
}

// "Assinatura de termo de recebimento e compromisso de uso" (03/10). Um só botão com dois caminhos:
// assinar agora (digital/facial, pelo quiosque do Motor de Assinatura) ou registrar que o
// funcionário JÁ assinou em papel — o sistema nunca fabrica assinatura eletrônica de quem assinou
// à mão. O registro manual guarda a data do papel, quem lançou, uma observação e, opcionalmente, a
// foto/PDF do papel; só o Administrador remove.
export function TermoCompromissoEpiDialog({ open, onClose, trabalhadorId, trabalhadorNome, aoAlterar }: TermoCompromissoEpiDialogProps) {
  const navigate = useNavigate();
  const souAdministrador = useSouAdministrador();
  const [termo, setTermo] = useState<TermoCompromissoEpi | null>(null);
  const [nomeObra, setNomeObra] = useState<string | null>(null);
  const [nr6, setNr6] = useState<{ numero?: string | null; data?: string | null }>({});
  const [modoPapel, setModoPapel] = useState(false);
  const [dataPapel, setDataPapel] = useState('');
  const [observacao, setObservacao] = useState('');
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [confirmandoRemocao, setConfirmandoRemocao] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [situacao, certificados] = await Promise.all([
        api.termosCompromissoEpi.obter(trabalhadorId),
        api.treinamentos.listarCertificados({ trabalhadorId }).catch(() => []),
      ]);
      setTermo(situacao);
      // Nome da obra do funcionário (o consórcio) para a cláusula 1 do termo.
      void Promise.all([api.trabalhadores.listar(), api.obras.listar()])
        .then(([trabalhadores, obras]) => {
          const obraId = trabalhadores.find((t) => t.id === trabalhadorId)?.obraId;
          setNomeObra(obras.find((o) => o.id === obraId)?.nome ?? null);
        })
        .catch(() => undefined);
      // Mesma escolha do backend/PDF: entre os certificados de NR-06, o de validade mais distante.
      const nr6Escolhido = certificados
        .filter((c) => c.atendeNr6)
        .sort((a, b) => b.dataValidade.localeCompare(a.dataValidade))[0];
      setNr6({ numero: nr6Escolhido?.numeroCertificado ?? null, data: nr6Escolhido?.dataRealizacao?.slice(0, 10) ?? null });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a situação do termo.');
    }
  }

  useEffect(() => {
    if (!open) return;
    setModoPapel(false);
    setDataPapel('');
    setObservacao('');
    setArquivo(null);
    setConfirmandoRemocao(false);
    setTermo(null);
    void carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, trabalhadorId]);

  async function registrarPapel() {
    if (!dataPapel) {
      setErro('Informe a data em que o funcionário assinou o papel.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.termosCompromissoEpi.registrarManual(trabalhadorId, dataPapel, observacao, arquivo);
      setModoPapel(false);
      await carregar();
      aoAlterar?.();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar a assinatura em papel.');
    } finally {
      setSalvando(false);
    }
  }

  async function baixarArquivo() {
    try {
      const blob = await api.termosCompromissoEpi.baixarArquivoManual(trabalhadorId);
      salvarBlob(blob, termo?.arquivoNome ?? 'termo-assinado.pdf');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o arquivo do termo.');
    }
  }

  async function removerRegistro() {
    try {
      setSalvando(true);
      setErro(null);
      await api.termosCompromissoEpi.removerManual(trabalhadorId);
      setConfirmandoRemocao(false);
      await carregar();
      aoAlterar?.();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao remover o registro.');
    } finally {
      setSalvando(false);
    }
  }

  const situacao = termo?.situacao;

  return (
    <Dialog open={open} onOpenChange={(_, dados) => !dados.open && onClose()}>
      <DialogSurface style={{ maxWidth: 720, width: '100%' }}>
        <DialogBody>
          <DialogTitle>Termo de Recebimento e Compromisso de Uso{trabalhadorNome ? ` — ${trabalhadorNome}` : ''}</DialogTitle>
          <DialogContent>
            {erro && (
              <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
                {erro}
              </FeedbackInline>
            )}

            {!termo && !erro && <Text>Carregando…</Text>}

            {termo && (
              <div style={{ display: 'grid', gap: 12 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  {situacao === SituacaoTermoCompromissoEpi.Digital && (
                    <StatusChip tom="ok">Assinado digitalmente em {formatarDataHora(termo.dataAssinatura)} · {rotuloMetodo(termo.metodo)}</StatusChip>
                  )}
                  {situacao === SituacaoTermoCompromissoEpi.Manual && (
                    <StatusChip tom="info">Assinado manualmente (em papel) em {formatarData(termo.dataAssinatura)}</StatusChip>
                  )}
                  {situacao === SituacaoTermoCompromissoEpi.Pendente && <StatusChip tom="atencao">Termo pendente</StatusChip>}
                </div>

                {situacao === SituacaoTermoCompromissoEpi.Manual && (
                  <div style={{ display: 'grid', gap: 4 }}>
                    <Text size={200}>
                      Registrado no sistema por {termo.registradoPorNome ?? 'usuário não identificado'} em {formatarDataHora(termo.registradoEm)}.
                    </Text>
                    {termo.observacao && <Text size={200}>Observação: {termo.observacao}</Text>}
                    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      {termo.temArquivo && (
                        <Button icon={<ArrowDownload24Regular />} onClick={baixarArquivo}>
                          Baixar arquivo do termo
                        </Button>
                      )}
                      {souAdministrador && !confirmandoRemocao && (
                        <Button appearance="subtle" onClick={() => setConfirmandoRemocao(true)}>
                          Remover registro
                        </Button>
                      )}
                      {souAdministrador && confirmandoRemocao && (
                        <>
                          <Text size={200}>Remover o registro de assinatura em papel?</Text>
                          <Button appearance="primary" disabled={salvando} onClick={removerRegistro}>
                            Confirmar remoção
                          </Button>
                          <Button appearance="subtle" onClick={() => setConfirmandoRemocao(false)}>
                            Cancelar
                          </Button>
                        </>
                      )}
                    </div>
                  </div>
                )}

                {situacao === SituacaoTermoCompromissoEpi.Pendente && !modoPapel && (
                  <>
                    <div style={{ display: 'grid', gap: 6 }}>
                      <Text weight="semibold">Cláusulas do termo</Text>
                      <ol style={{ margin: 0, paddingLeft: 20 }}>
                        {clausulasTermoCompromisso(nr6.numero, nr6.data, nomeObra).map((clausula, indice) => (
                          <li key={indice} style={{ marginBottom: 4 }}>
                            <Text size={200}>{clausula}</Text>
                          </li>
                        ))}
                      </ol>
                    </div>
                    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      <Button
                        appearance="primary"
                        icon={<Signature24Regular />}
                        onClick={() => {
                          onClose();
                          navigate(`/epi/termo/${trabalhadorId}/assinar`);
                        }}
                      >
                        Assinar agora (digital ou facial)
                      </Button>
                      <Button icon={<Checkmark24Filled />} onClick={() => setModoPapel(true)}>
                        Já assinou em papel — registrar
                      </Button>
                    </div>
                  </>
                )}

                {situacao === SituacaoTermoCompromissoEpi.Pendente && modoPapel && (
                  <div style={{ display: 'grid', gap: 12 }}>
                    <Text size={200}>
                      Use esta opção só se o funcionário já assinou o termo em papel. O registro fica identificado como
                      "assinado manualmente", com seu nome e a data de hoje; o sistema não gera assinatura eletrônica no lugar dele.
                    </Text>
                    <Field label="Data em que assinou o papel" required>
                      <CampoData value={dataPapel} onChange={(_, d) => setDataPapel(d.value)} />
                    </Field>
                    <Field label="Observação (opcional)">
                      <Textarea value={observacao} maxLength={500} onChange={(_, d) => setObservacao(d.value)} />
                    </Field>
                    <Field label="Foto ou PDF do termo em papel (opcional)">
                      <input
                        type="file"
                        accept="application/pdf,image/jpeg,image/png"
                        onChange={(ev) => setArquivo(ev.target.files?.[0] ?? null)}
                      />
                    </Field>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <Button appearance="primary" disabled={salvando} onClick={registrarPapel}>
                        {salvando ? 'Registrando…' : 'Registrar assinatura em papel'}
                      </Button>
                      <Button appearance="subtle" disabled={salvando} onClick={() => setModoPapel(false)}>
                        Voltar
                      </Button>
                    </div>
                  </div>
                )}
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              Fechar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
