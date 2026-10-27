import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button, Card, Carregando, Checkbox, FeedbackInline, Field, Input, PageHeader, Select, StatusChip, Text, Textarea, designTokens,
} from '@ui';
import {
  api, StatusUsuario, type AcaoPlanoGerada, type AtividadeGerada, type CatalogoEpi, type CategoriaMedida, type EstruturaGeracaoPgr, type GeracaoPgrEap,
  type GheGerado, type Obra, type RiscoGerado, type Usuario,
} from '../../lib/api';
import { rotuloStatusGeracao } from './GeracoesPgrEapCard';
import { DadosComplementaresPgr } from './DadosComplementaresPgr';
import { ExposicaoRiscoEditor, SeletorEpi } from './ExposicaoRiscoEditor';
import { InventarioPorPerigo } from './InventarioPorPerigo';

const PASSOS = ['Enviar a EAP', 'Conferir a leitura', 'Funções e GHE', 'Inventário de riscos', 'Aprovar'];
const FASES: Record<number, string> = { 1: 'Antes', 2: 'Durante', 3: 'Após' };
const TIPOS = ['Físico', 'Químico', 'Biológico', 'Ergonômico', 'Psicossocial', 'Acidente'];
// Ordem de prioridade da NR-01; o PDF separa EPC e EPI por estas categorias.
const CATEGORIAS: CategoriaMedida[] = ['Eliminação', 'EPC', 'Administrativa', 'EPI'];
const dataInput = (iso?: string | null) => (iso ? iso.slice(0, 10) : '');
const copiar = <T,>(x: T): T => JSON.parse(JSON.stringify(x)) as T;

// Assistente "Gerar PGR pela EAP" (21/10/2026), no desenho aprovado (5 passos). Tudo o que a IA gera é
// sugestão até o Engenheiro de Segurança aprovar; a aprovação cria o PGR vigente com GHE, funções,
// atividades, riscos e medidas, que passam a alimentar a Ordem de Serviço.
export function GeracaoPgrEapPage() {
  const { geracaoId } = useParams<{ geracaoId: string }>();
  return geracaoId ? <Assistente id={geracaoId} /> : <Passo1Envio />;
}

function Passos({ atual, maximo, aoIr }: { atual: number; maximo: number; aoIr?: (p: number) => void }) {
  return (
    <div style={{ display: 'flex', overflowX: 'auto', marginBottom: 16 }}>
      {PASSOS.map((p, i) => {
        const n = i + 1;
        const ativo = n === atual;
        const clicavel = aoIr && n >= 2 && n <= maximo && n !== atual;
        return (
          <button
            key={p}
            type="button"
            disabled={!clicavel}
            onClick={() => clicavel && aoIr!(n)}
            style={{
              flex: 1, minWidth: 140, padding: '10px 12px', background: 'none', border: 0, textAlign: 'left',
              borderBottom: `3px solid ${ativo ? designTokens.colorPrimary : 'var(--colorNeutralStroke2)'}`,
              color: ativo ? designTokens.colorPrimary : 'var(--colorNeutralForeground3)', fontWeight: 600,
              cursor: clicavel ? 'pointer' : 'default', font: 'inherit',
            }}
          >
            {n < atual ? '✓' : n}. {p}
          </button>
        );
      })}
    </div>
  );
}

