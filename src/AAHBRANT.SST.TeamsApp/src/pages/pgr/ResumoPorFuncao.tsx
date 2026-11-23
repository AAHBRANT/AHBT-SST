import { useEffect, useMemo, useState } from 'react';
import { Button, Card, Carregando, Checkbox, EstadoVazio, FeedbackInline, Input, StatusChip, Text } from '@ui';
import type { ResumoFuncaoPgr, ResumoPorFuncaoGeracaoPgr } from '../../lib/api';

// Resumo por função do PGR (mockup aprovado 23/11/2026, docs/mockups/pgr-resumo-por-funcao-2026-11-23.html): o PGR é
// organizado por GHE, mas quem confere pensa por função. Lista à esquerda (com pendência), detalhe à direita: riscos
// do mais alto para o mais baixo, EPIs das medidas e treinamentos pelas regras Risco → NR. Só leitura.
const NIVEL: Record<number, { rotulo: string; tom: 'alerta' | 'atencao' | 'info' | 'ok' }> = {
  5: { rotulo: 'Crítico', tom: 'alerta' },
  4: { rotulo: 'Alto', tom: 'atencao' },
  3: { rotulo: 'Moderado', tom: 'info' },
  2: { rotulo: 'Baixo', tom: 'ok' },
  1: { rotulo: 'Trivial', tom: 'ok' },
};
const VINHO = '#670000';
const borda = '1px solid var(--colorNeutralStroke2)';
const ghe = (n: number) => `GHE ${String(n).padStart(2, '0')}`;

