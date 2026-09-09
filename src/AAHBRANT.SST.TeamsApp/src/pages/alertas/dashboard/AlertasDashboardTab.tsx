import { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Field,
  FeedbackInline,
  KpiCard,
  Legenda,
  RankingBarChart,
  Select,
  StatusChip,
  StatusDonutChart,
  Text,
  usePaletaGraficos,
  type FatiaDonut,
  type ItemRanking,
  type Tom,
} from '@ui';
import {
  CheckmarkCircle24Regular,
  DismissCircle24Regular,
  PlayCircle24Regular,
  Alert24Regular,
  ArrowUpload24Regular,
  EyeOff24Regular,
} from '@fluentui/react-icons';
import {
  api,
  categoriaAlertaRotulo,
  severidadeAlertaLabel,
  statusAlertaLabel,
  StatusAlerta,
  SeveridadeAlerta,
  type Alerta,
  type Obra,
} from '../../../lib/api';

// Mesmo mapa de tons de AlertasListaTab.tsx/AlertasConfiguracaoTab.tsx (Guia de conversão item 5) —
// as três telas do módulo leem severidade igual (status não vira StatusChip nesta tela, só texto
// em Legenda no painel "Alertas mais urgentes").
const tomPorSeveridade: Record<number, Tom> = {
  [SeveridadeAlerta.Info]: 'info',
  [SeveridadeAlerta.Atencao]: 'atencao',
  [SeveridadeAlerta.Critico]: 'alerta',
};

