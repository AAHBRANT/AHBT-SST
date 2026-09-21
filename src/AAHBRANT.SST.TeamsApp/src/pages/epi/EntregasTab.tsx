import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button, Field, Input, Select, CampoData,
  Card, PageHeader, DataTable, StatusChip, nivelVencimento, tomDeVencimento, rotuloDeVencimento,
  PainelCriacaoInline, FormSection, FormGrid, FormRodape, Campo, SeletorPesquisavel, FeedbackInline,
  type Coluna,
} from '@ui';
import { Add24Regular, ArrowDownload24Regular, Signature24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaEpiLabel,
  MotivoEntregaEpi,
  MetodoAutenticacaoAssinatura,
  type AtualizarEntregaEpi,
  type CatalogoEpi,
  type CursoTreinamento,
  type EntregaEpi,
  type NovaEntregaEpi,
  type Trabalhador,
} from '../../lib/api';
import { AssinaturaEntregaEpiLoteDialog, type ItemLoteAssinaturaEpi } from '../../components/assinatura/AssinaturaEntregaEpiLoteDialog';
import { AssinaturaDevolucaoEpiDialog } from '../../components/assinatura/AssinaturaDevolucaoEpiDialog';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';
import { SeletorItensEpi, type ItemCarrinhoEpi } from './SeletorItensEpi';
import { CarrinhoEntregaEpi } from './CarrinhoEntregaEpi';

// Campos aplicados a todas as entregas do carrinho de uma vez (mesmo funcionário, mesma data,
// mesmo motivo/documentação NR-6) — o que varia por item é só EPI/quantidade/validade, tratado em
// ItemCarrinhoEpi (SeletorItensEpi.tsx).
interface CamposComunsEntrega {
  trabalhadorId: string;
  dataEntrega: string;
  vistoConsorcioResponsavel: string;
  motivo: string;
  observacoes: string;
  motivoTipo: number;
  numeroListaPresencaNr6: string;
  dataTreinamentoNr6: string;
}

function camposComunsVazios(): CamposComunsEntrega {
  return {
    trabalhadorId: '',
    dataEntrega: new Date().toISOString().slice(0, 10),
    vistoConsorcioResponsavel: '',
    motivo: '',
    observacoes: '',
    motivoTipo: MotivoEntregaEpi.Inicial,
    numeroListaPresencaNr6: '',
    dataTreinamentoNr6: '',
  };
}

interface LoteParaAssinar {
  trabalhadorId: string;
  dataEntrega: string;
  numeroListaPresencaNr6?: string | null;
  dataTreinamentoNr6?: string | null;
  itens: ItemLoteAssinaturaEpi[];
}

function escapeHtml(texto: string): string {
  return texto.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c] ?? c);
}

// Bloqueio pedido pelo usuário (21/09): sem a Integração de Segurança assinada, o funcionário não
// pode receber EPI — o backend já recusa (CriarEntregaEpiCommand), isto aqui só avisa antes, para
// não deixar montar o carrinho inteiro pra descobrir o bloqueio só na hora de confirmar.
type StatusIntegracao =
  | { tipo: 'sem-curso' }
  | { tipo: 'ok' }
  | { tipo: 'pendente'; nomeCurso: string; temRegistroValido: boolean };

