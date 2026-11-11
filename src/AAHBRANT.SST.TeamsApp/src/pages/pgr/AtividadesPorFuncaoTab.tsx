import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Button, Card, Carregando, EstadoVazio, FeedbackInline, Select, StatusChip, Text, designTokens, tokensUi,
} from '@ui';
import {
  Checkmark16Regular, Info16Regular, Link16Regular, Save16Regular, Sparkle16Regular,
} from '@fluentui/react-icons';
import { api, type CelulaMatriz, type Ghe, type MatrizAtividadesFuncao } from '../../lib/api';
import { podeNaObra, useUsuarioLogado } from '../../lib/UsuarioLogadoContext';

// Aba "Atividades por função" do PGR (mockup aprovado 10/11/2026): a IA sugere quais atividades do GHE
// cada função executa e o Engenheiro de Segurança confirma; a OS passa a listar só as confirmadas.

const chave = (atividadeId: string, funcaoId: string) => `${atividadeId}|${funcaoId}`;
const executaNaBase = (c?: CelulaMatriz) => c?.estado === 'Validada' || c?.estado === 'Pendente';
const dataHora = (iso: string) => new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

// Decisões ainda não salvas, por FUNÇÃO. A decisão é da linha inteira: toda atividade do GHE precisa ficar
// como "executa" ou "não executa" para a função, senão a OS continua pedindo confirmação. Por isso a linha
// editada guarda todas as atividades (true = executa, false = não executa).
type LinhaRascunho = Record<string, boolean>;
type Rascunho = Record<string, LinhaRascunho>;

// "01.01.3.1 Fundações" -> código da EAP em destaque + nome.
function separarCodigo(nome: string): { codigo: string | null; resto: string } {
  const m = /^(\d+(?:\.\d+)+)\s+(.+)$/.exec(nome);
  return m ? { codigo: m[1], resto: m[2] } : { codigo: null, resto: nome };
}

// Pendência para o ponto âmbar da aba: alguma função de algum GHE com sugestão por confirmar ou sem decisão.
// A tela só consulta GHE a GHE (sem endpoint novo): tudo ao abrir o PGR e, depois de salvar ou sugerir, só o
// GHE alterado. Se alguma consulta falhar, o ponto continua aceso ("não foi possível conferir").
export function useAtividadesFuncaoGhes(obraId: string) {
  const [ghes, setGhes] = useState<Ghe[] | null>(null);
  const [pendente, setPendente] = useState(false);
  const [naoConferiu, setNaoConferiu] = useState(false);
  const [falhou, setFalhou] = useState<string | null>(null);
  // Resultado por GHE: true = há pendência, 'falha' = não deu para conferir.
  const porGhe = useRef<Record<string, boolean | 'falha'>>({});

  const recalcular = useCallback(() => {
    const valores = Object.values(porGhe.current);
    setPendente(valores.some((v) => v === true));
    setNaoConferiu(valores.some((v) => v === 'falha'));
  }, []);

  const conferirGhe = useCallback(async (id: string) => {
    try {
      const m = await api.estruturaSst.obterMatrizAtividadesFuncao(obraId, id);
      porGhe.current[id] = m.funcoes.some((f) => f.pendentes > 0 || f.semDecisao > 0);
    } catch {
      porGhe.current[id] = 'falha';
    }
  }, [obraId]);

  const recarregar = useCallback(async () => {
    if (!obraId) return;
    try {
      const lista = await api.estruturaSst.listarGhes(obraId);
      setGhes(lista);
      setFalhou(null);
      porGhe.current = {};
      await Promise.all(lista.map((g) => conferirGhe(g.id)));
      recalcular();
    } catch (e) {
      setFalhou(e instanceof Error ? e.message : 'Falha ao carregar os GHE da obra.');
      setGhes((atual) => atual ?? []);
      setNaoConferiu(true);
    }
  }, [obraId, conferirGhe, recalcular]);

  // Depois de salvar ou sugerir: atualiza só o GHE alterado.
  const atualizarGhe = useCallback(async (id: string) => {
    await conferirGhe(id);
    recalcular();
  }, [conferirGhe, recalcular]);

  useEffect(() => {
    void recarregar();
  }, [recarregar]);

  return { ghes, pendente, naoConferiu, falhou, recarregar, atualizarGhe };
}

