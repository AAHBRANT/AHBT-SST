import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  Card,
  DataTable,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Field,
  FormGrid,
  Input,
  KpiCard,
  PageHeader,
  Select,
  StatusChip,
  Textarea,
  tokensUi,
  type Coluna,
  type Tom,
} from '@ui';
import {
  api,
  prioridadeIdeiaLabel,
  StatusIdeia,
  statusIdeiaLabel,
  type DashboardIdeias,
  type FiltroIdeias,
  type IdeiaResumo,
} from '../../lib/api';
import { formatarData, tomStatusIdeia } from './ideiasComuns';

// Cartões do painel (§14), na ordem do fluxo de status.
const CARTOES: { status: number | null; rotulo: string; tom: Tom }[] = [
  { status: null, rotulo: 'Total de ideias', tom: 'neutro' },
  { status: StatusIdeia.NovaIdeia, rotulo: 'Novas ideias', tom: 'neutro' },
  { status: StatusIdeia.EmAnalise, rotulo: 'Em análise', tom: 'info' },
  { status: StatusIdeia.AguardandoDecisao, rotulo: 'Aguardando decisão', tom: 'atencao' },
  { status: StatusIdeia.Aprovada, rotulo: 'Aprovadas', tom: 'ok' },
  { status: StatusIdeia.Priorizada, rotulo: 'Priorizadas', tom: 'ok' },
  { status: StatusIdeia.EmDesenvolvimento, rotulo: 'Em desenvolvimento', tom: 'info' },
  { status: StatusIdeia.EmTesteValidacao, rotulo: 'Em teste', tom: 'atencao' },
  { status: StatusIdeia.Implantada, rotulo: 'Implantadas', tom: 'ok' },
  { status: StatusIdeia.Adiada, rotulo: 'Adiadas', tom: 'neutro' },
  { status: StatusIdeia.Descartada, rotulo: 'Descartadas', tom: 'alerta' },
];

