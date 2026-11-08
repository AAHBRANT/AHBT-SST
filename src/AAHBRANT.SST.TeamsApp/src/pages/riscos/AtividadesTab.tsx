import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, FeedbackInline, Field, Input, Select, StatusChip } from '@ui';
import {
  api,
  StatusPgr,
  type Atividade,
  type Obra,
  type Pgr,
  type ResumoGeracaoPgrEap,
} from '../../lib/api';
import { dataBr } from '../../components/documentos-obra/CartoesVigentesPorObra';

// Aba Atividades só para consulta (07/11/2026, mockup atividades-consulta-pgr_2026-11-07_v1): as
// atividades nascem da aprovação do PGR pela EAP (uma por item da EAP, dentro do GHE, com os riscos).
// O cadastro manual saiu — corrigir atividade = nova revisão do PGR. A entidade continua: APR, PT,
// DDS, inspeções, ocorrências, cursos obrigatórios e escopo do PCMSO dependem dela.
const st: Record<string, CSSProperties> = {
  barra: { display: 'flex', gap: 12, alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: 16 },
  origem: {
    display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap',
    padding: '10px 14px', background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 8, marginBottom: 14, fontSize: 13,
  },
  ghe: {
    padding: '8px 12px', fontSize: 12, fontWeight: 700, color: '#670000', textTransform: 'uppercase',
    letterSpacing: '.04em', background: '#fafafa', borderTop: '1px solid #e5e7eb', borderBottom: '1px solid #e5e7eb',
  },
  tabela: { width: '100%', borderCollapse: 'collapse', fontSize: 13, tableLayout: 'fixed' },
  th: { textAlign: 'left', padding: '8px 12px', fontSize: 11, color: '#6b7280', textTransform: 'uppercase', fontWeight: 600 },
  td: { padding: '9px 12px', borderBottom: '1px solid #f0f0f0', verticalAlign: 'middle' },
  chips: { display: 'flex', gap: 4, flexWrap: 'wrap' },
  vazio: { color: '#6b7280', padding: '8px 0' },
};

// "3.1 Montagem de formas" -> código "3.1" + nome (o nome da atividade gerada vem com o código EAP na frente).
function separarCodigo(nome: string) {
  const m = /^(\d+(?:\.\d+)*)\s+(.*)$/.exec(nome);
  return m ? { codigo: m[1], nome: m[2] } : { codigo: '—', nome };
}

