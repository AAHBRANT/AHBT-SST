import { Button, Field, Input, Select, StatusChip, Text } from '@ui';
import type { CatalogoEpi, MedicaoExposicao, MedidaGerada, RiscoGerado } from '../../lib/api';

const dataInput = (iso?: string | null) => (iso ? iso.slice(0, 10) : '');

// Unidade usual da medição segundo o perigo (o técnico pode trocar). Os limites de tolerância e o nível de ação
// ficam por conta do técnico: confira na NR-15 e nas NHO da Fundacentro.
function unidadeSugerida(r: RiscoGerado): string {
  const t = `${r.perigo} ${r.tipo}`.toLowerCase();
  if (t.includes('ruíd') || t.includes('ruid')) return 'dB(A)';
  if (t.includes('calor')) return '°C (IBUTG)';
  if (t.includes('vibra')) return 'm/s²';
  if (/poeira|sílica|silica|fumo|névoa|nevoa|gás|gas/.test(t)) return 'mg/m³';
  return '';
}

// Exposição do risco: como foi avaliado (qualitativo pela matriz, ou quantitativo com medição) e as medições com
// data, valor, método e equipamento. A IA não preenche nada daqui: medição é dado de campo.
export function ExposicaoRiscoEditor({ r, editavel, alterar }: {
  r: RiscoGerado;
  editavel: boolean;
  alterar: (f: (r: RiscoGerado) => void) => void;
}) {
  const exp = r.exposicao ?? { criterio: 'Qualitativo', perfil: null, medicoes: [] };
  const quantitativo = exp.criterio === 'Quantitativo';

  const garantir = (x: RiscoGerado) => {
    x.exposicao ??= { criterio: 'Qualitativo', perfil: null, medicoes: [] };
    return x.exposicao;
  };

  return (
    <div style={{ marginTop: 8, paddingTop: 8, borderTop: '1px dashed var(--colorNeutralStroke2)' }}>
      <Text weight="semibold" size={200}>Exposição</Text>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end', marginTop: 4 }}>
        <Field label="Critério">
          <Select value={exp.criterio} disabled={!editavel}
            onChange={(_, d) => alterar((x) => { garantir(x).criterio = d.value as 'Qualitativo' | 'Quantitativo'; })}>
            <option value="Qualitativo">Qualitativo (pela matriz)</option>
            <option value="Quantitativo">Quantitativo (com medição)</option>
          </Select>
        </Field>
        <Field label="Perfil de exposição" style={{ flex: 1, minWidth: 240 }}>
          <Input value={exp.perfil ?? ''} disabled={!editavel} placeholder="Quem se expõe, com que frequência e por quanto tempo"
            onChange={(_, d) => alterar((x) => { garantir(x).perfil = d.value; })} />
        </Field>
      </div>

      {quantitativo && (
        <div style={{ marginTop: 6 }}>
          {exp.medicoes.length === 0 && <StatusChip tom="atencao">Informe ao menos uma medição</StatusChip>}
          {exp.medicoes.map((m, i) => (
            <MedicaoLinha key={i} medicao={m} editavel={editavel}
              alterar={(f) => alterar((x) => f(garantir(x).medicoes[i]))}
              remover={() => alterar((x) => { garantir(x).medicoes.splice(i, 1); })} />
          ))}
          {editavel && (
            <Button size="small" onClick={() => alterar((x) => { garantir(x).medicoes.push({ unidade: unidadeSugerida(x) || null }); })}>
              Adicionar medição
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

function MedicaoLinha({ medicao, editavel, alterar, remover }: {
  medicao: MedicaoExposicao;
  editavel: boolean;
  alterar: (f: (m: MedicaoExposicao) => void) => void;
  remover: () => void;
}) {
  return (
    <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center', marginBottom: 6 }}>
      <Input type="date" value={dataInput(medicao.data)} disabled={!editavel} aria-label="Data da medição"
        onChange={(_, d) => alterar((m) => { m.data = d.value || null; })} />
      <Input type="number" value={medicao.valor != null ? String(medicao.valor) : ''} disabled={!editavel} placeholder="Valor" style={{ width: 90 }}
        onChange={(_, d) => alterar((m) => { m.valor = d.value === '' ? null : Number(d.value); })} />
      <Input value={medicao.unidade ?? ''} disabled={!editavel} placeholder="Unidade" style={{ width: 100 }}
        onChange={(_, d) => alterar((m) => { m.unidade = d.value; })} />
      <Input value={medicao.limiteTolerancia ?? ''} disabled={!editavel} placeholder="Limite de tolerância" style={{ width: 140 }}
        onChange={(_, d) => alterar((m) => { m.limiteTolerancia = d.value; })} />
      <Input value={medicao.nivelAcao ?? ''} disabled={!editavel} placeholder="Nível de ação" style={{ width: 120 }}
        onChange={(_, d) => alterar((m) => { m.nivelAcao = d.value; })} />
      <Input value={medicao.empresa ?? ''} disabled={!editavel} placeholder="Empresa" style={{ flex: 1, minWidth: 140 }}
        onChange={(_, d) => alterar((m) => { m.empresa = d.value; })} />
      <Input value={medicao.tecnica ?? ''} disabled={!editavel} placeholder="Técnica" style={{ width: 130 }}
        onChange={(_, d) => alterar((m) => { m.tecnica = d.value; })} />
      <Input value={medicao.equipamento ?? ''} disabled={!editavel} placeholder="Equipamento" style={{ width: 140 }}
        onChange={(_, d) => alterar((m) => { m.equipamento = d.value; })} />
      {editavel && <Button size="small" onClick={remover}>Remover</Button>}
    </div>
  );
}

// Escolha do EPI do catálogo para uma medida da categoria EPI: o PDF mostra nome, CA e validade (NR-06).
export function SeletorEpi({ medida, epis, editavel, alterar }: {
  medida: MedidaGerada;
  epis: CatalogoEpi[];
  editavel: boolean;
  alterar: (f: (m: MedidaGerada) => void) => void;
}) {
  const escolhido = epis.find((e) => e.id === medida.catalogoEpiId);
  const vencido = escolhido?.certificadoAprovacaoValidade ? new Date(escolhido.certificadoAprovacaoValidade) < new Date() : false;
  return (
    <>
      <Select value={medida.catalogoEpiId ?? ''} disabled={!editavel} aria-label="EPI do catálogo" style={{ maxWidth: 260 }}
        onChange={(_, d) => alterar((m) => { m.catalogoEpiId = d.value || null; })}>
        <option value="">EPI do catálogo…</option>
        {epis.map((e) => <option key={e.id} value={e.id}>{e.nome}{e.certificadoAprovacaoNumero ? ` — CA ${e.certificadoAprovacaoNumero}` : ' — sem CA'}</option>)}
      </Select>
      {escolhido && !escolhido.certificadoAprovacaoNumero && <StatusChip tom="alerta">Sem CA</StatusChip>}
      {escolhido && vencido && <StatusChip tom="alerta">CA vencido</StatusChip>}
    </>
  );
}
