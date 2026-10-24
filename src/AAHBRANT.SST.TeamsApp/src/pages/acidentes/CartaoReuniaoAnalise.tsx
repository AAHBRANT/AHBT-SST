import { useMemo, useState } from 'react';
import {
  Button,
  Campo,
  CampoData,
  Card,
  ChipCheckboxGroup,
  FeedbackInline,
  Field,
  FormGrid,
  Input,
  Legenda,
  SeletorPesquisavel,
  Select,
  StatusChip,
  Text,
  Textarea,
  type Tom,
} from '@ui';
import { Dismiss16Regular, Video24Regular } from '@fluentui/react-icons';
import { api, SituacaoReuniaoTeams, type ReuniaoAnalise, type Usuario } from '../../lib/api';

// Reunião de análise na tela da ocorrência (23/10/2026, desenho aprovado em
// docs/mockups/reuniao-analise-ocorrencia-v1.html). Obrigatória em todo tipo de ocorrência:
// pendente → agendada (Teams ou fora dele) → realizada com presentes e ata. A finalização da
// investigação só libera com a reunião realizada.
type Modo = 'ver' | 'agendar' | 'remarcar' | 'realizada';

const DURACOES = [30, 60, 90, 120];

function rotuloDuracao(m: number) {
  return m < 60 ? `${m} minutos` : m === 60 ? '1 hora' : `${m / 60} horas`.replace('.5', ',5');
}

// Próximo dia útil (seg–sex), no formato do CampoData.
function proximoDiaUtil() {
  const d = new Date();
  do d.setDate(d.getDate() + 1);
  while (d.getDay() === 0 || d.getDay() === 6);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function minutosEntre(inicio: string, fim: string) {
  return Math.round((new Date(fim).getTime() - new Date(inicio).getTime()) / 60000);
}

function diaPorExtenso(dataHora: string) {
  const texto = new Date(dataHora).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: '2-digit' });
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

export interface CartaoReuniaoAnaliseProps {
  acidenteId: string;
  reuniao: ReuniaoAnalise | null;
  usuarios: Usuario[];
  somenteLeitura: boolean;
  aoAtualizar: () => Promise<void>;
}