export function ResumoPorFuncao({ carregar, aoIrParaGhe }: {
  carregar: () => Promise<ResumoPorFuncaoGeracaoPgr>;
  // No rascunho: leva ao GHE para corrigir. No PGR aprovado não há o que editar aqui.
  aoIrParaGhe?: (numero: number) => void;
}) {
  const [dados, setDados] = useState<ResumoPorFuncaoGeracaoPgr | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [busca, setBusca] = useState('');
  const [soPendentes, setSoPendentes] = useState(false);
  const [selecionada, setSelecionada] = useState<string | null>(null);

  useEffect(() => {
    let vivo = true;
    carregar()
      .then((d) => {
        if (!vivo) return;
        setDados(d);
        setSelecionada((atual) => atual ?? (d.funcoes.find((f) => f.riscosSemMedida > 0) ?? d.funcoes[0])?.nome ?? null);
      })
      .catch((e) => vivo && setErro(e instanceof Error ? e.message : 'Não foi possível montar o resumo por função.'));
    return () => { vivo = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const lista = useMemo(() => {
    const q = busca.trim().toLowerCase();
    return (dados?.funcoes ?? []).filter((f) => f.nome.toLowerCase().includes(q) && (!soPendentes || f.riscosSemMedida > 0));
  }, [dados, busca, soPendentes]);

  if (erro) return <EstadoVazio titulo="Resumo por função indisponível" descricao={erro} />;
  if (!dados) return <Carregando variante="detalhe" linhas={6} />;
  if (dados.funcoes.length === 0) return <EstadoVazio titulo="Nenhuma função nos GHEs" descricao="Inclua as funções nos GHEs para ver o resumo." />;

  const atual = dados.funcoes.find((f) => f.nome === selecionada) ?? dados.funcoes[0];
  const comPendencia = dados.funcoes.filter((f) => f.riscosSemMedida > 0).length;

  const kpi = (valor: number, rotulo: string, alerta = false) => (
    <div style={{ background: 'var(--colorNeutralBackground1)', border: borda, borderLeft: `4px solid ${alerta ? 'var(--colorPaletteDarkOrangeForeground1)' : VINHO}`, borderRadius: 8, padding: '10px 14px' }}>
      <Text size={200} style={{ display: 'block', color: 'var(--colorNeutralForeground3)' }}>{rotulo}</Text>
      <Text size={600} weight="bold" style={{ color: alerta && valor > 0 ? 'var(--colorPaletteDarkOrangeForeground1)' : undefined }}>{valor}</Text>
    </div>
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 10 }}>
        {kpi(dados.funcoes.length, 'Funções no PGR')}
        {kpi(dados.funcoes.filter((f) => f.temQuedaDeAltura).length, 'Com Queda de altura')}
        {kpi(comPendencia, 'Com risco sem medida', true)}
        {kpi(dados.totalGhes, 'GHEs')}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(240px, 300px) minmax(0, 1fr)', gap: 14, alignItems: 'start' }} className="resumo-por-funcao">
        <style>{'@media (max-width: 860px) { .resumo-por-funcao { grid-template-columns: 1fr !important; } }'}</style>
        <div style={{ background: 'var(--colorNeutralBackground1)', border: borda, borderRadius: 8, overflow: 'hidden', position: 'sticky', top: 8 }}>
          <div style={{ padding: 10, borderBottom: borda, display: 'flex', flexDirection: 'column', gap: 6 }}>
            <Input placeholder="Buscar função…" value={busca} onChange={(_, d) => setBusca(d.value)} aria-label="Buscar função" />
            <Checkbox label="Só funções com pendência" checked={soPendentes} onChange={(_, d) => setSoPendentes(!!d.checked)} />
          </div>
          <div role="listbox" aria-label="Funções do PGR" style={{ maxHeight: '60vh', overflowY: 'auto' }}>
            {lista.length === 0 && <Text size={200} style={{ display: 'block', padding: 12 }}>Nenhuma função encontrada.</Text>}
            {lista.map((f) => {
              const ativa = f.nome === atual.nome;
              return (
                <button key={f.nome} type="button" role="option" aria-selected={ativa} onClick={() => setSelecionada(f.nome)}
                  style={{
                    all: 'unset', boxSizing: 'border-box', width: '100%', cursor: 'pointer', display: 'flex', justifyContent: 'space-between',
                    alignItems: 'center', gap: 8, padding: '9px 12px', borderBottom: '1px solid var(--colorNeutralStroke3)',
                    background: ativa ? 'var(--colorNeutralBackground1Selected)' : undefined, boxShadow: ativa ? `inset 3px 0 0 ${VINHO}` : undefined,
                  }}>
                  <span>
                    <Text weight="semibold" size={300} style={{ display: 'block' }}>{f.nome}</Text>
                    <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
                      {f.riscos.length} riscos · {f.ghes.length} {f.ghes.length > 1 ? 'GHEs' : 'GHE'}
                    </Text>
                  </span>
                  {f.riscosSemMedida > 0
                    ? <StatusChip tom="atencao">{f.riscosSemMedida} sem medida</StatusChip>
                    : <StatusChip tom="ok">OK</StatusChip>}
                </button>
              );
            })}
          </div>
        </div>

        <Detalhe f={atual} aoIrParaGhe={aoIrParaGhe} />
      </div>
      <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
        EPIs saem das medidas do tipo EPI de cada risco. Treinamentos saem das regras de Treinamentos › Regras por NR e só passam a
        valer quando aprovados em Cursos obrigatórios por obra, depois que o PGR é aprovado.
      </Text>
    </div>
  );
}

function Detalhe({ f, aoIrParaGhe }: { f: ResumoFuncaoPgr; aoIrParaGhe?: (numero: number) => void }) {
  const semMedida = f.riscos.filter((r) => r.ghesSemMedida.length > 0);
  const th: React.CSSProperties = { textAlign: 'left', fontSize: 11, textTransform: 'uppercase', color: 'var(--colorNeutralForeground3)', padding: '6px 8px', borderBottom: borda };
  const td: React.CSSProperties = { padding: '8px', borderBottom: '1px solid var(--colorNeutralStroke3)', verticalAlign: 'top' };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, minWidth: 0 }}>
      <Card titulo={f.nome} subtitulo={`CBO ${f.cbo ?? '—'}${f.cboTitulo && f.cboTitulo !== f.nome ? ` · ${f.cboTitulo}` : ''}`}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
          {f.ghes.map((g) => <StatusChip key={g.numero} tom="neutro">{ghe(g.numero)} · {g.nome}</StatusChip>)}
        </div>
        {semMedida.length > 0 && (
          <div style={{ marginTop: 12 }}>
            <FeedbackInline tom="aviso"
              acao={aoIrParaGhe ? { rotulo: `Ir para o ${ghe(semMedida[0].ghesSemMedida[0])}`, aoClicar: () => aoIrParaGhe(semMedida[0].ghesSemMedida[0]) } : undefined}>
              {semMedida.map((r) => `${r.perigo} está sem medida de prevenção (${r.ghesSemMedida.map(ghe).join(', ')}).`).join(' ')}
            </FeedbackInline>
          </div>
        )}
      </Card>

      <Card titulo="Riscos da função" subtitulo={`${f.riscos.length} riscos, do mais alto para o mais baixo`}>
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead><tr><th style={th}>Perigo</th><th style={th}>Nível</th><th style={th}>Medidas</th></tr></thead>
            <tbody>
              {f.riscos.map((r) => {
                const n = r.nivel ? NIVEL[r.nivel] : undefined;
                const pontos = r.probabilidade * r.severidade;
                return (
                  <tr key={r.perigo}>
                    <td style={td}>
                      <Text size={300} style={{ display: 'block' }}>{r.perigo}</Text>
                      <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
                        {r.tipo}{r.incluidoPeloTecnico ? ' · incluído pelo técnico' : ''}
                      </Text>
                    </td>
                    <td style={td}>{n ? <StatusChip tom={n.tom}>{pontos} · {n.rotulo}</StatusChip> : <StatusChip tom="neutro">{pontos}</StatusChip>}</td>
                    <td style={td}>
                      {r.ghesSemMedida.length > 0
                        ? <Text weight="semibold" style={{ color: 'var(--colorPaletteDarkOrangeForeground1)' }}>Nenhuma{r.medidas > 0 ? ` em ${r.ghesSemMedida.map(ghe).join(', ')}` : ''}</Text>
                        : r.medidas}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </Card>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 12 }}>
        <Card titulo="EPIs" subtitulo="Pelas medidas do PGR">
          {f.epis.length === 0 ? <Text size={200}>Nenhum EPI nas medidas.</Text> : (
            <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
              {f.epis.map((e) => (
                <li key={`${e.nome}|${e.especificacao}`} style={{ padding: '6px 0', borderBottom: '1px solid var(--colorNeutralStroke3)' }}>
                  <Text weight="semibold" style={{ display: 'block' }}>{e.nome}</Text>
                  <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>Por: {e.perigos.join(', ')}</Text>
                </li>
              ))}
            </ul>
          )}
        </Card>
        <Card titulo="Treinamentos obrigatórios" subtitulo="Pelas regras Risco → NR">
          {f.treinamentos.length === 0 ? <Text size={200}>Nenhuma regra se aplica.</Text> : (
            <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
              {f.treinamentos.map((t) => (
                <li key={t.norma} style={{ padding: '6px 0', borderBottom: '1px solid var(--colorNeutralStroke3)' }}>
                  <span style={{ background: VINHO, color: '#fff', fontSize: 11, fontWeight: 700, padding: '2px 7px', borderRadius: 4, marginRight: 6 }}>{t.norma}</span>
                  <Text weight="semibold">{t.descricao}</Text>
                  <Text size={200} style={{ display: 'block', color: 'var(--colorNeutralForeground3)' }}>Por: {t.motivos.join(', ')}</Text>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
      {aoIrParaGhe && semMedida.length === 0 && f.ghes.length > 0 && (
        <div><Button size="small" onClick={() => aoIrParaGhe(f.ghes[0].numero)}>Ver no {ghe(f.ghes[0].numero)}</Button></div>
      )}
    </div>
  );
}
