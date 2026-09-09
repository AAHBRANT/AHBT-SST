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
  tipoMovimentacaoEstoqueEpcLabel,
  type CatalogoEpc,
  type EstoqueEpcPorObra,
  type MovimentacaoEstoqueEpc,
  type Obra,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// Estoque de EPC segmentado por Obra — mesma estrutura do estoque de EPI (Fase 3). Entradas/saídas
// por instalação e remoção são automáticas (ver CriarInstalacaoEpcCommand/RegistrarRemocaoEpcCommand
// no backend); aqui só entrada manual (reposição) e ajuste (correção de inventário) precisam de tela.
export function EstoqueEpcTab() {
  const estilos = usePageStyles();
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
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
        if (listaObras.length > 0) setObraId(listaObras[0].id);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar obras e catálogo de EPC.');
      }
    })();
  }, []);

  async function carregarSaldos() {
    if (!obraId) return;
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
  }, [obraId]);

  async function alternarHistorico(catalogoEpcId: string) {
    if (catalogoEpcIdSelecionado === catalogoEpcId) {
      setCatalogoEpcIdSelecionado(null);
      return;
    }
    try {
      setErro(null);
      setMovimentacoes(await api.estoquesEpc.listarMovimentacoes(obraId, catalogoEpcId));
      setCatalogoEpcIdSelecionado(catalogoEpcId);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar histórico de movimentações.');
    }
  }

  async function registrarEntrada() {
    if (!obraId || !entradaCatalogoEpcId) return;
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

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Estoque de EPC por Obra</Text>
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
            <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Entrada</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col5}>
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
              </div>
              <div className={estilos.col3}>
                <Field label="Quantidade">
                  <Input
                    type="number"
                    value={entradaQuantidade}
                    onChange={(_, d) => setEntradaQuantidade(d.value)}
                  />
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
                disabled={carregando || !entradaCatalogoEpcId || Number(entradaQuantidade) <= 0}
              >
                Registrar entrada
              </Button>
            </div>
          </div>

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Ajuste de saldo (correção de inventário)</Text>
            </div>
            <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados do Ajuste</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col5}>
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
              </div>
              <div className={estilos.col3}>
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
                disabled={carregando || !ajusteCatalogoEpcId || !ajusteObservacao.trim() || Number(ajusteNovoSaldo) < 0}
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
                  <TableHeaderCell>Nome</TableHeaderCell>
                  <TableHeaderCell>Fabricante</TableHeaderCell>
                  <TableHeaderCell>Saldo</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {saldos.map((s) => (
                  <Fragment key={s.catalogoEpcId}>
                    <TableRow
                      key={s.catalogoEpcId}
                      onClick={() => alternarHistorico(s.catalogoEpcId)}
                      style={{ cursor: 'pointer' }}
                    >
                      <TableCell>{s.catalogoEpcNome}</TableCell>
                      <TableCell>{s.fabricante}</TableCell>
                      <TableCell>{s.saldo}</TableCell>
                    </TableRow>
                    {catalogoEpcIdSelecionado === s.catalogoEpcId && (
                      <TableRow key={`${s.catalogoEpcId}-historico`}>
                        <TableCell colSpan={3}>
                          <div style={{ padding: '8px 0' }}>
                            <Text weight="semibold">Histórico de movimentações — {s.catalogoEpcNome}</Text>
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
                                      <TableCell>{tipoMovimentacaoEstoqueEpcLabel[m.tipo]}</TableCell>
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
                ))}
              </TableBody>
            </Table>
          </div>
        </>
      )}
    </div>
  );
}
