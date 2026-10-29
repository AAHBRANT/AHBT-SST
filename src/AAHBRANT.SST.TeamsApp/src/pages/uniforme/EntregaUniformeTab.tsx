import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  PainelCriacaoInline,
  Select,
  SeletorPesquisavel,
  Spinner,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Print24Regular, Signature24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaUniformeLabel,
  MotivoEntregaUniforme,
  type CatalogoUniforme,
  type EntregaUniforme,
  type NovaEntregaUniforme,
  type Obra,
  type TamanhoUniformeTrabalhador,
  type Trabalhador,
} from '../../lib/api';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { useRegistrosTravados } from '../../hooks/useRegistrosTravados';
import {
  AssinaturaEntregaUniformeLoteDialog,
  type ItemLoteAssinaturaUniforme,
} from '../../components/assinatura/AssinaturaEntregaUniformeLoteDialog';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';
import { SeletorItensUniforme, type ItemCarrinhoUniforme } from './SeletorItensUniforme';
import { CarrinhoEntregaUniforme } from './CarrinhoEntregaUniforme';

// Campos aplicados a todas as entregas do carrinho de uma vez; o que varia por item é só
// peça/quantidade/tamanho (ItemCarrinhoUniforme).
interface CamposComunsEntrega {
  trabalhadorId: string;
  dataEntrega: string;
  motivoTipo: number;
  observacoes: string;
}

function camposComunsVazios(): CamposComunsEntrega {
  return {
    trabalhadorId: '',
    dataEntrega: new Date().toISOString().slice(0, 10),
    motivoTipo: MotivoEntregaUniforme.Inicial,
    observacoes: '',
  };
}

interface LoteParaAssinar {
  trabalhadorId: string;
  dataEntrega: string;
  itens: ItemLoteAssinaturaUniforme[];
}

function escapeHtml(texto: string): string {
  return texto.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c] ?? c);
}

interface EntregaUniformeTabProps {
  aoNavegarParaMatriz: () => void;
}

