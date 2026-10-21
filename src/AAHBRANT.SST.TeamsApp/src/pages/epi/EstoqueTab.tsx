import { useCallback, useEffect, useMemo, useState, type CSSProperties, type ReactElement } from 'react';
import {
  ArrowSwap20Regular,
  BoxArrowLeft20Regular,
  CheckmarkCircle20Regular,
  ChevronRight20Regular,
  DocumentPdf20Regular,
  ErrorCircle20Regular,
  Lightbulb20Regular,
  Search20Regular,
  Warning20Regular,
  DismissCircle20Regular,
  QuestionCircle20Regular,
} from '@fluentui/react-icons';
import {
  Button,
  Card,
  Carregando,
  DataTable,
  EstadoVazio,
  FeedbackInline,
  Field,
  Input,
  KpiCard,
  Legenda,
  PageHeader,
  PainelLateral,
  Radio,
  RadioGroup,
  Select,
  StatusChip,
  Text,
  Textarea,
  designTokens,
  tokensUi,
  type Coluna,
  type Tom,
} from '@ui';
import {
  api,
  tipoMovimentacaoEstoqueEpiLabel,
  type EstoqueEpiPainel,
  type EstoqueEpiPainelItem,
  type MovimentacaoEstoqueEpi,
  type Obra,
  type SituacaoEstoqueEpi,
  type SolicitacaoReposicaoEpiResumo,
} from '../../lib/api';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';

// Estoque de EPI por obra, redesenhado (especificação 21/10/2026, Módulos 2 e 3). Os números vêm
// prontos do servidor (painel = CalculadoraEstoqueEpi): disponível já desconta os kits separados para
// funcionários novos, a situação segue as faixas do mínimo e a sugestão de mínimo/compra/transferência
// é a mesma que alimenta o alerta e a solicitação de reposição.

const SITUACOES: { chave: SituacaoEstoqueEpi; rotulo: string; tom: Tom; icone: ReactElement }[] = [
  { chave: 'emDia', rotulo: 'Em dia', tom: 'ok', icone: <CheckmarkCircle20Regular /> },
  { chave: 'atencao', rotulo: 'Atenção', tom: 'atencao', icone: <Warning20Regular /> },
  { chave: 'abaixoMinimo', rotulo: 'Abaixo do mínimo', tom: 'alerta', icone: <ErrorCircle20Regular /> },
  { chave: 'zerado', rotulo: 'Zerado', tom: 'alerta', icone: <DismissCircle20Regular /> },
  { chave: 'semMinimo', rotulo: 'Sem mínimo', tom: 'neutro', icone: <QuestionCircle20Regular /> },
];
const INFO_SITUACAO = Object.fromEntries(SITUACOES.map((s) => [s.chave, s])) as Record<SituacaoEstoqueEpi, (typeof SITUACOES)[number]>;
const ORDEM_SITUACAO: Record<SituacaoEstoqueEpi, number> = { zerado: 0, abaixoMinimo: 1, atencao: 2, semMinimo: 3, emDia: 4 };

type Painel = 'item' | 'entrada' | 'transferir' | 'solicitacao' | null;

const estilos: Record<string, CSSProperties> = {
  pagina: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg },
  barraTopo: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.md, alignItems: 'flex-end' },
  kpis: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: tokensUi.espaco.md },
  lista: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm },
  linha: {
    display: 'grid',
    gridTemplateColumns: '48px minmax(0, 1.4fr) minmax(0, 2fr) auto 20px',
    alignItems: 'center',
    gap: tokensUi.espaco.md,
    width: '100%',
    padding: `${tokensUi.espaco.md} ${tokensUi.espaco.lg}`,
    background: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.md,
    boxShadow: tokensUi.sombraLinha,
    cursor: 'pointer',
    textAlign: 'left',
    color: 'inherit',
    font: 'inherit',
  },
  nome: { fontWeight: 600, fontSize: '14px', lineHeight: '18px' },
  secundario: { fontSize: '12px', color: designTokens.colorNeutralMedium },
  numeros: { display: 'flex', flexWrap: 'wrap', gap: `2px ${tokensUi.espaco.md}`, fontSize: '12px', color: designTokens.colorNeutralMedium, fontVariantNumeric: 'tabular-nums' },
  destaqueNumero: { color: designTokens.colorNeutralDark, fontWeight: 700 },
  grade4: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: tokensUi.espaco.sm },
  caixaNumero: { border: `1px solid ${tokensUi.bordaSuave}`, borderRadius: tokensUi.raio.sm, padding: tokensUi.espaco.sm, display: 'flex', flexDirection: 'column', gap: '2px' },
  numeroGrande: { fontSize: '22px', fontWeight: 700, fontVariantNumeric: 'tabular-nums', lineHeight: '26px' },
  secaoPainel: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm, paddingTop: tokensUi.espaco.lg },
  tituloSecao: { fontSize: '12px', fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase', color: designTokens.colorNeutralMedium },
  linhaAcoes: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.sm, alignItems: 'flex-end' },
  dica: { display: 'flex', gap: tokensUi.espaco.sm, alignItems: 'flex-start', padding: tokensUi.espaco.md, borderRadius: tokensUi.raio.sm, background: tokensUi.status.info.fundo, color: tokensUi.status.info.tinta, fontSize: '13px' },
};