// Onda 2 Task 16 (camada ui/): mesmo formato de RiscosDashboardTab.tsx (Task 13)/AprDashboardTab.tsx
// (Task 11) — KpiCard com tom, gráficos com cores de usePaletaGraficos, grade CSS Grid simples
// (spec §4.4). "Escalonados" usava colorPrimary (vinho) no KPI original — remapeado para tom
// "alerta" porque status nunca é vinho (spec §1.1: vinho é ação/marca, nunca estado); "Abertos"
// desceu para "atencao" para não colidir com "Escalonados", que é o mais urgente dos dois (já
// passou da régua normal de tratamento). O painel "Alertas mais urgentes" (motorPainel) vira Card +
// StatusChip + Legenda, mesmo padrão de RiscosCriticosPanel.tsx/AprVencidaPanel.tsx.
export function AlertasDashboardTab() {
  const paleta = usePaletaGraficos();

  const [obras, setObras] = useState<Obra[]>([]);
  const [alertas, setAlertas] = useState<Alerta[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [processandoId, setProcessandoId] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [categoriaFiltro, setCategoriaFiltro] = useState('');
  const [severidadeFiltro, setSeveridadeFiltro] = useState('');

  async function carregar() {
    try {
      setErro(null);
      const [obrasResp, alertasResp] = await Promise.all([api.obras.listar(), api.alertas.listar({})]);
      setObras(obrasResp);
      setAlertas(alertasResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de alertas.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const categorias = useMemo(() => {
    const vistas = new Set(alertas.map((a) => a.entidadeOrigemTipo));
    return [...vistas].sort((a, b) => categoriaAlertaRotulo(a).localeCompare(categoriaAlertaRotulo(b)));
  }, [alertas]);

  const alertasFiltrados = useMemo(
    () =>
      alertas.filter(
        (a) =>
          (obraId === '' || a.obraId === obraId) &&
          (categoriaFiltro === '' || a.entidadeOrigemTipo === categoriaFiltro) &&
          (severidadeFiltro === '' || a.severidade === Number(severidadeFiltro)),
      ),
    [alertas, obraId, categoriaFiltro, severidadeFiltro],
  );

  const abertos = alertasFiltrados.filter((a) => a.status === StatusAlerta.Aberto).length;
  const emTratamento = alertasFiltrados.filter((a) => a.status === StatusAlerta.EmTratamento).length;
  const escalonados = alertasFiltrados.filter((a) => a.status === StatusAlerta.Escalonado).length;
  const resolvidos = alertasFiltrados.filter((a) => a.status === StatusAlerta.Resolvido).length;
  const ignorados = alertasFiltrados.filter((a) => a.status === StatusAlerta.Ignorado).length;

  const alertasAtivos = useMemo(
    () =>
      alertasFiltrados.filter(
        (a) =>
          a.status === StatusAlerta.Aberto ||
          a.status === StatusAlerta.EmTratamento ||
          a.status === StatusAlerta.Escalonado,
      ),
    [alertasFiltrados],
  );

  const severidadeDados: FatiaDonut[] = [
    { rotulo: 'Crítico', valor: alertasAtivos.filter((a) => a.severidade === SeveridadeAlerta.Critico).length, cor: paleta.alerta },
    { rotulo: 'Atenção', valor: alertasAtivos.filter((a) => a.severidade === SeveridadeAlerta.Atencao).length, cor: paleta.atencao },
    { rotulo: 'Informativo', valor: alertasAtivos.filter((a) => a.severidade === SeveridadeAlerta.Info).length, cor: paleta.info },
  ];

  const categoriaDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const alerta of alertasAtivos) {
      contagem.set(alerta.entidadeOrigemTipo, (contagem.get(alerta.entidadeOrigemTipo) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({ rotulo: categoriaAlertaRotulo(tipo), valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor);
  }, [alertasAtivos, paleta.marca]);

  const maisUrgentes = useMemo(
    () =>
      [...alertasAtivos].sort((a, b) => {
        if (b.severidade !== a.severidade) return b.severidade - a.severidade;
        const prazoA = a.dataLimiteTratamento ? new Date(a.dataLimiteTratamento).getTime() : Infinity;
        const prazoB = b.dataLimiteTratamento ? new Date(b.dataLimiteTratamento).getTime() : Infinity;
        return prazoA - prazoB;
      }),
    [alertasAtivos],
  );

  async function executar(acao: (id: string) => Promise<void>, id: string, mensagemErro: string) {
    try {
      setErro(null);
      setProcessandoId(id);
      await acao(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : mensagemErro);
    } finally {
      setProcessandoId(null);
    }
  }

  return (
    <div>
      <div style={{ display: 'flex', gap: 16, marginBottom: 16, flexWrap: 'wrap' }}>
        <Field label="Obra">
          <Select value={obraId} onChange={(_, data) => setObraId(data.value)}>
            <option value="">Todas as obras</option>
            {obras.map((o) => (
              <option key={o.id} value={o.id}>
                {o.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Categoria">
          <Select value={categoriaFiltro} onChange={(_, data) => setCategoriaFiltro(data.value)}>
            <option value="">Todas as categorias</option>
            {categorias.map((tipo) => (
              <option key={tipo} value={tipo}>
                {categoriaAlertaRotulo(tipo)}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Severidade">
          <Select value={severidadeFiltro} onChange={(_, data) => setSeveridadeFiltro(data.value)}>
            <option value="">Todas as severidades</option>
            {Object.entries(severidadeAlertaLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, marginBottom: 16 }}>
        <KpiCard rotulo="Abertos" valor={abertos} tom="atencao" indice={0} icone={<Alert24Regular />} />
        <KpiCard rotulo="Em tratamento" valor={emTratamento} tom="info" indice={1} icone={<PlayCircle24Regular />} />
        <KpiCard rotulo="Escalonados" valor={escalonados} tom="alerta" indice={2} icone={<ArrowUpload24Regular />} />
        <KpiCard rotulo="Resolvidos" valor={resolvidos} tom="ok" indice={3} icone={<CheckmarkCircle24Regular />} />
        <KpiCard rotulo="Ignorados" valor={ignorados} tom="neutro" indice={4} icone={<EyeOff24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Severidade dos alertas ativos" subtitulo="Abertos, em tratamento e escalonados">
          <StatusDonutChart dados={severidadeDados} legendaCentral="alertas" />
        </Card>
        <Card titulo="Alertas ativos por categoria" subtitulo="Distribuição dos alertas ativos por módulo de origem">
          <RankingBarChart dados={categoriaDados} />
        </Card>
      </div>

      <Card titulo="Alertas mais urgentes">
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 12 }}>
          {maisUrgentes.slice(0, 10).map((alerta) => (
            <div
              key={alerta.id}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 12,
                padding: '10px 0',
                borderBottom: '1px solid var(--colorNeutralStroke2)',
              }}
            >
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <StatusChip tom={tomPorSeveridade[alerta.severidade] ?? 'neutro'}>
                    {severidadeAlertaLabel[alerta.severidade]}
                  </StatusChip>
                  <Text weight="semibold">{alerta.titulo}</Text>
                </div>
                <Legenda>
                  {categoriaAlertaRotulo(alerta.entidadeOrigemTipo)} · {statusAlertaLabel[alerta.status]}
                  {alerta.obraNome ? ` · ${alerta.obraNome}` : ''}
                  {alerta.dataLimiteTratamento ? ` · prazo ${alerta.dataLimiteTratamento.slice(0, 10)}` : ''}
                </Legenda>
              </div>
              <div style={{ display: 'flex', gap: 4 }}>
                {alerta.status === StatusAlerta.Aberto && (
                  <Button
                    appearance="subtle"
                    icon={<PlayCircle24Regular />}
                    title="Iniciar tratamento"
                    disabled={processandoId === alerta.id}
                    onClick={() => executar(api.alertas.iniciarTratamento, alerta.id, 'Falha ao iniciar tratamento.')}
                  />
                )}
                <Button
                  appearance="subtle"
                  icon={<CheckmarkCircle24Regular />}
                  title="Resolver"
                  disabled={processandoId === alerta.id}
                  onClick={() => executar(api.alertas.resolver, alerta.id, 'Falha ao resolver alerta.')}
                />
                <Button
                  appearance="subtle"
                  icon={<DismissCircle24Regular />}
                  title="Ignorar"
                  disabled={processandoId === alerta.id}
                  onClick={() => executar(api.alertas.ignorar, alerta.id, 'Falha ao ignorar alerta.')}
                />
              </div>
            </div>
          ))}
          {!carregando && maisUrgentes.length === 0 && (
            <Legenda>Nenhum alerta ativo para os filtros selecionados.</Legenda>
          )}
        </div>
      </Card>
    </div>
  );
}