function Passo1Envio() {
  const navigate = useNavigate();
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [nome, setNome] = useState('');
  const [dataElaboracao, setDataElaboracao] = useState(new Date().toISOString().slice(0, 10));
  const [proximaRevisao, setProximaRevisao] = useState('');
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [lendo, setLendo] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    api.obras.listar().then(setObras).catch(() => setObras([]));
  }, []);

  async function ler() {
    if (!obraId || !nome.trim() || !arquivo) {
      setErro('Escolha a obra, informe o nome do PGR e envie o arquivo da EAP.');
      return;
    }
    try {
      setLendo(true);
      setErro(null);
      const { id } = await api.geracoesPgrEap.criar(
        { obraId, nome: nome.trim(), dataElaboracao, dataProximaRevisao: proximaRevisao || null }, arquivo);
      navigate(`/prevencao/pgr/gerar-eap/${id}`, { replace: true });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao ler a EAP.');
    } finally {
      setLendo(false);
    }
  }

  return (
    <div>
      <PageHeader titulo="Gerar PGR pela EAP da obra" voltarPara="/gestao-sst?secao=pgr" rotuloVoltar="Voltar para PGR" />
      <Passos atual={1} maximo={1} />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <Card titulo="Dados do PGR">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 12 }}>
          <Field label="Obra" required>
            <Select value={obraId} onChange={(_, d) => {
              setObraId(d.value);
              const obra = obras.find((o) => o.id === d.value);
              if (obra && !nome) setNome(`PGR ${obra.nome}`);
            }}>
              <option value="">Selecione</option>
              {obras.map((o) => <option key={o.id} value={o.id}>{o.nome}</option>)}
            </Select>
          </Field>
          <Field label="Nome do PGR" required><Input value={nome} onChange={(_, d) => setNome(d.value)} /></Field>
          <Field label="Data de elaboração" required><Input type="date" value={dataElaboracao} onChange={(_, d) => setDataElaboracao(d.value)} /></Field>
          <Field label="Próxima revisão"><Input type="date" value={proximaRevisao} onChange={(_, d) => setProximaRevisao(d.value)} /></Field>
        </div>
        <div style={{ marginTop: 16, border: '2px dashed var(--colorNeutralStroke1)', borderRadius: 8, padding: 20, textAlign: 'center' }}>
          <Text weight="semibold" style={{ display: 'block' }}>Arquivo da EAP</Text>
          <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
            Excel (.xlsx), XML do MS Project (no Project: Arquivo › Salvar como › XML) ou PDF · até 20 MB
          </Text>
          <input type="file" accept=".xlsx,.xlsm,.xml,.pdf,.csv" onChange={(e) => setArquivo(e.target.files?.[0] ?? null)} />
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 12 }}>
          <Button onClick={() => navigate('/gestao-sst?secao=pgr')}>Cancelar</Button>
          <Button appearance="primary" disabled={lendo} onClick={() => void ler()}>{lendo ? 'Lendo a EAP com IA…' : 'Ler EAP com IA'}</Button>
        </div>
      </Card>
    </div>
  );
}

