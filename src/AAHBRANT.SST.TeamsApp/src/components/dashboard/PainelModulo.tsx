import { useMemo, useState, type ReactElement, type ReactNode } from 'react';
import { Button, Card, KpiCard, Legenda, Select, type Tom } from '@ui';
import type { Obra } from '../../lib/api';
import { useDashboardStyles } from './dashboardStyles';

// Estrutura da tela Início aplicada ao dashboard de um módulo (pedido do usuário, 05/10): barra de
// filtros (período + obra) → faixa de indicadores → grade 2 para 1 (card largo + cards laterais) →
// grade de 2 colunas com os cards de baixo. Cada módulo só entrega os dados e os cards.

export type PeriodoPainel = 'semana' | 'mes' | 'ano' | 'tudo';

const PERIODOS: Array<{ valor: PeriodoPainel; rotulo: string }> = [
  { valor: 'semana', rotulo: 'Última semana' },
  { valor: 'mes', rotulo: 'Último mês' },
  { valor: 'ano', rotulo: 'Último ano' },
  { valor: 'tudo', rotulo: 'Tudo' },
];

export function hojeIsoPainel(): string {
  return new Date().toISOString().slice(0, 10);
}

// Mesmo critério de corte do Início (DashboardPage): período em relação a hoje, em datas ISO.
export function usePainelFiltros() {
  const [periodo, setPeriodo] = useState<PeriodoPainel>('tudo');
  const [obraId, setObraId] = useState('');
  const inicioPeriodoISO = useMemo(() => {
    if (periodo === 'tudo') return null;
    const data = new Date(hojeIsoPainel());
    if (periodo === 'semana') data.setDate(data.getDate() - 7);
    if (periodo === 'mes') data.setMonth(data.getMonth() - 1);
    if (periodo === 'ano') data.setFullYear(data.getFullYear() - 1);
    return data.toISOString().slice(0, 10);
  }, [periodo]);
  const noPeriodo = (dataISO?: string | null) => !!dataISO && (!inicioPeriodoISO || dataISO >= inicioPeriodoISO);
  return { periodo, setPeriodo, obraId, setObraId, noPeriodo };
}

export interface KpiPainel {
  rotulo: string;
  valor: number | string;
  tom: Tom;
  icone: ReactElement;
  aoClicar?: () => void;
}

export interface PainelModuloProps {
  obras: Obra[];
  periodo: PeriodoPainel;
  aoMudarPeriodo: (p: PeriodoPainel) => void;
  obraId: string;
  aoMudarObra: (id: string) => void;
  kpis: KpiPainel[];
  largo: ReactNode;
  laterais: ReactNode[];
  inferiores: ReactNode[];
  erro?: ReactNode;
  // Módulos sem datas de movimento (ex.: Terceirizados) escondem o filtro de período.
  semPeriodo?: boolean;
}

export function PainelModulo({ obras, periodo, aoMudarPeriodo, obraId, aoMudarObra, kpis, largo, laterais, inferiores, erro, semPeriodo }: PainelModuloProps) {
  const estilos = useDashboardStyles();
  return (
    <div>
      {erro}

      <div className={estilos.barraFiltrosDashboard} style={{ justifyContent: 'flex-end' }}>
        <div className={estilos.filtrosDireita}>
          {!semPeriodo && (
          <div className={estilos.grupoPeriodos} role="group" aria-label="Filtrar indicadores por período">
            {PERIODOS.map((p) => (
              <Button
                key={p.valor}
                appearance="subtle"
                className={periodo === p.valor ? estilos.botaoPeriodoSelecionado : estilos.botaoPeriodo}
                onClick={() => aoMudarPeriodo(p.valor)}
              >
                {p.rotulo}
              </Button>
            ))}
          </div>
          )}
          <label className={estilos.filtroObra}>
            <span style={{ fontWeight: 600, whiteSpace: 'nowrap' }}>Obra</span>
            <Select value={obraId} onChange={(_, d) => aoMudarObra(d.value)} aria-label="Filtrar indicadores por obra">
              <option value="">Todas as obras</option>
              {obras.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.nome}
                </option>
              ))}
            </Select>
          </label>
        </div>
      </div>

      <div className={estilos.linhaKpisCalendario}>
        <div className={estilos.faixaKpis}>
          {kpis.map((k, i) => (
            <KpiCard
              key={k.rotulo}
              rotulo={k.rotulo}
              valor={k.valor}
              tom={k.tom}
              icone={k.icone}
              indice={i}
              onClick={k.aoClicar}
              ariaLabel={k.aoClicar ? `Abrir ${k.rotulo}` : undefined}
            />
          ))}
        </div>
      </div>

      <div className={estilos.gradeDoisParaUm}>
        {largo}
        <div className={estilos.colunaDireita}>{laterais}</div>
      </div>

      <div className={estilos.gradeDoisColunas}>{inferiores}</div>
    </div>
  );
}

export interface ItemFeedPainel {
  id: string;
  titulo: string;
  meta?: string;
  hora?: string;
  icone: ReactElement;
  variante: 'bom' | 'info' | 'atencao' | 'alerta';
}

// Card de lista no estilo "Próximos vencimentos" / "Atividade recente" do Início.
export function FeedCardPainel({ titulo, subtitulo, itens, vazio }: { titulo: string; subtitulo: string; itens: ItemFeedPainel[]; vazio: string }) {
  const estilos = useDashboardStyles();
  const classe: Record<ItemFeedPainel['variante'], string> = {
    bom: estilos.feedIconeBom,
    info: estilos.feedIconeInfo,
    atencao: estilos.feedIconeAtencao,
    alerta: estilos.feedIconeAlerta,
  };
  return (
    <Card titulo={titulo} subtitulo={subtitulo}>
      <div className={estilos.feed}>
        {itens.map((item) => (
          <div key={item.id} className={estilos.feedItem}>
            <div className={`${estilos.feedIcone} ${classe[item.variante]}`}>{item.icone}</div>
            <div className={estilos.feedCorpo}>
              <div className={estilos.feedTitulo}>{item.titulo}</div>
              {item.meta && <div className={estilos.feedMeta}>{item.meta}</div>}
            </div>
            {item.hora && <span className={estilos.feedHora}>{item.hora}</span>}
          </div>
        ))}
        {itens.length === 0 && <Legenda>{vazio}</Legenda>}
      </div>
    </Card>
  );
}

export function dataCurtaPainel(dataISO: string): string {
  const [a, m, d] = dataISO.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

// Últimos 6 meses terminando no mês atual, para o card largo de evolução.
export function ultimosMeses(qtd = 6): Array<{ chave: string; rotulo: string }> {
  const nomes = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
  const base = new Date();
  const saida: Array<{ chave: string; rotulo: string }> = [];
  for (let i = qtd - 1; i >= 0; i--) {
    const d = new Date(base.getFullYear(), base.getMonth() - i, 1);
    saida.push({ chave: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`, rotulo: nomes[d.getMonth()] });
  }
  return saida;
}
