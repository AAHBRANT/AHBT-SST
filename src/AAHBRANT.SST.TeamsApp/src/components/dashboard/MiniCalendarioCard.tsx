import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { makeStyles, mergeClasses, shorthands, tokens, Text } from '@fluentui/react-components';
import { CalendarLtr20Regular } from '@fluentui/react-icons';
import { api, SeveridadeAlerta, type Calendario } from '../../lib/api';
import { usePageStyles } from '../../pages/pageStyles';
import { designTokens } from '../../theme';

const useStyles = makeStyles({
  cartao: {
    width: '200px',
    flexShrink: 0,
    cursor: 'pointer',
    padding: '12px 14px',
    ...shorthands.transition('box-shadow', '0.15s'),
    ':hover': {
      boxShadow: '0 4px 14px rgba(0, 0, 0, 0.1)',
    },
  },
  cabecalho: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '8px',
  },
  grade: {
    display: 'grid',
    gridTemplateColumns: 'repeat(7, 1fr)',
    gap: '2px',
  },
  cabecalhoDiaSemana: {
    fontSize: '9px',
    fontWeight: 600,
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
  },
  celulaDia: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    height: '20px',
    fontSize: '10px',
    color: tokens.colorNeutralForeground3,
  },
  celulaDiaDoMes: {
    color: tokens.colorNeutralForeground1,
  },
  numeroDiaHoje: {
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    width: '15px',
    height: '15px',
    borderRadius: '999px',
    backgroundColor: designTokens.colorPrimary,
    color: '#FFFFFF',
  },
  bolinhaEvento: {
    width: '4px',
    height: '4px',
    borderRadius: '999px',
    marginTop: '1px',
  },
});

function chaveDia(data: Date): string {
  return data.toISOString().slice(0, 10);
}

function inicioDaSemana(data: Date): Date {
  const d = new Date(data);
  d.setDate(d.getDate() - d.getDay());
  d.setHours(0, 0, 0, 0);
  return d;
}

// Prévia do calendário (pedido do usuário, 31/08): mesma fonte de dados da tela cheia
// (CalendarioPage/ObterCalendarioQuery — eventos do Outlook/Teams + vencimentos do Motor de
// Alertas), só que reduzida a uma grade mensal compacta sem navegação nem lista de eventos —
// clicar no card leva para /calendario, onde o dia pode ser explorado em detalhe.
export function MiniCalendarioCard() {
  const estilosPagina = usePageStyles();
  const estilos = useStyles();
  const navigate = useNavigate();

  const [calendario, setCalendario] = useState<Calendario | null>(null);

  const hoje = useMemo(() => new Date(), []);
  const mesAtual = useMemo(() => new Date(hoje.getFullYear(), hoje.getMonth(), 1), [hoje]);
  const primeiroDiaGrade = useMemo(() => inicioDaSemana(mesAtual), [mesAtual]);
  const ultimoDiaGrade = useMemo(() => {
    const ultimoDoMes = new Date(mesAtual.getFullYear(), mesAtual.getMonth() + 1, 0);
    const fim = inicioDaSemana(ultimoDoMes);
    fim.setDate(fim.getDate() + 6);
    fim.setHours(23, 59, 59, 999);
    return fim;
  }, [mesAtual]);

  const diasDaGrade = useMemo(() => {
    const dias: Date[] = [];
    const cursor = new Date(primeiroDiaGrade);
    while (cursor <= ultimoDiaGrade) {
      dias.push(new Date(cursor));
      cursor.setDate(cursor.getDate() + 1);
    }
    return dias;
  }, [primeiroDiaGrade, ultimoDiaGrade]);

  useEffect(() => {
    api.calendario
      .obter(primeiroDiaGrade, ultimoDiaGrade)
      .then(setCalendario)
      .catch(() => setCalendario(null));
  }, [primeiroDiaGrade, ultimoDiaGrade]);

  // Um único indicador por dia (não dá pra listar eventos num card deste tamanho) — prioriza a
  // cor do vencimento SST mais grave do dia; sem vencimento, mas com evento do Outlook/Teams, usa
  // a cor de marca.
  const corPorDia = useMemo(() => {
    const mapa = new Map<string, string>();
    if (!calendario) return mapa;

    function prioridade(cor: string): number {
      if (cor === designTokens.colorAlert) return 3;
      if (cor === designTokens.colorWarning) return 2;
      return 1;
    }

    for (const evento of calendario.eventosGraph) {
      const chave = chaveDia(new Date(evento.inicio));
      mapa.set(chave, mapa.get(chave) ?? designTokens.colorPrimary);
    }
    for (const evento of calendario.eventosSst) {
      const chave = chaveDia(new Date(evento.data));
      const cor =
        evento.severidade === SeveridadeAlerta.Critico
          ? designTokens.colorAlert
          : evento.severidade === SeveridadeAlerta.Atencao
            ? designTokens.colorWarning
            : designTokens.colorInfo;
      const atual = mapa.get(chave);
      if (!atual || prioridade(cor) > prioridade(atual)) mapa.set(chave, cor);
    }
    return mapa;
  }, [calendario]);

  const hojeChave = chaveDia(hoje);
  const nomeMesAbreviado = mesAtual
    .toLocaleDateString('pt-BR', { month: 'short', year: 'numeric' })
    .replace('.', '');

  return (
    <div
      className={mergeClasses(estilosPagina.card, estilos.cartao)}
      onClick={() => navigate('/calendario')}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && navigate('/calendario')}
    >
      <div className={estilos.cabecalho}>
        <Text weight="semibold" size={200}>
          {nomeMesAbreviado.charAt(0).toUpperCase() + nomeMesAbreviado.slice(1)}
        </Text>
        <CalendarLtr20Regular style={{ color: designTokens.colorPrimary }} />
      </div>
      <div className={estilos.grade}>
        {['D', 'S', 'T', 'Q', 'Q', 'S', 'S'].map((letra, i) => (
          <div key={i} className={estilos.cabecalhoDiaSemana}>
            {letra}
          </div>
        ))}
        {diasDaGrade.map((dia) => {
          const chave = chaveDia(dia);
          const doMesAtual = dia.getMonth() === mesAtual.getMonth();
          const cor = corPorDia.get(chave);
          return (
            <div key={chave} className={mergeClasses(estilos.celulaDia, doMesAtual && estilos.celulaDiaDoMes)}>
              {chave === hojeChave ? (
                <span className={estilos.numeroDiaHoje}>{dia.getDate()}</span>
              ) : (
                dia.getDate()
              )}
              <span
                className={estilos.bolinhaEvento}
                style={{ backgroundColor: cor ?? 'transparent' }}
              />
            </div>
          );
        })}
      </div>
    </div>
  );
}
