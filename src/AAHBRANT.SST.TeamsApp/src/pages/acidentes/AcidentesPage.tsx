import { useCallback, useEffect, useState, type ReactElement } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Abas,
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  ListaSelecaoMultipla,
  Select,
  StatusChip,
  Text,
  Textarea,
  type Coluna,
  type Tom,
  tokensUi,
} from '@ui';
import { AddCircle24Regular } from '@fluentui/react-icons';
import {
  api,
  GravidadeAcidente,
  gravidadeAcidenteLabel,
  StatusAcidente,
  statusAcidenteLabel,
  tipoOcorrenciaLabel,
  type Acidente,
  type Atividade,
  type NovoAcidente,
  type Obra,
  type RelatoOcorrenciaSugestao,
  type Trabalhador,
} from '../../lib/api';
import { HhtMensalTab } from './HhtMensalTab';
import { RelatoOcorrenciaIa } from './RelatoOcorrenciaIa';

// Campos que a IA pode preencher a partir do relato. Mostram o selo "IA" até o técnico editá-los.
type CampoIa =
  | 'tipo' | 'atividadeId' | 'local' | 'data' | 'hora' | 'trabalhadoresIds' | 'descricao' | 'lesao'
  | 'consequencia' | 'atendimento' | 'houveAfastamento' | 'diasAfastamento' | 'gravidade';

const seloIa = {
  fontSize: 10,
  fontWeight: 700,
  lineHeight: '14px',
  padding: '0 7px',
  borderRadius: 10,
  color: tokensUi.status.info.tinta,
  backgroundColor: tokensUi.status.info.fundo,
} as const;

const destaqueIa = { backgroundColor: tokensUi.status.info.fundo } as const;

function novaInicial(): NovoAcidente {
  return {
    tipo: 1,
    obraId: '',
    trabalhadoresIds: [],
    atividadeId: '',
    local: '',
    data: '',
    hora: '',
    descricao: '',
    lesao: '',
    consequencia: '',
    atendimento: '',
    houveAfastamento: false,
    diasAfastamento: undefined,
    numeroCat: '',
    causas: '',
    gravidade: GravidadeAcidente.SemAfastamento,
    diasDebitadosInformados: undefined,
  };
}

// Registrado/EmInvestigacao/Concluido — mesmo padrão de progressão neutro→atencao→ok já usado em
// StatusPcmsoDocumento (Task 12) para os 3 estágios de um fluxo sem workflow de aprovação formal.
const tomPorStatusAcidente: Record<number, Tom> = {
  [StatusAcidente.Registrado]: 'neutro',
  [StatusAcidente.EmInvestigacao]: 'atencao',
  [StatusAcidente.Concluido]: 'ok',
};

