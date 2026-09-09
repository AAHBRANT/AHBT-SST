import { useEffect, useState } from 'react';
import {
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
  type Coluna,
  type Tom,
} from '@ui';
import { AddCircle24Regular, Delete24Regular } from '@fluentui/react-icons';
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
};

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
      setExpandidoId(requisito.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar critérios do requisito.');
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

        <DataTable
          aria-label="Requisitos legais cadastrados"
          colunas={colunas}
          linhas={requisitos}
          chaveLinha={(r) => r.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum requisito legal cadastrado ainda.' }}
          aoClicarLinha={alternarExpansao}
          acoesLinha={(r) => (
            <Button appearance="subtle" icon={<Delete24Regular />} aria-label="Excluir" onClick={() => excluir(r.id)} />
          )}
          expansivel={{
            aberta: (r) => r.id === expandidoId,
            render: (r) => (
              <>
                <Text weight="semibold">Critérios de aplicabilidade — {r.norma}</Text>
                <Text size={200}>
                  Qualquer critério satisfeito já torna o requisito aplicável a uma obra (lógica "ou").
                </Text>

                {criterios.length === 0 ? (
                  <Text>Nenhum critério definido ainda.</Text>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    {criterios.map((c) => (
                      <div key={c.id} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <StatusChip tom="neutro">{tipoCriterioAplicabilidadeLabel[c.tipo]}</StatusChip>
                        <Text>{rotuloCriterio(c)}</Text>
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
                    <Field label="Perigo">
                      <Select
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
                  )}

                  {novoCriterio.tipo === TipoCriterioAplicabilidade.Funcao && (
                    <Field label="Função">
                      <Select
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
                  )}

                  {novoCriterio.tipo === TipoCriterioAplicabilidade.Equipamento && (
                    <Field label="Tipo de equipamento">
                      <Select
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
                  )}

                  {novoCriterio.tipo === TipoCriterioAplicabilidade.ItemQuestionario && (
                    <Field label="Item do questionário">
                      <Select
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
                  )}

                  <Button appearance="secondary" onClick={adicionarCriterio}>
                    Adicionar critério
                  </Button>
                </div>

                <div>
                  <Button appearance="primary" onClick={() => salvarCriterios(r.id)} disabled={processando}>
                    Salvar critérios
                  </Button>
                </div>
              </>
            ),
          }}
        />
      </Card>
    </>
  );
}
