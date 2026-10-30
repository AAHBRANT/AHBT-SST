import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
// A grade mensal é compartilhada com o planejamento de DDS.
// oxlint-disable-next-line no-restricted-imports
import { makeStyles, shorthands, tokens } from '@fluentui/react-components';
import { chaveDia, dataDoDia } from '../../lib/datasCalendario';
import {
  ArrowLeft24Regular,
  ArrowRight24Regular,
  CalendarLtr24Regular,
  Location24Regular,
  Video24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import {
  api,
  severidadeAlertaLabel,
  tipoAlertaLabel,
  type Calendario,
  type EventoGraphCalendario,
  type EventoSstCalendario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { Button, CalendarioMensal, designTokens, FeedbackInline, StatusChip, Text, type Tom } from '@ui';

const useStyles = makeStyles({
  chipEvento: {
    fontSize: '11px',
    lineHeight: '14px',
    padding: '2px 4px',
    borderRadius: '4px',
    whiteSpace: 'nowrap',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    color: tokens.colorNeutralForegroundStaticInverted,
  },
  maisEventos: {
    fontSize: '11px',
    color: tokens.colorNeutralForeground3,
  },
  legenda: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '16px',
    alignItems: 'center',
    marginBottom: '12px',
  },
  legendaItem: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    fontSize: '12px',
    color: tokens.colorNeutralForeground3,
  },
  bolinha: {
    width: '10px',
    height: '10px',
    borderRadius: '999px',
    display: 'inline-block',
  },
  painelDia: {
    marginTop: '16px',
  },
  itemEvento: {
    ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
    ...shorthands.borderRadius('8px'),
    padding: '12px',
    marginBottom: '8px',
  },
});

const CorGraph = designTokens.colorPrimary;
const CorSeveridade: Record<number, string> = {
  1: designTokens.colorInfo,
  2: designTokens.colorWarning,
  3: designTokens.colorAlert,
};
// Mesmos 3 níveis de CorSeveridade, como Tom de StatusChip — usado só no painel do dia selecionado
// (item 5 do Guia de conversão); o "chip" dentro da célula da grade continua com a cor sólida acima,
// ele é um marcador compacto de espaço mínimo, não um indicador de status no sentido do template.
const TomSeveridade: Record<number, Tom> = {
  1: 'info',
  2: 'atencao',
  3: 'alerta',
};

interface EventoDoDia {
  chave: string;
  data: Date;
  cor: string;
  rotulo: string;
  origem: 'graph' | 'sst';
  graph?: EventoGraphCalendario;
  sst?: EventoSstCalendario;
}

function inicioDaSemana(data: Date): Date {
  const d = new Date(data);
  d.setDate(d.getDate() - d.getDay());
  d.setHours(0, 0, 0, 0);
  return d;
}