function Assistente({ id }: { id: string }) {
  const navigate = useNavigate();
  const [g, setG] = useState<GeracaoPgrEap | null>(null);
  const [e, setE] = useState<EstruturaGeracaoPgr | null>(null);
  const [passo, setPasso] = useState(2);
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  async function carregar(irParaPasso?: number) {
    const d = await api.geracoesPgrEap.obter(id);
    setG(d);
    setE(copiar(d.estrutura));
    setPasso(irParaPasso ?? Math.max(2, d.passo));
  }

  useEffect(() => {
    carregar().catch((x) => setErro(x instanceof Error ? x.message : 'Falha ao carregar.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  if (!g || !e) return erro ? <FeedbackInline tom="erro">{erro}</FeedbackInline> : <Carregando variante="detalhe" linhas={6} />;

  const editavel = g.status === 1 || g.status === 4;
  const alterar = (f: (x: EstruturaGeracaoPgr) => void) => {
    const c = copiar(e);
    f(c);
    setE(c);
    setSalvo(false);
  };

  async function executar(rotulo: string, acao: () => Promise<unknown>) {
    setOcupado(rotulo);
    setErro(null);
    try {
      await acao();
    } catch (x) {
      setErro(x instanceof Error ? x.message : 'Não foi possível concluir.');
    } finally {
      setOcupado(null);
    }
  }

  const salvar = (p: number) => api.geracoesPgrEap.salvar(id, p, e);

  async function gerarInventario() {
    await salvar(4);
    const fila = e!.ghes.flatMap((gh) => gh.atividades.filter((a) => !a.riscosGerados).map((a) => ({ gh, a })));
    const falhas: string[] = [];
    for (let i = 0; i < fila.length; i++) {
      setOcupado(`Gerando riscos com IA: ${i + 1} de ${fila.length} (${fila[i].a.nome})…`);
      try {
        await api.geracoesPgrEap.gerarRiscos(id, fila[i].gh.numero, fila[i].a.codigoEap);
      } catch {
        falhas.push(fila[i].a.nome);
      }
    }
    if (falhas.length) setErro(`Não foi possível gerar os riscos de: ${falhas.join(', ')}. Use "Gerar riscos" na atividade para tentar de novo.`);
    await carregar(4);
  }

  return (
    <div>
      <PageHeader
        titulo={g.nome}
        status={<StatusChip tom={rotuloStatusGeracao[g.status].tom}>{rotuloStatusGeracao[g.status].rotulo}</StatusChip>}
        voltarPara="/gestao-sst?secao=pgr"
        rotuloVoltar="Voltar para PGR"
      />
      <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
        Obra: {g.obraNome} · EAP: {g.nomeArquivoEap} · Elaborado por {g.elaboradoPorNome ?? '—'}
      </Text>
      <Passos atual={passo} maximo={Math.max(passo, g.passo)} aoIr={(p) => setPasso(p)} />

      {g.status === 4 && g.motivoDevolucao && (
        <FeedbackInline tom="aviso">Devolvido pelo engenheiro: {g.motivoDevolucao}</FeedbackInline>
      )}
      {!editavel && g.status !== 3 && passo < 5 && (
        <FeedbackInline tom="info">Aguardando a aprovação do engenheiro. Para alterar, ele precisa devolver para ajustes.</FeedbackInline>
      )}
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {ocupado && <FeedbackInline tom="info">{ocupado}</FeedbackInline>}
      {salvo && <FeedbackInline tom="sucesso" aoFechar={() => setSalvo(false)}>Salvo.</FeedbackInline>}

      {passo === 2 && (
        <Card titulo={`Estrutura lida da EAP · ${e.itens.length} itens · ${e.itens.filter((i) => i.usar).length} entram no PGR`}>
          <div style={{ display: 'flex', flexDirection: 'column' }}>
            {e.itens.map((item, i) => (
              <div key={i} style={{
                display: 'flex', gap: 10, alignItems: 'center', padding: '6px 0', borderBottom: '1px solid var(--colorNeutralStroke2)',
                paddingLeft: (item.nivel - 1) * 22, background: item.conferir ? 'var(--colorPaletteYellowBackground1)' : undefined,
              }}>
                <Checkbox checked={item.usar} disabled={!editavel} onChange={(_, d) => alterar((x) => { x.itens[i].usar = !!d.checked; })} />
                <Text weight={item.nivel === 1 ? 'semibold' : 'regular'} style={{ minWidth: 48 }}>{item.codigo}</Text>
                <Input value={item.nome} disabled={!editavel} style={{ flex: 1 }} onChange={(_, d) => alterar((x) => { x.itens[i].nome = d.value; x.itens[i].conferir = false; })} />
                <Text size={200} style={{ minWidth: 110 }}>{item.periodo ?? ''}</Text>
                {item.conferir && <StatusChip tom="atencao">conferir</StatusChip>}
              </div>
            ))}
          </div>
          <Text size={200} style={{ display: 'block', marginTop: 8 }}>
            Itens sem frente de trabalho (orçamento, compras, medições) vêm desmarcados. Itens em amarelo a IA não leu com segurança: confira o nome.
          </Text>
          {editavel && (
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 12 }}>
              <Button disabled={!!ocupado} onClick={() => void executar('Salvando…', async () => { await salvar(2); setSalvo(true); })}>Salvar</Button>
              <Button appearance="primary" disabled={!!ocupado} onClick={() => void executar('A IA está montando os GHEs e as funções…', async () => {
                await salvar(3);
                await api.geracoesPgrEap.sugerirGhes(id);
                await carregar(3);
              })}>
                {e.ghes.length ? 'Confirmar e refazer GHEs' : 'Confirmar estrutura'}
              </Button>
              {e.ghes.length > 0 && <Button onClick={() => setPasso(3)}>Ir para os GHEs atuais</Button>}
            </div>
          )}
        </Card>
      )}

      {passo === 3 && (
        <>
          {e.ghes.length === 0 && <FeedbackInline tom="aviso">Nenhum GHE ainda. Volte ao passo 2 e confirme a estrutura.</FeedbackInline>}
          {e.ghes.map((gh, gi) => (
            <GheEditor key={gh.numero} ghe={gh} todos={e.ghes} funcoesCadastradas={g.funcoesCadastradas} editavel={editavel}
              alterar={(f) => alterar((x) => f(x.ghes[gi], x))} />
          ))}
          {editavel && e.ghes.length > 0 && (
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <Button disabled={!!ocupado} onClick={() => void executar('Salvando…', async () => { await salvar(3); setSalvo(true); })}>Salvar</Button>
              <Button appearance="primary" disabled={!!ocupado} onClick={() => void executar('Gerando…', gerarInventario)}>Gerar inventário de riscos</Button>
            </div>
          )}
        </>
      )}

      {passo === 4 && (
        <Inventario g={g} e={e} editavel={editavel} ocupado={!!ocupado} alterar={alterar}
          gerarAtividade={(gh, a) => void executar(`Gerando riscos de ${a.nome}…`, async () => {
            await salvar(4);
            await api.geracoesPgrEap.gerarRiscos(id, gh.numero, a.codigoEap);
            await carregar(4);
          })}
          salvar={() => void executar('Salvando…', async () => { await salvar(4); setSalvo(true); })}
          enviar={() => void executar('Enviando para aprovação…', async () => {
            await salvar(4);
            await api.geracoesPgrEap.enviarAprovacao(id);
            await carregar(5);
          })}
          gerarPendentes={() => void executar('Gerando…', gerarInventario)}
        />
      )}

      {passo === 5 && (
        <Aprovacao g={g} ocupado={!!ocupado}
          devolver={(motivo) => void executar('Devolvendo…', async () => { await api.geracoesPgrEap.devolver(id, motivo); await carregar(4); })}
          aprovar={() => void executar('Aprovando e criando o PGR…', async () => {
            const { pgrId } = await api.geracoesPgrEap.aprovar(id);
            navigate(`/prevencao/pgr/${pgrId}`);
          })}
          baixarPdf={() => void executar('Gerando o PDF…', async () => {
            const blob = await api.geracoesPgrEap.baixarPdf(id);
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `${g.nome.replace(/[\\/:*?"<>|]/g, '-')}.pdf`;
            link.click();
            URL.revokeObjectURL(url);
          })}
        />
      )}
    </div>
  );
}

function GheEditor({ ghe, todos, funcoesCadastradas, editavel, alterar }: {
  ghe: GheGerado;
  todos: GheGerado[];
  funcoesCadastradas: { id: string; nome: string }[];
  editavel: boolean;
  alterar: (f: (g: GheGerado, e: EstruturaGeracaoPgr) => void) => void;
}) {
  const [novaFuncao, setNovaFuncao] = useState('');
  return (
    <Card titulo={`GHE ${String(ghe.numero).padStart(2, '0')}`}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 12 }}>
        <Field label="Nome do GHE"><Input value={ghe.nome} disabled={!editavel} onChange={(_, d) => alterar((g) => { g.nome = d.value; })} /></Field>
        <Field label="Ambiente"><Input value={ghe.ambiente ?? ''} disabled={!editavel} onChange={(_, d) => alterar((g) => { g.ambiente = d.value; })} /></Field>
        <Field label="Jornada"><Input value={ghe.jornada ?? ''} disabled={!editavel} placeholder="Ex.: 44 horas semanais" onChange={(_, d) => alterar((g) => { g.jornada = d.value; })} /></Field>
      </div>

      <Text weight="semibold" style={{ display: 'block', margin: '12px 0 6px' }}>Funções e descrição das atividades (vai na íntegra para a Ordem de Serviço)</Text>
      {ghe.funcoes.map((f, fi) => (
        <div key={fi} style={{ border: '1px solid var(--colorNeutralStroke2)', borderRadius: 6, padding: 8, marginBottom: 8 }}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginBottom: 6 }}>
            <Text weight="semibold">{f.nome}</Text>
            {f.funcaoId ? <StatusChip tom="ok">do cadastro</StatusChip> : <StatusChip tom="atencao">função nova</StatusChip>}
            {editavel && <Button size="small" onClick={() => alterar((g) => { g.funcoes.splice(fi, 1); })}>Remover</Button>}
          </div>
          <Textarea value={f.descricao ?? ''} disabled={!editavel} maxLength={2000} style={{ width: '100%' }}
            onChange={(_, d) => alterar((g) => { g.funcoes[fi].descricao = d.value; g.funcoes[fi].sugeridaPelaIa = false; })} />
          {f.sugeridaPelaIa && <Text size={200}>Descrição sugerida pela IA: revise antes de enviar.</Text>}
        </div>
      ))}
      {editavel && (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 12 }}>
          <Select value={novaFuncao} onChange={(_, d) => setNovaFuncao(d.value)} style={{ minWidth: 260 }}>
            <option value="">Adicionar função do cadastro…</option>
            {funcoesCadastradas.filter((c) => !ghe.funcoes.some((f) => f.funcaoId === c.id)).map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </Select>
          <Button disabled={!novaFuncao} onClick={() => {
            const c = funcoesCadastradas.find((x) => x.id === novaFuncao);
            if (c) alterar((g) => { g.funcoes.push({ funcaoId: c.id, nome: c.nome, descricao: '', sugeridaPelaIa: false }); });
            setNovaFuncao('');
          }}>Adicionar</Button>
        </div>
      )}

      <Text weight="semibold" style={{ display: 'block', marginBottom: 6 }}>Atividades da EAP neste GHE</Text>
      {ghe.atividades.map((a, ai) => (
        <div key={a.codigoEap} style={{ display: 'flex', gap: 8, alignItems: 'center', padding: '4px 0', flexWrap: 'wrap' }}>
          <Text style={{ flex: 1 }}>{a.codigoEap} {a.nome}</Text>
          {editavel && todos.length > 1 && (
            <Select value={String(ghe.numero)} aria-label="Mover para GHE" onChange={(_, d) => alterar((g, e) => {
              const [mov] = g.atividades.splice(ai, 1);
              e.ghes.find((x) => x.numero === Number(d.value))?.atividades.push(mov);
            })}>
              {todos.map((x) => <option key={x.numero} value={x.numero}>GHE {String(x.numero).padStart(2, '0')}</option>)}
            </Select>
          )}
        </div>
      ))}
    </Card>
  );
}

