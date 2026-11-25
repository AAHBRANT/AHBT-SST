import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Field,
  Input,
  Select,
  CampoData,
  Card,
  CelulaDupla,
  PageHeader,
  DataTable,
  StatusChip,
  FeedbackInline,
  FiltroRapido,
  PainelCriacaoInline,
  FormSection,
  FormGrid,
  FormRodape,
  Campo,
  tokensUi,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, CalendarLtr20Regular, DocumentPdf24Regular, Open24Regular, Play24Regular, Search20Regular } from '@fluentui/react-icons';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { FotoTrabalhador } from '../../components/FotoTrabalhador';
import {
  api,
  statusDdsSemanalLabel,
  StatusDdsSemanal,
  tipoDdsSemanalLabel,
  tipoDdsSemanalIncluiTerceirizados,
  TipoDdsSemanal,
  type DdsSemanal,
  type NovaDdsSemanal,
  type Obra,
} from '../../lib/api';
import { segundaFeiraAtualIso } from '../../lib/datas';
import { usePodeNaObra } from '../../lib/UsuarioLogadoContext';
import { FaixaSupervisao } from '../../components/FaixaSupervisao';

function semanalVazia(): NovaDdsSemanal {
  return { obraId: '', tipo: TipoDdsSemanal.Proprios, dataInicioSemana: segundaFeiraAtualIso() };
}

const tomStatusSemanal: Record<number, Tom> = {
  [StatusDdsSemanal.EmAndamento]: 'info',
  [StatusDdsSemanal.Concluida]: 'ok',
};

// Visual e utilidade aprovados em 25/11/2026 (docs/mockups/dds-apr-pt-visual-2026-11-25.html).
type FiltroSemana = 'todas' | 'andamento' | 'passou' | 'concluidas';
const LETRAS_DIAS = ['S', 'T', 'Q', 'Q', 'S'];
const NOMES_DIAS = ['segunda', 'terça', 'quarta', 'quinta', 'sexta'];
const MS_DIA = 24 * 60 * 60 * 1000;

function diaLocal(iso: string): Date {
  return new Date(iso.slice(0, 10) + 'T00:00:00');
}

// Semana em andamento cuja sexta já passou: falta concluir.
function semanaPassouSemConcluir(s: DdsSemanal): boolean {
  if (s.status !== StatusDdsSemanal.EmAndamento) return false;
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  return diaLocal(s.dataFimSemana).getTime() < hoje.getTime();
}

function passaNoFiltro(s: DdsSemanal, f: FiltroSemana): boolean {
  if (f === 'andamento') return s.status === StatusDdsSemanal.EmAndamento;
  if (f === 'passou') return semanaPassouSemConcluir(s);
  if (f === 'concluidas') return s.status === StatusDdsSemanal.Concluida;
  return true;
}

// "05 a 09/10/2026" quando o mês é o mesmo; "28/09 a 02/10/2026" quando muda.
function rotuloSemana(s: DdsSemanal): string {
  const ini = diaLocal(s.dataInicioSemana);
  const fim = diaLocal(s.dataFimSemana);
  const dd = (d: Date) => String(d.getDate()).padStart(2, '0');
  const mm = (d: Date) => String(d.getMonth() + 1).padStart(2, '0');
  const inicio = ini.getMonth() === fim.getMonth() ? dd(ini) : `${dd(ini)}/${mm(ini)}`;
  return `${inicio} a ${dd(fim)}/${mm(fim)}/${fim.getFullYear()}`;
}

function rotuloRelativo(s: DdsSemanal): string {
  const segundaAtual = diaLocal(segundaFeiraAtualIso()).getTime();
  const semanas = Math.round((segundaAtual - diaLocal(s.dataInicioSemana).getTime()) / (7 * MS_DIA));
  if (semanas === 0) return 'esta semana';
  if (semanas === 1) return 'semana passada';
  if (semanas > 1) return `há ${semanas} semanas`;
  return 'semana futura';
}

