import { useEffect, useState } from 'react';
import {
  BotaoAcao,
  Button,
  Campo,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Legenda,
  Select,
  StatusChip,
  Text,
  Textarea,
  useConfirmar,
  tokensUi,
  type Coluna,
  type Tom,
} from '@ui';
import { AddCircle24Regular, CheckmarkCircle24Regular, Delete24Regular, Edit24Regular } from '@fluentui/react-icons';
import {
  api,
  categoriaRequisitoLegalLabel,
  statusRequisitoLegalLabel,
  tipoCriterioAplicabilidadeLabel,
  tipoAtivoLabel,
  TipoCriterioAplicabilidade,
  StatusRequisitoLegal,
  type CriterioAplicabilidadeInput,
  type Funcao,
  type NovoRequisitoLegal,
  type Perigo,
  type RequisitoLegal,
  type RequisitoLegalCriterio,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function novoInicial(): NovoRequisitoLegal {
  return { norma: '', artigo: '', titulo: '', descricao: '', categoria: 1, fonte: '' };
}

function novoCriterioInicial(): CriterioAplicabilidadeInput {
  return { tipo: TipoCriterioAplicabilidade.Perigo, perigoId: '', funcaoId: '', tipoEquipamento: null, itemQuestionarioAplicabilidadeId: '' };
}

// Mapeamento 1:1 pelo nome semântico do Fluent (Guia de conversão item 5): success→ok, danger→alerta
// — só os dois status existentes hoje (Ativo/Revogado).
const tomPorStatusRequisito: Record<number, Tom> = {
  [StatusRequisitoLegal.Ativo]: 'ok',
  [StatusRequisitoLegal.Revogado]: 'alerta',
  [StatusRequisitoLegal.EmRevisao]: 'atencao',
};

// Ordem dos filtros por status (null = todos). "Em revisão" primeiro: é o trabalho pendente do QSMS.
const filtrosStatus: { valor: number | null; rotulo: string }[] = [
  { valor: null, rotulo: 'Todos' },
  { valor: StatusRequisitoLegal.EmRevisao, rotulo: 'Em revisão' },
  { valor: StatusRequisitoLegal.Ativo, rotulo: 'Ativos' },
  { valor: StatusRequisitoLegal.Revogado, rotulo: 'Revogados' },
];

// A fonte dos requisitos carregados das NRs é "descrição: https://...pdf" — separa o link para
// virar "abrir PDF oficial" em vez de uma URL enorme na tela.
function separarFonte(fonte: string | null | undefined): { texto: string | null; url: string | null } {
  if (!fonte) return { texto: null, url: null };
  const url = fonte.match(/https?:\/\/\S+/)?.[0] ?? null;
  const texto = (url ? fonte.replace(url, '') : fonte).replace(/[\s:·]+$/, '').trim();
  return { texto: texto || null, url };
}

const rotuloSecao = { textTransform: 'uppercase', letterSpacing: '0.04em' } as const;

// Seletores de critério: o nome de perigo mais longo não pode alargar a coluna (painel estreito do Teams).
const campoSelecaoLargo = { flex: '1 1 220px', minWidth: 0, maxWidth: '100%' } as const;

interface EdicaoTexto {
  titulo: string;
  descricao: string;
  categoria: number;
}

// Onda 2 Task 18 (camada ui/): lista de requisitos legais + cadastro de critérios de aplicabilidade
// por linha expandida — mesmo padrão de MatrizEpiTab.tsx (piloto 3): aoClicarLinha alterna a
// expansão (e busca os critérios do requisito), expansivel.render mostra o conteúdo. O "tipo de
// critério" (Perigo/Função/Equipamento/Questionário) era um Badge appearance="outline" — sem
// equivalente de "etiqueta de categoria" em @ui (só StatusChip, pensado para estado), então usa
// StatusChip tom="neutro" como rótulo neutro, mesmo julgamento de colapso de tom já registrado em
// outras tasks quando a união de casos não bate 1:1 com os 5 tons disponíveis.
export function RequisitosLegaisTab() {
  const [requisitos, setRequisitos] = useState<RequisitoLegal[]>([]);
  const [perigos, setPerigos] = useState<Perigo[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [itensQuestionario, setItensQuestionario] = useState<{ id: string; pergunta: string }[]>([]);
  const [novo, setNovo] = useState<NovoRequisitoLegal>(novoInicial());
  const [expandidoId, setExpandidoId] = useState<string | null>(null);
  const [criterios, setCriterios] = useState<RequisitoLegalCriterio[]>([]);
  const [novoCriterio, setNovoCriterio] = useState<CriterioAplicabilidadeInput>(novoCriterioInicial());
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [filtroStatus, setFiltroStatus] = useState<number | null>(null);
  const [filtroDefinido, setFiltroDefinido] = useState(false);
  const [edicao, setEdicao] = useState<EdicaoTexto | null>(null);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaRequisitos, listaPerigos, listaFuncoes, listaItens] = await Promise.all([
        api.requisitosLegais.listar(),
        api.perigos.listar(),
        api.funcoes.listar(),
        api.questionarioAplicabilidade.listarItens(),
      ]);
      setRequisitos(listaRequisitos);
      // Na primeira carga, abre direto no que falta validar (se houver).
      if (!filtroDefinido) {
        if (listaRequisitos.some((r) => r.status === StatusRequisitoLegal.EmRevisao)) {
          setFiltroStatus(StatusRequisitoLegal.EmRevisao);
        }
        setFiltroDefinido(true);
      }
      setPerigos(listaPerigos);
      setFuncoes(listaFuncoes);
      setItensQuestionario(listaItens);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar requisitos legais.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!novo.norma.trim() || !novo.titulo.trim() || !novo.descricao.trim()) {
      setErro('Informe ao menos Norma, Título e Descrição.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.requisitosLegais.criar({
        ...novo,
        artigo: novo.artigo || null,
        fonte: novo.fonte || null,
      });
      setNovo(novoInicial());
      await carregar();
      sucessoToast('Requisito legal criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar requisito legal.');
    } finally {
      setProcessando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este requisito legal? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.requisitosLegais.excluir(id);
      await carregar();
      sucessoToast('Requisito legal excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir requisito legal.');
    }
  }

  async function alternarExpansao(requisito: RequisitoLegal) {
    if (expandidoId === requisito.id) {
      setExpandidoId(null);
      return;
    }
    try {
      setErro(null);
      const detalhe = await api.requisitosLegais.obterDetalhe(requisito.id);
      setCriterios(detalhe.criterios);
      setNovoCriterio(novoCriterioInicial());
      setEdicao(null);
      setExpandidoId(requisito.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar critérios do requisito.');
    }
  }

  async function validar(requisito: RequisitoLegal) {
    try {
      setProcessando(true);
      setErro(null);
      await api.requisitosLegais.validar(requisito.id);
      setExpandidoId(null);
      await carregar();
      sucessoToast(`${requisito.norma} ${requisito.artigo ?? ''} ativado. Já vale para o plano de ação da IA.`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao validar o requisito legal.');
    } finally {
      setProcessando(false);
    }
  }

  async function salvarTexto(requisito: RequisitoLegal) {
    if (!edicao) return;
    if (!edicao.titulo.trim() || !edicao.descricao.trim()) {
      setErro('Título e texto do requisito não podem ficar vazios.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.requisitosLegais.atualizar(requisito.id, {
        norma: requisito.norma,
        artigo: requisito.artigo ?? null,
        titulo: edicao.titulo.trim(),
        descricao: edicao.descricao.trim(),
        categoria: edicao.categoria,
        status: requisito.status,
        fonte: requisito.fonte ?? null,
      });
      setEdicao(null);
      setExpandidoId(null);
      await carregar();
      sucessoToast('Texto do requisito atualizado.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar o texto do requisito.');
    } finally {
      setProcessando(false);
    }
  }

  function adicionarCriterio() {
    const base: RequisitoLegalCriterio = { ...novoCriterio, id: `novo-${Date.now()}` };
    setCriterios((atual) => [...atual, base]);
    setNovoCriterio(novoCriterioInicial());
  }

  function removerCriterio(id: string) {
    setCriterios((atual) => atual.filter((c) => c.id !== id));
  }

  async function salvarCriterios(requisitoId: string) {
    try {
      setProcessando(true);
      setErro(null);
      await api.requisitosLegais.definirCriterios(
        requisitoId,
        criterios.map((c) => ({
          tipo: c.tipo,
          perigoId: c.tipo === TipoCriterioAplicabilidade.Perigo ? c.perigoId : null,
          funcaoId: c.tipo === TipoCriterioAplicabilidade.Funcao ? c.funcaoId : null,
          tipoEquipamento: c.tipo === TipoCriterioAplicabilidade.Equipamento ? c.tipoEquipamento : null,
          itemQuestionarioAplicabilidadeId:
            c.tipo === TipoCriterioAplicabilidade.ItemQuestionario ? c.itemQuestionarioAplicabilidadeId : null,
        })),
      );
      setExpandidoId(null);
      sucessoToast('Critérios de aplicabilidade salvos com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar critérios de aplicabilidade.');
    } finally {
      setProcessando(false);
    }
  }

  function rotuloCriterio(c: RequisitoLegalCriterio): string {
    switch (c.tipo) {
      case TipoCriterioAplicabilidade.Perigo:
        return `Perigo: ${c.perigoNome ?? perigos.find((p) => p.id === c.perigoId)?.nome ?? c.perigoId}`;
      case TipoCriterioAplicabilidade.Funcao:
        return `Função: ${c.funcaoNome ?? funcoes.find((f) => f.id === c.funcaoId)?.nome ?? c.funcaoId}`;
      case TipoCriterioAplicabilidade.Equipamento:
        return `Equipamento: ${c.tipoEquipamento != null ? tipoAtivoLabel[c.tipoEquipamento] : '—'}`;
      case TipoCriterioAplicabilidade.ItemQuestionario:
        return `Questionário: ${c.itemQuestionarioPergunta ?? itensQuestionario.find((i) => i.id === c.itemQuestionarioAplicabilidadeId)?.pergunta ?? c.itemQuestionarioAplicabilidadeId}`;
      default:
        return '—';
    }
  }

  const colunas: Coluna<RequisitoLegal>[] = [
    { chave: 'norma', rotulo: 'Norma/Artigo', render: (r) => (r.artigo ? `${r.norma} — ${r.artigo}` : r.norma) },
    { chave: 'titulo', rotulo: 'Título' },
    { chave: 'categoria', rotulo: 'Categoria', render: (r) => categoriaRequisitoLegalLabel[r.categoria] },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (r) => (
        <StatusChip tom={tomPorStatusRequisito[r.status] ?? 'neutro'}>{statusRequisitoLegalLabel[r.status]}</StatusChip>
      ),
    },
  ];

  const contagem = (status: number | null) =>
    status == null ? requisitos.length : requisitos.filter((r) => r.status === status).length;
  const emRevisao = contagem(StatusRequisitoLegal.EmRevisao);
  const linhasFiltradas = filtroStatus == null ? requisitos : requisitos.filter((r) => r.status === filtroStatus);

  return (
    <>
      {dialogElement}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Legenda>
        Cadastro estruturado dos requisitos legais e de seus critérios de aplicabilidade. O conteúdo jurídico
        (norma, artigo, critério) deve ser validado por QSMS/jurídico antes de publicar — este cadastro não
        substitui essa validação.
      </Legenda>

      {emRevisao > 0 && (
        <FeedbackInline
          tom="aviso"
          acao={
            filtroStatus === StatusRequisitoLegal.EmRevisao
              ? undefined
              : { rotulo: 'Ver só os em revisão', aoClicar: () => setFiltroStatus(StatusRequisitoLegal.EmRevisao) }
          }
        >
          <strong>
            {emRevisao} {emRevisao === 1 ? 'requisito carregado' : 'requisitos carregados'} do texto oficial das NRs{' '}
            {emRevisao === 1 ? 'aguarda' : 'aguardam'} validação.
          </strong>{' '}
          Enquanto estiverem em revisão, não entram no plano de ação sugerido pela IA. Abra cada um, confira o texto e
          os perigos ligados e clique em <strong>Validar e ativar</strong>.
        </FeedbackInline>
      )}

      <Card titulo="Requisitos legais cadastrados">
        <FormSection titulo="Dados do Requisito Legal" numero={1} primeira>
          <FormGrid>
            <Campo span={2}>
              <Field label="Norma" required>
                <Input value={novo.norma} onChange={(_, d) => setNovo({ ...novo, norma: d.value })} placeholder="ex.: NR-35" />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Artigo">
                <Input value={novo.artigo ?? ''} onChange={(_, d) => setNovo({ ...novo, artigo: d.value })} placeholder="ex.: 35.4" />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Categoria">
                <Select value={String(novo.categoria)} onChange={(_, d) => setNovo({ ...novo, categoria: Number(d.value) })}>
                  {Object.entries(categoriaRequisitoLegalLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={5}>
              <Field label="Título" required>
                <Input value={novo.titulo} onChange={(_, d) => setNovo({ ...novo, titulo: d.value })} />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Fonte">
                <Input value={novo.fonte ?? ''} onChange={(_, d) => setNovo({ ...novo, fonte: d.value })} placeholder="link/referência" />
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Descrição" required>
                <Textarea value={novo.descricao} onChange={(_, d) => setNovo({ ...novo, descricao: d.value })} />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={processando}>
              Cadastrar requisito
            </Button>
          </FormRodape>
        </FormSection>

        <div
          role="group"
          aria-label="Filtrar por status"
          style={{ display: 'flex', gap: tokensUi.espaco.sm, flexWrap: 'wrap', alignItems: 'center' }}
        >
          <Text size={200} weight="semibold">
            Status
          </Text>
          {filtrosStatus.map((f) => (
            <Button
              key={f.rotulo}
              size="small"
              shape="circular"
              appearance={filtroStatus === f.valor ? 'primary' : 'outline'}
              aria-pressed={filtroStatus === f.valor}
              onClick={() => {
                setFiltroStatus(f.valor);
                setExpandidoId(null);
              }}
            >
              {f.rotulo} ({contagem(f.valor)})
            </Button>
          ))}
        </div>

        <DataTable
          aria-label="Requisitos legais cadastrados"
          colunas={colunas}
          linhas={linhasFiltradas}
          chaveLinha={(r) => r.id}
          carregando={carregandoLista}
          vazio={{
            titulo:
              requisitos.length === 0 ? 'Nenhum requisito legal cadastrado ainda.' : 'Nenhum requisito com este status.',
          }}
          aoClicarLinha={alternarExpansao}
          acoesLinha={(r) => (
            <BotaoAcao tom="excluir" icon={<Delete24Regular />} aria-label="Excluir" onClick={() => excluir(r.id)} />
          )}
          expansivel={{
            aberta: (r) => r.id === expandidoId,
            render: (r) => (
              <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg }}>
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 320px), 1fr))',
                    gap: tokensUi.espaco.xl,
                    alignItems: 'start',
                  }}
                >
                  <TextoDoRequisito
                    requisito={r}
                    edicao={edicao}
                    processando={processando}
                    aoEditar={setEdicao}
                    aoSalvar={() => salvarTexto(r)}
                  />

                  <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm, minWidth: 0 }}>
                <Text size={200} weight="semibold" style={rotuloSecao}>
                  Critérios de aplicabilidade
                </Text>
                <Text size={200}>
                  Qualquer critério satisfeito já torna o requisito aplicável a uma obra (lógica "ou").
                </Text>

                {criterios.length === 0 ? (
                  <Text>Nenhum critério definido ainda.</Text>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    {criterios.map((c) => (
                      <div key={c.id} style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                        <StatusChip tom="neutro">{tipoCriterioAplicabilidadeLabel[c.tipo]}</StatusChip>
                        <Text>{rotuloCriterio(c)}</Text>
                        {c.sugeridoPelaCarga && <StatusChip tom="info">sugerido pela carga</StatusChip>}
                        <Button appearance="subtle" size="small" onClick={() => removerCriterio(c.id)}>
                          Remover
                        </Button>
                      </div>
                    ))}
                  </div>
                )}

                <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end', flexWrap: 'wrap' }}>
                  <Field label="Tipo de critério">
                    <Select
                      value={String(novoCriterio.tipo)}
                      onChange={(_, d) => setNovoCriterio({ ...novoCriterio, tipo: Number(d.value) })}
                    >
                      {Object.entries(tipoCriterioAplicabilidadeLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>

                  {novoCriterio.tipo === TipoCriterioAplicabilidade.Perigo && (
                    <div style={campoSelecaoLargo}>
<Field label="Perigo">
                      <Select
                        style={{ width: '100%', minWidth: 0 }}
                        value={novoCriterio.perigoId ?? ''}
                        onChange={(_, d) => setNovoCriterio({ ...novoCriterio, perigoId: d.value })}
                      >
                        <option value="">Selecione</option>
                        {perigos.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
</div>
                  )}

                  {novoCriterio.tipo === TipoCriterioAplicabilidade.Funcao && (
                    <div style={campoSelecaoLargo}>
<Field label="Função">
                      <Select
                        style={{ width: '100%', minWidth: 0 }}
                        value={novoCriterio.funcaoId ?? ''}
                        onChange={(_, d) => setNovoCriterio({ ...novoCriterio, funcaoId: d.value })}
                      >
                        <option value="">Selecione</option>
                        {funcoes.map((f) => (
                          <option key={f.id} value={f.id}>
                            {f.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
</div>
                  )}

                  {novoCriterio.tipo === TipoCriterioAplicabilidade.Equipamento && (
                    <div style={campoSelecaoLargo}>
<Field label="Tipo de equipamento">
                      <Select
                        style={{ width: '100%', minWidth: 0 }}
                        value={novoCriterio.tipoEquipamento != null ? String(novoCriterio.tipoEquipamento) : ''}
                        onChange={(_, d) => setNovoCriterio({ ...novoCriterio, tipoEquipamento: Number(d.value) })}
                      >
                        <option value="">Selecione</option>
                        {Object.entries(tipoAtivoLabel).map(([valor, rotulo]) => (
                          <option key={valor} value={valor}>
                            {rotulo}
                          </option>
                        ))}
                      </Select>
                    </Field>
</div>
                  )}

                  {novoCriterio.tipo === TipoCriterioAplicabilidade.ItemQuestionario && (
                    <div style={campoSelecaoLargo}>
<Field label="Item do questionário">
                      <Select
                        style={{ width: '100%', minWidth: 0 }}
                        value={novoCriterio.itemQuestionarioAplicabilidadeId ?? ''}
                        onChange={(_, d) =>
                          setNovoCriterio({ ...novoCriterio, itemQuestionarioAplicabilidadeId: d.value })
                        }
                      >
                        <option value="">Selecione</option>
                        {itensQuestionario.map((i) => (
                          <option key={i.id} value={i.id}>
                            {i.pergunta}
                          </option>
                        ))}
                      </Select>
                    </Field>
</div>
                  )}

                  <Button appearance="secondary" onClick={adicionarCriterio}>
                    Adicionar critério
                  </Button>
                </div>

                <div>
                  <Button appearance="secondary" onClick={() => salvarCriterios(r.id)} disabled={processando}>
                    Salvar critérios
                  </Button>
                </div>
                  </div>
                </div>

                <div
                  style={{
                    display: 'flex',
                    gap: tokensUi.espaco.sm,
                    flexWrap: 'wrap',
                    alignItems: 'center',
                    paddingTop: tokensUi.espaco.md,
                    borderTop: `1px solid ${tokensUi.bordaSuave}`,
                  }}
                >
                  {r.status === StatusRequisitoLegal.EmRevisao && (
                    <Button
                      appearance="primary"
                      icon={<CheckmarkCircle24Regular />}
                      onClick={() => validar(r)}
                      disabled={processando || edicao !== null}
                    >
                      Validar e ativar
                    </Button>
                  )}
                  {!edicao && (
                    <Button
                      icon={<Edit24Regular />}
                      onClick={() => setEdicao({ titulo: r.titulo, descricao: r.descricao, categoria: r.categoria })}
                      disabled={processando}
                    >
                      Editar texto
                    </Button>
                  )}
                  {r.status === StatusRequisitoLegal.EmRevisao && (
                    <Text size={200} style={{ flex: '1 1 220px' }}>
                      Ao ativar, o requisito passa a valer para o plano de ação da IA nas atividades com o perigo
                      ligado. Fica registrado quem validou e quando.
                    </Text>
                  )}
                </div>
              </div>
            ),
          }}
        />
      </Card>
    </>
  );
}

// Coluna esquerda da linha expandida: o texto do requisito (literal da NR quando veio da carga
// oficial), a fonte com link para o PDF e quem validou — ou o formulário de edição do texto.
function TextoDoRequisito({
  requisito,
  edicao,
  processando,
  aoEditar,
  aoSalvar,
}: {
  requisito: RequisitoLegal;
  edicao: EdicaoTexto | null;
  processando: boolean;
  aoEditar: (edicao: EdicaoTexto | null) => void;
  aoSalvar: () => void;
}) {
  const fonte = separarFonte(requisito.fonte);
  const oficial = (fonte.url ?? '').includes('gov.br');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm, minWidth: 0 }}>
      <Text size={200} weight="semibold" style={rotuloSecao}>
        {oficial ? 'Texto oficial da norma' : 'Texto do requisito'}
      </Text>

      {edicao ? (
        <>
          <Field label="Título" required>
            <Input value={edicao.titulo} onChange={(_, d) => aoEditar({ ...edicao, titulo: d.value })} />
          </Field>
          <Field label="Categoria">
            <Select
              value={String(edicao.categoria)}
              onChange={(_, d) => aoEditar({ ...edicao, categoria: Number(d.value) })}
            >
              {Object.entries(categoriaRequisitoLegalLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Texto" required>
            <Textarea
              resize="vertical"
              rows={8}
              value={edicao.descricao}
              onChange={(_, d) => aoEditar({ ...edicao, descricao: d.value })}
            />
          </Field>
          <div style={{ display: 'flex', gap: tokensUi.espaco.sm, flexWrap: 'wrap' }}>
            <Button appearance="primary" onClick={aoSalvar} disabled={processando}>
              Salvar texto
            </Button>
            <Button onClick={() => aoEditar(null)} disabled={processando}>
              Cancelar
            </Button>
          </div>
        </>
      ) : (
        <blockquote
          style={{
            margin: 0,
            paddingLeft: tokensUi.espaco.md,
            borderLeft: `3px solid ${tokensUi.bordaSuave}`,
            maxWidth: '72ch',
          }}
        >
          <Text>{requisito.descricao}</Text>
        </blockquote>
      )}

      {(fonte.texto || fonte.url) && (
        <Text size={200} style={{ overflowWrap: 'anywhere' }}>
          Fonte: {fonte.texto}
          {fonte.url && (
            <>
              {fonte.texto ? ' · ' : ''}
              <a href={fonte.url} target="_blank" rel="noopener noreferrer">
                {oficial ? 'abrir PDF oficial' : 'abrir fonte'}
              </a>
            </>
          )}
        </Text>
      )}

      {requisito.validadoEmUtc && (
        <Text size={200}>
          Validado{requisito.validadoPorNome ? ` por ${requisito.validadoPorNome}` : ''} em{' '}
          {new Date(requisito.validadoEmUtc).toLocaleDateString('pt-BR')}.
        </Text>
      )}
    </div>
  );
}
