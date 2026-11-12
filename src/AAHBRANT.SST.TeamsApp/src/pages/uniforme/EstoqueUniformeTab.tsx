import { useEffect, useRef, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  Carregando,
  Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
  Field,
  FeedbackInline,
  FormGrid,
  FormSection,
  Legenda,
  PageHeader,
  Select,
  designTokens,
  tokensUi,
} from '@ui';
import { Add24Regular, Dismiss24Regular, ZoomIn24Regular } from '@fluentui/react-icons';
import { mergeClasses, useEstoqueStyles } from './estilosUniforme';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';
import { LancamentoEstoqueDialog, type DadosLancamento } from './LancamentoEstoqueDialog';
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
// de barras — decisão do brainstorming, 2026-09-07).
// Mockup aprovado (2026-11-11): docs/mockups/2026-11-11-uniforme-melhorias-v1.html, Seção 2. Tudo parte
// do card da peça: "Registrar entrada" (qualquer tamanho, inclusive novo); tocar num tamanho apenas o
// seleciona e mostra Entrada / Ajustar saldo / Histórico daquele tamanho. Entrada e ajuste abrem em
// diálogo (folha na base no celular); o histórico abre em painel de largura total abaixo da grade.

type Selecao = { catalogoUniformeId: string; tamanho: string };

// Cor da pílula do tipo de movimentação no histórico (mockup: entrada verde, saída azul, ajuste âmbar).
function pilulaDoTipo(tipo: number) {
  if (tipo === 0 || tipo === 4) return tokensUi.status.ok;
  if (tipo === 1) return tokensUi.status.info;
  if (tipo === 2) return tokensUi.status.atencao;
  return tokensUi.status.neutro;
}

