import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Abas,
  Button,
  Campo,
  Card,
  Carregando,
  DetailPageLayout,
  EstadoVazio,
  FeedbackInline,
  Field,
  FormGrid,
  Input,
  Select,
  StatusChip,
  Textarea,
  WorkflowActions,
  tokensUi,
  type AcaoWorkflow,
} from '@ui';
import {
  api,
  canalIdeiaLabel,
  decisaoIdeiaLabel,
  nivelIdeiaLabel,
  prioridadeIdeiaLabel,
  StatusIdeia,
  statusDemandaLabel,
  statusIdeiaLabel,
  statusRequisitoIdeiaLabel,
  tipoHistoricoIdeiaLabel,
  viabilidadeIdeiaLabel,
  type AnaliseIdeia,
  type IdeiaDetalhe,
  type IdeiaRequisito,
  type SugestoesAnaliseIdeia,
} from '../../lib/api';
import {
  formatarData,
  formatarDataHora,
  formatarTamanho,
  permissaoParaStatus,
  tomStatusIdeia,
  usePermissoesIdeia,
} from './ideiasComuns';

type Aba = 'analise' | 'discussao' | 'requisitos' | 'anexos' | 'historico';

const STATUS_ENCERRADOS: number[] = [StatusIdeia.Implantada, StatusIdeia.Descartada];
const STATUS_COM_REQUISITO: number[] = [
  StatusIdeia.Aprovada,
  StatusIdeia.Priorizada,
  StatusIdeia.EmDesenvolvimento,
  StatusIdeia.EmTesteValidacao,
];

function analiseDe(d: IdeiaDetalhe): AnaliseIdeia {
  return {
    titulo: d.titulo, descricao: d.descricao, problemaOportunidade: d.problemaOportunidade,
    objetivo: d.objetivo, solucaoSugerida: d.solucaoSugerida, modulo: d.modulo, submodulo: d.submodulo,
    categoria: d.categoria, beneficioEsperado: d.beneficioEsperado, possiveisImpactos: d.possiveisImpactos,
    integracoesNecessarias: d.integracoesNecessarias, necessidadeIa: d.necessidadeIa,
    dependencias: d.dependencias, informacoesFaltantes: d.informacoesFaltantes, impacto: d.impacto,
    urgencia: d.urgencia, complexidade: d.complexidade, esforco: d.esforco, valorNegocio: d.valorNegocio,
    esforcoEstimado: d.esforcoEstimado, viabilidadeTecnica: d.viabilidadeTecnica,
    viabilidadeOperacional: d.viabilidadeOperacional, responsavelAnaliseNome: d.responsavelAnaliseNome,
    responsavelDesenvolvimentoNome: d.responsavelDesenvolvimentoNome, observacoes: d.observacoes,
  };
}

function SelectNivel({ valor, aoMudar }: { valor?: number | null; aoMudar: (v: number | null) => void }) {
  return (
    <Select value={valor ? String(valor) : ''} onChange={(_, d) => aoMudar(d.value === '' ? null : Number(d.value))}>
      <option value="">Não avaliado</option>
      {Object.entries(nivelIdeiaLabel).map(([v, r]) => (
        <option key={v} value={v}>{r}</option>
      ))}
    </Select>
  );
}

function SelectViabilidade({ valor, aoMudar }: { valor: number; aoMudar: (v: number) => void }) {
  return (
    <Select value={String(valor)} onChange={(_, d) => aoMudar(Number(d.value))}>
      {Object.entries(viabilidadeIdeiaLabel).map(([v, r]) => (
        <option key={v} value={v}>{r}</option>
      ))}
    </Select>
  );
}

function Texto({ rotulo, valor }: { rotulo: string; valor?: string | null }) {
  return (
    <div style={{ marginBottom: tokensUi.espaco.md }}>
      <div style={{ fontWeight: 600, fontSize: 12, opacity: 0.7 }}>{rotulo}</div>
      <div style={{ whiteSpace: 'pre-wrap' }}>{valor?.trim() ? valor : '—'}</div>
    </div>
  );
}

