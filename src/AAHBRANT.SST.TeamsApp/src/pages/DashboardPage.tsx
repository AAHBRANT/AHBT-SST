import { useEffect, useState, type ReactElement } from 'react';
import { Text, Title3 } from '@fluentui/react-components';
import {
  BuildingBank24Regular,
  Person24Regular,
  Warning24Regular,
  DocumentLock24Regular,
  ClipboardTaskListLtr24Regular,
} from '@fluentui/react-icons';
import { api, StatusApr, StatusPt, type Acidente, type RegistroHhtMensal } from '../lib/api';
import { CardGrid } from '../layout/AppShell';
import { usePageStyles } from './pageStyles';
import { designTokens } from '../theme';
import { TaxaGravidadeCard } from '../components/dashboard/TaxaGravidadeCard';

interface Kpi {
  rotulo: string;
  valor: number | null;
  icone: ReactElement;
}

const kpisIniciais: Kpi[] = [
  { rotulo: 'Obras ativas', valor: null, icone: <BuildingBank24Regular /> },
  { rotulo: 'Trabalhadores ativos', valor: null, icone: <Person24Regular /> },
  { rotulo: 'ASOs vencidos', valor: null, icone: <Warning24Regular /> },
  { rotulo: 'Treinamentos vencidos', valor: null, icone: <Warning24Regular /> },
  { rotulo: 'PTs abertas', valor: null, icone: <DocumentLock24Regular /> },
  { rotulo: 'APRs aguardando aprovação', valor: null, icone: <ClipboardTaskListLtr24Regular /> },
];

export function DashboardPage() {
  const estilos = usePageStyles();
  const [kpis, setKpis] = useState<Kpi[]>(kpisIniciais);
  const [acidentes, setAcidentes] = useState<Acidente[]>([]);
  const [registrosHht, setRegistrosHht] = useState<RegistroHhtMensal[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      api.obras.listar(),
      api.trabalhadores.listar(),
      api.asos.listar(),
      api.treinamentos.listar(),
      api.permissoesTrabalho.listar(),
      api.aprs.listar(),
      api.acidentes.listar(),
      api.registrosHht.listar(),
    ])
      .then(([obras, trabalhadores, asos, treinamentos, pts, aprs, acidentesLista, registrosHhtLista]) => {
        const hoje = new Date().toISOString().slice(0, 10);
        const asosVencidos = asos.filter((a) => a.dataValidade < hoje).length;
        const treinamentosVencidos = treinamentos.filter((t) => t.dataValidade < hoje).length;
        const ptsAbertas = pts.filter((pt) => pt.status !== StatusPt.Encerrada).length;
        const aprsAguardando = aprs.filter((apr) => apr.status === StatusApr.AguardandoAprovacao).length;

        setKpis([
          { rotulo: 'Obras ativas', valor: obras.length, icone: <BuildingBank24Regular /> },
          { rotulo: 'Trabalhadores ativos', valor: trabalhadores.length, icone: <Person24Regular /> },
          { rotulo: 'ASOs vencidos', valor: asosVencidos, icone: <Warning24Regular /> },
          { rotulo: 'Treinamentos vencidos', valor: treinamentosVencidos, icone: <Warning24Regular /> },
          { rotulo: 'PTs abertas', valor: ptsAbertas, icone: <DocumentLock24Regular /> },
          { rotulo: 'APRs aguardando aprovação', valor: aprsAguardando, icone: <ClipboardTaskListLtr24Regular /> },
        ]);
        setAcidentes(acidentesLista);
        setRegistrosHht(registrosHhtLista);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar indicadores.'));
  }, []);

  return (
    <div>
      {erro && (
        <Text className={estilos.erro}>
          Não foi possível conectar à API ({erro}). Verifique se o backend está rodando localmente.
        </Text>
      )}
      <CardGrid>
        {kpis.map((kpi) => (
          <div key={kpi.rotulo} className={estilos.card} style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <div style={{ color: designTokens.colorPrimary }}>{kpi.icone}</div>
            <Title3>{kpi.valor ?? '—'}</Title3>
            <Text style={{ color: designTokens.colorNeutralMedium }}>{kpi.rotulo}</Text>
          </div>
        ))}
        <TaxaGravidadeCard acidentes={acidentes} registrosHht={registrosHht} />
      </CardGrid>
    </div>
  );
}
