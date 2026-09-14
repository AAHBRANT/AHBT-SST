import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Campo,
  Card,
  CampoData,
  Carregando,
  DetailPageLayout,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  Select,
  StatusChip,
  Text,
  Textarea,
  WorkflowActions,
  type AcaoWorkflow,
  type Tom,
} from '@ui';
import {
  ArrowDownload24Regular,
  Open24Regular,
  Save24Regular,
  Signature24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
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
import { SlotFoto } from '../../components/camera/SlotFoto';

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

// EmAndamento/Concluida — mesmo mapeamento de InspecoesTab.tsx (Onda 2 ruling: mesmos tons entre
// lista e detalhe do módulo).
const tomPorStatusInspecao: Record<number, Tom> = {
  [StatusInspecao.EmAndamento]: 'info',
  [StatusInspecao.Concluida]: 'ok',
};

// Cor do status do ponto verificado — mesmo esquema verde/amarelo da planilha "Patrulha de Segurança do
// Trabalho" (pendente = ainda não conforme, resolvido = já corrigido e reavaliado como conforme).
const tomPorStatusItem: Record<number, Tom> = {
  [StatusItemChecklist.Conforme]: 'ok',
  [StatusItemChecklist.NaoConforme]: 'atencao',
  [StatusItemChecklist.NaoAplicavel]: 'info',
};

// Onda 2 Task 10 (camada ui/): detalhe de uma execução de inspeção. DetailPageLayout com resumo e a
// única ação de fluxo (encerrar) na lateral — "Baixar PDF" e "Assinar inspeção" ficam no cabeçalho
// por serem navegação/exportação, não transição de estado (mesmo critério de AprDetalhePage.tsx: só
// aprovar/reprovar entram em WorkflowActions, exportar fica em cabecalho.acoes). Retorno antecipado
// com OU erro OU skeleton na carga inicial, nunca cabeçalho+skeleton+erro empilhados (padrão já
// revisado em AprDetalhePage.tsx/PgrDetalhePage.tsx).
export function InspecaoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<InspecaoDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [edicoes, setEdicoes] = useState<Record<string, EdicaoResposta>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [gerandoOcorrenciaId, setGerandoOcorrenciaId] = useState<string | null>(null);
  const [baixandoPdf, setBaixandoPdf] = useState(false);
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});
  const [fotoDepoisUrls, setFotoDepoisUrls] = useState<Record<string, string>>({});

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

  // Miniaturas das evidências (antes/depois) são baixadas sob demanda (só para respostas com
  // temFoto/temFotoDepois) e mantidas como object URL até a página ser desmontada — mesmo padrão de
  // TrabalhadoresTab.baixarFoto/ObrasPage.baixarLogo.
  useEffect(() => {
    let cancelado = false;
    (async () => {
      if (!detalhe) return;
      for (const resposta of detalhe.respostas) {
        if (resposta.temFoto && !fotoUrls[resposta.id]) {
          try {
            const blob = await api.inspecoes.baixarFoto(resposta.id);
            if (cancelado) return;
            setFotoUrls((atual) => ({ ...atual, [resposta.id]: URL.createObjectURL(blob) }));
          } catch {
            // Falha ao carregar miniatura não impede o uso da página; o slot fica vazio.
          }
        }
        if (resposta.temFotoDepois && !fotoDepoisUrls[resposta.id]) {
          try {
            const blob = await api.inspecoes.baixarFotoDepois(resposta.id);
            if (cancelado) return;
            setFotoDepoisUrls((atual) => ({ ...atual, [resposta.id]: URL.createObjectURL(blob) }));
          } catch {
            // Falha ao carregar miniatura não impede o uso da página; o slot fica vazio.
          }
        }
      }
    })();
    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detalhe]);

  useEffect(() => {
    return () => {
      Object.values(fotoUrls).forEach((url) => URL.revokeObjectURL(url));
      Object.values(fotoDepoisUrls).forEach((url) => URL.revokeObjectURL(url));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function atualizarEdicao(respostaId: string, campos: Partial<EdicaoResposta>) {
    setEdicoes((atual) => ({
      ...atual,
      [respostaId]: { ...(atual[respostaId] ?? edicaoInicial()), ...campos },
    }));
  }

  async function salvarResposta(respostaId: string, descricaoOriginal: string) {
    const edicao = edicoes[respostaId];
    if (!edicao?.statusItem) {
      setErro('Selecione o status do ponto verificado antes de salvar.');
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
      setErro(e instanceof Error ? e.message : 'Falha ao salvar o ponto verificado.');
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

  async function enviarFotoDepois(respostaId: string, arquivo: File) {
    try {
      setErro(null);
      await api.inspecoes.anexarFotoDepois(respostaId, arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a evidência posterior.');
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

  // Entra em WorkflowActions: retornar `false` mantém o formulário aberto (aqui não há formulário —
  // a ação executa direto — mas o contrato pede erro tratado sem lançar, mesmo padrão de
  // AprDetalhePage.tsx).
  async function encerrar() {
    if (!id) return false;
    try {
      setProcessando(true);
      setErro(null);
      await api.inspecoes.encerrar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar inspeção. Confira se todos os itens foram respondidos.');
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

  if (!id) return <FeedbackInline tom="erro">Inspeção não encontrada.</FeedbackInline>;

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre e a falha
  // (API fora, id inexistente) não teria onde ser lida.
  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  const inspecao = detalhe.inspecao;

  const acoes: AcaoWorkflow[] = [];
  if (inspecao.status === StatusInspecao.EmAndamento) {
    acoes.push({
      chave: 'encerrar',
      rotulo: 'Encerrar inspeção',
      descricao: 'Confira se todos os itens foram respondidos antes de encerrar.',
      tom: 'primario',
      habilitada: true,
      aoExecutar: encerrar,
    });
  }

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: `${tipoInspecaoLabel[inspecao.tipoInspecao]} — ${inspecao.checklistModeloNome} (v${inspecao.checklistModeloVersao})`,
        status: (
          <StatusChip tom={tomPorStatusInspecao[inspecao.status] ?? 'neutro'}>
            {statusInspecaoLabel[inspecao.status]}
          </StatusChip>
        ),
        voltarPara: '/prevencao/inspecoes',
        rotuloVoltar: 'Voltar para Inspeções',
        acoes: (
          <>
            <Button appearance="secondary" icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf}>
              Baixar PDF
            </Button>
            {inspecao.status === StatusInspecao.Concluida && (
              <Button
                appearance="primary"
                icon={<Signature24Regular />}
                onClick={() => navigate(`/prevencao/inspecoes/${id}/assinar`)}
              >
                Assinar inspeção
              </Button>
            )}
          </>
        ),
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              <Campo span={12}>
                <Field label="Obra">
                  <Input value={inspecao.obraNome} readOnly />
                </Field>
              </Campo>
              {inspecao.atividadeNome && (
                <Campo span={12}>
                  <Field label="Atividade">
                    <Input value={inspecao.atividadeNome} readOnly />
                  </Field>
                </Campo>
              )}
              <Campo span={12}>
                <Field label="Data">
                  <Input value={inspecao.data?.slice(0, 10) ?? ''} readOnly />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Responsável">
                  <Input value={inspecao.responsavelUsuarioNome} readOnly />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Pontos verificados respondidos">
                  <Input value={`${inspecao.itensRespondidos}/${inspecao.totalItens}`} readOnly />
                </Field>
              </Campo>
            </FormGrid>
            {inspecao.itensNaoConformes > 0 && (
              <div style={{ marginTop: 12 }}>
                <StatusChip tom="atencao">{inspecao.itensNaoConformes} pendente(s)</StatusChip>
              </div>
            )}
          </Card>
          {acoes.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis">
              <WorkflowActions acoes={acoes} processando={processando} />
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

      <Text weight="semibold">Pontos verificados</Text>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        {detalhe.respostas.map((resposta, indice) => {
          const edicao = edicoes[resposta.id] ?? edicaoInicial();
          const somenteLeitura = inspecao.status !== StatusInspecao.EmAndamento;
          // Cabeçalho de seção só aparece na fronteira entre seções diferentes — checklists sem
          // Secao preenchida (todos os anteriores ao campo existir) continuam em lista corrida.
          const secaoAnterior = indice > 0 ? detalhe.respostas[indice - 1].secao : undefined;
          const mostrarCabecalhoSecao = !!resposta.secao && resposta.secao !== secaoAnterior;
          return (
            <div key={resposta.id}>
              {mostrarCabecalhoSecao && (
                <Text weight="semibold" style={{ display: 'block', margin: '16px 0 8px' }}>
                  {resposta.secao}
                </Text>
              )}
              <Card densidade="compacta">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
                <div style={{ flex: 1, minWidth: 260 }}>
                  <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                    Ponto verificado {resposta.ordem}
                  </Text>
                  <Field label="Descrição do ponto verificado">
                    <Input
                      value={edicao.descricao}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { descricao: d.value })}
                      disabled={somenteLeitura}
                      placeholder="Descreva a irregularidade encontrada"
                    />
                  </Field>
                </div>
                <div style={{ minWidth: 180 }}>
                  <Field label="Status do ponto verificado">
                    <Select
                      value={edicao.statusItem}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { statusItem: d.value })}
                      disabled={somenteLeitura}
                      style={{ width: '100%' }}
                    >
                      <option value="">Selecione o status</option>
                      {Object.entries(statusItemChecklistLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </div>
                {resposta.statusItem != null && (
                  <StatusChip tom={tomPorStatusItem[resposta.statusItem] ?? 'neutro'}>
                    {statusItemChecklistLabel[resposta.statusItem]}
                  </StatusChip>
                )}
              </div>

              <div style={{ display: 'flex', gap: 12, marginTop: 12, flexWrap: 'wrap' }}>
                <div style={{ flex: 1, minWidth: 200 }}>
                  <Field label="Local">
                    <Input
                      value={edicao.local}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { local: d.value })}
                      disabled={somenteLeitura}
                    />
                  </Field>
                </div>
                <div style={{ flex: 1, minWidth: 200 }}>
                  <Field label="Responsável">
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
                  </Field>
                </div>
                <div style={{ minWidth: 160 }}>
                  <Field label="Prazo">
                    <CampoData
                      value={edicao.prazo}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { prazo: d.value })}
                      disabled={somenteLeitura}
                    />
                  </Field>
                </div>
              </div>

              <div style={{ marginTop: 12 }}>
                <Field label="Plano de ação">
                  <Textarea
                    value={edicao.planoDeAcao}
                    onChange={(_, d) => atualizarEdicao(resposta.id, { planoDeAcao: d.value })}
                    disabled={somenteLeitura}
                    resize="vertical"
                  />
                </Field>
              </div>

              <div style={{ marginTop: 12 }}>
                <Field label="Observação">
                  <Textarea
                    value={edicao.observacao}
                    onChange={(_, d) => atualizarEdicao(resposta.id, { observacao: d.value })}
                    disabled={somenteLeitura}
                    resize="vertical"
                  />
                </Field>
              </div>

              {/* Evidências no mesmo padrão de quadro/miniatura usado em todo o sistema (pedido do
                  usuário, 14/09) — ver components/camera/SlotFoto.tsx. */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 16, marginTop: 12 }}>
                <Field label="Evidência anterior">
                  <div style={{ maxWidth: 160 }}>
                    <SlotFoto
                      rotulo="Evidência anterior"
                      url={fotoUrls[resposta.id]}
                      carregandoMiniatura={resposta.temFoto && !fotoUrls[resposta.id]}
                      somenteLeitura={somenteLeitura}
                      aoSelecionarArquivo={(arquivo) => enviarFoto(resposta.id, arquivo)}
                      aoErroValidacao={setErro}
                    />
                  </div>
                </Field>

                <Field label="Evidência posterior">
                  <div style={{ maxWidth: 160 }}>
                    <SlotFoto
                      rotulo="Evidência posterior"
                      url={fotoDepoisUrls[resposta.id]}
                      carregandoMiniatura={resposta.temFotoDepois && !fotoDepoisUrls[resposta.id]}
                      somenteLeitura={somenteLeitura}
                      aoSelecionarArquivo={(arquivo) => enviarFotoDepois(resposta.id, arquivo)}
                      aoErroValidacao={setErro}
                    />
                  </div>
                </Field>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap', marginTop: 16 }}>
                <div>
                  {resposta.naoConformidadeId ? (
                    <Button
                      appearance="secondary"
                      icon={<Open24Regular />}
                      onClick={() => navigate(`/nao-conformidades/${resposta.naoConformidadeId}`)}
                    >
                      Ver ocorrência
                    </Button>
                  ) : (
                    resposta.statusItem === StatusItemChecklist.NaoConforme && (
                      <Button
                        appearance="secondary"
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
                    Salvar ponto verificado
                  </Button>
                )}
              </div>
              </Card>
            </div>
          );
        })}
      </div>
    </DetailPageLayout>
  );
}