// Cinco quadradinhos de segunda a sexta: verde = concluído; âmbar = registrado, ainda não concluído.
function DiasDaSemana({ semana }: { semana: DdsSemanal }) {
  const inicio = diaLocal(semana.dataInicioSemana).getTime();
  const porDia = new Map<number, boolean>();
  for (const d of semana.diasResumo ?? []) {
    const indice = Math.round((diaLocal(d.data).getTime() - inicio) / MS_DIA);
    if (indice >= 0 && indice < 5) porDia.set(indice, (porDia.get(indice) ?? false) || d.concluido);
  }
  const rotulo = `${semana.totalDiasConcluidos} de 5 dias concluídos, ${semana.totalDiasRegistrados} registrados`;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <div role="img" aria-label={rotulo} style={{ display: 'flex', gap: 4 }}>
        {LETRAS_DIAS.map((letra, i) => {
          const situacao = porDia.has(i) ? (porDia.get(i) ? 'ok' : 'atencao') : null;
          return (
            <span
              key={i}
              title={`${NOMES_DIAS[i]}: ${situacao === 'ok' ? 'concluído' : situacao === 'atencao' ? 'registrado, falta concluir' : 'sem registro'}`}
              style={{
                width: 18, height: 18, borderRadius: 6, display: 'grid', placeItems: 'center', fontSize: 8.5, fontWeight: 700,
                color: situacao === 'ok' ? '#fff' : situacao === 'atencao' ? tokensUi.status.atencao.tinta : tokensUi.status.neutro.tinta,
                backgroundColor: situacao === 'ok' ? tokensUi.status.ok.tinta : situacao === 'atencao' ? tokensUi.status.atencao.fundo : tokensUi.status.neutro.fundo,
                boxShadow: situacao === 'ok' ? 'none' : `inset 0 0 0 1px ${tokensUi.bordaSuave}`,
              }}
            >
              {letra}
            </span>
          );
        })}
      </div>
      <span style={{ fontSize: 11.5, color: tokensUi.status.neutro.tinta }}>
        {semana.totalDiasConcluidos === semana.totalDiasRegistrados
          ? `${semana.totalDiasConcluidos} de 5 dias`
          : `${semana.totalDiasRegistrados} registrados · ${semana.totalDiasConcluidos} concluídos`}
      </span>
    </div>
  );
}

