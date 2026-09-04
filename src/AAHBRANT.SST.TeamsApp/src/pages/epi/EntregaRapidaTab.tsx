import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Avatar,
  Badge,
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
} from '@fluentui/react-components';
import { Delete24Regular, Fingerprint24Regular, Flash24Regular, ScanObject24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaEpiLabel,
  MotivoEntregaEpi,
  TipoEntidadeVinculada,
  type CatalogoEpi,
  type CursoTreinamento,
  type EntregaEpi,
  type NovaEntregaEpi,
  type PerfilCompletoTrabalhador,
  type Trabalhador,
} from '../../lib/api';
import { mascararCpf } from '../../lib/cpf';
import { usePageStyles } from '../pageStyles';
import { designTokens } from '../../theme';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';
import { AssinaturaLoteEntregaEpiDialog, type ItemAssinaturaLote } from '../../components/assinatura/AssinaturaLoteEntregaEpiDialog';

interface ItemLote {
  catalogoEpiId: string;
  quantidade: number;
  motivoTipo: number;
}

const corAptidao: Record<string, 'success' | 'warning' | 'danger' | 'informative'> = {
  Apto: 'success',
  'Apto com restrição': 'warning',
  Inapto: 'danger',
};

const JANELA_ALERTA_TROCA_DIAS = 90;

function ehNormaNr6(normaReferencia?: string | null): boolean {
  return (normaReferencia ?? '').replace(/\D/g, '') === '6';
}

function calcularValidade(dataEntregaIso: string, vidaUtilEmMeses: number): string {
  const data = new Date(dataEntregaIso);
  data.setMonth(data.getMonth() + vidaUtilEmMeses);
  return data.toISOString().slice(0, 10);
}