function tomBarra(situacao: SituacaoEstoqueEpi): string {
  const tom = INFO_SITUACAO[situacao].tom;
  return tom === 'neutro' ? designTokens.colorNeutralMedium : tokensUi.status[tom].tinta;
}

// Barra de saldo com a marca do mínimo: parte cheia = disponível; parte clara = separado para kits.
function BarraSaldo({ item }: { item: EstoqueEpiPainelItem }) {
  const minimo = item.estoqueMinimo ?? 0;
  const escala = Math.max(item.saldo, minimo * 2, item.reservado, 1);
  const disponivel = Math.max(0, item.disponivel);
  const larguraDisponivel = (disponivel / escala) * 100;
  const larguraReservado = (Math.min(item.reservado, item.saldo) / escala) * 100;
  const cor = tomBarra(item.situacao);
  return (
    <div
      role="img"
      aria-label={`Disponível ${disponivel}, separado ${item.reservado}${minimo ? `, mínimo ${minimo}` : ''}`}
      style={{ position: 'relative', height: 8, borderRadius: tokensUi.raio.full, background: designTokens.colorNeutralLight, overflow: 'visible' }}
    >
      <div style={{ position: 'absolute', inset: 0, borderRadius: tokensUi.raio.full, overflow: 'hidden', display: 'flex' }}>
        <div style={{ width: `${larguraDisponivel}%`, background: cor, transition: `width ${tokensUi.duracao.normal}` }} />
        <div style={{ width: `${larguraReservado}%`, background: `repeating-linear-gradient(135deg, ${cor} 0 3px, transparent 3px 6px)`, opacity: 0.45 }} />
      </div>
      {minimo > 0 && (
        <div
          title={`Mínimo: ${minimo}`}
          style={{ position: 'absolute', top: -3, bottom: -3, left: `calc(${(minimo / escala) * 100}% - 1px)`, width: 2, background: designTokens.colorNeutralDark, borderRadius: 1 }}
        />
      )}
    </div>
  );
}

function LinhaItem({ item, aoAbrir }: { item: EstoqueEpiPainelItem; aoAbrir: () => void }) {
  const info = INFO_SITUACAO[item.situacao];
  return (
    <button type="button" style={estilos.linha} onClick={aoAbrir} className="sst-linha-estoque">
      <FotoCatalogoEpi catalogoEpiId={item.catalogoEpiId} temFoto={item.temFoto} tamanho={44} />
      <span style={{ minWidth: 0 }}>
        <span style={{ ...estilos.nome, display: 'block' }}>{item.nome}</span>
        <span style={estilos.secundario}>
          {item.certificadoAprovacaoNumero ?? 'Sem CA'}
          {item.fabricante ? ` · ${item.fabricante}` : ''}
        </span>
      </span>
      <span style={{ display: 'flex', flexDirection: 'column', gap: 6, minWidth: 0 }}>
        <BarraSaldo item={item} />
        <span style={estilos.numeros}>
          <span>Físico <b style={estilos.destaqueNumero}>{item.saldo}</b></span>
          <span>Separado <b style={estilos.destaqueNumero}>{item.reservado}</b></span>
          <span>Disponível <b style={estilos.destaqueNumero}>{Math.max(0, item.disponivel)}</b></span>
          <span>Mínimo <b style={estilos.destaqueNumero}>{item.estoqueMinimo ?? '—'}</b></span>
          {item.tamanhosEmFalta.length > 0 && <span>Falta tam. {item.tamanhosEmFalta.join(', ')}</span>}
        </span>
      </span>
      <StatusChip tom={info.tom} pulsar={item.situacao === 'zerado' ? 'leve' : undefined}>
        {info.rotulo}
      </StatusChip>
      <ChevronRight20Regular aria-hidden="true" />
    </button>
  );
}

