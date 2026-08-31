import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Badge, Button, Input, Select, Text, Textarea } from '@fluentui/react-components';
import { ArrowDownload24Regular, ArrowLeft24Regular, LockClosed24Regular, Save24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  api,
  StatusInspecao,
  StatusItemChecklist,
  statusInspecaoLabel,
  statusItemChecklistLabel,
  tipoInspecaoLabel,
  type InspecaoDetalhe,
  type Usuario,
} from '../../lib/api';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { usePageStyles } from '../pageStyles';

interface EdicaoResposta {
  descricao: string;
  statusItem: string;
  observacao: string;
  local: string;
  planoDeAcao: string;
  responsavelUsuarioId: string;
  prazo: string;
}

function edicaoInicial(): EdicaoResposta {
  return { descricao: '', statusItem: '', observacao: '', local: '', planoDeAcao: '', responsavelUsuarioId: '', prazo: '' };
}

// Cor do status do achado — mesmo esquema verde/amarelo da planilha "Patrulha de Segurança do
// Trabalho" (pendente = ainda não conforme, resolvido = já corrigido e reavaliado como conforme).
function corStatusItem(statusItem?: number | null): 'success' | 'warning' | 'informative' | undefined {
  if (statusItem === StatusItemChecklist.Conforme) return 'success';
  if (statusItem === StatusItemChecklist.NaoConforme) return 'warning';
  if (statusItem === StatusItemChecklist.NaoAplicavel) return 'informative';
  return undefined;
}

