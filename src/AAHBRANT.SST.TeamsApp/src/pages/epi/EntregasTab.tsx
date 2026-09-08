import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button, Field, Input, Select, CampoData,
  Card, PageHeader, DataTable, StatusChip, nivelVencimento, tomDeVencimento, rotuloDeVencimento,
  PainelLateral, FormSection, FormGrid, Campo, SeletorPesquisavel, FeedbackInline,
  type Coluna,
} from '@ui';
import { Add24Regular, ArrowDownload24Regular, Signature24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaEpiLabel,
  MotivoEntregaEpi,
  type AtualizarEntregaEpi,
  type CatalogoEpi,
  type CursoTreinamento,
  type EntregaEpi,
  type NovaEntregaEpi,
  type Trabalhador,
} from '../../lib/api';
import { AssinaturaEntregaEpiDialog } from '../../components/assinatura/AssinaturaEntregaEpiDialog';
import { AssinaturaDevolucaoEpiDialog } from '../../components/assinatura/AssinaturaDevolucaoEpiDialog';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';

function entregaVazia(): NovaEntregaEpi {
  return {
    trabalhadorId: '',
    catalogoEpiId: '',
    dataEntrega: new Date().toISOString().slice(0, 10),
    dataDevolucao: '',
    dataValidade: '',
    quantidade: 1,
    vistoConsorcioResponsavel: '',
    motivo: '',
    observacoes: '',
    motivoTipo: MotivoEntregaEpi.Inicial,
    numeroListaPresencaNr6: '',
    dataTreinamentoNr6: '',
  };
}

// Entregas de EPI do módulo dedicado /epi — registro, devolução (repõe estoque no backend),
// ficha em PDF e atalho para a assinatura eletrônica (AssinarEntregaEpiPage). Primeira página na
// camada ui/ (piloto 1 da Onda 1, spec §5.1): nada de Fluent cru nem de pageStyles aqui — lista em
// DataTable com chips de vencimento e o formulário de criação em PainelLateral. O bloqueio de
// estoque insuficiente / CA vencido acontece no backend (CriarEntregaEpiCommand); o erro retornado
// é exibido como veio, mesmo padrão já usado em todo o resto do frontend (ver api.ts request()).
// "NR-06"/"NR-6"/"NR 06" etc. — compara só o número, não o formato exato do texto cadastrado no
// curso (ver CursoTreinamento.normaReferencia, campo livre).
function ehNormaNr6(normaReferencia?: string | null): boolean {
  return (normaReferencia ?? '').replace(/\D/g, '') === '6';
}

interface EntregasTabProps {
  aoNavegarParaMatriz: () => void;
}

