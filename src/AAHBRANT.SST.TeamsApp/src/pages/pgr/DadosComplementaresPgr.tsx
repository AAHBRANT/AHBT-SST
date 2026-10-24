import { useState } from 'react';
import { Button, Card, Checkbox, FeedbackInline, Field, Input, Select, StatusChip, Text } from '@ui';
import {
  api, type ContatoEmergencia, type EstruturaGeracaoPgr, type OpcaoEmergencia, type SugestaoEmergencia,
} from '../../lib/api';

// Cenários que o técnico pode marcar; os cinco primeiros são o mínimo do modelo de PGR da obra.
const CENARIOS = [
  'Acidente pessoal ou mal súbito',
  'Incêndio e explosão',
  'Vazamento ou derrame de óleos, graxas e líquidos combustíveis ou inflamáveis',
  'Acidente com eletricidade',
  'Acidente de trajeto',
  'Resgate em trabalho em altura',
  'Resgate em espaço confinado',
  'Soterramento ou deslizamento de talude',
  'Atropelamento ou acidente com máquinas',
];

const ROTULO_TIPO: Record<OpcaoEmergencia['chave'], string> = {
  hospital: 'Hospitais',
  bombeiros: 'Corpo de Bombeiros',
  pm: 'Polícia',
};

const km = (v?: number | null) => (v == null ? '' : v.toFixed(1).replace('.', ','));