// Tela de balcão do almoxarifado (pedido do usuário, 04/09) — busca o funcionário, monta um lote de
// itens (kit padrão da função e/ou leitor de código de barras) e finaliza tudo com UMA assinatura só
// (biometria via leitor Futronic, único método do sistema desde 31/08 — ver AssinaturaQuiosque.tsx).
// Reaproveita integralmente as regras já existentes em EntregasTab.tsx (bloqueio de CA vencido,
// estoque, motivos, NR-6) — só troca a experiência de "um item por vez" por "lote de uma vez".
export function EntregaRapidaTab() {
  const estilos = usePageStyles();
  const buscaRef = useRef<HTMLInputElement>(null);

  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);

  const [busca, setBusca] = useState('');
  const [buscandoUid, setBuscandoUid] = useState(false);

  const [selecionado, setSelecionado] = useState<PerfilCompletoTrabalhador | null>(null);
  const [fotoUrl, setFotoUrl] = useState<string | null>(null);
  const [episPermitidos, setEpisPermitidos] = useState<CatalogoEpi[]>([]);
  const [entregasRecentes, setEntregasRecentes] = useState<EntregaEpi[]>([]);

  const [lote, setLote] = useState<ItemLote[]>([]);
  const [codigoBusca, setCodigoBusca] = useState('');
  const [itemManualId, setItemManualId] = useState('');

  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [itensParaAssinar, setItensParaAssinar] = useState<ItemAssinaturaLote[] | null>(null);

  useEffect(() => {
    api.trabalhadores.listar().then(setTrabalhadores).catch(() => setTrabalhadores([]));
    api.catalogosEpi.listar().then(setEpis).catch(() => setEpis([]));
    api.cursosTreinamento.listar().then(setCursos).catch(() => setCursos([]));
    buscaRef.current?.focus();
  }, []);

  const sugestoes = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    const termoDigitos = termo.replace(/\D/g, '');
    if (!termo) return [];
    return trabalhadores
      .filter(
        (t) =>
          t.nome.toLowerCase().includes(termo) ||
          t.matricula.toLowerCase().includes(termo) ||
          (termoDigitos.length >= 3 && t.cpf.includes(termoDigitos)),
      )
      .slice(0, 8);
  }, [busca, trabalhadores]);

  async function selecionarTrabalhador(trabalhadorId: string) {
    setErro(null);
    setBusca('');
    setLote([]);
    setSelecionado(null);
    try {
      const perfil = await api.trabalhadores.obterPerfilCompleto(trabalhadorId);
      setSelecionado(perfil);
      const [permitidos, entregas] = await Promise.all([
        api.funcoes.listarEpis(perfil.funcaoId),
        api.entregasEpi.listar(trabalhadorId),
      ]);
      setEpisPermitidos(permitidos);
      setEntregasRecentes(entregas);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar o perfil do funcionário.');
    }
  }

  // "Bipar o crachá" — o leitor NFC/QR do crachá digital (módulo Identificação) "digita" o Uid da
  // tag + Enter, igual a um leitor de código de barras. Se não for um Uid de tag conhecido, cai pra
  // busca normal por nome/matrícula/CPF.
  async function aoConfirmarBusca() {
    const termo = busca.trim();
    if (!termo) return;
    if (sugestoes.length === 1) {
      await selecionarTrabalhador(sugestoes[0].id);
      return;
    }
    try {
      setBuscandoUid(true);
      const resolvido = await api.tagsIdentificacao.resolverPorUid(termo);
      if (resolvido.entidadeVinculadaTipo === TipoEntidadeVinculada.Trabalhador && resolvido.entidadeVinculadaId) {
        await selecionarTrabalhador(resolvido.entidadeVinculadaId);
      } else {
        setErro('Nenhum funcionário encontrado — refine a busca ou bipe o crachá.');
      }
    } catch {
      setErro('Nenhum funcionário encontrado — refine a busca ou bipe o crachá.');
    } finally {
      setBuscandoUid(false);
    }
  }

  useEffect(() => {
    if (!selecionado?.temFoto) {
      setFotoUrl(null);
      return;
    }
    let cancelado = false;
    let urlCriada: string | null = null;
    (async () => {
      try {
        const blob = await api.trabalhadores.baixarFoto(selecionado.id);
        if (cancelado) return;
        urlCriada = URL.createObjectURL(blob);
        setFotoUrl(urlCriada);
      } catch {
        // Sem foto não impede o atendimento — Avatar cai para as iniciais do nome.
      }
    })();
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [selecionado?.id, selecionado?.temFoto]);

  function adicionarItem(catalogoEpiId: string) {
    setErro(null);
    setLote((atual) => {
      const existente = atual.find((i) => i.catalogoEpiId === catalogoEpiId);
      if (existente) {
        return atual.map((i) => (i.catalogoEpiId === catalogoEpiId ? { ...i, quantidade: i.quantidade + 1 } : i));
      }
      return [...atual, { catalogoEpiId, quantidade: 1, motivoTipo: MotivoEntregaEpi.Inicial }];
    });
  }

  function carregarKitPadrao() {
    setErro(null);
    setLote((atual) => {
      const idsExistentes = new Set(atual.map((i) => i.catalogoEpiId));
      const novosItens = episPermitidos
        .filter((e) => !idsExistentes.has(e.id))
        .map((e) => ({ catalogoEpiId: e.id, quantidade: 1, motivoTipo: MotivoEntregaEpi.Inicial }));
      return [...atual, ...novosItens];
    });
  }

  function adicionarPorCodigo() {
    const valor = codigoBusca.trim();
    if (!valor) return;
    const epi = epis.find((e) => e.codigoBarras === valor || e.id === valor);
    if (!epi) {
      setErro(`Nenhum EPI encontrado com o código "${valor}".`);
      setCodigoBusca('');
      return;
    }
    adicionarItem(epi.id);
    setCodigoBusca('');
  }

  function nomeEpi(id: string): string {
    return epis.find((e) => e.id === id)?.nome ?? id;
  }

  function statusCa(catalogoEpiId: string): { rotulo: string; cor: 'success' | 'danger' } {
    const epi = epis.find((e) => e.id === catalogoEpiId);
    if (!epi?.certificadoAprovacaoValidade) return { rotulo: 'CA sem validade cadastrada', cor: 'success' };
    const vencido = new Date(epi.certificadoAprovacaoValidade) < new Date();
    return vencido ? { rotulo: 'CA VENCIDO', cor: 'danger' } : { rotulo: 'CA VÁLIDO', cor: 'success' };
  }

  // Alerta não-bloqueante (pedido do usuário, 04/09): avisa quando o funcionário já trocou o MESMO
  // item recentemente — não impede a entrega (pode ser legítimo), só chama atenção do atendente.
  function alertaTrocaFrequente(catalogoEpiId: string): string | null {
    const limite = new Date();
    limite.setDate(limite.getDate() - JANELA_ALERTA_TROCA_DIAS);
    const trocasRecentes = entregasRecentes.filter(
      (e) =>
        e.catalogoEpiId === catalogoEpiId &&
        e.motivoTipo !== null &&
        e.motivoTipo !== MotivoEntregaEpi.Inicial &&
        new Date(e.dataEntrega) >= limite,
    );
    if (trocasRecentes.length === 0) return null;
    return `${trocasRecentes.length + 1}ª troca deste item nos últimos ${JANELA_ALERTA_TROCA_DIAS} dias`;
  }

  function atualizarItem(catalogoEpiId: string, mudanca: Partial<ItemLote>) {
    setLote((atual) => atual.map((i) => (i.catalogoEpiId === catalogoEpiId ? { ...i, ...mudanca } : i)));
  }

  function removerItem(catalogoEpiId: string) {
    setLote((atual) => atual.filter((i) => i.catalogoEpiId !== catalogoEpiId));
  }

  function cancelar() {
    setSelecionado(null);
    setLote([]);
    setErro(null);
    setBusca('');
    buscaRef.current?.focus();
  }

  async function finalizarEColherAssinatura() {
    if (!selecionado || lote.length === 0) return;
    try {
      setProcessando(true);
      setErro(null);
      const hoje = new Date().toISOString().slice(0, 10);

      // Mesmo preenchimento automático de NR-06 já usado em EntregasTab.tsx — um só treinamento
      // vale pro lote inteiro, já que é o mesmo funcionário recebendo tudo na mesma visita.
      const treinamentosDoTrabalhador = await api.treinamentos.listar(selecionado.id);
      const treinamentoNr6 = treinamentosDoTrabalhador
        .filter((t) => ehNormaNr6(cursos.find((c) => c.id === t.cursoTreinamentoId)?.normaReferencia))
        .sort((a, b) => b.dataRealizacao.localeCompare(a.dataRealizacao))[0];

      const criadas: ItemAssinaturaLote[] = [];
      for (const item of lote) {
        const epi = epis.find((e) => e.id === item.catalogoEpiId);
        const payload: NovaEntregaEpi = {
          trabalhadorId: selecionado.id,
          catalogoEpiId: item.catalogoEpiId,
          dataEntrega: hoje,
          dataDevolucao: null,
          dataValidade: epi ? calcularValidade(hoje, epi.vidaUtilEmMeses) : null,
          quantidade: item.quantidade,
          quantidadeDevolucao: null,
          vistoConsorcioResponsavel: null,
          motivo: null,
          observacoes: null,
          motivoTipo: item.motivoTipo,
          numeroListaPresencaNr6: treinamentoNr6?.numeroCertificado ?? null,
          dataTreinamentoNr6: treinamentoNr6?.dataRealizacao.slice(0, 10) ?? null,
        };
        const { id } = await api.entregasEpi.criar(payload);
        criadas.push({
          entregaId: id,
          catalogoEpiId: item.catalogoEpiId,
          catalogoEpiNome: nomeEpi(item.catalogoEpiId),
          epiTemFoto: epi?.temFoto ?? false,
          quantidade: item.quantidade,
        });
      }
      setLote([]);
      setItensParaAssinar(criadas);
    } catch (e) {
      setErro(
        e instanceof Error
          ? `${e.message} — os itens já registrados antes deste continuam válidos; remova o item problemático e finalize de novo para o restante.`
          : 'Falha ao registrar o lote de entrega.',
      );
    } finally {
      setProcessando(false);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Bloco Topo — Identificação do Colaborador */}
      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Buscar funcionário</Text>
        </div>
        {erro && <Text className={estilos.erro}>{erro}</Text>}
        <div style={{ position: 'relative', maxWidth: 480 }}>
          <Input
            ref={buscaRef}
            size="large"
            value={busca}
            placeholder="Digite CPF, nome ou matrícula, ou bipe o crachá"
            onChange={(_, d) => setBusca(d.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') aoConfirmarBusca();
            }}
            disabled={buscandoUid}
          />
          {sugestoes.length > 0 && (
            <div
              style={{
                position: 'absolute',
                zIndex: 10,
                top: '100%',
                left: 0,
                right: 0,
                backgroundColor: designTokens.colorWhite,
                border: `1px solid ${designTokens.colorCardBorder}`,
                borderRadius: 8,
                marginTop: 4,
                boxShadow: '0 4px 12px rgba(0,0,0,0.12)',
              }}
            >
              {sugestoes.map((t) => (
                <div
                  key={t.id}
                  onClick={() => selecionarTrabalhador(t.id)}
                  style={{ padding: '8px 12px', cursor: 'pointer' }}
                >
                  <Text weight="semibold" style={{ display: 'block' }}>
                    {t.nome}
                  </Text>
                  <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                    Matrícula {t.matricula} · CPF {mascararCpf(t.cpf)}
                  </Text>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {selecionado && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 16, alignItems: 'start' }}>
          {/* Bloco Esquerdo — Perfil & Kit de Atalho */}
          <div className={estilos.card}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 12 }}>
              <Avatar name={selecionado.nome} image={fotoUrl ? { src: fotoUrl } : undefined} color="brand" size={64} />
              <div>
                <Text weight="semibold" style={{ display: 'block' }}>
                  {selecionado.nome}
                </Text>
                <Text size={200} style={{ display: 'block', color: designTokens.colorNeutralMedium }}>
                  Mat. {selecionado.matricula} · CPF {mascararCpf(selecionado.cpf)}
                </Text>
              </div>
            </div>
            <Text style={{ display: 'block' }}>Função: {selecionado.funcaoNome}</Text>
            <Text style={{ display: 'block', marginBottom: 8 }}>Obra: {selecionado.obraNome}</Text>
            <Badge color={corAptidao[selecionado.statusAptidao] ?? 'informative'} appearance="tint" size="large">
              {selecionado.statusAptidao}
            </Badge>

            <div style={{ marginTop: 16 }}>
              <Button
                appearance="primary"
                icon={<Flash24Regular />}
                onClick={carregarKitPadrao}
                disabled={episPermitidos.length === 0}
              >
                Carregar Kit Padrão da Função
              </Button>
              {episPermitidos.length === 0 && (
                <Text size={200} style={{ display: 'block', marginTop: 8, color: designTokens.colorNeutralMedium }}>
                  Esta função não tem EPIs cadastrados na Matriz de EPI por Função.
                </Text>
              )}
            </div>
          </div>

          {/* Bloco Direito — Itens da Entrega Atual */}
          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Itens da entrega ({lote.length})</Text>
            </div>
            <div style={{ display: 'flex', gap: 8, marginBottom: 12, alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <div style={{ flex: 1, minWidth: 220 }}>
                <Field label="Bipe o código de barras">
                  <Input
                    contentBefore={<ScanObject24Regular />}
                    value={codigoBusca}
                    onChange={(_, d) => setCodigoBusca(d.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') adicionarPorCodigo();
                    }}
                  />
                </Field>
              </div>
              <div style={{ flex: 1, minWidth: 220 }}>
                <Field label="Ou selecione manualmente">
                  <Select value={itemManualId} onChange={(_, d) => setItemManualId(d.value)}>
                    <option value="">Selecione um item</option>
                    {epis.map((e) => (
                      <option key={e.id} value={e.id}>
                        {e.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <Button
                appearance="secondary"
                onClick={() => {
                  if (itemManualId) {
                    adicionarItem(itemManualId);
                    setItemManualId('');
                  }
                }}
                disabled={!itemManualId}
              >
                Adicionar
              </Button>
            </div>

            {lote.length === 0 ? (
              <Text style={{ color: designTokens.colorNeutralMedium }}>
                Nenhum item no lote ainda — carregue o kit padrão ou bipe/selecione um item.
              </Text>
            ) : (
              <Table noNativeElements>
                <TableHeader>
                  <TableRow>
                    <TableHeaderCell>Item</TableHeaderCell>
                    <TableHeaderCell>CA</TableHeaderCell>
                    <TableHeaderCell>Qtd</TableHeaderCell>
                    <TableHeaderCell>Motivo</TableHeaderCell>
                    <TableHeaderCell></TableHeaderCell>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {lote.map((item) => {
                    const ca = statusCa(item.catalogoEpiId);
                    const alerta = alertaTrocaFrequente(item.catalogoEpiId);
                    const epi = epis.find((e) => e.id === item.catalogoEpiId);
                    return (
                      <TableRow key={item.catalogoEpiId}>
                        <TableCell>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            <FotoCatalogoEpi catalogoEpiId={item.catalogoEpiId} temFoto={epi?.temFoto ?? false} tamanho={28} />
                            <div>
                              <Text>{nomeEpi(item.catalogoEpiId)}</Text>
                              {alerta && (
                                <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                                  <Warning24Regular style={{ color: designTokens.colorWarning, fontSize: 14 }} />
                                  <Text size={200} style={{ color: designTokens.colorWarning }}>
                                    {alerta}
                                  </Text>
                                </div>
                              )}
                            </div>
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge color={ca.cor} appearance="tint">
                            {ca.rotulo}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            style={{ width: 64 }}
                            value={String(item.quantidade)}
                            onChange={(_, d) => atualizarItem(item.catalogoEpiId, { quantidade: Number(d.value) || 1 })}
                          />
                        </TableCell>
                        <TableCell>
                          <Select
                            value={String(item.motivoTipo)}
                            onChange={(_, d) => atualizarItem(item.catalogoEpiId, { motivoTipo: Number(d.value) })}
                          >
                            {Object.entries(motivoEntregaEpiLabel).map(([valor, rotulo]) => (
                              <option key={valor} value={valor}>
                                {rotulo}
                              </option>
                            ))}
                          </Select>
                        </TableCell>
                        <TableCell>
                          <Button
                            appearance="subtle"
                            icon={<Delete24Regular />}
                            onClick={() => removerItem(item.catalogoEpiId)}
                            aria-label="Remover item"
                          />
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
          </div>
        </div>
      )}

      {/* Bloco Rodapé — Autenticação e Finalização */}
      {selecionado && (
        <div className={estilos.card}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Fingerprint24Regular />
              <Text>Assinatura: digital via leitor Futronic (único método do sistema)</Text>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <Button appearance="secondary" onClick={cancelar} disabled={processando}>
                Cancelar
              </Button>
              <Button
                appearance="primary"
                onClick={finalizarEColherAssinatura}
                disabled={processando || lote.length === 0}
              >
                Finalizar e Colher Assinatura
              </Button>
            </div>
          </div>
        </div>
      )}

      {itensParaAssinar && selecionado && (
        <AssinaturaLoteEntregaEpiDialog
          open={!!itensParaAssinar}
          onClose={() => setItensParaAssinar(null)}
          itens={itensParaAssinar}
          trabalhadorNome={selecionado.nome}
          dataEntrega={new Date().toISOString().slice(0, 10)}
        />
      )}
    </div>
  );
}