// Onda 2 Task 20 (camada ui/): lista + formulário de registro de acidentes/incidentes/quase-acidentes,
// sempre aninhada como aba de OcorrenciasPage (nunca teve PageHeader próprio). Abas internas
// "Acidentes & Incidentes"/"HHT Mensal" viram Abas nivel="interno" — navegação de conteúdo, não de
// página-pilar, então sem useAbaNaUrl (mesmo critério já usado nas sub-abas de TrabalhadorDetalhePage).
export function AcidentesPage({ tipoFixo }: { tipoFixo?: number } = {}) {
  const navigate = useNavigate();
  const [acidentes, setAcidentes] = useState<Acidente[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [nova, setNova] = useState<NovoAcidente>(novaInicial());
  // Suporta abrir a tela já filtrada por tipo via URL (?tipo=3) — legado, de quando "Acidentes",
  // "Incidentes" e "Quase-acidentes" eram itens de menu apontando pra essa mesma tela. Hoje viraram
  // abas de OcorrenciasPage, que passa tipoFixo diretamente (sem depender de querystring).
  const [searchParams] = useSearchParams();
  const [filtroStatus, setFiltroStatus] = useState<string>('');
  const [filtroTipo, setFiltroTipo] = useState<string>(
    tipoFixo != null ? String(tipoFixo) : searchParams.get('tipo') ?? '',
  );
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [aba, setAba] = useState<'ocorrencias' | 'hht'>('ocorrencias');
  const [camposIa, setCamposIa] = useState<Set<CampoIa>>(new Set());
  const [editados, setEditados] = useState<Set<CampoIa>>(new Set());
  const [avisosIa, setAvisosIa] = useState<string[]>([]);

  // Alteração feita pelo técnico: tira o selo "IA" e protege o campo nas próximas rodadas de perguntas.
  function alterar(campo: CampoIa, patch: Partial<NovoAcidente>) {
    setNova((atual) => ({ ...atual, ...patch }));
    setCamposIa((atual) => {
      if (!atual.has(campo)) return atual;
      const proximo = new Set(atual);
      proximo.delete(campo);
      return proximo;
    });
    setEditados((atual) => new Set(atual).add(campo));
  }

  const aplicarSugestao = useCallback(
    (s: RelatoOcorrenciaSugestao, primeiraRodada: boolean) => {
      const protegidos = primeiraRodada ? new Set<CampoIa>() : editados;
      const preenchidos = new Set<CampoIa>();
      setNova((atual) => {
        const proxima = { ...atual };
        const definir = <K extends CampoIa>(campo: K, valor: NovoAcidente[K] | null | undefined, vazio: NovoAcidente[K]) => {
          if (protegidos.has(campo)) return;
          if (valor === null || valor === undefined || valor === '') {
            // Relato novo substitui o formulário; nas rodadas de perguntas, "não sei" não apaga nada.
            if (primeiraRodada) proxima[campo] = vazio;
            return;
          }
          proxima[campo] = valor;
          preenchidos.add(campo);
        };
        definir('tipo', s.tipo, 1);
        definir('atividadeId', s.atividadeId, '');
        definir('local', s.local, '');
        definir('data', s.data, '');
        definir('hora', s.hora, '');
        definir('descricao', s.descricao, '');
        definir('lesao', s.lesao, '');
        definir('consequencia', s.consequencia, '');
        definir('atendimento', s.atendimento, '');
        definir('houveAfastamento', s.houveAfastamento, false);
        definir('diasAfastamento', s.diasAfastamento ?? undefined, undefined);
        definir('gravidade', s.gravidade, GravidadeAcidente.SemAfastamento);
        if (!protegidos.has('trabalhadoresIds') && (primeiraRodada || s.trabalhadoresIds.length > 0)) {
          proxima.trabalhadoresIds = s.trabalhadoresIds;
          if (s.trabalhadoresIds.length > 0) preenchidos.add('trabalhadoresIds');
        }
        return proxima;
      });
      setCamposIa((atual) => {
        const base = primeiraRodada ? new Set<CampoIa>() : new Set([...atual].filter((c) => !protegidos.has(c)));
        preenchidos.forEach((c) => base.add(c));
        return base;
      });
      if (primeiraRodada) setEditados(new Set());
      setAvisosIa(s.avisos);
      setErro(null);
    },
    [editados],
  );

  const aoSoTranscrever = useCallback((transcricao: string) => {
    setNova((atual) => ({ ...atual, descricao: transcricao }));
    setCamposIa(new Set<CampoIa>(['descricao']));
    setAvisosIa(['A fala foi transcrita na descrição, mas a IA não conseguiu classificar. Complete os outros campos.']);
  }, []);

  function rotulo(texto: string, campo: CampoIa): ReactElement | string {
    return camposIa.has(campo) ? (
      <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
        {texto} <span style={seloIa}>IA</span>
      </span>
    ) : (
      texto
    );
  }

  const estiloCampo = (campo: CampoIa) => (camposIa.has(campo) ? destaqueIa : undefined);
  const nomesSelecionados = trabalhadores
    .filter((t) => nova.trabalhadoresIds?.includes(t.id))
    .map((t) => (t.matricula ? `${t.nome} (${t.matricula})` : t.nome));

  async function carregar() {
    try {
      setErro(null);
      const [listaAcidentes, listaObras, listaAtividades] = await Promise.all([
        api.acidentes.listar({
          status: filtroStatus ? Number(filtroStatus) : undefined,
          tipo: filtroTipo ? Number(filtroTipo) : undefined,
        }),
        api.obras.listar(),
        api.atividades.listar(),
      ]);
      setAcidentes(listaAcidentes);
      setObras(listaObras);
      setAtividades(listaAtividades);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar acidentes.');
    }
  }

  // Funcionários envolvidos são sempre da obra escolhida (200+ no total não cabem num select único).
  useEffect(() => {
    if (!nova.obraId) {
      setTrabalhadores([]);
      return;
    }
    api.trabalhadores
      .listar(nova.obraId)
      .then(setTrabalhadores)
      .catch(() => setTrabalhadores([]));
  }, [nova.obraId]);

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtroStatus, filtroTipo]);

  async function criar() {
    if (!nova.obraId) {
      setErro('Selecione a obra.');
      return;
    }
    if (!nova.local.trim()) {
      setErro('Informe o local da ocorrência.');
      return;
    }
    if (!nova.data) {
      setErro('Informe a data da ocorrência.');
      return;
    }
    if (!nova.descricao.trim()) {
      setErro('Informe a descrição da ocorrência.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.acidentes.criar({
        ...nova,
        trabalhadoresIds: nova.trabalhadoresIds ?? [],
        atividadeId: nova.atividadeId || null,
        // input type="time" retorna "HH:mm"; TimeSpan? no backend exige segundos ("HH:mm:ss").
        hora: nova.hora ? `${nova.hora}:00` : null,
        lesao: nova.lesao || null,
        consequencia: nova.consequencia || null,
        atendimento: nova.atendimento || null,
        diasAfastamento: nova.houveAfastamento ? nova.diasAfastamento ?? null : null,
        numeroCat: nova.numeroCat || null,
        causas: nova.causas || null,
      });
      setNova(novaInicial());
      setCamposIa(new Set());
      setEditados(new Set());
      setAvisosIa([]);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar ocorrência.');
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<Acidente>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoOcorrenciaLabel[a.tipo] },
    { chave: 'obra', rotulo: 'Obra', render: (a) => a.obraNome ?? '—' },
    { chave: 'funcionario', rotulo: 'Funcionário', render: (a) => {
        const nomes = a.envolvidos.map((e) => e.nome);
        if (nomes.length === 0) return '—';
        return nomes.length === 1 ? nomes[0] : `${nomes[0]} +${nomes.length - 1}`;
      },
    },
    { chave: 'data', rotulo: 'Data', render: (a) => a.data?.slice(0, 10) ?? '' },
    { chave: 'local', rotulo: 'Local' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (a) => <StatusChip tom={tomPorStatusAcidente[a.status] ?? 'neutro'}>{statusAcidenteLabel[a.status]}</StatusChip>,
    },
    { chave: 'gravidade', rotulo: 'Gravidade', render: (a) => gravidadeAcidenteLabel[a.gravidade] },
  ];

  return (
    <div>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Abas
        nivel="interno"
        abas={[
          { valor: 'ocorrencias', rotulo: 'Acidentes & Incidentes' },
          { valor: 'hht', rotulo: 'HHT Mensal' },
        ]}
        valor={aba}
        aoMudar={setAba}
      />

      {aba === 'hht' && <HhtMensalTab obras={obras} />}

      {aba === 'ocorrencias' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card titulo="Registrar acidente / incidente">
            {/* minmax(0, 1fr): sem isso o grid cresce com o texto do aviso em vez de quebrar a linha. */}
            <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 12, marginBottom: 16 }}>
              <div style={{ maxWidth: 420 }}>
                <Field label="Obra" required>
                  <Select value={nova.obraId} onChange={(_, d) => setNova({ ...nova, obraId: d.value, trabalhadoresIds: [] })}>
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <RelatoOcorrenciaIa
                obraId={nova.obraId}
                desabilitado={carregando}
                aoSugerir={aplicarSugestao}
                aoSoTranscrever={aoSoTranscrever}
                aoErro={setErro}
              />
              {camposIa.size > 0 && (
                <FeedbackInline tom="info">
                  A IA preencheu o registro a partir do relato. Revise cada campo antes de registrar: tipo, gravidade
                  e afastamento alimentam os indicadores da NBR 14280.
                </FeedbackInline>
              )}
              {avisosIa.length > 0 && (
                <FeedbackInline tom="aviso" aoFechar={() => setAvisosIa([])}>
                  {avisosIa.map((a) => (
                    <div key={a}>{a}</div>
                  ))}
                </FeedbackInline>
              )}
            </div>
            <FormSection titulo="Dados Gerais da Ocorrência" numero={1} primeira>
              <FormGrid>
                <Campo span={2}>
                  <Field label={rotulo('Tipo', 'tipo')} required>
                    <Select select={{ style: estiloCampo('tipo') }} value={String(nova.tipo)} onChange={(_, d) => alterar('tipo', { tipo: Number(d.value) })}>
                      {Object.entries(tipoOcorrenciaLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Atividade', 'atividadeId')}>
                    <Select
                      select={{ style: estiloCampo('atividadeId') }}
                      value={nova.atividadeId ?? ''}
                      onChange={(_, d) => alterar('atividadeId', { atividadeId: d.value })}
                    >
                      <option value="">Nenhuma</option>
                      {atividades.map((atividade) => (
                        <option key={atividade.id} value={atividade.id}>
                          {atividade.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Local', 'local')} required>
                    <Input style={estiloCampo('local')} value={nova.local} onChange={(_, d) => alterar('local', { local: d.value })} />
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label={rotulo('Data', 'data')} required>
                    <CampoData style={estiloCampo('data')} value={nova.data} onChange={(_, d) => alterar('data', { data: d.value })} />
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label={rotulo('Hora', 'hora')}>
                    <Input style={estiloCampo('hora')} type="time" value={nova.hora ?? ''} onChange={(_, d) => alterar('hora', { hora: d.value })} />
                  </Field>
                </Campo>
                <Campo span={12}>
                  <Field
                    label={rotulo(`Funcionários envolvidos${(nova.trabalhadoresIds?.length ?? 0) > 0 ? ` (${nova.trabalhadoresIds?.length})` : ''}`, 'trabalhadoresIds')}
                    hint={
                      !nova.obraId
                        ? undefined
                        : nomesSelecionados.length > 0
                          // A lista tem centenas de nomes: sem isso, quem a IA marcou fica escondido no meio dela.
                          ? `Selecionados: ${nomesSelecionados.join(', ')}`
                          : 'Selecione todos os funcionários envolvidos na ocorrência. Deixe vazio se não houve funcionário envolvido.'
                    }
                  >
                    {nova.obraId ? (
                      <ListaSelecaoMultipla
                        aria-label="Funcionários envolvidos"
                        opcoes={trabalhadores.map((t) => ({ id: t.id, rotulo: t.matricula ? `${t.nome} (${t.matricula})` : t.nome }))}
                        selecionados={nova.trabalhadoresIds ?? []}
                        aoMudar={(atualizar) => alterar('trabalhadoresIds', { trabalhadoresIds: atualizar(nova.trabalhadoresIds ?? []) })}
                      />
                    ) : (
                      <Text>Selecione a obra primeiro.</Text>
                    )}
                  </Field>
                </Campo>
              </FormGrid>
            </FormSection>

            <FormSection titulo="Lesão, Gravidade e Consequências" numero={2}>
              <FormGrid>
                <Campo span={12}>
                  <Field label={rotulo('Descrição', 'descricao')} required>
                    <Textarea style={estiloCampo('descricao')} resize="vertical" rows={5} value={nova.descricao} onChange={(_, d) => alterar('descricao', { descricao: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Lesão', 'lesao')}>
                    <Input style={estiloCampo('lesao')} value={nova.lesao ?? ''} onChange={(_, d) => alterar('lesao', { lesao: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Consequência', 'consequencia')}>
                    <Input style={estiloCampo('consequencia')} value={nova.consequencia ?? ''} onChange={(_, d) => alterar('consequencia', { consequencia: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Atendimento prestado', 'atendimento')}>
                    <Input style={estiloCampo('atendimento')} value={nova.atendimento ?? ''} onChange={(_, d) => alterar('atendimento', { atendimento: d.value })} />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label={rotulo('Houve afastamento?', 'houveAfastamento')}>
                    <Select
                      select={{ style: estiloCampo('houveAfastamento') }}
                      value={nova.houveAfastamento ? '1' : '0'}
                      onChange={(_, d) => alterar('houveAfastamento', { houveAfastamento: d.value === '1' })}
                    >
                      <option value="0">Não</option>
                      <option value="1">Sim</option>
                    </Select>
                  </Field>
                </Campo>
                {nova.houveAfastamento && (
                  <Campo span={3}>
                    <Field label={rotulo('Dias de afastamento', 'diasAfastamento')}>
                      <Input
                        style={estiloCampo('diasAfastamento')}
                        type="number"
                        min={0}
                        value={nova.diasAfastamento?.toString() ?? ''}
                        onChange={(_, d) => alterar('diasAfastamento', { diasAfastamento: d.value ? Number(d.value) : undefined })}
                      />
                    </Field>
                  </Campo>
                )}
                <Campo span={3}>
                  <Field label={rotulo('Gravidade', 'gravidade')} required>
                    <Select
                      select={{ style: estiloCampo('gravidade') }}
                      value={String(nova.gravidade)}
                      onChange={(_, d) =>
                        alterar('gravidade', { gravidade: Number(d.value), diasDebitadosInformados: undefined })
                      }
                    >
                      {Object.entries(gravidadeAcidenteLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                {nova.gravidade === GravidadeAcidente.IncapacidadePermanenteParcial && (
                  <Campo span={6}>
                    <Field
                      label="Dias Debitados (consultar Quadro III da NBR 14280)"
                      required
                      hint="Valor não calculado automaticamente pelo sistema — consulte a tabela oficial de Dias Debitados por lesão/parte do corpo."
                    >
                      <Input
                        type="number"
                        min={1}
                        value={nova.diasDebitadosInformados?.toString() ?? ''}
                        onChange={(_, d) =>
                          setNova({ ...nova, diasDebitadosInformados: d.value ? Number(d.value) : undefined })
                        }
                      />
                    </Field>
                  </Campo>
                )}
                {(nova.gravidade === GravidadeAcidente.Obito ||
                  nova.gravidade === GravidadeAcidente.IncapacidadePermanenteTotal) && (
                  <Campo span={3}>
                    <Field label="Dias Debitados">
                      <Text>6.000 dias (fixo, calculado automaticamente)</Text>
                    </Field>
                  </Campo>
                )}
                <Campo span={3}>
                  <Field label="Número da CAT">
                    <Input value={nova.numeroCat ?? ''} onChange={(_, d) => setNova({ ...nova, numeroCat: d.value })} />
                  </Field>
                </Campo>
              </FormGrid>
            </FormSection>

            <FormRodape>
              <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
                Registrar
              </Button>
            </FormRodape>
          </Card>

          <Card
            titulo="Acidentes e incidentes"
            acoes={
              <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end' }}>
                {tipoFixo == null && (
                  <Field label="Tipo">
                    <Select value={filtroTipo} onChange={(_, d) => setFiltroTipo(d.value)}>
                      <option value="">Todos</option>
                      {Object.entries(tipoOcorrenciaLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                )}
                <Field label="Status">
                  <Select value={filtroStatus} onChange={(_, d) => setFiltroStatus(d.value)}>
                    <option value="">Todos</option>
                    {Object.entries(statusAcidenteLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
            }
          >
            <DataTable
              aria-label="Acidentes e incidentes"
              colunas={colunas}
              linhas={acidentes}
              chaveLinha={(a) => a.id}
              vazio={{ titulo: 'Nenhuma ocorrência cadastrada ainda.' }}
              aoClicarLinha={(a) => navigate(`/acidentes/${a.id}`)}
            />
          </Card>
        </div>
      )}
    </div>
  );
}