function Aviso({ tom, children }: { tom: 'atencao' | 'info' | 'ok'; children: React.ReactNode }) {
  const t = tokensUi.status[tom];
  return (
    <div
      role="status"
      style={{
        display: 'flex', gap: 10, alignItems: 'flex-start', padding: '10px 14px', borderRadius: tokensUi.raio.sm,
        background: t.fundo, color: t.tinta, fontSize: 13, lineHeight: '19px', marginBottom: 12,
      }}
    >
      <Info16Regular style={{ flex: 'none', marginTop: 2 }} />
      <div>{children}</div>
    </div>
  );
}

export function AtividadesPorFuncaoTab({ obraId, ghes, aoAlterar, aoVincularProcedimento }: {
  obraId: string;
  ghes: Ghe[];
  aoAlterar: (gheId: string) => Promise<void>;
  aoVincularProcedimento: (atividadeId: string) => void;
}) {
  const { usuario } = useUsuarioLogado();
  const [gheId, setGheId] = useState<string>(() => ghes[0]?.id ?? '');
  const [matriz, setMatriz] = useState<MatrizAtividadesFuncao | null>(null);
  const [funcaoId, setFuncaoId] = useState<string | null>(null);
  const [rascunhos, setRascunhos] = useState<Record<string, Rascunho>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [sucesso, setSucesso] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [sugerindo, setSugerindo] = useState(false);
  const [recusado, setRecusado] = useState(false);

  // Só o Engenheiro de Segurança desta obra confirma (global ou com a obra na lista). O servidor decide de
  // verdade; a recusa (403) também é tratada abaixo.
  const podeEditarRiscos = podeNaObra(usuario, 'risco:editar', obraId);
  const podeDecidir = !recusado
    && !!usuario && (usuario.engenheiroSegurancaGlobal === true || (usuario.obrasEngenheiroSeguranca ?? []).includes(obraId));
  // Trava contra duplo clique: o estado só atualiza no próximo render, a ref vale na hora.
  const emAndamento = useRef(false);
  const ocupado = sugerindo || salvando;

  // Outra pessoa alterou as atividades (400 do servidor): mostra a mensagem, descarta o rascunho deste
  // GHE (seria aplicado sobre dados que já mudaram) e recarrega a matriz.
  async function tratarConflito(msg: string): Promise<boolean> {
    if (!/Outra pessoa acabou de alterar/i.test(msg)) return false;
    setRascunhos((todos) => ({ ...todos, [gheId]: {} }));
    setErro(`${msg} As marcações não salvas deste GHE foram descartadas.`);
    await carregar(gheId, false);
    await aoAlterar(gheId);
    return true;
  }

  const carregar = useCallback(async (id: string, limparErro = true) => {
    if (!id) return;
    try {
      if (limparErro) setErro(null);
      const m = await api.estruturaSst.obterMatrizAtividadesFuncao(obraId, id);
      setMatriz(m);
      setFuncaoId((atual) => (atual && m.funcoes.some((f) => f.funcaoId === atual) ? atual : (m.funcoes[0]?.funcaoId ?? null)));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar as atividades por função.');
    }
  }, [obraId]);

  useEffect(() => {
    setMatriz(null);
    void carregar(gheId);
  }, [gheId, carregar]);

  const celulas = useMemo(() => {
    const mapa = new Map<string, CelulaMatriz>();
    matriz?.celulas.forEach((c) => mapa.set(chave(c.atividadeId, c.funcaoId), c));
    return mapa;
  }, [matriz]);

  const rascunho = rascunhos[gheId] ?? {};
  const atividades = matriz?.atividades ?? [];

  const celulaDe = (atividadeId: string, fId: string) => celulas.get(chave(atividadeId, fId));
  // Linha como está salva; com `confirmandoSugeridas`, as sugeridas pela IA entram marcadas.
  const linhaBase = (fId: string, confirmandoSugeridas: boolean): LinhaRascunho =>
    Object.fromEntries(atividades.map((a) => {
      const c = celulaDe(a.atividadeId, fId);
      return [a.atividadeId, c?.estado === 'Validada' || (confirmandoSugeridas && c?.estado === 'Pendente')];
    }));
  const linhaDecidida = (fId: string) =>
    atividades.length > 0 && atividades.every((a) => {
      const e = celulaDe(a.atividadeId, fId)?.estado;
      return e === 'Validada' || e === 'Descartada';
    });

  function definirLinha(fId: string, linha: LinhaRascunho | null) {
    setRascunhos((todos) => {
      const atual = { ...(todos[gheId] ?? {}) };
      if (linha) atual[fId] = linha; else delete atual[fId];
      return { ...todos, [gheId]: atual };
    });
  }

  const marcada = (atividadeId: string, fId: string) =>
    fId in rascunho ? rascunho[fId][atividadeId] === true : executaNaBase(celulaDe(atividadeId, fId));

  function alternar(atividadeId: string, fId: string) {
    setSucesso(null);
    const linha = { ...(rascunho[fId] ?? linhaBase(fId, false)) };
    linha[atividadeId] = !linha[atividadeId];
    // Linha já decidida que volta a ser igual à salva: não há o que gravar.
    const igualBase = linhaDecidida(fId) && atividades.every((a) => linha[a.atividadeId] === executaNaBase(celulaDe(a.atividadeId, fId)));
    definirLinha(fId, igualBase ? null : linha);
  }

  const totalAtividades = atividades.length;
  const funcoes = matriz?.funcoes ?? [];
  const funcao = funcoes.find((f) => f.funcaoId === funcaoId) ?? null;

  const executa = (fId: string) => atividades.filter((a) => marcada(a.atividadeId, fId)).length;
  const contagem = (fId: string, estado: CelulaMatriz['estado']) =>
    atividades.filter((a) => celulaDe(a.atividadeId, fId)?.estado === estado).length;

  // Funções com sugestão da IA ainda sem rascunho: é o alcance de "Confirmar todas as sugeridas".
  const funcoesSugeridas = funcoes.filter((f) => f.pendentes > 0 && !(f.funcaoId in rascunho));
  const sugeridasNoGhe = funcoesSugeridas.reduce((t, f) => t + f.pendentes, 0);
  const nuncaAvaliado = matriz !== null && funcoes.length > 0 && funcoes.every((f) => f.pendentes + f.validadas + f.descartadas === 0);
  const funcoesComSugestao = funcoes.filter((f) => f.pendentes > 0);
  const funcoesSemDecisao = funcoes.filter((f) => f.pendentes === 0 && f.semDecisao > 0);
  const haPendencia = funcoesComSugestao.length > 0 || funcoesSemDecisao.length > 0;
  const ultimaDecisao = (matriz?.celulas ?? [])
    .filter((c) => c.validadoEmUtc && c.validadoPorNome)
    .sort((a, b) => (b.validadoEmUtc ?? '').localeCompare(a.validadoEmUtc ?? ''))[0];

  // O que "Salvar" envia: as funções que o engenheiro alterou e, se ainda não decidida, a função aberta na tela
  // (com as sugeridas dela marcadas) quando tem sugestão da IA aguardando. Função que ele não abriu nem alterou nunca é enviada.
  const linhasParaSalvar = useMemo(() => {
    const linhas: Rascunho = { ...rascunho };
    if (funcao && funcao.pendentes > 0 && !(funcao.funcaoId in linhas) && !linhaDecidida(funcao.funcaoId)) {
      linhas[funcao.funcaoId] = linhaBase(funcao.funcaoId, true);
    }
    return linhas;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rascunho, funcao, matriz]);
  const alteracoes = Object.keys(linhasParaSalvar).length;
  const nomeFuncao = (fId: string) => funcoes.find((f) => f.funcaoId === fId)?.nome ?? '';

  async function sugerirComIa() {
    if (emAndamento.current) return;
    emAndamento.current = true;
    setSugerindo(true);
    setErro(null);
    setSucesso(null);
    try {
      const r = await api.estruturaSst.sugerirAtividadesFuncaoComIa(obraId, gheId);
      await carregar(gheId);
      await aoAlterar(gheId);
      setSucesso(
        r.sugeridos > 0
          ? `Sugestões prontas: ${r.sugeridos} vínculo(s) novo(s). Confira e confirme.`
          : 'A IA não encontrou vínculos novos para este GHE.',
      );
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Não foi possível pedir as sugestões à IA.';
      if (!(await tratarConflito(msg))) setErro(msg);
    } finally {
      emAndamento.current = false;
      setSugerindo(false);
    }
  }

  function confirmarTodasSugeridas() {
    if (emAndamento.current) return;
    setSucesso(null);
    setRascunhos((todos) => {
      const atual = { ...(todos[gheId] ?? {}) };
      funcoesSugeridas.forEach((f) => { atual[f.funcaoId] = linhaBase(f.funcaoId, true); });
      return { ...todos, [gheId]: atual };
    });
  }

  async function salvar() {
    const itens = Object.entries(linhasParaSalvar).flatMap(([fId, linha]) =>
      atividades.map((a) => ({ atividadeId: a.atividadeId, funcaoId: fId, executa: linha[a.atividadeId] === true })));
    if (itens.length === 0 || emAndamento.current) return;
    emAndamento.current = true;
    setSalvando(true);
    setErro(null);
    try {
      await api.estruturaSst.decidirAtividadesFuncao(obraId, itens);
      setRascunhos((todos) => ({ ...todos, [gheId]: {} }));
      await carregar(gheId);
      await aoAlterar(gheId);
      setSucesso('Salvo. A OS passa a listar só as atividades confirmadas.');
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Não foi possível salvar.';
      if (/\(403\)|Forbidden|Só o Engenheiro de Segurança desta obra/i.test(msg)) {
        setRecusado(true);
        setErro('Só o Engenheiro de Segurança desta obra pode confirmar as atividades da função.');
      } else if (!(await tratarConflito(msg))) {
        setErro(msg);
      }
    } finally {
      emAndamento.current = false;
      setSalvando(false);
    }
  }

  if (ghes.length === 0) {
    return <EstadoVazio titulo="Esta obra ainda não tem GHE cadastrado. Gere ou importe o PGR para definir as funções." />;
  }

  // A IA já olhou esta função (há sugerida ou decisão): o que ela não sugeriu aparece como "Não executa".
  const iaJaOlhou = (fId: string) => atividades.some((a) => celulaDe(a.atividadeId, fId)?.estado !== 'SemDecisao');

  const seloDe = (c: CelulaMatriz | undefined, on: boolean, editada: boolean, fId: string) => {
    if (editada) return on
      ? <StatusChip tom="ok" icone={<Checkmark16Regular />}>Confirmada</StatusChip>
      : <StatusChip tom="neutro">Não executa</StatusChip>;
    if (c?.estado === 'Pendente') return <StatusChip tom="info" icone={<Sparkle16Regular />}>Sugerida pela IA</StatusChip>;
    if (c?.estado === 'Validada') return <StatusChip tom="ok" icone={<Checkmark16Regular />}>Confirmada</StatusChip>;
    if (c?.estado === 'Descartada' || iaJaOlhou(fId)) return <StatusChip tom="neutro">Não executa</StatusChip>;
    return <StatusChip tom="neutro">Sem vínculo</StatusChip>;
  };

  const motivoDe = (c: CelulaMatriz | undefined, on: boolean, editada: boolean, fId: string) => {
    if (editada) return on ? 'Marcada para confirmar. Salve para valer na OS.' : 'Desmarcada. Ao salvar, fica como "Não executa" e sai da OS desta função.';
    if (!c || c.estado === 'SemDecisao') return iaJaOlhou(fId) ? 'IA: sem relação com o ofício desta função' : 'Ainda não avaliada para esta função';
    if (c.estado === 'Pendente') return c.justificativaIa ? `IA: ${c.justificativaIa}` : 'Sugerida pela IA, aguardando o engenheiro';
    if (c.estado === 'Validada') return c.justificativaIa ?? (c.origem === 'Pgr' ? 'Atividade descrita no PGR' : 'Confirmada pelo engenheiro');
    return c.justificativaIa ? `IA: ${c.justificativaIa}` : 'Desmarcada pelo engenheiro';
  };

  const nomesDe = (lista: { nome: string }[]) => lista.map((f) => f.nome).join(', ');
  const funcoesFaltando = funcoes.filter((f) => f.pendentes > 0 || f.semDecisao > 0);
  const editadaAtual = !!funcao && funcao.funcaoId in linhasParaSalvar;

  return (
    <div>
      {!podeDecidir && (
        <Aviso tom="info">
          <b>Somente leitura.</b> Só o Engenheiro de Segurança desta obra confirma as atividades da função. Você pode consultar o que está definido.
        </Aviso>
      )}
      {matriz && nuncaAvaliado && (
        <Aviso tom="atencao">
          <b>Atividades por função ainda não definidas.</b> Enquanto isso, a Ordem de Serviço fica como minuta com a pendência
          "Atividades da função ainda não confirmadas". Peça as sugestões à IA e confirme.
        </Aviso>
      )}
      {matriz && !nuncaAvaliado && funcoesComSugestao.length > 0 && (
        <Aviso tom="info">
          <b>
            A IA sugeriu atividades para {funcoesComSugestao.length} de {funcoes.length} função(ões) deste GHE
            ({sugeridasNoGhe > 0 ? `${sugeridasNoGhe} sugestão(ões) aguardando` : 'sugestões já marcadas, falta salvar'}).
          </b>{' '}
          Confira cada função, desmarque o que não se aplica e toque em <b>Confirmar todas as sugeridas</b>. A OS só muda depois que você salvar.
          {funcoesSemDecisao.length > 0 && <> Ainda sem confirmação: {nomesDe(funcoesSemDecisao)}.</>}
        </Aviso>
      )}
      {matriz && !nuncaAvaliado && funcoesComSugestao.length === 0 && funcoesSemDecisao.length > 0 && (
        <Aviso tom="atencao">
          <b>{funcoesSemDecisao.length} função(ões) ainda sem confirmação neste GHE: {nomesDe(funcoesSemDecisao)}.</b>{' '}
          Selecione a função, marque o que ela executa e salve. A OS dessas funções continua como minuta até lá.
        </Aviso>
      )}
      {matriz && !nuncaAvaliado && !haPendencia && funcoesFaltando.length === 0 && (
        <Aviso tom="ok">
          <b>
            Atividades confirmadas
            {ultimaDecisao ? ` por ${ultimaDecisao.validadoPorNome}, em ${dataHora(ultimaDecisao.validadoEmUtc!)}` : ''}.
          </b>{' '}
          A Ordem de Serviço lista só as atividades confirmadas para cada função.
        </Aviso>
      )}

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {sucesso && <FeedbackInline tom="sucesso" aoFechar={() => setSucesso(null)}>{sucesso}</FeedbackInline>}
      {sugerindo && (
        <FeedbackInline tom="info">
          A IA está analisando as atividades e as funções deste GHE. Isso pode levar alguns instantes…
        </FeedbackInline>
      )}

      <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap', justifyContent: 'space-between', marginBottom: 12 }}>
        <label
          style={{
            display: 'flex', gap: 8, alignItems: 'center', minHeight: 48, flex: 1, minWidth: 260, maxWidth: 520,
            border: `1px solid ${designTokens.colorCardBorder}`, borderRadius: tokensUi.raio.sm, padding: '0 12px',
            background: designTokens.colorSurface,
          }}
        >
          <Text size={200}>GHE</Text>
          <Select
            aria-label="GHE"
            style={{ flex: 1 }}
            value={gheId}
            onChange={(_, d) => setGheId(d.value)}
          >
            {ghes.map((g) => (
              <option key={g.id} value={g.id}>
                {`GHE ${String(g.numero).padStart(2, '0')}${g.setor ? ` · ${g.setor}` : ''}`}
              </option>
            ))}
          </Select>
          <Text size={200}>{funcoes.length} função(ões)</Text>
        </label>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <Button
            size="large"
            appearance={nuncaAvaliado ? 'primary' : 'secondary'}
            icon={<Sparkle16Regular />}
            disabled={ocupado || !matriz || !podeEditarRiscos}
            onClick={() => void sugerirComIa()}
          >
            {sugerindo ? 'Sugerindo…' : 'Sugerir com IA'}
          </Button>
          {podeDecidir && (
            <>
          <Button
            size="large"
            appearance={sugeridasNoGhe > 0 ? 'primary' : 'secondary'}
            icon={<Checkmark16Regular />}
            disabled={sugeridasNoGhe === 0 || ocupado}
            onClick={confirmarTodasSugeridas}
          >
            Confirmar todas as sugeridas{sugeridasNoGhe > 0 ? ` (${sugeridasNoGhe})` : ''}
          </Button>
          <Button
            size="large"
            appearance={alteracoes > 0 ? 'primary' : 'secondary'}
            icon={<Save16Regular />}
            disabled={alteracoes === 0 || ocupado}
            onClick={() => void salvar()}
          >
            {salvando ? 'Salvando…' : 'Salvar'}
          </Button>
            </>
          )}
        </div>
      </div>

      {!matriz && !erro && <Carregando variante="detalhe" linhas={5} />}

      {matriz && funcoes.length === 0 && (
        <EstadoVazio titulo="Este GHE não tem funções cadastradas no PGR." />
      )}
      {matriz && funcoes.length > 0 && matriz.atividades.length === 0 && (
        <EstadoVazio titulo="Este GHE ainda não tem atividades na EAP." />
      )}

      {matriz && funcao && matriz.atividades.length > 0 && (
        <>
          <div role="group" aria-label="Função do GHE" style={{ display: 'flex', gap: 8, overflowX: 'auto', paddingBottom: 4, marginBottom: 12 }}>
            {funcoes.map((f) => {
              const ativa = f.funcaoId === funcao.funcaoId;
              const ponto = f.pendentes > 0 ? tokensUi.status.info.tinta
                : f.semDecisao === totalAtividades ? tokensUi.bordaSuave : tokensUi.status.ok.tinta;
              return (
                <button
                  key={f.funcaoId}
                  type="button"
                  aria-pressed={ativa}
                  onClick={() => setFuncaoId(f.funcaoId)}
                  style={{
                    flex: 'none', minHeight: 48, padding: '0 14px', borderRadius: tokensUi.raio.full, cursor: 'pointer',
                    display: 'inline-flex', alignItems: 'center', gap: 8, font: 'inherit', fontWeight: 600,
                    border: `1px solid ${ativa ? designTokens.colorPrimary : designTokens.colorCardBorder}`,
                    background: ativa ? designTokens.colorPrimary : designTokens.colorSurface,
                    color: ativa ? designTokens.colorWhite : 'inherit',
                  }}
                >
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: ponto, flex: 'none' }} />
                  {f.nome}
                  <b style={{ fontSize: 12, opacity: 0.8 }}>{executa(f.funcaoId)}/{totalAtividades}</b>
                </button>
              );
            })}
          </div>

          <Card
            titulo={`${funcao.nome}: ${executa(funcao.funcaoId)} de ${totalAtividades} atividades`}
            acoes={
              <span style={{ display: 'inline-flex', gap: 6, flexWrap: 'wrap' }}>
                {contagem(funcao.funcaoId, 'Validada') > 0 && <StatusChip tom="ok">{contagem(funcao.funcaoId, 'Validada')} confirmadas</StatusChip>}
                {contagem(funcao.funcaoId, 'Pendente') > 0 && <StatusChip tom="info">{contagem(funcao.funcaoId, 'Pendente')} sugeridas</StatusChip>}
                {contagem(funcao.funcaoId, 'SemDecisao') > 0 && <StatusChip tom="neutro">{contagem(funcao.funcaoId, 'SemDecisao')} sem vínculo</StatusChip>}
                {(() => {
                  const semProc = matriz.atividades.filter((a) =>
                    celulaDe(a.atividadeId, funcao.funcaoId)?.estado === 'Validada' && a.procedimentos === 0).length;
                  return semProc > 0 ? <StatusChip tom="atencao">{semProc} sem procedimento</StatusChip> : null;
                })()}
              </span>
            }
          >
            <div style={{ margin: '-8px -8px' }}>
              {matriz.atividades.map((a) => {
                const c = celulaDe(a.atividadeId, funcao.funcaoId);
                const on = marcada(a.atividadeId, funcao.funcaoId);
                const mudou = funcao.funcaoId in rascunho;
                const { codigo, resto } = separarCodigo(a.nome);
                const confirmada = !mudou && c?.estado === 'Validada';
                const fundo = mudou ? (on ? tokensUi.status.ok.fundo : 'transparent')
                  : c?.estado === 'Pendente' ? tokensUi.status.info.fundo
                  : confirmada ? tokensUi.status.ok.fundo : 'transparent';
                return (
                  <div
                    key={a.atividadeId}
                    style={{
                      display: 'grid', gridTemplateColumns: '48px minmax(0, 1fr) auto', columnGap: 10, alignItems: 'center',
                      padding: '6px 14px 6px 6px', minHeight: 72, borderBottom: `1px solid ${designTokens.colorCardBorder}`,
                      background: `color-mix(in srgb, ${fundo} 55%, transparent)`,
                    }}
                  >
                    <button
                      type="button"
                      role="checkbox"
                      aria-checked={on}
                      aria-label={`${funcao.nome} executa ${a.nome}`}
                      disabled={!podeDecidir || ocupado}
                      onClick={() => alternar(a.atividadeId, funcao.funcaoId)}
                      style={{
                        width: 48, height: 48, display: 'flex', alignItems: 'center', justifyContent: 'center',
                        border: 'none', background: 'transparent', padding: 0, cursor: podeDecidir ? 'pointer' : 'not-allowed',
                      }}
                    >
                      <span
                        style={{
                          width: 26, height: 26, borderRadius: 7, display: 'flex', alignItems: 'center', justifyContent: 'center',
                          border: `2px solid ${on ? designTokens.colorPrimary : tokensUi.bordaSuave}`,
                          background: on ? designTokens.colorPrimary : 'transparent', color: designTokens.colorWhite,
                          opacity: podeDecidir ? 1 : 0.6,
                        }}
                      >
                        {on && <Checkmark16Regular />}
                      </span>
                    </button>
                    <div style={{ minWidth: 0 }}>
                      <Text weight="semibold" style={{ display: 'block' }}>
                        {codigo && <span style={{ opacity: 0.65, marginRight: 6, fontVariantNumeric: 'tabular-nums' }}>{codigo}</span>}
                        {resto}
                      </Text>
                      <Text size={200} style={{ display: 'block', opacity: 0.8 }}>{motivoDe(c, on, mudou, funcao.funcaoId)}</Text>
                    </div>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                      {confirmada && (a.procedimentos > 0
                        ? <StatusChip tom="neutro">{a.procedimentos} procedimento(s)</StatusChip>
                        : (
                          <Button
                            size="small"
                            icon={<Link16Regular />}
                            onClick={() => aoVincularProcedimento(a.atividadeId)}
                            style={{
                              background: tokensUi.status.atencao.fundo, color: tokensUi.status.atencao.tinta,
                              border: `1px solid ${tokensUi.status.atencao.tinta}`,
                            }}
                          >
                            Vincular procedimento
                          </Button>
                        ))}
                      {seloDe(c, on, mudou, funcao.funcaoId)}
                    </div>
                  </div>
                );
              })}
            </div>
          </Card>

          {podeDecidir && editadaAtual && (
            <div style={{ marginTop: 12 }}>
              <Aviso tom="atencao">
                <b>{funcao.nome}: {executa(funcao.funcaoId)} de {totalAtividades} atividades.</b> Ao salvar, as atividades
                desmarcadas de {funcao.nome} ficam como "Não executa" e saem da OS dessa função.
                {Object.keys(linhasParaSalvar).length > 1 && (
                  <> Também serão salvas: {Object.keys(linhasParaSalvar).filter((f) => f !== funcao.funcaoId).map(nomeFuncao).join(', ')}.</>
                )}
              </Aviso>
            </div>
          )}
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10, flexWrap: 'wrap', marginTop: 12 }}>
            <Text size={200}>
              Vale para toda a obra: a OS de qualquer {funcao.nome.toLowerCase()} deste GHE lista só estas atividades.
            </Text>
            <Text size={200}>Uma nova revisão pela EAP mantém os vínculos; atividade nova entra como "Sem vínculo".</Text>
          </div>
        </>
      )}
    </div>
  );
}
