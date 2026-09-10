import { Fragment, useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
} from '@fluentui/react-components';
import {
  api,
  tipoMovimentacaoEstoqueUniformeLabel,
  type CatalogoUniforme,
  type EstoqueUniformePorObra,
  type MovimentacaoEstoqueUniforme,
  type Obra,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// Estoque de Uniforme — grade por Obra + Tamanho (mesmo princípio de segmentação por Obra do
// EstoqueTab.tsx do EPI, com uma dimensão a mais: o tamanho). Entrada é sempre manual (sem código
// de barras — decisão do brainstorming, 2026-09-07): escolhe a peça, digita o tamanho (cria um
// tamanho novo se ainda não existir nessa peça+obra) e a quantidade recebida.
export function EstoqueUniformeTab() {
  const estilos = usePageStyles();
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
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
        if (listaObras.length > 0) setObraId(listaObras[0].id);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar obras e catálogo de uniforme.');
      }
    })();
  }, []);

  async function carregarSaldos() {
    if (!obraId) return;
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

  async function alternarHistorico(catalogoUniformeId: string, tamanho: string) {
    if (linhaSelecionada?.catalogoUniformeId === catalogoUniformeId && linhaSelecionada.tamanho === tamanho) {
      setLinhaSelecionada(null);
      return;
    }
    try {
      setErro(null);
      setMovimentacoes(await api.estoquesUniforme.listarMovimentacoes(obraId, catalogoUniformeId, tamanho));
      setLinhaSelecionada({ catalogoUniformeId, tamanho });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar histórico de movimentações.');
    }
  }

  async function registrarEntrada() {
    if (!obraId || !entradaCatalogoUniformeId || !entradaTamanho.trim()) return;
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

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Estoque de Uniforme por Obra</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

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
      </div>

      {obraId && (
        <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Entrada manual (reposição)</Text>
            </div>
            <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Informações da entrada</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col4}>
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
              </div>
              <div className={estilos.col2}>
                <Field label="Tamanho">
                  <Input value={entradaTamanho} onChange={(_, d) => setEntradaTamanho(d.value)} placeholder="ex.: M, 42..." />
                </Field>
              </div>
              <div className={estilos.col2}>
                <Field label="Quantidade">
                  <Input type="number" value={entradaQuantidade} onChange={(_, d) => setEntradaQuantidade(d.value)} />
                </Field>
              </div>
              <div className={estilos.col4}>
                <Field label="Observação (opcional)">
                  <Input value={entradaObservacao} onChange={(_, d) => setEntradaObservacao(d.value)} />
                </Field>
              </div>
            </div>
            <div className={estilos.formActions}>
              <Button
                appearance="primary"
                onClick={registrarEntrada}
                disabled={carregando || !entradaCatalogoUniformeId || !entradaTamanho.trim() || Number(entradaQuantidade) <= 0}
              >
                Registrar entrada
              </Button>
            </div>
          </div>

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Ajuste de saldo (correção de inventário)</Text>
            </div>
            <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Informações do ajuste</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col4}>
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
              </div>
              <div className={estilos.col2}>
                <Field label="Tamanho">
                  <Input value={ajusteTamanho} onChange={(_, d) => setAjusteTamanho(d.value)} placeholder="ex.: M, 42..." />
                </Field>
              </div>
              <div className={estilos.col2}>
                <Field label="Novo saldo">
                  <Input type="number" value={ajusteNovoSaldo} onChange={(_, d) => setAjusteNovoSaldo(d.value)} />
                </Field>
              </div>
              <div className={estilos.col4}>
                <Field label="Observação (obrigatória)">
                  <Textarea value={ajusteObservacao} onChange={(_, d) => setAjusteObservacao(d.value)} />
                </Field>
              </div>
            </div>
            <div className={estilos.formActions}>
              <Button
                appearance="primary"
                onClick={ajustarSaldo}
                disabled={carregando || !ajusteCatalogoUniformeId || !ajusteTamanho.trim() || !ajusteObservacao.trim() || Number(ajusteNovoSaldo) < 0}
              >
                Ajustar saldo
              </Button>
            </div>
          </div>

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Saldo atual</Text>
            </div>
            <Table noNativeElements>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Peça</TableHeaderCell>
                  <TableHeaderCell>Tamanho</TableHeaderCell>
                  <TableHeaderCell>Saldo</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {saldos.map((s) => {
                  const chave = `${s.catalogoUniformeId}-${s.tamanho}`;
                  const selecionada = linhaSelecionada?.catalogoUniformeId === s.catalogoUniformeId && linhaSelecionada.tamanho === s.tamanho;
                  return (
                    <Fragment key={chave}>
                      <TableRow onClick={() => alternarHistorico(s.catalogoUniformeId, s.tamanho)} style={{ cursor: 'pointer' }}>
                        <TableCell>{s.catalogoUniformeNome}</TableCell>
                        <TableCell>{s.tamanho}</TableCell>
                        <TableCell>{s.saldo}</TableCell>
                      </TableRow>
                      {selecionada && (
                        <TableRow key={`${chave}-historico`}>
                          <TableCell colSpan={3}>
                            <div style={{ padding: '8px 0' }}>
                              <Text weight="semibold">
                                Histórico de movimentações — {s.catalogoUniformeNome} ({s.tamanho})
                              </Text>
                              {movimentacoes.length === 0 ? (
                                <Text as="p" size={200}>
                                  Nenhuma movimentação registrada.
                                </Text>
                              ) : (
                                <Table noNativeElements>
                                  <TableHeader>
                                    <TableRow>
                                      <TableHeaderCell>Data</TableHeaderCell>
                                      <TableHeaderCell>Tipo</TableHeaderCell>
                                      <TableHeaderCell>Quantidade</TableHeaderCell>
                                      <TableHeaderCell>Saldo resultante</TableHeaderCell>
                                      <TableHeaderCell>Observação</TableHeaderCell>
                                    </TableRow>
                                  </TableHeader>
                                  <TableBody>
                                    {movimentacoes.map((m) => (
                                      <TableRow key={m.id}>
                                        <TableCell>{new Date(m.createdAtUtc).toLocaleString('pt-BR')}</TableCell>
                                        <TableCell>{tipoMovimentacaoEstoqueUniformeLabel[m.tipo]}</TableCell>
                                        <TableCell>{m.quantidade}</TableCell>
                                        <TableCell>{m.saldoResultante}</TableCell>
                                        <TableCell>{m.observacao}</TableCell>
                                      </TableRow>
                                    ))}
                                  </TableBody>
                                </Table>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      )}
                    </Fragment>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        </>
      )}
    </div>
  );
}