export function IdeiasPage() {
  const navigate = useNavigate();
  const [painel, setPainel] = useState<DashboardIdeias | null>(null);
  const [ideias, setIdeias] = useState<IdeiaResumo[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [busca, setBusca] = useState('');
  const [status, setStatus] = useState('');
  const [modulo, setModulo] = useState('');
  const [categoria, setCategoria] = useState('');
  const [prioridade, setPrioridade] = useState('');
  const [responsavel, setResponsavel] = useState('');
  const [criador, setCriador] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');

  const [novaAberta, setNovaAberta] = useState(false);
  const [mensagem, setMensagem] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erroNova, setErroNova] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const filtro = useMemo<FiltroIdeias>(
    () => ({
      busca: busca.trim() || undefined,
      status: status === '' ? undefined : Number(status),
      modulo: modulo || undefined,
      categoria: categoria.trim() || undefined,
      prioridade: prioridade === '' ? undefined : Number(prioridade),
      responsavel: responsavel.trim() || undefined,
      criador: criador.trim() || undefined,
      de: de || undefined,
      ate: ate || undefined,
    }),
    [busca, status, modulo, categoria, prioridade, responsavel, criador, de, ate],
  );

  const carregarPainel = useCallback(async () => {
    try {
      setPainel(await api.ideias.dashboard());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar o painel.');
    }
  }, []);

  useEffect(() => {
    void carregarPainel();
  }, [carregarPainel]);

  // Pesquisa com pequeno atraso para não consultar a cada tecla.
  useEffect(() => {
    let cancelado = false;
    setCarregando(true);
    const timer = setTimeout(() => {
      api.ideias
        .listar(filtro)
        .then((dados) => {
          if (!cancelado) {
            setIdeias(dados);
            setErro(null);
          }
        })
        .catch((e) => {
          if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar as ideias.');
        })
        .finally(() => {
          if (!cancelado) setCarregando(false);
        });
    }, 250);
    return () => {
      cancelado = true;
      clearTimeout(timer);
    };
  }, [filtro]);

  async function registrar() {
    if (!mensagem.trim()) {
      setErroNova('Escreva a ideia.');
      return;
    }
    setEnviando(true);
    setErroNova(null);
    try {
      const r = await api.ideias.registrar(mensagem.trim());
      setNovaAberta(false);
      setMensagem('');
      setAviso(
        `Ideia ${r.codigo} registrada — ${r.titulo}.` +
          (r.ideiaSemelhanteCodigo
            ? ` Foi encontrada uma ideia semelhante (${r.ideiaSemelhanteCodigo}); abra a nova ideia para vincular ou manter separada.`
            : ''),
      );
      void carregarPainel();
      setBusca('');
      navigate(`/ideias/${r.id}`);
    } catch (e) {
      setErroNova(e instanceof Error ? e.message : 'Não foi possível registrar a ideia.');
    } finally {
      setEnviando(false);
    }
  }

  const colunas: Coluna<IdeiaResumo>[] = [
    { chave: 'codigo', rotulo: 'ID', largura: '110px', render: (i) => <strong>{i.codigo}</strong> },
    {
      chave: 'titulo',
      rotulo: 'Ideia',
      render: (i) => (
        <div>
          <div>{i.titulo}</div>
          {i.ideiaPrincipalCodigo && (
            <small style={{ opacity: 0.7 }}>Vinculada a {i.ideiaPrincipalCodigo}</small>
          )}
        </div>
      ),
    },
    { chave: 'modulo', rotulo: 'Módulo', render: (i) => i.modulo ?? '—' },
    { chave: 'categoria', rotulo: 'Categoria', render: (i) => i.categoria ?? '—' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (i) => <StatusChip tom={tomStatusIdeia[i.status] ?? 'neutro'}>{statusIdeiaLabel[i.status]}</StatusChip>,
    },
    {
      chave: 'prioridade',
      rotulo: 'Prioridade',
      render: (i) => (i.prioridade ? prioridadeIdeiaLabel[i.prioridade] : '—'),
    },
    {
      chave: 'pontuacao',
      rotulo: 'Pontuação',
      alinhar: 'direita',
      render: (i) => i.pontuacao ?? '—',
    },
    { chave: 'registradoPorNome', rotulo: 'Registrada por', render: (i) => i.registradoPorNome ?? '—' },
    { chave: 'createdAtUtc', rotulo: 'Data', render: (i) => formatarData(i.createdAtUtc) },
  ];

  return (
    <div>
      <PageHeader
        titulo="Banco de Ideias"
        subtitulo="Nenhuma ideia se perde: registre aqui ou envie pelo Telegram — a equipe analisa, decide e acompanha até a implantação."
        acoes={
          <Button appearance="primary" onClick={() => setNovaAberta(true)}>
            Registrar ideia
          </Button>
        }
      />

      {aviso && (
        <FeedbackInline tom="sucesso" aoFechar={() => setAviso(null)}>
          {aviso}
        </FeedbackInline>
      )}
      {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(190px, 1fr))',
          gap: tokensUi.espaco.md,
          marginBottom: tokensUi.espaco.lg,
        }}
      >
        {CARTOES.map((c, indice) => (
          <KpiCard
            key={c.rotulo}
            rotulo={c.rotulo}
            tom={c.tom}
            indice={indice}
            carregando={!painel}
            valor={painel ? (c.status === null ? painel.total : (painel.porStatus[String(c.status)] ?? 0)) : '—'}
            onClick={() => setStatus(c.status === null ? '' : String(c.status))}
            ariaLabel={`Filtrar por ${c.rotulo}`}
          />
        ))}
      </div>

      <Card titulo="Pesquisar e filtrar" densidade="compacta">
        <FormGrid>
          <Campo span={4}>
            <Field label="Palavra-chave" hint="Busca em ideias, requisitos e demandas">
              <Input value={busca} onChange={(_, d) => setBusca(d.value)} placeholder="ex.: não conformidade" />
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Status">
              <Select value={status} onChange={(_, d) => setStatus(d.value)}>
                <option value="">Todos</option>
                {Object.entries(statusIdeiaLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Módulo">
              <Select value={modulo} onChange={(_, d) => setModulo(d.value)}>
                <option value="">Todos</option>
                {(painel?.porModulo ?? [])
                  .filter((m) => m.nome !== 'Não classificado')
                  .map((m) => (
                    <option key={m.nome} value={m.nome}>
                      {m.nome} ({m.quantidade})
                    </option>
                  ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Prioridade">
              <Select value={prioridade} onChange={(_, d) => setPrioridade(d.value)}>
                <option value="">Todas</option>
                {Object.entries(prioridadeIdeiaLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Categoria">
              <Input value={categoria} onChange={(_, d) => setCategoria(d.value)} />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Responsável">
              <Input value={responsavel} onChange={(_, d) => setResponsavel(d.value)} />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Criador">
              <Input value={criador} onChange={(_, d) => setCriador(d.value)} />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="De">
              <Input type="date" value={de} onChange={(_, d) => setDe(d.value)} />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Até">
              <Input type="date" value={ate} onChange={(_, d) => setAte(d.value)} />
            </Field>
          </Campo>
        </FormGrid>
      </Card>

      <div style={{ marginTop: tokensUi.espaco.lg }}>
        <DataTable
          aria-label="Ideias registradas"
          colunas={colunas}
          linhas={ideias}
          chaveLinha={(i) => i.id}
          carregando={carregando}
          aoClicarLinha={(i) => navigate(`/ideias/${i.id}`)}
          vazio={{
            titulo: 'Nenhuma ideia encontrada',
            descricao: 'Ajuste os filtros ou registre uma nova ideia — pela tela ou enviando uma mensagem no Telegram.',
          }}
        />
      </div>

      <Dialog open={novaAberta} onOpenChange={(_, d) => !enviando && setNovaAberta(d.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Registrar ideia</DialogTitle>
            <DialogContent>
              <p style={{ marginTop: 0 }}>
                Escreva do jeito que vier à cabeça — a ideia não precisa estar madura. O sistema organiza e guarda o texto
                original.
              </p>
              {erroNova && <FeedbackInline tom="erro">{erroNova}</FeedbackInline>}
              <Field label="Sua ideia" required>
                <Textarea
                  value={mensagem}
                  onChange={(_, d) => setMensagem(d.value)}
                  rows={6}
                  placeholder="Ex.: Seria interessante o sistema de SST analisar automaticamente uma não conformidade e sugerir a causa raiz e as ações corretivas."
                />
              </Field>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" disabled={enviando} onClick={() => setNovaAberta(false)}>
                Cancelar
              </Button>
              <Button appearance="primary" disabled={enviando} onClick={() => void registrar()}>
                {enviando ? 'Registrando…' : 'Registrar'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
