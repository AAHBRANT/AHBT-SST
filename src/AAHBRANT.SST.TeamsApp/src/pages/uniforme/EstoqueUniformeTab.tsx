import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  DataTable,
  Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
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
  designTokens,
  useConfirmar,
} from '@ui';
import { ZoomIn24Regular } from '@fluentui/react-icons';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';
import { referenciaUniforme } from './referenciasUniforme';
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
  const [fotoAmpliada, setFotoAmpliada] = useState<{ item: CatalogoUniforme; url: string; revogar: boolean } | null>(null);

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

  // Peça com modelo visual AAHBRANT amplia a foto de referência; peça com foto própria baixa o original.
  async function ampliar(item: CatalogoUniforme) {
    const referencia = referenciaUniforme(item.referenciaVisual);
    if (referencia) {
      setFotoAmpliada({ item, url: referencia.frente, revogar: false });
      return;
    }
    if (!item.temFoto) return;
    try {
      const blob = await api.catalogosUniforme.baixarFoto(item.id);
      setFotoAmpliada({ item, url: URL.createObjectURL(blob), revogar: true });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a foto original.');
    }
  }

  function fecharFotoAmpliada() {
    if (fotoAmpliada?.revogar) URL.revokeObjectURL(fotoAmpliada.url);
    setFotoAmpliada(null);
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

  const colunasMovimentacoes: Coluna<MovimentacaoEstoqueUniforme>[] = [
    { chave: 'data', rotulo: 'Data', render: (m) => formatarDataHoraBrasilia(m.createdAtUtc) },
    { chave: 'tipo', rotulo: 'Tipo', render: (m) => tipoMovimentacaoEstoqueUniformeLabel[m.tipo] },
    { chave: 'quantidade', rotulo: 'Quantidade', alinhar: 'direita' },
    { chave: 'saldoResultante', rotulo: 'Saldo resultante', alinhar: 'direita' },
    { chave: 'observacao', rotulo: 'Observação' },
  ];
  const saldosPorPeca = itensCatalogo.map((item) => ({
    item,
    tamanhos: saldos.filter((saldo) => saldo.catalogoUniformeId === item.id && saldo.tamanho),
  }));

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
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 16 }}>
              {saldosPorPeca.map(({ item, tamanhos }) => {
                const saldoTotal = tamanhos.reduce((total, s) => total + s.saldo, 0);
                const referencia = referenciaUniforme(item.referenciaVisual);
                return (
                  <div key={item.id} style={{ border: `1px solid ${designTokens.colorCardBorder}`, borderRadius: 12, overflow: 'hidden', background: '#fff' }}>
                    <div style={{ minHeight: 200, background: designTokens.colorNeutralLight, display: 'grid', placeItems: 'center', position: 'relative' }}>
                      <FotoCatalogoUniforme catalogoUniformeId={item.id} temFoto={item.temFoto} referenciaVisual={item.referenciaVisual} destaque />
                      {(item.temFoto || referencia) && (
                        <Button icon={<ZoomIn24Regular />} appearance="primary" onClick={() => ampliar(item)} style={{ position: 'absolute', right: 10, bottom: 10 }}>
                          Ampliar
                        </Button>
                      )}
                    </div>
                    <div style={{ padding: 15 }}>
                      <div style={{ fontWeight: 800 }}>{item.nome}</div>
                      <div style={{ color: designTokens.colorNeutralMedium, fontSize: 12, margin: '4px 0 12px' }}>
                        {item.categoria || 'Sem categoria'} · {referencia?.cor ?? 'Foto própria'}
                      </div>
                      {saldoTotal === 0 && (
                        <div style={{ color: '#8b5e00', background: '#fff7df', border: '1px solid #f0dfa9', borderRadius: 99, padding: '3px 8px', display: 'inline-block', fontSize: 11, fontWeight: 700, marginBottom: 10 }}>
                          Sem estoque · Saldo 0
                        </div>
                      )}
                      <div style={{ borderTop: `1px solid ${designTokens.colorCardBorder}`, paddingTop: 12 }}>
                        <div style={{ fontWeight: 800, fontSize: 12, color: designTokens.colorNeutralMedium, marginBottom: 8 }}>Tamanho · saldo na obra</div>
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                          {tamanhos.length === 0 && <Legenda>Nenhum tamanho com entrada nesta obra.</Legenda>}
                          {tamanhos.map((s) => {
                            const aberto = linhaSelecionada?.catalogoUniformeId === item.id && linhaSelecionada.tamanho === s.tamanho;
                            return (
                              <button
                                key={s.tamanho}
                                type="button"
                                aria-pressed={aberto}
                                aria-label={`Tamanho ${s.tamanho}, saldo ${s.saldo}. Ver histórico`}
                                onClick={() => alternarHistorico(s)}
                                style={{
                                  minWidth: 48,
                                  border: `1px solid ${aberto ? designTokens.colorPrimary : designTokens.colorCardBorder}`,
                                  borderRadius: 7,
                                  background: '#fff',
                                  padding: '5px 7px',
                                  textAlign: 'center',
                                  cursor: 'pointer',
                                }}
                              >
                                <b>{s.tamanho}</b>
                                <small style={{ display: 'block', color: designTokens.colorNeutralMedium }}>{s.saldo}</small>
                              </button>
                            );
                          })}
                        </div>
                      </div>
                      {linhaSelecionada?.catalogoUniformeId === item.id && (
                        <div style={{ marginTop: 12 }}>
                          <Text weight="semibold">Histórico de movimentações ({linhaSelecionada.tamanho})</Text>
                          {movimentacoes.length ? (
                            <DataTable
                              aria-label={`Movimentações de ${item.nome} (${linhaSelecionada.tamanho})`}
                              densidade="compacta"
                              colunas={colunasMovimentacoes}
                              linhas={movimentacoes}
                              chaveLinha={(m) => m.id}
                            />
                          ) : (
                            <Legenda>Nenhuma movimentação registrada.</Legenda>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
            {saldosPorPeca.length === 0 && <Legenda>Nenhum item do catálogo cadastrado.</Legenda>}
          </Card>
        </>
      )}
      <Dialog open={Boolean(fotoAmpliada)} onOpenChange={(_, d) => { if (!d.open) fecharFotoAmpliada(); }}>
        <DialogSurface style={{ maxWidth: 900, width: '96vw' }}>
          <DialogBody>
            <DialogTitle>{fotoAmpliada?.item.nome}</DialogTitle>
            <DialogContent>
              {fotoAmpliada && (
                <img src={fotoAmpliada.url} alt={`${fotoAmpliada.item.nome} — imagem ampliada`} style={{ width: '100%', maxHeight: '75vh', objectFit: 'contain' }} />
              )}
            </DialogContent>
            <DialogActions>
              <Button onClick={fecharFotoAmpliada}>Fechar</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