// Dados da obra que o PGR exige (24/10/2026) e o plano de emergência. O técnico preenche uma vez; as próximas
// gerações da mesma obra abrem já preenchidas. "Sugerir pelo local da obra" consulta o mapa (OpenStreetMap) e a IA
// ranqueia os locais reais; nada vai para a ficha sem o técnico clicar em Usar.
export function DadosComplementaresPgr({ geracaoId, e, editavel, alterar }: {
  geracaoId: string;
  e: EstruturaGeracaoPgr;
  editavel: boolean;
  alterar: (f: (x: EstruturaGeracaoPgr) => void) => void;
}) {
  const comp = e.complementares;
  const [sugestao, setSugestao] = useState<SugestaoEmergencia | null>(null);
  const [buscando, setBuscando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function sugerir() {
    setBuscando(true);
    setErro(null);
    try {
      setSugestao(await api.geracoesPgrEap.sugerirEmergencia(geracaoId));
    } catch (x) {
      setErro(x instanceof Error ? x.message : 'Não foi possível consultar o mapa agora.');
    } finally {
      setBuscando(false);
    }
  }

  // Aplica uma opção do mapa na linha da ficha do mesmo tipo (ou cria a linha). Bombeiros e polícia mantêm o
  // número nacional (193 e 190); o telefone do posto, se o mapa tiver, vai junto do endereço.
  function aplicar(x: EstruturaGeracaoPgr, op: OpcaoEmergencia, s: SugestaoEmergencia) {
    const lista = x.complementares.contatosEmergencia;
    let linha = lista.find((c) => c.chave === op.chave);
    if (!linha) {
      linha = { chave: op.chave, local: '', recomendado: false };
      lista.push(linha);
    }
    const nacional = op.chave === 'bombeiros' || op.chave === 'pm';
    // "Hospital — Biocor", mas sem repetir quando o nome do mapa já diz o tipo (ex.: "Corpo de Bombeiros Militar de MG").
    const rotulo = op.chave === 'hospital' ? 'Hospital' : op.chave === 'bombeiros' ? 'Bombeiros' : 'Polícia';
    linha.local = op.nome.toLowerCase().includes(rotulo.toLowerCase()) ? op.nome : `${rotulo} — ${op.nome}`;
    linha.endereco = [op.endereco, nacional && op.telefone ? `Tel. do local: ${op.telefone}` : null].filter(Boolean).join(' · ') || null;
    if (!nacional) linha.telefone = op.telefone ?? linha.telefone ?? null;
    linha.distanciaKm = op.distanciaKm;
    linha.tempoMinutos = op.tempoMinutos;
    linha.rota = op.rota;
    linha.recomendado = op.recomendado;
    x.complementares.rotasCalculadasEmUtc = s.calculadoEmUtc;
    x.complementares.rotasFonte = s.fonte;
  }

  const usar = (op: OpcaoEmergencia, s: SugestaoEmergencia) => alterar((x) => aplicar(x, op, s));

  // Tudo numa única alteração: várias chamadas seguidas de alterar() partiriam do mesmo estado e a última sobrescreveria as outras.
  function usarRecomendados(s: SugestaoEmergencia) {
    alterar((x) => {
      s.opcoes.filter((o) => o.recomendado).forEach((o) => aplicar(x, o, s));
      if (s.cenariosSugeridos.length > 0)
        x.complementares.cenariosEmergencia = Array.from(new Set([...x.complementares.cenariosEmergencia, ...s.cenariosSugeridos]));
    });
  }

  const opcoesPorTipo = (chave: OpcaoEmergencia['chave']) => (sugestao?.opcoes ?? []).filter((o) => o.chave === chave);

  return (
    <>
      <Card titulo="Dados da obra para o PGR">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 8 }}>
          <Field label="CNAE (número e descrição)">
            <Input value={comp.cnae ?? ''} disabled={!editavel} placeholder="Ex.: 4211-1/01 — Construção de rodovias"
              onChange={(_, d) => alterar((x) => { x.complementares.cnae = d.value; })} />
          </Field>
          <Field label="Grau de risco (NR-04)">
            <Select value={comp.grauRisco != null ? String(comp.grauRisco) : ''} disabled={!editavel}
              onChange={(_, d) => alterar((x) => { x.complementares.grauRisco = d.value ? Number(d.value) : null; })}>
              <option value="">Selecione…</option>
              {[1, 2, 3, 4].map((n) => <option key={n} value={n}>{n}</option>)}
            </Select>
          </Field>
          <Field label="CEP">
            <Input value={comp.cep ?? ''} disabled={!editavel} maxLength={9}
              onChange={(_, d) => alterar((x) => { x.complementares.cep = d.value; })} />
          </Field>
          <Field label="Registro profissional (CREA) do responsável técnico">
            <Input value={comp.responsavelTecnicoRegistro ?? ''} disabled={!editavel}
              onChange={(_, d) => alterar((x) => { x.complementares.responsavelTecnicoRegistro = d.value; })} />
          </Field>
          <Field label="Responsável pela implantação do PGR">
            <Input value={comp.responsavelImplantacaoNome ?? ''} disabled={!editavel}
              onChange={(_, d) => alterar((x) => { x.complementares.responsavelImplantacaoNome = d.value; })} />
          </Field>
          <Field label="Cargo do responsável pela implantação">
            <Input value={comp.responsavelImplantacaoCargo ?? ''} disabled={!editavel}
              onChange={(_, d) => alterar((x) => { x.complementares.responsavelImplantacaoCargo = d.value; })} />
          </Field>
        </div>
      </Card>

      <Card
        titulo="Plano de emergência"
        acoes={editavel ? <Button appearance="primary" disabled={buscando} onClick={sugerir}>{buscando ? 'Consultando o mapa…' : 'Sugerir pelo local da obra'}</Button> : undefined}
      >
        <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
          A IA usa o endereço da obra para achar hospitais, bombeiros e polícia no mapa e indicar o melhor trajeto. Confira os
          locais e os telefones antes de aprovar.
        </Text>
        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

        {sugestao && (
          <div style={{ border: '1px solid var(--colorNeutralStroke2)', borderRadius: 8, padding: 10, marginBottom: 12 }}>
            <Text size={200} style={{ display: 'block' }}>
              Local da obra no mapa: <strong>{sugestao.enderecoEncontrado ?? '—'}</strong>
            </Text>
            {sugestao.avisos.map((a) => <FeedbackInline key={a} tom="aviso">{a}</FeedbackInline>)}
            {(['hospital', 'bombeiros', 'pm'] as const).map((chave) => {
              const ops = opcoesPorTipo(chave);
              if (ops.length === 0) return null;
              return (
                <div key={chave} style={{ marginTop: 10 }}>
                  <Text weight="semibold" style={{ display: 'block' }}>{ROTULO_TIPO[chave]}</Text>
                  {ops.map((o) => (
                    <div key={o.id} style={{ padding: '6px 0', borderBottom: '1px solid var(--colorNeutralStroke3)' }}>
                      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                        <Text weight="semibold">{o.nome}</Text>
                        {o.recomendado && <StatusChip tom="ok">Recomendado</StatusChip>}
                        {o.atendeUrgencia && <StatusChip tom="info">Atende urgência</StatusChip>}
                        <Text size={200}>{km(o.distanciaKm)} km · {o.tempoMinutos} min de carro</Text>
                        <span style={{ flex: 1 }} />
                        <a href={o.linkMapa} target="_blank" rel="noreferrer">Ver rota no mapa</a>
                        {editavel && <Button size="small" appearance={o.recomendado ? 'primary' : 'secondary'} onClick={() => usar(o, sugestao)}>Usar na ficha</Button>}
                      </div>
                      <Text size={200} style={{ display: 'block' }}>
                        {o.endereco ?? 'Endereço não informado no mapa'} · {o.telefone ? `Tel. ${o.telefone}` : 'sem telefone no mapa'}
                      </Text>
                      {o.justificativa && <Text size={200} style={{ display: 'block', fontStyle: 'italic' }}>{o.justificativa}</Text>}
                      <details>
                        <summary style={{ cursor: 'pointer', fontSize: 12 }}>Como chegar</summary>
                        <ol style={{ margin: '4px 0 0', paddingLeft: 18 }}>
                          {o.rota.split('\n').map((p, i) => <li key={i}><Text size={200}>{p}</Text></li>)}
                        </ol>
                      </details>
                    </div>
                  ))}
                </div>
              );
            })}
            {editavel && sugestao.opcoes.some((o) => o.recomendado) && (
              <div style={{ marginTop: 10 }}>
                <Button appearance="primary" onClick={() => usarRecomendados(sugestao)}>Usar todos os recomendados e cenários sugeridos</Button>
              </div>
            )}
          </div>
        )}

        <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>Contatos de emergência da ficha</Text>
        {comp.contatosEmergencia.map((c, i) => (
          <ContatoLinha key={i} contato={c} editavel={editavel}
            alterar={(f) => alterar((x) => f(x.complementares.contatosEmergencia[i]))}
            remover={() => alterar((x) => { x.complementares.contatosEmergencia.splice(i, 1); })} />
        ))}
        {editavel && (
          <Button size="small" onClick={() => alterar((x) => { x.complementares.contatosEmergencia.push({ local: '', recomendado: false }); })}>
            Adicionar contato
          </Button>
        )}
        {comp.rotasCalculadasEmUtc && (
          <Text size={200} style={{ display: 'block', marginTop: 6 }}>
            Rotas calculadas com {comp.rotasFonte ?? 'mapa'} em {new Date(comp.rotasCalculadasEmUtc).toLocaleDateString('pt-BR')}.
          </Text>
        )}

        <Text weight="semibold" style={{ display: 'block', margin: '12px 0 4px' }}>Cenários de emergência que se aplicam à obra</Text>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 2 }}>
          {CENARIOS.map((n) => (
            <Checkbox key={n} label={n} disabled={!editavel} checked={comp.cenariosEmergencia.includes(n)}
              onChange={(_, d) => alterar((x) => {
                const atual = x.complementares.cenariosEmergencia.filter((c) => c !== n);
                x.complementares.cenariosEmergencia = d.checked ? [...atual, n] : atual;
              })} />
          ))}
        </div>
      </Card>
    </>
  );
}