function Inventario({ g, e, editavel, ocupado, alterar, gerarAtividade, salvar, enviar, gerarPendentes }: {
  g: GeracaoPgrEap;
  e: EstruturaGeracaoPgr;
  editavel: boolean;
  ocupado: boolean;
  alterar: (f: (x: EstruturaGeracaoPgr) => void) => void;
  gerarAtividade: (gh: GheGerado, a: AtividadeGerada) => void;
  salvar: () => void;
  enviar: () => void;
  gerarPendentes: () => void;
}) {
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [modo, setModo] = useState<'perigo' | 'atividade'>('perigo');
  useEffect(() => {
    api.usuarios.listar(StatusUsuario.Ativo).then(setUsuarios).catch(() => setUsuarios([]));
    api.catalogosEpi.listar().then(setEpis).catch(() => setEpis([]));
  }, []);
  const riscos = e.ghes.flatMap((x) => x.atividades.flatMap((a) => a.riscos)).filter((r) => !r.excluido);
  const medidas = riscos.flatMap((r) => r.medidas);
  const semGerar = e.ghes.flatMap((x) => x.atividades).filter((a) => !a.riscosGerados).length;
  const kpi = (valor: number, rotulo: string, cor?: string) => (
    <div style={{ border: '1px solid var(--colorNeutralStroke2)', borderRadius: 8, padding: '8px 12px', minWidth: 130, background: 'var(--colorNeutralBackground1)' }}>
      <Text size={600} weight="bold" style={{ display: 'block', color: cor }}>{valor}</Text>
      <Text size={200}>{rotulo}</Text>
    </div>
  );

  return (
    <>
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 12 }}>
        {kpi(e.ghes.length, 'GHEs')}
        {kpi(e.ghes.reduce((t, x) => t + x.atividades.length, 0), 'atividades')}
        {kpi(riscos.length, 'riscos')}
        {kpi(riscos.filter((r) => !r.revisado).length, 'riscos não revisados', riscos.some((r) => !r.revisado) ? 'var(--colorPaletteRedForeground1)' : undefined)}
        {kpi(medidas.filter((m) => m.decisao === 'Pendente').length, 'medidas sem decisão')}
      </div>
      {semGerar > 0 && editavel && (
        <FeedbackInline tom="aviso" acao={{ rotulo: `Gerar as ${semGerar} restantes`, aoClicar: gerarPendentes }}>
          {semGerar} atividade(s) ainda sem riscos gerados.
        </FeedbackInline>
      )}

      <div style={{ display: 'flex', gap: 6, alignItems: 'center', marginBottom: 12 }} role="group" aria-label="Forma de revisar">
        <Text size={200}>Revisar:</Text>
        <Button size="small" appearance={modo === 'perigo' ? 'primary' : 'secondary'} onClick={() => setModo('perigo')}>Por perigo</Button>
        <Button size="small" appearance={modo === 'atividade' ? 'primary' : 'secondary'} onClick={() => setModo('atividade')}>Por atividade</Button>
      </div>

      {e.ghes.map((gh, gi) => (
        <Card key={gh.numero} titulo={`GHE ${String(gh.numero).padStart(2, '0')} · ${gh.nome}`}>
          {modo === 'perigo' && (
            <InventarioPorPerigo gh={gh} gi={gi} editavel={editavel} ocupado={ocupado} alterar={alterar} gerarAtividade={gerarAtividade}
              renderRisco={(r, alt) => <RiscoEditor r={r} maxP={g.maxProbabilidade} maxS={g.maxSeveridade} editavel={editavel} epis={epis} alterar={alt} />} />
          )}
          {modo === 'atividade' && gh.atividades.map((a, ai) => (
            <div key={a.codigoEap} style={{ marginBottom: 16 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                <Text weight="semibold">{a.codigoEap} {a.nome}</Text>
                {editavel && (
                  <span style={{ display: 'inline-flex', gap: 6 }}>
                    {a.riscos.some((r) => !r.excluido && !r.revisado) && (
                      <Button size="small" onClick={() => alterar((x) => {
                        x.ghes[gi].atividades[ai].riscos.forEach((r) => { r.revisado = true; r.medidas.forEach((m) => { if (m.decisao === 'Pendente') m.decisao = 'Aceita'; }); });
                      })}>Revisar todos e aceitar medidas</Button>
                    )}
                    <Button size="small" disabled={ocupado} onClick={() => gerarAtividade(gh, a)}>{a.riscosGerados ? 'Gerar de novo' : 'Gerar riscos'}</Button>
                  </span>
                )}
              </div>
              {!a.riscosGerados && <Text size={200}>Riscos ainda não gerados.</Text>}
              {a.riscos.map((r, ri) => !r.excluido && (
                <RiscoEditor key={r.id} r={r} maxP={g.maxProbabilidade} maxS={g.maxSeveridade} editavel={editavel} epis={epis}
                  alterar={(f) => alterar((x) => f(x.ghes[gi].atividades[ai].riscos[ri]))} />
              ))}
            </div>
          ))}
        </Card>
      ))}

      <Card titulo="Vigência do programa">
        <Field label="Término da vigência (obrigatório)">
          <Input
            type="date"
            value={dataInput(e.dataTermino)}
            disabled={!editavel}
            onChange={(_, d) => alterar((x) => { x.dataTermino = d.value || null; })}
          />
        </Field>
        <Text size={200} style={{ display: 'block', marginTop: 4 }}>
          É a data em que o PGR vence. Sem ela, o alerta de PGR vencido não funciona.
        </Text>
      </Card>

      <DadosComplementaresPgr geracaoId={g.id} e={e} editavel={editavel} alterar={alterar} />

      <Card titulo="Plano de ação">
        {(e.acoesPlano ?? []).map((a, i) => (
          <AcaoPlanoLinha key={i} acao={a} usuarios={usuarios} editavel={editavel}
            alterar={(f) => alterar((x) => f(x.acoesPlano[i]))}
            remover={() => alterar((x) => { x.acoesPlano.splice(i, 1); })} />
        ))}
        {editavel && (
          <Button size="small" onClick={() => alterar((x) => { (x.acoesPlano ??= []).push({ descricao: '', responsavelUsuarioId: null, responsavelNome: null, prazo: null }); })}>
            Adicionar ação
          </Button>
        )}
      </Card>

      {editavel && (
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <Button disabled={ocupado} onClick={salvar}>Salvar</Button>
          <Button appearance="primary" disabled={ocupado} onClick={enviar}>Enviar para aprovação do engenheiro</Button>
        </div>
      )}
    </>
  );
}