// Reformulação 31/08 — o DDS passou a ser organizado por semana (contêiner), seguindo o modelo em
// papel "Registro Semanal de DDS - Empregados Próprios/Terceirizados". Os registros diários (feitos
// e assinados todo dia) ficam dentro de cada semana — ver DdsSemanalDetalhePage.
// Onda 2 (Task 14): conversões 1 (Table → DataTable), 5 (Badge color → StatusChip), 3
// (Text size=500 → PageHeader.titulo), 4 (erro → FeedbackInline). Migrado para o padrão
// PainelCriacaoInline (spec 2026-09-11): o PainelLateral (drawer) saiu — agora o formulário de
// criação cresce acima da lista, sem cobrir a tela com uma gaveta.
export function DdsSemanalPage() {
  const navigate = useNavigate();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [registros, setRegistros] = useState<DdsSemanal[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [nova, setNova] = useState<NovaDdsSemanal>(semanalVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [listaCarregada, setListaCarregada] = useState(false);
  const [obrasIndisponiveis, setObrasIndisponiveis] = useState(false);
  const [painelAberto, setPainelAberto] = useState(false);
  const [obraFiltro, setObraFiltro] = useState('');
  const pode = usePodeNaObra();

  async function carregar() {
    try {
      setErro(null);
      setCarregandoLista(true);
      const [lista, listaObras] = await Promise.allSettled([api.ddsSemanal.listar(), api.obras.listar()]);
      if (lista.status === 'rejected') throw lista.reason;
      setRegistros(lista.value);
      setListaCarregada(true);
      setObrasIndisponiveis(listaObras.status === 'rejected');
      if (listaObras.status === 'fulfilled') setObras(listaObras.value);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar as semanas de DDS.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function fecharPainel() {
    setPainelAberto(false);
    setNova(semanalVazia());
    setErroPainel(null);
  }

  async function criar() {
    if (!nova.obraId || !nova.dataInicioSemana || (tipoDdsSemanalIncluiTerceirizados(nova.tipo) && !nova.empresaTerceirizada)) {
      setErroPainel('Preencha obra, data de início da semana e, se terceirizado, a empresa.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      const resultado = await api.ddsSemanal.criar(nova);
      setNova(semanalVazia());
      await carregar();
      navigate(`/prevencao/dds/semana/${resultado.id}`);
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar a semana de DDS.');
    } finally {
      setCarregando(false);
    }
  }

  // Responsável x Supervisão (06/11/2026): "Minhas obras" = onde pode abrir semana; as demais
  // obras visíveis são só acompanhadas (consulta).
  const ehMinha = (obraId: string) => pode('dds:criar', obraId) || pode('dds:conduzir', obraId);
  const minhasObras = obras.filter((o) => ehMinha(o.id));
  const obrasSupervisao = obras.filter((o) => !ehMinha(o.id));
  const obrasParaCriar = obras.filter((o) => pode('dds:criar', o.id));
  const podeCriar = obrasParaCriar.length > 0;
  const obraFiltroSelecionada = obras.find((o) => o.id === obraFiltro);
  const daObra = obraFiltro ? registros.filter((r) => r.obraId === obraFiltro) : registros;
  const [filtro, setFiltro] = useState<FiltroSemana>('todas');
  const [busca, setBusca] = useState('');
  const contagem = useMemo(() => {
    const c: Record<FiltroSemana, number> = { todas: daObra.length, andamento: 0, passou: 0, concluidas: 0 };
    for (const s of daObra) for (const f of ['andamento', 'passou', 'concluidas'] as const) if (passaNoFiltro(s, f)) c[f] += 1;
    return c;
  }, [daObra]);
  const termo = busca.trim().toLocaleLowerCase('pt-BR');
  const linhasVisiveis = daObra.filter(
    (s) =>
      passaNoFiltro(s, filtro) &&
      (!termo ||
        [s.obraNome, s.numeroDocumento, s.responsavelUsuarioNome, s.empresaTerceirizada, s.localFrenteServico]
          .some((v) => v?.toLocaleLowerCase('pt-BR').includes(termo))),
  );

  // Aviso da semana atual: obras onde o usuário pode abrir semana e que ainda não têm a desta semana.
  const segundaAtual = segundaFeiraAtualIso();
  const sextaData = new Date(diaLocal(segundaAtual).getTime() + 4 * MS_DIA);
  const sextaAtual = `${sextaData.getFullYear()}-${String(sextaData.getMonth() + 1).padStart(2, '0')}-${String(sextaData.getDate()).padStart(2, '0')}`;
  const obrasSemSemanaAtual = obrasParaCriar.filter(
    (o) => (!obraFiltro || o.id === obraFiltro) && !registros.some((r) => r.obraId === o.id && r.dataInicioSemana.slice(0, 10) === segundaAtual),
  );
  function abrirSemanaAtual() {
    setNova({ ...semanalVazia(), obraId: obrasSemSemanaAtual.length === 1 ? obrasSemSemanaAtual[0].id : '' });
    setPainelAberto(true);
  }
  function abrirPdf(s: DdsSemanal) {
    void visualizar({ titulo: 'PDF da semana de DDS', nomeArquivo: `dds-semanal-${s.id}.pdf`, obter: () => api.ddsSemanal.baixarPdf(s.id) });
  }

  const colunas: Coluna<DdsSemanal>[] = [
    {
      chave: 'semana',
      rotulo: 'Semana',
      render: (s) => {
        const passou = semanaPassouSemConcluir(s);
        return (
          <CelulaDupla
            inicio={
              <span aria-hidden="true" style={{ flexShrink: 0, width: 34, height: 34, borderRadius: 10, display: 'grid', placeItems: 'center', color: 'var(--sst-acao-marca-tinta)', backgroundColor: 'var(--sst-acao-marca-wash)' }}>
                <CalendarLtr20Regular />
              </span>
            }
            titulo={<span style={{ fontVariantNumeric: 'tabular-nums' }}>{rotuloSemana(s)}</span>}
            detalhe={
              <>
                {s.numeroDocumento && <span style={{ fontFamily: "'Roboto Mono', ui-monospace, monospace" }}>{s.numeroDocumento} · </span>}
                {passou ? <span style={{ color: tokensUi.status.atencao.tinta, fontWeight: 600 }}>semana passou, falta concluir</span> : rotuloRelativo(s)}
              </>
            }
          />
        );
      },
    },
    {
      chave: 'obra',
      rotulo: 'Obra',
      render: (s) => (
        <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center', flexWrap: 'wrap', fontWeight: 500 }}>
          {s.obraNome}
          {!ehMinha(s.obraId) && <StatusChip tom="neutro">Acompanhando</StatusChip>}
        </span>
      ),
    },
    {
      chave: 'tipo',
      rotulo: 'Tipo',
      render: (s) => (
        <CelulaDupla
          tituloLeve
          titulo={tipoDdsSemanalLabel[s.tipo].replace(/^Empregados /, '')}
          detalhe={s.empresaTerceirizada ?? undefined}
        />
      ),
    },
    {
      chave: 'responsavel',
      rotulo: 'Responsável',
      render: (s) => (
        <CelulaDupla
          tituloLeve
          inicio={<FotoTrabalhador nome={s.responsavelUsuarioNome} trabalhadorId={s.responsavelTrabalhadorId ?? ''} temFoto={s.responsavelTemFoto} tamanho={36} />}
          titulo={s.responsavelUsuarioNome}
        />
      ),
    },
    { chave: 'dias', rotulo: 'Dias (seg a sex)', render: (s) => <DiasDaSemana semana={s} /> },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (s) => <StatusChip tom={tomStatusSemanal[s.status] ?? 'neutro'}>{statusDdsSemanalLabel[s.status]}</StatusChip>,
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="Semanas de DDS"
        subtitulo="Diálogo Diário de Segurança, registrado por semana"
        acoes={
          podeCriar && <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-semana-dds"
          >
            {painelAberto ? 'Fechar' : 'Nova semana'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {obras.length > 1 && (
        <div style={{ maxWidth: 360 }}>
          <Field label="Obra">
            <Select value={obraFiltro} onChange={(_, d) => setObraFiltro(d.value)}>
              <option value="">Todas as obras</option>
              {minhasObras.length > 0 && (
                <optgroup label="Minhas obras">
                  {minhasObras.map((o) => (
                    <option key={o.id} value={o.id}>{o.nome}</option>
                  ))}
                </optgroup>
              )}
              {obrasSupervisao.length > 0 && (
                <optgroup label="Supervisão (somente consulta)">
                  {obrasSupervisao.map((o) => (
                    <option key={o.id} value={o.id}>{o.nome}</option>
                  ))}
                </optgroup>
              )}
            </Select>
          </Field>
        </div>
      )}

      {obraFiltroSelecionada && !ehMinha(obraFiltroSelecionada.id) && <FaixaSupervisao obraNome={obraFiltroSelecionada.nome} />}

      {listaCarregada && !painelAberto && obrasSemSemanaAtual.length > 0 && (
        <div
          role="status"
          style={{
            display: 'flex', flexWrap: 'wrap', gap: 14, alignItems: 'center', justifyContent: 'space-between',
            padding: '14px 16px', borderRadius: 14, backgroundColor: tokensUi.status.atencao.fundo,
            border: `1px solid ${tokensUi.bordaSuave}`,
          }}
        >
          <div>
            <div style={{ fontWeight: 700, fontSize: 14, color: tokensUi.status.atencao.tinta }}>
              Semana atual ({rotuloSemana({ dataInicioSemana: segundaAtual, dataFimSemana: sextaAtual } as DdsSemanal)}): nenhuma semana de DDS aberta
            </div>
            <div style={{ fontSize: 12.5, marginTop: 2 }}>
              {obrasSemSemanaAtual.length === 1
                ? `${obrasSemSemanaAtual[0].nome} ainda não tem registro desta semana.`
                : `${obrasSemSemanaAtual.map((o) => o.nome).join(', ')} ainda não têm registro desta semana.`}
            </div>
          </div>
          <Button appearance="primary" icon={<Add24Regular />} onClick={abrirSemanaAtual}>
            Abrir semana atual
          </Button>
        </div>
      )}

      <div id="painel-nova-semana-dds">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova semana de DDS">
          {obrasIndisponiveis && (
            <FeedbackInline tom="aviso">A lista de obras não está disponível neste dispositivo. Conecte-se à internet e reabra esta tela para carregar as opções.</FeedbackInline>
          )}
          <FormSection titulo="Dados da Semana" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={4}>
                <Field label="Obra" required>
                  <Select value={nova.obraId} onChange={(_, d) => setNova({ ...nova, obraId: d.value })}>
                    <option value="">Selecione</option>
                    {obrasParaCriar.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Tipo">
                  <Select value={String(nova.tipo)} onChange={(_, d) => setNova({ ...nova, tipo: Number(d.value) })}>
                    <option value={String(TipoDdsSemanal.Proprios)}>{tipoDdsSemanalLabel[TipoDdsSemanal.Proprios]}</option>
                    <option value={String(TipoDdsSemanal.Terceirizados)}>{tipoDdsSemanalLabel[TipoDdsSemanal.Terceirizados]}</option>
                    <option value={String(TipoDdsSemanal.ProprioseTerceirizados)}>
                      {tipoDdsSemanalLabel[TipoDdsSemanal.ProprioseTerceirizados]}
                    </option>
                  </Select>
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Início da semana" required>
                  <CampoData
                    value={nova.dataInicioSemana}
                    onChange={(_, d) => setNova({ ...nova, dataInicioSemana: d.value })}
                  />
                </Field>
              </Campo>
              {tipoDdsSemanalIncluiTerceirizados(nova.tipo) && (
                <Campo span={4}>
                  <Field label="Empresa terceirizada" required>
                    <Input
                      value={nova.empresaTerceirizada ?? ''}
                      onChange={(_, d) => setNova({ ...nova, empresaTerceirizada: d.value })}
                    />
                  </Field>
                </Campo>
              )}
              <Campo span={5}>
                <Field label="Local / Frente de serviço">
                  <Input
                    value={nova.localFrenteServico ?? ''}
                    onChange={(_, d) => setNova({ ...nova, localFrenteServico: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Abrir semana
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card densidade="compacta">
        {daObra.length > 0 && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
            <FiltroRapido<FiltroSemana>
              aria-label="Filtrar semanas de DDS pela situação"
              valor={filtro}
              aoMudar={setFiltro}
              opcoes={[
                { valor: 'todas', rotulo: 'Todas', contagem: contagem.todas },
                { valor: 'andamento', rotulo: 'Em andamento', contagem: contagem.andamento },
                { valor: 'passou', rotulo: 'Semana passou sem concluir', contagem: contagem.passou },
                { valor: 'concluidas', rotulo: 'Concluídas', contagem: contagem.concluidas },
              ]}
            />
            <Input
              aria-label="Buscar obra, número ou responsável"
              placeholder="Buscar obra, nº ou responsável"
              contentBefore={<Search20Regular />}
              value={busca}
              onChange={(_, d) => setBusca(d.value)}
              style={{ minWidth: 260 }}
            />
          </div>
        )}
        <DataTable
          aria-label="Semanas de DDS registradas"
          colunas={colunas}
          linhas={linhasVisiveis}
          chaveLinha={(s) => s.id}
          carregando={carregandoLista}
          tomLinha={(s) => (s.status === StatusDdsSemanal.Concluida ? 'ok' : semanaPassouSemConcluir(s) ? 'atencao' : 'info')}
          vazio={daObra.length > 0 ? {
            titulo: 'Nenhuma semana nesta seleção',
            descricao: 'Troque o filtro ou a busca para ver outras semanas.',
          } : listaCarregada ? {
            titulo: 'Nenhuma semana de DDS registrada ainda.',
            acao: podeCriar ? { rotulo: 'Nova semana', aoClicar: () => setPainelAberto(true) } : undefined,
          } : {
            titulo: 'Lista indisponível neste dispositivo.',
            descricao: 'Conecte-se à internet e carregue as semanas de DDS antes de usá-las offline.',
            acao: { rotulo: 'Tentar novamente', aoClicar: () => void carregar() },
          }}
          // 18/11/2026: semana concluída abre o PDF (com internet); em andamento segue para o preenchimento.
          aoClicarLinha={(s) =>
            s.status === StatusDdsSemanal.Concluida && navigator.onLine
              ? abrirPdf(s)
              : navigate(`/prevencao/dds/semana/${s.id}`)
          }
          // Ação principal (25/11/2026): em andamento → Continuar (é o próprio detalhe); concluída → PDF + Abrir.
          acoesLinha={(s) =>
            s.status === StatusDdsSemanal.EmAndamento ? (
              <BotaoAcao
                tom="ver"
                icon={<Play24Regular />}
                aria-label="Continuar a semana de DDS"
                onClick={(evento) => {
                  evento.stopPropagation();
                  navigate(`/prevencao/dds/semana/${s.id}`);
                }}
              >
                Continuar
              </BotaoAcao>
            ) : (
              <>
                {navigator.onLine && (
                  <BotaoAcao
                    tom="baixar"
                    icon={<DocumentPdf24Regular />}
                    aria-label="Abrir o PDF da semana de DDS"
                    onClick={(evento) => {
                      evento.stopPropagation();
                      abrirPdf(s);
                    }}
                  >
                    PDF
                  </BotaoAcao>
                )}
                <BotaoAcao
                  tom="ver"
                  icon={<Open24Regular />}
                  aria-label="Abrir detalhe da semana de DDS"
                  title="Abrir detalhe"
                  onClick={(evento) => {
                    evento.stopPropagation();
                    navigate(`/prevencao/dds/semana/${s.id}`);
                  }}
                />
              </>
            )
          }
        />
      </Card>
      {dialogoVisualizador}
    </div>
  );
}
