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
  PageHeader,
  Select,
  Text,
  Textarea,
  type Coluna,
  useConfirmar,
} from '@ui';
import {
  api,
  tipoMovimentacaoEstoqueEpcLabel,
  type CatalogoEpc,
  type EstoqueEpcPorObra,
  type MovimentacaoEstoqueEpc,
  type Obra,
} from '../../lib/api';
import { useObraAtiva } from '../../lib/ObraAtivaContext';
import { formatarDataHoraBrasilia } from '../../components/assinatura/assinaturaDigital';

// Estoque de EPC segmentado por Obra — mesma estrutura do estoque de EPI (Fase 3). Entradas/saídas
// por instalação e remoção são automáticas (ver CriarInstalacaoEpcCommand/RegistrarRemocaoEpcCommand
// no backend); aqui só entrada manual (reposição) e ajuste (correção de inventário) precisam de tela.
// Onda 3 Task 22.5 (camada ui/): mesmo padrão de EstoqueTab.tsx (EPI, Onda 2 Task 19) — saldo por EPC
// vira DataTable expansível (expandir uma linha mostra o histórico de movimentações numa segunda
// DataTable aninhada), substituindo a Table crua com Fragment que a versão antiga usava.
export function EstoqueEpcTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  // Obra = obra ativa do sistema (cabeçalho). Nunca se pega a "primeira da lista" sozinho; com "Todas as
  // obras" ativa, a tela fica em consulta e o usuário precisa escolher a obra para lançar.
  const { obraAtivaId: obraId, definirObraAtiva } = useObraAtiva();
  const { confirmar, dialogElement } = useConfirmar();
  const nomeObra = obras.find((o) => o.id === obraId)?.nome ?? '';
  function setObraId(id: string) {
    definirObraAtiva(id, obras.find((o) => o.id === id)?.nome ?? '');
  }
  const [epcs, setEpcs] = useState<CatalogoEpc[]>([]);
  const [saldos, setSaldos] = useState<EstoqueEpcPorObra[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  const [catalogoEpcIdSelecionado, setCatalogoEpcIdSelecionado] = useState<string | null>(null);
  const [movimentacoes, setMovimentacoes] = useState<MovimentacaoEstoqueEpc[]>([]);

  const [entradaCatalogoEpcId, setEntradaCatalogoEpcId] = useState('');
  const [entradaQuantidade, setEntradaQuantidade] = useState('1');
  const [entradaObservacao, setEntradaObservacao] = useState('');

  const [ajusteCatalogoEpcId, setAjusteCatalogoEpcId] = useState('');
  const [ajusteNovoSaldo, setAjusteNovoSaldo] = useState('0');
  const [ajusteObservacao, setAjusteObservacao] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const [listaObras, listaEpcs] = await Promise.all([api.obras.listar(), api.catalogosEpc.listar()]);
        setObras(listaObras);
        setEpcs(listaEpcs);
        // Quem só tem uma obra não precisa escolher: ela vira a obra ativa.
        if (listaObras.length === 1) definirObraAtiva(listaObras[0].id, listaObras[0].nome);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar obras e catálogo de EPC.');
      }
    })();
  }, [definirObraAtiva]);

  async function carregarSaldos() {
    if (!obraId) {
      setSaldos([]);
      return;
    }
    try {
      setErro(null);
      setSaldos(await api.estoquesEpc.listarPorObra(obraId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar estoque da obra.');
    }
  }

  useEffect(() => {
    carregarSaldos();
    setCatalogoEpcIdSelecionado(null);
    setMovimentacoes([]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obraId]);

  async function alternarHistorico(saldo: EstoqueEpcPorObra) {
    if (catalogoEpcIdSelecionado === saldo.catalogoEpcId) {
      setCatalogoEpcIdSelecionado(null);
      return;
    }
    try {
      setErro(null);
      setMovimentacoes(await api.estoquesEpc.listarMovimentacoes(obraId, saldo.catalogoEpcId));
      setCatalogoEpcIdSelecionado(saldo.catalogoEpcId);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar histórico de movimentações.');
    }
  }

  async function registrarEntrada() {
    if (!obraId || !entradaCatalogoEpcId) return;
    const nomeEpc = epcs.find((x) => x.id === entradaCatalogoEpcId)?.nome ?? 'o EPC selecionado';
    if (!(await confirmar({ titulo: 'Confirme a obra do lançamento', mensagem: `Registrar entrada de ${entradaQuantidade} un. de ${nomeEpc} na obra "${nomeObra}"?`, rotuloConfirmar: 'Confirmar e salvar', tom: 'neutro' }))) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.estoquesEpc.registrarEntrada({
        obraId,
        catalogoEpcId: entradaCatalogoEpcId,
        quantidade: Number(entradaQuantidade),
        observacao: entradaObservacao || null,
      });
      setEntradaCatalogoEpcId('');
      setEntradaQuantidade('1');
      setEntradaObservacao('');
      await carregarSaldos();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar entrada de estoque.');
    } finally {
      setCarregando(false);
    }
  }

  async function ajustarSaldo() {
    if (!obraId || !ajusteCatalogoEpcId || !ajusteObservacao.trim()) return;
    const nomeEpc = epcs.find((x) => x.id === ajusteCatalogoEpcId)?.nome ?? 'o EPC selecionado';
    if (!(await confirmar({ titulo: 'Confirme a obra do lançamento', mensagem: `Ajustar o saldo de ${nomeEpc} na obra "${nomeObra}" para ${ajusteNovoSaldo} un.?`, rotuloConfirmar: 'Confirmar e salvar', tom: 'neutro' }))) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.estoquesEpc.ajustar({
        obraId,
        catalogoEpcId: ajusteCatalogoEpcId,
        novoSaldo: Number(ajusteNovoSaldo),
        observacao: ajusteObservacao,
      });
      setAjusteCatalogoEpcId('');
      setAjusteNovoSaldo('0');
      setAjusteObservacao('');
      await carregarSaldos();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao ajustar estoque.');
    } finally {
      setCarregando(false);
    }
  }

  const colunasSaldo: Coluna<EstoqueEpcPorObra>[] = [
    { chave: 'nome', rotulo: 'Nome', render: (s) => s.catalogoEpcNome },
    { chave: 'fabricante', rotulo: 'Fabricante' },
    { chave: 'saldo', rotulo: 'Saldo', alinhar: 'direita' },
  ];

  const colunasMovimentacoes: Coluna<MovimentacaoEstoqueEpc>[] = [
    { chave: 'data', rotulo: 'Data', render: (m) => formatarDataHoraBrasilia(m.createdAtUtc) },
    { chave: 'tipo', rotulo: 'Tipo', render: (m) => tipoMovimentacaoEstoqueEpcLabel[m.tipo] },
    { chave: 'quantidade', rotulo: 'Quantidade', alinhar: 'direita' },
    { chave: 'saldoResultante', rotulo: 'Saldo resultante', alinhar: 'direita' },
    { chave: 'observacao', rotulo: 'Observação' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader titulo="Estoque de EPC por Obra" />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card densidade="compacta">
        <FormSection titulo="Obra" numero={1} primeira>
          <FormGrid>
            <Campo span={6}>
              <Field label="Obra">
                <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
                  <option value="">Selecione</option>
                  {obras.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
      </Card>

      {obraId && (
        <>
          <Card titulo="Entrada manual (reposição)" densidade="compacta">
            <FormSection titulo="Dados da entrada" numero={1} primeira>
              <FormGrid>
                <Campo span={5}>
                  <Field label="EPC">
                    <Select value={entradaCatalogoEpcId} onChange={(_, d) => setEntradaCatalogoEpcId(d.value)}>
                      <option value="">Selecione</option>
                      {epcs.map((e) => (
                        <option key={e.id} value={e.id}>
                          {e.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Quantidade">
                    <Input type="number" value={entradaQuantidade} onChange={(_, d) => setEntradaQuantidade(d.value)} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Observação (opcional)">
                    <Input value={entradaObservacao} onChange={(_, d) => setEntradaObservacao(d.value)} />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button
                  appearance="primary"
                  onClick={registrarEntrada}
                  disabled={carregando || !entradaCatalogoEpcId || Number(entradaQuantidade) <= 0}
                >
                  Registrar entrada
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card titulo="Ajuste de saldo (correção de inventário)" densidade="compacta">
            <FormSection titulo="Dados do ajuste" numero={1} primeira>
              <FormGrid>
                <Campo span={5}>
                  <Field label="EPC">
                    <Select value={ajusteCatalogoEpcId} onChange={(_, d) => setAjusteCatalogoEpcId(d.value)}>
                      <option value="">Selecione</option>
                      {epcs.map((e) => (
                        <option key={e.id} value={e.id}>
                          {e.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Novo saldo">
                    <Input type="number" value={ajusteNovoSaldo} onChange={(_, d) => setAjusteNovoSaldo(d.value)} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Observação (obrigatória)">
                    <Textarea value={ajusteObservacao} onChange={(_, d) => setAjusteObservacao(d.value)} />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button
                  appearance="primary"
                  onClick={ajustarSaldo}
                  disabled={carregando || !ajusteCatalogoEpcId || !ajusteObservacao.trim() || Number(ajusteNovoSaldo) < 0}
                >
                  Ajustar saldo
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card titulo="Saldo atual" densidade="compacta">
            <DataTable
              aria-label="Saldo de EPC por obra"
              densidade="compacta"
              colunas={colunasSaldo}
              linhas={saldos}
              chaveLinha={(s) => s.catalogoEpcId}
              vazio={{ titulo: 'Nenhum saldo registrado para esta obra.' }}
              aoClicarLinha={alternarHistorico}
              expansivel={{
                aberta: (s) => s.catalogoEpcId === catalogoEpcIdSelecionado,
                render: (s) => (
                  <>
                    <Text weight="semibold">Histórico de movimentações — {s.catalogoEpcNome}</Text>
                    {movimentacoes.length === 0 ? (
                      <Legenda>Nenhuma movimentação registrada.</Legenda>
                    ) : (
                      <DataTable
                        aria-label={`Movimentações de ${s.catalogoEpcNome}`}
                        densidade="compacta"
                        colunas={colunasMovimentacoes}
                        linhas={movimentacoes}
                        chaveLinha={(m) => m.id}
                      />
                    )}
                  </>
                ),
              }}
            />
          </Card>
        </>
      )}
    </div>
  );
}