function ContatoLinha({ contato, editavel, alterar, remover }: {
  contato: ContatoEmergencia;
  editavel: boolean;
  alterar: (f: (x: ContatoEmergencia) => void) => void;
  remover: () => void;
}) {
  return (
    <div style={{ display: 'flex', gap: 6, marginBottom: 6, flexWrap: 'wrap', alignItems: 'center' }}>
      <Input value={contato.local} disabled={!editavel} placeholder="Local ou setor" style={{ flex: 1, minWidth: 200 }}
        onChange={(_, d) => alterar((x) => { x.local = d.value; })} />
      <Input value={contato.endereco ?? ''} disabled={!editavel} placeholder="Endereço" style={{ flex: 2, minWidth: 220 }}
        onChange={(_, d) => alterar((x) => { x.endereco = d.value; })} />
      <Input value={contato.telefone ?? ''} disabled={!editavel} placeholder="Telefone" style={{ width: 150 }}
        onChange={(_, d) => alterar((x) => { x.telefone = d.value; })} />
      {contato.tempoMinutos != null && <Text size={200}>{km(contato.distanciaKm)} km · {contato.tempoMinutos} min</Text>}
      {contato.recomendado && <StatusChip tom="ok">Recomendado</StatusChip>}
      {editavel && <Button size="small" onClick={remover}>Remover</Button>}
    </div>
  );
}