export function CartaoReuniaoAnalise({ acidenteId, reuniao, usuarios, somenteLeitura, aoAtualizar }: CartaoReuniaoAnaliseProps) {
  const [modo, setModo] = useState<Modo>('ver');
  const [data, setData] = useState('');
  const [hora, setHora] = useState('');
  const [duracao, setDuracao] = useState(60);
  const [participantes, setParticipantes] = useState<string[]>([]);
  const [presentes, setPresentes] = useState<string[]>([]);
  const [ata, setAta] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const opcoesUsuarios = useMemo(
    () => usuarios.filter((u) => !participantes.includes(u.id)).map((u) => ({ id: u.id, rotulo: u.nome, descricao: u.email })),
    [usuarios, participantes],
  );

  const realizada = !!reuniao?.realizadaEm;
  const [tom, rotulo]: [Tom, string] = !reuniao
    ? ['atencao', 'Pendente']
    : realizada
      ? ['ok', 'Realizada']
      : reuniao.situacao === SituacaoReuniaoTeams.Criada
        ? ['atencao', 'Agendada']
        : ['alerta', 'Fora do Teams'];

  function abrirAgendar() {
    setData(proximoDiaUtil());
    setHora('14:00');
    setDuracao(60);
    setParticipantes([]);
    setErro(null);
    setModo('agendar');
  }

  function abrirRemarcar() {
    if (!reuniao) return;
    setData(reuniao.inicio.slice(0, 10));
    setHora(reuniao.inicio.slice(11, 16));
    const minutos = minutosEntre(reuniao.inicio, reuniao.fim);
    setDuracao(DURACOES.includes(minutos) ? minutos : 60);
    setErro(null);
    setModo('remarcar');
  }

  function abrirRealizada() {
    if (!reuniao) return;
    const base = reuniao.realizadaEm ?? reuniao.inicio;
    setData(base.slice(0, 10));
    setHora(base.slice(11, 16));
    setPresentes(reuniao.presentes?.length ? reuniao.presentes : reuniao.participantes);
    setAta(reuniao.ata ?? '');
    setErro(null);
    setModo('realizada');
  }

  async function executar(acao: () => Promise<void>, mensagemErro: string) {
    try {
      setSalvando(true);
      setErro(null);
      await acao();
      await aoAtualizar();
      setModo('ver');
    } catch (e) {
      setErro(e instanceof Error ? e.message : mensagemErro);
    } finally {
      setSalvando(false);
    }
  }

  function exigirDataHora() {
    if (!data || !hora) {
      setErro('Informe a data e o horário.');
      return null;
    }
    return `${data}T${hora}:00`;
  }

  function agendar() {
    const inicio = exigirDataHora();
    if (!inicio) return;
    void executar(
      () => api.acidentes.agendarReuniao(acidenteId, { inicio, duracaoMinutos: duracao, participantesUsuarioIds: participantes }),
      'Falha ao agendar a reunião.',
    );
  }

  function remarcar() {
    const inicio = exigirDataHora();
    if (!inicio) return;
    void executar(() => api.acidentes.remarcarReuniao(acidenteId, { inicio, duracaoMinutos: duracao }), 'Falha ao remarcar a reunião.');
  }

  function salvarRealizada() {
    const realizadaEm = exigirDataHora();
    if (!realizadaEm) return;
    if (presentes.length === 0) {
      setErro('Marque quem participou da reunião.');
      return;
    }
    if (!ata.trim()) {
      setErro('Escreva a ata: o que foi discutido e decidido.');
      return;
    }
    void executar(
      () => api.acidentes.registrarReuniaoRealizada(acidenteId, { realizadaEm, presentes, ata }),
      'Falha ao registrar a reunião.',
    );
  }

  const camposDataHora = (rotuloData: string) => (
    <>
      <Campo span={4}>
        <Field label={rotuloData} required>
          <CampoData value={data} onChange={(_, d) => setData(d.value)} />
        </Field>
      </Campo>
      <Campo span={4}>
        <Field label="Hora" required>
          <Input type="time" value={hora} onChange={(_, d) => setHora(d.value)} />
        </Field>
      </Campo>
    </>
  );

  const campoDuracao = (
    <Campo span={4}>
      <Field label="Duração">
        <Select value={String(duracao)} onChange={(_, d) => setDuracao(Number(d.value))}>
          {DURACOES.map((m) => (
            <option key={m} value={m}>
              {rotuloDuracao(m)}
            </option>
          ))}
        </Select>
      </Field>
    </Campo>
  );

  const botoesFormulario = (rotuloSalvar: string, aoSalvar: () => void) => (
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      <Button appearance="primary" onClick={aoSalvar} disabled={salvando}>
        {salvando ? 'Salvando...' : rotuloSalvar}
      </Button>
      <Button onClick={() => setModo('ver')} disabled={salvando}>
        Cancelar
      </Button>
    </div>
  );

  let conteudo: React.ReactNode;
  if (modo === 'agendar') {
    conteudo = (
      <>
        <FormGrid>
          {camposDataHora('Data')}
          {campoDuracao}
          <Campo span={12}>
            <Field label="Participantes" dica="Você entra como organizador. O convite chega pelo Teams/Outlook de cada um.">
              <div style={{ display: 'grid', gap: 6 }}>
                <SeletorPesquisavel
                  opcoes={opcoesUsuarios}
                  valor=""
                  aoMudar={(id) => id && setParticipantes((atuais) => [...atuais, id])}
                  placeholder="Buscar pessoa para convidar"
                  vazio="Nenhum usuário encontrado."
                />
                {participantes.length > 0 && (
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    {participantes.map((id) => {
                      const nome = usuarios.find((u) => u.id === id)?.nome ?? id;
                      return (
                        <Button
                          key={id}
                          size="small"
                          appearance="outline"
                          icon={<Dismiss16Regular />}
                          iconPosition="after"
                          aria-label={`Remover ${nome}`}
                          onClick={() => setParticipantes((atuais) => atuais.filter((x) => x !== id))}
                        >
                          {nome}
                        </Button>
                      );
                    })}
                  </div>
                )}
              </div>
            </Field>
          </Campo>
        </FormGrid>
        {botoesFormulario('Agendar no Teams', agendar)}
      </>
    );
  } else if (modo === 'remarcar') {
    conteudo = (
      <>
        <FormGrid>
          {camposDataHora('Nova data')}
          {campoDuracao}
        </FormGrid>
        <Legenda>Remarcar muda a data e o horário e atualiza o convite no Teams de todos os participantes.</Legenda>
        {botoesFormulario('Remarcar', remarcar)}
      </>
    );
  } else if (modo === 'realizada' && reuniao) {
    const nomes = Array.from(new Set([...reuniao.participantes, ...(reuniao.presentes ?? [])]));
    conteudo = (
      <>
        <FormGrid>
          {camposDataHora('Data em que aconteceu')}
          <Campo span={12}>
            <Field label="Quem participou" required dica="Toque no nome para marcar presença ou ausência.">
              <ChipCheckboxGroup
                aria-label="Quem participou da reunião"
                opcoes={nomes.map((n) => ({ id: n, rotulo: n }))}
                selecionados={presentes}
                aoMudar={setPresentes}
              />
            </Field>
          </Campo>
          <Campo span={12}>
            <Field label="Ata · o que foi discutido e decidido" required>
              <Textarea value={ata} onChange={(_, d) => setAta(d.value)} rows={5} resize="vertical" maxLength={4000} />
            </Field>
          </Campo>
        </FormGrid>
        {botoesFormulario('Salvar reunião realizada', salvarRealizada)}
      </>
    );
  } else if (!reuniao) {
    conteudo = (
      <>
        <FeedbackInline tom="aviso">
          Obrigatória em toda ocorrência. A investigação só pode ser finalizada depois que a reunião for registrada como realizada.
        </FeedbackInline>
        {!somenteLeitura && (
          <div>
            <Button appearance="primary" onClick={abrirAgendar}>
              Agendar reunião de análise
            </Button>
          </div>
        )}
      </>
    );
  } else if (realizada) {
    conteudo = (
      <>
        <Text>
          <strong>{reuniao.realizadaEm!.slice(8, 10)}/{reuniao.realizadaEm!.slice(5, 7)}/{reuniao.realizadaEm!.slice(0, 4)}</strong>
          {' · '}
          {reuniao.realizadaEm!.slice(11, 16)}
        </Text>
        <Legenda>Presentes: {reuniao.presentes?.join(', ') || '—'}</Legenda>
        <Text style={{ display: 'block', whiteSpace: 'pre-wrap', textAlign: 'justify' }}>{reuniao.ata}</Text>
        {!somenteLeitura && (
          <div>
            <Button onClick={abrirRealizada}>Editar ata</Button>
          </div>
        )}
      </>
    );
  } else {
    const noTeams = reuniao.situacao === SituacaoReuniaoTeams.Criada;
    conteudo = (
      <>
        <Text>
          <strong>{diaPorExtenso(reuniao.inicio)}</strong>
          {' · '}
          {reuniao.inicio.slice(11, 16)} às {reuniao.fim.slice(11, 16)}
        </Text>
        <Legenda>Participantes: {reuniao.participantes.join(', ') || '—'}</Legenda>
        {noTeams && reuniao.linkTeams && (
          <div>
            <Button
              appearance="secondary"
              size="small"
              icon={<Video24Regular />}
              as="a"
              href={reuniao.linkTeams}
              target="_blank"
              rel="noopener noreferrer"
            >
              Entrar na reunião do Teams
            </Button>
          </div>
        )}
        {reuniao.motivoFalha && <FeedbackInline tom={noTeams ? 'aviso' : 'erro'}>{reuniao.motivoFalha}</FeedbackInline>}
        {!somenteLeitura && (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {!noTeams && (
              <Button
                appearance="primary"
                disabled={salvando}
                onClick={() => void executar(() => api.acidentes.tentarReuniaoNoTeams(acidenteId), 'Falha ao marcar no Teams.')}
              >
                Tentar de novo no Teams
              </Button>
            )}
            <Button appearance={noTeams ? 'primary' : 'secondary'} onClick={abrirRealizada} disabled={salvando}>
              Registrar que aconteceu
            </Button>
            <Button onClick={abrirRemarcar} disabled={salvando}>
              Remarcar
            </Button>
          </div>
        )}
      </>
    );
  }

  return (
    <Card titulo="Reunião de análise" acoes={<StatusChip tom={tom}>{rotulo}</StatusChip>}>
      <div style={{ display: 'grid', gap: 10 }}>
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}
        {conteudo}
      </div>
    </Card>
  );
}