export function IdeiaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const { podeAnalisar, podeDecidir, podeDesenvolver, pode } = usePermissoesIdeia();
  const podeUsar = pode('ideia:usar') || podeAnalisar || podeDecidir || podeDesenvolver;

  const [ideia, setIdeia] = useState<IdeiaDetalhe | null>(null);
  const [form, setForm] = useState<AnaliseIdeia | null>(null);
  const [aba, setAba] = useState<Aba>('analise');
  const [erroCarga, setErroCarga] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [sucesso, setSucesso] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  const [justificativa, setJustificativa] = useState('');
  const [prioridade, setPrioridade] = useState('');

  const [sugestoes, setSugestoes] = useState<SugestoesAnaliseIdeia | null>(null);
  const [carregandoSugestoes, setCarregandoSugestoes] = useState(false);

  const [comentario, setComentario] = useState('');
  const [reqTitulo, setReqTitulo] = useState('');
  const [reqDescricao, setReqDescricao] = useState('');
  const [reqCriterios, setReqCriterios] = useState('');
  const [demandaResp, setDemandaResp] = useState<Record<string, string>>({});
  const [demandaEntrega, setDemandaEntrega] = useState<Record<string, string>>({});

  const aplicar = useCallback((d: IdeiaDetalhe) => {
    setIdeia(d);
    setForm(analiseDe(d));
    setPrioridade(d.prioridade ? String(d.prioridade) : '');
  }, []);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErroCarga(null);
      aplicar(await api.ideias.obter(id));
    } catch (e) {
      setErroCarga(e instanceof Error ? e.message : 'Falha ao carregar a ideia.');
    }
  }, [id, aplicar]);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  async function executar<T>(acao: () => Promise<T>, mensagemSucesso?: string): Promise<T | undefined> {
    setErro(null);
    setSucesso(null);
    try {
      const r = await acao();
      if (mensagemSucesso) setSucesso(mensagemSucesso);
      return r;
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível concluir a operação.');
      return undefined;
    }
  }

  if (!id) return <FeedbackInline tom="erro">Ideia não encontrada.</FeedbackInline>;
  if (!ideia || !form) {
    return erroCarga ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erroCarga}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={6} />
    );
  }

  const encerrada = STATUS_ENCERRADOS.includes(ideia.status);
  const editaAnalise = podeAnalisar && !encerrada;
  const atual = ideia;
  const f = form;
  const set = (parcial: Partial<AnaliseIdeia>) => setForm({ ...f, ...parcial });

  async function salvarAnalise() {
    setSalvando(true);
    const r = await executar(() => api.ideias.atualizarAnalise(atual.id, f), 'Análise salva.');
    if (r) aplicar(r);
    setSalvando(false);
  }

  async function mudarStatus(destino: number): Promise<boolean> {
    if ((destino === StatusIdeia.Adiada || destino === StatusIdeia.Descartada) && !justificativa.trim()) {
      setErro('Informe a justificativa.');
      return false;
    }
    if (destino === StatusIdeia.Priorizada && !prioridade) {
      setErro('Defina a prioridade (P1, P2 ou P3).');
      return false;
    }
    const r = await executar(
      () => api.ideias.alterarStatus(atual.id, destino, justificativa, prioridade ? Number(prioridade) : null),
      `Status alterado para ${statusIdeiaLabel[destino]}.`,
    );
    if (!r) return false;
    aplicar(r);
    setJustificativa('');
    return true;
  }

  const acoes: AcaoWorkflow[] = atual.proximosStatus
    .filter((d) => pode(permissaoParaStatus(d)))
    .map((d) => {
      const pedeJustificativa = d === StatusIdeia.Adiada || d === StatusIdeia.Descartada || d === StatusIdeia.Aprovada;
      return {
        chave: `status-${d}`,
        rotulo: statusIdeiaLabel[d],
        tom: d === StatusIdeia.Descartada ? 'destrutivo' : d === StatusIdeia.Adiada ? 'neutro' : 'primario',
        rotuloExecutar: 'Confirmar',
        aoExecutar: () => mudarStatus(d),
        formulario:
          pedeJustificativa || d === StatusIdeia.Priorizada ? (
            <>
              {d === StatusIdeia.Priorizada && (
                <Field label="Prioridade" required>
                  <Select value={prioridade} onChange={(_, x) => setPrioridade(x.value)}>
                    <option value="">Selecione</option>
                    {Object.entries(prioridadeIdeiaLabel).map(([v, r]) => (
                      <option key={v} value={v}>{r}</option>
                    ))}
                  </Select>
                </Field>
              )}
              {pedeJustificativa && (
                <Field
                  label="Justificativa"
                  required={d !== StatusIdeia.Aprovada}
                  hint={d === StatusIdeia.Aprovada ? 'Opcional' : undefined}
                >
                  <Textarea value={justificativa} onChange={(_, x) => setJustificativa(x.value)} rows={3} />
                </Field>
              )}
            </>
          ) : undefined,
      } satisfies AcaoWorkflow;
    });

  async function pedirSugestoes() {
    setCarregandoSugestoes(true);
    const r = await executar(() => api.ideias.sugestoes(atual.id));
    if (r) setSugestoes(r);
    setCarregandoSugestoes(false);
  }

  async function vincular(principalId: string) {
    const r = await executar(() => api.ideias.vincular(atual.id, principalId), 'Ideia vinculada.');
    if (r) {
      aplicar(r);
      setSugestoes(null);
    }
  }

  async function comentar() {
    if (!comentario.trim()) return;
    const r = await executar(() => api.ideias.comentar(atual.id, comentario.trim()));
    if (r) {
      setComentario('');
      await carregar();
    }
  }

  async function anexar(arquivo: File | undefined) {
    if (!arquivo) return;
    const ok = await executar(() => api.ideias.anexar(atual.id, arquivo).then(() => true), 'Arquivo anexado.');
    if (ok) await carregar();
  }

  async function baixar(anexoId: string, nome: string) {
    const blob = await executar(() => api.ideias.baixarAnexo(atual.id, anexoId));
    if (!blob) return;
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = nome;
    link.click();
    URL.revokeObjectURL(url);
  }

  async function criarRequisito() {
    if (!reqTitulo.trim() || !reqDescricao.trim() || !reqCriterios.trim()) {
      setErro('Preencha título, descrição do requisito e ao menos um critério de aceite.');
      return;
    }
    const r = await executar(
      () => api.ideias.criarRequisito(atual.id, { titulo: reqTitulo, descricao: reqDescricao, criteriosAceite: reqCriterios }),
      'Requisito criado.',
    );
    if (r) {
      setReqTitulo('');
      setReqDescricao('');
      setReqCriterios('');
      await carregar();
    }
  }

  async function aprovarRequisito(r: IdeiaRequisito) {
    if (await executar(() => api.ideias.aprovarRequisito(r.id), 'Requisito aprovado.')) await carregar();
  }

  async function criarDemanda(r: IdeiaRequisito) {
    const d = await executar(
      () => api.ideias.criarDemanda(r.id, { titulo: r.titulo, descricao: r.descricao }),
      'Demanda de desenvolvimento criada.',
    );
    if (d) await carregar();
  }

  async function atualizarDemanda(demandaId: string, status: number, atualResp?: string | null, atualEntrega?: string | null) {
    const ok = await executar(
      () =>
        api.ideias.atualizarDemanda(demandaId, {
          status,
          responsavelNome: demandaResp[demandaId] ?? atualResp ?? null,
          funcionalidadeEntregue: demandaEntrega[demandaId] ?? atualEntrega ?? null,
        }),
      'Demanda atualizada.',
    );
    if (ok) await carregar();
  }

  const abas = [
    { valor: 'analise' as const, rotulo: 'Análise' },
    { valor: 'discussao' as const, rotulo: 'Discussão', contador: atual.comentarios.length },
    { valor: 'requisitos' as const, rotulo: 'Requisitos e demandas', contador: atual.requisitos.length },
    { valor: 'anexos' as const, rotulo: 'Anexos', contador: atual.anexos.length },
    { valor: 'historico' as const, rotulo: 'Histórico' },
  ];

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: `${atual.codigo} — ${atual.titulo}`,
        subtitulo: [atual.modulo, atual.categoria].filter(Boolean).join(' · ') || 'Módulo ainda não identificado',
        status: <StatusChip tom={tomStatusIdeia[atual.status] ?? 'neutro'}>{statusIdeiaLabel[atual.status]}</StatusChip>,
        voltarPara: '/ideias',
        rotuloVoltar: 'Banco de Ideias',
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <Texto rotulo="Registrada por" valor={`${atual.registradoPorNome ?? '—'} (${canalIdeiaLabel[atual.canal]})`} />
            <Texto rotulo="Registrada em" valor={formatarDataHora(atual.createdAtUtc)} />
            <Texto rotulo="Prioridade" valor={atual.prioridade ? prioridadeIdeiaLabel[atual.prioridade] : undefined} />
            <Texto
              rotulo="Pontuação (apoio à decisão)"
              valor={
                atual.pontuacao != null
                  ? `${atual.pontuacao}/100${atual.prioridadeSugerida ? ` — sugere ${prioridadeIdeiaLabel[atual.prioridadeSugerida]}` : ''}`
                  : 'Avalie impacto, urgência, valor, esforço e complexidade'
              }
            />
            {atual.decisao != null && (
              <Texto rotulo="Decisão" valor={`${decisaoIdeiaLabel[atual.decisao]} por ${atual.decididoPorNome ?? '—'}`} />
            )}
            {atual.justificativa && <Texto rotulo="Justificativa" valor={atual.justificativa} />}
            {atual.ideiaPrincipalCodigo && <Texto rotulo="Vinculada a" valor={atual.ideiaPrincipalCodigo} />}
            {atual.ideiaSemelhanteCodigo && !atual.ideiaPrincipalId && (
              <FeedbackInline tom="aviso">
                A IA encontrou uma ideia semelhante: {atual.ideiaSemelhanteCodigo}. Veja em “Análise → Sugestões da IA”.
              </FeedbackInline>
            )}
          </Card>
          {acoes.length > 0 && <WorkflowActions acoes={acoes} erro={erro} />}
        </>
      }
    >
      {sucesso && <FeedbackInline tom="sucesso" aoFechar={() => setSucesso(null)}>{sucesso}</FeedbackInline>}
      {erro && acoes.length === 0 && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Registro de origem" subtitulo="Mensagem original — nunca é alterada" densidade="compacta">
        <div style={{ whiteSpace: 'pre-wrap' }}>{atual.mensagemOriginal}</div>
        <small style={{ opacity: 0.7 }}>
          {canalIdeiaLabel[atual.canal]}
          {atual.telegramUsuarioNome ? ` · ${atual.telegramUsuarioNome}` : ''} · estruturação: {atual.estruturadoPor}
        </small>
      </Card>

      <div style={{ margin: `${tokensUi.espaco.lg} 0` }}>
        <Abas nivel="modulo" abas={abas} valor={aba} aoMudar={setAba} aria-label="Seções da ideia" />
      </div>

      {aba === 'analise' && (
        <>
          <Card titulo="Estruturação e análise" densidade="compacta"
            acoes={
              editaAnalise ? (
                <Button appearance="primary" disabled={salvando} onClick={() => void salvarAnalise()}>
                  {salvando ? 'Salvando…' : 'Salvar análise'}
                </Button>
              ) : undefined
            }
          >
            {atual.informacoesFaltantes && (
              <FeedbackInline tom="info">Informações faltantes apontadas no registro: {atual.informacoesFaltantes}</FeedbackInline>
            )}
            <FormGrid>
              <Campo span={8}>
                <Field label="Título" required>
                  <Input value={f.titulo} readOnly={!editaAnalise} onChange={(_, d) => set({ titulo: d.value })} />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Categoria">
                  <Input value={f.categoria ?? ''} readOnly={!editaAnalise} onChange={(_, d) => set({ categoria: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Módulo">
                  <Input value={f.modulo ?? ''} readOnly={!editaAnalise} onChange={(_, d) => set({ modulo: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Submódulo">
                  <Input value={f.submodulo ?? ''} readOnly={!editaAnalise} onChange={(_, d) => set({ submodulo: d.value })} />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Descrição" required>
                  <Textarea value={f.descricao} rows={3} readOnly={!editaAnalise} onChange={(_, d) => set({ descricao: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Problema ou oportunidade" hint="Qual problema esta ideia resolve?">
                  <Textarea value={f.problemaOportunidade ?? ''} rows={3} readOnly={!editaAnalise} onChange={(_, d) => set({ problemaOportunidade: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Objetivo" hint="O que queremos alcançar?">
                  <Textarea value={f.objetivo ?? ''} rows={3} readOnly={!editaAnalise} onChange={(_, d) => set({ objetivo: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Solução sugerida">
                  <Textarea value={f.solucaoSugerida ?? ''} rows={3} readOnly={!editaAnalise} onChange={(_, d) => set({ solucaoSugerida: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Benefício esperado" hint="Qual benefício essa solução trará?">
                  <Textarea value={f.beneficioEsperado ?? ''} rows={3} readOnly={!editaAnalise} onChange={(_, d) => set({ beneficioEsperado: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Possíveis impactos">
                  <Textarea value={f.possiveisImpactos ?? ''} rows={2} readOnly={!editaAnalise} onChange={(_, d) => set({ possiveisImpactos: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Dependências">
                  <Textarea value={f.dependencias ?? ''} rows={2} readOnly={!editaAnalise} onChange={(_, d) => set({ dependencias: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Integrações necessárias">
                  <Input value={f.integracoesNecessarias ?? ''} readOnly={!editaAnalise} onChange={(_, d) => set({ integracoesNecessarias: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Necessidade de IA">
                  <Select
                    disabled={!editaAnalise}
                    value={f.necessidadeIa == null ? '' : f.necessidadeIa ? 'sim' : 'nao'}
                    onChange={(_, d) => set({ necessidadeIa: d.value === '' ? null : d.value === 'sim' })}
                  >
                    <option value="">Não avaliado</option>
                    <option value="sim">Sim</option>
                    <option value="nao">Não</option>
                  </Select>
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Informações faltantes">
                  <Textarea value={f.informacoesFaltantes ?? ''} rows={2} readOnly={!editaAnalise} onChange={(_, d) => set({ informacoesFaltantes: d.value })} />
                </Field>
              </Campo>
            </FormGrid>
          </Card>

          <Card titulo="Avaliação" subtitulo="Negócio e Tecnologia" densidade="compacta">
            <FormGrid>
              {([
                ['impacto', 'Impacto'], ['urgencia', 'Urgência'], ['valorNegocio', 'Valor para o negócio'],
                ['esforco', 'Esforço'], ['complexidade', 'Complexidade'],
              ] as const).map(([chave, rotulo]) => (
                <Campo span={4} key={chave}>
                  <Field label={rotulo}>
                    {editaAnalise ? (
                      <SelectNivel valor={f[chave]} aoMudar={(v) => set({ [chave]: v })} />
                    ) : (
                      <Input readOnly value={f[chave] ? nivelIdeiaLabel[f[chave]!] : 'Não avaliado'} />
                    )}
                  </Field>
                </Campo>
              ))}
              <Campo span={4}>
                <Field label="Esforço estimado" hint="Ex.: 5 dias, 2 sprints">
                  <Input value={f.esforcoEstimado ?? ''} readOnly={!editaAnalise} onChange={(_, d) => set({ esforcoEstimado: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Viabilidade técnica">
                  {editaAnalise || podeDesenvolver ? (
                    <SelectViabilidade valor={f.viabilidadeTecnica} aoMudar={(v) => set({ viabilidadeTecnica: v })} />
                  ) : (
                    <Input readOnly value={viabilidadeIdeiaLabel[f.viabilidadeTecnica]} />
                  )}
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Viabilidade operacional">
                  {editaAnalise ? (
                    <SelectViabilidade valor={f.viabilidadeOperacional} aoMudar={(v) => set({ viabilidadeOperacional: v })} />
                  ) : (
                    <Input readOnly value={viabilidadeIdeiaLabel[f.viabilidadeOperacional]} />
                  )}
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Responsável pela análise">
                  <Input value={f.responsavelAnaliseNome ?? ''} readOnly={!editaAnalise} onChange={(_, d) => set({ responsavelAnaliseNome: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Responsável pelo desenvolvimento">
                  <Input value={f.responsavelDesenvolvimentoNome ?? ''} readOnly={!editaAnalise} onChange={(_, d) => set({ responsavelDesenvolvimentoNome: d.value })} />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Observações">
                  <Textarea value={f.observacoes ?? ''} rows={2} readOnly={!editaAnalise} onChange={(_, d) => set({ observacoes: d.value })} />
                </Field>
              </Campo>
            </FormGrid>
            <small style={{ opacity: 0.7 }}>
              Datas — aprovação: {formatarData(atual.dataAprovacaoUtc)} · início: {formatarData(atual.dataInicioUtc)} · conclusão:{' '}
              {formatarData(atual.dataConclusaoUtc)} · implantação: {formatarData(atual.dataImplantacaoUtc)}
            </small>
          </Card>

          {podeAnalisar && (
            <Card
              titulo="Sugestões da IA"
              subtitulo="Apoio à análise — a decisão final é sempre do gestor"
              densidade="compacta"
              acoes={
                <Button appearance="secondary" disabled={carregandoSugestoes} onClick={() => void pedirSugestoes()}>
                  {carregandoSugestoes ? 'Analisando…' : sugestoes ? 'Atualizar' : 'Gerar sugestões'}
                </Button>
              }
            >
              {!sugestoes ? (
                <EstadoVazio
                  titulo="Nenhuma sugestão gerada"
                  descricao="Veja ideias semelhantes, perguntas a responder antes da aprovação e um modelo de requisito."
                />
              ) : (
                <>
                  <h4>Ideias semelhantes</h4>
                  {sugestoes.ideiasSemelhantes.length === 0 ? (
                    <p>Nenhuma ideia semelhante encontrada.</p>
                  ) : (
                    sugestoes.ideiasSemelhantes.map((s) => (
                      <div key={s.id} style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center', marginBottom: 8 }}>
                        <span>
                          <strong>{s.codigo}</strong> — {s.titulo} <StatusChip tom={tomStatusIdeia[s.status] ?? 'neutro'}>{statusIdeiaLabel[s.status]}</StatusChip>
                        </span>
                        {editaAnalise && !atual.ideiaPrincipalId && (
                          <Button size="small" onClick={() => void vincular(s.id)}>Vincular a esta ideia</Button>
                        )}
                      </div>
                    ))
                  )}
                  <h4>Perguntas antes de aprovar</h4>
                  {sugestoes.perguntasAntesDeAprovar.length === 0 ? (
                    <p>Nenhuma pendência identificada.</p>
                  ) : (
                    <ul>{sugestoes.perguntasAntesDeAprovar.map((p) => <li key={p}>{p}</li>)}</ul>
                  )}
                  <h4>Requisito sugerido</h4>
                  <p>{sugestoes.requisitoSugerido}</p>
                  <h4>Critérios de aceite sugeridos</h4>
                  <ul>{sugestoes.criteriosAceiteSugeridos.map((c) => <li key={c}>{c}</li>)}</ul>
                  {STATUS_COM_REQUISITO.includes(atual.status) && (
                    <Button
                      size="small"
                      onClick={() => {
                        setReqTitulo(atual.titulo);
                        setReqDescricao(sugestoes.requisitoSugerido);
                        setReqCriterios(sugestoes.criteriosAceiteSugeridos.join('\n'));
                        setAba('requisitos');
                      }}
                    >
                      Usar como base do requisito
                    </Button>
                  )}
                  <p><small style={{ opacity: 0.7 }}>{sugestoes.aviso}</small></p>
                </>
              )}
            </Card>
          )}

          {atual.ideiasVinculadas.length > 0 && (
            <Card titulo="Ideias vinculadas" densidade="compacta">
              {atual.ideiasVinculadas.map((v) => (
                <div key={v.id}><strong>{v.codigo}</strong> — {v.titulo} ({v.registradoPorNome ?? '—'})</div>
              ))}
            </Card>
          )}
        </>
      )}

      {aba === 'discussao' && (
        <Card titulo="Discussão" subtitulo="Comentários, perguntas e decisões ficam vinculados à ideia" densidade="compacta">
          {atual.comentarios.length === 0 ? (
            <EstadoVazio titulo="Sem comentários" descricao="Seja o primeiro a comentar." />
          ) : (
            atual.comentarios.map((c) => (
              <div key={c.id} style={{ marginBottom: tokensUi.espaco.md }}>
                <strong>{c.autorNome}</strong> <small style={{ opacity: 0.7 }}>{formatarDataHora(c.createdAtUtc)}</small>
                <div style={{ whiteSpace: 'pre-wrap' }}>{c.texto}</div>
              </div>
            ))
          )}
          {podeUsar && (
            <>
              <Field label="Novo comentário">
                <Textarea value={comentario} rows={3} onChange={(_, d) => setComentario(d.value)} />
              </Field>
              <Button appearance="primary" disabled={!comentario.trim()} onClick={() => void comentar()}>Comentar</Button>
            </>
          )}
        </Card>
      )}

      {aba === 'requisitos' && (
        <>
          {atual.requisitos.length === 0 ? (
            <Card titulo="Requisitos e demandas" densidade="compacta">
              <EstadoVazio
                titulo="Nenhum requisito ainda"
                descricao="Quando a ideia for aprovada, ela pode ser transformada em requisito funcional e, depois, em demanda de desenvolvimento."
              />
            </Card>
          ) : (
            atual.requisitos.map((r) => (
              <Card
                key={r.id}
                titulo={r.titulo}
                subtitulo={`Requisito — ${statusRequisitoIdeiaLabel[r.status]}${r.aprovadoPorNome ? ` por ${r.aprovadoPorNome}` : ''}`}
                densidade="compacta"
                acoes={
                  <>
                    {r.status === 0 && podeDecidir && <Button onClick={() => void aprovarRequisito(r)}>Aprovar requisito</Button>}
                    {r.status === 1 && podeDecidir && (
                      <Button appearance="primary" onClick={() => void criarDemanda(r)}>Criar demanda de desenvolvimento</Button>
                    )}
                  </>
                }
              >
                <Texto rotulo="Descrição" valor={r.descricao} />
                <Texto rotulo="Critérios de aceite" valor={r.criteriosAceite} />
                {r.demandas.map((d) => (
                  <div key={d.id} style={{ borderTop: `1px solid ${tokensUi.bordaSuave}`, paddingTop: tokensUi.espaco.md, marginTop: tokensUi.espaco.md }}>
                    <strong>{d.codigo}</strong> — {d.titulo} <StatusChip tom="info">{statusDemandaLabel[d.status]}</StatusChip>
                    {podeDesenvolver ? (
                      <FormGrid>
                        <Campo span={3}>
                          <Field label="Status da demanda">
                            <Select value={String(d.status)} onChange={(_, x) => void atualizarDemanda(d.id, Number(x.value), d.responsavelNome, d.funcionalidadeEntregue)}>
                              {Object.entries(statusDemandaLabel).map(([v, rotulo]) => <option key={v} value={v}>{rotulo}</option>)}
                            </Select>
                          </Field>
                        </Campo>
                        <Campo span={3}>
                          <Field label="Responsável">
                            <Input
                              value={demandaResp[d.id] ?? d.responsavelNome ?? ''}
                              onChange={(_, x) => setDemandaResp({ ...demandaResp, [d.id]: x.value })}
                              onBlur={() => demandaResp[d.id] !== undefined && void atualizarDemanda(d.id, d.status, d.responsavelNome, d.funcionalidadeEntregue)}
                            />
                          </Field>
                        </Campo>
                        <Campo span={6}>
                          <Field label="Funcionalidade entregue">
                            <Input
                              value={demandaEntrega[d.id] ?? d.funcionalidadeEntregue ?? ''}
                              onChange={(_, x) => setDemandaEntrega({ ...demandaEntrega, [d.id]: x.value })}
                              onBlur={() => demandaEntrega[d.id] !== undefined && void atualizarDemanda(d.id, d.status, d.responsavelNome, d.funcionalidadeEntregue)}
                            />
                          </Field>
                        </Campo>
                      </FormGrid>
                    ) : (
                      <div>
                        Responsável: {d.responsavelNome ?? '—'} · Entregue: {d.funcionalidadeEntregue ?? '—'}
                      </div>
                    )}
                  </div>
                ))}
              </Card>
            ))
          )}

          {podeAnalisar && STATUS_COM_REQUISITO.includes(atual.status) && (
            <Card titulo="Novo requisito" subtitulo="Ideia → requisito → demanda → funcionalidade" densidade="compacta">
              <FormGrid>
                <Campo span={12}>
                  <Field label="Título" required>
                    <Input value={reqTitulo} onChange={(_, d) => setReqTitulo(d.value)} />
                  </Field>
                </Campo>
                <Campo span={12}>
                  <Field label="Requisito funcional" required hint='Ex.: "O sistema deverá analisar os registros de uma inspeção e sugerir ações corretivas."'>
                    <Textarea value={reqDescricao} rows={3} onChange={(_, d) => setReqDescricao(d.value)} />
                  </Field>
                </Campo>
                <Campo span={12}>
                  <Field label="Critérios de aceite" required hint="Um critério por linha">
                    <Textarea value={reqCriterios} rows={4} onChange={(_, d) => setReqCriterios(d.value)} />
                  </Field>
                </Campo>
              </FormGrid>
              <Button appearance="primary" onClick={() => void criarRequisito()}>Criar requisito</Button>
            </Card>
          )}
        </>
      )}

      {aba === 'anexos' && (
        <Card titulo="Anexos" subtitulo="Documentos, imagens, prints e fluxogramas — até 5 MB por arquivo" densidade="compacta">
          {atual.anexos.length === 0 ? (
            <EstadoVazio titulo="Nenhum anexo" />
          ) : (
            atual.anexos.map((a) => (
              <div key={a.id} style={{ display: 'flex', justifyContent: 'space-between', gap: 8, marginBottom: 8 }}>
                <span>
                  {a.nomeArquivo} <small style={{ opacity: 0.7 }}>({formatarTamanho(a.tamanho)} · {a.enviadoPorNome ?? '—'} · {formatarDataHora(a.createdAtUtc)})</small>
                </span>
                <Button size="small" onClick={() => void baixar(a.id, a.nomeArquivo)}>Baixar</Button>
              </div>
            ))
          )}
          {podeUsar && (
            <Field label="Adicionar arquivo">
              <input type="file" onChange={(e) => { void anexar(e.target.files?.[0]); e.target.value = ''; }} />
            </Field>
          )}
        </Card>
      )}

      {aba === 'historico' && (
        <Card titulo="Histórico" subtitulo="Registro permanente — nada é apagado" densidade="compacta">
          {atual.historico.map((h) => (
            <div key={h.id} style={{ marginBottom: tokensUi.espaco.md }}>
              <small style={{ opacity: 0.7 }}>{formatarDataHora(h.ocorridoEmUtc)} · {tipoHistoricoIdeiaLabel[h.tipo]}</small>
              <div>{h.descricao}</div>
            </div>
          ))}
        </Card>
      )}
    </DetailPageLayout>
  );
}