export function EntregasTab({ aoNavegarParaMatriz }: EntregasTabProps) {
  const navigate = useNavigate();
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [episPermitidos, setEpisPermitidos] = useState<CatalogoEpi[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novaEntrega, setNovaEntrega] = useState<NovaEntregaEpi>(entregaVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [devolucaoId, setDevolucaoId] = useState<string | null>(null);
  const [devolucaoData, setDevolucaoData] = useState('');
  const [devolucaoQtd, setDevolucaoQtd] = useState('');
  const [entregaParaAssinar, setEntregaParaAssinar] = useState<EntregaEpi | null>(null);
  const [devolucaoParaAssinar, setDevolucaoParaAssinar] = useState<EntregaEpi | null>(null);

  async function carregar() {
    try {
      setCarregandoLista(true);
      setErro(null);
      const [lista, listaEpis, listaTrabalhadores] = await Promise.all([
        api.entregasEpi.listar(),
        api.catalogosEpi.listar(),
        api.trabalhadores.listar(),
      ]);
      setEntregas(lista);
      setEpis(listaEpis);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de EPI.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    api.cursosTreinamento.listar().then(setCursos).catch(() => setCursos([]));
  }, []);

  // Pedido do usuário (03/09): não faz sentido digitar manualmente o nº da lista de presença e a
  // data do treinamento de NR-06 se o funcionário já tem esse treinamento cadastrado no módulo de
  // Treinamentos — busca automaticamente o mais recente ao trocar de funcionário (o usuário ainda
  // pode sobrescrever os campos à mão, ex.: quando o treinamento ainda não foi cadastrado no sistema).
  useEffect(() => {
    let cancelado = false;
    async function preencherDadosNr6() {
      if (!novaEntrega.trabalhadorId) return;
      try {
        const treinamentosTrabalhador = await api.treinamentos.listar(novaEntrega.trabalhadorId);
        if (cancelado) return;
        const treinamentoNr6 = treinamentosTrabalhador
          .filter((t) => ehNormaNr6(cursos.find((c) => c.id === t.cursoTreinamentoId)?.normaReferencia))
          .sort((a, b) => b.dataRealizacao.localeCompare(a.dataRealizacao))[0];
        if (!treinamentoNr6) return;
        setNovaEntrega((atual) =>
          atual.trabalhadorId === treinamentoNr6.trabalhadorId
            ? {
                ...atual,
                numeroListaPresencaNr6: treinamentoNr6.numeroCertificado ?? '',
                dataTreinamentoNr6: treinamentoNr6.dataRealizacao.slice(0, 10),
              }
            : atual,
        );
      } catch {
        // Falha ao buscar o treinamento não impede o preenchimento manual dos campos.
      }
    }
    preencherDadosNr6();
    return () => {
      cancelado = true;
    };
  }, [novaEntrega.trabalhadorId, cursos]);

  useEffect(() => {
    let cancelado = false;
    async function carregarEpisPermitidos() {
      const trabalhador = trabalhadores.find((t) => t.id === novaEntrega.trabalhadorId);
      if (!trabalhador) {
        setEpisPermitidos([]);
        return;
      }
      try {
        const lista = await api.funcoes.listarEpis(trabalhador.funcaoId);
        if (!cancelado) setEpisPermitidos(lista);
      } catch {
        if (!cancelado) setEpisPermitidos([]);
      }
    }
    carregarEpisPermitidos();
    return () => {
      cancelado = true;
    };
  }, [novaEntrega.trabalhadorId, trabalhadores]);

  // SeletorPesquisavel pede a lista memoizada (a de trabalhadores é a maior do app).
  const opcoesTrabalhadores = useMemo(
    () => trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome, descricao: t.matricula })),
    [trabalhadores],
  );

  function nomeEpi(id: string) {
    return epis.find((e) => e.id === id)?.nome ?? id;
  }

  function epiTemFoto(id: string) {
    return epis.find((e) => e.id === id)?.temFoto ?? false;
  }

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  async function criar() {
    if (!novaEntrega.trabalhadorId || !novaEntrega.catalogoEpiId || !novaEntrega.dataEntrega || novaEntrega.quantidade < 1) {
      setErro('Preencha funcionário, EPI, data de entrega e quantidade.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      const payload: NovaEntregaEpi = {
        ...novaEntrega,
        dataDevolucao: novaEntrega.dataDevolucao || null,
        dataValidade: novaEntrega.dataValidade || null,
        numeroListaPresencaNr6: novaEntrega.numeroListaPresencaNr6 || null,
        dataTreinamentoNr6: novaEntrega.dataTreinamentoNr6 || null,
      };
      const { id } = await api.entregasEpi.criar(payload);
      setEntregaParaAssinar({ ...payload, id });
      setNovaEntrega(entregaVazia());
      await carregar();
      setPainelAberto(false);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar entrega de EPI.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarDevolucao(entrega: EntregaEpi) {
    setDevolucaoId(entrega.id);
    setDevolucaoData(new Date().toISOString().slice(0, 10));
    setDevolucaoQtd(String(entrega.quantidade));
  }

  async function confirmarDevolucao(entrega: EntregaEpi) {
    try {
      setCarregando(true);
      setErro(null);
      const atualizada: AtualizarEntregaEpi = {
        ...entrega,
        // Entregas registradas antes da Fase 2 podem não ter motivoTipo (campo era opcional);
        // o backend exige o enum em toda atualização, então assume Inicial como padrão seguro.
        motivoTipo: entrega.motivoTipo ?? MotivoEntregaEpi.Inicial,
        dataDevolucao: devolucaoData,
        quantidadeDevolucao: Number(devolucaoQtd) || entrega.quantidade,
      };
      await api.entregasEpi.atualizar(atualizada);
      setDevolucaoId(null);
      setDevolucaoParaAssinar(atualizada);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar devolução.');
    } finally {
      setCarregando(false);
    }
  }

  async function baixarFicha(trabalhadorId: string) {
    try {
      setBaixandoId(trabalhadorId);
      const blob = await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ficha-epi-${trabalhadorId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ficha em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  // Coluna de devolução com edição na própria linha: o DataTable não tem edição inline embutida
  // (e não deveria — é caso desta página), então o estado de devolução entra pelo `render`.
  const colunas: Coluna<EntregaEpi>[] = [
    { chave: 'trabalhador', rotulo: 'Funcionário', render: (e) => nomeTrabalhador(e.trabalhadorId) },
    {
      chave: 'epi',
      rotulo: 'EPI',
      render: (e) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <FotoCatalogoEpi catalogoEpiId={e.catalogoEpiId} temFoto={epiTemFoto(e.catalogoEpiId)} tamanho={28} />
          {nomeEpi(e.catalogoEpiId)}
        </div>
      ),
    },
    { chave: 'quantidade', rotulo: 'Qtd.', alinhar: 'direita', largura: '64px' },
    { chave: 'dataEntrega', rotulo: 'Entrega', render: (e) => e.dataEntrega?.slice(0, 10) },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (e) => {
        if (e.dataDevolucao) return <StatusChip tom="neutro">Devolvido {e.dataDevolucao.slice(0, 10)}</StatusChip>;
        const nivel = nivelVencimento(e.dataValidade);
        return nivel ? <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip> : '—';
      },
    },
    {
      chave: 'devolucao',
      rotulo: 'Devolução',
      render: (e) =>
        devolucaoId === e.id ? (
          <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
            <CampoData value={devolucaoData} onChange={(_, d) => setDevolucaoData(d.value)} style={{ width: 130 }} />
            <Input type="number" value={devolucaoQtd} onChange={(_, d) => setDevolucaoQtd(d.value)} style={{ width: 60 }} />
            <Button size="small" appearance="primary" onClick={() => confirmarDevolucao(e)} disabled={carregando}>
              Confirmar
            </Button>
          </div>
        ) : e.dataDevolucao ? null : (
          <Button size="small" appearance="subtle" onClick={() => iniciarDevolucao(e)}>
            Registrar devolução
          </Button>
        ),
    },
  ];

  return (
    <div>
      <PageHeader
        titulo="Entregas de EPI"
        subtitulo={`${entregas.filter((e) => !e.dataDevolucao).length} entregas ativas`}
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Nova entrega
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card densidade="compacta">
        <DataTable
          aria-label="Entregas de EPI"
          colunas={colunas}
          linhas={entregas}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma entrega registrada',
            descricao: 'Registre a primeira entrega para começar o controle de EPI.',
            acao: { rotulo: 'Nova entrega', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(e) => (
            <>
              <Button
                appearance="subtle"
                size="small"
                icon={<Signature24Regular />}
                onClick={() => navigate(`/epi/${e.id}/assinar`)}
                aria-label="Assinar ficha"
                title="Assinar ficha"
              />
              <Button
                appearance="subtle"
                size="small"
                icon={<ArrowDownload24Regular />}
                onClick={() => baixarFicha(e.trabalhadorId)}
                disabled={baixandoId === e.trabalhadorId}
                aria-label="Baixar ficha do funcionário"
                title="Baixar ficha de EPI do funcionário em PDF"
              />
            </>
          )}
        />
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={() => setPainelAberto(false)}
        titulo="Nova entrega de EPI"
        subtitulo="Nada é salvo até você registrar."
        largura="lg"
        rodape={
          <>
            <Button onClick={() => setPainelAberto(false)}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Registrar entrega
            </Button>
          </>
        }
      >
        <FormSection titulo="Quem recebe" numero={1} primeira>
          <FormGrid>
            <Campo>
              <Field label="Funcionário" required>
                <SeletorPesquisavel
                  placeholder={`Buscar entre ${trabalhadores.length} funcionários`}
                  opcoes={opcoesTrabalhadores}
                  valor={novaEntrega.trabalhadorId}
                  aoMudar={(id) =>
                    setNovaEntrega({
                      ...novaEntrega,
                      trabalhadorId: id,
                      catalogoEpiId: '',
                      numeroListaPresencaNr6: '',
                      dataTreinamentoNr6: '',
                    })
                  }
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>

        <FormSection titulo="O que é entregue" numero={2}>
          <FormGrid>
            <Campo>
              <Field label="EPI" required>
                <Select
                  value={novaEntrega.catalogoEpiId}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoEpiId: d.value })}
                  disabled={!novaEntrega.trabalhadorId || episPermitidos.length === 0}
                >
                  <option value="">Selecione</option>
                  {episPermitidos.map((e) => (
                    <option key={e.id} value={e.id}>
                      {e.nome} (estoque total: {e.saldoTotal})
                    </option>
                  ))}
                </Select>
              </Field>
              {novaEntrega.trabalhadorId && episPermitidos.length === 0 && (
                <FeedbackInline
                  tom="aviso"
                  acao={{
                    rotulo: 'Cadastrar na matriz',
                    aoClicar: () => {
                      setPainelAberto(false);
                      aoNavegarParaMatriz();
                    },
                  }}
                >
                  Esta função não tem EPIs cadastrados na matriz.
                </FeedbackInline>
              )}
            </Campo>
            <Campo span={4}>
              <Field label="Quantidade">
                <Input
                  type="number"
                  value={String(novaEntrega.quantidade)}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, quantidade: Number(d.value) })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Data de entrega">
                <CampoData
                  value={novaEntrega.dataEntrega}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataEntrega: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Validade">
                <CampoData
                  value={novaEntrega.dataValidade ?? ''}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataValidade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Motivo">
                <Select
                  value={novaEntrega.motivoTipo}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, motivoTipo: Number(d.value) })}
                >
                  {Object.entries(motivoEntregaEpiLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Observação do motivo">
                <Input
                  value={novaEntrega.motivo ?? ''}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, motivo: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>

        <FormSection titulo="Documentação NR-6" numero={3}>
          <FormGrid>
            <Campo span={6}>
              <Field label="Nº lista de presença" hint="Preenchido do treinamento de NR-06 cadastrado, se houver.">
                <Input
                  value={novaEntrega.numeroListaPresencaNr6 ?? ''}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, numeroListaPresencaNr6: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Data do treinamento">
                <CampoData
                  value={novaEntrega.dataTreinamentoNr6 ?? ''}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataTreinamentoNr6: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Visto do consórcio/responsável">
                <Input
                  value={novaEntrega.vistoConsorcioResponsavel ?? ''}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, vistoConsorcioResponsavel: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Observações">
                <Input
                  value={novaEntrega.observacoes ?? ''}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, observacoes: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
      </PainelLateral>

      {entregaParaAssinar && (
        <AssinaturaEntregaEpiDialog
          open={!!entregaParaAssinar}
          onClose={() => setEntregaParaAssinar(null)}
          entregaId={entregaParaAssinar.id}
          trabalhadorNome={nomeTrabalhador(entregaParaAssinar.trabalhadorId)}
          epiNome={nomeEpi(entregaParaAssinar.catalogoEpiId)}
          catalogoEpiId={entregaParaAssinar.catalogoEpiId}
          epiTemFoto={epiTemFoto(entregaParaAssinar.catalogoEpiId)}
          quantidade={entregaParaAssinar.quantidade}
          dataEntrega={entregaParaAssinar.dataEntrega}
          numeroListaPresencaNr6={entregaParaAssinar.numeroListaPresencaNr6}
          dataTreinamentoNr6={entregaParaAssinar.dataTreinamentoNr6}
        />
      )}

      {devolucaoParaAssinar && (
        <AssinaturaDevolucaoEpiDialog
          open={!!devolucaoParaAssinar}
          onClose={() => setDevolucaoParaAssinar(null)}
          entregaId={devolucaoParaAssinar.id}
          trabalhadorNome={nomeTrabalhador(devolucaoParaAssinar.trabalhadorId)}
          epiNome={nomeEpi(devolucaoParaAssinar.catalogoEpiId)}
          quantidadeDevolucao={devolucaoParaAssinar.quantidadeDevolucao ?? devolucaoParaAssinar.quantidade}
          dataDevolucao={devolucaoParaAssinar.dataDevolucao ?? ''}
        />
      )}
    </div>
  );
}
