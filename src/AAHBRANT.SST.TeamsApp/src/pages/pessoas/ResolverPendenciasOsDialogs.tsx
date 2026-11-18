import { useEffect, useMemo, useState } from 'react';
import {
  Button, Carregando, Checkbox, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
  FeedbackInline, Select, StatusChip, Text,
} from '@ui';
import { api, type MaterialApoio, type ProcedimentosAtividade, type SugestaoPendenteOs } from '../../lib/api';

// Resolver as pendências da Ordem de Serviço sem sair da OS (17/11/2026). Antes, cada pendência abria o PGR
// e a SST tinha de achar a atividade, abrir "Detalhes para a OS", marcar e salvar — uma atividade por vez.
// Aqui: vincular procedimentos a várias atividades de uma vez e aceitar/descartar as sugestões da IA em bloco.
// As regras são as mesmas do PGR (só procedimento/IT vigente; quem decide vem do token).

const rotuloDoc = (m: MaterialApoio) => `${m.codigo ? `${m.codigo} — ` : ''}${m.nome}${m.revisao ? ` · rev. ${m.revisao}` : ''}`;

const caixa: React.CSSProperties = {
  border: '1px solid var(--colorNeutralStroke2)', borderRadius: 6, padding: '8px 10px', marginBottom: 8,
};

// ---------------------------------------------------------------------------------------------------------
// Vincular procedimentos em lote
// ---------------------------------------------------------------------------------------------------------

