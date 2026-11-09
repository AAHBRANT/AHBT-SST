import { useEffect, useMemo, useState } from 'react';
import {
  Button,
  CampoData,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Field,
  Input,
  SeletorPesquisavel,
  Select,
  Text,
  Textarea,
  designTokens,
} from '@ui';
import { Dismiss16Regular, Mail16Regular, Video24Regular } from '@fluentui/react-icons';
import { api, type ConvidavelReuniao, type Obra, type ReuniaoCalendarioCriada } from '../../lib/api';

// "Nova reunião" no Calendário (pedido do usuário em 09/11/2026; desenho aprovado em
// docs/mockups/2026-11-09-calendario-nova-reuniao.html). Cria uma reunião do Teams na agenda de quem
// está logado; os convidados recebem o convite por e-mail pelo próprio Outlook.
const TIPOS: { rotulo: string; titulo: string; pauta: string }[] = [
  { rotulo: 'DDS', titulo: 'DDS – Diálogo Diário de Segurança', pauta: '1. Tema do dia\n2. Riscos da atividade\n3. Dúvidas e registro de presença' },
  { rotulo: 'CIPA', titulo: 'Reunião ordinária da CIPA', pauta: '1. Leitura da ata anterior\n2. Análise de ocorrências do mês\n3. Plano de ação e prazos' },
  { rotulo: 'Reunião de obra', titulo: 'Reunião de obra – Segurança', pauta: '1. Indicadores de SST da obra\n2. Pendências e não conformidades\n3. Próximas atividades críticas' },
  { rotulo: 'Alinhamento', titulo: 'Alinhamento SST', pauta: '' },
  { rotulo: 'Outra', titulo: '', pauta: '' },
];

const REPETICOES: { valor: '' | '1' | '2' | '3'; rotulo: (dia: Date) => string }[] = [
  { valor: '', rotulo: () => 'Não se repete' },
  { valor: '1', rotulo: (d) => `Toda semana, ${d.toLocaleDateString('pt-BR', { weekday: 'long' })}` },
  { valor: '2', rotulo: (d) => `A cada 2 semanas, ${d.toLocaleDateString('pt-BR', { weekday: 'long' })}` },
  { valor: '3', rotulo: (d) => `Todo mês, no dia ${d.getDate()}` },
];