function RiscoEditor({ r, maxP, maxS, editavel, epis, alterar }: {
  r: RiscoGerado;
  maxP: number;
  maxS: number;
  editavel: boolean;
  epis: CatalogoEpi[];
  alterar: (f: (r: RiscoGerado) => void) => void;
}) {
  const nivel = r.probabilidade * r.severidade;
  const tom = nivel >= 20 ? 'alerta' : nivel >= 10 ? 'atencao' : 'info';
  return (
    <div style={{
      border: '1px solid var(--colorNeutralStroke2)', borderRadius: 6, padding: 10, marginTop: 8,
      background: r.revisado ? undefined : 'var(--colorPaletteYellowBackground1)',
    }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 8, alignItems: 'end' }}>
        <Field label="Perigo"><Input value={r.perigo} disabled={!editavel} onChange={(_, d) => alterar((x) => { x.perigo = d.value; })} /></Field>
        <Field label="Tipo">
          <Select value={r.tipo} disabled={!editavel} onChange={(_, d) => alterar((x) => { x.tipo = d.value; })}>
            {TIPOS.map((t) => <option key={t}>{t}</option>)}
          </Select>
        </Field>
        <Field label="Danos"><Input value={r.danos ?? ''} disabled={!editavel} onChange={(_, d) => alterar((x) => { x.danos = d.value; })} /></Field>
        <Field label="Probabilidade">
          <Select value={String(r.probabilidade)} disabled={!editavel} onChange={(_, d) => alterar((x) => { x.probabilidade = Number(d.value); })}>
            {Array.from({ length: maxP }, (_, i) => <option key={i + 1}>{i + 1}</option>)}
          </Select>
        </Field>
        <Field label="Severidade">
          <Select value={String(r.severidade)} disabled={!editavel} onChange={(_, d) => alterar((x) => { x.severidade = Number(d.value); })}>
            {Array.from({ length: maxS }, (_, i) => <option key={i + 1}>{i + 1}</option>)}
          </Select>
        </Field>
      </div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginTop: 6 }}>
        <StatusChip tom={tom}>P × S = {nivel}</StatusChip>
        {r.base.map((b) => <StatusChip key={b} tom="neutro">{b}</StatusChip>)}
        {editavel && <Checkbox label="Revisado" checked={r.revisado} onChange={(_, d) => alterar((x) => { x.revisado = !!d.checked; })} />}
        {editavel && <Button size="small" onClick={() => alterar((x) => { x.excluido = true; })}>Excluir risco</Button>}
      </div>
      <div style={{ marginTop: 6 }}>
        {r.medidas.map((m, mi) => m.decisao !== 'Descartada' && (
          <div key={mi} style={{ display: 'flex', gap: 6, alignItems: 'center', padding: '3px 0', flexWrap: 'wrap' }}>
            <StatusChip tom="neutro">{FASES[m.fase]}</StatusChip>
            {editavel ? (
              <Select value={m.categoria ?? ''} aria-label="Classificação da medida"
                onChange={(_, d) => alterar((x) => { x.medidas[mi].categoria = (d.value || null) as CategoriaMedida | null; })}>
                <option value="">Classificar…</option>
                {CATEGORIAS.map((c) => <option key={c} value={c}>{c}</option>)}
              </Select>
            ) : <StatusChip tom="neutro">{m.categoria ?? 'Sem classificação'}</StatusChip>}
            {m.categoria === 'EPI' && (
              <SeletorEpi medida={m} epis={epis} editavel={editavel} alterar={(f) => alterar((x) => f(x.medidas[mi]))} />
            )}
            <Text style={{ flex: 1 }}>{m.texto}</Text>
            {m.decisao === 'Aceita' ? <StatusChip tom="ok">Aceita</StatusChip> : <StatusChip tom="atencao">Sugestão da IA</StatusChip>}
            {editavel && m.decisao === 'Pendente' && <Button size="small" appearance="primary" onClick={() => alterar((x) => { x.medidas[mi].decisao = 'Aceita'; })}>Aceitar</Button>}
            {editavel && <Button size="small" onClick={() => alterar((x) => { x.medidas[mi].decisao = 'Descartada'; })}>Descartar</Button>}
          </div>
        ))}
        {editavel && <NovaMedida aoAdicionar={(fase, categoria, texto) => alterar((x) => { x.medidas.push({ fase, texto, categoria, decisao: 'Aceita' }); })} />}
      </div>
      <ExposicaoRiscoEditor r={r} editavel={editavel} alterar={alterar} />
    </div>
  );
}

