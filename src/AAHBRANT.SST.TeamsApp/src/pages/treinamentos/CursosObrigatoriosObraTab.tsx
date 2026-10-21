import { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  DataTable,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Field,
  KpiCard,
  PageHeader,
  Select,
  SeletorPesquisavel,
  StatusChip,
  Text,
  Textarea,
  type Coluna,
  type Tom,
} from '@ui';
import {
  api,
  StatusCursoObrigatorio,
  type CursoObrigatorio,
  type CursosObrigatoriosObra,
  type CursoTreinamento,
  type Funcao,
  type Obra,
} from '../../lib/api';

// Cursos obrigatórios por função a partir do PGR (21/10, desenho aprovado pelo usuário): o sistema
// leu o PGR da obra e sugeriu os cursos pelas NRs; o Engenheiro de Segurança aprova ou recusa (com
// justificativa). Só o aprovado entra na aptidão do Início. Regerar nunca apaga — só acrescenta.
const statusInfo: Record<number, { rotulo: string; tom: Tom }> = {
  [StatusCursoObrigatorio.Sugerido]: { rotulo: 'Sugerido', tom: 'atencao' },
  [StatusCursoObrigatorio.Aprovado]: { rotulo: 'Aprovado', tom: 'ok' },
  [StatusCursoObrigatorio.Recusado]: { rotulo: 'Recusado', tom: 'alerta' },
};

function formatarData(iso?: string | null) {
  return iso ? new Date(iso).toLocaleDateString('pt-BR') : '';
}

export function CursosObrigatoriosObraTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [dados, setDados] = useState<CursosObrigatoriosObra | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [recusando, setRecusando] = useState<CursoObrigatorio | null>(null);
  const [justificativa, setJustificativa] = useState('');
  const [adicionando, setAdicionando] = useState(false);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novaFuncaoId, setNovaFuncaoId] = useState('');
  const [novoCursoId, setNovoCursoId] = useState('');

  useEffect(() => {
    api.obras
      .listar()
      .then((lista) => {
        setObras(lista);
        if (lista.length === 1) setObraId(lista[0].id);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar obras.'));
  }, []);

  async function carregar(id = obraId) {
    if (!id) return;
    try {
      setCarregando(true);
      setErro(null);
      setDados(await api.cursosObrigatorios.listar(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os cursos obrigatórios.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    setDados(null);
    setAviso(null);
    carregar(obraId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obraId]);

  async function executar(acao: () => Promise<string | null>) {
    try {
      setOcupado(true);
      setErro(null);
      setAviso(await acao());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível concluir a ação.');
    } finally {
      setOcupado(false);
    }
  }

  const regerar = () =>
    executar(async () => {
      const r = await api.cursosObrigatorios.gerar(obraId);
      const partes = [r.sugeridos === 0 ? 'Nenhuma sugestão nova: o PGR não trouxe curso diferente do que já está aqui.' : `${r.sugeridos} sugestão(ões) nova(s) a partir do PGR.`];
      if (r.normasSemCurso.length > 0)
        partes.push(`Sem curso no catálogo para ${r.normasSemCurso.join(', ')} — cadastre em Catálogo de Cursos e gere de novo.`);
      return partes.join(' ');
    });

  const aprovarTodas = () =>
    executar(async () => {
      const r = await api.cursosObrigatorios.aprovarSugestoes(obraId);
      return `${r.aprovados} curso(s) aprovado(s).`;
    });

  const aprovar = (item: CursoObrigatorio) =>
    executar(async () => {
      await api.cursosObrigatorios.decidir(item.id, true);
      return null;
    });

  async function confirmarRecusa() {
    if (!recusando || !justificativa.trim()) return;
    const item = recusando;
    setRecusando(null);
    await executar(async () => {
      await api.cursosObrigatorios.decidir(item.id, false, justificativa.trim());
      return null;
    });
  }

  async function abrirAdicionar() {
    try {
      if (funcoes.length === 0 || cursos.length === 0) {
        const [f, c] = await Promise.all([api.funcoes.listar(), api.cursosTreinamento.listar()]);
        setFuncoes(f);
        setCursos(c);
      }
      setNovaFuncaoId('');
      setNovoCursoId('');
      setAdicionando(true);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções e cursos.');
    }
  }

  async function confirmarAdicionar() {
    if (!novaFuncaoId || !novoCursoId) return;
    setAdicionando(false);
    await executar(async () => {
      await api.cursosObrigatorios.adicionar(obraId, novaFuncaoId, novoCursoId);
      return 'Curso adicionado e aprovado para a função.';
    });
  }

  const itens = useMemo(() => dados?.itens ?? [], [dados]);
  const sugeridos = itens.filter((i) => i.status === StatusCursoObrigatorio.Sugerido).length;
  const aprovados = itens.filter((i) => i.status === StatusCursoObrigatorio.Aprovado).length;
  const nrs = new Set(itens.filter((i) => i.status !== StatusCursoObrigatorio.Recusado).map((i) => i.normaReferencia ?? i.cursoNome)).size;

  const colunas: Coluna<CursoObrigatorio>[] = [
    {
      chave: 'funcao',
      rotulo: 'Função · GHE',
      render: (i) => (
        <div>
          <Text weight="semibold" style={{ display: 'block' }}>{i.funcaoNome}</Text>
          <Text size={200}>
            {[i.gheNumero != null ? `GHE ${String(i.gheNumero).padStart(2, '0')}` : null, i.gheSetor, `${i.trabalhadores} trabalhador(es)`]
              .filter(Boolean)
              .join(' · ')}
          </Text>
        </div>
      ),
    },
    {
      chave: 'curso',
      rotulo: 'Curso',
      render: (i) => (
        <Text>
          {/* O nome do curso costuma já começar pela NR ("NR-35 Trabalho em Altura"). */}
          {i.normaReferencia && !i.cursoNome.startsWith(i.normaReferencia) && <strong>{i.normaReferencia} </strong>}
          {i.cursoNome}
        </Text>
      ),
    },
    {
      chave: 'motivo',
      rotulo: 'Por quê (vem do PGR)',
      render: (i) => (
        <div>
          <Text style={{ display: 'block' }}>{i.motivo}</Text>
          {i.justificativaRecusa && <Text size={200}>Recusa: “{i.justificativaRecusa}”</Text>}
        </div>
      ),
    },
    {
      chave: 'status',
      rotulo: 'Situação',
      largura: '130px',
      render: (i) => <StatusChip tom={statusInfo[i.status]?.tom ?? 'neutro'}>{statusInfo[i.status]?.rotulo ?? '—'}</StatusChip>,
    },
  ];

  return (
    <div>
      <PageHeader
        titulo="Cursos obrigatórios por função"
        subtitulo={
          dados?.pgrNome
            ? `Base: ${dados.pgrNome}${dados.pgrDataElaboracao ? ` (elaborado em ${formatarData(dados.pgrDataElaboracao)})` : ''}. O sistema leu os riscos e atividades do PGR e sugeriu os cursos pelas NRs. Confira e aprove — só depois contam na aptidão da obra.`
            : 'O sistema lê os riscos e atividades do PGR da obra e sugere os cursos pelas NRs. Confira e aprove — só depois contam na aptidão da obra.'
        }
        filtros={
          <Field label="Obra">
            <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
              <option value="">Selecione a obra</option>
              {obras.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.nome}
                </option>
              ))}
            </Select>
          </Field>
        }
        acoes={
          obraId ? (
            <>
              <Button onClick={regerar} disabled={ocupado}>Regerar a partir do PGR</Button>
              <Button appearance="primary" onClick={aprovarTodas} disabled={ocupado || sugeridos === 0}>
                Aprovar todas as sugestões
              </Button>
            </>
          ) : undefined
        }
      />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {aviso && <FeedbackInline tom="info" aoFechar={() => setAviso(null)}>{aviso}</FeedbackInline>}

      {!obraId ? (
        <Card>
          <Text>Escolha a obra para ver os cursos obrigatórios que saíram do PGR dela.</Text>
        </Card>
      ) : (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12, marginBottom: 16 }}>
            <KpiCard rotulo="Funções no PGR" valor={dados?.funcoesNoPgr ?? '—'} tom="info" carregando={carregando} />
            <KpiCard rotulo="NRs aplicáveis" valor={nrs} tom="info" carregando={carregando} />
            <KpiCard rotulo="Aguardando aprovação" valor={sugeridos} tom={sugeridos > 0 ? 'atencao' : 'ok'} carregando={carregando} />
            <KpiCard rotulo="Cursos aprovados" valor={aprovados} tom="ok" carregando={carregando} />
          </div>
          <Card densidade="compacta">
            <DataTable
              aria-label="Cursos obrigatórios por função da obra"
              densidade="compacta"
              colunas={colunas}
              linhas={itens}
              chaveLinha={(i) => i.id}
              carregando={carregando}
              vazio={{
                titulo: 'Nenhum curso obrigatório nesta obra ainda',
                descricao: dados?.funcoesNoPgr
                  ? 'Clique em "Regerar a partir do PGR" para o sistema sugerir os cursos pelas NRs.'
                  : 'A obra ainda não tem PGR com GHE e funções. Gere ou importe o PGR primeiro.',
              }}
              acoesLinha={(i) =>
                i.status === StatusCursoObrigatorio.Sugerido ? (
                  <div style={{ display: 'flex', gap: 8 }}>
                    <Button size="small" appearance="primary" onClick={() => aprovar(i)} disabled={ocupado}>Aprovar</Button>
                    <Button size="small" onClick={() => { setJustificativa(''); setRecusando(i); }} disabled={ocupado}>Recusar</Button>
                  </div>
                ) : (
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    <Text size={200}>{[i.decididoPorNome, formatarData(i.decididoEmUtc)].filter(Boolean).join(' · ')}</Text>
                    {i.status === StatusCursoObrigatorio.Recusado && (
                      <Button size="small" onClick={() => aprovar(i)} disabled={ocupado}>Aprovar</Button>
                    )}
                  </div>
                )
              }
            />
          </Card>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'center', marginTop: 12 }}>
            <Text size={200}>Curso adicionado à mão continua valendo — a regeração nunca apaga nada, só sugere.</Text>
            <Button appearance="subtle" onClick={abrirAdicionar} disabled={ocupado}>+ Adicionar curso manualmente</Button>
          </div>
        </>
      )}

      <Dialog open={recusando !== null} onOpenChange={(_, d) => !d.open && setRecusando(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Recusar {recusando?.normaReferencia ?? recusando?.cursoNome} para {recusando?.funcaoNome}</DialogTitle>
            <DialogContent>
              <Field label="Por que o curso não é obrigatório para esta função nesta obra?" required>
                <Textarea value={justificativa} onChange={(_, d) => setJustificativa(d.value)} maxLength={500} />
              </Field>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setRecusando(null)}>Cancelar</Button>
              <Button appearance="primary" onClick={confirmarRecusa} disabled={!justificativa.trim()}>Recusar</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={adicionando} onOpenChange={(_, d) => !d.open && setAdicionando(false)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Adicionar curso obrigatório</DialogTitle>
            <DialogContent>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <Field label="Função" required>
                  <SeletorPesquisavel
                    opcoes={funcoes.map((f) => ({ id: f.id, rotulo: f.nome }))}
                    valor={novaFuncaoId}
                    aoMudar={setNovaFuncaoId}
                    placeholder="Buscar função"
                  />
                </Field>
                <Field label="Curso" required>
                  <SeletorPesquisavel
                    opcoes={cursos.map((c) => ({ id: c.id, rotulo: c.normaReferencia ? `${c.normaReferencia} · ${c.nome}` : c.nome }))}
                    valor={novoCursoId}
                    aoMudar={setNovoCursoId}
                    placeholder="Buscar curso"
                  />
                </Field>
              </div>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setAdicionando(false)}>Cancelar</Button>
              <Button appearance="primary" onClick={confirmarAdicionar} disabled={!novaFuncaoId || !novoCursoId}>Adicionar</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
