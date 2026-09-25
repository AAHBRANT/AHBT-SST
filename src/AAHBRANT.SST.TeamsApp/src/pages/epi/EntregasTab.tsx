import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
  Button, Field, Input, Select, CampoData,
  Card, PageHeader, DataTable, StatusChip, nivelVencimento, tomDeVencimento, rotuloDeVencimento,
  PainelCriacaoInline, FormSection, FormGrid, FormRodape, Campo, SeletorPesquisavel, FeedbackInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, ArrowDownload24Regular, Delete24Regular, Eye24Regular, Signature24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaEpiLabel,
  MotivoEntregaEpi,
  MetodoAutenticacaoAssinatura,
  OrigemCertificadoTreinamento,
  type AtualizarEntregaEpi,
  type CatalogoEpi,
  type CursoTreinamento,
  type EntregaEpi,
  type Funcao,
  type NovaEntregaEpi,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';
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
function formatarDataBr(valor?: string | null): string {
  if (!valor) return '—';
  const [ano, mes, dia] = valor.slice(0, 10).split('-');
  return dia && mes && ano ? `${dia}/${mes}/${ano}` : valor.slice(0, 10);
}

// Rede de segurança para curso cadastrado antes da migration que marcou os existentes. A versão
// anterior (`replace(/\D/g, '') === '6'`) recusava praticamente tudo o que o técnico digita de
// verdade: "NR-06" vira "06" (≠ "6") e "NR-06 e NR-18" vira "0618". Era esse o motivo de um
// certificado lançado corretamente não destravar a entrega de EPI (achado em hml, 23/09).
// "NR-16"/"NR-36" continuam de fora: o 6 tem que ser o número da norma, não o fim dele.
function normaMencionaNr6(norma?: string | null): boolean {
  const texto = (norma ?? '').trim();
  if (!texto) return false;
  // Norma que cita uma única NR: "6", "06", "NR-06", "NR 6", "nr06".
  if (/^0*6$/.test(texto.replace(/\D/g, ''))) return true;
  // Norma composta: "NR-06 e NR-18", "NR 06/NR 35".
  return /\bn\.?r\.?\s*-?\s*0*6\b/i.test(texto);
}

// Curso que atende à NR-06 (o que habilita a entrega de EPI). Desde 22/09 vale o marcador explícito
// do Catálogo de Cursos (CursoTreinamento.atendeNr6): antes isto era adivinhado do texto livre de
// normaReferencia, e bastava alguém cadastrar "NR-06 e NR-18" para o curso deixar de ser reconhecido
// e a obra inteira travar na entrega. Recebe tanto um curso do catálogo quanto uma linha de
// certificado — as duas carregam o marcador e a norma.
function ehCursoNr6(curso?: { atendeNr6?: boolean; normaReferencia?: string | null }): boolean {
  if (!curso) return false;
  if (curso.atendeNr6) return true;
  return normaMencionaNr6(curso.normaReferencia);
}

interface EntregasTabProps {
  aoNavegarParaMatriz: () => void;
}