function NovaMedida({ aoAdicionar }: { aoAdicionar: (fase: 1 | 2 | 3, categoria: CategoriaMedida, texto: string) => void }) {
  const [fase, setFase] = useState<1 | 2 | 3>(2);
  const [categoria, setCategoria] = useState<CategoriaMedida>('Administrativa');
  const [texto, setTexto] = useState('');
  return (
    <div style={{ display: 'flex', gap: 6, marginTop: 4, flexWrap: 'wrap' }}>
      <Select value={String(fase)} onChange={(_, d) => setFase(Number(d.value) as 1 | 2 | 3)} aria-label="Fase">
        {[1, 2, 3].map((f) => <option key={f} value={f}>{FASES[f]}</option>)}
      </Select>
      <Select value={categoria} onChange={(_, d) => setCategoria(d.value as CategoriaMedida)} aria-label="Classificação">
        {CATEGORIAS.map((c) => <option key={c} value={c}>{c}</option>)}
      </Select>
      <Input value={texto} onChange={(_, d) => setTexto(d.value)} placeholder="Nova medida…" style={{ flex: 1, minWidth: 200 }} maxLength={1000} />
      <Button size="small" disabled={!texto.trim()} onClick={() => { aoAdicionar(fase, categoria, texto.trim()); setTexto(''); }}>Adicionar</Button>
    </div>
  );
}