// Entrega de Uniforme em carrinho (mesmo desenho de EntregasTab.tsx do EPI): vários itens para o
// mesmo funcionário, confirmados de uma vez e assinados numa única interação (digital ou facial).
// Sem mudar o banco: cada item vira uma EntregaUniforme individual via o endpoint existente
// (CriarEntregaUniformeCommand, que revalida matriz, tamanho e estoque); o erro do servidor é
// exibido como veio. Ver AssinaturaEntregaUniformeLoteDialog para a assinatura em lote.
export function EntregaUniformeTab({ aoNavegarParaMatriz }: EntregaUniformeTabProps) {
  const navigate = useNavigate();
  const [entregas, setEntregas] = useState<EntregaUniforme[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [itensPermitidos, setItensPermitidos] = useState<CatalogoUniforme[]>([]);
  const [tamanhosTrabalhador, setTamanhosTrabalhador] = useState<TamanhoUniformeTrabalhador[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [dadosComuns, setDadosComuns] = useState<CamposComunsEntrega>(camposComunsVazios());
  const [carrinho, setCarrinho] = useState<ItemCarrinhoUniforme[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  // Erro do painel separado do erro da lista: estados independentes.
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [excluindoId, setExcluindoId] = useState<string | null>(null);
  const souAdministrador = useSouAdministrador();
  // Já assinados não podem ser excluídos (o servidor recusa): o botão some deles.
  const travados = useRegistrosTravados('EntregaUniforme', entregas.map((e) => e.id));
  const { confirmar, dialogElement } = useConfirmar();
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [loteParaAssinar, setLoteParaAssinar] = useState<LoteParaAssinar | null>(null);
  // O Teams bloqueia window.open: o canhoto abre num iframe dentro de um diálogo da própria tela.
  const [canhotoAberto, setCanhotoAberto] = useState(false);
  const [canhotoHtml, setCanhotoHtml] = useState<string | null>(null);
  const canhotoFrameRef = useRef<HTMLIFrameElement>(null);

  async function carregar() {
    try {
      setCarregandoLista(true);
      setErro(null);
      const [lista, listaItens, listaTrabalhadores, listaObras] = await Promise.all([
        api.entregasUniforme.listar(),
        api.catalogosUniforme.listar(),
        api.trabalhadores.listar(),
        api.obras.listar(),
      ]);
      setEntregas(lista);
      setItensCatalogo(listaItens);
      setTrabalhadores(listaTrabalhadores);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de uniforme.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  useEffect(() => {
    let cancelado = false;
    async function carregarItensPermitidosETamanhos() {
      const trabalhador = trabalhadores.find((t) => t.id === dadosComuns.trabalhadorId);
      if (!trabalhador) {
        setItensPermitidos([]);
        setTamanhosTrabalhador([]);
        return;
      }
      try {
        const [permitidos, tamanhos] = await Promise.all([
          api.funcoes.listarUniformes(trabalhador.funcaoId),
          api.trabalhadores.listarTamanhosUniforme(trabalhador.id),
        ]);
        if (!cancelado) {
          setItensPermitidos(permitidos);
          setTamanhosTrabalhador(tamanhos);
        }
      } catch {
        if (!cancelado) {
          setItensPermitidos([]);
          setTamanhosTrabalhador([]);
        }
      }
    }
    carregarItensPermitidosETamanhos();
    return () => {
      cancelado = true;
    };
  }, [dadosComuns.trabalhadorId, trabalhadores]);

  const opcoesTrabalhadores = useMemo(
    () => trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome, descricao: t.matricula ?? undefined })),
    [trabalhadores],
  );

  function nomeItem(id: string) {
    return itensCatalogo.find((i) => i.id === id)?.nome ?? id;
  }

  function itemTemFoto(id: string) {
    return itensCatalogo.find((i) => i.id === id)?.temFoto ?? false;
  }

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  function obraIdTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.obraId ?? '';
  }

  function nomeObra(id?: string | null) {
    if (!id) return '—';
    const obra = obras.find((o) => o.id === id);
    return obra ? obra.codigo || obra.nome : id;
  }

  function itemLote(entrega: EntregaUniforme): ItemLoteAssinaturaUniforme {
    return {
      entregaId: entrega.id,
      catalogoUniformeId: entrega.catalogoUniformeId,
      pecaNome: nomeItem(entrega.catalogoUniformeId),
      pecaTemFoto: itemTemFoto(entrega.catalogoUniformeId),
      tamanho: entrega.tamanho,
      quantidade: entrega.quantidade,
    };
  }

  function trocarTrabalhador(id: string) {
    // As peças permitidas e os tamanhos mudam com o funcionário: o carrinho anterior perde o sentido.
    setDadosComuns({ ...dadosComuns, trabalhadorId: id });
    setCarrinho([]);
  }

  function adicionarAoCarrinho(item: ItemCarrinhoUniforme) {
    setCarrinho((atual) => [...atual.filter((i) => i.catalogoUniformeId !== item.catalogoUniformeId), item]);
    setErroPainel(null);
  }

  function removerDoCarrinho(catalogoUniformeId: string) {
    setCarrinho((atual) => atual.filter((i) => i.catalogoUniformeId !== catalogoUniformeId));
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setCarrinho([]);
    setDadosComuns(camposComunsVazios());
  }

  // Cria uma EntregaUniforme por item, em sequência (não existe endpoint em lote). Se um item falhar
  // no meio, os anteriores já foram gravados e debitaram estoque; o carrinho passa a mostrar só o
  // que falta, com aviso de quantos entraram antes do erro.
  async function confirmarCarrinho() {
    if (!dadosComuns.trabalhadorId || !dadosComuns.dataEntrega) {
      setErroPainel('Selecione o funcionário e a data de entrega.');
      return;
    }
    if (carrinho.length === 0) {
      setErroPainel('Adicione ao menos uma peça ao carrinho antes de confirmar.');
      return;
    }
    setCarregando(true);
    setErroPainel(null);
    const criados: { item: ItemCarrinhoUniforme; entrega: EntregaUniforme }[] = [];
    try {
      for (const item of carrinho) {
        const payload: NovaEntregaUniforme = {
          trabalhadorId: dadosComuns.trabalhadorId,
          catalogoUniformeId: item.catalogoUniformeId,
          dataEntrega: dadosComuns.dataEntrega,
          quantidade: item.quantidade,
          motivoTipo: dadosComuns.motivoTipo,
          observacoes: dadosComuns.observacoes || null,
        };
        const { id } = await api.entregasUniforme.criar(payload);
        criados.push({ item, entrega: await api.entregasUniforme.obterPorId(id) });
      }

      const loteConfirmado: LoteParaAssinar = {
        trabalhadorId: dadosComuns.trabalhadorId,
        dataEntrega: dadosComuns.dataEntrega,
        itens: criados.map(({ entrega }) => itemLote(entrega)),
      };
      fecharPainel();
      await carregar();
      setLoteParaAssinar(loteConfirmado);
    } catch (e) {
      const itemFalho = carrinho[criados.length];
      const mensagem = e instanceof Error ? e.message : 'Falha ao registrar entrega.';
      if (criados.length > 0) {
        setErroPainel(
          `${criados.length} de ${carrinho.length} peças foram registradas antes do erro em "${nomeItem(itemFalho?.catalogoUniformeId ?? '')}": ${mensagem} ` +
            'As peças já registradas permanecem no sistema — o carrinho foi ajustado para mostrar só o que falta.',
        );
        const idsCriados = new Set(criados.map((c) => c.item.catalogoUniformeId));
        setCarrinho((atual) => atual.filter((i) => !idsCriados.has(i.catalogoUniformeId)));
        await carregar();
      } else {
        setErroPainel(`Falha ao registrar "${nomeItem(itemFalho?.catalogoUniformeId ?? '')}": ${mensagem}`);
      }
    } finally {
      setCarregando(false);
    }
  }

  // Recibo de conferência gerado no navegador, antes de confirmar. Não substitui a ficha oficial.
  async function imprimirRascunhoCarrinho() {
    if (!dadosComuns.trabalhadorId || carrinho.length === 0) return;
    setCanhotoHtml(null);
    setCanhotoAberto(true);

    function blobParaDataUrl(blob: Blob): Promise<string> {
      return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(blob);
      });
    }

    async function fotoItem(peca?: CatalogoUniforme): Promise<string | null> {
      if (!peca?.temFoto) return null;
      try {
        return await blobParaDataUrl(await api.catalogosUniforme.baixarFoto(peca.id));
      } catch {
        return null;
      }
    }

    const trabalhador = trabalhadores.find((t) => t.id === dadosComuns.trabalhadorId);
    const dataFmt = dadosComuns.dataEntrega ? dadosComuns.dataEntrega.split('-').reverse().join('/') : '—';
    const emitidoEm = new Date().toLocaleString('pt-BR');
    const motivo = motivoEntregaUniformeLabel[dadosComuns.motivoTipo] ?? '—';
    const linhas = (
      await Promise.all(
        carrinho.map(async (item) => {
          const peca = itensCatalogo.find((i) => i.id === item.catalogoUniformeId);
          const foto = await fotoItem(peca);
          return `<div class="item">
          ${foto ? `<img class="item-foto" src="${foto}" alt="">` : ''}
          <div class="item-conteudo">
            <div class="item-nome">${escapeHtml(peca?.nome ?? item.catalogoUniformeId)}</div>
            <div class="item-linha"><span>Tamanho: ${escapeHtml(item.tamanho)}</span><span>Qtd: ${item.quantidade}</span></div>
          </div>
        </div>`;
        }),
      )
    ).join('');
    setCanhotoHtml(`<!DOCTYPE html><html lang="pt-br"><head><meta charset="UTF-8"><title>Canhoto de recibo - Uniforme</title>
      <style>
        @page { size: 80mm auto; margin: 4mm; }
        * { box-sizing: border-box; }
        body { width: 72mm; margin: 0 auto; font-family: Arial, sans-serif; color: #1a1a1a; font-size: 11px; }
        h1 { font-size: 13px; color: #670000; margin: 0 0 2px; text-align: center; text-transform: uppercase; }
        h2 { font-size: 11px; color: #670000; margin: 10px 0 5px; padding-top: 6px; border-top: 1px dashed #999; text-transform: uppercase; }
        .subtitulo { font-size: 10px; color: #555; margin-bottom: 8px; text-align: center; }
        .campo { display: flex; justify-content: space-between; gap: 8px; font-size: 10.5px; margin: 2px 0; }
        .campo b { color: #555; font-weight: 700; white-space: nowrap; }
        .campo span:last-child { text-align: right; overflow-wrap: anywhere; }
        .item { border-top: 1px dashed #ccc; padding: 6px 0; display: flex; gap: 6px; align-items: flex-start; }
        .item:first-child { border-top: 0; }
        .item-foto { width: 16mm; height: 16mm; object-fit: cover; border: 1px solid #ddd; border-radius: 3px; flex: 0 0 auto; }
        .item-conteudo { flex: 1; min-width: 0; }
        .item-nome { font-weight: 700; font-size: 11px; margin-bottom: 3px; overflow-wrap: anywhere; }
        .item-linha { display: flex; justify-content: space-between; gap: 8px; font-size: 10px; color: #333; }
        .aviso { margin-top: 10px; padding-top: 6px; border-top: 1px dashed #999; font-size: 9.5px; color: #666; text-align: center; }
      </style></head>
      <body>
        <h1>Uniformes recebidos</h1>
        <div class="subtitulo">Documento de conferência do carrinho · Emitido em ${emitidoEm}</div>

        <h2>Identificação</h2>
        <div class="campo"><b>Funcionário</b><span>${escapeHtml(trabalhador?.nome ?? nomeTrabalhador(dadosComuns.trabalhadorId))}</span></div>
        <div class="campo"><b>Obra</b><span>${escapeHtml(nomeObra(trabalhador?.obraId))}</span></div>
        <div class="campo"><b>Data</b><span>${dataFmt}</span></div>

        <h2>Itens do carrinho</h2>
        ${linhas}

        <h2>Motivo</h2>
        <div class="campo"><b>Motivo</b><span>${escapeHtml(motivo)}</span></div>
        <div class="campo"><b>Obs.</b><span>${escapeHtml(dadosComuns.observacoes || '—')}</span></div>

        <p class="aviso">Comprovante de conferência prévia. O registro oficial vale após confirmar e assinar eletronicamente a entrega.</p>
      </body></html>`);
  }

  function imprimirCanhoto() {
    const frame = canhotoFrameRef.current?.contentWindow;
    if (!frame) return;
    frame.focus();
    frame.print();
  }

  const colunas: Coluna<EntregaUniforme>[] = [
    { chave: 'funcionario', rotulo: 'Funcionário', render: (e) => nomeTrabalhador(e.trabalhadorId) },
    {
      chave: 'peca',
      rotulo: 'Peça',
      render: (e) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <FotoCatalogoUniforme
            catalogoUniformeId={e.catalogoUniformeId}
            temFoto={itemTemFoto(e.catalogoUniformeId)}
            tamanho={28}
          />
          {nomeItem(e.catalogoUniformeId)}
        </div>
      ),
    },
    { chave: 'tamanho', rotulo: 'Tamanho' },
    { chave: 'quantidade', rotulo: 'Qtd.', alinhar: 'direita' },
    { chave: 'entrega', rotulo: 'Entrega', render: (e) => e.dataEntrega?.slice(0, 10) ?? '' },
    { chave: 'motivo', rotulo: 'Motivo', render: (e) => motivoEntregaUniformeLabel[e.motivoTipo] },
  ];

  // Exclusão definitiva, só para Administrador (o servidor recusa os demais). A peça volta para o
  // estoque do tamanho correspondente na obra.
  async function excluir(entrega: EntregaUniforme) {
    const confirmado = await confirmar({
      titulo: 'Excluir entrega de uniforme',
      mensagem:
        `Excluir a entrega de ${nomeItem(entrega.catalogoUniformeId)} (tamanho ${entrega.tamanho})` +
        ` para ${nomeTrabalhador(entrega.trabalhadorId)}?` +
        ` ${entrega.quantidade} unidade(s) voltam para o estoque da obra. Esta ação não pode ser desfeita.`,
      rotuloConfirmar: 'Excluir',
    });
    if (!confirmado) return;
    try {
      setExcluindoId(entrega.id);
      setErro(null);
      await api.entregasUniforme.excluir(entrega.id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir a entrega de uniforme.');
    } finally {
      setExcluindoId(null);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <Dialog open={canhotoAberto} onOpenChange={(_, d) => { if (!d.open) setCanhotoAberto(false); }}>
        <DialogSurface style={{ maxWidth: 480 }}>
          <DialogBody>
            <DialogTitle>Uniformes recebidos — canhoto</DialogTitle>
            <DialogContent>
              {canhotoHtml ? (
                <iframe
                  ref={canhotoFrameRef}
                  title="Canhoto de uniformes recebidos"
                  srcDoc={canhotoHtml}
                  style={{ width: '100%', height: '65vh', border: '1px solid #ddd', background: '#fff' }}
                />
              ) : (
                <div style={{ padding: 48, textAlign: 'center' }}>
                  <Spinner label="Gerando canhoto..." />
                </div>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setCanhotoAberto(false)}>Fechar</Button>
              <Button appearance="primary" icon={<Print24Regular />} disabled={!canhotoHtml} onClick={imprimirCanhoto}>
                Imprimir
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <PageHeader
        titulo="Entrega de Uniforme"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-entrega-uniforme"
          >
            {painelAberto ? 'Fechar' : 'Nova entrega'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div id="painel-nova-entrega-uniforme">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova entrega de uniforme">
          <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 340px', gap: 20, alignItems: 'start' }}>
            <div>
              <FormSection titulo="Quem recebe" numero={1} primeira>
                {erroPainel && (
                  <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                    {erroPainel}
                  </FeedbackInline>
                )}
                <FormGrid>
                  <Campo span={6}>
                    <Field label="Funcionário" required>
                      <SeletorPesquisavel
                        placeholder={`Buscar entre ${trabalhadores.length} funcionários`}
                        opcoes={opcoesTrabalhadores}
                        valor={dadosComuns.trabalhadorId}
                        aoMudar={trocarTrabalhador}
                      />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Data de entrega" required>
                      <CampoData
                        value={dadosComuns.dataEntrega}
                        onChange={(_, d) => setDadosComuns({ ...dadosComuns, dataEntrega: d.value })}
                      />
                    </Field>
                  </Campo>
                </FormGrid>
              </FormSection>

              <FormSection titulo="O que é entregue" numero={2}>
                {!dadosComuns.trabalhadorId ? (
                  <FeedbackInline tom="info">Selecione o funcionário para ver as peças vinculadas à função dele.</FeedbackInline>
                ) : itensPermitidos.length === 0 ? (
                  <FeedbackInline
                    tom="aviso"
                    acao={{
                      rotulo: 'Cadastrar em Matriz por Função',
                      aoClicar: () => {
                        fecharPainel();
                        aoNavegarParaMatriz();
                      },
                    }}
                  >
                    Esta função não tem peças cadastradas na matriz.
                  </FeedbackInline>
                ) : (
                  <SeletorItensUniforme
                    pecasPermitidas={itensPermitidos}
                    tamanhos={tamanhosTrabalhador}
                    carrinho={carrinho}
                    aoAdicionar={adicionarAoCarrinho}
                    aoRemover={removerDoCarrinho}
                    aoCadastrarTamanho={() => navigate(`/pessoas/${dadosComuns.trabalhadorId}`)}
                  />
                )}

                <FormGrid>
                  <Campo span={6}>
                    <Field label="Motivo">
                      <Select
                        value={dadosComuns.motivoTipo}
                        onChange={(_, d) => setDadosComuns({ ...dadosComuns, motivoTipo: Number(d.value) })}
                      >
                        {Object.entries(motivoEntregaUniformeLabel).map(([valor, rotulo]) => (
                          <option key={valor} value={valor}>
                            {rotulo}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Observações (opcional)">
                      <Input
                        value={dadosComuns.observacoes}
                        onChange={(_, d) => setDadosComuns({ ...dadosComuns, observacoes: d.value })}
                      />
                    </Field>
                  </Campo>
                </FormGrid>
                <FormRodape>
                  <Button onClick={fecharPainel}>Cancelar</Button>
                </FormRodape>
              </FormSection>
            </div>

            <CarrinhoEntregaUniforme
              carrinho={carrinho}
              pecas={itensCatalogo}
              carregando={carregando}
              aoRemover={removerDoCarrinho}
              aoImprimirRascunho={imprimirRascunhoCarrinho}
              aoConfirmar={confirmarCarrinho}
            />
          </div>
        </PainelCriacaoInline>
      </div>

      <Card titulo="Entregas registradas" densidade="compacta">
        <DataTable
          aria-label="Entregas de uniforme registradas"
          densidade="compacta"
          colunas={colunas}
          linhas={entregas}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma entrega de uniforme registrada ainda.',
            acao: { rotulo: 'Nova entrega', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(e) => (
            <>
              <BotaoAcao
                tom="ver"
                icon={<Signature24Regular />}
                onClick={() =>
                  setLoteParaAssinar({
                    trabalhadorId: e.trabalhadorId,
                    dataEntrega: e.dataEntrega,
                    itens: [itemLote(e)],
                  })
                }
                aria-label="Assinar esta entrega"
                title="Assinar esta entrega (digital ou facial)"
              />
              {/* Exclusão é privilégio de Administrador (o servidor recusa os demais). */}
              {souAdministrador && !travados.has(e.id) && (
                <BotaoAcao
                  tom="excluir"
                  icon={<Delete24Regular />}
                  disabled={excluindoId === e.id}
                  onClick={() => excluir(e)}
                  aria-label="Excluir entrega de uniforme"
                  title="Excluir esta entrega (somente administrador)"
                />
              )}
            </>
          )}
        />
      </Card>

      {loteParaAssinar && (
        <AssinaturaEntregaUniformeLoteDialog
          open={!!loteParaAssinar}
          onClose={() => setLoteParaAssinar(null)}
          itens={loteParaAssinar.itens}
          obraId={obraIdTrabalhador(loteParaAssinar.trabalhadorId)}
          trabalhadorNome={nomeTrabalhador(loteParaAssinar.trabalhadorId)}
          dataEntrega={loteParaAssinar.dataEntrega}
        />
      )}
    </div>
  );
}