// Entregas de EPI do módulo dedicado /epi — registro (em carrinho: vários itens de uma vez para o
// mesmo funcionário, assinados numa única interação — spec pedida pelo usuário, 19-21/09), devolução
// (repõe estoque no backend), ficha em PDF e atalho para a assinatura eletrônica. Primeira página na
// camada ui/ (piloto 1 da Onda 1, spec §5.1): nada de Fluent cru nem de pageStyles aqui — lista em
// DataTable com chips de vencimento e o formulário de criação em PainelCriacaoInline, que cresce
// acima da lista em vez de cobrir a tela com um drawer (migração de 2026-09-11, mesmo padrão de
// AtividadesTab.tsx/InspecoesTab.tsx). O bloqueio de estoque insuficiente / CA vencido acontece no
// backend (CriarEntregaEpiCommand, chamado uma vez por item do carrinho); o erro retornado é exibido
// como veio, mesmo padrão já usado em todo o resto do frontend (ver api.ts request()).
// Decisão de escopo do carrinho (confirmada com o usuário): sem mudar o banco — cada item do
// carrinho ainda vira uma EntregaEpi individual via o endpoint que já existe, só que a tela deixa de
// mandar uma de cada vez. Ver AssinaturaEntregaEpiLoteDialog.tsx para como a assinatura única cobre
// as N entregas por trás.
// "NR-06"/"NR-6"/"NR 06" etc. — compara só o número, não o formato exato do texto cadastrado no
// curso (ver CursoTreinamento.normaReferencia, campo livre).
function ehNormaNr6(normaReferencia?: string | null): boolean {
  return (normaReferencia ?? '').replace(/\D/g, '') === '6';
}

interface EntregasTabProps {
  aoNavegarParaMatriz: () => void;
}

