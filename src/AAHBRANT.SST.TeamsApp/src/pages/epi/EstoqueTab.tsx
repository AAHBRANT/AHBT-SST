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
} from '@ui';
import {
  api,
  tipoMovimentacaoEstoqueEpiLabel,
  type CatalogoEpi,
  type EstoqueEpiPorObra,
  type MovimentacaoEstoqueEpi,
  type Obra,
} from '../../lib/api';

// Estoque de EPI segmentado por Obra (Fase 3 da reformulação do módulo EPI) — substitui o antigo
// saldo único global do catálogo. Entradas/saídas por entrega e devolução são automáticas (ver
// CriarEntregaEpiCommand/AtualizarEntregaEpiCommand no backend); aqui só entrada manual (reposição)
// e ajuste (correção de inventário, com observação obrigatória) precisam de tela.
// Onda 2 Task 19 (camada ui/): saldo por EPI vira DataTable expansivel — expandir uma linha mostra o
// histórico de movimentações numa segunda DataTable aninhada (mesmo padrão de AprEtapasTab.tsx),
// substituindo a Table crua com Fragment que a versão antiga usava para simular a mesma expansão.
export function EstoqueTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [saldos, setSaldos] = useState<EstoqueEpiPorObra[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  const [catalogoEpiIdSelecionado, setCatalogoEpiIdSelecionado] = useState<string | null>(null);
  const [movimentacoes, setMovimentacoes] = useState<MovimentacaoEstoqueEpi[]>([]);

  const [entradaCatalogoEpiId, setEntradaCatalogoEpiId] = useState('');
  const [entradaQuantidade, setEntradaQuantidade] = useState('1');
  const [entradaObservacao, setEntradaObservacao] = useState('');

  const [ajusteCatalogoEpiId, setAjusteCatalogoEpiId] = useState('');
  const [ajusteNovoSaldo, setAjusteNovoSaldo] = useState('0');
  const [ajusteObservacao, setAjusteObservacao] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const [listaObras, listaEpis] = await Promise.all([api.obras.listar(), api.catalogosEpi.listar()]);
        setObras(listaObras);
        setEpis(listaEpis);
        if (listaObras.length > 0) setObraId(listaObras[0].id);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar obras e catálogo de EPI.');
      }
    })();
  }, []);

  async function carregarSaldos() {
    if (!obraId) return;
    try {
      setErro(null);
      setSaldos(await api.estoquesEpi.listarPorObra(obraId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar estoque da obra.');
    }
  }

  useEffect(() => {
    carregarSaldos();
    setCatalogoEpiIdSelecionado(null);
    setMovimentacoes([]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obraId]);

  async function alternarHistorico(saldo: EstoqueEpiPorObra) {
    if (catalogoEpiIdSelecionado === saldo.catalogoEpiId) {
      setCatalogoEpiIdSelecionado(null);
      return;
    }
    try {
      setErro(null);
      setMovimentacoes(await api.estoquesEpi.listarMovimentacoes(obraId, saldo.catalogoEpiId));
      setCatalogoEpiIdSelecionado(saldo.catalogoEpiId);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar histórico de movimentações.');
    }
  }

  async function registrarEntrada() {
    if (!obraId || !entradaCatalogoEpiId) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.estoquesEpi.registrarEntrada({
        obraId,
        catalogoEpiId: entradaCatalogoEpiId,
        quantidade: Number(entradaQuantidade),
        observacao: entradaObservacao || null,
      });
      setEntradaCatalogoEpiId('');
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
    if (!obraId || !ajusteCatalogoEpiId || !ajusteObservacao.trim()) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.estoquesEpi.ajustar({
        obraId,
        catalogoEpiId: ajusteCatalogoEpiId,
        novoSaldo: Number(ajusteNovoSaldo),
        observacao: ajusteObservacao,
      });
      setAjusteCatalogoEpiId('');
      setAjusteNovoSaldo('0');
      setAjusteObservacao('');
      await carregarSaldos();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao ajustar estoque.');
    } finally {
      setCarregando(false);
    }
  }

  const colunasSaldo: Coluna<EstoqueEpiPorObra>[] = [
    { chave: 'nome', rotulo: 'Nome', render: (s) => s.catalogoEpiNome },
    { chave: 'fabricante', rotulo: 'Fabricante' },
    { chave: 'saldo', rotulo: 'Saldo', alinhar: 'direita' },
  ];

  const colunasMovimentacoes: Coluna<MovimentacaoEstoqueEpi>[] = [
    { chave: 'data', rotulo: 'Data', render: (m) => new Date(m.createdAtUtc).toLocaleString('pt-BR') },
    { chave: 'tipo', rotulo: 'Tipo', render: (m) => tipoMovimentacaoEstoqueEpiLabel[m.tipo] },
    { chave: 'quantidade', rotulo: 'Quantidade', alinhar: 'direita' },
    { chave: 'saldoResultante', rotulo: 'Saldo resultante', alinhar: 'direita' },
    { chave: 'observacao', rotulo: 'Observação' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader titulo="Estoque de EPI por Obra" />

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
                  <Field label="EPI">
                    <Select value={entradaCatalogoEpiId} onChange={(_, d) => setEntradaCatalogoEpiId(d.value)}>
                      <option value="">Selecione</option>
                      {epis.map((e) => (
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
                  disabled={carregando || !entradaCatalogoEpiId || Number(entradaQuantidade) <= 0}
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
                  <Field label="EPI">
                    <Select value={ajusteCatalogoEpiId} onChange={(_, d) => setAjusteCatalogoEpiId(d.value)}>
                      <option value="">Selecione</option>
                      {epis.map((e) => (
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
                  disabled={carregando || !ajusteCatalogoEpiId || !ajusteObservacao.trim() || Number(ajusteNovoSaldo) < 0}
                >
                  Ajustar saldo
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card titulo="Saldo atual" densidade="compacta">
            <DataTable
              aria-label="Saldo de EPI por obra"
              densidade="compacta"
              colunas={colunasSaldo}
              linhas={saldos}
              chaveLinha={(s) => s.catalogoEpiId}
              vazio={{ titulo: 'Nenhum saldo registrado para esta obra.' }}
              aoClicarLinha={alternarHistorico}
              expansivel={{
                aberta: (s) => s.catalogoEpiId === catalogoEpiIdSelecionado,
                render: (s) => (
                  <>
                    <Text weight="semibold">Histórico de movimentações — {s.catalogoEpiNome}</Text>
                    {movimentacoes.length === 0 ? (
                      <Legenda>Nenhuma movimentação registrada.</Legenda>
                    ) : (
                      <DataTable
                        aria-label={`Movimentações de ${s.catalogoEpiNome}`}
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