export function EstoqueUniformeTab() {
  const e = useEstoqueStyles();
  const [obras, setObras] = useState<Obra[]>([]);
  // Obra = obra ativa do sistema (cabeçalho). Nunca se pega a "primeira da lista" sozinho; com "Todas as
  // obras" ativa, a tela fica em consulta e o usuário precisa escolher a obra para lançar.
  const { obraAtivaId: obraId, definirObraAtiva } = useObraAtiva();
  const nomeObra = obras.find((o) => o.id === obraId)?.nome ?? '';
  function setObraId(id: string) {
    definirObraAtiva(id, obras.find((o) => o.id === id)?.nome ?? '');
  }
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [saldos, setSaldos] = useState<EstoqueUniformePorObra[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  // Estado da carga de saldos da obra atual: enquanto carrega (ou se falhou) a tela NÃO pode afirmar
  // "Sem estoque" nem "Nenhum tamanho", e os botões de lançamento ficam desabilitados.
  const [estadoSaldos, setEstadoSaldos] = useState<'carregando' | 'ok' | 'erro'>('carregando');
  const pedidoSaldos = useRef(0);
  const pedidoHistorico = useRef(0);
  const obraAtualRef = useRef(obraId);
  const origemLancamento = useRef<HTMLElement | null>(null);

  // Tamanho apenas selecionado (mostra a faixa de ações) e, à parte, o histórico aberto no painel.
  const [selecao, setSelecao] = useState<Selecao | null>(null);
  const [historico, setHistorico] = useState<Selecao | null>(null);
  const [movimentacoes, setMovimentacoes] = useState<MovimentacaoEstoqueUniforme[]>([]);
  const [estadoHistorico, setEstadoHistorico] = useState<'carregando' | 'ok' | 'erro'>('ok');
  const [erroHistorico, setErroHistorico] = useState<string | null>(null);
  const painelRef = useRef<HTMLElement | null>(null);
  const [fotoAmpliada, setFotoAmpliada] = useState<{ item: CatalogoUniforme; url: string; revogar: boolean } | null>(null);
  const [lancamento, setLancamento] = useState<{ modo: 'entrada' | 'ajuste'; item: CatalogoUniforme; tamanho: string } | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const [listaObras, listaItens] = await Promise.all([api.obras.listar(), api.catalogosUniforme.listar()]);
        setObras(listaObras);
        setItensCatalogo(listaItens);
        // Quem só tem uma obra não precisa escolher: ela vira a obra ativa.
        if (listaObras.length === 1) definirObraAtiva(listaObras[0].id, listaObras[0].nome);
      } catch (ex) {
        setErro(ex instanceof Error ? ex.message : 'Falha ao carregar obras e catálogo de uniforme.');
      }
    })();
  }, [definirObraAtiva]);

  // Só a resposta do pedido mais recente, da obra que ainda está ativa, é aplicada.
  async function carregarSaldos() {
    const pedido = ++pedidoSaldos.current;
    const obra = obraAtualRef.current;
    if (!obra) {
      setSaldos([]);
      setEstadoSaldos('ok');
      return;
    }
    setEstadoSaldos('carregando');
    try {
      setErro(null);
      const dados = await api.estoquesUniforme.listarPorObra(obra);
      if (pedido !== pedidoSaldos.current) return;
      setSaldos(dados);
      setEstadoSaldos('ok');
    } catch (ex) {
      if (pedido !== pedidoSaldos.current) return;
      setSaldos([]);
      setEstadoSaldos('erro');
      setErro(ex instanceof Error ? ex.message : 'Falha ao carregar estoque da obra.');
    }
  }

  // Trocou de obra: limpa tudo da obra anterior ANTES de buscar a nova e invalida pedidos em voo.
  useEffect(() => {
    obraAtualRef.current = obraId;
    pedidoHistorico.current++;
    setSaldos([]);
    setSelecao(null);
    setHistorico(null);
    setMovimentacoes([]);
    setLancamento(null);
    carregarSaldos();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obraId]);

  // Painel de histórico aberto: rola até ele quando abre ou troca de tamanho.
  useEffect(() => {
    if (historico) painelRef.current?.scrollIntoView?.({ block: 'nearest', behavior: 'smooth' });
  }, [historico]);

  // Abre (ou troca) o painel de histórico. Só a resposta do pedido mais recente é aplicada; a falha
  // aparece dentro do painel. "manter" atualiza sem piscar (depois de um lançamento).
  async function carregarHistorico(alvo: Selecao, manter = false) {
    const pedido = ++pedidoHistorico.current;
    const obra = obraAtualRef.current;
    setHistorico(alvo);
    setErroHistorico(null);
    if (!manter) {
      setMovimentacoes([]);
      setEstadoHistorico('carregando');
    }
    try {
      const lista = await api.estoquesUniforme.listarMovimentacoes(obra, alvo.catalogoUniformeId, alvo.tamanho);
      if (pedido !== pedidoHistorico.current) return;
      setMovimentacoes(lista);
      setEstadoHistorico('ok');
    } catch (ex) {
      if (pedido !== pedidoHistorico.current) return;
      setMovimentacoes([]);
      setEstadoHistorico('erro');
      setErroHistorico(ex instanceof Error ? ex.message : 'Falha ao carregar histórico de movimentações.');
    }
  }

  function mesmaSelecao(a: Selecao | null, b: Selecao) {
    return a?.catalogoUniformeId === b.catalogoUniformeId && a.tamanho === b.tamanho;
  }

  // Tocar no tamanho só seleciona (de novo, desmarca). Com o painel de histórico aberto, ele acompanha.
  function alternarTamanho(alvo: Selecao) {
    if (mesmaSelecao(selecao, alvo)) {
      setSelecao(null);
      fecharHistorico();
      return;
    }
    setSelecao(alvo);
    if (historico) carregarHistorico(alvo);
  }

  function fecharHistorico() {
    pedidoHistorico.current++;
    setHistorico(null);
    setErroHistorico(null);
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
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Falha ao carregar a foto original.');
    }
  }

  function fecharFotoAmpliada() {
    if (fotoAmpliada?.revogar) URL.revokeObjectURL(fotoAmpliada.url);
    setFotoAmpliada(null);
  }

  function abrirLancamento(modo: 'entrada' | 'ajuste', item: CatalogoUniforme, tamanho: string) {
    origemLancamento.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setLancamento({ modo, item, tamanho });
  }

  // Ao fechar, o foco volta ao botão que abriu o diálogo.
  function fecharLancamento() {
    setLancamento(null);
    const origem = origemLancamento.current;
    origemLancamento.current = null;
    window.setTimeout(() => { if (origem?.isConnected) origem.focus(); }, 0);
  }

  // Depois de gravar: recarrega os saldos e, se o histórico daquele item estiver aberto, atualiza também.
  // Se a obra mudou enquanto gravava, a carga da nova obra já está em curso e nada daqui deve atropelá-la.
  async function aposLancamento(obra: string, catalogoUniformeId: string, tamanho: string) {
    if (obraAtualRef.current !== obra) return;
    await carregarSaldos();
    if (obraAtualRef.current !== obra) return;
    if (historico && historico.catalogoUniformeId === catalogoUniformeId && historico.tamanho.trim().toUpperCase() === tamanho) {
      await carregarHistorico(historico, true);
    }
  }

  // A confirmação da obra e o descarte acontecem dentro do diálogo. Devolvem a mensagem de erro
  // (mostrada no diálogo) ou null quando deu certo.
  async function registrarEntrada(item: CatalogoUniforme, dados: DadosLancamento): Promise<string | null> {
    const obra = obraAtualRef.current;
    if (!obra) return null;
    const tamanho = dados.tamanho.trim().toUpperCase();
    try {
      await api.estoquesUniforme.registrarEntrada({
        obraId: obra,
        catalogoUniformeId: item.id,
        tamanho,
        quantidade: dados.quantidade,
        observacao: dados.observacao || null,
      });
    } catch (ex) {
      return ex instanceof Error ? ex.message : 'Falha ao registrar entrada de estoque.';
    }
    fecharLancamento();
    await aposLancamento(obra, item.id, tamanho);
    return null;
  }

  async function ajustarSaldo(item: CatalogoUniforme, dados: DadosLancamento): Promise<string | null> {
    const obra = obraAtualRef.current;
    if (!obra || !dados.observacao.trim()) return null;
    const tamanho = dados.tamanho.trim().toUpperCase();
    try {
      await api.estoquesUniforme.ajustar({
        obraId: obra,
        catalogoUniformeId: item.id,
        tamanho,
        novoSaldo: dados.quantidade,
        observacao: dados.observacao,
      });
    } catch (ex) {
      return ex instanceof Error ? ex.message : 'Falha ao ajustar estoque.';
    }
    fecharLancamento();
    await aposLancamento(obra, item.id, tamanho);
    return null;
  }

  const saldosPorPeca = itensCatalogo.map((item) => ({
    item,
    tamanhos: saldos.filter((saldo) => saldo.catalogoUniformeId === item.id && saldo.tamanho),
  }));
  const itemDoHistorico = historico ? itensCatalogo.find((i) => i.id === historico.catalogoUniformeId) : undefined;
  const saldoDoHistorico = historico
    ? saldos.find((s) => s.catalogoUniformeId === historico.catalogoUniformeId && s.tamanho === historico.tamanho)?.saldo
    : undefined;
  const saldosProntos = estadoSaldos === 'ok';
  const itemDoLancamento = lancamento ? itensCatalogo.find((i) => i.id === lancamento.item.id) ?? lancamento.item : null;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
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
        <Card titulo="Saldo atual" densidade="compacta">
          <div className={e.grade}>
            {saldosPorPeca.map(({ item, tamanhos }) => {
              const saldoTotal = tamanhos.reduce((total, s) => total + s.saldo, 0);
              const referencia = referenciaUniforme(item.referenciaVisual);
              const selecionadoAqui = selecao?.catalogoUniformeId === item.id ? selecao : null;
              const saldoSelecionado = selecionadoAqui ? tamanhos.find((s) => s.tamanho === selecionadoAqui.tamanho)?.saldo ?? 0 : 0;
              return (
                <div key={item.id} className={mergeClasses(e.card, !!selecionadoAqui && e.cardSel)}>
                  <div className={e.foto}>
                    <FotoCatalogoUniforme catalogoUniformeId={item.id} temFoto={item.temFoto} referenciaVisual={item.referenciaVisual} destaque />
                    {(item.temFoto || referencia) && (
                      <Button icon={<ZoomIn24Regular />} appearance="primary" onClick={() => ampliar(item)} style={{ position: 'absolute', right: 10, bottom: 10 }}>
                        Ampliar
                      </Button>
                    )}
                  </div>
                  <div className={e.corpo}>
                    <div style={{ fontWeight: 800 }}>{item.nome}</div>
                    <div style={{ color: designTokens.colorNeutralMedium, fontSize: 12, margin: '4px 0 12px' }}>
                      {item.categoria || 'Sem categoria'} · {referencia?.cor ?? 'Foto própria'}
                    </div>
                    {saldosProntos && saldoTotal === 0 && (
                      <div className={e.semEstoque}>Sem estoque · Saldo 0</div>
                    )}
                    <div style={{ borderTop: `1px solid ${designTokens.colorCardBorder}`, paddingTop: 12 }}>
                      <div style={{ fontWeight: 800, fontSize: 12, color: designTokens.colorNeutralMedium, marginBottom: 8 }}>Tamanho · saldo na obra</div>
                      {estadoSaldos === 'carregando' && <Carregando variante="lista" linhas={1} />}
                      {estadoSaldos === 'erro' && <Legenda>Não foi possível carregar o saldo desta obra.</Legenda>}
                      <div className={e.tamanhos}>
                        {saldosProntos && tamanhos.length === 0 && <Legenda>Nenhum tamanho com entrada nesta obra.</Legenda>}
                        {tamanhos.map((s) => {
                          const ativo = selecionadoAqui?.tamanho === s.tamanho;
                          return (
                            <button
                              key={s.tamanho}
                              type="button"
                              aria-pressed={ativo}
                              aria-label={`Tamanho ${s.tamanho}, saldo ${s.saldo}`}
                              onClick={() => alternarTamanho({ catalogoUniformeId: item.id, tamanho: s.tamanho })}
                              className={mergeClasses(e.tamanho, ativo && e.tamanhoAtivo)}
                            >
                              <b style={{ display: 'block', fontSize: 14 }}>{s.tamanho}</b>
                              <small style={{ display: 'block', fontSize: 12, color: ativo ? 'inherit' : designTokens.colorNeutralMedium }}>{s.saldo}</small>
                            </button>
                          );
                        })}
                      </div>
                    </div>

                    {saldosProntos && selecionadoAqui && (
                      <div className={e.acoes}>
                        <div style={{ fontSize: 13, marginBottom: 8 }}>
                          Tamanho <b className={e.destaque}>{selecionadoAqui.tamanho}</b> · saldo <b className={e.destaque}>{saldoSelecionado}</b>
                        </div>
                        <div className={e.acoesGrade}>
                          <Button className={e.acaoBotao} icon={<Add24Regular />} onClick={() => abrirLancamento('entrada', item, selecionadoAqui.tamanho)}>
                            Entrada
                          </Button>
                          <Button className={e.acaoBotao} onClick={() => abrirLancamento('ajuste', item, selecionadoAqui.tamanho)}>
                            Ajustar saldo
                          </Button>
                          <Button className={mergeClasses(e.acaoBotao, e.acaoHistorico)} onClick={() => carregarHistorico(selecionadoAqui)}>
                            Histórico
                          </Button>
                        </div>
                      </div>
                    )}

                    <Button className={e.registrar} icon={<Add24Regular />} disabled={!saldosProntos} onClick={() => abrirLancamento('entrada', item, '')}>
                      Registrar entrada
                    </Button>
                  </div>
                </div>
              );
            })}

            {historico && (
              <section ref={painelRef} className={e.painel} aria-label="Histórico de movimentações">
                <header className={e.painelCab}>
                  <div>
                    <h3 className={e.painelTitulo}>
                      Histórico · <span className={e.destaque}>{itemDoHistorico?.nome}</span> · tamanho <span className={e.destaque}>{historico.tamanho}</span>
                    </h3>
                    <p style={{ margin: '4px 0 0', color: designTokens.colorNeutralMedium, fontSize: 13 }}>
                      {saldoDoHistorico !== undefined && <>Saldo atual <b style={{ color: 'inherit' }}>{saldoDoHistorico}</b> un.</>}
                      {estadoHistorico === 'ok' && <> · {movimentacoes.length} {movimentacoes.length === 1 ? 'movimentação' : 'movimentações'}</>}
                    </p>
                  </div>
                  <Button className={e.fecharPainel} icon={<Dismiss24Regular />} onClick={fecharHistorico} aria-label="Fechar histórico" appearance="subtle" />
                </header>
                {estadoHistorico === 'carregando' && <Carregando variante="lista" linhas={2} />}
                {estadoHistorico === 'erro' && (
                  <div className={e.painelErro}>
                    <FeedbackInline tom="erro">
                      {erroHistorico ?? 'Falha ao carregar histórico de movimentações.'}
                    </FeedbackInline>
                  </div>
                )}
                {estadoHistorico === 'ok' && movimentacoes.length > 0 && (
                  <div className={e.historicoWrap}>
                    <table
                      className={mergeClasses(e.historico, e.historicoCelular)}
                      aria-label={`Movimentações de ${itemDoHistorico?.nome ?? ''} (${historico.tamanho})`}
                    >
                      <thead>
                        <tr>
                          <th className={e.historicoTh}>Data</th>
                          <th className={e.historicoTh}>Tipo</th>
                          <th className={mergeClasses(e.historicoTh, e.direita)}>Quantidade</th>
                          <th className={mergeClasses(e.historicoTh, e.direita)}>Saldo resultante</th>
                          <th className={e.historicoTh}>Observação</th>
                        </tr>
                      </thead>
                      <tbody>
                        {movimentacoes.map((m) => {
                          const cor = pilulaDoTipo(m.tipo);
                          return (
                            <tr key={m.id}>
                              <td className={e.historicoTd} data-l="Data">{formatarDataHoraBrasilia(m.createdAtUtc)}</td>
                              <td className={e.historicoTd} data-l="Tipo">
                                <span className={e.pilula} style={{ background: cor.fundo, color: cor.tinta }}>{tipoMovimentacaoEstoqueUniformeLabel[m.tipo]}</span>
                              </td>
                              <td className={mergeClasses(e.historicoTd, e.direita)} data-l="Quantidade">{m.quantidade}</td>
                              <td className={mergeClasses(e.historicoTd, e.direita)} data-l="Saldo resultante"><b>{m.saldoResultante}</b></td>
                              <td className={e.historicoTd} data-l="Observação">{m.observacao}</td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
                {estadoHistorico === 'ok' && movimentacoes.length === 0 && <Legenda>Nenhuma movimentação registrada.</Legenda>}
              </section>
            )}
          </div>
          {saldosPorPeca.length === 0 && <Legenda>Nenhum item do catálogo cadastrado.</Legenda>}
        </Card>
      )}

      {lancamento && itemDoLancamento && (
        <LancamentoEstoqueDialog
          key={`${lancamento.modo}-${lancamento.item.id}-${lancamento.tamanho}`}
          modo={lancamento.modo}
          item={itemDoLancamento}
          nomeObra={nomeObra}
          tamanhos={saldos
            .filter((s) => s.catalogoUniformeId === lancamento.item.id && s.tamanho)
            .map((s) => ({ tamanho: s.tamanho, saldo: s.saldo }))}
          tamanhoInicial={lancamento.tamanho}
          aoConfirmar={(dados) => (lancamento.modo === 'entrada' ? registrarEntrada(itemDoLancamento, dados) : ajustarSaldo(itemDoLancamento, dados))}
          aoFechar={fecharLancamento}
        />
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
