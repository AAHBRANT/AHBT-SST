// Re-export dos gráficos Recharts (spec §1.6). Os quatro componentes ficam em
// src/components/dashboard/charts/ até a Onda 3 — aqui só expomos a porta ui/.
export { usePaletaGraficos, type PaletaGraficos } from './paleta';
export { RankingBarChart, type ItemRanking } from '../../components/dashboard/charts/RankingBarChart';
export { StatusDonutChart, type FatiaDonut } from '../../components/dashboard/charts/StatusDonutChart';
export { TrendBarChart, type PontoTendencia } from '../../components/dashboard/charts/TrendBarChart';
export { TrendLineChart } from '../../components/dashboard/charts/TrendLineChart';
