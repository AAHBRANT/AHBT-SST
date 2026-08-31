import { useMemo } from 'react';
import { makeStyles, mergeClasses, Text } from '@fluentui/react-components';
import { designTokens } from '../../theme';
import { useDashboardStyles } from './dashboardStyles';

const NOMES_MESES = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

const DIAS_SEMANA = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];

export interface DiaComPrazo {
  dataISO: string;
  vencido: boolean;
}

interface MiniCalendarCardProps {
  prazos: DiaComPrazo[];
}

interface CelulaDia {
  dia: number;
  dataISO: string;
  ehHoje: boolean;
  temPrazo: boolean;
  temPrazoVencido: boolean;
}

const useEstilos = makeStyles({
  cabecalho: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '12px',
  },
  mes: {
    fontSize: '13px',
    fontWeight: 700,
  },
  grade: {
    display: 'grid',
    gridTemplateColumns: 'repeat(7, 1fr)',
    gap: '2px',
  },
  cabecalhoDiaSemana: {
    fontSize: '10.5px',
    fontWeight: 700,
    color: designTokens.colorNeutralMedium,
    textAlign: 'center',
    paddingBottom: '4px',
  },
  celulaVazia: {},
  celulaDia: {
    position: 'relative',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    height: '26px',
    borderRadius: '50%',
    fontSize: '12px',
    fontWeight: 600,
    color: designTokens.colorNeutralDark,
  },
  celulaHoje: {
    backgroundColor: designTokens.colorPrimary,
    color: designTokens.colorWhite,
    fontWeight: 800,
  },
  marcador: {
    position: 'absolute',
    bottom: '1px',
    width: '4px',
    height: '4px',
    borderRadius: '50%',
    backgroundColor: designTokens.colorWarning,
  },
  marcadorVencido: {
    backgroundColor: designTokens.colorAlert,
  },
  legenda: {
    display: 'flex',
    gap: '14px',
    marginTop: '12px',
    fontSize: '11px',
    color: designTokens.colorNeutralMedium,
  },
  legendaItem: {
    display: 'flex',
    alignItems: 'center',
    gap: '5px',
  },
  bolinhaLegenda: {
    width: '7px',
    height: '7px',
    borderRadius: '50%',
    flexShrink: 0,
  },
});

// Calendário mensal compacto (só leitura) — marca com um ponto os dias que têm prazo de alerta
// em aberto (ver DashboardPage: mesma fonte de dados do card "Próximos vencimentos"), pra dar uma
// visão de agenda do mês sem precisar abrir a tela de Alertas.
export function MiniCalendarCard({ prazos }: MiniCalendarCardProps) {
  const dashEstilos = useDashboardStyles();
  const estilos = useEstilos();

  const hoje = new Date();
  const hojeISO = hoje.toISOString().slice(0, 10);
  const ano = hoje.getFullYear();
  const mes = hoje.getMonth();

  const prazosPorDia = useMemo(() => {
    const mapa = new Map<string, boolean>();
    for (const prazo of prazos) {
      const vencidoAtual = mapa.get(prazo.dataISO);
      mapa.set(prazo.dataISO, vencidoAtual ? true : prazo.vencido);
    }
    return mapa;
  }, [prazos]);

  const celulas = useMemo(() => {
    const primeiroDiaSemana = new Date(ano, mes, 1).getDay();
    const totalDias = new Date(ano, mes + 1, 0).getDate();
    const lista: Array<CelulaDia | null> = [];

    for (let i = 0; i < primeiroDiaSemana; i += 1) {
      lista.push(null);
    }
    for (let dia = 1; dia <= totalDias; dia += 1) {
      const dataISO = `${ano}-${String(mes + 1).padStart(2, '0')}-${String(dia).padStart(2, '0')}`;
      lista.push({
        dia,
        dataISO,
        ehHoje: dataISO === hojeISO,
        temPrazo: prazosPorDia.has(dataISO),
        temPrazoVencido: prazosPorDia.get(dataISO) === true,
      });
    }
    return lista;
  }, [ano, mes, hojeISO, prazosPorDia]);

  return (
    <div className={dashEstilos.chartCard}>
      <Text className={dashEstilos.chartTitulo}>Calendário</Text>
      <div className={dashEstilos.chartSubtitulo}>Prazos de alertas em aberto no mês</div>

      <div className={estilos.cabecalho}>
        <span className={estilos.mes}>
          {NOMES_MESES[mes]} de {ano}
        </span>
      </div>

      <div className={estilos.grade}>
        {DIAS_SEMANA.map((letra, indice) => (
          <div key={`${letra}-${indice}`} className={estilos.cabecalhoDiaSemana}>
            {letra}
          </div>
        ))}
        {celulas.map((celula, indice) =>
          celula === null ? (
            <div key={`vazia-${indice}`} className={estilos.celulaVazia} />
          ) : (
            <div
              key={celula.dataISO}
              className={mergeClasses(estilos.celulaDia, celula.ehHoje && estilos.celulaHoje)}
              title={celula.dataISO}
            >
              {celula.dia}
              {celula.temPrazo && (
                <span
                  className={mergeClasses(estilos.marcador, celula.temPrazoVencido && estilos.marcadorVencido)}
                />
              )}
            </div>
          ),
        )}
      </div>

      <div className={estilos.legenda}>
        <div className={estilos.legendaItem}>
          <span className={estilos.bolinhaLegenda} style={{ backgroundColor: designTokens.colorWarning }} />
          Prazo no mês
        </div>
        <div className={estilos.legendaItem}>
          <span className={estilos.bolinhaLegenda} style={{ backgroundColor: designTokens.colorAlert }} />
          Prazo vencido
        </div>
      </div>
    </div>
  );
}