export function AtividadesTab() {
  const navigate = useNavigate();
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [pgrs, setPgrs] = useState<Pgr[]>([]);
  const [geracoes, setGeracoes] = useState<ResumoGeracaoPgrEap[]>([]);
  const [obraId, setObraId] = useState('');
  const [busca, setBusca] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const [a, o, p, g] = await Promise.all([
          api.atividades.listar(),
          api.obras.listar(),
          api.pgrs.listar(),
          api.geracoesPgrEap.listar().catch(() => [] as ResumoGeracaoPgrEap[]),
        ]);
        setAtividades(a);
        setObras(o);
        setPgrs(p);
        setGeracoes(g);
        const comAtividade = o.find((x) => a.some((y) => y.obraId === x.id));
        setObraId((atual) => atual || comAtividade?.id || o[0]?.id || '');
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Não foi possível carregar as atividades.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const pgrVigente = pgrs.find((p) => p.obraId === obraId && p.status === StatusPgr.Vigente);
  const geracao = pgrVigente ? geracoes.find((g) => g.pgrId === pgrVigente.id) : undefined;

  const grupos = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    const lista = atividades
      .filter((a) => a.obraId === obraId)
      .filter((a) => !termo || a.nome.toLowerCase().includes(termo) || (a.gheSetor ?? '').toLowerCase().includes(termo));
    const mapa = new Map<string, { titulo: string; ordem: number; itens: Atividade[] }>();
    for (const a of lista) {
      const chave = a.gheNumero != null ? `ghe-${a.gheNumero}` : 'sem-ghe';
      if (!mapa.has(chave)) {
        mapa.set(chave, {
          titulo: a.gheNumero != null
            ? `GHE ${String(a.gheNumero).padStart(2, '0')} · ${a.gheSetor ?? ''}`
            : 'Sem GHE (cadastro manual antigo)',
          ordem: a.gheNumero ?? 9999,
          itens: [],
        });
      }
      mapa.get(chave)!.itens.push(a);
    }
    const ordenarCodigo = (x: Atividade, y: Atividade) =>
      x.nome.localeCompare(y.nome, 'pt-BR', { numeric: true });
    return [...mapa.values()].sort((x, y) => x.ordem - y.ordem).map((g) => ({ ...g, itens: g.itens.sort(ordenarCodigo) }));
  }, [atividades, obraId, busca]);

  return (
    <div>
      <div style={st.barra}>
        <Field label="Obra" style={{ minWidth: 260 }}>
          <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
            {obras.map((o) => (
              <option key={o.id} value={o.id}>{o.nome}</option>
            ))}
          </Select>
        </Field>
        <Field label="Buscar" style={{ minWidth: 240 }}>
          <Input value={busca} onChange={(_, d) => setBusca(d.value)} placeholder="Atividade, código EAP ou GHE" />
        </Field>
      </div>

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card
        titulo="Atividades do PGR vigente"
        subtitulo="Somente consulta — vêm do PGR aprovado. Usadas em APR, PT, DDS, inspeções e ocorrências. Para corrigir, faça uma nova revisão do PGR pela EAP."
      >
        {carregando ? (
          <div style={st.vazio}>Carregando…</div>
        ) : (
          <>
            {pgrVigente ? (
              <div style={st.origem}>
                <span>
                  Origem:{' '}
                  <b style={{ color: '#166534' }}>
                    {geracao ? 'PGR gerado pela EAP' : 'PGR importado (legado)'} · {pgrVigente.nome}
                  </b>
                  {geracao?.aprovadoPorNome ? ` · aprovado por ${geracao.aprovadoPorNome}` : ''} · elaboração{' '}
                  {dataBr(pgrVigente.dataElaboracao)}
                </span>
                <Button onClick={() => navigate(`/prevencao/pgr/${pgrVigente.id}`)}>Abrir PGR</Button>
              </div>
            ) : (
              <div style={{ ...st.origem, background: '#fff7ed', borderColor: '#fed7aa' }}>
                <span>Esta obra ainda não tem PGR vigente. As atividades aparecem aqui quando o PGR for aprovado (aba PGRs).</span>
              </div>
            )}

            {grupos.length === 0 ? (
              <div style={st.vazio}>{busca ? 'Nenhuma atividade encontrada para essa busca.' : 'Nenhuma atividade nesta obra.'}</div>
            ) : (
              grupos.map((g, i) => (
                <div key={g.titulo}>
                  <div style={st.ghe}>{g.titulo}</div>
                  <table style={st.tabela}>
                    {i === 0 && (
                      <thead>
                        <tr>
                          <th style={{ ...st.th, width: 110 }}>Código EAP</th>
                          <th style={st.th}>Atividade</th>
                          <th style={{ ...st.th, width: 240 }}>Riscos</th>
                        </tr>
                      </thead>
                    )}
                    <tbody>
                      {g.itens.map((a) => {
                        const { codigo, nome } = separarCodigo(a.nome);
                        return (
                          <tr key={a.id}>
                            <td style={{ ...st.td, width: 110 }}>{codigo}</td>
                            <td style={st.td}>{nome}</td>
                            <td style={{ ...st.td, width: 240 }}>
                              <div style={st.chips}>
                                {!!a.riscosAltos && <StatusChip tom="alerta">{a.riscosAltos} alto</StatusChip>}
                                {!!a.riscosModerados && <StatusChip tom="atencao">{a.riscosModerados} médio</StatusChip>}
                                {!!a.riscosBaixos && <StatusChip tom="ok">{a.riscosBaixos} baixo</StatusChip>}
                                {!a.riscosAltos && !a.riscosModerados && !a.riscosBaixos && (
                                  <span style={{ color: '#9ca3af' }}>Sem riscos</span>
                                )}
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              ))
            )}
          </>
        )}
      </Card>
    </div>
  );
}