export function VincularProcedimentosDialog({ atividadeIds, aoFechar }: {
  atividadeIds: string[];
  // alterou = true quando salvou algo (a OS recarrega a minuta).
  aoFechar: (alterou: boolean) => void;
}) {
  const [docs, setDocs] = useState<MaterialApoio[] | null>(null);
  const [linhas, setLinhas] = useState<ProcedimentosAtividade[]>([]);
  const [original, setOriginal] = useState<Record<string, string[]>>({});
  const [selecionadas, setSelecionadas] = useState<string[]>([]);
  const [docEscolhido, setDocEscolhido] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    void (async () => {
      try {
        const [materiais, atuais] = await Promise.all([
          api.materiaisApoio.listar(),
          api.riscos.obterProcedimentosAtividades(atividadeIds),
        ]);
        const vigentes = materiais.filter((m) => (m.tipo === 2 || m.tipo === 3) && m.situacao === 1)
          .sort((a, b) => (a.codigo ?? a.nome).localeCompare(b.codigo ?? b.nome, 'pt-BR'));
        setDocs(vigentes);
        setDocEscolhido(vigentes[0]?.id ?? '');
        setLinhas(atuais);
        setOriginal(Object.fromEntries(atuais.map((l) => [l.atividadeId, [...l.documentoIds].sort()])));
        setSelecionadas(atuais.map((l) => l.atividadeId));
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar os procedimentos.');
        setDocs([]);
      }
    })();
  }, [atividadeIds]);

  const porId = useMemo(() => new Map((docs ?? []).map((d) => [d.id, d])), [docs]);
  const alteradas = linhas.filter((l) => [...l.documentoIds].sort().join() !== (original[l.atividadeId] ?? []).join());
  const semProcedimento = linhas.filter((l) => l.documentoIds.length === 0).length;

  const alterar = (atividadeId: string, fn: (ids: string[]) => string[]) =>
    setLinhas((ls) => ls.map((l) => (l.atividadeId === atividadeId ? { ...l, documentoIds: fn(l.documentoIds) } : l)));

  function aplicarNasSelecionadas() {
    if (!docEscolhido) return;
    setLinhas((ls) => ls.map((l) =>
      selecionadas.includes(l.atividadeId) && !l.documentoIds.includes(docEscolhido)
        ? { ...l, documentoIds: [...l.documentoIds, docEscolhido] }
        : l));
  }

  async function salvar() {
    setSalvando(true);
    setErro(null);
    try {
      await api.riscos.vincularProcedimentosEmLote(alteradas.map((l) => ({ atividadeId: l.atividadeId, documentoIds: l.documentoIds })));
      aoFechar(true);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível salvar os procedimentos.');
      setSalvando(false);
    }
  }

  const todasMarcadas = linhas.length > 0 && selecionadas.length === linhas.length;

  return (
    <Dialog open onOpenChange={(_, d) => { if (!d.open && !salvando) aoFechar(false); }}>
      <DialogSurface style={{ maxWidth: 820, width: '95vw' }}>
        <DialogBody>
          <DialogTitle>Vincular procedimentos às atividades</DialogTitle>
          <DialogContent>
            {docs === null ? <Carregando variante="detalhe" linhas={4} /> : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
                {docs.length === 0 ? (
                  <FeedbackInline tom="aviso">
                    Nenhum procedimento ou instrução de trabalho vigente. Cadastre em Gestão SST › Documentos &amp; Procedimentos.
                  </FeedbackInline>
                ) : (
                  <>
                    <Text size={200} style={{ display: 'block' }}>
                      Escolha um procedimento e aplique de uma vez nas atividades marcadas. Depois ajuste uma a uma, se precisar.
                      O vínculo vale para o PGR inteiro: todos os trabalhadores que executam estas atividades passam a recebê-lo na OS.
                    </Text>
                    <div style={{ ...caixa, background: 'var(--colorNeutralBackground2)', display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center', marginBottom: 0 }}>
                      <Select
                        aria-label="Procedimento a aplicar"
                        value={docEscolhido}
                        onChange={(_, d) => setDocEscolhido(d.value)}
                        style={{ flex: '1 1 320px', minWidth: 0 }}
                      >
                        {docs.map((d) => <option key={d.id} value={d.id}>{rotuloDoc(d)}</option>)}
                      </Select>
                      <Button appearance="primary" disabled={!docEscolhido || selecionadas.length === 0} onClick={aplicarNasSelecionadas}>
                        Aplicar nas marcadas ({selecionadas.length})
                      </Button>
                    </div>

                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                      <Checkbox
                        label={todasMarcadas ? 'Desmarcar todas' : 'Marcar todas'}
                        checked={todasMarcadas ? true : selecionadas.length > 0 ? 'mixed' : false}
                        onChange={() => setSelecionadas(todasMarcadas ? [] : linhas.map((l) => l.atividadeId))}
                      />
                      {semProcedimento > 0
                        ? <StatusChip tom="alerta">{semProcedimento} ainda sem procedimento</StatusChip>
                        : <StatusChip tom="ok">Todas com procedimento</StatusChip>}
                    </div>

                    <div>
                      {linhas.map((l) => (
                        <div
                          key={l.atividadeId}
                          style={{
                            ...caixa,
                            borderLeft: `4px solid ${l.documentoIds.length ? 'var(--colorPaletteGreenBorderActive)' : 'var(--colorPaletteRedBorderActive)'}`,
                          }}
                        >
                          <Checkbox
                            label={<Text weight="semibold">{l.atividadeNome}</Text>}
                            checked={selecionadas.includes(l.atividadeId)}
                            onChange={() => setSelecionadas((s) => (s.includes(l.atividadeId) ? s.filter((x) => x !== l.atividadeId) : [...s, l.atividadeId]))}
                          />
                          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 4, paddingLeft: 28 }}>
                            {l.documentoIds.length === 0 && <Text size={200} style={{ color: 'var(--colorPaletteRedForeground1)' }}>Nenhum procedimento</Text>}
                            {l.documentoIds.map((id) => {
                              const d = porId.get(id);
                              return (
                                <span
                                  key={id}
                                  style={{
                                    display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 4px 2px 8px', borderRadius: 12,
                                    background: 'var(--colorNeutralBackground3)', fontSize: 12,
                                  }}
                                >
                                  {d ? rotuloDoc(d) : 'Procedimento'}
                                  <Button
                                    size="small"
                                    appearance="subtle"
                                    aria-label={`Remover ${d ? rotuloDoc(d) : 'procedimento'} de ${l.atividadeNome}`}
                                    onClick={() => alterar(l.atividadeId, (ids) => ids.filter((x) => x !== id))}
                                    style={{ minWidth: 0, padding: '0 4px', height: 20 }}
                                  >
                                    ×
                                  </Button>
                                </span>
                              );
                            })}
                          </div>
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button disabled={salvando} onClick={() => aoFechar(false)}>Cancelar</Button>
            <Button appearance="primary" disabled={salvando || alteradas.length === 0} onClick={() => void salvar()}>
              {salvando ? 'Salvando…' : alteradas.length ? `Salvar (${alteradas.length} atividade${alteradas.length > 1 ? 's' : ''})` : 'Salvar'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}

// ---------------------------------------------------------------------------------------------------------
// Revisar sugestões da IA em bloco
// ---------------------------------------------------------------------------------------------------------

export function RevisarSugestoesIaDialog({ atividadeIds, aoFechar, aoEditarNoPgr }: {
  atividadeIds: string[];
  aoFechar: (alterou: boolean) => void;
  // Para editar o texto de uma sugestão: abre a atividade no Inventário do PGR.
  aoEditarNoPgr?: (atividadeId: string) => void;
}) {
  const [lista, setLista] = useState<SugestaoPendenteOs[] | null>(null);
  const [marcadas, setMarcadas] = useState<string[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [confirmarDescarte, setConfirmarDescarte] = useState(false);
  const [alterou, setAlterou] = useState(false);

  async function carregar() {
    try {
      const l = await api.riscos.listarSugestoesPendentes(atividadeIds);
      setLista(l);
      setMarcadas(l.map((s) => s.id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar as sugestões.');
      setLista([]);
    }
  }

  useEffect(() => {
    void carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [atividadeIds]);

  const grupos = useMemo(() => {
    const m = new Map<string, { nome: string; itens: SugestaoPendenteOs[] }>();
    for (const s of lista ?? []) {
      if (!m.has(s.atividadeId)) m.set(s.atividadeId, { nome: s.atividadeNome, itens: [] });
      m.get(s.atividadeId)!.itens.push(s);
    }
    return [...m.entries()];
  }, [lista]);

  async function decidir(aceitar: boolean) {
    if (!lista) return;
    const sel = lista.filter((s) => marcadas.includes(s.id));
    setOcupado(true);
    setErro(null);
    setAviso(null);
    try {
      const r = await api.riscos.decidirSugestoesEmLote(
        sel.filter((s) => s.origem === 1).map((s) => s.id),
        sel.filter((s) => s.origem === 2).map((s) => s.id),
        aceitar,
      );
      setAlterou(true);
      setAviso(`${r.decididas} sugestão(ões) ${aceitar ? 'aceita(s)' : 'descartada(s)'}.`
        + (r.decididas < sel.length ? ' As demais já tinham sido decididas por outra pessoa.' : ''));
      setConfirmarDescarte(false);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível registrar a decisão.');
    } finally {
      setOcupado(false);
    }
  }

  const alternar = (ids: string[], marcar: boolean) =>
    setMarcadas((m) => (marcar ? [...new Set([...m, ...ids])] : m.filter((x) => !ids.includes(x))));

  const total = lista?.length ?? 0;
  const todas = total > 0 && marcadas.length === total;

  return (
    <Dialog open onOpenChange={(_, d) => { if (!d.open && !ocupado) aoFechar(alterou); }}>
      <DialogSurface style={{ maxWidth: 860, width: '95vw' }}>
        <DialogBody>
          <DialogTitle>Revisar sugestões da IA</DialogTitle>
          <DialogContent>
            {lista === null ? <Carregando variante="detalhe" linhas={5} /> : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
                {aviso && <FeedbackInline tom="sucesso" aoFechar={() => setAviso(null)}>{aviso}</FeedbackInline>}
                {total === 0 ? (
                  <FeedbackInline tom="sucesso">Nenhuma sugestão pendente nestas atividades.</FeedbackInline>
                ) : (
                  <>
                    <Text size={200} style={{ display: 'block' }}>
                      Sugestão da IA não é controle implantado: só entra na OS depois de aceita pelo responsável de SST.
                      Leia, desmarque o que não se aplica e aceite o restante de uma vez. Para mudar o texto, use “Editar no PGR”.
                    </Text>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                      <Checkbox
                        label={todas ? 'Desmarcar todas' : 'Marcar todas'}
                        checked={todas ? true : marcadas.length > 0 ? 'mixed' : false}
                        onChange={() => setMarcadas(todas ? [] : lista.map((s) => s.id))}
                      />
                      <StatusChip tom="atencao">{marcadas.length} de {total} marcada(s)</StatusChip>
                    </div>
                    {grupos.map(([atividadeId, g]) => {
                      const ids = g.itens.map((s) => s.id);
                      const todasDoGrupo = ids.every((id) => marcadas.includes(id));
                      return (
                        <div key={atividadeId} style={{ ...caixa, borderLeft: '4px solid var(--colorPaletteMarigoldBorderActive)' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                            <Checkbox
                              label={<Text weight="semibold">{g.nome} ({g.itens.length})</Text>}
                              checked={todasDoGrupo ? true : ids.some((id) => marcadas.includes(id)) ? 'mixed' : false}
                              onChange={() => alternar(ids, !todasDoGrupo)}
                            />
                            {aoEditarNoPgr && <Button size="small" appearance="subtle" onClick={() => aoEditarNoPgr(atividadeId)}>Editar no PGR</Button>}
                          </div>
                          <div style={{ paddingLeft: 28, display: 'flex', flexDirection: 'column', gap: 2 }}>
                            {g.itens.map((s) => (
                              <Checkbox
                                key={s.id}
                                checked={marcadas.includes(s.id)}
                                onChange={(_, d) => alternar([s.id], !!d.checked)}
                                label={
                                  <span style={{ fontSize: 13 }}>
                                    <span style={{ color: 'var(--colorNeutralForeground3)' }}>
                                      {s.perigo ? `${s.perigo} · ` : ''}{s.categoria}:
                                    </span>{' '}
                                    {s.texto}{s.requisito ? <> → <b>{s.requisito}</b></> : null}
                                  </span>
                                }
                              />
                            ))}
                          </div>
                        </div>
                      );
                    })}
                  </>
                )}
              </div>
            )}
          </DialogContent>
          {/* Rodapé próprio (não DialogActions): com 3 botões e a confirmação do descarte, o grid do Fluent quebrava os rótulos. */}
          <div style={{ gridColumn: '1 / -1', display: 'flex', flexDirection: 'column', gap: 8, marginTop: 8 }}>
            {total > 0 && confirmarDescarte && (
              <FeedbackInline tom="aviso">
                Descartar {marcadas.length} sugestão(ões)? Elas saem da lista e não entram na OS.
              </FeedbackInline>
            )}
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, flexWrap: 'wrap' }}>
              {total > 0 && (confirmarDescarte ? (
                <>
                  <Button style={{ whiteSpace: 'nowrap' }} disabled={ocupado} onClick={() => setConfirmarDescarte(false)}>Voltar</Button>
                  <Button style={{ whiteSpace: 'nowrap' }} appearance="primary" disabled={ocupado} onClick={() => void decidir(false)}>
                    {ocupado ? 'Registrando…' : 'Confirmar descarte'}
                  </Button>
                </>
              ) : (
                <>
                  <Button style={{ whiteSpace: 'nowrap' }} disabled={ocupado || marcadas.length === 0} onClick={() => setConfirmarDescarte(true)}>
                    Descartar marcadas
                  </Button>
                  <Button style={{ whiteSpace: 'nowrap' }} appearance="primary" disabled={ocupado || marcadas.length === 0} onClick={() => void decidir(true)}>
                    {ocupado ? 'Registrando…' : `Aceitar marcadas (${marcadas.length})`}
                  </Button>
                </>
              ))}
              {!confirmarDescarte && (
                <Button style={{ whiteSpace: 'nowrap' }} appearance={total === 0 ? 'primary' : 'secondary'} disabled={ocupado} onClick={() => aoFechar(alterou)}>
                  {total === 0 ? 'Concluir' : 'Fechar'}
                </Button>
              )}
            </div>
          </div>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