// Reformulação pedida pelo usuário (2026-08-29): "quero o calendário dentro do aplicativo, tem que
// ser o Teams" — grade mensal combinando os eventos reais do Outlook/Teams (Microsoft Graph) com os
// vencimentos que o Motor de Alertas já gera para o usuário logado (ver ObterCalendarioQuery). Sem
// biblioteca de calendário (nenhuma no projeto) — grade 7x6 construída com Date nativo, mesmo
// princípio de cálculo de data já usado em outras telas (ex.: PtDashboardTab).
export function CalendarioPage() {
  const estilos = usePageStyles();
  const estilosLocais = useStyles();
  const navigate = useNavigate();

  const [mesAtual, setMesAtual] = useState(() => {
    const hoje = new Date();
    return new Date(hoje.getFullYear(), hoje.getMonth(), 1);
  });
  const [diaSelecionado, setDiaSelecionado] = useState<string>(() => chaveDia(new Date()));
  const [calendario, setCalendario] = useState<Calendario | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const primeiroDiaGrade = useMemo(() => inicioDaSemana(mesAtual), [mesAtual]);
  const ultimoDiaGrade = useMemo(() => {
    const ultimoDoMes = new Date(mesAtual.getFullYear(), mesAtual.getMonth() + 1, 0);
    const fim = inicioDaSemana(ultimoDoMes);
    fim.setDate(fim.getDate() + 6);
    fim.setHours(23, 59, 59, 999);
    return fim;
  }, [mesAtual]);

  useEffect(() => {
    let ativo = true;
    (async () => {
      try {
        setCarregando(true);
        setErro(null);
        setCalendario(null);
        const recebido = await api.calendario.obter(primeiroDiaGrade, ultimoDiaGrade);
        if (ativo) setCalendario(recebido);
      } catch (e) {
        if (ativo) setErro(e instanceof Error ? e.message : 'Falha ao carregar o calendário.');
      } finally {
        if (ativo) setCarregando(false);
      }
    })();
    return () => { ativo = false; };
  }, [primeiroDiaGrade, ultimoDiaGrade]);

  const eventosPorDia = useMemo(() => {
    const mapa = new Map<string, EventoDoDia[]>();
    if (!calendario) return mapa;

    for (const evento of calendario.eventosGraph) {
      const data = new Date(evento.inicio);
      const chave = chaveDia(data);
      const lista = mapa.get(chave) ?? [];
      lista.push({ chave: `graph-${evento.graphEventId}`, data, cor: CorGraph, rotulo: evento.assunto, origem: 'graph', graph: evento });
      mapa.set(chave, lista);
    }
    for (const evento of calendario.eventosSst) {
      const data = new Date(evento.data);
      const chave = chaveDia(data);
      const lista = mapa.get(chave) ?? [];
      lista.push({
        chave: `sst-${evento.alertaId}`,
        data,
        cor: CorSeveridade[evento.severidade] ?? designTokens.colorInfo,
        rotulo: evento.titulo,
        origem: 'sst',
        sst: evento,
      });
      mapa.set(chave, lista);
    }
    return mapa;
  }, [calendario]);

  const eventosDoDiaSelecionado = (eventosPorDia.get(diaSelecionado) ?? []).sort(
    (a, b) => a.data.getTime() - b.data.getTime(),
  );

  const nomeMes = mesAtual.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });

  return (
    <div>
      <div className={estilos.toolbar}>
        <Text size={500} weight="semibold">
          <CalendarLtr24Regular style={{ verticalAlign: 'middle', marginRight: 8 }} />
          {nomeMes.charAt(0).toUpperCase() + nomeMes.slice(1)}
        </Text>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button
            appearance="subtle"
            icon={<ArrowLeft24Regular />}
            onClick={() => setMesAtual((m) => new Date(m.getFullYear(), m.getMonth() - 1, 1))}
            aria-label="Mês anterior"
          />
          <Button
            appearance="secondary"
            onClick={() => {
              const hoje = new Date();
              setMesAtual(new Date(hoje.getFullYear(), hoje.getMonth(), 1));
              setDiaSelecionado(chaveDia(hoje));
            }}
          >
            Hoje
          </Button>
          <Button
            appearance="subtle"
            icon={<ArrowRight24Regular />}
            onClick={() => setMesAtual((m) => new Date(m.getFullYear(), m.getMonth() + 1, 1))}
            aria-label="Próximo mês"
          />
        </div>
      </div>

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {calendario && !calendario.usuarioIdentificado && (
        <div className={estilos.card} style={{ marginBottom: 16 }}>
          <Text>
            <Warning24Regular style={{ verticalAlign: 'middle', marginRight: 8, color: designTokens.colorWarning }} />
            Não foi possível identificar seu usuário do Teams nesta sessão — faça login novamente para ver seu
            calendário do Outlook/Teams e seus vencimentos do SST.
          </Text>
        </div>
      )}

      {calendario && calendario.usuarioIdentificado && !calendario.graphDisponivel && (
        <div className={estilos.card} style={{ marginBottom: 16 }}>
          <Text>
            <Warning24Regular style={{ verticalAlign: 'middle', marginRight: 8, color: designTokens.colorWarning }} />
            Não foi possível ler o calendário do Outlook/Teams agora — os vencimentos do SST abaixo continuam
            atualizados normalmente.
          </Text>
          {calendario.mensagemErroGraph && (
            <Text size={200} style={{ color: designTokens.colorNeutralMedium, display: 'block', marginTop: 4 }}>
              {calendario.mensagemErroGraph}
            </Text>
          )}
        </div>
      )}

      <div className={estilosLocais.legenda}>
        <div className={estilosLocais.legendaItem}>
          <span className={estilosLocais.bolinha} style={{ backgroundColor: CorGraph }} />
          Evento do Outlook/Teams
        </div>
        <div className={estilosLocais.legendaItem}>
          <span className={estilosLocais.bolinha} style={{ backgroundColor: designTokens.colorInfo }} />
          Vencimento SST — informativo
        </div>
        <div className={estilosLocais.legendaItem}>
          <span className={estilosLocais.bolinha} style={{ backgroundColor: designTokens.colorWarning }} />
          Vencimento SST — atenção
        </div>
        <div className={estilosLocais.legendaItem}>
          <span className={estilosLocais.bolinha} style={{ backgroundColor: designTokens.colorAlert }} />
          Vencimento SST — crítico
        </div>
      </div>

      <CalendarioMensal mes={mesAtual} selecionado={diaSelecionado} aoSelecionar={setDiaSelecionado}
        conteudoDia={(chave) => {
          const eventos = [...(eventosPorDia.get(chave) ?? [])].sort((a, b) => a.data.getTime() - b.data.getTime());
          return <>
            {eventos.slice(0, 3).map(evento => <span key={evento.chave} className={estilosLocais.chipEvento} style={{ backgroundColor: evento.cor }}>{evento.rotulo}</span>)}
            {eventos.length > 3 && <span className={estilosLocais.maisEventos}>+{eventos.length - 3} mais</span>}
          </>;
        }} />

      <div className={estilosLocais.painelDia}>
        <Text weight="semibold">
          {dataDoDia(diaSelecionado).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' })}
        </Text>

        {!carregando && eventosDoDiaSelecionado.length === 0 && (
          <Text style={{ display: 'block', marginTop: 8, color: designTokens.colorNeutralMedium }}>
            Nenhum evento ou vencimento neste dia.
          </Text>
        )}

        {eventosDoDiaSelecionado.map((evento) =>
          evento.origem === 'graph' && evento.graph ? (
            <div key={evento.chave} className={estilosLocais.itemEvento}>
              <Text weight="semibold">{evento.graph.assunto}</Text>
              <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginTop: 4, alignItems: 'center' }}>
                <Text size={200}>
                  {evento.graph.diaInteiro
                    ? 'Dia inteiro'
                    : `${new Date(evento.graph.inicio).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })} – ${new Date(evento.graph.fim).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}`}
                </Text>
                {evento.graph.local && (
                  <Text size={200}>
                    <Location24Regular style={{ verticalAlign: 'middle', marginRight: 4 }} />
                    {evento.graph.local}
                  </Text>
                )}
                {evento.graph.reuniaoOnline && evento.graph.linkReuniaoOnline && (
                  <Button
                    appearance="secondary"
                    size="small"
                    icon={<Video24Regular />}
                    as="a"
                    href={evento.graph.linkReuniaoOnline}
                    target="_blank"
                    rel="noreferrer"
                  >
                    Entrar na reunião
                  </Button>
                )}
              </div>
            </div>
          ) : evento.sst ? (
            <div
              key={evento.chave}
              className={estilosLocais.itemEvento}
              style={{ cursor: 'pointer' }}
              onClick={() => navigate('/alertas')}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
                <Text weight="semibold">{evento.sst.titulo}</Text>
                <StatusChip tom={TomSeveridade[evento.sst.severidade] ?? 'info'}>
                  {severidadeAlertaLabel[evento.sst.severidade]}
                </StatusChip>
              </div>
              <Text size={200} style={{ display: 'block', marginTop: 4 }}>
                {tipoAlertaLabel[evento.sst.tipo]}
                {evento.sst.descricao && ` — ${evento.sst.descricao}`}
              </Text>
            </div>
          ) : null,
        )}
      </div>
    </div>
  );
}