// Uma ação do plano: descrição, responsável (usuário ativo) e prazo.
function AcaoPlanoLinha({ acao, usuarios, editavel, alterar, remover }: {
  acao: AcaoPlanoGerada;
  usuarios: Usuario[];
  editavel: boolean;
  alterar: (f: (x: AcaoPlanoGerada) => void) => void;
  remover: () => void;
}) {
  return (
    <div style={{ display: 'flex', gap: 6, marginBottom: 6, flexWrap: 'wrap', alignItems: 'center' }}>
      <Input value={acao.descricao} disabled={!editavel} placeholder="Ação a executar" maxLength={500} style={{ flex: 2, minWidth: 220 }}
        onChange={(_, d) => alterar((x) => { x.descricao = d.value; })} />
      <Select value={acao.responsavelUsuarioId ?? ''} disabled={!editavel} aria-label="Responsável" style={{ minWidth: 180 }}
        onChange={(_, d) => alterar((x) => {
          x.responsavelUsuarioId = d.value || null;
          x.responsavelNome = usuarios.find((u) => u.id === d.value)?.nome ?? null;
        })}>
        <option value="">Responsável…</option>
        {usuarios.map((u) => <option key={u.id} value={u.id}>{u.nome}</option>)}
      </Select>
      <Input type="date" value={dataInput(acao.inicio)} disabled={!editavel} aria-label="Início (opcional)" title="Início (opcional)"
        onChange={(_, d) => alterar((x) => { x.inicio = d.value || null; })} />
      <Input type="date" value={dataInput(acao.prazo)} disabled={!editavel} aria-label="Prazo" title="Prazo"
        onChange={(_, d) => alterar((x) => { x.prazo = d.value || null; })} />
      {editavel && <Button size="small" onClick={remover}>Remover</Button>}
    </div>
  );
}

