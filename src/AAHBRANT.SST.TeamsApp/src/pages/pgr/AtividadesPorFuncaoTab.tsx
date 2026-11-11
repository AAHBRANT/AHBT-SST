import { useCallback, useEffect, useMemo, useState } from 'react';
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

// Decisões ainda não salvas, por par atividade × função (true = executa, false = não executa).
type Rascunho = Record<string, boolean>;

// Pendência para o ponto âmbar da aba: alguma função de algum GHE com sugestão por confirmar ou sem decisão.
// A tela só consulta GHE a GHE (sem endpoint novo) e faz isso em segundo plano, depois que o PGR abre.
export function useAtividadesFuncaoGhes(obraId: string) {
  const [ghes, setGhes] = useState<Ghe[] | null>(null);
  const [pendente, setPendente] = useState(false);
  const [falhou, setFalhou] = useState<string | null>(null);

  const recarregar = useCallback(async () => {
    if (!obraId) return;
    try {
      const lista = await api.estruturaSst.listarGhes(obraId);
      setGhes(lista);
      setFalhou(null);
      const matrizes = await Promise.all(
        lista.map((g) => api.estruturaSst.obterMatrizAtividadesFuncao(obraId, g.id).catch(() => null)),
      );
      setPendente(matrizes.some((m) => m?.funcoes.some((f) => f.pendentes > 0 || f.semDecisao > 0)));
    } catch (e) {
      setFalhou(e instanceof Error ? e.message : 'Falha ao carregar os GHE da obra.');
      setGhes((atual) => atual ?? []);
    }
  }, [obraId]);

  useEffect(() => {
    void recarregar();
  }, [recarregar]);

  return { ghes, pendente, falhou, recarregar };
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
  aoAlterar: () => Promise<void>;
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

  // O servidor decide de verdade (só o Engenheiro de Segurança grava). Se ele já informar o perfil, usamos;
  // senão liberamos a edição a quem pode editar riscos e tratamos a recusa (403) quando ela vier.
  const podeEditarRiscos = podeNaObra(usuario, 'risco:editar', obraId);
  const podeDecidir = !recusado && (usuario?.ehEngenheiroSeguranca ?? podeEditarRiscos);

  const carregar = useCallback(async (id: string) => {
    if (!id) return;
    try {
      setErro(null);
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
  const alteracoes = Object.keys(rascunho).length;

  function definirRascunho(atualizar: (r: Rascunho) => Rascunho) {
    setRascunhos((todos) => ({ ...todos, [gheId]: atualizar(todos[gheId] ?? {}) }));
  }

  const marcada = (atividadeId: string, fId: string) => {
    const k = chave(atividadeId, fId);
    return k in rascunho ? rascunho[k] : executaNaBase(celulas.get(k));
  };

  function alternar(atividadeId: string, fId: string) {
    const k = chave(atividadeId, fId);
    const base = celulas.get(k);
    const novo = !marcada(atividadeId, fId);
    setSucesso(null);
    definirRascunho((r) => {
      const copia = { ...r };
      // Validada/Descartada: voltar ao estado salvo desfaz a alteração. Sem decisão: desmarcar também.
      // Sugerida (Pendente): qualquer toque é uma decisão explícita.
      const volta = base?.estado === 'Pendente' ? false : base?.estado === 'Validada' || base?.estado === 'Descartada'
        ? novo === executaNaBase(base)
        : novo === false;
      if (volta) delete copia[k]; else copia[k] = novo;
      return copia;
    });
  }

  const totalAtividades = matriz?.atividades.length ?? 0;
  const funcoes = matriz?.funcoes ?? [];
  const funcao = funcoes.find((f) => f.funcaoId === funcaoId) ?? null;

  const executa = (fId: string) => (matriz?.atividades ?? []).filter((a) => marcada(a.atividadeId, fId)).length;
  const contagem = (fId: string, estado: CelulaMatriz['estado']) =>
    (matriz?.atividades ?? []).filter((a) => celulas.get(chave(a.atividadeId, fId))?.estado === estado).length;

  const sugeridasNoGhe = funcoes.reduce((t, f) => t + f.pendentes, 0);
  const nuncaAvaliado = matriz !== null && funcoes.length > 0 && funcoes.every((f) => f.pendentes + f.validadas + f.descartadas === 0);
  const haPendencia = funcoes.some((f) => f.pendentes > 0 || f.semDecisao > 0);
  const ultimaDecisao = (matriz?.celulas ?? [])
    .filter((c) => c.validadoEmUtc && c.validadoPorNome)
    .sort((a, b) => (b.validadoEmUtc ?? '').localeCompare(a.validadoEmUtc ?? ''))[0];

  async function sugerirComIa() {
    setSugerindo(true);
    setErro(null);
    setSucesso(null);
    try {
      const r = await api.estruturaSst.sugerirAtividadesFuncaoComIa(obraId, gheId);
      await carregar(gheId);
      await aoAlterar();
      setSucesso(
        r.sugeridos > 0
          ? `Sugestões prontas: ${r.sugeridos} vínculo(s) novo(s). Confira e confirme.`
          : 'A IA não encontrou vínculos novos para este GHE.',
      );
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível pedir as sugestões à IA.');
    } finally {
      setSugerindo(false);
    }
  }

  function confirmarTodasSugeridas() {
    setSucesso(null);
    definirRascunho((r) => {
      const copia = { ...r };
      matriz?.celulas.forEach((c) => {
        const k = chave(c.atividadeId, c.funcaoId);
        if (c.estado === 'Pendente' && !(k in copia)) copia[k] = true;
      });
      return copia;
    });
  }

  async function salvar() {
    const itens = Object.entries(rascunho).map(([k, executa]) => {
      const [atividadeId, fId] = k.split('|');
      return { atividadeId, funcaoId: fId, executa };
    });
    if (itens.length === 0) return;
    setSalvando(true);
    setErro(null);
    try {
      await api.estruturaSst.decidirAtividadesFuncao(obraId, itens);
      setRascunhos((todos) => ({ ...todos, [gheId]: {} }));
      await carregar(gheId);
      await aoAlterar();
      setSucesso('Salvo. A OS passa a listar só as atividades confirmadas.');
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Não foi possível salvar.';
      if (/\(403\)/.test(msg)) {
        setRecusado(true);
        setErro('Só o Engenheiro de Segurança confirma as atividades por função. Suas alterações não foram salvas.');
      } else {
        setErro(msg);
      }
    } finally {
      setSalvando(false);
    }
  }

  if (ghes.length === 0) {
    return <EstadoVazio titulo="Esta obra ainda não tem GHE cadastrado. Gere ou importe o PGR para definir as funções." />;
  }

  const seloDe = (c: CelulaMatriz | undefined, on: boolean, mudou: boolean) => {
    if (mudou) return on
      ? <StatusChip tom="ok" icone={<Checkmark16Regular />}>Confirmada</StatusChip>
      : <StatusChip tom="neutro">Não executa</StatusChip>;
    if (c?.estado === 'Pendente') return <StatusChip tom="info" icone={<Sparkle16Regular />}>Sugerida pela IA</StatusChip>;
    if (c?.estado === 'Validada') return <StatusChip tom="ok" icone={<Checkmark16Regular />}>Confirmada</StatusChip>;
    if (c?.estado === 'Descartada') return <StatusChip tom="neutro">Não executa</StatusChip>;
    return <StatusChip tom="neutro">Sem vínculo</StatusChip>;
  };

  const motivoDe = (c: CelulaMatriz | undefined, on: boolean, mudou: boolean) => {
    if (mudou) return on ? 'Marcada para confirmar. Salve para valer na OS.' : 'Desmarcada. Salve para registrar que a função não executa.';
    if (!c || c.estado === 'SemDecisao') return 'Ainda não avaliada para esta função';
    if (c.estado === 'Pendente') return c.justificativaIa ? `IA: ${c.justificativaIa}` : 'Sugerida pela IA, aguardando o engenheiro';
    if (c.estado === 'Validada') return c.justificativaIa ?? (c.origem === 'Pgr' ? 'Atividade descrita no PGR' : 'Confirmada pelo engenheiro');
    return c.justificativaIa ? `IA: ${c.justificativaIa}` : 'Desmarcada pelo engenheiro';
  };

  return (
    <div>
      {!podeDecidir && (
        <Aviso tom="info">
          <b>Somente leitura.</b> Só o Engenheiro de Segurança confirma as atividades por função. Você pode consultar o que está definido.
        </Aviso>
      )}
      {matriz && nuncaAvaliado && (
        <Aviso tom="atencao">
          <b>Atividades por função ainda não definidas.</b> Enquanto isso, a Ordem de Serviço fica como minuta com a pendência
          "Atividades da função ainda não confirmadas". Peça as sugestões à IA e confirme.
        </Aviso>
      )}
      {matriz && !nuncaAvaliado && haPendencia && (
        <Aviso tom="info">
          <b>A IA sugeriu atividades para {funcoes.filter((f) => f.pendentes > 0).length} função(ões) deste GHE.</b>{' '}
          Confira cada função, desmarque o que não se aplica e toque em <b>Confirmar todas as sugeridas</b>. A OS só muda depois que você salvar.
        </Aviso>
      )}
      {matriz && !nuncaAvaliado && !haPendencia && (
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
            disabled={sugerindo || salvando || !matriz || !podeEditarRiscos}
            onClick={() => void sugerirComIa()}
          >
            {sugerindo ? 'Sugerindo…' : 'Sugerir com IA'}
          </Button>
          <Button
            size="large"
            appearance={sugeridasNoGhe > 0 ? 'primary' : 'secondary'}
            icon={<Checkmark16Regular />}
            disabled={!podeDecidir || sugeridasNoGhe === 0 || salvando}
            onClick={confirmarTodasSugeridas}
          >
            Confirmar todas as sugeridas{sugeridasNoGhe > 0 ? ` (${sugeridasNoGhe})` : ''}
          </Button>
          <Button
            size="large"
            appearance={alteracoes > 0 ? 'primary' : 'secondary'}
            icon={<Save16Regular />}
            disabled={!podeDecidir || alteracoes === 0 || salvando}
            onClick={() => void salvar()}
          >
            {salvando ? 'Salvando…' : 'Salvar'}
          </Button>
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
                    celulas.get(chave(a.atividadeId, funcao.funcaoId))?.estado === 'Validada' && a.procedimentos === 0).length;
                  return semProc > 0 ? <StatusChip tom="atencao">{semProc} sem procedimento</StatusChip> : null;
                })()}
              </span>
            }
          >
            <div style={{ margin: '-8px -8px' }}>
              {matriz.atividades.map((a) => {
                const k = chave(a.atividadeId, funcao.funcaoId);
                const c = celulas.get(k);
                const on = marcada(a.atividadeId, funcao.funcaoId);
                const mudou = k in rascunho;
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
                      disabled={!podeDecidir || salvando}
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
                      <Text weight="semibold" style={{ display: 'block' }}>{a.nome}</Text>
                      <Text size={200} style={{ display: 'block', opacity: 0.8 }}>{motivoDe(c, on, mudou)}</Text>
                    </div>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                      {confirmada && (a.procedimentos > 0
                        ? <StatusChip tom="neutro">{a.procedimentos} procedimento(s)</StatusChip>
                        : (
                          <Button size="small" icon={<Link16Regular />} onClick={() => aoVincularProcedimento(a.atividadeId)}>
                            Vincular procedimento
                          </Button>
                        ))}
                      {seloDe(c, on, mudou)}
                    </div>
                  </div>
                );
              })}
            </div>
          </Card>

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
