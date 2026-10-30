import { useState, type ReactNode } from 'react';
import { Button, StatusChip, Text } from '@ui';
import type { AtividadeGerada, EstruturaGeracaoPgr, GheGerado, RiscoGerado } from '../../lib/api';
import { agruparPorPerigo, compararComPadrao, nivelDe, nivelMatriz, type CelulaMatriz, type DiferencaDoPadrao, type GrupoPerigo, type RefRisco } from './agruparPorPerigo';

const FASES: Record<number, string> = { 1: 'Antes', 2: 'Durante', 3: 'Após' };
const linha = { borderTop: '1px solid var(--colorNeutralStroke2)' } as const;

export function InventarioPorPerigo({ gh, gi, editavel, ocupado, matriz, alterar, gerarAtividade, renderRisco }: {
  gh: GheGerado;
  matriz?: CelulaMatriz[];
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
        const difs = new Map(gr.itens.map((i) => [i, compararComPadrao(gr.padrao.r, i.r)] as const));
        const nDif = [...difs.values()].filter((d) => !d.igual).length;
        const pendentes = gr.itens.some((i) => !i.r.revisado || i.r.medidas.some((m) => m.decisao === 'Pendente'));
        // Cor e piscar pelo nível da matriz do pior caso do grupo: só o Crítico pisca.
        const pior = gr.itens.map((i) => nivelMatriz(matriz, i.r)).reduce((a, b) => (b.ordem > a.ordem ? b : a));
        const tom = pior.tom;
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
                  <StatusChip tom={tom}>P × S = {min === max ? max : `${min}–${max}`}{pior.rotulo ? ` · ${pior.rotulo}` : ''}</StatusChip>
                  {nDif > 0 && <StatusChip tom="atencao">{nDif} fora do padrão</StatusChip>}
                  {nDif === 0 && gr.itens.length > 1 && <StatusChip tom="ok">todas iguais ao padrão</StatusChip>}
                  {!pendentes && <StatusChip tom="ok">Revisado</StatusChip>}
                </div>
              </div>
              {editavel && pendentes && (
                <Button size="small" appearance="primary" onClick={() => revisarGrupo(gr)}>
                  Revisar e aceitar medidas ({gr.itens.length})
                </Button>
              )}
            </div>
            <DetalheLazy resumo={<Text size={200}>Medidas padrão ({medidasPadrao.length}) · atividade {gr.padrao.atividade.codigoEap}</Text>}>
              {() => (
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
              )}
            </DetalheLazy>
            <DetalheLazy resumo={<Text size={200}>Atividades onde aparece ({gr.itens.length})</Text>}>
              {() => (
                <div style={{ padding: '0 12px 10px' }}>
                  {gr.itens.map((i) => (
                    <LinhaAtividade key={`${i.ai}-${i.ri}`} i={i} dif={difs.get(i)!} ehPadrao={i === gr.padrao}
                      editor={() => renderRisco(i.r, (f) => alterar((x) => f(x.ghes[gi].atividades[i.ai].riscos[i.ri])))} />
                  ))}
                </div>
              )}
            </DetalheLazy>
          </div>
        );
      })}
    </>
  );
}

// O editor completo do risco só é montado quando a linha é aberta: um PGR de obra grande tem mais de mil riscos
// e montar todos os editores de uma vez trava a tela.
function LinhaAtividade({ i, dif, ehPadrao, editor }: { i: RefRisco; dif: DiferencaDoPadrao; ehPadrao: boolean; editor: () => ReactNode }) {
  const [aberto, setAberto] = useState(false);
  return (
    <details style={{ borderTop: '1px dashed var(--colorNeutralStroke2)', padding: '4px 0' }} onToggle={(ev) => setAberto(ev.currentTarget.open)}>
      <summary style={{ cursor: 'pointer', display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <Text size={200}>{i.atividade.codigoEap} {i.atividade.nome}</Text>
        <StatusChip tom="neutro">P × S = {nivelDe(i.r)}</StatusChip>
        {ehPadrao ? <Text size={200}>padrão</Text> : dif.igual ? <Text size={200}>igual ao padrão</Text> : <MotivosDiferenca dif={dif} />}
        {i.r.revisado && <StatusChip tom="ok">Revisado</StatusChip>}
      </summary>
      {aberto && !ehPadrao && !dif.igual && <DetalheDiferenca dif={dif} />}
      {aberto && editor()}
    </details>
  );
}

// Conteúdo montado só quando o bloco é aberto, para a tela do GHE não carregar milhares de elementos fechados.
function DetalheLazy({ resumo, children }: { resumo: ReactNode; children: () => ReactNode }) {
  const [aberto, setAberto] = useState(false);
  return (
    <details style={linha} onToggle={(ev) => setAberto(ev.currentTarget.open)}>
      <summary style={{ padding: '8px 12px', cursor: 'pointer' }}>{resumo}</summary>
      {aberto && children()}
    </details>
  );
}

const plural = (n: number, um: string, varios: string) => (n === 1 ? `1 ${um}` : `${n} ${varios}`);

// Diz por que a atividade está fora do padrão: nível, medidas só dela ou medidas que só o padrão tem.
function MotivosDiferenca({ dif }: { dif: DiferencaDoPadrao }) {
  return (
    <>
      {dif.nivel && <StatusChip tom="atencao">nível diferente</StatusChip>}
      {dif.aMais.length > 0 && <StatusChip tom="atencao">{plural(dif.aMais.length, 'medida a mais', 'medidas a mais')}</StatusChip>}
      {dif.aMenos.length > 0 && <StatusChip tom="atencao">{plural(dif.aMenos.length, 'medida a menos', 'medidas a menos')}</StatusChip>}
    </>
  );
}

function DetalheDiferenca({ dif }: { dif: DiferencaDoPadrao }) {
  return (
    <div style={{ margin: '6px 0', padding: '6px 10px', borderLeft: '3px solid var(--colorPaletteYellowBorder1)', background: 'var(--colorPaletteYellowBackground1)', borderRadius: '0 6px 6px 0' }}>
      {dif.nivel && <Text size={200} style={{ display: 'block' }}><b>Nível:</b> {dif.nivel.atividade} nesta atividade, {dif.nivel.padrao} no padrão.</Text>}
      {dif.aMais.length > 0 && (
        <Text size={200} style={{ display: 'block' }}><b>Só nesta atividade:</b> {dif.aMais.map((m) => m.texto).join(' • ')}</Text>
      )}
      {dif.aMenos.length > 0 && (
        <Text size={200} style={{ display: 'block' }}><b>Só no padrão:</b> {dif.aMenos.map((m) => m.texto).join(' • ')}</Text>
      )}
    </div>
  );
}