function Aprovacao({ g, ocupado, devolver, aprovar, baixarPdf }: {
  g: GeracaoPgrEap;
  ocupado: boolean;
  devolver: (motivo: string) => void;
  aprovar: () => void;
  baixarPdf: () => void;
}) {
  const [motivo, setMotivo] = useState('');
  const navigate = useNavigate();

  // Aprovado: o PDF só existe a partir daqui, então o download fica neste cartão (antes ficava num
  // cartão que nunca aparecia depois da aprovação).
  if (g.status === 3) {
    return (
      <Card titulo="PGR aprovado">
        <Text style={{ display: 'block' }}>Aprovado por {g.aprovadoPorNome} em {g.aprovadoEmUtc ? new Date(g.aprovadoEmUtc).toLocaleString('pt-BR') : ''}.</Text>
        <Text size={200} style={{ display: 'block', margin: '8px 0' }}>
          PDF do PGR com identificação, GHE, funções, inventário de riscos, medidas aceitas, plano de ação e registro da aprovação.
        </Text>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <Button appearance="primary" disabled={ocupado} onClick={baixarPdf}>Baixar PDF do PGR</Button>
          {g.pgrId && <Button onClick={() => navigate(`/prevencao/pgr/${g.pgrId}`)}>Abrir o PGR</Button>}
        </div>
      </Card>
    );
  }

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 16 }}>
      <Card titulo="Aprovação do Engenheiro de Segurança">
        {g.pendenciasAprovacao.length === 0
          ? <StatusChip tom="ok">Tudo revisado</StatusChip>
          : (
            <ul style={{ margin: '0 0 8px', paddingLeft: 18 }}>
              {g.pendenciasAprovacao.map((p) => <li key={p}><Text>{p}</Text></li>)}
            </ul>
          )}
        <Text size={200} style={{ display: 'block', margin: '8px 0' }}>
          Ao aprovar: os riscos ficam avaliados por você, as medidas aceitas ficam validadas e o PGR começa a alimentar a Ordem de Serviço. Se a obra já tem PGR vigente, esta geração entra como uma nova revisão dele, com o histórico de revisões mantido; se não tem, nasce o PGR vigente.
        </Text>
        {g.status === 2 ? (
          <>
            <Field label="Motivo, se for devolver para ajustes">
              <Textarea value={motivo} onChange={(_, d) => setMotivo(d.value)} maxLength={1000} />
            </Field>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 8 }}>
              <Button disabled={ocupado || !motivo.trim()} onClick={() => devolver(motivo.trim())}>Devolver para ajustes</Button>
              <Button appearance="primary" disabled={ocupado || g.pendenciasAprovacao.length > 0} onClick={aprovar}>Aprovar PGR</Button>
            </div>
          </>
        ) : (
          <FeedbackInline tom="info">Envie para aprovação no passo 4.</FeedbackInline>
        )}
      </Card>
      <Card titulo="Documento PGR">
        <Text size={200}>O PDF do PGR fica disponível depois da aprovação do Engenheiro de Segurança.</Text>
      </Card>
    </div>
  );
}