export function EntregasTab({ aoNavegarParaMatriz }: EntregasTabProps) {
  const navigate = useNavigate();
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [episPermitidos, setEpisPermitidos] = useState<CatalogoEpi[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
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
  const [excluindoId, setExcluindoId] = useState<string | null>(null);
  const souAdministrador = useSouAdministrador();
  const { confirmar, dialogElement } = useConfirmar();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [devolucaoId, setDevolucaoId] = useState<string | null>(null);
  const [devolucaoData, setDevolucaoData] = useState('');
  const [devolucaoQtd, setDevolucaoQtd] = useState('');
  const [loteParaAssinar, setLoteParaAssinar] = useState<LoteParaAssinar | null>(null);
  const [devolucaoParaAssinar, setDevolucaoParaAssinar] = useState<EntregaEpi | null>(null);
  const [statusIntegracao, setStatusIntegracao] = useState<StatusIntegracao | null>(null);
  // Validade do treinamento de NR-06 do funcionário selecionado (22/09). Fora de `dadosComuns`
  // porque não é dado da entrega — só o que a trava precisa para recusar NR-06 vencida.
  const [validadeNr6, setValidadeNr6] = useState<string | null>(null);
  // Sem isto, entre escolher o funcionário e a busca responder a tela afirmava que ele "não tem
  // treinamento de NR-06" — alarme falso que faz o técnico recadastrar um treinamento que existe.
  const [verificandoNr6, setVerificandoNr6] = useState(false);
  // Por que a NR-06 não foi encontrada (23/09). A mesma frase servia para "nunca foi lançado" e
  // para "lançado em curso que não habilita EPI", e só a segunda tem solução no Catálogo de cursos.
  const [motivoSemNr6, setMotivoSemNr6] = useState<string | null>(null);

  async function carregar() {
    try {
      setCarregandoLista(true);
      setErro(null);
      const [lista, listaEpis, listaTrabalhadores, listaObras, listaFuncoes] = await Promise.all([
        api.entregasEpi.listar(),
        api.catalogosEpi.listar(),
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.funcoes.listar(),
      ]);
      setEntregas(lista);
      setEpis(listaEpis);
      setTrabalhadores(listaTrabalhadores);
      setObras(listaObras);
      setFuncoes(listaFuncoes);
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

  // Pedido do usuário (03/09, travado em 22/09): nº da lista de presença e data do treinamento de
  // NR-06 nunca são digitados à mão — vêm só do treinamento mais recente cadastrado no módulo de
  // Treinamentos para o funcionário selecionado. Os dois campos ficam desabilitados na tela (evita
  // divergência entre o que está na ficha de EPI e o que está realmente cadastrado); se não houver
  // treinamento de NR-06, os campos ficam vazios e `confirmarCarrinho()` bloqueia o registro.
  // Também verifica aqui (mesma lista de treinamentos já buscada) se a Integração de Segurança está
  // em dia e assinada pelo próprio trabalhador (pedido do usuário, 21/09) — o backend é quem
  // efetivamente bloqueia; isto só avisa antes de montar o carrinho inteiro à toa.
  useEffect(() => {
    let cancelado = false;
    async function sincronizarDadosTrabalhador() {
      if (!dadosComuns.trabalhadorId) {
        setStatusIntegracao(null);
        setVerificandoNr6(false);
        return;
      }
      setVerificandoNr6(true);
      try {
        // Usa a lista de Certificados (e não `treinamentos.listar`) porque só ela traz, por
        // registro, o marcador do curso, a norma, a origem (Aahbrant/Externo) e se há arquivo
        // anexado — os quatro dados de que as duas travas desta tela precisam. Antes faltavam
        // origem e arquivo, e a tela não tinha como reproduzir a regra do servidor.
        const treinamentosTrabalhador = await api.treinamentos.listarCertificados({
          trabalhadorId: dadosComuns.trabalhadorId,
        });
        if (cancelado) return;

        // Entre dois treinamentos de NR-06, vale o de validade mais longe — e não o de realização
        // mais recente. Com o lançamento retroativo (22/09) o técnico cadastra certificado antigo
        // depois do novo, e ordenar por realização fazia um certificado vencido tomar o lugar do
        // válido e bloquear a entrega.
        const treinamentoNr6 = treinamentosTrabalhador
          .filter((t) => ehCursoNr6(t))
          .sort((a, b) => b.dataValidade.localeCompare(a.dataValidade))[0];
        setValidadeNr6(treinamentoNr6?.dataValidade?.slice(0, 10) ?? null);
        if (treinamentoNr6) {
          setMotivoSemNr6(null);
          setDadosComuns((atual) =>
            atual.trabalhadorId === treinamentoNr6.trabalhadorId
              ? {
                  ...atual,
                  numeroListaPresencaNr6: treinamentoNr6.numeroCertificado ?? '',
                  dataTreinamentoNr6: treinamentoNr6.dataRealizacao.slice(0, 10),
                }
              : atual,
          );
        } else {
          // Distinguir os dois casos é o que faltava para o técnico resolver sozinho: "não lancei o
          // certificado" e "lancei, mas o curso dele não está marcado como Habilita EPI no Catálogo"
          // apareciam com a mesma frase, e no segundo o certificado estava lá na tela de
          // Treinamentos, o que fazia a mensagem parecer mentira (hml, 23/09).
          setMotivoSemNr6(
            treinamentosTrabalhador.length === 0
              ? 'Este funcionário não tem nenhum treinamento cadastrado. Lance o certificado de NR-06 em Treinamentos › Certificados antes de registrar a entrega de EPI.'
              : 'Este funcionário tem treinamento cadastrado, mas nenhum em curso marcado como "Habilita EPI (NR-06)". Abra Treinamentos › Catálogo de cursos e marque o curso de NR-06 — ou lance o certificado no curso correto.',
          );
        }

        // Integração de Segurança: verificada depois da NR-06, e nunca antes. Sem curso de
        // Integração marcado no catálogo o backend não bloqueia nada de propósito (ver
        // GarantirIntegracaoSegurancaAssinadaAsync) — mas esta função saía aqui, antes de buscar os
        // treinamentos, e a tela acusava falta de NR-06 em obra que só não tinha configurado o
        // curso de Integração. O `return` agora é dentro do try, então o `finally` roda e a tela
        // não fica presa em "Verificando…".
        const cursoIntegracao = cursos.find((c) => c.ehIntegracaoSeguranca);
        if (!cursoIntegracao) {
          setStatusIntegracao({ tipo: 'sem-curso' });
          return;
        }

        const hoje = new Date().toISOString().slice(0, 10);
        const integracoesValidas = treinamentosTrabalhador.filter(
          (t) => t.cursoTreinamentoId === cursoIntegracao.id && t.dataValidade.slice(0, 10) >= hoje,
        );
        if (integracoesValidas.length === 0) {
          setStatusIntegracao({ tipo: 'pendente', nomeCurso: cursoIntegracao.nome, temRegistroValido: false });
          return;
        }
        // Mesma exceção do servidor (23/09): obra em andamento tem gente treinada antes de o
        // sistema existir, e essa assinatura nunca vai existir aqui. Certificado externo COM
        // arquivo anexado vale como prova no lugar dela — ver
        // GarantirIntegracaoSegurancaAssinadaAsync em CriarEntregaEpiCommand.
        if (integracoesValidas.some((t) => t.origemCertificado === OrigemCertificadoTreinamento.Externo && t.temArquivo)) {
          setStatusIntegracao({ tipo: 'ok' });
          return;
        }
        // Qualquer um dos registros válidos assinado já libera — são um ou dois por trabalhador.
        const documentos = await Promise.all(
          integracoesValidas.map((t) => api.assinatura.obter('Treinamento', t.id).catch(() => null)),
        );
        if (cancelado) return;
        const trabalhadorAssinou = documentos.some(
          (documento) =>
            documento?.signatarios.some((s) => s.metodoAutenticacao !== MetodoAutenticacaoAssinatura.SessaoLogada) ?? false,
        );
        setStatusIntegracao(
          trabalhadorAssinou ? { tipo: 'ok' } : { tipo: 'pendente', nomeCurso: cursoIntegracao.nome, temRegistroValido: true },
        );
      } catch {
        // Falha ao verificar não deve travar a tela — o backend valida de qualquer forma ao confirmar.
        // Também deixa os campos de NR-06 vazios; `confirmarCarrinho()` bloqueia nesse caso.
        if (!cancelado) setStatusIntegracao(null);
      } finally {
        if (!cancelado) setVerificandoNr6(false);
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

  function catalogoEpi(id: string) {
    return epis.find((e) => e.id === id);
  }

  function epiTemFoto(id: string) {
    return epis.find((e) => e.id === id)?.temFoto ?? false;
  }

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  function trabalhadorPorId(id: string) {
    return trabalhadores.find((t) => t.id === id);
  }

  function nomeObra(id?: string | null) {
    if (!id) return '—';
    const obra = obras.find((o) => o.id === id);
    return obra ? (obra.codigo || obra.nome) : id;
  }

  function nomeFuncao(id?: string | null) {
    if (!id) return '—';
    return funcoes.find((f) => f.id === id)?.nome ?? id;
  }

  function obraIdTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.obraId ?? '';
  }

  // Exclusão é definitiva e só aparece para Administrador (ver PoliticasAutorizacao no backend, que
  // é quem de fato recusa). O servidor devolve ao estoque o que ainda estava com o trabalhador —
  // ver ExcluirEntregaEpiCommand —, por isso a confirmação avisa que o saldo volta.
  async function excluirEntrega(entrega: EntregaEpi) {
    const emPosse = entrega.quantidade - (entrega.quantidadeDevolucao ?? 0);
    const confirmado = await confirmar({
      titulo: 'Excluir entrega de EPI',
      mensagem:
        `Excluir a entrega de ${nomeEpi(entrega.catalogoEpiId)} para ${nomeTrabalhador(entrega.trabalhadorId)}` +
        ` registrada em ${formatarDataBr(entrega.dataEntrega)}?` +
        (emPosse > 0 ? ` ${emPosse} unidade(s) voltam para o estoque da obra.` : '') +
        ' Esta ação não pode ser desfeita.',
      rotuloConfirmar: 'Excluir',
    });
    if (!confirmado) return;
    try {
      setExcluindoId(entrega.id);
      setErro(null);
      await api.entregasEpi.excluir(entrega.id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir a entrega de EPI.');
    } finally {
      setExcluindoId(null);
    }
  }

  function trocarTrabalhador(id: string) {
    // A lista de EPIs permitidos (matriz da função) muda com o funcionário — itens já escolhidos
    // para o funcionário anterior podem nem existir na matriz do novo, então o carrinho é limpo.
    setDadosComuns({ ...dadosComuns, trabalhadorId: id, numeroListaPresencaNr6: '', dataTreinamentoNr6: '' });
    setValidadeNr6(null);
    setMotivoSemNr6(null);
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
    // Pedido do usuário (22/09): nº da lista de presença e data do treinamento de NR-06 deixaram de
    // ser digitáveis (só vêm do treinamento cadastrado, ver useEffect sincronizarDadosTrabalhador
    // acima) — sem essa checagem aqui, um funcionário sem NR-06 cadastrada geraria uma ficha de EPI
    // incompleta e sem chance de corrigir depois (os campos ficam travados na tela também).
    // A checagem é só pela DATA, e não pelo número: certificado antigo de instituição externa
    // frequentemente não tem numeração, e exigir o número travava a entrega de EPI de quem tem o
    // treinamento em dia (achado no lançamento retroativo, 22/09).
    if (!dadosComuns.dataTreinamentoNr6) {
      setErroPainel(
        motivoSemNr6 ??
          'Este funcionário não tem treinamento de NR-06 cadastrado. Cadastre o treinamento em Treinamentos › Certificados antes de registrar a entrega de EPI.',
      );
      return;
    }
    // NR-06 vencida não habilita entrega de EPI: treinamento fora da validade é o mesmo que não ter
    // treinamento perante a fiscalização (decisão do usuário, 22/09).
    if (nivelVencimento(validadeNr6) === 'vencido') {
      setErroPainel(
        `O treinamento de NR-06 deste funcionário está vencido (validade ${formatarDataBr(validadeNr6)}). Registre o novo treinamento em Treinamentos antes de entregar EPI.`,
      );
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
  async function imprimirRascunhoCarrinho() {
    if (!dadosComuns.trabalhadorId || carrinho.length === 0) return;
    const janela = window.open('', '_blank', 'width=420,height=720');
    if (!janela) return;
    janela.document.write('<!DOCTYPE html><html lang="pt-br"><head><meta charset="UTF-8"><title>EPIs recebidos</title></head><body>Gerando canhoto...</body></html>');
    janela.document.close();

    function blobParaDataUrl(blob: Blob): Promise<string> {
      return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(blob);
      });
    }

    async function fotoItem(epi?: CatalogoEpi): Promise<string | null> {
      if (!epi?.temFoto) return null;
      try {
        return await blobParaDataUrl(await api.catalogosEpi.baixarFoto(epi.id));
      } catch {
        return null;
      }
    }

    const trabalhador = trabalhadorPorId(dadosComuns.trabalhadorId);
    const nomeFunc = escapeHtml(trabalhador?.nome ?? nomeTrabalhador(dadosComuns.trabalhadorId));
    const dataFmt = dadosComuns.dataEntrega ? dadosComuns.dataEntrega.split('-').reverse().join('/') : '—';
    const dataTreinamentoFmt = dadosComuns.dataTreinamentoNr6 ? dadosComuns.dataTreinamentoNr6.split('-').reverse().join('/') : '—';
    const emitidoEm = new Date().toLocaleString('pt-BR');
    const motivo = motivoEntregaEpiLabel[dadosComuns.motivoTipo] ?? '—';
    const observacaoMotivo = dadosComuns.motivo || '—';
    const observacoes = dadosComuns.observacoes || '—';
    const vistoResponsavel = dadosComuns.vistoConsorcioResponsavel || '—';
    const numeroLista = dadosComuns.numeroListaPresencaNr6 || '—';
    const linhas = (await Promise.all(carrinho
      .map(async (item) => {
        const epi = catalogoEpi(item.catalogoEpiId);
        const foto = await fotoItem(epi);
        const validade = item.dataValidade ? item.dataValidade.split('-').reverse().join('/') : '—';
        const ca = epi?.certificadoAprovacaoNumero || '—';
        const validadeCa = epi?.certificadoAprovacaoValidade ? epi.certificadoAprovacaoValidade.slice(0, 10).split('-').reverse().join('/') : '—';
        const fabricante = epi?.fabricante || '—';
        return `<div class="item">
          ${foto ? `<img class="item-foto" src="${foto}" alt="">` : ''}
          <div class="item-conteudo">
            <div class="item-nome">${escapeHtml(epi?.nome ?? nomeEpi(item.catalogoEpiId))}</div>
            <div class="item-linha"><span>Fabricante: ${escapeHtml(fabricante)}</span><span>Qtd: ${item.quantidade}</span></div>
            <div class="item-linha"><span>CA: ${escapeHtml(ca)}</span><span>Val. CA: ${validadeCa}</span></div>
            <div class="item-linha"><span>Val. entrega: ${validade}</span></div>
          </div>
        </div>`;
      })
    )).join('');
    janela.document.write(`<!DOCTYPE html><html lang="pt-br"><head><meta charset="UTF-8"><title>Canhoto de recibo - EPI</title>
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
        <h1>EPIs recebidos</h1>
        <div class="subtitulo">Documento de conferência do carrinho · Emitido em ${emitidoEm}</div>

        <h2>Identificação</h2>
        <div class="campo"><b>Funcionário</b><span>${nomeFunc}</span></div>
        <div class="campo"><b>Função</b><span>${escapeHtml(nomeFuncao(trabalhador?.funcaoId))}</span></div>
        <div class="campo"><b>Obra</b><span>${escapeHtml(nomeObra(trabalhador?.obraId))}</span></div>
        <div class="campo"><b>Data</b><span>${dataFmt}</span></div>

        <h2>Itens do carrinho</h2>
        ${linhas}

        <h2>Documentação e motivo</h2>
        <div class="campo"><b>Motivo</b><span>${escapeHtml(motivo)}</span></div>
        <div class="campo"><b>Obs. motivo</b><span>${escapeHtml(observacaoMotivo)}</span></div>
        <div class="campo"><b>Lista NR-6</b><span>${escapeHtml(numeroLista)}</span></div>
        <div class="campo"><b>Trein. NR-6</b><span>${dataTreinamentoFmt}</span></div>
        <div class="campo"><b>Responsável</b><span>${escapeHtml(vistoResponsavel)}</span></div>
        <div class="campo"><b>Obs.</b><span>${escapeHtml(observacoes)}</span></div>

        <p class="aviso">Comprovante de conferência prévia. A ficha oficial é emitida após confirmar e assinar eletronicamente a entrega.</p>
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
      salvarBlob(await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId), `ficha-epi-${trabalhadorId}.pdf`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ficha em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  // Mesma ficha do "Baixar", aberta na janela de visualização.
  function visualizarFicha(trabalhadorId: string) {
    void visualizar({
      titulo: `Ficha de EPI — ${nomeTrabalhador(trabalhadorId)}`,
      nomeArquivo: `ficha-epi-${trabalhadorId}.pdf`,
      obter: () => api.entregasEpi.baixarFichaTrabalhador(trabalhadorId),
    });
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
      {dialogElement}
      {dialogoVisualizador}
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
                    {' '}— a entrega de EPI fica bloqueada até isso ser resolvido em Treinamentos.
                    {' '}Se o treinamento foi feito antes de a obra entrar no sistema, lance-o em Treinamentos › Certificados
                    {' '}como certificado externo e anexe o arquivo — isso vale como prova no lugar da assinatura.
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
                {/* Avisa ao escolher o funcionário, não só ao confirmar: no canteiro o técnico
                    precisa saber de cara que a entrega não vai sair e o que resolver. */}
                {dadosComuns.trabalhadorId && verificandoNr6 && (
                  <FeedbackInline tom="info">Verificando o treinamento de NR-06 deste funcionário…</FeedbackInline>
                )}
                {dadosComuns.trabalhadorId && !verificandoNr6 && !dadosComuns.dataTreinamentoNr6 && (
                  <FeedbackInline tom="erro">
                    {motivoSemNr6 ??
                      'Este funcionário não tem treinamento de NR-06 cadastrado. Cadastre em Treinamentos › Certificados antes de registrar a entrega de EPI.'}
                  </FeedbackInline>
                )}
                {dadosComuns.dataTreinamentoNr6 && nivelVencimento(validadeNr6) === 'vencido' && (
                  <FeedbackInline tom="erro">
                    O treinamento de NR-06 deste funcionário venceu em {formatarDataBr(validadeNr6)}. Registre o novo
                    treinamento antes de entregar EPI.
                  </FeedbackInline>
                )}
                {dadosComuns.dataTreinamentoNr6 && nivelVencimento(validadeNr6) === 'alerta' && (
                  <FeedbackInline tom="aviso">
                    O treinamento de NR-06 deste funcionário vence em {formatarDataBr(validadeNr6)}. A entrega é
                    permitida, mas programe a reciclagem.
                  </FeedbackInline>
                )}
                <FormGrid>
                  <Campo span={6}>
                    <Field
                      label="Nº lista de presença"
                      hint="Puxado do treinamento de NR-06 cadastrado — não editável. Fica vazio quando o certificado não tem numeração, o que não impede a entrega."
                    >
                      <Input value={dadosComuns.numeroListaPresencaNr6} disabled />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field
                      label="Data do treinamento"
                      hint="Puxado automaticamente do treinamento de NR-06 cadastrado — não editável."
                    >
                      <CampoData value={dadosComuns.dataTreinamentoNr6} onChange={() => {}} disabled />
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
              <BotaoAcao
                tom="ver"
                icon={<Signature24Regular />}
                onClick={() => navigate(`/epi/${e.id}/assinar`)}
                aria-label="Assinar ficha"
                title="Assinar ficha"
              />
              <BotaoAcao
                tom="ver"
                icon={<Eye24Regular />}
                onClick={() => visualizarFicha(e.trabalhadorId)}
                aria-label="Visualizar ficha do funcionário"
                title="Visualizar ficha de EPI do funcionário"
              />
              <BotaoAcao
                tom="baixar"
                icon={<ArrowDownload24Regular />}
                onClick={() => baixarFicha(e.trabalhadorId)}
                disabled={baixandoId === e.trabalhadorId}
                aria-label="Baixar ficha do funcionário"
                title="Baixar ficha de EPI do funcionário em PDF"
              />
              {/* Só Administrador (pedido do usuário, 23/09): o servidor recusa a exclusão de quem
                  não for, então mostrar o botão para os outros só geraria erro na cara do técnico. */}
              {souAdministrador && (
                <BotaoAcao
                  tom="excluir"
                  icon={<Delete24Regular />}
                  onClick={() => excluirEntrega(e)}
                  disabled={excluindoId === e.id}
                  aria-label="Excluir entrega"
                  title="Excluir esta entrega (somente administrador)"
                />
              )}
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
