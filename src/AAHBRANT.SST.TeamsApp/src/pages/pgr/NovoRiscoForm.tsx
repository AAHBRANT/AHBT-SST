import { useId, useState } from 'react';
import { Button, Checkbox, Field, Input, Select, StatusChip, Text } from '@ui';
import type { AtividadeGerada } from '../../lib/api';
import { nivelMatriz, type CelulaMatriz } from './agruparPorPerigo';
import { DANOS_QUEDA_DE_ALTURA, PERIGOS_PADRAO, QUEDA_DE_ALTURA, normalizar, type DadosNovoRisco } from './riscoManual';

export interface InicialNovoRisco { perigo: string; tipo: string; danos: string }

// Formulário inline "Adicionar risco" do passo 4 (mockup pgr-eap-adicionar-risco-altura_2026-11-17_v1). O técnico escolhe um perigo
// padrão (o tipo vem junto) ou digita o seu, avalia P × S pela matriz da obra e marca as atividades onde o risco se aplica.
export function NovoRiscoForm({ titulo, atividades, marcadas, inicial, tipos, grupoDoTipo, maxP, maxS, matriz, aoCancelar, aoAdicionar }: {
  titulo: string;
  atividades: AtividadeGerada[];
  // Códigos EAP já marcados ao abrir (a atividade atual, ou todas quando vem do aviso de altura).
  marcadas: string[];
  inicial?: InicialNovoRisco;
  tipos: string[];
  grupoDoTipo: (tipo: string) => { numero: number; nome: string } | undefined;
  maxP: number;
  maxS: number;
  matriz?: CelulaMatriz[];
  aoCancelar: () => void;
  // Devolve uma mensagem quando não pode incluir (ex.: perigo já existe na atividade); nulo = incluído.
  aoAdicionar: (dados: DadosNovoRisco, codigos: string[]) => string | null;
}) {
  const idForm = useId();
  const ehQueda = inicial?.perigo === QUEDA_DE_ALTURA;
  const [perigo, setPerigo] = useState(inicial?.perigo ?? '');
  const [tipo, setTipo] = useState(inicial?.tipo ?? 'Acidente');
  const [danos, setDanos] = useState(inicial?.danos ?? '');
  const [prob, setProb] = useState(Math.ceil(maxP / 2));
  // Queda de altura vem mais grave por padrão (o mockup parte de 3 × 4); o técnico ajusta.
  const [sev, setSev] = useState(ehQueda ? Math.max(1, maxS - 1) : Math.ceil(maxS / 2));
  const [codigos, setCodigos] = useState<string[]>(marcadas);
  const [sugerindo, setSugerindo] = useState(false);
  // Opção destacada pelo teclado (setas); -1 = nenhuma.
  const [ativa, setAtiva] = useState(-1);
  const [erro, setErro] = useState<string | null>(null);

  const termo = normalizar(perigo);
  const sugestoes = PERIGOS_PADRAO.filter((p) => !termo || normalizar(p.nome).includes(termo));
  const exato = PERIGOS_PADRAO.some((p) => normalizar(p.nome) === termo);
  const comTextoLivre = !!termo && !exato;
  const totalOpcoes = sugestoes.length + (comTextoLivre ? 1 : 0);
  const aberta = sugerindo && totalOpcoes > 0;
  const idLista = `${idForm}-perigos`;
  const idOpcao = (i: number) => `${idForm}-perigo-${i}`;
  const grupo = grupoDoTipo(tipo);
  const { rotulo, tom } = nivelMatriz(matriz, { probabilidade: prob, severidade: sev });
  const podeAdicionar = perigo.trim().length > 0 && codigos.length > 0;

  const escolher = (p: { tipo: string; nome: string }) => {
    setPerigo(p.nome);
    setTipo(p.tipo);
    if (p.nome === QUEDA_DE_ALTURA && !danos.trim()) setDanos(DANOS_QUEDA_DE_ALTURA);
    setSugerindo(false);
    setAtiva(-1);
  };

  // Digitar exatamente um perigo padrão (sem clicar na sugestão) também traz o tipo dele: "Ruído" não pode ficar como Acidente.
  const digitar = (valor: string) => {
    setPerigo(valor);
    setSugerindo(true);
    setAtiva(-1);
    const padrao = PERIGOS_PADRAO.find((p) => normalizar(p.nome) === normalizar(valor));
    if (padrao) setTipo(padrao.tipo);
  };

  const teclado = (ev: React.KeyboardEvent) => {
    if (ev.key === 'ArrowDown' || ev.key === 'ArrowUp') {
      ev.preventDefault();
      if (!sugerindo) { setSugerindo(true); return; }
      if (totalOpcoes === 0) return;
      const passo = ev.key === 'ArrowDown' ? 1 : -1;
      setAtiva((a) => (a + passo + totalOpcoes) % totalOpcoes);
    } else if (ev.key === 'Enter' && aberta && ativa >= 0) {
      ev.preventDefault();
      if (ativa < sugestoes.length) escolher(sugestoes[ativa]);
      else { setSugerindo(false); setAtiva(-1); }
    } else if (ev.key === 'Escape' && aberta) {
      ev.preventDefault();
      setSugerindo(false);
      setAtiva(-1);
    }
  };

  return (
    <div style={{ border: '1px dashed var(--colorBrandStroke1)', borderRadius: 8, padding: 12, marginTop: 10, background: 'var(--colorNeutralBackground1)' }}>
      <Text weight="semibold" style={{ display: 'block', marginBottom: 8, color: 'var(--colorBrandForeground1)' }}>{titulo}</Text>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 8, alignItems: 'start' }}>
        <div style={{ gridColumn: 'span 2', minWidth: 0 }}>
          <Field label="Perigo" hint="Escolha um perigo padrão ou digite o seu.">
            <Input value={perigo} maxLength={200} autoComplete="off"
              role="combobox" aria-autocomplete="list" aria-expanded={aberta} aria-controls={idLista}
              aria-activedescendant={aberta && ativa >= 0 ? idOpcao(ativa) : undefined}
              onChange={(_, d) => digitar(d.value)}
              onKeyDown={teclado}
              onFocus={() => setSugerindo(true)}
              onBlur={() => { setSugerindo(false); setAtiva(-1); }} />
          </Field>
          {aberta && (
            <div id={idLista} role="listbox" aria-label="Perigos padrão" style={{
              border: '1px solid var(--colorNeutralStroke1)', borderRadius: 4, background: 'var(--colorNeutralBackground1)',
              boxShadow: 'var(--shadow8)', marginTop: 2, maxHeight: 180, overflowY: 'auto',
            }}>
              {sugestoes.map((p, i) => (
                <div key={p.nome} id={idOpcao(i)} role="option" aria-selected={i === ativa || normalizar(p.nome) === termo}
                  onMouseDown={(ev) => { ev.preventDefault(); escolher(p); }}
                  ref={(el) => { if (el && i === ativa) el.scrollIntoView({ block: 'nearest' }); }}
                  style={{
                    padding: '6px 10px', minHeight: 32, display: 'flex', alignItems: 'center', cursor: 'pointer',
                    background: i === ativa || normalizar(p.nome) === termo ? 'var(--colorNeutralBackground1Selected)' : undefined,
                  }}>
                  <Text size={300}>{p.nome}</Text>
                </div>
              ))}
              {comTextoLivre && (
                <div id={idOpcao(sugestoes.length)} role="option" aria-selected={ativa === sugestoes.length}
                  onMouseDown={(ev) => { ev.preventDefault(); setSugerindo(false); setAtiva(-1); }}
                  style={{
                    padding: '6px 10px', minHeight: 32, display: 'flex', alignItems: 'center', cursor: 'pointer', borderTop: '1px solid var(--colorNeutralStroke2)',
                    background: ativa === sugestoes.length ? 'var(--colorNeutralBackground1Selected)' : undefined,
                  }}>
                  <Text size={300} style={{ color: 'var(--colorNeutralForeground3)' }}>Usar texto digitado: "{perigo.trim()}"</Text>
                </div>
              )}
            </div>
          )}
        </div>
        <Field label="Tipo" hint={grupo ? `Grupo ${grupo.numero} · ${grupo.nome}` : undefined}>
          <Select value={tipo} onChange={(_, d) => setTipo(d.value)}>
            {tipos.map((t) => <option key={t}>{t}</option>)}
          </Select>
        </Field>
        <Field label="Danos" style={{ gridColumn: 'span 2' }}>
          <Input value={danos} maxLength={500} onChange={(_, d) => setDanos(d.value)} />
        </Field>
        <Field label="Probabilidade">
          <Select value={String(prob)} onChange={(_, d) => setProb(Number(d.value))}>
            {Array.from({ length: maxP }, (_, i) => <option key={i + 1}>{i + 1}</option>)}
          </Select>
        </Field>
        <Field label="Severidade">
          <Select value={String(sev)} onChange={(_, d) => setSev(Number(d.value))}>
            {Array.from({ length: maxS }, (_, i) => <option key={i + 1}>{i + 1}</option>)}
          </Select>
        </Field>
      </div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginTop: 8 }}>
        <StatusChip tom={tom}>P × S = {prob * sev}{rotulo ? ` · ${rotulo}` : ''}</StatusChip>
        <Text size={200}>calculado pela matriz da obra</Text>
      </div>
      {/* Sem Field aqui: ele daria o mesmo id a todos os checkboxes. */}
      <div role="group" aria-labelledby={`${idForm}-atividades`} style={{ marginTop: 10 }}>
        <Text id={`${idForm}-atividades`} weight="semibold" size={200} style={{ display: 'block' }}>Atividades onde se aplica</Text>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
          {atividades.map((a) => (
            <Checkbox key={a.codigoEap} checked={codigos.includes(a.codigoEap)}
              label={`${a.codigoEap} ${a.nome}${marcadas.length === 1 && marcadas[0] === a.codigoEap ? ' (atual)' : ''}`}
              onChange={(_, d) => setCodigos((c) => (d.checked ? [...c, a.codigoEap] : c.filter((x) => x !== a.codigoEap)))} />
          ))}
        </div>
      </div>
      {erro && <Text size={200} role="alert" style={{ display: 'block', marginTop: 8, color: 'var(--colorPaletteRedForeground1)' }}>{erro}</Text>}
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12, flexWrap: 'wrap' }}>
        <Button onClick={aoCancelar}>Cancelar</Button>
        <Button appearance="primary" disabled={!podeAdicionar}
          onClick={() => setErro(aoAdicionar({ perigo, tipo, danos, probabilidade: prob, severidade: sev }, codigos))}>Adicionar</Button>
      </div>
    </div>
  );
}
