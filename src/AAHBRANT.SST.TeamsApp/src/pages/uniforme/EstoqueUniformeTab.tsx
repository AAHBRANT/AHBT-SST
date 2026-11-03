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
  tipoMovimentacaoEstoqueUniformeLabel,
  type CatalogoUniforme,
  type EstoqueUniformePorObra,
  type MovimentacaoEstoqueUniforme,
  type Obra,
} from '../../lib/api';
import { useObraAtiva } from '../../lib/ObraAtivaContext';
import { formatarDataHoraBrasilia } from '../../components/assinatura/assinaturaDigital';

// Estoque de Uniforme — grade por Obra + Tamanho (mesmo princípio de segmentação por Obra do
// EstoqueTab.tsx do EPI, com uma dimensão a mais: o tamanho). Entrada é sempre manual (sem código
// de barras — decisão do brainstorming, 2026-09-07): escolhe a peça, digita o tamanho (cria um
// tamanho novo se ainda não existir nessa peça+obra) e a quantidade recebida.
// Camada ui/: saldo por peça+tamanho vira DataTable expansivel — expandir uma linha mostra o
// histórico de movimentações numa segunda DataTable aninhada (mesmo padrão de EstoqueTab.tsx do EPI),
// substituindo a Table crua com Fragment que a versão antiga usava para simular a mesma expansão.
export function EstoqueUniformeTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  // Obra = obra ativa do sistema (cabeçalho). Nunca se pega a "primeira da lista" sozinho; com "Todas as
  // obras" ativa, a tela fica em consulta e o usuário precisa escolher a obra para lançar.
  const { obraAtivaId: obraId, definirObraAtiva } = useObraAtiva();
  const { confirmar, dialogElement } = useConfirmar();
  const nomeObra = obras.find((o) => o.id === obraId)?.nome ?? '';
  function setObraId(id: string) {
    definirObraAtiva(id, obras.find((o) => o.id === id)?.nome ?? '');
  }
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [saldos, setSaldos] = useState<EstoqueUniformePorObra[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  const [linhaSelecionada, setLinhaSelecionada] = useState<{ catalogoUniformeId: string; tamanho: string } | null>(null);
  const [movimentacoes, setMovimentacoes] = useState<MovimentacaoEstoqueUniforme[]>([]);

  const [entradaCatalogoUniformeId, setEntradaCatalogoUniformeId] = useState('');
  const [entradaTamanho, setEntradaTamanho] = useState('');
  const [entradaQuantidade, setEntradaQuantidade] = useState('1');
  const [entradaObservacao, setEntradaObservacao] = useState('');

  const [ajusteCatalogoUniformeId, setAjusteCatalogoUniformeId] = useState('');
  const [ajusteTamanho, setAjusteTamanho] = useState('');
  const [ajusteNovoSaldo, setAjusteNovoSaldo] = useState('0');
  const [ajusteObservacao, setAjusteObservacao] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const [listaObras, listaItens] = await Promise.all([api.obras.listar(), api.catalogosUniforme.listar()]);
        setObras(listaObras);
        setItensCatalogo(listaItens);
        // Quem só tem uma obra não precisa escolher: ela vira a obra ativa.
        if (listaObras.length === 1) definirObraAtiva(listaObras[0].id, listaObras[0].nome);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar obras e catálogo de uniforme.');
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
      setSaldos(await api.estoquesUniforme.listarPorObra(obraId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar estoque da obra.');
    }
  }

  useEffect(() => {
    carregarSaldos();
    setLinhaSelecionada(null);
    setMovimentacoes([]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obraId]);

  async function alternarHistorico(saldo: EstoqueUniformePorObra) {
    if (linhaSelecionada?.catalogoUniformeId === saldo.catalogoUniformeId && linhaSelecionada.tamanho === saldo.tamanho) {
      setLinhaSelecionada(null);
      return;
    }
    try {
      setErro(null);
      setMovimentacoes(await api.estoquesUniforme.listarMovimentacoes(obraId, saldo.catalogoUniformeId, saldo.tamanho));
      setLinhaSelecionada({ catalogoUniformeId: saldo.catalogoUniformeId, tamanho: saldo.tamanho });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar histórico de movimentações.');
    }
  }

  async function registrarEntrada() {
    if (!obraId || !entradaCatalogoUniformeId || !entradaTamanho.trim()) return;
    const nomeItem = itensCatalogo.find((x) => x.id === entradaCatalogoUniformeId)?.nome ?? 'o uniforme selecionado';
    if (!(await confirmar({ titulo: 'Confirme a obra do lançamento', mensagem: `Registrar entrada de ${entradaQuantidade} un. de ${nomeItem} (tam. ${entradaTamanho.trim()}) na obra "${nomeObra}"?`, rotuloConfirmar: 'Confirmar e salvar', tom: 'neutro' }))) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.estoquesUniforme.registrarEntrada({
        obraId,
        catalogoUniformeId: entradaCatalogoUniformeId,
        tamanho: entradaTamanho.trim(),
        quantidade: Number(entradaQuantidade),
        observacao: entradaObservacao || null,
      });
      setEntradaCatalogoUniformeId('');
      setEntradaTamanho('');
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
    if (!obraId || !ajusteCatalogoUniformeId || !ajusteTamanho.trim() || !ajusteObservacao.trim()) return;
    const nomeItem = itensCatalogo.find((x) => x.id === ajusteCatalogoUniformeId)?.nome ?? 'o uniforme selecionado';
    if (!(await confirmar({ titulo: 'Confirme a obra do lançamento', mensagem: `Ajustar o saldo de ${nomeItem} (tam. ${ajusteTamanho.trim()}) na obra "${nomeObra}" para ${ajusteNovoSaldo} un.?`, rotuloConfirmar: 'Confirmar e salvar', tom: 'neutro' }))) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.estoquesUniforme.ajustar({
        obraId,
        catalogoUniformeId: ajusteCatalogoUniformeId,
        tamanho: ajusteTamanho.trim(),
        novoSaldo: Number(ajusteNovoSaldo),
        observacao: ajusteObservacao,
      });
      setAjusteCatalogoUniformeId('');
      setAjusteTamanho('');
      setAjusteNovoSaldo('0');
      setAjusteObservacao('');
      await carregarSaldos();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao ajustar estoque.');
    } finally {
      setCarregando(false);
    }
  }

  const colunasSaldo: Coluna<EstoqueUniformePorObra>[] = [
    { chave: 'peca', rotulo: 'Peça', render: (s) => s.catalogoUniformeNome },
    { chave: 'tamanho', rotulo: 'Tamanho' },
    { chave: 'saldo', rotulo: 'Saldo', alinhar: 'direita' },
  ];

  const colunasMovimentacoes: Coluna<MovimentacaoEstoqueUniforme>[] = [
    { chave: 'data', rotulo: 'Data', render: (m) => formatarDataHoraBrasilia(m.createdAtUtc) },
    { chave: 'tipo', rotulo: 'Tipo', render: (m) => tipoMovimentacaoEstoqueUniformeLabel[m.tipo] },
    { chave: 'quantidade', rotulo: 'Quantidade', alinhar: 'direita' },
    { chave: 'saldoResultante', rotulo: 'Saldo resultante', alinhar: 'direita' },
    { chave: 'observacao', rotulo: 'Observação' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader titulo="Estoque de Uniforme por Obra" />

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
                <Campo span={4}>
                  <Field label="Peça">
                    <Select value={entradaCatalogoUniformeId} onChange={(_, d) => setEntradaCatalogoUniformeId(d.value)}>
                      <option value="">Selecione</option>
                      {itensCatalogo.map((i) => (
                        <option key={i.id} value={i.id}>
                          {i.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label="Tamanho">
                    <Input value={entradaTamanho} onChange={(_, d) => setEntradaTamanho(d.value)} placeholder="ex.: M, 42..." />
                  </Field>
                </Campo>
                <Campo span={2}>
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
                  disabled={carregando || !entradaCatalogoUniformeId || !entradaTamanho.trim() || Number(entradaQuantidade) <= 0}
                >
                  Registrar entrada
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card titulo="Ajuste de saldo (correção de inventário)" densidade="compacta">
            <FormSection titulo="Dados do ajuste" numero={1} primeira>
              <FormGrid>
                <Campo span={4}>
                  <Field label="Peça">
                    <Select value={ajusteCatalogoUniformeId} onChange={(_, d) => setAjusteCatalogoUniformeId(d.value)}>
                      <option value="">Selecione</option>
                      {itensCatalogo.map((i) => (
                        <option key={i.id} value={i.id}>
                          {i.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label="Tamanho">
                    <Input value={ajusteTamanho} onChange={(_, d) => setAjusteTamanho(d.value)} placeholder="ex.: M, 42..." />
                  </Field>
                </Campo>
                <Campo span={2}>
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
                  disabled={
                    carregando ||
                    !ajusteCatalogoUniformeId ||
                    !ajusteTamanho.trim() ||
                    !ajusteObservacao.trim() ||
                    Number(ajusteNovoSaldo) < 0
                  }
                >
                  Ajustar saldo
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card titulo="Saldo atual" densidade="compacta">
            <DataTable
              aria-label="Saldo de uniforme por obra"
              densidade="compacta"
              colunas={colunasSaldo}
              linhas={saldos}
              chaveLinha={(s) => `${s.catalogoUniformeId}-${s.tamanho}`}
              vazio={{ titulo: 'Nenhum saldo registrado para esta obra.' }}
              aoClicarLinha={alternarHistorico}
              expansivel={{
                aberta: (s) =>
                  s.catalogoUniformeId === linhaSelecionada?.catalogoUniformeId && s.tamanho === linhaSelecionada.tamanho,
                render: (s) => (
                  <>
                    <Text weight="semibold">
                      Histórico de movimentações — {s.catalogoUniformeNome} ({s.tamanho})
                    </Text>
                    {movimentacoes.length === 0 ? (
                      <Legenda>Nenhuma movimentação registrada.</Legenda>
                    ) : (
                      <DataTable
                        aria-label={`Movimentações de ${s.catalogoUniformeNome} (${s.tamanho})`}
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