export function EntregasTab({ aoNavegarParaMatriz }: EntregasTabProps) {
  const navigate = useNavigate();
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [episPermitidos, setEpisPermitidos] = useState<CatalogoEpi[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [dadosComuns, setDadosComuns] = useState<CamposComunsEntrega>(camposComunsVazios());
  const [carrinho, setCarrinho] = useState<ItemCarrinhoEpi[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  // Erro do formulário de criação fica separado do erro da lista: são estados independentes, um
  // erro de carga da lista não deveria fechar/limpar o erro de validação do formulário de criação
  // (estoque insuficiente, CA vencido, campos obrigatórios), nem vice-versa.
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [devolucaoId, setDevolucaoId] = useState<string | null>(null);
  const [devolucaoData, setDevolucaoData] = useState('');
  const [devolucaoQtd, setDevolucaoQtd] = useState('');
  const [loteParaAssinar, setLoteParaAssinar] = useState<LoteParaAssinar | null>(null);
  const [devolucaoParaAssinar, setDevolucaoParaAssinar] = useState<EntregaEpi | null>(null);
  const [statusIntegracao, setStatusIntegracao] = useState<StatusIntegracao | null>(null);

  async function carregar() {
    try {
      setCarregandoLista(true);
      setErro(null);
      const [lista, listaEpis, listaTrabalhadores] = await Promise.all([
        api.entregasEpi.listar(),
        api.catalogosEpi.listar(),
        api.trabalhadores.listar(),
      ]);
      setEntregas(lista);
      setEpis(listaEpis);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de EPI.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    api.cursosTreinamento.listar().then(setCursos).catch(() => setCursos([]));
  }, []);

  // Pedido do usuário (03/09): não faz sentido digitar manualmente o nº da lista de presença e a
  // data do treinamento de NR-06 se o funcionário já tem esse treinamento cadastrado no módulo de
  // Treinamentos — busca automaticamente o mais recente ao trocar de funcionário (o usuário ainda
  // pode sobrescrever os campos à mão, ex.: quando o treinamento ainda não foi cadastrado no sistema).
  // Também verifica aqui (mesma lista de treinamentos já buscada) se a Integração de Segurança está
  // em dia e assinada pelo próprio trabalhador (pedido do usuário, 21/09) — o backend é quem
  // efetivamente bloqueia; isto só avisa antes de montar o carrinho inteiro à toa.
  useEffect(() => {
    let cancelado = false;
    async function sincronizarDadosTrabalhador() {
      if (!dadosComuns.trabalhadorId) {
        setStatusIntegracao(null);
        return;
      }
      const cursoIntegracao = cursos.find((c) => c.ehIntegracaoSeguranca);
      if (!cursoIntegracao) {
        setStatusIntegracao({ tipo: 'sem-curso' });
        return;
      }
      try {
        const treinamentosTrabalhador = await api.treinamentos.listar(dadosComuns.trabalhadorId);
        if (cancelado) return;

        const treinamentoNr6 = treinamentosTrabalhador
          .filter((t) => ehNormaNr6(cursos.find((c) => c.id === t.cursoTreinamentoId)?.normaReferencia))
          .sort((a, b) => b.dataRealizacao.localeCompare(a.dataRealizacao))[0];
        if (treinamentoNr6) {
          setDadosComuns((atual) =>
            atual.trabalhadorId === treinamentoNr6.trabalhadorId
              ? {
                  ...atual,
                  numeroListaPresencaNr6: treinamentoNr6.numeroCertificado ?? '',
                  dataTreinamentoNr6: treinamentoNr6.dataRealizacao.slice(0, 10),
                }
              : atual,
          );
        }

        const hoje = new Date().toISOString().slice(0, 10);
        const treinamentoIntegracao = treinamentosTrabalhador
          .filter((t) => t.cursoTreinamentoId === cursoIntegracao.id && t.dataValidade.slice(0, 10) >= hoje)
          .sort((a, b) => b.dataRealizacao.localeCompare(a.dataRealizacao))[0];
        if (!treinamentoIntegracao) {
          setStatusIntegracao({ tipo: 'pendente', nomeCurso: cursoIntegracao.nome, temRegistroValido: false });
          return;
        }
        const documento = await api.assinatura.obter('Treinamento', treinamentoIntegracao.id);
        if (cancelado) return;
        const trabalhadorAssinou =
          documento?.signatarios.some((s) => s.metodoAutenticacao !== MetodoAutenticacaoAssinatura.SessaoLogada) ?? false;
        setStatusIntegracao(
          trabalhadorAssinou ? { tipo: 'ok' } : { tipo: 'pendente', nomeCurso: cursoIntegracao.nome, temRegistroValido: true },
        );
      } catch {
        // Falha ao verificar não deve travar a tela — o backend valida de qualquer forma ao confirmar.
        if (!cancelado) setStatusIntegracao(null);
      }
    }
    sincronizarDadosTrabalhador();
    return () => {
      cancelado = true;
    };
  }, [dadosComuns.trabalhadorId, cursos]);

  useEffect(() => {
    let cancelado = false;
    async function carregarEpisPermitidos() {
      const trabalhador = trabalhadores.find((t) => t.id === dadosComuns.trabalhadorId);
      if (!trabalhador) {
        setEpisPermitidos([]);
        return;
      }
      try {
        const lista = await api.funcoes.listarEpis(trabalhador.funcaoId);
        if (!cancelado) setEpisPermitidos(lista);
      } catch {
        if (!cancelado) setEpisPermitidos([]);
      }
    }
    carregarEpisPermitidos();
    return () => {
      cancelado = true;
    };
  }, [dadosComuns.trabalhadorId, trabalhadores]);

  // SeletorPesquisavel pede a lista memoizada (a de trabalhadores é a maior do app).
  const opcoesTrabalhadores = useMemo(
    () => trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome, descricao: t.matricula ?? undefined })),
    [trabalhadores],
  );

  function nomeEpi(id: string) {
    return epis.find((e) => e.id === id)?.nome ?? id;
  }

  function epiTemFoto(id: string) {
    return epis.find((e) => e.id === id)?.temFoto ?? false;
  }

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  function obraIdTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.obraId ?? '';
  }

  function trocarTrabalhador(id: string) {
    // A lista de EPIs permitidos (matriz da função) muda com o funcionário — itens já escolhidos
    // para o funcionário anterior podem nem existir na matriz do novo, então o carrinho é limpo.
    setDadosComuns({ ...dadosComuns, trabalhadorId: id, numeroListaPresencaNr6: '', dataTreinamentoNr6: '' });
    setCarrinho([]);
    // Evita mostrar por um instante o status de integração do funcionário anterior até o efeito
    // que verifica o novo terminar de carregar.
    setStatusIntegracao(null);
  }

  function adicionarAoCarrinho(item: ItemCarrinhoEpi) {
    setCarrinho((atual) => [...atual.filter((i) => i.catalogoEpiId !== item.catalogoEpiId), item]);
    setErroPainel(null);
  }

  function removerDoCarrinho(catalogoEpiId: string) {
    setCarrinho((atual) => atual.filter((i) => i.catalogoEpiId !== catalogoEpiId));
  }

  // Confirma todo o carrinho: cria uma EntregaEpi por item, em sequência (o backend não tem uma
  // transação em lote — não existe hoje um endpoint que crie N entregas atomicamente). Se um item
  // falhar (estoque insuficiente, CA vencido) no meio do caminho, os anteriores já foram gravados e
  // já debitaram estoque; o carrinho é ajustado para mostrar só o que falta, com um aviso claro de
  // quantos itens já entraram antes do erro.
  async function confirmarCarrinho() {
    if (!dadosComuns.trabalhadorId || !dadosComuns.dataEntrega) {
      setErroPainel('Selecione o funcionário e a data de entrega.');
      return;
    }
    if (carrinho.length === 0) {
      setErroPainel('Adicione ao menos um EPI ao carrinho antes de confirmar.');
      return;
    }
    setCarregando(true);
    setErroPainel(null);
    const criados: { item: ItemCarrinhoEpi; id: string }[] = [];
    try {
      for (const item of carrinho) {
        const payload: NovaEntregaEpi = {
          trabalhadorId: dadosComuns.trabalhadorId,
          catalogoEpiId: item.catalogoEpiId,
          dataEntrega: dadosComuns.dataEntrega,
          dataDevolucao: null,
          dataValidade: item.dataValidade || null,
          quantidade: item.quantidade,
          vistoConsorcioResponsavel: dadosComuns.vistoConsorcioResponsavel || null,
          motivo: dadosComuns.motivo || null,
          observacoes: dadosComuns.observacoes || null,
          motivoTipo: dadosComuns.motivoTipo,
          numeroListaPresencaNr6: dadosComuns.numeroListaPresencaNr6 || null,
          dataTreinamentoNr6: dadosComuns.dataTreinamentoNr6 || null,
        };
        const { id } = await api.entregasEpi.criar(payload);
        criados.push({ item, id });
      }

      const loteConfirmado: LoteParaAssinar = {
        trabalhadorId: dadosComuns.trabalhadorId,
        dataEntrega: dadosComuns.dataEntrega,
        numeroListaPresencaNr6: dadosComuns.numeroListaPresencaNr6,
        dataTreinamentoNr6: dadosComuns.dataTreinamentoNr6,
        itens: criados.map(({ item, id }) => ({
          entregaId: id,
          catalogoEpiId: item.catalogoEpiId,
          epiNome: nomeEpi(item.catalogoEpiId),
          epiTemFoto: epiTemFoto(item.catalogoEpiId),
          quantidade: item.quantidade,
        })),
      };
      setCarrinho([]);
      setDadosComuns(camposComunsVazios());
      fecharPainel();
      await carregar();
      setLoteParaAssinar(loteConfirmado);
    } catch (e) {
      const itemFalho = carrinho[criados.length];
      const mensagem = e instanceof Error ? e.message : 'Falha ao registrar entrega.';
      if (criados.length > 0) {
        setErroPainel(
          `${criados.length} de ${carrinho.length} itens foram registrados antes do erro em "${nomeEpi(itemFalho?.catalogoEpiId ?? '')}": ${mensagem} ` +
            'Os itens já registrados permanecem no sistema — o carrinho foi ajustado para mostrar só o que falta.',
        );
        const idsCriados = new Set(criados.map((c) => c.item.catalogoEpiId));
        setCarrinho((atual) => atual.filter((i) => !idsCriados.has(i.catalogoEpiId)));
        await carregar();
      } else {
        setErroPainel(`Falha ao registrar "${nomeEpi(itemFalho?.catalogoEpiId ?? '')}": ${mensagem}`);
      }
    } finally {
      setCarregando(false);
    }
  }

  // Recibo de conferência do carrinho antes de confirmar — gerado 100% no navegador (sem endpoint
  // novo no backend), pedido do usuário a partir do mockup fornecido. Deixa claro que não substitui
  // a ficha oficial (essa só existe depois de "Confirmar entrega" + assinatura eletrônica).
  function imprimirRascunhoCarrinho() {
    if (!dadosComuns.trabalhadorId || carrinho.length === 0) return;
    const nomeFunc = escapeHtml(nomeTrabalhador(dadosComuns.trabalhadorId));
    const dataFmt = dadosComuns.dataEntrega ? dadosComuns.dataEntrega.split('-').reverse().join('/') : '—';
    const linhas = carrinho
      .map((item) => {
        const validade = item.dataValidade ? item.dataValidade.split('-').reverse().join('/') : '—';
        return `<tr><td>${escapeHtml(nomeEpi(item.catalogoEpiId))}</td><td style="text-align:right">${item.quantidade}</td><td>${validade}</td></tr>`;
      })
      .join('');
    const janela = window.open('', '_blank', 'width=720,height=900');
    if (!janela) return;
    janela.document.write(`<!DOCTYPE html><html lang="pt-br"><head><meta charset="UTF-8"><title>Canhoto de recibo - EPI</title>
      <style>
        body { font-family: Arial, sans-serif; padding: 28px; color: #1a1a1a; }
        h1 { font-size: 18px; color: #670000; margin-bottom: 4px; }
        .subtitulo { font-size: 12px; color: #555; margin-bottom: 20px; }
        table { width: 100%; border-collapse: collapse; margin-top: 12px; }
        th, td { border: 1px solid #ccc; padding: 8px; text-align: left; font-size: 13px; }
        th { background: #ebe9ad; }
        .assinatura { margin-top: 64px; border-top: 1px solid #000; width: 320px; padding-top: 4px; font-size: 12px; }
        .aviso { margin-top: 28px; font-size: 11px; color: #666; }
      </style></head>
      <body>
        <h1>Canhoto de Recibo — Entrega de EPI</h1>
        <div class="subtitulo">Funcionário: ${nomeFunc} · Data de entrega: ${dataFmt}</div>
        <table>
          <thead><tr><th>EPI</th><th>Qtd.</th><th>Validade</th></tr></thead>
          <tbody>${linhas}</tbody>
        </table>
        <div class="assinatura">Assinatura do recebedor</div>
        <p class="aviso">Documento de conferência gerado antes da confirmação da entrega — não substitui a ficha oficial assinada eletronicamente, emitida após "Confirmar entrega".</p>
      </body></html>`);
    janela.document.close();
    janela.focus();
    janela.print();
  }

  // Todo caminho de fechar o painel limpa o carrinho e o erro do formulário — senão reabrir mostra
  // itens/mensagem velhos.
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setCarrinho([]);
    setDadosComuns(camposComunsVazios());
  }

  function iniciarDevolucao(entrega: EntregaEpi) {
    setDevolucaoId(entrega.id);
    setDevolucaoData(new Date().toISOString().slice(0, 10));
    setDevolucaoQtd(String(entrega.quantidade));
  }

  async function confirmarDevolucao(entrega: EntregaEpi) {
    try {
      setCarregando(true);
      setErro(null);
      const atualizada: AtualizarEntregaEpi = {
        ...entrega,
        // Entregas registradas antes da Fase 2 podem não ter motivoTipo (campo era opcional);
        // o backend exige o enum em toda atualização, então assume Inicial como padrão seguro.
        motivoTipo: entrega.motivoTipo ?? MotivoEntregaEpi.Inicial,
        dataDevolucao: devolucaoData,
        quantidadeDevolucao: Number(devolucaoQtd) || entrega.quantidade,
      };
      await api.entregasEpi.atualizar(atualizada);
      setDevolucaoId(null);
      setDevolucaoParaAssinar(atualizada);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar devolução.');
    } finally {
      setCarregando(false);
    }
  }

  async function baixarFicha(trabalhadorId: string) {
    try {
      setBaixandoId(trabalhadorId);
      const blob = await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ficha-epi-${trabalhadorId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ficha em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  // Coluna de devolução com edição na própria linha: o DataTable não tem edição inline embutida
  // (e não deveria — é caso desta página), então o estado de devolução entra pelo `render`.
  const colunas: Coluna<EntregaEpi>[] = [
    { chave: 'trabalhador', rotulo: 'Funcionário', render: (e) => nomeTrabalhador(e.trabalhadorId) },
    {
      chave: 'epi',
      rotulo: 'EPI',
      render: (e) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <FotoCatalogoEpi catalogoEpiId={e.catalogoEpiId} temFoto={epiTemFoto(e.catalogoEpiId)} tamanho={28} />
          {nomeEpi(e.catalogoEpiId)}
        </div>
      ),
    },
    { chave: 'quantidade', rotulo: 'Qtd.', alinhar: 'direita', largura: '64px' },
    { chave: 'dataEntrega', rotulo: 'Entrega', render: (e) => e.dataEntrega?.slice(0, 10) },
    {
      chave: 'validade',
      rotulo: 'Validade',
      // Data crua + chip juntos: o rótulo do chip diz o nível, não *quando* vence nem *há quanto*
      // está vencido, e isso é dado de fiscalização (NR-6) que a tela antiga mostrava.
      render: (e) => {
        if (e.dataDevolucao) return <StatusChip tom="neutro">Devolvido {e.dataDevolucao.slice(0, 10)}</StatusChip>;
        const nivel = nivelVencimento(e.dataValidade);
        return (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span>{e.dataValidade?.slice(0, 10) ?? '—'}</span>
            {nivel && <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip>}
          </div>
        );
      },
    },
    {
      chave: 'devolucao',
      rotulo: 'Devolução',
      render: (e) =>
        devolucaoId === e.id ? (
          <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
            <CampoData value={devolucaoData} onChange={(_, d) => setDevolucaoData(d.value)} style={{ width: 130 }} />
            <Input type="number" value={devolucaoQtd} onChange={(_, d) => setDevolucaoQtd(d.value)} style={{ width: 60 }} />
            <Button size="small" appearance="primary" onClick={() => confirmarDevolucao(e)} disabled={carregando}>
              Confirmar
            </Button>
          </div>
        ) : e.dataDevolucao ? null : (
          <Button size="small" appearance="subtle" onClick={() => iniciarDevolucao(e)}>
            Registrar devolução
          </Button>
        ),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="Entregas de EPI"
        subtitulo={`${entregas.filter((e) => !e.dataDevolucao).length} entregas ativas`}
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-entrega-epi"
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

      <div id="painel-nova-entrega-epi">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova entrega de EPI">
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: '1fr 340px',
              gap: 20,
              alignItems: 'start',
            }}
          >
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
                  <FeedbackInline tom="info">Selecione o funcionário para ver os EPIs vinculados à função dele.</FeedbackInline>
                ) : statusIntegracao?.tipo === 'pendente' ? (
                  <FeedbackInline tom="erro">
                    Este funcionário {statusIntegracao.temRegistroValido
                      ? <>tem o treinamento de <b>{statusIntegracao.nomeCurso}</b> registrado, mas ainda não o assinou</>
                      : <>ainda não tem o treinamento de <b>{statusIntegracao.nomeCurso}</b> em dia</>}
                    {' '}— a entrega de EPI fica bloqueada até isso ser resolvido em Pessoas &gt; Treinamentos.
                  </FeedbackInline>
                ) : episPermitidos.length === 0 ? (
                  <FeedbackInline
                    tom="aviso"
                    acao={{
                      rotulo: 'Cadastrar na matriz',
                      aoClicar: () => {
                        fecharPainel();
                        aoNavegarParaMatriz();
                      },
                    }}
                  >
                    Esta função não tem EPIs cadastrados na matriz.
                  </FeedbackInline>
                ) : (
                  <SeletorItensEpi
                    episPermitidos={episPermitidos}
                    carrinho={carrinho}
                    aoAdicionar={adicionarAoCarrinho}
                    aoRemover={removerDoCarrinho}
                  />
                )}

                <FormGrid>
                  <Campo span={6}>
                    <Field label="Motivo">
                      <Select
                        value={dadosComuns.motivoTipo}
                        onChange={(_, d) => setDadosComuns({ ...dadosComuns, motivoTipo: Number(d.value) })}
                      >
                        {Object.entries(motivoEntregaEpiLabel).map(([valor, rotulo]) => (
                          <option key={valor} value={valor}>
                            {rotulo}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Observação do motivo">
                      <Input
                        value={dadosComuns.motivo}
                        onChange={(_, d) => setDadosComuns({ ...dadosComuns, motivo: d.value })}
                      />
                    </Field>
                  </Campo>
                </FormGrid>
              </FormSection>

              <FormSection titulo="Documentação NR-6" numero={3}>
                <FormGrid>
                  <Campo span={6}>
                    <Field label="Nº lista de presença" hint="Preenchido do treinamento de NR-06 cadastrado, se houver.">
                      <Input
                        value={dadosComuns.numeroListaPresencaNr6}
                        onChange={(_, d) => setDadosComuns({ ...dadosComuns, numeroListaPresencaNr6: d.value })}
                      />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Data do treinamento">
                      <CampoData
                        value={dadosComuns.dataTreinamentoNr6}
                        onChange={(_, d) => setDadosComuns({ ...dadosComuns, dataTreinamentoNr6: d.value })}
                      />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Visto do consórcio/responsável">
                      <Input
                        value={dadosComuns.vistoConsorcioResponsavel}
                        onChange={(_, d) => setDadosComuns({ ...dadosComuns, vistoConsorcioResponsavel: d.value })}
                      />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Observações">
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

            <CarrinhoEntregaEpi
              carrinho={carrinho}
              epis={epis}
              carregando={carregando}
              aoRemover={removerDoCarrinho}
              aoImprimirRascunho={imprimirRascunhoCarrinho}
              aoConfirmar={confirmarCarrinho}
            />
          </div>
        </PainelCriacaoInline>
      </div>

      <Card densidade="compacta">
        <DataTable
          aria-label="Entregas de EPI"
          colunas={colunas}
          linhas={entregas}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma entrega registrada',
            descricao: 'Registre a primeira entrega para começar o controle de EPI.',
            acao: { rotulo: 'Nova entrega', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(e) => (
            <>
              <Button
                appearance="subtle"
                size="small"
                icon={<Signature24Regular />}
                onClick={() => navigate(`/epi/${e.id}/assinar`)}
                aria-label="Assinar ficha"
                title="Assinar ficha"
              />
              <Button
                appearance="subtle"
                size="small"
                icon={<ArrowDownload24Regular />}
                onClick={() => baixarFicha(e.trabalhadorId)}
                disabled={baixandoId === e.trabalhadorId}
                aria-label="Baixar ficha do funcionário"
                title="Baixar ficha de EPI do funcionário em PDF"
              />
            </>
          )}
        />
      </Card>

      {loteParaAssinar && (
        <AssinaturaEntregaEpiLoteDialog
          open={!!loteParaAssinar}
          onClose={() => setLoteParaAssinar(null)}
          itens={loteParaAssinar.itens}
          obraId={obraIdTrabalhador(loteParaAssinar.trabalhadorId)}
          trabalhadorNome={nomeTrabalhador(loteParaAssinar.trabalhadorId)}
          dataEntrega={loteParaAssinar.dataEntrega}
          numeroListaPresencaNr6={loteParaAssinar.numeroListaPresencaNr6}
          dataTreinamentoNr6={loteParaAssinar.dataTreinamentoNr6}
        />
      )}

      {devolucaoParaAssinar && (
        <AssinaturaDevolucaoEpiDialog
          open={!!devolucaoParaAssinar}
          onClose={() => setDevolucaoParaAssinar(null)}
          entregaId={devolucaoParaAssinar.id}
          obraId={obraIdTrabalhador(devolucaoParaAssinar.trabalhadorId)}
          trabalhadorNome={nomeTrabalhador(devolucaoParaAssinar.trabalhadorId)}
          epiNome={nomeEpi(devolucaoParaAssinar.catalogoEpiId)}
          quantidadeDevolucao={devolucaoParaAssinar.quantidadeDevolucao ?? devolucaoParaAssinar.quantidade}
          dataDevolucao={devolucaoParaAssinar.dataDevolucao ?? ''}
        />
      )}
    </div>
  );
}