// Padrão de 3 meses para o fim da repetição.
function somarMeses(data: string, meses: number) {
  const d = new Date(`${data}T12:00:00`);
  d.setMonth(d.getMonth() + meses);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

const EMAIL_VALIDO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function somarUmaHora(hora: string) {
  const [h, m] = hora.split(':').map(Number);
  return `${String(Math.min(h + 1, 23)).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

export function DialogoNovaReuniao({
  aberto,
  dataInicial,
  aoFechar,
  aoCriar,
}: {
  aberto: boolean;
  /** "yyyy-MM-dd" do dia selecionado no calendário. */
  dataInicial: string;
  aoFechar: () => void;
  aoCriar: (criada: ReuniaoCalendarioCriada) => void;
}) {
  const [tipo, setTipo] = useState('Outra');
  const [titulo, setTitulo] = useState('');
  const [data, setData] = useState(dataInicial);
  const [inicio, setInicio] = useState('09:00');
  const [fim, setFim] = useState('10:00');
  const [obraId, setObraId] = useState('');
  const [repetir, setRepetir] = useState<'' | '1' | '2' | '3'>('');
  const [repetirAte, setRepetirAte] = useState('');
  const [convidados, setConvidados] = useState<string[]>([]);
  const [externos, setExternos] = useState<string[]>([]);
  const [emailDigitado, setEmailDigitado] = useState('');
  const [pauta, setPauta] = useState('');
  const [pessoas, setPessoas] = useState<ConvidavelReuniao[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    if (!aberto) return;
    setData(dataInicial);
    let ativo = true;
    void api.calendario.convidaveis().then((p) => ativo && setPessoas(p)).catch(() => undefined);
    void api.obras.listar().then((o) => ativo && setObras(o)).catch(() => undefined);
    return () => { ativo = false; };
  }, [aberto, dataInicial]);

  const opcoesPessoas = useMemo(
    () => pessoas.filter((p) => !convidados.includes(p.id)).map((p) => ({ id: p.id, rotulo: p.nome, descricao: p.email })),
    [pessoas, convidados],
  );

  function limpar() {
    setTipo('Outra');
    setTitulo('');
    setInicio('09:00');
    setFim('10:00');
    setObraId('');
    setRepetir('');
    setRepetirAte('');
    setConvidados([]);
    setExternos([]);
    setEmailDigitado('');
    setPauta('');
    setErro(null);
  }

  function fechar() {
    if (salvando) return;
    limpar();
    aoFechar();
  }

  function escolherTipo(rotulo: string) {
    const t = TIPOS.find((x) => x.rotulo === rotulo)!;
    setTipo(rotulo);
    if (t.titulo) setTitulo(t.titulo);
    if (t.pauta) setPauta(t.pauta);
  }

  function adicionarEmail() {
    const email = emailDigitado.trim();
    if (!email) return;
    if (!EMAIL_VALIDO.test(email)) {
      setErro(`E-mail inválido: ${email}`);
      return;
    }
    const usuario = pessoas.find((p) => p.email.toLowerCase() === email.toLowerCase());
    if (usuario) {
      if (!convidados.includes(usuario.id)) setConvidados((c) => [...c, usuario.id]);
    } else if (!externos.some((e) => e.toLowerCase() === email.toLowerCase())) {
      setExternos((e) => [...e, email]);
    }
    setEmailDigitado('');
    setErro(null);
  }

  async function criar() {
    if (!titulo.trim()) return setErro('Informe o título da reunião.');
    if (!data || !inicio || !fim) return setErro('Informe a data e os horários.');
    if (fim <= inicio) return setErro('O horário de fim deve ser depois do início.');
    if (repetir && !repetirAte) return setErro('Informe até quando a reunião se repete.');
    if (repetir && repetirAte <= data) return setErro('A data final da repetição deve ser depois do primeiro dia.');
    if (repetir && repetirAte > somarMeses(data, 12)) return setErro('A repetição pode ir no máximo até 1 ano.');
    try {
      setSalvando(true);
      setErro(null);
      const criada = await api.calendario.criarReuniao({
        titulo: titulo.trim(),
        pauta: pauta.trim() || null,
        inicio: `${data}T${inicio}:00`,
        fim: `${data}T${fim}:00`,
        convidadosUsuarioIds: convidados,
        emailsExternos: externos,
        obraId: obraId || null,
        repetir: repetir ? (Number(repetir) as 1 | 2 | 3) : null,
        repetirAte: repetir ? repetirAte : null,
      });
      limpar();
      aoCriar(criada);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar a reunião.');
    } finally {
      setSalvando(false);
    }
  }

  const diaPorExtenso = data
    ? new Date(`${data}T12:00:00`).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' })
    : '';

  return (
    <Dialog open={aberto} onOpenChange={(_, d) => !d.open && fechar()}>
      <DialogSurface style={{ maxWidth: 640 }}>
        <DialogBody>
          <DialogTitle>
            Nova reunião
            {diaPorExtenso && (
              <Text size={200} style={{ display: 'block', fontWeight: 400, color: designTokens.colorNeutralMedium }}>
                {diaPorExtenso.charAt(0).toUpperCase() + diaPorExtenso.slice(1)} · vai para o Teams e o Outlook
              </Text>
            )}
          </DialogTitle>
          <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {erro && (
              <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
                {erro}
              </FeedbackInline>
            )}

            <Field label="Tipo">
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {TIPOS.map((t) => (
                  <Button
                    key={t.rotulo}
                    size="small"
                    shape="circular"
                    appearance={tipo === t.rotulo ? 'primary' : 'outline'}
                    onClick={() => escolherTipo(t.rotulo)}
                  >
                    {t.rotulo}
                  </Button>
                ))}
              </div>
            </Field>

            <Field label="Título" required>
              <Input value={titulo} maxLength={200} onChange={(_, d) => setTitulo(d.value)} />
            </Field>

            <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0,1.4fr) minmax(0,1fr) minmax(0,1fr)', gap: 10 }}>
              <Field label="Data" required>
                <CampoData value={data} onChange={(_, d) => setData(d.value)} />
              </Field>
              <Field label="Início" required>
                <Input
                  type="time"
                  value={inicio}
                  onChange={(_, d) => {
                    setInicio(d.value);
                    if (d.value && (!fim || fim <= d.value)) setFim(somarUmaHora(d.value));
                  }}
                />
              </Field>
              <Field label="Fim" required>
                <Input type="time" value={fim} onChange={(_, d) => setFim(d.value)} />
              </Field>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: repetir ? 'minmax(0,1.6fr) minmax(0,1fr)' : '1fr', gap: 10 }}>
              <Field label="Repetir">
                <Select
                  value={repetir}
                  onChange={(_, d) => {
                    const valor = d.value as '' | '1' | '2' | '3';
                    setRepetir(valor);
                    if (valor && !repetirAte && data) setRepetirAte(somarMeses(data, 3));
                  }}
                >
                  {REPETICOES.map((r) => (
                    <option key={r.valor} value={r.valor}>
                      {r.rotulo(new Date(`${data || new Date().toISOString().slice(0, 10)}T12:00:00`))}
                    </option>
                  ))}
                </Select>
              </Field>
              {repetir && (
                <Field label="Até" required>
                  <CampoData value={repetirAte} onChange={(_, d) => setRepetirAte(d.value)} />
                </Field>
              )}
            </div>

            <Field label="Obra (opcional)">
              <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
                <option value="">Sem obra</option>
                {obras.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.nome}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Convidados" hint="Busque pelo nome ou digite o e-mail de quem é de fora e clique em Adicionar.">
              <div style={{ display: 'grid', gap: 6 }}>
                <SeletorPesquisavel
                  opcoes={opcoesPessoas}
                  valor=""
                  aoMudar={(id) => id && setConvidados((c) => [...c, id])}
                  placeholder="Buscar pessoa para convidar"
                  vazio="Ninguém encontrado. Para alguém de fora, digite o e-mail abaixo."
                />
                <div style={{ display: 'flex', gap: 6 }}>
                  <Input
                    style={{ flex: 1, minWidth: 0 }}
                    type="email"
                    placeholder="E-mail de convidado externo"
                    value={emailDigitado}
                    onChange={(_, d) => setEmailDigitado(d.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        adicionarEmail();
                      }
                    }}
                  />
                  <Button onClick={adicionarEmail} disabled={!emailDigitado.trim()}>
                    Adicionar
                  </Button>
                </div>
                {(convidados.length > 0 || externos.length > 0) && (
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    {convidados.map((id) => {
                      const nome = pessoas.find((p) => p.id === id)?.nome ?? id;
                      return (
                        <Button
                          key={id}
                          size="small"
                          shape="circular"
                          appearance="outline"
                          icon={<Dismiss16Regular />}
                          iconPosition="after"
                          aria-label={`Remover ${nome}`}
                          onClick={() => setConvidados((c) => c.filter((x) => x !== id))}
                        >
                          {nome}
                        </Button>
                      );
                    })}
                    {externos.map((email) => (
                      <Button
                        key={email}
                        size="small"
                        shape="circular"
                        appearance="subtle"
                        icon={<Dismiss16Regular />}
                        iconPosition="after"
                        aria-label={`Remover ${email}`}
                        onClick={() => setExternos((x) => x.filter((e) => e !== email))}
                      >
                        <Mail16Regular style={{ marginRight: 4, verticalAlign: 'middle' }} />
                        {email}
                      </Button>
                    ))}
                  </div>
                )}
              </div>
            </Field>

            <Field label="Pauta">
              <Textarea value={pauta} maxLength={4000} rows={4} resize="vertical" onChange={(_, d) => setPauta(d.value)} />
            </Field>

            <div
              style={{
                display: 'flex',
                gap: 10,
                alignItems: 'center',
                background: '#f1f1fb',
                border: '1px solid #d9daf3',
                borderRadius: 8,
                padding: '10px 12px',
              }}
            >
              <Video24Regular style={{ color: '#5b5fc7', flex: 'none' }} />
              <div>
                <Text weight="semibold" style={{ display: 'block' }}>
                  Reunião online do Teams
                </Text>
                <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                  O link é gerado automaticamente e vai no convite.
                </Text>
              </div>
            </div>
            <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
              Ao criar, a reunião já vai para o Teams: você e os convidados recebem o convite por e-mail. Convidados externos recebem o título,
              o horário, a pauta e o link.
            </Text>
          </DialogContent>
          <DialogActions>
            <Button onClick={fechar} disabled={salvando}>
              Cancelar
            </Button>
            <Button appearance="primary" onClick={() => void criar()} disabled={salvando}>
              {salvando ? 'Criando...' : 'Criar reunião'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