export function InspecaoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<InspecaoDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [edicoes, setEdicoes] = useState<Record<string, EdicaoResposta>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoFotoId, setBaixandoFotoId] = useState<string | null>(null);
  const [baixandoFotoDepoisId, setBaixandoFotoDepoisId] = useState<string | null>(null);
  const [gerandoOcorrenciaId, setGerandoOcorrenciaId] = useState<string | null>(null);
  const [baixandoPdf, setBaixandoPdf] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaUsuarios] = await Promise.all([api.inspecoes.obterDetalhe(id), api.usuarios.listar()]);
      setDetalhe(det);
      setUsuarios(listaUsuarios);
      setEdicoes((atual) => {
        const novo: Record<string, EdicaoResposta> = {};
        for (const resposta of det.respostas) {
          novo[resposta.id] = atual[resposta.id] ?? {
            descricao: resposta.descricao,
            statusItem: resposta.statusItem != null ? String(resposta.statusItem) : '',
            observacao: resposta.observacao ?? '',
            local: resposta.local ?? '',
            planoDeAcao: resposta.planoDeAcao ?? '',
            responsavelUsuarioId: resposta.responsavelUsuarioId ?? '',
            prazo: resposta.prazo?.slice(0, 10) ?? '',
          };
        }
        return novo;
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar inspeção.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function atualizarEdicao(respostaId: string, campos: Partial<EdicaoResposta>) {
    setEdicoes((atual) => ({
      ...atual,
      [respostaId]: { ...(atual[respostaId] ?? edicaoInicial()), ...campos },
    }));
  }

  async function salvarResposta(respostaId: string, descricaoOriginal: string) {
    const edicao = edicoes[respostaId];
    if (!edicao?.statusItem) {
      setErro('Selecione o status do achado antes de salvar.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.inspecoes.responderItem(
        respostaId,
        Number(edicao.statusItem),
        edicao.observacao || null,
        edicao.responsavelUsuarioId || null,
        edicao.prazo || null,
        edicao.descricao !== descricaoOriginal ? edicao.descricao || null : undefined,
        edicao.local || null,
        edicao.planoDeAcao || null,
      );
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar o achado.');
    } finally {
      setProcessando(false);
    }
  }

  async function enviarFoto(respostaId: string, arquivo: File) {
    try {
      setErro(null);
      await api.inspecoes.anexarFoto(respostaId, arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a evidência anterior.');
    }
  }

  async function baixarFoto(respostaId: string) {
    try {
      setBaixandoFotoId(respostaId);
      setErro(null);
      const blob = await api.inspecoes.baixarFoto(respostaId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `inspecao-item-${respostaId}-antes`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a evidência anterior.');
    } finally {
      setBaixandoFotoId(null);
    }
  }

  async function enviarFotoDepois(respostaId: string, arquivo: File) {
    try {
      setErro(null);
      await api.inspecoes.anexarFotoDepois(respostaId, arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a evidência posterior.');
    }
  }

  async function baixarFotoDepois(respostaId: string) {
    try {
      setBaixandoFotoDepoisId(respostaId);
      setErro(null);
      const blob = await api.inspecoes.baixarFotoDepois(respostaId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `inspecao-item-${respostaId}-depois`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a evidência posterior.');
    } finally {
      setBaixandoFotoDepoisId(null);
    }
  }

  async function gerarOcorrencia(respostaId: string) {
    try {
      setGerandoOcorrenciaId(respostaId);
      setErro(null);
      const { id: ncId } = await api.inspecoes.gerarOcorrencia(respostaId, {});
      await carregar();
      navigate(`/nao-conformidades/${ncId}`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar ocorrência a partir do item.');
    } finally {
      setGerandoOcorrenciaId(null);
    }
  }

  async function encerrar() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.inspecoes.encerrar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar inspeção. Confira se todos os itens foram respondidos.');
    } finally {
      setProcessando(false);
    }
  }

  async function baixarPdf() {
    if (!id || !detalhe) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.inspecoes.baixarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `inspecao-${detalhe.inspecao.data?.slice(0, 10)}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o PDF da inspeção.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  if (!id) {
    return <Text>Inspeção não encontrada.</Text>;
  }

  const inspecao = detalhe?.inspecao;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/prevencao/inspecoes')}
        style={{ marginBottom: 12 }}
      >
        Voltar para Inspeções
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {inspecao ? (
          <>
            <Text size={500} weight="semibold">
              {tipoInspecaoLabel[inspecao.tipoInspecao]} — {inspecao.checklistModeloNome} (v
              {inspecao.checklistModeloVersao})
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Obra: {inspecao.obraNome}</Text>
              {inspecao.atividadeNome && <Text>Atividade: {inspecao.atividadeNome}</Text>}
              <Text>Data: {inspecao.data?.slice(0, 10)}</Text>
              <Text>Responsável: {inspecao.responsavelUsuarioNome}</Text>
              <Badge appearance="tint">{statusInspecaoLabel[inspecao.status]}</Badge>
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, alignItems: 'center' }}>
              <Text>
                Achados respondidos: {inspecao.itensRespondidos}/{inspecao.totalItens}
              </Text>
              {inspecao.itensNaoConformes > 0 && (
                <Badge color="warning" appearance="tint">
                  {inspecao.itensNaoConformes} pendente(s)
                </Badge>
              )}
            </div>

            <div className={estilos.formActions} style={{ marginTop: 16 }}>
              <Button appearance="secondary" icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf}>
                Baixar PDF
              </Button>
              {inspecao.status === StatusInspecao.EmAndamento && (
                <Button appearance="primary" icon={<LockClosed24Regular />} onClick={encerrar} disabled={processando}>
                  Encerrar inspeção
                </Button>
              )}
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
        Achados
      </Text>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        {detalhe?.respostas.map((resposta) => {
          const edicao = edicoes[resposta.id] ?? edicaoInicial();
          const somenteLeitura = inspecao?.status !== StatusInspecao.EmAndamento;
          return (
            <div key={resposta.id} className={estilos.card}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, flex: 1, minWidth: 260 }}>
                  <Text weight="semibold">{resposta.ordem}.</Text>
                  <Input
                    value={edicao.descricao}
                    onChange={(_, d) => atualizarEdicao(resposta.id, { descricao: d.value })}
                    disabled={somenteLeitura}
                    style={{ flex: 1 }}
                    placeholder="Descrição do achado / irregularidade encontrada"
                  />
                </div>
                <Select
                  value={edicao.statusItem}
                  onChange={(_, d) => atualizarEdicao(resposta.id, { statusItem: d.value })}
                  disabled={somenteLeitura}
                  style={{ minWidth: 160 }}
                >
                  <option value="">Selecione o status</option>
                  {Object.entries(statusItemChecklistLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
                {resposta.statusItem != null && (
                  <Badge color={corStatusItem(resposta.statusItem)} appearance="tint">
                    {statusItemChecklistLabel[resposta.statusItem]}
                  </Badge>
                )}
              </div>

              <div style={{ display: 'flex', gap: 12, marginTop: 12, flexWrap: 'wrap' }}>
                <div style={{ flex: 1, minWidth: 200 }}>
                  <Text size={200} block style={{ marginBottom: 2 }}>Local</Text>
                  <Input
                    value={edicao.local}
                    onChange={(_, d) => atualizarEdicao(resposta.id, { local: d.value })}
                    disabled={somenteLeitura}
                    style={{ width: '100%' }}
                  />
                </div>
                <div style={{ flex: 1, minWidth: 200 }}>
                  <Text size={200} block style={{ marginBottom: 2 }}>Responsável</Text>
                  <Select
                    value={edicao.responsavelUsuarioId}
                    onChange={(_, d) => atualizarEdicao(resposta.id, { responsavelUsuarioId: d.value })}
                    disabled={somenteLeitura}
                    style={{ width: '100%' }}
                  >
                    <option value="">Selecione</option>
                    {usuarios.map((usuario) => (
                      <option key={usuario.id} value={usuario.id}>
                        {usuario.nome}
                      </option>
                    ))}
                  </Select>
                </div>
                <div style={{ minWidth: 160 }}>
                  <Text size={200} block style={{ marginBottom: 2 }}>Prazo</Text>
                  <Input
                    type="date"
                    value={edicao.prazo}
                    onChange={(_, d) => atualizarEdicao(resposta.id, { prazo: d.value })}
                    disabled={somenteLeitura}
                  />
                </div>
              </div>

              <div style={{ marginTop: 12 }}>
                <Text size={200} block style={{ marginBottom: 2 }}>Plano de ação</Text>
                <Textarea
                  value={edicao.planoDeAcao}
                  onChange={(_, d) => atualizarEdicao(resposta.id, { planoDeAcao: d.value })}
                  disabled={somenteLeitura}
                  resize="vertical"
                  style={{ width: '100%' }}
                />
              </div>

              <div style={{ marginTop: 12 }}>
                <Text size={200} block style={{ marginBottom: 2 }}>OBS</Text>
                <Textarea
                  value={edicao.observacao}
                  onChange={(_, d) => atualizarEdicao(resposta.id, { observacao: d.value })}
                  disabled={somenteLeitura}
                  resize="vertical"
                  style={{ width: '100%' }}
                />
              </div>

              <div style={{ display: 'flex', gap: 24, marginTop: 12, flexWrap: 'wrap' }}>
                <div>
                  <Text size={200} block weight="semibold" style={{ marginBottom: 4 }}>Evidência anterior</Text>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'flex-start' }}>
                    {!somenteLeitura && (
                      <SeletorFotoCamera
                        rotulo="Tirar foto"
                        aoSelecionarArquivo={(arquivo) => enviarFoto(resposta.id, arquivo)}
                      />
                    )}
                    {resposta.temFoto && (
                      <Button
                        appearance="subtle"
                        size="small"
                        icon={<ArrowDownload24Regular />}
                        onClick={() => baixarFoto(resposta.id)}
                        disabled={baixandoFotoId === resposta.id}
                      >
                        Ver foto
                      </Button>
                    )}
                  </div>
                </div>

                <div>
                  <Text size={200} block weight="semibold" style={{ marginBottom: 4 }}>Evidência posterior</Text>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'flex-start' }}>
                    {!somenteLeitura && (
                      <SeletorFotoCamera
                        rotulo="Tirar foto"
                        aoSelecionarArquivo={(arquivo) => enviarFotoDepois(resposta.id, arquivo)}
                      />
                    )}
                    {resposta.temFotoDepois && (
                      <Button
                        appearance="subtle"
                        size="small"
                        icon={<ArrowDownload24Regular />}
                        onClick={() => baixarFotoDepois(resposta.id)}
                        disabled={baixandoFotoDepoisId === resposta.id}
                      >
                        Ver foto
                      </Button>
                    )}
                  </div>
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 12 }}>
                <div>
                  {resposta.naoConformidadeId ? (
                    <Button appearance="subtle" size="small" onClick={() => navigate(`/nao-conformidades/${resposta.naoConformidadeId}`)}>
                      Ver ocorrência
                    </Button>
                  ) : (
                    resposta.statusItem === StatusItemChecklist.NaoConforme && (
                      <Button
                        appearance="subtle"
                        size="small"
                        icon={<Warning24Regular />}
                        onClick={() => gerarOcorrencia(resposta.id)}
                        disabled={gerandoOcorrenciaId === resposta.id}
                      >
                        Gerar ocorrência
                      </Button>
                    )
                  )}
                </div>
                {!somenteLeitura && (
                  <Button
                    appearance="primary"
                    icon={<Save24Regular />}
                    onClick={() => salvarResposta(resposta.id, resposta.descricao)}
                    disabled={processando}
                  >
                    Salvar achado
                  </Button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
