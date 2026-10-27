import type { ReactNode } from 'react';
import { Button, StatusChip, Text } from '@ui';
import type { AtividadeGerada, EstruturaGeracaoPgr, GheGerado, RiscoGerado } from '../../lib/api';
import { agruparPorPerigo, assinaturaMedidas, nivelDe, type GrupoPerigo, type RefRisco } from './agruparPorPerigo';

const FASES: Record<number, string> = { 1: 'Antes', 2: 'Durante', 3: 'Após' };
const linha = { borderTop: '1px solid var(--colorNeutralStroke2)' } as const;

export function InventarioPorPerigo({ gh, gi, editavel, ocupado, alterar, gerarAtividade, renderRisco }: {
  gh: GheGerado;
  gi: number;
  editavel: boolean;
  ocupado: boolean;
  alterar: (f: (x: EstruturaGeracaoPgr) => void) => void;
  gerarAtividade: (gh: GheGerado, a: AtividadeGerada) => void;
  renderRisco: (r: RiscoGerado, alterar: (f: (r: RiscoGerado) => void) => void) => ReactNode;
}) {
  const grupos = agruparPorPerigo(gh);
  const semRiscos = gh.atividades.filter((a) => !a.riscosGerados);

  const revisarGrupo = (gr: GrupoPerigo) => alterar((x) => {
    gr.itens.forEach(({ ai, ri }) => {
      const r = x.ghes[gi].atividades[ai].riscos[ri];
      r.revisado = true;
      r.medidas.forEach((m) => { if (m.decisao === 'Pendente') m.decisao = 'Aceita'; });
    });
  });

  return (
    <>
      <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
        {grupos.length} perigo(s) em {gh.atividades.length} atividade(s). Revisar um perigo aceita as medidas dele em todas as atividades onde aparece;
        as que diferem do padrão ficam marcadas e podem ser editadas uma a uma.
      </Text>
      {semRiscos.length > 0 && (
        <div style={{ marginBottom: 8 }}>
          <Text size={200}>Sem riscos gerados: {semRiscos.map((a) => a.codigoEap).join(', ')}. </Text>
          {editavel && semRiscos.map((a) => (
            <Button key={a.codigoEap} size="small" disabled={ocupado} onClick={() => gerarAtividade(gh, a)} style={{ marginRight: 4 }}>Gerar {a.codigoEap}</Button>
          ))}
        </div>
      )}
      {grupos.map((gr) => {
        const niveis = gr.itens.map((i) => nivelDe(i.r));
        const min = Math.min(...niveis);
        const max = Math.max(...niveis);
        const nivelPadrao = nivelDe(gr.padrao.r);
        const assPadrao = assinaturaMedidas(gr.padrao.r);
        const diferentes = (i: RefRisco) => nivelDe(i.r) !== nivelPadrao || assinaturaMedidas(i.r) !== assPadrao;
        const nDif = gr.itens.filter(diferentes).length;
        const pendentes = gr.itens.some((i) => !i.r.revisado || i.r.medidas.some((m) => m.decisao === 'Pendente'));
        const tom = max >= 20 ? 'alerta' : max >= 10 ? 'atencao' : 'info';
        const medidasPadrao = gr.padrao.r.medidas.filter((m) => m.decisao !== 'Descartada');
        return (
          <div key={gr.chave} style={{
            border: '1px solid var(--colorNeutralStroke2)', borderRadius: 8, marginBottom: 10,
            background: pendentes ? 'var(--colorPaletteYellowBackground1)' : undefined,
          }}>
            <div style={{ display: 'flex', gap: 8, padding: '10px 12px', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap' }}>
              <div style={{ minWidth: 0 }}>
                <Text weight="semibold">{gr.nome}</Text>{' '}
                <StatusChip tom="neutro">{gr.tipo}</StatusChip>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginTop: 4 }}>
                  <Text size={200}>em {gr.itens.length} atividade(s)</Text>
                  <StatusChip tom={tom}>P × S = {min === max ? max : `${min}–${max}`}</StatusChip>
                  {nDif > 0 && <StatusChip tom="atencao">{nDif} fora do padrão</StatusChip>}
                  {!pendentes && <StatusChip tom="ok">Revisado</StatusChip>}
                </div>
              </div>
              {editavel && pendentes && (
                <Button size="small" appearance="primary" onClick={() => revisarGrupo(gr)}>
                  Revisar e aceitar medidas ({gr.itens.length})
                </Button>
              )}
            </div>
            <details style={linha}>
              <summary style={{ padding: '8px 12px', cursor: 'pointer' }}>
                <Text size={200}>Medidas padrão ({medidasPadrao.length}) · atividade {gr.padrao.atividade.codigoEap}</Text>
              </summary>
              <div style={{ padding: '0 12px 10px' }}>
                {medidasPadrao.map((m, mi) => (
                  <div key={mi} style={{ display: 'flex', gap: 6, alignItems: 'center', padding: '2px 0', flexWrap: 'wrap' }}>
                    <StatusChip tom="neutro">{FASES[m.fase]}</StatusChip>
                    <StatusChip tom="neutro">{m.categoria ?? 'Sem classificação'}</StatusChip>
                    <Text style={{ flex: 1 }}>{m.texto}</Text>
                    {m.decisao === 'Aceita' ? <StatusChip tom="ok">Aceita</StatusChip> : <StatusChip tom="atencao">Sugestão da IA</StatusChip>}
                  </div>
                ))}
              </div>
            </details>
            <details style={linha}>
              <summary style={{ padding: '8px 12px', cursor: 'pointer' }}>
                <Text size={200}>Atividades onde aparece ({gr.itens.length})</Text>
              </summary>
              <div style={{ padding: '0 12px 10px' }}>
                {gr.itens.map((i) => (
                  <details key={`${i.ai}-${i.ri}`} style={{ borderTop: '1px dashed var(--colorNeutralStroke2)', padding: '4px 0' }}>
                    <summary style={{ cursor: 'pointer', display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                      <Text size={200}>{i.atividade.codigoEap} {i.atividade.nome}</Text>
                      <StatusChip tom="neutro">P × S = {nivelDe(i.r)}</StatusChip>
                      {diferentes(i) ? <StatusChip tom="atencao">fora do padrão</StatusChip> : <Text size={200}>igual ao padrão</Text>}
                      {i.r.revisado && <StatusChip tom="ok">Revisado</StatusChip>}
                    </summary>
                    {renderRisco(i.r, (f) => alterar((x) => f(x.ghes[gi].atividades[i.ai].riscos[i.ri])))}
                  </details>
                ))}
              </div>
            </details>
          </div>
        );
      })}
    </>
  );
}