export function EstoqueTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [painel, setPainel] = useState<EstoqueEpiPainel | null>(null);
  const [solicitacoes, setSolicitacoes] = useState<SolicitacaoReposicaoEpiResumo[]>([]);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [filtro, setFiltro] = useState<SituacaoEstoqueEpi | null>(null);
  const [busca, setBusca] = useState('');
  const [prazo, setPrazo] = useState('');

  const [painelAberto, setPainelAberto] = useState<Painel>(null);
  const [itemId, setItemId] = useState<string | null>(null);
  const [movimentacoes, setMovimentacoes] = useState<MovimentacaoEstoqueEpi[] | null>(null);
  const [minimoEditado, setMinimoEditado] = useState('');

  const [formEpiId, setFormEpiId] = useState('');
  const [formTipo, setFormTipo] = useState<'entrada' | 'ajuste'>('entrada');
  const [formQuantidade, setFormQuantidade] = useState('1');
  const [formObservacao, setFormObservacao] = useState('');
  const [formObraDestino, setFormObraDestino] = useState('');

  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  useEffect(() => {
    api.obras
      .listar()
      .then((lista) => {
        setObras(lista);
        if (lista.length > 0) setObraId(lista[0].id);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar as obras.'));
  }, []);

  const recarregar = useCallback(async () => {
    if (!obraId) return;
    const [p, s] = await Promise.all([api.estoquesEpi.painel(obraId), api.estoquesEpi.listarSolicitacoesReposicao(obraId)]);
    setPainel(p);
    setPrazo(String(p.prazoReposicaoEpiDias));
    setSolicitacoes(s);
  }, [obraId]);

  useEffect(() => {
    if (!obraId) return;
    let cancelado = false;
    recarregar().catch((e) => !cancelado && setErro(e instanceof Error ? e.message : 'Falha ao carregar o estoque da obra.'));
    return () => {
      cancelado = true;
    };
  }, [obraId, recarregar]);

  // Carregando = o painel na tela ainda é de outra obra (ou nenhum chegou).
  const carregando = !!obraId && painel?.obraId !== obraId;

  const contagem = useMemo(() => {
    const c: Record<SituacaoEstoqueEpi, number> = { emDia: 0, atencao: 0, abaixoMinimo: 0, zerado: 0, semMinimo: 0 };
    painel?.itens.forEach((i) => c[i.situacao]++);
    return c;
  }, [painel]);

  const itensVisiveis = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    return (painel?.itens ?? [])
      .filter((i) => !filtro || i.situacao === filtro)
      .filter((i) => !termo || i.nome.toLowerCase().includes(termo) || (i.certificadoAprovacaoNumero ?? '').toLowerCase().includes(termo))
      .sort((a, b) => ORDEM_SITUACAO[a.situacao] - ORDEM_SITUACAO[b.situacao] || a.nome.localeCompare(b.nome, 'pt-BR'));
  }, [painel, filtro, busca]);

  const pedemReposicao = (painel?.itens ?? []).filter((i) => (i.situacao === 'abaixoMinimo' || i.situacao === 'zerado') && i.quantidadeSugeridaCompra > 0);
  const semMinimoComSugestao = (painel?.itens ?? []).filter((i) => i.estoqueMinimo == null && i.minimoSugerido > 0).length;
  const item = painel?.itens.find((i) => i.catalogoEpiId === itemId) ?? null;
  const outrasObras = obras.filter((o) => o.id !== obraId);

  async function executar(acao: () => Promise<void>, mensagemSucesso: string) {
    setSalvando(true);
    setErro(null);
    setAviso(null);
    try {
      await acao();
      await recarregar();
      setAviso(mensagemSucesso);
      return true;
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível concluir agora.');
      return false;
    } finally {
      setSalvando(false);
    }
  }

  async function abrirItem(i: EstoqueEpiPainelItem) {
    setItemId(i.catalogoEpiId);
    setMinimoEditado(i.estoqueMinimo != null ? String(i.estoqueMinimo) : '');
    setMovimentacoes(null);
    setPainelAberto('item');
    try {
      setMovimentacoes(await api.estoquesEpi.listarMovimentacoes(obraId, i.catalogoEpiId));
    } catch {
      setMovimentacoes([]);
    }
  }

  function abrirFormulario(tipo: 'entrada' | 'transferir', epiId?: string) {
    setFormEpiId(epiId ?? '');
    setFormTipo('entrada');
    setFormQuantidade('1');
    setFormObservacao('');
    setFormObraDestino(outrasObras[0]?.id ?? '');
    setPainelAberto(tipo);
  }

  async function salvarPrazo() {
    const dias = Number(prazo);
    if (!painel || !dias || dias === painel.prazoReposicaoEpiDias) return;
    await executar(() => api.estoquesEpi.definirPrazoReposicao(obraId, dias), `Prazo de reposição: ${dias} dias.`);
  }

  async function salvarMinimo(valor: number | null) {
    if (!item) return;
    await executar(
      () => api.estoquesEpi.definirMinimo(obraId, item.catalogoEpiId, valor),
      valor == null ? `Mínimo de ${item.nome} removido.` : `Mínimo de ${item.nome}: ${valor}.`,
    );
    setMinimoEditado(valor != null ? String(valor) : '');
  }

  async function salvarMovimento() {
    const quantidade = Number(formQuantidade);
    if (!formEpiId || Number.isNaN(quantidade)) return;
    const ok =
      painelAberto === 'transferir'
        ? await executar(
            () => api.estoquesEpi.transferir({ catalogoEpiId: formEpiId, obraOrigemId: obraId, obraDestinoId: formObraDestino, quantidade, observacao: formObservacao || null }),
            'Transferência registrada.',
          )
        : formTipo === 'entrada'
          ? await executar(
              () => api.estoquesEpi.registrarEntrada({ obraId, catalogoEpiId: formEpiId, quantidade, observacao: formObservacao || null }),
              'Entrada registrada.',
            )
          : await executar(
              () => api.estoquesEpi.ajustar({ obraId, catalogoEpiId: formEpiId, novoSaldo: quantidade, observacao: formObservacao }),
              'Saldo ajustado.',
            );
    if (ok) setPainelAberto(null);
  }

  async function trazerDeOutraObra(origemId: string, quantidade: number) {
    if (!item) return;
    await executar(
      () => api.estoquesEpi.transferir({ catalogoEpiId: item.catalogoEpiId, obraOrigemId: origemId, obraDestinoId: obraId, quantidade, observacao: 'Sugestão do estoque mínimo' }),
      `${quantidade} unidade(s) transferida(s) para esta obra.`,
    );
  }

  async function gerarSolicitacao() {
    setSalvando(true);
    setErro(null);
    try {
      const { id } = await api.estoquesEpi.criarSolicitacaoReposicao(obraId, formObservacao || null);
      await recarregar();
      setPainelAberto(null);
      setAviso('Solicitação de reposição gerada.');
      verSolicitacao({ id, numeroDocumento: 'Solicitação de reposição' } as SolicitacaoReposicaoEpiResumo);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível gerar a solicitação agora.');
    } finally {
      setSalvando(false);
    }
  }

  function verSolicitacao(s: SolicitacaoReposicaoEpiResumo) {
    void visualizar({
      titulo: s.numeroDocumento,
      nomeArquivo: `solicitacao-reposicao-epi-${s.numeroDocumento}.pdf`,
      obter: () => api.estoquesEpi.baixarSolicitacaoReposicao(s.id),
    });
  }

  const colunasMovimentacoes: Coluna<MovimentacaoEstoqueEpi>[] = [
    { chave: 'data', rotulo: 'Data', render: (m) => new Date(m.createdAtUtc).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' }) },
    { chave: 'tipo', rotulo: 'Tipo', render: (m) => tipoMovimentacaoEstoqueEpiLabel[m.tipo] ?? '—' },
    { chave: 'quantidade', rotulo: 'Qtd.', alinhar: 'direita' },
    { chave: 'saldoResultante', rotulo: 'Saldo', alinhar: 'direita' },
    { chave: 'observacao', rotulo: 'Observação' },
  ];

  const colunasSolicitacoes: Coluna<SolicitacaoReposicaoEpiResumo>[] = [
    { chave: 'numeroDocumento', rotulo: 'Documento' },
    { chave: 'criadaEm', rotulo: 'Emitida em', render: (s) => new Date(s.criadaEm + (s.criadaEm.endsWith('Z') ? '' : 'Z')).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' }) },
    { chave: 'solicitanteNome', rotulo: 'Solicitante' },
    { chave: 'totalItens', rotulo: 'Itens', alinhar: 'direita' },
    {
      chave: 'assinada',
      rotulo: 'Assinatura',
      render: (s) => <StatusChip tom={s.assinada ? 'ok' : 'neutro'}>{s.assinada ? 'Assinada' : 'Pendente'}</StatusChip>,
    },
    {
      chave: 'pdf',
      rotulo: '',
      alinhar: 'direita',
      render: (s) => (
        <Button size="small" icon={<DocumentPdf20Regular />} onClick={() => verSolicitacao(s)}>
          Ver PDF
        </Button>
      ),
    },
  ];

  return (
    <div style={estilos.pagina}>
      <style>{`.sst-linha-estoque{transition:box-shadow ${tokensUi.duracao.rapido},border-color ${tokensUi.duracao.rapido}}
.sst-linha-estoque:hover{box-shadow:${tokensUi.sombraLinhaHover};border-color:${designTokens.colorPrimary}}
.sst-linha-estoque:focus-visible{outline:2px solid ${designTokens.colorPrimary};outline-offset:2px}
@media (max-width:720px){.sst-linha-estoque{grid-template-columns:44px minmax(0,1fr) auto!important}.sst-linha-estoque>:nth-child(3){grid-column:1/-1}.sst-linha-estoque>:nth-child(5){display:none}}`}</style>

      <PageHeader
        titulo="Estoque de EPI"
        subtitulo="Disponível = saldo físico menos os kits separados para funcionários novos. Clique num item para ver detalhes, mínimo e histórico."
        acoes={
          <>
            <Button icon={<BoxArrowLeft20Regular />} disabled={!painel} onClick={() => abrirFormulario('entrada')}>
              Entrada ou ajuste
            </Button>
            <Button icon={<ArrowSwap20Regular />} disabled={!painel || outrasObras.length === 0} onClick={() => abrirFormulario('transferir')}>
              Transferir
            </Button>
            <Button
              appearance="primary"
              icon={<DocumentPdf20Regular />}
              disabled={!painel || pedemReposicao.length === 0}
              onClick={() => {
                setFormObservacao('');
                setPainelAberto('solicitacao');
              }}
            >
              Solicitar reposição{pedemReposicao.length > 0 ? ` (${pedemReposicao.length})` : ''}
            </Button>
          </>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      {aviso && (
        <FeedbackInline tom="sucesso" aoFechar={() => setAviso(null)}>
          {aviso}
        </FeedbackInline>
      )}

      <div style={estilos.barraTopo}>
        <Field label="Obra" style={{ minWidth: 240, flex: '1 1 240px' }}>
          <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
            {obras.map((o) => (
              <option key={o.id} value={o.id}>
                {o.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Prazo de reposição (dias)" hint="Usado no mínimo sugerido" style={{ width: 200 }}>
          <Input
            type="number"
            min={1}
            max={180}
            value={prazo}
            onChange={(_, d) => setPrazo(d.value)}
            onBlur={salvarPrazo}
            onKeyDown={(e) => e.key === 'Enter' && salvarPrazo()}
          />
        </Field>
        <Field label="Buscar" style={{ minWidth: 220, flex: '1 1 220px' }}>
          <Input contentBefore={<Search20Regular />} placeholder="Nome do EPI ou CA" value={busca} onChange={(_, d) => setBusca(d.value)} />
        </Field>
      </div>

      {carregando ? (
        <Carregando variante="kpi" />
      ) : (
        <div style={estilos.kpis}>
          {SITUACOES.map((s, i) => (
            <KpiCard
              key={s.chave}
              indice={i}
              rotulo={filtro === s.chave ? `${s.rotulo} · filtrando` : s.rotulo}
              valor={contagem[s.chave]}
              tom={s.tom}
              icone={s.icone}
              ariaLabel={`${s.rotulo}: ${contagem[s.chave]} item(ns). ${filtro === s.chave ? 'Clique para limpar o filtro.' : 'Clique para filtrar.'}`}
              onClick={() => setFiltro((atual) => (atual === s.chave ? null : s.chave))}
            />
          ))}
        </div>
      )}

      {semMinimoComSugestao > 0 && (
        <div style={estilos.dica}>
          <Lightbulb20Regular aria-hidden="true" />
          <span style={{ flex: 1 }}>
            {semMinimoComSugestao} EPI(s) usados nesta obra ainda não têm mínimo. O sistema já calculou uma sugestão para cada um, pelo consumo e
            pelas contratações recentes.
          </span>
          <Button
            size="small"
            appearance="primary"
            disabled={salvando}
            onClick={() =>
              executar(async () => {
                const r = await api.estoquesEpi.aplicarSugestoes(obraId);
                setAviso(`${r.aplicados} mínimo(s) definido(s) pela sugestão.`);
              }, 'Sugestões aplicadas.')
            }
          >
            Aplicar sugestões
          </Button>
        </div>
      )}

      <Card
        titulo={filtro ? `${INFO_SITUACAO[filtro].rotulo} (${itensVisiveis.length})` : `Itens de EPI (${itensVisiveis.length})`}
        acoes={filtro ? <Button size="small" appearance="subtle" onClick={() => setFiltro(null)}>Limpar filtro</Button> : undefined}
        densidade="compacta"
      >
        {carregando ? (
          <Carregando variante="lista" linhas={6} />
        ) : itensVisiveis.length === 0 ? (
          <EstadoVazio variante={filtro || busca ? 'sem-resultado' : 'vazio'} titulo={filtro || busca ? 'Nenhum EPI com esse filtro.' : 'Nenhum EPI no catálogo.'} />
        ) : (
          <div style={estilos.lista}>
            {itensVisiveis.map((i) => (
              <LinhaItem key={i.catalogoEpiId} item={i} aoAbrir={() => abrirItem(i)} />
            ))}
          </div>
        )}
        <div style={{ paddingTop: tokensUi.espaco.md }}>
          <Legenda>Barra cheia = disponível · listrada = separado para kits · traço vertical = mínimo.</Legenda>
        </div>
      </Card>

      <Card titulo="Solicitações de reposição" subtitulo="Pedido interno para Suprimentos, com os itens abaixo do mínimo no momento da emissão." densidade="compacta">
        <DataTable
          aria-label="Solicitações de reposição de EPI"
          densidade="compacta"
          colunas={colunasSolicitacoes}
          linhas={solicitacoes}
          chaveLinha={(s) => s.id}
          vazio={{ titulo: 'Nenhuma solicitação emitida para esta obra.' }}
        />
      </Card>

      {/* Detalhe do item */}
      <PainelLateral
        aberto={painelAberto === 'item' && item != null}
        aoFechar={() => setPainelAberto(null)}
        largura="lg"
        titulo={item?.nome ?? ''}
        subtitulo={item ? `${item.certificadoAprovacaoNumero ?? 'Sem CA'}${item.certificadoAprovacaoValidade ? ` · CA válido até ${new Date(item.certificadoAprovacaoValidade).toLocaleDateString('pt-BR')}` : ''}` : undefined}
        rodape={
          <>
            <Button onClick={() => item && abrirFormulario('transferir', item.catalogoEpiId)} disabled={outrasObras.length === 0}>
              Transferir
            </Button>
            <Button appearance="primary" onClick={() => item && abrirFormulario('entrada', item.catalogoEpiId)}>
              Entrada ou ajuste
            </Button>
          </>
        }
      >
        {item && (
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: tokensUi.espaco.md, flexWrap: 'wrap' }}>
              <FotoCatalogoEpi catalogoEpiId={item.catalogoEpiId} temFoto={item.temFoto} tamanho={64} />
              <StatusChip tom={INFO_SITUACAO[item.situacao].tom}>{INFO_SITUACAO[item.situacao].rotulo}</StatusChip>
              {item.quantidadeSugeridaCompra > 0 && (
                <Text>
                  Repor <b>{item.quantidadeSugeridaCompra}</b> para voltar a {item.estoqueMinimo ? `2× o mínimo` : 'zero em falta'}.
                </Text>
              )}
            </div>

            <div style={{ ...estilos.grade4, paddingTop: tokensUi.espaco.lg }}>
              {[
                ['Físico', item.saldo],
                ['Separado para kits', item.reservado],
                ['Disponível', Math.max(0, item.disponivel)],
                ['Mínimo', item.estoqueMinimo ?? '—'],
              ].map(([rotulo, valor]) => (
                <div key={String(rotulo)} style={estilos.caixaNumero}>
                  <span style={estilos.secundario}>{rotulo}</span>
                  <span style={estilos.numeroGrande}>{valor}</span>
                </div>
              ))}
            </div>
            <div style={{ paddingTop: tokensUi.espaco.md }}>
              <BarraSaldo item={item} />
            </div>
            {item.reservasEmFalta > 0 && (
              <div style={{ paddingTop: tokensUi.espaco.md }}>
                <FeedbackInline tom="aviso">
                  {item.reservasEmFalta} kit(s) de funcionário novo aguardando este EPI
                  {item.tamanhosEmFalta.length > 0 ? ` · tamanhos ${item.tamanhosEmFalta.join(', ')}` : ''}.
                </FeedbackInline>
              </div>
            )}

            <div style={estilos.secaoPainel}>
              <span style={estilos.tituloSecao}>Estoque mínimo</span>
              <div style={estilos.linhaAcoes}>
                <Field label="Mínimo desta obra" style={{ width: 160 }}>
                  <Input type="number" min={0} style={{ minWidth: 0, width: '100%' }} value={minimoEditado} onChange={(_, d) => setMinimoEditado(d.value)} />
                </Field>
                <Button appearance="primary" disabled={salvando || minimoEditado === '' || Number(minimoEditado) < 0} onClick={() => salvarMinimo(Number(minimoEditado))}>
                  Salvar mínimo
                </Button>
                {item.minimoSugerido > 0 && item.minimoSugerido !== item.estoqueMinimo && (
                  <Button disabled={salvando} onClick={() => salvarMinimo(item.minimoSugerido)}>
                    Usar sugestão ({item.minimoSugerido})
                  </Button>
                )}
                {item.estoqueMinimo != null && (
                  <Button appearance="subtle" disabled={salvando} onClick={() => salvarMinimo(null)}>
                    Remover mínimo
                  </Button>
                )}
              </div>
              <Legenda>
                Sugestão {item.minimoSugerido}: {item.consumoUltimos90Dias} saída(s) nos últimos 90 dias, prazo de reposição de {painel?.prazoReposicaoEpiDias} dias
                e reserva de segurança pelas contratações recentes.
                {item.estoqueMinimoDefinidoEm &&
                  ` Definido ${item.estoqueMinimoDefinidoPor ? `por ${item.estoqueMinimoDefinidoPor} ` : ''}em ${new Date(item.estoqueMinimoDefinidoEm + (item.estoqueMinimoDefinidoEm.endsWith('Z') ? '' : 'Z')).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}.`}
              </Legenda>
            </div>

            {item.transferencias.length > 0 && (
              <div style={estilos.secaoPainel}>
                <span style={estilos.tituloSecao}>Outras obras com sobra</span>
                {item.transferencias.map((t) => (
                  <div key={t.obraId} style={{ ...estilos.linhaAcoes, alignItems: 'center', justifyContent: 'space-between' }}>
                    <Text>
                      {t.obraNome}: pode ceder <b>{t.quantidade}</b>
                    </Text>
                    <Button size="small" icon={<ArrowSwap20Regular />} disabled={salvando} onClick={() => trazerDeOutraObra(t.obraId, t.quantidade)}>
                      Trazer {t.quantidade}
                    </Button>
                  </div>
                ))}
              </div>
            )}

            <div style={estilos.secaoPainel}>
              <span style={estilos.tituloSecao}>Movimentações</span>
              {movimentacoes == null ? (
                <Carregando variante="lista" linhas={3} />
              ) : (
                <DataTable
                  aria-label={`Movimentações de ${item.nome}`}
                  densidade="compacta"
                  colunas={colunasMovimentacoes}
                  linhas={movimentacoes}
                  chaveLinha={(m) => m.id}
                  vazio={{ titulo: 'Nenhuma movimentação registrada.' }}
                />
              )}
            </div>
          </div>
        )}
      </PainelLateral>

      {/* Entrada / ajuste e transferência */}
      <PainelLateral
        aberto={painelAberto === 'entrada' || painelAberto === 'transferir'}
        aoFechar={() => setPainelAberto(null)}
        titulo={painelAberto === 'transferir' ? 'Transferir para outra obra' : 'Entrada ou ajuste de saldo'}
        subtitulo={painel?.obraNome}
        rodape={
          <>
            <Button onClick={() => setPainelAberto(null)}>Cancelar</Button>
            <Button
              appearance="primary"
              disabled={
                salvando ||
                !formEpiId ||
                formQuantidade === '' ||
                (painelAberto === 'transferir' && (!formObraDestino || Number(formQuantidade) <= 0)) ||
                (painelAberto === 'entrada' && formTipo === 'entrada' && Number(formQuantidade) <= 0) ||
                (painelAberto === 'entrada' && formTipo === 'ajuste' && (!formObservacao.trim() || Number(formQuantidade) < 0))
              }
              onClick={salvarMovimento}
            >
              {painelAberto === 'transferir' ? 'Transferir' : formTipo === 'entrada' ? 'Registrar entrada' : 'Ajustar saldo'}
            </Button>
          </>
        }
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md }}>
          <Field label="EPI" required>
            <Select value={formEpiId} onChange={(_, d) => setFormEpiId(d.value)}>
              <option value="">Selecione</option>
              {(painel?.itens ?? []).map((i) => (
                <option key={i.catalogoEpiId} value={i.catalogoEpiId}>
                  {i.nome} — disponível {Math.max(0, i.disponivel)}
                </option>
              ))}
            </Select>
          </Field>
          {painelAberto === 'entrada' && (
            <RadioGroup value={formTipo} onChange={(_, d) => setFormTipo(d.value as 'entrada' | 'ajuste')} layout="horizontal">
              <Radio value="entrada" label="Entrada (chegou material)" />
              <Radio value="ajuste" label="Ajuste (contagem de inventário)" />
            </RadioGroup>
          )}
          {painelAberto === 'transferir' && (
            <Field label="Obra de destino" required>
              <Select value={formObraDestino} onChange={(_, d) => setFormObraDestino(d.value)}>
                {outrasObras.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.nome}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          <Field label={painelAberto === 'entrada' && formTipo === 'ajuste' ? 'Novo saldo físico' : 'Quantidade'} required>
            <Input type="number" min={0} value={formQuantidade} onChange={(_, d) => setFormQuantidade(d.value)} />
          </Field>
          <Field
            label={painelAberto === 'entrada' && formTipo === 'ajuste' ? 'Motivo do ajuste' : 'Observação'}
            required={painelAberto === 'entrada' && formTipo === 'ajuste'}
          >
            <Textarea value={formObservacao} onChange={(_, d) => setFormObservacao(d.value)} />
          </Field>
          {painelAberto === 'transferir' && (
            <Legenda>Só sai o disponível: unidades separadas para kits de funcionários novos ficam na obra.</Legenda>
          )}
        </div>
      </PainelLateral>

      {/* Solicitação de reposição */}
      <PainelLateral
        aberto={painelAberto === 'solicitacao'}
        aoFechar={() => setPainelAberto(null)}
        largura="lg"
        titulo="Solicitação de reposição de EPI"
        subtitulo={`${painel?.obraNome ?? ''} · prazo de reposição de ${painel?.prazoReposicaoEpiDias ?? '—'} dias`}
        rodape={
          <>
            <Button onClick={() => setPainelAberto(null)}>Cancelar</Button>
            <Button appearance="primary" icon={<DocumentPdf20Regular />} disabled={salvando || pedemReposicao.length === 0} onClick={gerarSolicitacao}>
              {salvando ? 'Gerando…' : 'Gerar e assinar'}
            </Button>
          </>
        }
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md }}>
          <Text>
            Entram os {pedemReposicao.length} EPI(s) abaixo do mínimo ou zerados. A quantidade recompõe o estoque até o dobro do mínimo. O documento
            é numerado, assinado por você com um clique e fica disponível em PDF para Suprimentos.
          </Text>
          <DataTable
            aria-label="Itens da solicitação"
            densidade="compacta"
            colunas={[
              { chave: 'nome', rotulo: 'EPI' },
              { chave: 'disponivel', rotulo: 'Disponível', alinhar: 'direita', render: (i) => Math.max(0, i.disponivel) },
              { chave: 'estoqueMinimo', rotulo: 'Mínimo', alinhar: 'direita', render: (i) => i.estoqueMinimo ?? '—' },
              { chave: 'quantidadeSugeridaCompra', rotulo: 'Repor', alinhar: 'direita', render: (i) => <b>{i.quantidadeSugeridaCompra}</b> },
            ]}
            linhas={pedemReposicao}
            chaveLinha={(i) => i.catalogoEpiId}
          />
          <Field label="Observação para Suprimentos (opcional)">
            <Textarea value={formObservacao} onChange={(_, d) => setFormObservacao(d.value)} />
          </Field>
        </div>
      </PainelLateral>

      {dialogoVisualizador}
    </div>
  );
}
